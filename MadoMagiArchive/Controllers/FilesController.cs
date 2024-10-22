using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
using SkiaSharp;

using MadoMagiArchive.DataServices.Data;
using MadoMagiArchive.DataServices.DataModels;
using MadoMagiArchive.CoreServices.Api;
using MadoMagiArchive.CoreServices.User;
using MadoMagiArchive.CoreServices.Permission;
using MadoMagiArchive.FileServices.Media;


namespace MadoMagiArchive.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [UseTablePermission(nameof(DataDbContext.Files))]
    public class FilesController(DataDbContext dbContext, UserContext userContext, IConfiguration configuration) : ControllerBase
    {
        public string StorageLocation => Path.GetFullPath(configuration["StorageLocation"] ?? "Files");
        public string UploadDirectory => configuration["UploadDirectory"] ?? "";

        public static string GetContentType(string path)
        {
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(path, out string? contentType))
                contentType = "application/octet-stream";
            return contentType;
        }

        [HttpGet]
        [RequireTableReadPermission]
        public async Task<ActionResult<IEnumerable<FileItem>>> GetFiles(int? fromId, DateTime? fromTime, string? type, int count = 30)
        {
            var query = dbContext.Files.Where(x => userContext.ReadLevel >= (x.Permission & 0x00ff0000) / 0x10000 || userContext.Id == x.Owner);

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
            var fileItem = await dbContext.Files.FindAsync(id);

            if (fileItem == null) return ApiRawResponse.NotFound;
            if (!userContext.CanRead(fileItem)) return ApiRawResponse.NoReadPermission;

            if (filename == null) return Redirect($"{id}/{Uri.EscapeDataString(fileItem.File)}");

            var file = Path.Combine(StorageLocation, fileItem.File);

            string? rangeHeader = Request.Headers["Range"];
            var rangeValue = !string.IsNullOrEmpty(rangeHeader) ? RangeHeaderValue.Parse(rangeHeader) : null;

            if (rangeValue == null)
            {
                return PhysicalFile(file, GetContentType(file), true);
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

                return new FileStreamResult(stream, GetContentType(file)) { EnableRangeProcessing = true };
            }
        }

        [HttpGet("{id}/thumb")]
        [RequireTableReadPermission]
        public async Task<IActionResult> GetFileThumb(int id)
        {
            var fileItem = await dbContext.Files.FindAsync(id);

            if (fileItem == null) return ApiRawResponse.NotFound;
            if (!userContext.CanRead(fileItem)) return ApiRawResponse.NoReadPermission;

            var thumbDir = Path.Combine(StorageLocation, UploadDirectory, "thumbs");
            var thumbFile = Path.Combine(thumbDir, $"tmb_{id}.jpg");
            var sourceFile = Path.Combine(StorageLocation, fileItem.File);

            const int thumbMaxWidth = 500;
            const int thumbMaxHeight = 200;

            var contentType = GetContentType(fileItem.File);

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
                var result = await Media.CreateVideoThumb(sourceFile, thumbFile, thumbMaxWidth, thumbMaxHeight);

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
            var file = await dbContext.Files
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
                    var type = GetContentType(file.FileName);
                    if (type == "application/octet-stream") return new ApiResponse<IEnumerable<int>>(-1, "Uploaded file type not supported");

                    var filePath = Path.Combine(UploadDirectory, Guid.NewGuid().ToString() + Path.GetExtension(file.FileName));
                    var fileFullPath = Path.Combine(StorageLocation, filePath);

                    Directory.CreateDirectory(Path.Combine(StorageLocation, UploadDirectory));

                    using var stream = System.IO.File.Create(fileFullPath);
                    await file.CopyToAsync(stream);

                    var fileEntity = await dbContext.Files.AddAsync(new()
                    {
                        File = filePath,
                        Size = file.Length,
                        Owner = userContext.Id,
                        Type = type,
                        //Permission = 0x00646464,
                    });

                    if (type.StartsWith("image"))
                    {
                        stream.Seek(0, SeekOrigin.Begin);
                        using var skImage = SKBitmap.Decode(stream);
                        if (skImage != null)
                        {
                            fileEntity.Entity.Width = skImage.Width;
                            fileEntity.Entity.Height = skImage.Height;
                        }
                    }

                    if (type.StartsWith("video"))
                    {
                        await stream.DisposeAsync();
                        var video = await Media.GetMediaInfo(fileFullPath);
                        if (video != null)
                        {
                            fileEntity.Entity.Width = video.Width;
                            fileEntity.Entity.Height = video.Height;
                            fileEntity.Entity.Duration = video.Duration;
                        }
                    }

                    await dbContext.SaveChangesAsync();

                    ids.Add(fileEntity.Entity.Id);
                }
            }

            return ApiResponse<IEnumerable<int>>.Success(ids);
        }

        [HttpPut]
        [RequireTableWritePermission]
        public async Task<ActionResult<ApiResponse<IEnumerable<int>>>> PutFileDetail(FilesUpdateDTO update)
        {
            var entities = await dbContext.Files
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
                    var newTags = await dbContext.Tags
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

            await dbContext.SaveChangesAsync();

            return update.Ids.Count == updatedIds.Count
                ? ApiResponse<IEnumerable<int>>.Success()
                : new(-1, "One or more items cannot be updated", update.Ids.Except(updatedIds));
        }

        [HttpDelete("{id}")]
        [RequireTableDeletePermission]
        public async Task<ActionResult<ApiResponse>> DeleteItem(int id)
        {
            var item = await dbContext.Files.FindAsync(id);

            if (item == null) return ApiRawResponse.NotFound;
            if (!userContext.CanDelete(item)) return ApiRawResponse.NoDeletePermission;

            dbContext.Files.Remove(item);
            await dbContext.SaveChangesAsync();

            return ApiResponse.Success;
        }
    }
}
