using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

using MadoMagiArchive.CoreServices.CoreModels;
using MadoMagiArchive.CoreServices.Permission;


namespace MadoMagiArchive.DataServices.DataModels
{
    [Index(nameof(File), IsUnique = true)]
    public class FileItem : BasePermissionItem
    {
        public FileItem()
        {
            _permission = new Permission(1, 20, 100);
        }
        public int Id { get; set; }
        public string? Type { get; set; }
        public bool? R18 { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Source { get; set; }
        public int? Width { get; set; }
        public int? Height { get; set; }
        public TimeSpan? Duration { get; set; }
        public required string File { get; set; }
        public required long Size { get; set; }

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public DateTime DateModified { get; set; } = DateTime.UtcNow;

        public List<Tag> Tags { get; } = [];
    }

    public class FilesUpdateDTO
    {
        public required List<int> Ids { get; set; }
        public string? Type { get; set; }
        public bool? R18 { get; set; }
        public string? Title { get; set; }
        public string? Description { get; set; }
        public string? Source { get; set; }
        public List<int>? TagsAdded { get; set; }
        public List<int>? TagsDeleted { get; set; }
        public uint? Permission { get; set; }
    }

    public class Tag : BasePermissionItem
    {
        public Tag()
        {
            _permission = new Permission(1, 20, 100);
        }
        public int Id { get; set; }
        public string? Type { get; set; }
        public int? ImageFile { get; set; }
        public string? Description { get; set; }
        public required List<TagName> Names { get; set; }
    }

    public class TagsUpdateDTO
    {
        public required List<int> Ids { get; set; }
        public string? Type { get; set; }
        public int? ImageFile { get; set; }
        public string? Description { get; set; }
        public List<TagName>? Names { get; set; }
        public uint? Permission { get; set; }
    }

    [Index(nameof(TagId), nameof(Lang), IsUnique = true)]
    public class TagName
    {
        [JsonIgnore]
        public int TagId { get; set; }
        [Key]
        public required string Name { get; set; }
        public string? Lang { get; set; }
    }

    [Index(nameof(FileId), nameof(TagId), IsUnique = true)]
    public class FileTag
    {
        public required int FileId { get; set; }
        public required int TagId { get; set; }
        public int? FileOrder { get; set; }
    }
}
