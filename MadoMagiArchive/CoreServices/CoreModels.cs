using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;


namespace MadoMagiArchive.CoreServices.CoreModels
{
    public class BasePermissionItem
    {
        protected virtual uint _permission { get; set; } = 0x00016464;
        public uint Permission
        {
            get => _permission;
            set => _permission = value & 0x00ffffff;
        }
        public int? Owner { get; set; }
    }

    public class UserItem : BasePermissionItem
    {
        public int Id { get; set; }

        protected virtual uint _accessLevel { get; set; } = 0x00010101;
        public uint AccessLevel
        {
            get => _accessLevel;
            set => _accessLevel = value & 0x00ffffff;
        }

        public List<UserSetting> Settings { get; set; } = [];
    }

    public class UserUpdateDTO
    {
        public required List<int> Ids { get; set; }
        public uint? AccessLevel { get; set; }
        public uint? Permission { get; set; }
        public List<Setting>? Settings { get; set; }
    }

    public class Setting
    {
        [Key]
        public required string Name { get; set; }
        public required string Value { get; set; }
    }

    [Index(nameof(UserId), nameof(Name), IsUnique = true)]
    public class UserSetting
    {
        [JsonIgnore]
        public int Id { get; set; }
        [JsonIgnore]
        public int UserId { get; set; }
        public required string Name { get; set; }
        public required string Value { get; set; }
    }

    public class TablePermission : BasePermissionItem
    {
        [Key]
        public required string Table { get; set; }
    }

    public class LogItem
    {
        public int Id { get; set; }
        public required string Type { get; set; }
        public int? UserId { get; set; }
        public required string Detail { get; set; }
        public string? TargetTable { get; set; }
        public int? TargetRowId { get; set; }
        public string? TargetOldData { get; set; }
        public string? TargetNewData { get; set; }
    }

    public class UserMe
    {
        public int id { get; set; }
    }
}
