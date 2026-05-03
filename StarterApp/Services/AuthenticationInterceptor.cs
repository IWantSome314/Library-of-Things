using System.Net;
using System.Net.Http.Headers;

namespace StarterApp.Services;

// File purpose:
// Central HTTP pipeline hook that injects bearer tokens and retries once after a refresh on 401.
/// <summary>
/// A DelegatingHandler (HTTP Interceptor) that sits in the pipeline of outgoing API requests.
/// It automatically injects the active JWT token into the headers, and gracefully forces 
/// a token refresh sequence if the API randomly rejects the request (401 Unauthorized).
/// </summary>
public class AuthenticationInterceptor : DelegatingHandler
{
    private readonly IServiceProvider _serviceProvider;

    public AuthenticationInterceptor(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authService = _serviceProvider.GetRequiredService<IAuthenticationService>();
        
        // 1. Proactive pre-flight check and refresh
        var token = await authService.GetAccessTokenAsync();
        if (!string.IsNullOrWhiteSpace(token))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // 2. Transmit request
        var response = await base.SendAsync(request, cancellationToken);

        // 3. Reactive refresh if server unexpectedly threw 401
        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            var refreshed = await authService.ForceRefreshTokenAsync();
            if (refreshed)
            {
                var newToken = await authService.GetAccessTokenAsync();
                if (!string.IsNullOrWhiteSpace(newToken))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", newToken);
                    response.Dispose(); // release old response
                    return await base.SendAsync(request, cancellationToken);
                }
            }
            else 
            {
                await authService.LogoutAsync();
            }
        }

        return response;
    }
}
