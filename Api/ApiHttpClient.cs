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
    private readonly AsyncPolicy<HttpResponseMessage> _policy;

    private string _accessToken;
    private string _refreshToken;
    private DateTimeOffset _accessTokenExpiry;
    private DateTimeOffset _refreshTokenExpiry;

    private bool NeedsLogin => string.IsNullOrEmpty(_accessToken) || DateTimeOffset.UtcNow > _refreshTokenExpiry;
    private bool NeedsRefresh => DateTimeOffset.UtcNow > _accessTokenExpiry;

    public ApiHttpClient(HttpClient httpClient, ConfigService configService, ILogger<ApiHttpClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri(configService.ApiUrl.Value);

        AsyncRetryPolicy<HttpResponseMessage> failurePolicy = Policy
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

        _policy = Policy.WrapAsync(failurePolicy, unauthorizedPolicy);
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
        return await _policy.ExecuteAsync(
            () =>
            {
                HttpRequestMessage request = new(HttpMethod.Post, url);
                request.Headers.Add("Authorization", "Bearer " + _accessToken);
                string json = JsonConvert.SerializeObject(data);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return _httpClient.SendAsync(request);
            });
    }

    public async UniTask<bool> Login()
    {
        LoginPostResource data = new()
        {
            ModVersion = MyPluginInfo.PLUGIN_VERSION,
            AuthenticationTicket = CreateAuthenticationTicket(),
            SteamId = SteamClient.SteamId
        };

        HttpResponseMessage response = await _policy.ExecuteAsync(
            () =>
            {
                HttpRequestMessage request = new(HttpMethod.Post, "Authentication/login");
                string json = JsonConvert.SerializeObject(data);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");
                return _httpClient.SendAsync(request);
            });
        await ProcessAuthenticationResponse(response);
        return !NeedsLogin && !NeedsRefresh;
    }

    private async UniTask Refresh()
    {
        HttpRequestMessage request = new(HttpMethod.Post, "Authentication/refresh");
        RefreshPostResource data = new()
        {
            ModVersion = MyPluginInfo.PLUGIN_VERSION,
            LoginToken = _accessToken,
            RefreshToken = _refreshToken,
            SteamId = SteamClient.SteamId
        };
        string json = JsonConvert.SerializeObject(data);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");
        HttpResponseMessage response = await _policy.ExecuteAsync(() => _httpClient.SendAsync(request));
        await ProcessAuthenticationResponse(response);
    }

    private async Task ProcessAuthenticationResponse(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Failed to refresh: {StatusCode}", response.StatusCode);
            ResetData();
            return;
        }

        // TODO: Should this be done in a safe way?
        string content = await response.Content.ReadAsStringAsync();
        AuthenticationResource resource = JsonConvert.DeserializeObject<AuthenticationResource>(content);

        _accessToken = resource.AccessToken;
        _refreshToken = resource.RefreshToken;
        _accessTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(long.Parse(resource.AccessTokenExpiry));
        _refreshTokenExpiry = DateTimeOffset.FromUnixTimeSeconds(long.Parse(resource.RefreshTokenExpiry));
    }

    private void ResetData()
    {
        _accessToken = null;
        _refreshToken = null;
        _accessTokenExpiry = DateTimeOffset.MinValue;
        _refreshTokenExpiry = DateTimeOffset.MinValue;
    }
}
