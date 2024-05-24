using Core.Configurations;
using Infrastructure.ApiClients.MoveIt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;
using System.Collections.Concurrent;

namespace Core
{
    /// <summary>
    /// Manages file synchronization between the local system and MOVEit Cloud storage.
    /// </summary>
    /// <param name="apiClient"> </param>
    /// <param name="logger"></param>
    public class CloudFileSyncManager(IOptions<CloudFileSyncManagerOptions> options, MoveItApiClient apiClient, ILogger<CloudFileSyncManager> logger) : ICloudFileSyncManager
    {
        private readonly int _fetchFilesPerPage = options.Value.FetchFilesPerPage;
        private readonly int _maxDegreeOfParallelism = options.Value.MaxDegreeOfParallelism;
        private readonly int _bufferSize = options.Value.BufferSize;
        private readonly int _retryCount = options.Value.RetryCount;
        private readonly int _retryDelaySeconds = options.Value.RetryDelaySeconds;

        private readonly MoveItApiClient _apiClient = apiClient;
        private readonly ILogger<CloudFileSyncManager> _logger = logger;

        // Store the file name and its corresponding ID in the cloud storage.
        private readonly Dictionary<string, int> _cloudFileIds = [];
        private long? _folderId;

        /// <summary>
        /// Initializes the CloudFileSyncManager by fetching the user's home folder ID and the content of the folder.
        /// </summary>
        /// <returns></returns>
        public async Task InitializeAsync()
        {
            await FetchHomeFolderIdAsync();
            await FetchHomeFolderContentAsync();
        }

        /// <summary>
        /// Uploads a file to the MOVEit Cloud storage using a provided file path and file name.
        /// </summary>
        /// <param name="filePath">The full path of the file on the local system that needs to be uploaded.</param>
        /// <param name="fileName">The name of the file to be used when uploading to MOVEit Cloud. This name is used to reference the file in the cloud and to track it locally.</param>
        /// <exception cref="ApiException">Thrown when there is an issue with the API during the upload process, such as a connectivity or server-side problem.</exception>
        /// <exception cref="Exception">General exceptions are caught and re-thrown after logging, indicating severe issues that need addressing.</exception>
        public async Task UploadAsync(string filePath, string fileName)
        {
            _logger.LogInformation("Uploading file {FileName}...", fileName);

            try
            {
                await ExecuteUploadWithRetryAsync(filePath, fileName);

                _logger.LogInformation("File {FileName} uploaded", fileName);
            }
            catch (IOException ex)
            {
                _logger.LogError(ex, "Error occurred while opening file {FileName} for upload.", fileName);
            }
            catch (ApiException ex)
            {
                _logger.LogError("API error during file upload: {Message}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error during file upload");
                throw;
            }
        }

        /// <summary>
        /// Deletes a file from MOVEit Cloud storage based on the provided file name.
        /// </summary>
        /// <param name="fileName">The name of the file to be deleted. This name should correspond to one previously uploaded and tracked in the internal dictionary.</param>
        /// <exception cref="ApiException">Thrown if there is a problem with the cloud service during the deletion process, such as connectivity issues or server errors.</exception>
        /// <exception cref="Exception">General exceptions are logged and then re-thrown, indicating severe issues that require further handling.</exception>
        public async Task DeleteFileAsync(string fileName)
        {
            _logger.LogInformation("Deleting file {FileName}...", fileName);

            if (_cloudFileIds.TryGetValue(fileName, out int fileId))
            {
                try
                {
                    await _apiClient.ApiV1FilesDeleteAsync(fileId.ToString());
                    _cloudFileIds.Remove(fileName);

                    _logger.LogInformation("File {FileName} deleted", fileName);
                }
                catch (ApiException ex)
                {
                    _logger.LogError("API error during file deletion: {Message}", ex.Message);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error during file deletion");
                    throw;
                }
            }
            else
            {
                _logger.LogWarning("Attempted to delete non-tracked file: {FileName}", fileName);
            }
        }

        private async Task FetchHomeFolderIdAsync()
        {
            try
            {
                UserDetailsModel userDetails = await _apiClient.ApiV1UsersSelfAsync();
                _folderId = userDetails.HomeFolderID ?? throw new InvalidOperationException("User does not have a home folder.");

                _logger.LogInformation("User's home folder ID retrieved: {HomeFolderId}", _folderId);
            }
            catch (ApiException apiEx)
            {
                _logger.LogError(apiEx, "API error occurred while fetching user's home folder ID. Status Code: {StatusCode}, Response: {Response}", apiEx.StatusCode, apiEx.Response);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch user's home folder ID.");
                throw;
            }
        }

        private async Task FetchHomeFolderContentAsync()
        {
            if (!_folderId.HasValue)
            {
                _logger.LogError("Home folder ID is not set. Cannot fetch folder content.");
                throw new InvalidOperationException("Home folder ID is not set. Cannot fetch folder content.");
            }

            try
            {
                PagedModelOfFolderContentItemModel initialContent = await _apiClient.ApiV1FoldersContentAsync(_folderId.Value.ToString(),
                                                                                                                page: 1,
                                                                                                                perPage: _fetchFilesPerPage);

                if (initialContent?.Items == null || initialContent.Items.Count == 0)
                {
                    _logger.LogInformation("No files found in the home folder with ID: {HomeFolderId}.", _folderId);
                    return;
                }

                int totalPages = initialContent.Paging?.TotalPages ?? 1;
                ConcurrentBag<PagedModelOfFolderContentItemModel> itemsToProcess = [initialContent];

                if (totalPages > 1)
                {
                    IEnumerable<int> pages = Enumerable.Range(2, totalPages - 1);
                    ParallelOptions parallelOptions = new() { MaxDegreeOfParallelism = _maxDegreeOfParallelism };

                    await Parallel.ForEachAsync(pages, parallelOptions, async (page, token) =>
                    {
                        PagedModelOfFolderContentItemModel folderContent = await _apiClient.ApiV1FoldersContentAsync(_folderId.Value.ToString(),
                                                                                                                       page: page,
                                                                                                                       perPage: _fetchFilesPerPage,
                                                                                                                       cancellationToken: token);

                        if (folderContent?.Items != null)
                        {
                            itemsToProcess.Add(folderContent);
                        }
                    });
                }

                IEnumerable<FolderContentItemModel> folderContentItems = itemsToProcess.SelectMany(c => c.Items!);

                foreach (var file in folderContentItems)
                {
                    if (!string.IsNullOrEmpty(file.Name) && file.Id.HasValue)
                    {
                        _cloudFileIds.TryAdd(file.Name, file.Id.Value);
                    }
                }

                _logger.LogInformation("CloudFileSyncManager initialized with {Count} files.", _cloudFileIds.Count);
            }
            catch (ApiException ex)
            {
                _logger.LogError(ex, "An API error occurred while fetching file data from the cloud.");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error occurred while initializing the CloudFileSyncManager.");
                throw;
            }
        }

        private async Task ExecuteUploadWithRetryAsync(string filePath, string fileName)
        {
            if (!_folderId.HasValue)
            {
                _logger.LogError("Home folder ID is not set. Cannot upload file.");
                throw new InvalidOperationException("Home folder ID is not set. Cannot upload file.");
            }

            AsyncRetryPolicy retryPolicy = GetUploadRetryPolicy(fileName);

            await retryPolicy.ExecuteAsync(async () =>
            {
                using FileStream stream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: _bufferSize, useAsync: true);

                FileParameter fileParameter = new(stream, fileName);

                ResumableFileUploadModel result = await _apiClient.ApiV1FoldersFilesPostAsync(_folderId.Value.ToString(), fileParameter)
                                                                    .ConfigureAwait(false);

                if (!string.IsNullOrEmpty(result.FileId))
                {
                    _cloudFileIds.TryAdd(fileName, int.Parse(result.FileId));
                }
            });
        }

        private AsyncRetryPolicy GetUploadRetryPolicy(string fileName)
        {
            return Policy
                .Handle<IOException>()
                .Or<ApiException>()
                .WaitAndRetryAsync(_retryCount, retryAttempt => TimeSpan.FromSeconds(Math.Pow(_retryDelaySeconds, retryAttempt)), (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(exception, "Error occurred while uploading file {FileName}. Retrying in {TimeSpan}. Retry attempt {RetryCount}.", fileName, timeSpan, retryCount);
                });
        }
    }
}
