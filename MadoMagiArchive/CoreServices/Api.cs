using Microsoft.AspNetCore.Mvc;


namespace MadoMagiArchive.CoreServices.Api
{
    public class ApiResponse<T>(int code, string message, T? data = default)
    {
        public int Code { get; set; } = code;
        public string Message { get; set; } = message;
        public T? Data { get; set; } = data;

        public static ApiResponse<T> Success(T? data = default) => new(1, ApiResponseMessage.Success, data);

        public static implicit operator ApiResponse(ApiResponse<T> r) => new(r.Code, r.Message) { Data = r.Data };
    }

    public class ApiResponse(int code, string message)
    {
        public int Code { get; set; } = code;
        public string Message { get; set; } = message;
        public object? Data = null;

        public static ApiResponse Success = new(1, ApiResponseMessage.Success);
    }

    public static class ApiRawResponse
    {
        public static ObjectResult Success = new(ApiResponseMessage.Success) { StatusCode = 1 };
        public static ObjectResult NoReadPermission = new(ApiResponseMessage.NoReadPermission) { StatusCode = 403 };
        public static ObjectResult NoWritePermission = new(ApiResponseMessage.NoWritePermission) { StatusCode = 403 };
        public static ObjectResult NoDeletePermission = new(ApiResponseMessage.NoDeletePermission) { StatusCode = 403 };
        public static ObjectResult Forbidden = new(ApiResponseMessage.Forbidden) { StatusCode = 403 };
        public static ObjectResult NotFound = new(ApiResponseMessage.NotFound) { StatusCode = 404 };
        public static ObjectResult UnknownError = new(ApiResponseMessage.UnknownError) { StatusCode = 500 };
    }

    public static class ApiResponseMessage
    {
        public const string Success = "Success";
        public const string NoReadPermission = "You don't have permission to view this item";
        public const string NoWritePermission = "You don't have permission to change this item";
        public const string NoDeletePermission = "You are not allowed to delete this item";
        public const string Forbidden = "You are not allowed to perform this operation";
        public const string NotFound = "Item not found";
        public const string UnknownError = "Unknown error";
    }

    public static class HttpContextExtensions
    {
        public static async Task ReplyForbidden(this HttpContext context, string message = ApiResponseMessage.Forbidden)
        {
            context.Response.StatusCode = 403;
            context.Response.ContentType = "text/plain";
            await context.Response.WriteAsync(message);
        }
    }
}
