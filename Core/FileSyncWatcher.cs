using Core.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Core
{
    /// <summary>
    /// FileSyncWatcher class monitors a specified directory for file changes and synchronizes these changes with a cloud storage.
    /// </summary>
    public class FileSyncWatcher
    {
        private readonly FileSystemWatcher _watcher;
        private readonly ICloudFileSyncManager _cloudFileSyncManager;
        private readonly ILogger<FileSyncWatcher> _logger;

        /// <summary>
        /// Initializes a new instance of the <see cref="FileSyncWatcher"/> class.
        /// </summary>
        /// <param name="options">The options containing the local folder path to watch.</param>
        /// <param name="cloudFileSyncManager">The cloud file synchronization manager.</param>
        /// <param name="logger">The logger instance for logging events and errors.</param>
        public FileSyncWatcher(IOptions<FileSyncWatcherOptions> options, ICloudFileSyncManager cloudFileSyncManager, ILogger<FileSyncWatcher> logger)
        {
            _watcher = new FileSystemWatcher(options.Value.LocalFolderPath);
            _cloudFileSyncManager = cloudFileSyncManager;
            _logger = logger;

            this.ConfigureWatcher();
        }

        /// <summary>
        /// Starts watching the specified directory for file changes.
        /// </summary>
        public void Start()
        {
            _watcher.EnableRaisingEvents = true;

            _logger.LogInformation("Started watching");
        }

        /// <summary>
        /// Stops watching the directory and disposes the file system watcher.
        /// </summary>
        public void StopWatching()
        {
            _watcher.EnableRaisingEvents = false;
            _watcher.Dispose();

            _logger.LogInformation("Stopped watching");
        }

        private void ConfigureWatcher()
        {
            _watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName;
            _watcher.Filter = "*";

            _watcher.Created += async (s, e) => await this.OnCreatedAsync(s, e);
            _watcher.Deleted += async (s, e) => await this.OnDeletedAsync(s, e);
            _watcher.Error += this.OnError;
        }

        private async Task OnCreatedAsync(object sender, FileSystemEventArgs e)
        {
            if (IsDirectory(e.FullPath))
            {
                _logger.LogWarning("Cannot upload directories");
            }
            else
            {
                if (string.IsNullOrEmpty(e.Name))
                {
                    _logger.LogWarning("Cannot upload files without a name");
                    return;
                }

                await _cloudFileSyncManager.UploadAsync(e.FullPath, e.Name);
            }
        }

        private async Task OnDeletedAsync(object sender, FileSystemEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Name))
            {
                _logger.LogWarning("Cannot delete files without a name");
                return;
            }

            await _cloudFileSyncManager.DeleteFileAsync(e.Name);
        }

        private void OnError(object sender, ErrorEventArgs e)
        {
            _logger.LogError(e.GetException(), $"The {nameof(FileSystemWatcher)} has encountered an error and cannot continue.");
        }

        private static bool IsDirectory(string path) => Directory.Exists(path);
    }
}
