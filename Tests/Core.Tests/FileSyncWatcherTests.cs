using Core.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using System.Reflection;

namespace Core.Tests
{
    [TestClass]
    public class FileSyncWatcherTests
    {
        private const string testDirectory = "testDirectory";
        private const string testFileName = "testFileName.txt";

        private Mock<ICloudFileSyncManager>? _cloudFileSyncManagerMock;
        private Mock<ILogger<FileSyncWatcher>>? _loggerMock;
        private FileSyncWatcher? _fileSyncWatcher;
        private FileSystemWatcher? _watcher;

        [TestInitialize]
        public void Setup()
        {
            _cloudFileSyncManagerMock = new Mock<ICloudFileSyncManager>();
            _loggerMock = new Mock<ILogger<FileSyncWatcher>>();
            IOptions<FileSyncWatcherOptions> options = Options.Create(new FileSyncWatcherOptions { LocalFolderPath = Path.GetTempPath() });
            _fileSyncWatcher = new FileSyncWatcher(options, _cloudFileSyncManagerMock.Object, _loggerMock.Object);

            // Access the private _watcher field using reflection
            _watcher = _fileSyncWatcher.GetType()
                .GetField("_watcher", BindingFlags.NonPublic | BindingFlags.Instance)!
                .GetValue(_fileSyncWatcher) as FileSystemWatcher;
        }

        [TestMethod]
        public void Start_ShouldEnableRaisingEvents()
        {
            _fileSyncWatcher!.Start();
            Assert.IsTrue(_watcher!.EnableRaisingEvents);
        }

        [TestMethod]
        public void StopWatching_ShouldDisableRaisingEventsAndDisposeWatcher()
        {
            _fileSyncWatcher!.Start();
            _fileSyncWatcher.StopWatching();

            Assert.IsFalse(_watcher!.EnableRaisingEvents);
        }

        [TestMethod]
        public void OnCreated_ShouldUploadFile_WhenFileCreated()
        {
            string filePath = Path.Combine(Path.GetTempPath(), testFileName);

            FileSystemEventArgs fileSystemEventArgs = new(WatcherChangeTypes.Created, Path.GetTempPath(), testFileName);
            InvokePrivateMethod(_fileSyncWatcher!, "OnCreated", [_watcher!, fileSystemEventArgs]);

            _cloudFileSyncManagerMock!.Verify(x => x.UploadAsync(filePath, testFileName), Times.Once);
        }

        [TestMethod]
        public void OnCreated_ShouldNotUploadDirectory()
        {
            string dirPath = Path.Combine(Path.GetTempPath(), testDirectory);
            Directory.CreateDirectory(dirPath);

            FileSystemEventArgs fileSystemEventArgs = new(WatcherChangeTypes.Created, Path.GetTempPath(), testDirectory);
            InvokePrivateMethod(_fileSyncWatcher!, "OnCreated", [_watcher!, fileSystemEventArgs]);

            _cloudFileSyncManagerMock!.Verify(x => x.UploadAsync(It.IsAny<string>(), It.IsAny<string>()), Times.Never);

            Directory.Delete(dirPath);
        }

        [TestMethod]
        public void OnDeleted_ShouldDeleteFile_WhenFileDeleted()
        {
            FileSystemEventArgs fileSystemEventArgs = new(WatcherChangeTypes.Deleted, Path.GetTempPath(), testFileName);
            InvokePrivateMethod(_fileSyncWatcher!, "OnDeleted", [_watcher!, fileSystemEventArgs]);

            _cloudFileSyncManagerMock!.Verify(x => x.DeleteFileAsync(testFileName), Times.Once);
        }

        private static void InvokePrivateMethod(object instance, string methodName, object[] parameters)
        {
            MethodInfo method = instance.GetType()
                .GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Instance)
                ?? throw new MissingMethodException($"{methodName} not found in {instance.GetType().Name}");

            method.Invoke(instance, parameters);
        }
    }
}
