using Microsoft.EntityFrameworkCore;

namespace MadoMagiArchive.DataServices
{
    public class DataDbContext : DbContext
    {
        public DbSet<FileItem> Files { get; set; }
        public DbSet<Tag> Tags { get; set; }
        public DbSet<TagName> TagNames { get; set; }
        public DbSet<FileTag> FileTags { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<FileItem>()
                .HasMany(e => e.Tags)
                .WithMany()
                .UsingEntity<FileTag>(
                    l => l.HasOne<Tag>().WithMany().HasForeignKey(e => e.TagId),
                    r => r.HasOne<FileItem>().WithMany().HasForeignKey(e => e.FileId));

            modelBuilder.Entity<Tag>()
                .HasMany(e => e.Names)
                .WithOne()
                .HasForeignKey(e => e.TagId)
                .IsRequired();
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=Databases/Data.db");
        }
    }

    public static class DataDbContextExtensions
    {
        public static void SeedData(this DataDbContext context)
        {
            if (!context.Tags.Any())
            {
                context.Tags.AddRange([
                    new() {
                        Type = "character",
                        Description = "Kaname Madoka 小圆 鹿目圆香 甜瓜",
                        Names = [
                            new() { Name="鹿目圆", Lang="zh" },
                            new() { Name="Madoka Kaname", Lang="en" },
                            new() { Name="鹿目まどか", Lang="jp" },
                        ],
                        Permission = 0x00016464,
                    },
                    new() {
                        Type = "character",
                        Description = "Akemi Homura 小焰 晓美炎 南瓜",
                        Names = [
                            new() { Name="晓美焰", Lang="zh" },
                            new() { Name="Homura Akemi", Lang="en" },
                            new() { Name="暁美ほむら", Lang="jp" },
                        ],
                        Permission = 0x00016464,
                    },
                    new() {
                        Type = "character",
                        Description = "Miki Sayaka 小爽 爽哥 美树沙耶加 美树爽 树莓 灭火器",
                        Names = [
                            new() { Name="美树沙耶香", Lang="zh" },
                            new() { Name="Sayaka Miki", Lang="en" },
                            new() { Name="美樹さやか", Lang="jp" },
                        ],
                        Permission = 0x00016464,
                    },
                    new() {
                        Type = "character",
                        Description = "Sakura Kyoko 苹果",
                        Names = [
                            new() { Name="佐仓杏子", Lang="zh" },
                            new() { Name="Kyoko Sakura", Lang="en" },
                            new() { Name="佐倉杏子", Lang="jp" },
                        ],
                        Permission = 0x00016464,
                    },
                    new() {
                        Type = "character",
                        Description = "Tomoe Mami 学姐 红茶",
                        Names = [
                            new() { Name="巴麻美", Lang="zh" },
                            new() { Name="Mami Tomoe", Lang="en" },
                            new() { Name="巴マミ", Lang="jp" },
                        ],
                        Permission = 0x00016464,
                    },
                    new() {
                        Type = "character",
                        Description = "Momoe Nagisa 小渚 奶酪",
                        Names = [
                            new() { Name="百江渚", Lang="zh" },
                            new() { Name="Nagisa Momoe", Lang="en" },
                            new() { Name="百江なぎさ", Lang="jp" },
                        ],
                        Permission = 0x00016464,
                    },
                ]);
            }
            context.SaveChanges();
        }
    }
}
