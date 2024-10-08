using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

using MadoMagiArchive.DataServices.Data;
using MadoMagiArchive.DataServices.DataModels;
using MadoMagiArchive.CoreServices.Permission;
using MadoMagiArchive.CoreServices.User;
using MadoMagiArchive.CoreServices.Api;


namespace MadoMagiArchive.Controllers
{
    [Route("[controller]")]
    [ApiController]
    [UseTablePermission(nameof(DataDbContext.Tags))]
    public class TagsController(DataDbContext dbContext, UserContext userContext) : ControllerBase
    {
        [HttpGet]
        [RequireTableReadPermission]
        public async Task<ActionResult<IEnumerable<Tag>>> GetTags(string? type, string? searchKey, bool reverse, int page = 0, int pageSize = 20)
        {
            var query = dbContext.Tags.Where(x => userContext.ReadLevel >= (x.Permission & 0x00ff0000) / 0x10000 || userContext.Id == x.Owner);

            if (type != null) query = query.Where(x => x.Type == type);

            if (searchKey != null)
            {
                var tagNameQuery = dbContext.TagNames
                    .Where(x => x.Name.ToLower().Contains(searchKey.ToLower()))
                    .Select(x => x.TagId);

                var tagDescriptionQuery = dbContext.Tags
                    .Where(x => x.Description != null && x.Description.ToLower().Contains(searchKey.ToLower()))
                    .Select(x => x.Id);

                var tagIds = await tagNameQuery
                    .Union(tagDescriptionQuery)
                    .ToListAsync();

                query = query.Where(x => tagIds.Contains(x.Id));
            }

            if (reverse) query = query.OrderByDescending(x => x.Id);

            return await query.Include(x => x.Names).Skip(page * pageSize).Take(pageSize).ToListAsync();
        }

        [HttpGet("{id}")]
        [RequireTableReadPermission]
        public async Task<ActionResult<Tag>> GetTagItem(int id)
        {
            var tag = await dbContext.Tags.Include(x => x.Names).SingleOrDefaultAsync(x => x.Id == id);

            if (tag == null) return ApiRawResponse.NotFound;
            if (!userContext.CanRead(tag)) return ApiRawResponse.NoReadPermission;

            return tag;
        }

        [HttpPost]
        [RequireTableWritePermission]
        public async Task<ActionResult<ApiResponse>> PostTag(Tag tag)
        {
            tag.Id = 0;
            tag.Owner = userContext.Id;
            await dbContext.Tags.AddAsync(tag);

            await dbContext.SaveChangesAsync();

            return ApiResponse.Success;
        }

        [HttpPut]
        [RequireTableWritePermission]
        public async Task<ActionResult<ApiResponse<IEnumerable<int>>>> PutTags(TagsUpdateDTO update)
        {
            if (update.Ids.Count > 1 && update.Names != null) return new ApiResponse<IEnumerable<int>>(-1, "Cannot apply name updates to multiple tags");

            var entities = await dbContext.Tags
                .Where(x => update.Ids.Contains(x.Id))
                .Include(x => x.Names)
                .ToListAsync();

            if (entities == null || entities.Count == 0) return ApiRawResponse.NotFound;

            List<int> updatedIds = [];

            foreach (var entity in entities)
            {
                if (!userContext.CanWrite(entity)) continue;

                if (update.Type != null)
                    entity.Type = update.Type;

                if (update.ImageFile != null)
                    entity.ImageFile = update.ImageFile;

                if (update.Description != null)
                    entity.Description = update.Description;

                if (update.Names != null && update.Names.Count > 0)
                    entity.Names = update.Names;

                if (update.Permission != null)
                    entity.Permission = (uint)update.Permission;

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
            var item = await dbContext.Tags.FindAsync(id);

            if (item == null) return ApiRawResponse.NotFound;
            if (!userContext.CanDelete(item)) return ApiRawResponse.NoDeletePermission;

            dbContext.Tags.Remove(item);
            await dbContext.SaveChangesAsync();

            return ApiResponse.Success;
        }
    }
}
