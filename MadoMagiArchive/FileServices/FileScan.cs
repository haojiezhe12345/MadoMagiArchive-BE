using Microsoft.AspNetCore.SignalR;
using System.Collections.Concurrent;
using System.Diagnostics;
using MadoMagiArchive.CoreServices;
using MadoMagiArchive.DataServices;

namespace MadoMagiArchive.FileServices
{
    public interface IFileScanClient
    {
        Task IsScanning(bool isScanning);
        Task ScanProgress(int added, int total, string filename);
        Task Message(string message);
        Task Error(string message);
    }

    public class FileScanHub(FileScanService scanService) : Hub<IFileScanClient>
    {
        public override async Task OnConnectedAsync()
        {
            await Clients.Caller.IsScanning(scanService.IsScanning);
        }

        public async Task StartScan()
        {
            if (!scanService.IsScanning) await scanService.StartScanningAsync();
        }

        public async void StopScan()
        {
            await scanService.StopScanning();
        }

        public async Task GetScanState()
        {
            await Clients.Caller.IsScanning(scanService.IsScanning);
        }
    }

    public class FileScanService(IServiceScopeFactory scopeFactory, IHubContext<FileScanHub, IFileScanClient> hub, StorageService storage, ILogger<UserService> logger)
    {
        private bool scanning = false;
        private readonly SemaphoreSlim scanningSemaphore = new(1);
        private CancellationTokenSource? cts;

        public bool IsScanning => scanning;

        public async Task StartScanningAsync(string? folder = null)
        {
            await scanningSemaphore.WaitAsync();
            try
            {
                if (scanning) return;
                scanning = true;

                cts?.Dispose();
                cts = new CancellationTokenSource();

                _ = Task.Run(() => ScanFiles(folder, cts.Token));
            }
            catch
            {
                scanning = false;
            }
            scanningSemaphore.Release();
            _ = hub.Clients.All.IsScanning(scanning);
        }

        public async Task StopScanning()
        {
            if (cts != null)
            {
                await cts.CancelAsync();
                LogInfo("Trying to cancel the scan");
            }
        }

        private void ScanFiles(string? folder, CancellationToken cancellationToken)
        {
            try
            {
                LogInfo("Scan started");

                var stopwatch = new Stopwatch();
                stopwatch.Start();

                using var scope = scopeFactory.CreateScope();
                var dataDb = scope.ServiceProvider.GetRequiredService<DataDbContext>();

                folder ??= storage.StorageLocation;

                var fileQueue = new ConcurrentQueue<string>();

                int total = 0;
                bool fileEnumCompleted = false;

                int added = 0;
                string fileAdding = "";
                bool fileAddCompleted = false;

                var fileEnumTask = Task.Run(() =>
                {
                    try
                    {
                        foreach (var fullPath in Directory.EnumerateFiles(folder, "*.*", new EnumerationOptions
                        {
                            RecurseSubdirectories = true,
                            IgnoreInaccessible = true,
                        }))
                        {
                            if (cancellationToken.IsCancellationRequested) break;
                            if (fullPath.Contains(storage.UploadDirectory)) continue;

                            total++;
                            fileQueue.Enqueue(fullPath);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError(ex, "An error occured while enumerating files");
                    }
                    finally
                    {
                        fileEnumCompleted = true;
                    }
                });


                var fileAddTask = Task.Run(() =>
                {
                    try
                    {
                        const int maxThreads = 16;
                        var semaphore = new SemaphoreSlim(maxThreads);
                        object lockObj = new();

                        while (!(
                            (
                                fileEnumCompleted && fileQueue.IsEmpty  // fully completed
                                || cancellationToken.IsCancellationRequested  // or manually stopped
                            )
                            && semaphore.CurrentCount == maxThreads  // and no remaining threads
                            ))
                        {
                            if (fileQueue.TryDequeue(out var fullPath) && !cancellationToken.IsCancellationRequested)
                            {
                                var file = fullPath.Replace(folder, "");
                                fileAdding = file;

                                if (!dataDb.Files.Any(x => x.File == file))
                                {
                                    semaphore.Wait();
                                    Task.Run(() =>
                                    {
                                        try
                                        {
                                            var fileItem = new FileItem()
                                            {
                                                File = file,
                                                Size = new FileInfo(fullPath).Length,
                                                Type = Utils.GetContentType(fullPath),
                                                Permission = 0x00646464,
                                                Owner = UserService.SystemUserId,
                                                Title = Path.GetFileNameWithoutExtension(fullPath),
                                                DateCreated = File.GetCreationTime(fullPath),
                                                DateModified = File.GetLastWriteTime(fullPath),
                                            };

                                            if (fileItem.IsImage()) fileItem.AddImageInfo(fullPath).Wait();
                                            if (fileItem.IsVideo()) fileItem.AddVideoInfo(fullPath).Wait();

                                            lock (lockObj)
                                            {
                                                dataDb.Files.Add(fileItem);
                                                added++;
                                                if (added % 1000 == 0) dataDb.SaveChanges();
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            LogError(ex, "An error occured while adding a file to database");
                                        }
                                        finally
                                        {
                                            semaphore.Release();
                                        }
                                    });
                                }
                                else
                                {
                                    lock (lockObj) added++;
                                }
                            }
                            else
                            {
                                Thread.Sleep(10);
                            }
                        }

                        dataDb.SaveChanges();
                    }
                    catch (Exception ex)
                    {
                        LogError(ex, "An error occured while adding files to database");
                    }
                    finally
                    {
                        fileAddCompleted = true;
                    }
                });

                var progressPrintTask = Task.Run(() =>
                {
                    try
                    {
                        string lastPrintFile = "";
                        while (!fileAddCompleted)
                        {
                            if (fileAdding != lastPrintFile)
                            {
                                logger.LogInformation($"{added} / {total} - {fileAdding}");
                                hub.Clients.All.ScanProgress(added, total, fileAdding);
                                lastPrintFile = fileAdding;
                            }
                            Thread.Sleep(10);
                        }
                    }
                    catch (Exception ex)
                    {
                        LogError(ex, "An error occured while displaying scan progress");
                    }
                });

                Task.WaitAll(fileEnumTask, fileAddTask, progressPrintTask);

                stopwatch.Stop();
                LogInfo($"Scan exited gracefully after {stopwatch.Elapsed.TotalSeconds}s");
            }

            catch (Exception ex)
            {
                LogError(ex, "Scan exited with an error");
            }

            finally
            {
                scanning = false;
                hub.Clients.All.IsScanning(scanning);
            }
        }

        private void LogError(Exception ex, string message)
        {
            logger.LogError(ex, message);
            hub.Clients.All.Error(ex.ToString());
        }

        private void LogInfo(string message)
        {
            logger.LogInformation(message);
            hub.Clients.All.Message(message);
        }
    }
}
