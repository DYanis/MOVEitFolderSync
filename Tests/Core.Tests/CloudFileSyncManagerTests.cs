using Core.Configurations;
using Infrastructure.ApiClients.MoveIt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;

namespace Core.Tests
{
    [TestClass]
    public class CloudFileSyncManagerTests
    {
        private Mock<MoveItApiClient>? _apiClientMock;
        private Mock<ILogger<CloudFileSyncManager>>? _loggerMock;
        private CloudFileSyncManager? _cloudFileSyncManager;

        [TestInitialize]
        public void Setup()
        {
            _apiClientMock = new Mock<MoveItApiClient>(new HttpClient());
            _loggerMock = new Mock<ILogger<CloudFileSyncManager>>();

            IOptions<CloudFileSyncManagerOptions> options = Options.Create(new CloudFileSyncManagerOptions { FetchFilesPerPage = 100, MaxDegreeOfParallelism = 5, BufferSize = 8192 });
            _cloudFileSyncManager = new CloudFileSyncManager(options, _apiClientMock.Object, _loggerMock.Object);
        }

        [TestMethod]
        public async Task InitializeAsync_ShouldFetchHomeFolderIdAndContent()
        {
            UserDetailsModel userDetails = new() { HomeFolderID = 12345 };
            _apiClientMock!.Setup(x => x.ApiV1UsersSelfAsync(It.IsAny<CancellationToken>()))
                .ReturnsAsync(userDetails);

            PagedModelOfFolderContentItemModel folderContent = new()
            {
                Items = new List<FolderContentItemModel>
                {
                    new () { Id = 1, Name = "file1.txt" },
                    new () { Id = 2, Name = "file2.txt" }
                },
                Paging = new PagingInfoModel { TotalPages = 1 }
            };
            _apiClientMock!.Setup(x => x.ApiV1FoldersContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(folderContent);

            await _cloudFileSyncManager!.InitializeAsync();

            Dictionary<string, int>? cloudFileIds = _cloudFileSyncManager.GetType()
                .GetField("_cloudFileIds", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(_cloudFileSyncManager) as Dictionary<string, int>;

            long? folderId = _cloudFileSyncManager.GetType()
                .GetField("_folderId", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(_cloudFileSyncManager) as long?;

            Assert.IsNotNull(cloudFileIds);
            Assert.AreEqual(2, cloudFileIds.Count);
            Assert.AreEqual(12345, folderId);

            _apiClientMock.Verify(x => x.ApiV1UsersSelfAsync(It.IsAny<CancellationToken>()), Times.Once);
            _apiClientMock.Verify(x => x.ApiV1FoldersContentAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>(),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task DeleteFileAsync_ShouldDeleteFile()
        {
            string fileName = "file.txt";

            FieldInfo? cloudFileIdsField = typeof(CloudFileSyncManager).GetField("_cloudFileIds", BindingFlags.NonPublic | BindingFlags.Instance);
            Dictionary<string, int>? cloudFileIds = new() { { fileName, 1 } };
            cloudFileIdsField?.SetValue(_cloudFileSyncManager, cloudFileIds);

            await _cloudFileSyncManager!.DeleteFileAsync(fileName);

            cloudFileIds = cloudFileIdsField?.GetValue(_cloudFileSyncManager) as Dictionary<string, int>;

            Assert.IsNotNull(cloudFileIds);
            Assert.AreEqual(0, cloudFileIds.Count);

            _apiClientMock!.Verify(x => x.ApiV1FilesDeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task DeleteFileAsync_ShouldNotDeleteNonTrackedFile()
        {
            string fileName = "nonTrackedFile.txt";

            await _cloudFileSyncManager!.DeleteFileAsync(fileName);

            _loggerMock!.Verify(x => x.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains("Attempted to delete non-tracked file")),
                null,
                It.IsAny<Func<It.IsAnyType, Exception, string>>()!), Times.Once);

            _apiClientMock!.Verify(x => x.ApiV1FilesDeleteAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        }
    }
}
