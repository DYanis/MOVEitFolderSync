namespace Core.Configurations
{
    public class CloudFileSyncManagerOptions
    {
        public int FetchFilesPerPage { get; set; }

        public int MaxDegreeOfParallelism { get; set; }

        public int BufferSize { get; set; }

        public int RetryCount { get; set; }

        public int RetryDelaySeconds { get; set; }
    }
}
