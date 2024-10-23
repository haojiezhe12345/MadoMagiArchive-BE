using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MadoMagiArchive.CoreServices;


namespace MadoMagiArchive.Controllers
{
    [Route("users")]
    [ApiController]
    [UseTablePermission(nameof(CoreDbContext.Users))]
    public class UserController(CoreDbContext dbContext, UserContext userContext, UserService userService) : ControllerBase
    {
        private async Task<bool> UserExists(int id) => await dbContext.Users.AsNoTracking().AnyAsync(e => e.Id == id);

        [HttpGet]
        [RequireTableReadPermission]
        public async Task<ActionResult<IEnumerable<UserItem>>> GetUsers()
        {
            return await dbContext.Users.ToListAsync();
        }

        [HttpGet("self")]
        public async Task<ActionResult<UserItem>> GetSelf()
        {
            return await userService.GetFullUserById(userContext.Id) ?? userContext.User;
        }

        [HttpGet("{id}")]
        [RequireTableReadPermission]
        public async Task<ActionResult<UserItem>> GetUser(int id)
        {
            var user = await userService.GetFullUserById(id);

            if (user == null) return ApiRawResponse.NotFound;
            if (!userContext.CanRead(user)) return ApiRawResponse.NoReadPermission;

            return user;
        }

        [HttpPost]
        [RequireTableWritePermission]
        public async Task<ActionResult<ApiResponse>> AddUser(UserItem user)
        {
            if (user.GetReadLevel() > userContext.ReadLevel || user.GetWriteLevel() > userContext.WriteLevel)
            {
                return new ApiResponse(-1, "You cannot add a user with an access level higher than yours");
            }

            user.Owner = userContext.Id;
            await dbContext.Users.AddAsync(user);
            await dbContext.SaveChangesAsync();

            return ApiResponse.Success;
        }

        [HttpPut]
        [RequireTableWritePermission]
        public async Task<ActionResult<ApiResponse<IEnumerable<int>>>> UpdateUser(UserUpdateDTO update)
        {
            if (update.AccessLevel != null)
            {
                var user = new UserItem() { AccessLevel = (uint)update.AccessLevel };
                if (user.GetReadLevel() > userContext.ReadLevel || user.GetWriteLevel() > userContext.WriteLevel)
                {
                    return new ApiResponse<IEnumerable<int>>(-1, "You cannot set a user's access level to a value higher than yours");
                }
            }

            var entities = await dbContext.Users
                .Where(x => update.Ids.Contains(x.Id))
                .Include(x => x.Settings)
                .ToListAsync();

            if (entities == null || entities.Count == 0) return ApiRawResponse.NotFound;

            List<int> updatedIds = [];

            foreach (var entity in entities)
            {
                if (!userContext.CanWrite(entity)) continue;

                if (update.AccessLevel != null)
                    entity.AccessLevel = (uint)update.AccessLevel;

                if (update.Permission != null)
                    entity.Permission = (uint)update.Permission;

                if (update.Settings != null)
                    foreach (var newSetting in update.Settings)
                    {
                        var oldSetting = entity.Settings.FirstOrDefault(x => x.Name == newSetting.Name);
                        if (oldSetting != null)
                        {
                            oldSetting.Value = newSetting.Value;
                        }
                        else
                        {
                            entity.Settings.Add(new()
                            {
                                Name = newSetting.Name,
                                Value = newSetting.Value
                            });
                        }
                    }

                updatedIds.Add(entity.Id);
            }

            await dbContext.SaveChangesAsync();

            return update.Ids.Count == updatedIds.Count
                ? ApiResponse<IEnumerable<int>>.Success()
                : new(-1, "One or more items cannot be updated", update.Ids.Except(updatedIds));
        }

        [HttpDelete("{id}")]
        [RequireTableDeletePermission]
        public async Task<ActionResult<ApiResponse>> DeleteUser(int id)
        {
            var user = await dbContext.Users.FindAsync(id);

            if (user == null) return ApiRawResponse.NotFound;
            if (!userContext.CanDelete(user)) return ApiRawResponse.NoDeletePermission;

            dbContext.Users.Remove(user);
            await dbContext.SaveChangesAsync();

            return ApiResponse.Success;
        }

        [HttpGet("anonymous")]
        [RequireTableReadPermission]
        public async Task<ActionResult<UserItem>> GetAnonymousUser() => await dbContext.Users.FindAsync(UserService.AnonymousUserId) ?? new() { Id = UserService.AnonymousUserId };

        [HttpPut("anonymous")]
        [RequireTableWritePermission]
        public async Task<ActionResult<ApiResponse>> UpdateAnonymousUser(UserItem user)
        {
            if (await UserExists(UserService.AnonymousUserId))
            {
                var result = await UpdateUser(new()
                {
                    Ids = [UserService.AnonymousUserId],
                    AccessLevel = user.AccessLevel,
                    Permission = user.Permission,
                    Settings = [],
                });

                if (result.Value != null) return new(result.Value);
                else if (result.Result != null) return new(result.Result);
                else return ApiRawResponse.UnknownError;
            }
            else
            {
                user.Id = UserService.AnonymousUserId;
                user.Settings = [];
                return await AddUser(user);
            }
        }
    }
}
