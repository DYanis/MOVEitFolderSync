using Infrastructure.ApiClients;

namespace Infrastructure.Handlers
{
    public class AuthenticationHandler(MoveItTokenProvider tokenProvider) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string token = await tokenProvider.EnsureValidTokenAsync();
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            return await base.SendAsync(request, cancellationToken);
        }
    }
}
