using Microsoft.AspNetCore.StaticFiles;

namespace MadoMagiArchive.FileServices
{
    public partial class Utils
    {
        public static string GetContentType(string path)
        {
            var provider = new FileExtensionContentTypeProvider();
            if (!provider.TryGetContentType(path, out string? contentType))
                contentType = "application/octet-stream";
            return contentType;
        }

        public static string NormalizeDownloadFilename(string filename)
        {
            return filename
                .Replace("\\", "_")
                .Replace("/", "_")
                .Replace("?", "_")
                ;
        }
    }
}
