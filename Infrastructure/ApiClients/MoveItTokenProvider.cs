using Infrastructure.ApiClients.MoveIt;
using Infrastructure.Configurations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure.ApiClients
{
    /// <summary>
    /// Provides methods to manage and refresh MOVEit access tokens.
    /// Handles token acquisition and refresh logic, ensuring that a valid token is always available.
    /// </summary>
    public class MoveItTokenProvider(MoveItApiClient moveItApiClient, IOptions<CredentialsOptions> options, ILogger<MoveItTokenProvider> logger)
    {
        private const int TokenExpiryToleranceSeconds = 30;

        private readonly MoveItApiClient _moveItApiClient = moveItApiClient;
        private readonly ILogger<MoveItTokenProvider> _logger = logger;
        private readonly string _username = options.Value.Username;
        private readonly string _password = options.Value.Password;
        private TokenAcquiredModel? _currentToken;
        private DateTime _tokenExpiryTime;

        /// <summary>
        /// Ensures that a valid access token is available for use.
        /// using the refresh token if available, or acquires a new token.
        /// </summary>
        /// <returns>
        /// A valid access token as a string.
        /// Throws an InvalidOperationException if a valid access token cannot be obtained.
        /// </returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown when an access token is not available.
        /// </exception>
        public async Task<string> EnsureValidTokenAsync()
        {
            DateTime expiryTimeWithTolerance = _tokenExpiryTime != DateTime.MinValue ? _tokenExpiryTime.AddSeconds(-TokenExpiryToleranceSeconds) : _tokenExpiryTime;

            if (_currentToken == null || DateTime.UtcNow > expiryTimeWithTolerance)
            {
                if (_currentToken != null && !string.IsNullOrEmpty(_currentToken.Refresh_token))
                {
                    await this.RefreshTokenAsync();
                }
                else
                {
                    await this.AcquireTokenAsync();
                }
            }

            return _currentToken?.Access_token ?? throw new InvalidOperationException("Access token is not available.");
        }

        private async Task AcquireTokenAsync()
        {
            try
            {
                _currentToken = await _moveItApiClient.ApiV1TokenAsync(Grant_type.Password, _username, _password);
                this.SetTokenExpiry();

                _logger.LogInformation($"Token acquired. Expires in {_currentToken.Expires_in} seconds.");
            }
            catch (ApiException ex)
            {
                _logger.LogError(ex, $"API error while acquiring token: {ex.StatusCode} - {ex.Response}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while acquiring token.");
                throw;
            }
        }

        private async Task RefreshTokenAsync()
        {
            try
            {
                if (string.IsNullOrEmpty(_currentToken?.Refresh_token))
                    throw new InvalidOperationException("Refresh token is not available.");

                _currentToken = await _moveItApiClient.ApiV1TokenAsync(Grant_type.Refresh_token, refresh_token: _currentToken.Refresh_token);
                this.SetTokenExpiry();

                _logger.LogInformation($"Token refreshed. Expires in {_currentToken.Expires_in} seconds.");
            }
            catch (ApiException ex)
            {
                _logger.LogError(ex, $"API error while refreshing token: {ex.StatusCode} - {ex.Response}");
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error while refreshing token.");
                throw;
            }
        }

        private void SetTokenExpiry()
        {
            if (_currentToken?.Expires_in == null)
                throw new ArgumentNullException("Token expiration time is not defined.");

            _tokenExpiryTime = DateTime.UtcNow.AddSeconds((double)_currentToken.Expires_in);
        }
    }
}
