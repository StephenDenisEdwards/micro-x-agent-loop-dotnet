using Google.Apis.Auth.OAuth2;
using Google.Apis.Http;
using Google.Apis.Services;
using Google.Apis.Util.Store;

namespace MicroXAgentLoop.Tools;

/// <summary>
/// Thread-safe base class for Google API service authentication.
/// Subclasses specify scopes, token directory, and service construction.
/// </summary>
public abstract class GoogleAuthBase<TService> where TService : IClientService
{
    private static readonly SemaphoreSlim Lock = new(1, 1);
    private static TService? _service;

    protected abstract string[] Scopes { get; }
    protected abstract string TokenDirectory { get; }
    protected abstract TService CreateService(IConfigurableHttpClientInitializer credential);

    public async Task<TService> GetServiceAsync(string clientId, string clientSecret)
    {
        if (_service is not null)
            return _service;

        await Lock.WaitAsync();
        try
        {
            // Double-check after acquiring lock
            if (_service is not null)
                return _service;

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                throw new InvalidOperationException(
                    "GOOGLE_CLIENT_ID and GOOGLE_CLIENT_SECRET must be set in .env");

            var secrets = new ClientSecrets
            {
                ClientId = clientId,
                ClientSecret = clientSecret,
            };

            var tokenPath = Path.Combine(Directory.GetCurrentDirectory(), TokenDirectory);

            var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
                secrets,
                Scopes,
                "user",
                CancellationToken.None,
                new FileDataStore(tokenPath, true));

            _service = CreateService(credential);
            return _service;
        }
        finally
        {
            Lock.Release();
        }
    }
}
