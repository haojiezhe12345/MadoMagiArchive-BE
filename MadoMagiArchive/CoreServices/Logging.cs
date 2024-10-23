namespace MadoMagiArchive.CoreServices
{
    public class LoggingService(CoreDbContext coreDb)
    {
        public async Task AddLog(string type, string detail)
        {
            await coreDb.Logs.AddAsync(new()
            {
                Type = type,
                Detail = detail
            });
            await coreDb.SaveChangesAsync();
        }

        public async Task AddLog(LogItem log)
        {
            await coreDb.Logs.AddAsync(log);
            await coreDb.SaveChangesAsync();
        }
    }
}
