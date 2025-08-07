using System;
using System.Net;
using System.Net.Http;
using System.Text;
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
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApiHttpClient> _logger;
    private readonly AsyncPolicy<HttpResponseMessage> _wrappedPolicy;
    private readonly AsyncPolicy<HttpResponseMessage> _failurePolicy;

    private readonly string _gameMajorVersion;
    private readonly string _gameVersion;
    private readonly string _modVersion;
    private readonly string _steamId;

    private string _accessToken;
    private string _refreshToken;
    private DateTimeOffset _accessTokenExpiry = DateTimeOffset.MinValue;
    private DateTimeOffset _refreshTokenExpiry = DateTimeOffset.MinValue;

    private bool NeedsLogin => string.IsNullOrEmpty(_accessToken) || DateTimeOffset.UtcNow > _refreshTokenExpiry;
    private bool NeedsRefresh => DateTimeOffset.UtcNow > _accessTokenExpiry;

    public ApiHttpClient(HttpClient httpClient, ConfigService configService, ILogger<ApiHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(configService.BackendUrl.Value);

        _gameMajorVersion = PlayerManager.Instance.version.version.ToString();
        _gameVersion = $"{_gameMajorVersion}.{PlayerManager.Instance.version.patch}";
        _modVersion = MyPluginInfo.PLUGIN_VERSION;
        _steamId = SteamClient.SteamId.ToString();

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
                return _httpClient.SendAsync(request);
            });
    }

    public async UniTask<bool> LoginOrRefresh()
    {
        if (NeedsLogin)
        {
            return await Login();
        }

        if (await Refresh())
        {
            return true;
        }

        return await Login();
    }

    public async UniTask<bool> Login()
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
                return _httpClient.SendAsync(request);
            });
        return await ProcessAuthenticationResponse(response);
    }

    private async UniTask<bool> Refresh()
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
            return _httpClient.SendAsync(request);
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

        // TODO: Should this be done in a safe way?
        string content = await response.Content.ReadAsStringAsync();
        AuthenticationResource resource = JsonConvert.DeserializeObject<AuthenticationResource>(content);

        _accessToken = resource.AccessToken;
        _refreshToken = resource.RefreshToken;
        _accessTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(long.Parse(resource.AccessTokenExpiry));
        _refreshTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(long.Parse(resource.RefreshTokenExpiry));
        return true;
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
        request.Headers.Add("X-Zeepkist-Version", _gameVersion);
        request.Headers.Add("X-Zeepkist-Major-Version", _gameMajorVersion);
        request.Headers.Add("X-GTR-Version", _modVersion);
        request.Headers.Add("X-Steam-ID", _steamId);

        if (isAuthenticated)
        {
            request.Headers.Add("Authorization", "Bearer " + _accessToken);
        }
    }
}
