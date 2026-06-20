using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using Steamworks;
using Steamworks.Data;
using TNRD.Zeepkist.GTR.Configuration;
using ZeepSDK.External.Cysharp.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Api;

public class ApiHttpClient
{
    public const string ClientKey = "API";
    
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiHttpClient> _logger;
    private readonly AsyncPolicy<HttpResponseMessage> _wrappedPolicy;
    private readonly AsyncPolicy<HttpResponseMessage> _failurePolicy;
    private readonly SemaphoreSlim _authenticationLock = new(1, 1);

    private string _accessToken;
    private string _refreshToken;
    private DateTimeOffset _accessTokenExpiry = DateTimeOffset.MinValue;
    private DateTimeOffset _refreshTokenExpiry = DateTimeOffset.MinValue;

    private bool NeedsLogin => string.IsNullOrEmpty(_accessToken) || DateTimeOffset.UtcNow > _refreshTokenExpiry;
    private bool NeedsRefresh => DateTimeOffset.UtcNow.AddSeconds(30) >= _accessTokenExpiry;

    public ApiHttpClient(IHttpClientFactory httpClientFactory, ILogger<ApiHttpClient> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _failurePolicy = Policy
            .Handle<Exception>()
            .OrResult<HttpResponseMessage>(x => !x.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                3,
                retryCount =>
                {
                    _logger.LogWarning("Request failed, retrying ({RetryCount})", retryCount);
                    return TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                });

        AsyncRetryPolicy<HttpResponseMessage> unauthorizedPolicy = Policy
            .HandleResult<HttpResponseMessage>(x => x.StatusCode == HttpStatusCode.Unauthorized)
            .RetryAsync(3, HandleUnauthorizedRequest);

        _wrappedPolicy = Policy.WrapAsync(_failurePolicy, unauthorizedPolicy);
    }

    private async Task HandleUnauthorizedRequest(DelegateResult<HttpResponseMessage> response, int retryCount)
    {
        if (retryCount > 3)
        {
            throw new Exception("Unauthorized request failed after 3 retries");
        }

        _logger.LogInformation("Unauthorized, retrying ({RetryCount})", retryCount);

        if (NeedsLogin)
        {
            await Login();
        }
        else if (NeedsRefresh)
        {
            await Refresh();
        }
    }

    private static string CreateAuthenticationTicket()
    {
        AuthTicket authSessionTicket = SteamUser.GetAuthSessionTicket(new NetIdentity());
        StringBuilder stringBuilder = new();
        foreach (byte b in authSessionTicket.Data)
        {
            stringBuilder.AppendFormat("{0:x2}", b);
        }

        return stringBuilder.ToString();
    }

    public async UniTask<HttpResponseMessage> PostAsync(string url, object data)
    {
        if (NeedsLogin)
        {
            if (!await Login())
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    ReasonPhrase = "Failed to authenticate (Login)"
                };
            }
        }
        else if (NeedsRefresh)
        {
            if (!await Refresh())
            {
                return new HttpResponseMessage(HttpStatusCode.Unauthorized)
                {
                    ReasonPhrase = "Failed to authenticate (Refresh)"
                };
            }
        }

        return await _wrappedPolicy.ExecuteAsync(
            () =>
            {
                HttpRequestMessage request = new(HttpMethod.Post, url);
                AddHeaders(request, true);
                string json = JsonConvert.SerializeObject(data);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var httpClient = _httpClientFactory.CreateClient(ClientKey);
                return httpClient.SendAsync(request);
            });
    }

    public async UniTask<bool> LoginOrRefresh()
    {
        await _authenticationLock.WaitAsync();
        try
        {
            if (!NeedsLogin && !NeedsRefresh)
                return true;

            if (!NeedsLogin && await RefreshCore())
                return true;

            return await LoginCore();
        }
        finally
        {
            _authenticationLock.Release();
        }
    }

    public async UniTask<bool> Login()
    {
        await _authenticationLock.WaitAsync();
        try
        {
            if (!NeedsLogin && !NeedsRefresh)
                return true;

            return await LoginCore();
        }
        finally
        {
            _authenticationLock.Release();
        }
    }

    private async UniTask<bool> LoginCore()
    {
        LoginPostResource data = new()
        {
            ModVersion = MyPluginInfo.PLUGIN_VERSION,
            AuthenticationTicket = CreateAuthenticationTicket(),
            SteamId = SteamClient.SteamId.ToString()
        };

        HttpResponseMessage response = await _failurePolicy.ExecuteAsync(
            () =>
            {
                HttpRequestMessage request = new(HttpMethod.Post, "auth/login");
                AddHeaders(request, false);
                string json = JsonConvert.SerializeObject(data);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                var httpClient = _httpClientFactory.CreateClient(ClientKey);
                return httpClient.SendAsync(request);
            });
        return await ProcessAuthenticationResponse(response);
    }

    private async UniTask<bool> Refresh()
    {
        await _authenticationLock.WaitAsync();
        try
        {
            if (!NeedsRefresh)
                return true;
            if (NeedsLogin)
                return await LoginCore();

            return await RefreshCore();
        }
        finally
        {
            _authenticationLock.Release();
        }
    }

    private async UniTask<bool> RefreshCore()
    {
        RefreshPostResource data = new()
        {
            ModVersion = MyPluginInfo.PLUGIN_VERSION,
            LoginToken = _accessToken,
            RefreshToken = _refreshToken,
            SteamId = SteamClient.SteamId.ToString()
        };

        HttpResponseMessage response = await _failurePolicy.ExecuteAsync(() =>
        {
            HttpRequestMessage request = new(HttpMethod.Post, "auth/refresh");
            AddHeaders(request, false);
            string json = JsonConvert.SerializeObject(data);
            request.Content = new StringContent(json, Encoding.UTF8, "application/json");
            var httpClient = _httpClientFactory.CreateClient(ClientKey);
            return httpClient.SendAsync(request);
        });
        return await ProcessAuthenticationResponse(response);
    }

    private async UniTask<bool> ProcessAuthenticationResponse(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to refresh: {StatusCode}", response.StatusCode);
            ResetData();
            return false;
        }

        try
        {
            string content = await response.Content.ReadAsStringAsync();
            AuthenticationResource resource = JsonConvert.DeserializeObject<AuthenticationResource>(content);
            if (resource == null ||
                string.IsNullOrWhiteSpace(resource.AccessToken) ||
                string.IsNullOrWhiteSpace(resource.RefreshToken) ||
                !long.TryParse(resource.AccessTokenExpiry, out long accessTokenExpiry) ||
                !long.TryParse(resource.RefreshTokenExpiry, out long refreshTokenExpiry))
            {
                _logger.LogError("Authentication response was malformed");
                ResetData();
                return false;
            }

            DateTimeOffset parsedAccessExpiry = DateTimeOffset.FromUnixTimeSeconds(accessTokenExpiry);
            DateTimeOffset parsedRefreshExpiry = DateTimeOffset.FromUnixTimeSeconds(refreshTokenExpiry);
            if (parsedAccessExpiry <= DateTimeOffset.UtcNow || parsedRefreshExpiry <= parsedAccessExpiry)
            {
                _logger.LogError("Authentication response contained invalid expiry values");
                ResetData();
                return false;
            }

            _accessToken = resource.AccessToken;
            _refreshToken = resource.RefreshToken;
            _accessTokenExpiry = parsedAccessExpiry;
            _refreshTokenExpiry = parsedRefreshExpiry;
            return true;
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to parse authentication response");
            ResetData();
            return false;
        }
    }

    private void ResetData()
    {
        _accessToken = null;
        _refreshToken = null;
        _accessTokenExpiry = DateTimeOffset.MinValue;
        _refreshTokenExpiry = DateTimeOffset.MinValue;
    }

    private void AddHeaders(HttpRequestMessage request, bool isAuthenticated)
    {
        if (isAuthenticated)
        {
            request.Headers.Add("Authorization", "Bearer " + _accessToken);
        }
    }
}
