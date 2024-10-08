using Microsoft.EntityFrameworkCore;

using MadoMagiArchive.CoreServices.CoreModels;
using MadoMagiArchive.DataServices.Data;


namespace MadoMagiArchive.CoreServices.Core
{
    public class CoreDbContext : DbContext
    {
        public DbSet<Setting> Settings { get; set; }
        public DbSet<TablePermission> TablePermissions { get; set; }
        public DbSet<UserItem> Users { get; set; }
        public DbSet<UserSetting> UserSettings { get; set; }
        public DbSet<LogItem> Logs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserItem>()
                .HasMany(e => e.Settings)
                .WithOne()
                .HasForeignKey(e => e.UserId);
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlite("Data Source=Databases/Core.db");
        }
    }

    public class CoreService(CoreDbContext dbContext)
    {
        public async Task<string?> GetSetting(string name)
        {
            return (await dbContext.Settings.SingleOrDefaultAsync(x => x.Name == name))?.Value;
        }

        public async Task SetSetting(string name, string? value)
        {
            var oldRow = await dbContext.Settings.SingleOrDefaultAsync(x => x.Name == name);

            if (value == null)
            {
                if (oldRow != null)
                {
                    dbContext.Settings.Remove(oldRow);
                }
            }
            else
            {
                if (oldRow != null)
                {
                    oldRow.Value = value;
                    dbContext.Settings.Update(oldRow);
                }
                else
                {
                    await dbContext.Settings.AddAsync(new()
                    {
                        Name = name,
                        Value = value
                    });
                }
            }

            await dbContext.SaveChangesAsync();
        }
    }
    public static class CoreDbContextExtensions
    {
        public static void SeedData(this CoreDbContext context)
        {
            foreach (var table in new string[] {
                nameof(DataDbContext.Files),
                nameof(DataDbContext.Tags),
                nameof(DataDbContext.TagNames),
            })
                if (!context.TablePermissions.Any(x => x.Table == table))
                    context.TablePermissions.Add(new()
                    {
                        Table = table,
                        Permission = 0x00010101,
                    });

            if (!context.Users.Any())
            {
                context.Users.Add(new()
                {
                    Id = 1,
                    AccessLevel = 0x00ffffff,
                    Permission = 0x00ffffff
                });
            }
            context.SaveChanges();
        }
    }

}
