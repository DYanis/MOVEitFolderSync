using Infrastructure.ApiClients;
using Infrastructure.ApiClients.MoveIt;
using Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;

namespace Infrastructure.Tests
{
    [TestClass]
    public class MoveItTokenProviderTests
    {
        private Mock<MoveItApiClient>? _moveItApiClientMock;
        private Mock<ILogger<MoveItTokenProvider>>? _loggerMock;
        private MoveItTokenProvider? _tokenProvider;
        private IOptions<CredentialsOptions>? _credentialsOptions;

        [TestInitialize]
        public void Setup()
        {
            _moveItApiClientMock = new Mock<MoveItApiClient>(new HttpClient());
            _loggerMock = new Mock<ILogger<MoveItTokenProvider>>();
            _credentialsOptions = Options.Create(new CredentialsOptions { Username = "testUsername", Password = "testPass" });
            _tokenProvider = new MoveItTokenProvider(_moveItApiClientMock.Object, _credentialsOptions, _loggerMock.Object);
        }

        [TestMethod]
        public async Task EnsureValidTokenAsync_ShouldAcquireToken_WhenNoTokenExists()
        {
            _moveItApiClientMock!.Setup(x => x.ApiV1TokenAsync(
                It.Is<Grant_type>(g => g == Grant_type.Password),
                It.Is<string>(u => u == "testUsername"),
                It.Is<string>(p => p == "testPass"),
                null, null, null, null, null, null, null, null, null, null, null, null, null,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenAcquiredModel { Access_token = "newAccessToken", Expires_in = 1199 });

            string token = await _tokenProvider!.EnsureValidTokenAsync();

            Assert.AreEqual("newAccessToken", token);
            _moveItApiClientMock.Verify(x => x.ApiV1TokenAsync(
                It.Is<Grant_type>(g => g == Grant_type.Password),
                It.Is<string>(u => u == "testUsername"),
                It.Is<string>(p => p == "testPass"),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<object>(), It.IsAny<string>(), It.IsAny<string>(), null, It.IsAny<string>(), It.IsAny<string>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [TestMethod]
        public async Task EnsureValidTokenAsync_ShouldRefreshToken_WhenTokenIsExpired()
        {
            _moveItApiClientMock!.Setup(x => x.ApiV1TokenAsync(
                It.Is<Grant_type>(g => g == Grant_type.Password),
                It.Is<string>(u => u == "testUsername"),
                It.Is<string>(p => p == "testPass"),
                null, null, null, null, null, null, null, null, null, null, null, null, null,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenAcquiredModel { Access_token = "newAccessToken", Expires_in = 1199, Refresh_token = "refreshToken" });

            string initialToken = await _tokenProvider!.EnsureValidTokenAsync();

            // Set the token expiry time to the past to simulate an expired token
            _tokenProvider.GetType()
                .GetField("_tokenExpiryTime", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(_tokenProvider, DateTime.UtcNow.AddSeconds(-10));

            _moveItApiClientMock.Setup(x => x.ApiV1TokenAsync(
                It.Is<Grant_type>(g => g == Grant_type.Refresh_token),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.Is<string>(r => r == "refreshToken"),
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>(),
                It.IsAny<string>(), null, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenAcquiredModel { Access_token = "refreshedAccessToken", Expires_in = 1199 });

            string refreshedToken = await _tokenProvider.EnsureValidTokenAsync();

            Assert.AreEqual("refreshedAccessToken", refreshedToken);
        }

        [TestMethod]
        public async Task EnsureValidTokenAsync_ShouldNotRefreshToken_WhenTokenIsValid()
        {
            _moveItApiClientMock!.Setup(x => x.ApiV1TokenAsync(
                It.Is<Grant_type>(g => g == Grant_type.Password),
                It.Is<string>(u => u == "testUsername"),
                It.Is<string>(p => p == "testPass"),
                null, null, null, null, null, null, null, null, null, null, null, null, null,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenAcquiredModel { Access_token = "initialToken", Expires_in = 1199 });

            // First call to acquire token
            string initialToken = await _tokenProvider!.EnsureValidTokenAsync();

            _moveItApiClientMock.Setup(x => x.ApiV1TokenAsync(
                It.Is<Grant_type>(g => g == Grant_type.Password),
                It.Is<string>(u => u == "testUsername"),
                It.Is<string>(p => p == "testPass"),
                null, null, null, null, null, null, null, null, null, null, null, null, null,
                It.IsAny<CancellationToken>()))
                .ReturnsAsync(new TokenAcquiredModel { Access_token = "secondToken", Expires_in = 1199 });

            // Second call to acquire token
            string secondToken = await _tokenProvider.EnsureValidTokenAsync();

            Assert.AreEqual(initialToken, secondToken);
        }
    }
}
