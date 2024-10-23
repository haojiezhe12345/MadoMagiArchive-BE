﻿namespace MadoMagiArchive.CoreServices
{
    public class LoggingService(CoreDbContext dbContext)
    {
        public async Task AddLog(string type, string detail)
        {
            await dbContext.Logs.AddAsync(new()
            {
                Type = type,
                Detail = detail
            });
            await dbContext.SaveChangesAsync();
        }

        public async Task AddLog(LogItem log)
        {
            await dbContext.Logs.AddAsync(log);
            await dbContext.SaveChangesAsync();
        }
    }
}
