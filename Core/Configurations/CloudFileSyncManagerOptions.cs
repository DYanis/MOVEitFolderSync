namespace Core.Configurations
{
    public record CloudFileSyncManagerOptions
    {
        public required int FetchFilesPerPage { get; set; }

        public required int MaxDegreeOfParallelism { get; set; }

        public required int BufferSize { get; set; }

        public required int RetryCount { get; set; }

        public required int RetryDelaySeconds { get; set; }
    }
}
