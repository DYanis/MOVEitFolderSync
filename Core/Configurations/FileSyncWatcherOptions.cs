namespace Core.Configurations
{
    public record FileSyncWatcherOptions
    {
        public required string LocalFolderPath { get; set; }
    }
}
