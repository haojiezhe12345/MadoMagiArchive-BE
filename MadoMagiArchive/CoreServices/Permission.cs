using MadoMagiArchive.CoreServices.User;
using MadoMagiArchive.CoreServices.Api;
using MadoMagiArchive.CoreServices.CoreModels;


namespace MadoMagiArchive.CoreServices.Permission
{
    public static class PermissionExtensions
    {
        public static byte GetReadPermission(this BasePermissionItem item) => new Permission(item.Permission).Read;
        public static byte GetWritePermission(this BasePermissionItem item) => new Permission(item.Permission).Write;
        public static byte GetDeletePermission(this BasePermissionItem item) => new Permission(item.Permission).Delete;

        public static byte GetReadLevel(this UserItem user) => new Permission(user.AccessLevel).Read;
        public static byte GetWriteLevel(this UserItem user) => new Permission(user.AccessLevel).Write;
        public static byte GetDeleteLevel(this UserItem user) => new Permission(user.AccessLevel).Delete;

        public static bool CanRead(this UserContext context, BasePermissionItem item) => context.ReadLevel >= item.GetReadPermission() || context.Id == item.Owner;
        public static bool CanWrite(this UserContext context, BasePermissionItem item) => context.WriteLevel >= item.GetWritePermission() || context.Id == item.Owner;
        public static bool CanDelete(this UserContext context, BasePermissionItem item) => context.DeleteLevel >= item.GetDeletePermission() || context.Id == item.Owner;

        public static async Task<bool> CanRead(this UserContext context, string table) => context.ReadLevel >= (await context.GetTablePermission(table)).GetReadPermission();
        public static async Task<bool> CanWrite(this UserContext context, string table) => context.WriteLevel >= (await context.GetTablePermission(table)).GetWritePermission();
        public static async Task<bool> CanDelete(this UserContext context, string table) => context.DeleteLevel >= (await context.GetTablePermission(table)).GetDeletePermission();
    }

    public class Permission
    {
        public uint _permission;
        public byte Read => (byte)((_permission & 0x00ff0000) / 0x10000);
        public byte Write => (byte)((_permission & 0x0000ff00) / 0x100);
        public byte Delete => (byte)(_permission & 0x000000ff);

        public Permission(byte read, byte write, byte delete)
        {
            _permission = (uint)(read * 0x10000 + write * 0x100 + delete);
        }

        public Permission(uint permission)
        {
            _permission = permission;
        }

        public uint ToUint() => _permission;
        public static implicit operator uint(Permission permission) => permission.ToUint();
    }

    [AttributeUsage(AttributeTargets.Class)]
    public class UseTablePermissionAttribute(string table) : Attribute
    {
        public string Table { get; } = table;
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class RequireTableReadPermissionAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Method)]
    public class RequireTableWritePermissionAttribute : Attribute;

    [AttributeUsage(AttributeTargets.Method)]
    public class RequireTableDeletePermissionAttribute : Attribute;

    public class PermissionCheckMiddleware(RequestDelegate next)
    {
        public async Task InvokeAsync(HttpContext context)
        {
            var userContext = context.RequestServices.GetRequiredService<UserContext>();
            var endpoint = context.GetEndpoint();

            var table = endpoint?.Metadata.GetMetadata<UseTablePermissionAttribute>()?.Table;

            if (table != null)
            {
                if (endpoint?.Metadata.GetMetadata<RequireTableReadPermissionAttribute>() != null && !await userContext.CanRead(table))
                {
                    await context.ReplyForbidden(ApiResponseMessage.NoReadPermission);
                    return;
                }
                if (endpoint?.Metadata.GetMetadata<RequireTableWritePermissionAttribute>() != null && !await userContext.CanWrite(table))
                {
                    await context.ReplyForbidden(ApiResponseMessage.NoWritePermission);
                    return;
                }
                if (endpoint?.Metadata.GetMetadata<RequireTableDeletePermissionAttribute>() != null && !await userContext.CanDelete(table))
                {
                    await context.ReplyForbidden(ApiResponseMessage.NoDeletePermission);
                    return;
                }
            }

            await next(context);
        }
    }

}
