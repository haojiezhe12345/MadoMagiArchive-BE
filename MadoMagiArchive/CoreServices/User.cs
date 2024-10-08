using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Net;

using MadoMagiArchive.CoreServices.CoreModels;
using MadoMagiArchive.CoreServices.Core;
using MadoMagiArchive.CoreServices.Permission;


namespace MadoMagiArchive.CoreServices.User
{
    public class UserContext(CoreDbContext coreDb)
    {
        public UserItem User = new() { Id = UserService.AnonymousUserId };
        public void SetUser(UserItem? user)
        {
            if (user != null) User = user;
        }

        public int Id => User.Id;
        public byte ReadLevel => User.GetReadLevel();
        public byte WriteLevel => User.GetWriteLevel();
        public byte DeleteLevel => User.GetDeleteLevel();

        public async Task<TablePermission> GetTablePermission(string table)
        {
            return await coreDb.TablePermissions.SingleOrDefaultAsync(x => x.Table == table) ?? new() { Table = table };
        }
    }

    public class UserService(IHttpClientFactory httpClientFactory, IMemoryCache memoryCache, IConfiguration configuration, CoreDbContext dbContext, ILogger<UserService> logger)
    {
        public Uri MadoHomuAPI_BaseUrl = new UriBuilder(configuration["MadoHomuAPI_BaseUrl"] ?? "localhost").Uri;

        public const int AnonymousUserId = -1;

        public async Task<int> GetUserIdByToken(string? token)
        {
            if (token == null) return AnonymousUserId;

            if (memoryCache.TryGetValue(token, out int id))
            {
                return id;
            }
            else
            {
                var client = httpClientFactory.CreateClient();
                client.DefaultRequestHeaders.Add("token", token);
                var requestUrl = new Uri(MadoHomuAPI_BaseUrl, "user/me");
                try
                {
                    var response = await client.GetAsync(requestUrl);

                    if (response.IsSuccessStatusCode)
                    {
                        var user = JsonSerializer.Deserialize<UserMe>(await response.Content.ReadAsStringAsync()) ?? throw new Exception($"Failed to parse JSON");
                        memoryCache.Set(token, user.id, TimeSpan.FromMinutes(10));
                        return user.id;
                    }
                    else if (response.StatusCode == HttpStatusCode.Unauthorized)
                    {
                        return AnonymousUserId;
                    }
                    else
                    {
                        throw new Exception($"Request returned with status {response.StatusCode}");
                    }
                }
                catch (Exception ex)
                {
                    logger.LogError(1000, ex, $"Error occurred while sending request to {requestUrl}");
                    return AnonymousUserId;
                }
            }
        }

        public async Task<UserItem?> GetUserById(int id)
        {
            return await dbContext.Users.SingleOrDefaultAsync(u => u.Id == id);
        }

        public async Task<UserItem?> GetFullUserById(int id)
        {
            return await dbContext.Users.Include(x => x.Settings).SingleOrDefaultAsync(u => u.Id == id);
        }

        public async Task<UserItem?> GetUserByToken(string? token)
        {
            return await GetUserById(await GetUserIdByToken(token));
        }
    }

    public class UserAuthMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext httpContext)
        {
            var userService = httpContext.RequestServices.GetRequiredService<UserService>();
            var userContext = httpContext.RequestServices.GetRequiredService<UserContext>();

            userContext.SetUser(
                await userService.GetUserByToken(
                    (string?)httpContext.Request.Headers["token"] ?? httpContext.Request.Cookies["token"]
                ));
            httpContext.Response.Headers["User-Id"] = userContext.Id.ToString();

            await next(httpContext);
        }
    }
}
