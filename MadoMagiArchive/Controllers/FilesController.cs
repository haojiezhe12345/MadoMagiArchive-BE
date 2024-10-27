using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using SkiaSharp;
using MadoMagiArchive.CoreServices;
using MadoMagiArchive.DataServices;
using MadoMagiArchive.FileServices;


namespace MadoMagiArchive.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [UseTablePermission(nameof(DataDbContext.Files))]
    public class FilesController(DataDbContext dataDb, UserContext userContext, StorageService storage) : ControllerBase
    {
        private string StorageLocation => storage.StorageLocation;
        private string UploadDirectory => storage.UploadDirectory;

        [HttpGet]
        [RequireTableReadPermission]
        public async Task<ActionResult<IEnumerable<FileItem>>> GetFiles(int? fromId, DateTime? fromTime, string? type, int count = 30)
        {
            var query = dataDb.Files.Where(x => userContext.ReadLevel >= (x.Permission & 0x00ff0000) / 0x10000 || userContext.Id == x.Owner);

            if (fromId != null) query = query.Where(x => x.Id <= fromId);
            if (fromTime != null) query = query.Where(x => x.DateCreated <= fromTime);
            if (type != null) query = query.Where(x => x.Type == type);

            return await query.OrderByDescending(x => x.Id).Take(count).ToListAsync();
        }

        [HttpGet("{id}")]
        [HttpGet("{id}/{filename}")]
        [RequireTableReadPermission]
        public async Task<IActionResult> GetFileContent(int id, string? filename)
        {
            var fileItem = await dataDb.Files.FindAsync(id);

            if (fileItem == null) return ApiRawResponse.NotFound;
            if (!userContext.CanRead(fileItem)) return ApiRawResponse.NoReadPermission;

            //if (filename == null) return Redirect($"{id}/{Uri.EscapeDataString(Utils.NormalizeDownloadFilename(fileItem.File))}");

            var file = Path.Combine(StorageLocation, fileItem.File);

            string? rangeHeader = Request.Headers.Range;
            var rangeValue = !string.IsNullOrEmpty(rangeHeader) ? RangeHeaderValue.Parse(rangeHeader) : null;

            if (rangeValue == null)
            {
                return PhysicalFile(file, Utils.GetContentType(file), true);
            }
            else
            {
                var fileSize = new FileInfo(file).Length;

                var from = rangeValue?.Ranges?.FirstOrDefault()?.From ?? 0;
                var to = rangeValue?.Ranges?.FirstOrDefault()?.To ?? fileSize - 1;

                var stream = new FileStream(file, FileMode.Open, FileAccess.Read);
                stream.Seek(from, SeekOrigin.Begin);

                Response.StatusCode = 206;
                Response.Headers.ContentRange = $"bytes {from}-{to}/{fileSize}";

                return new FileStreamResult(stream, Utils.GetContentType(file)) { EnableRangeProcessing = true };
            }
        }

        [HttpGet("{id}/thumb")]
        [RequireTableReadPermission]
        public async Task<IActionResult> GetFileThumb(int id)
        {
            var fileItem = await dataDb.Files.FindAsync(id);

            if (fileItem == null) return ApiRawResponse.NotFound;
            if (!userContext.CanRead(fileItem)) return ApiRawResponse.NoReadPermission;

            var thumbDir = Path.Combine(StorageLocation, UploadDirectory, "thumbs");
            var thumbFile = Path.Combine(thumbDir, $"tmb_{id}.jpg");
            var sourceFile = Path.Combine(StorageLocation, fileItem.File);

            const int thumbMaxWidth = 500;
            const int thumbMaxHeight = 200;

            var contentType = Utils.GetContentType(fileItem.File);

            if (contentType.StartsWith("image"))
            {
                if (System.IO.File.Exists(thumbFile)) return PhysicalFile(thumbFile, "image/jpeg", true);

                using var bmp = SKBitmap.Decode(sourceFile);

                var aspectRatio = (float)bmp.Width / bmp.Height;
                int width, height;
                if (aspectRatio > thumbMaxWidth / thumbMaxHeight)
                {
                    width = thumbMaxWidth;
                    height = (int)(thumbMaxWidth / aspectRatio);
                }
                else
                {
                    width = (int)(thumbMaxHeight * aspectRatio);
                    height = thumbMaxHeight;
                }

                Directory.CreateDirectory(thumbDir);
                using var thumbStream = System.IO.File.Create(thumbFile);

                bmp.Resize(new SKImageInfo(width, height), SKFilterQuality.Medium)
                    .Encode(SKEncodedImageFormat.Jpeg, 80)
                    .SaveTo(thumbStream);

                return PhysicalFile(thumbFile, "image/jpeg", true);
            }

            if (contentType.StartsWith("video"))
            {
                if (System.IO.File.Exists(thumbFile)) return PhysicalFile(thumbFile, "image/jpeg", true);

                Directory.CreateDirectory(thumbDir);
                var result = await FFmpeg.CreateVideoThumb(sourceFile, thumbFile, thumbMaxWidth, thumbMaxHeight);

                return result == null
                    ? Problem("Could not start ffmpeg")
                    : result == 0
                        ? PhysicalFile(thumbFile, "image/jpeg", true)
                        : Problem($"FFmpeg exited unexpectedly with code {result}");
            }

            else return NoContent();
        }

        [HttpGet("{id}/detail")]
        [RequireTableReadPermission]
        public async Task<ActionResult<FileItem>> GetFileDetail(int id)
        {
            var file = await dataDb.Files
                .Include(x => x.Tags)
                .ThenInclude(x => x.Names)
                .SingleOrDefaultAsync(x => x.Id == id);

            return file != null ? file : ApiRawResponse.NotFound;
        }

        [HttpPost]
        [RequireTableWritePermission]
        public async Task<ActionResult<ApiResponse<IEnumerable<int>>>> PostFiles(IEnumerable<IFormFile> files)
        {
            List<int> ids = [];

            if (!files.Any()) return new ApiResponse<IEnumerable<int>>(-1, "No files specified");

            foreach (var file in files)
            {
                if (file.Length > 0)
                {
                    var type = Utils.GetContentType(file.FileName);
                    if (type == "application/octet-stream") return new ApiResponse<IEnumerable<int>>(-1, "Uploaded file type not supported");

                    var filePath = Path.Combine(UploadDirectory, Guid.NewGuid().ToString() + Path.GetExtension(file.FileName));
                    var fileFullPath = Path.Combine(StorageLocation, filePath);

                    Directory.CreateDirectory(Path.Combine(StorageLocation, UploadDirectory));

                    using var stream = System.IO.File.Create(fileFullPath);
                    await file.CopyToAsync(stream);

                    var fileEntity = await dataDb.Files.AddAsync(new()
                    {
                        File = filePath,
                        Size = file.Length,
                        Owner = userContext.Id,
                        Type = type,
                        //Permission = 0x00646464,
                    });

                    if (fileEntity.Entity.IsImage())
                    {
                        fileEntity.Entity.AddImageInfo(stream);
                    }

                    if (fileEntity.Entity.IsVideo())
                    {
                        await stream.DisposeAsync();
                        await fileEntity.Entity.AddVideoInfo(fileFullPath);
                    }

                    await dataDb.SaveChangesAsync();

                    ids.Add(fileEntity.Entity.Id);
                }
            }

            return ApiResponse<IEnumerable<int>>.Success(ids);
        }

        [HttpPut]
        [RequireTableWritePermission]
        public async Task<ActionResult<ApiResponse<IEnumerable<int>>>> PutFileDetail(FilesUpdateDTO update)
        {
            var entities = await dataDb.Files
                  .Where(x => update.Ids.Contains(x.Id))
                  .Include(x => x.Tags)
                  .ToListAsync();

            if (entities == null || entities.Count == 0) return ApiRawResponse.NotFound;

            List<int> updatedIds = [];

            foreach (var entity in entities)
            {
                if (!userContext.CanWrite(entity)) continue;

                if (update.Type != null)
                    entity.Type = update.Type;

                if (update.R18 != null)
                    entity.R18 = update.R18;

                if (update.Title != null)
                    entity.Title = update.Title;

                if (update.Description != null)
                    entity.Description = update.Description;

                if (update.Source != null)
                    entity.Source = update.Source;

                if (update.TagsAdded != null)
                {
                    var newTags = await dataDb.Tags
                        .Where(t => update.TagsAdded.Contains(t.Id))
                        .ToListAsync();
                    entity.Tags.AddRange(newTags);
                }

                if (update.TagsDeleted != null)
                {
                    entity.Tags.RemoveAll(t => update.TagsDeleted.Contains(t.Id));
                }

                if (update.Permission != null)
                    entity.Permission = (uint)update.Permission;

                entity.DateModified = DateTime.UtcNow;

                updatedIds.Add(entity.Id);
            }

            await dataDb.SaveChangesAsync();

            return update.Ids.Count == updatedIds.Count
                ? ApiResponse<IEnumerable<int>>.Success()
                : new(-1, "One or more items cannot be updated", update.Ids.Except(updatedIds));
        }

        [HttpDelete("{id}")]
        [RequireTableDeletePermission]
        public async Task<ActionResult<ApiResponse>> DeleteItem(int id)
        {
            var item = await dataDb.Files.FindAsync(id);

            if (item == null) return ApiRawResponse.NotFound;
            if (!userContext.CanDelete(item)) return ApiRawResponse.NoDeletePermission;

            dataDb.Files.Remove(item);
            await dataDb.SaveChangesAsync();

            return ApiResponse.Success;
        }
    }
}
