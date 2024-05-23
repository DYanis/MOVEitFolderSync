namespace Core
{
    /// <summary>
    /// Interface for managing file synchronization with cloud storage.
    /// </summary>
    public interface ICloudFileSyncManager
    {
        /// <summary>
        /// Asynchronously uploads a file to cloud storage.
        /// </summary>
        /// <param name="filePath">The path of the local file to be uploaded.</param>
        /// <param name="fileName">The name of the file in the cloud storage.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task UploadAsync(string filePath, string fileName);

        /// <summary>
        /// Asynchronously deletes a file from cloud storage.
        /// </summary>
        /// <param name="fileName">The name of the file to be deleted from the cloud storage.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task DeleteFileAsync(string fileName);
    }
}
