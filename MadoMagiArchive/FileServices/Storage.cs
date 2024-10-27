namespace MadoMagiArchive.FileServices
{
    public class StorageService(IConfiguration configuration)
    {
        public string StorageLocation => Path.GetFullPath((configuration["StorageLocation"] ?? "Files") + "/");
        public string UploadDirectory => configuration["UploadDirectory"] ?? "";
        public string ThumbDir => Path.Combine(StorageLocation, UploadDirectory, "thumbs");
    }
}
