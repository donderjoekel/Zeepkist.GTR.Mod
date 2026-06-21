using System;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Steamworks;
using Steamworks.Data;
using ZeepSDK.External.Cysharp.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Api;

public class ApiHttpClient
{
    public const string ClientKey = "API";

    private const int MaxRetryCount = 3;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ApiHttpClient> _logger;
    private readonly SemaphoreSlim _authenticationLock = new(1, 1);
    private readonly Random _jitter = new();
    private readonly object _jitterLock = new();

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
    }

    private static string CreateAuthenticationTicket()
    {
        AuthTicket authSessionTicket = SteamUser.GetAuthSessionTicket(new NetIdentity());
        StringBuilder stringBuilder = new();
        foreach (byte b in authSessionTicket.Data)
            stringBuilder.AppendFormat("{0:x2}", b);
        return stringBuilder.ToString();
    }

    public async UniTask<HttpResponseMessage> PostAsync(string url, object data)
    {
        if (!await LoginOrRefresh())
            return CreateAuthenticationFailure("Failed to authenticate");

        bool allowTransientRetries = !string.Equals(url, "record/submit", StringComparison.OrdinalIgnoreCase);
        string accessToken = _accessToken;
        HttpResponseMessage response = await SendPostAsync(url, data, true, allowTransientRetries);
        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        response.Dispose();
        if (string.Equals(accessToken, _accessToken, StringComparison.Ordinal))
            _accessTokenExpiry = DateTimeOffset.MinValue;

        if (!await LoginOrRefresh())
            return CreateAuthenticationFailure("Failed to re-authenticate");

        return await SendPostAsync(url, data, true, allowTransientRetries);
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

        using HttpResponseMessage response = await SendPostAsync("auth/login", data, false, true);
        return await ProcessAuthenticationResponse(response);
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

        using HttpResponseMessage response = await SendPostAsync("auth/refresh", data, false, true);
        return await ProcessAuthenticationResponse(response);
    }

    private async Task<HttpResponseMessage> SendPostAsync(
        string url,
        object data,
        bool authenticated,
        bool allowTransientRetries)
    {
        string json = JsonConvert.SerializeObject(data);
        int retryCount = 0;

        while (true)
        {
            try
            {
                using HttpRequestMessage request = new(HttpMethod.Post, url);
                if (authenticated)
                    request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + _accessToken);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                HttpClient httpClient = _httpClientFactory.CreateClient(ClientKey);
                HttpResponseMessage response = await httpClient.SendAsync(request);

                if (!allowTransientRetries || !IsTransient(response.StatusCode) || retryCount >= MaxRetryCount)
                    return response;

                TimeSpan delay = GetRetryDelay(response, retryCount++);
                response.Dispose();
                _logger.LogWarning("Transient response, retrying in {Delay} ({RetryCount})", delay, retryCount);
                await Task.Delay(delay);
            }
            catch (Exception e) when (IsTransient(e) && allowTransientRetries && retryCount < MaxRetryCount)
            {
                TimeSpan delay = GetRetryDelay(null, retryCount++);
                _logger.LogWarning(e, "Transient request failure, retrying in {Delay} ({RetryCount})", delay, retryCount);
                await Task.Delay(delay);
            }
        }
    }

    private static bool IsTransient(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout ||
               (int)statusCode == 429 ||
               (int)statusCode >= 500;
    }

    private static bool IsTransient(Exception exception)
    {
        return exception is HttpRequestException || exception is TaskCanceledException;
    }

    private TimeSpan GetRetryDelay(HttpResponseMessage response, int retryCount)
    {
        if (response?.Headers.RetryAfter?.Delta is TimeSpan retryAfterDelta)
            return retryAfterDelta > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : retryAfterDelta;

        if (response?.Headers.RetryAfter?.Date is DateTimeOffset retryAfterDate)
        {
            TimeSpan serverDelay = retryAfterDate - DateTimeOffset.UtcNow;
            if (serverDelay > TimeSpan.Zero)
                return serverDelay > TimeSpan.FromSeconds(30) ? TimeSpan.FromSeconds(30) : serverDelay;
        }

        int jitterMilliseconds;
        lock (_jitterLock)
            jitterMilliseconds = _jitter.Next(100, 501);
        return TimeSpan.FromSeconds(Math.Pow(2, retryCount)) + TimeSpan.FromMilliseconds(jitterMilliseconds);
    }

    private async UniTask<bool> ProcessAuthenticationResponse(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Authentication failed: {StatusCode}", response.StatusCode);
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
                !long.TryParse(resource.AccessTokenExpiry, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out long accessTokenExpiry) ||
                !long.TryParse(resource.RefreshTokenExpiry, NumberStyles.Integer, CultureInfo.InvariantCulture,
                    out long refreshTokenExpiry))
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

    private static HttpResponseMessage CreateAuthenticationFailure(string reason)
    {
        return new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            ReasonPhrase = reason
        };
    }

    private void ResetData()
    {
        _accessToken = null;
        _refreshToken = null;
        _accessTokenExpiry = DateTimeOffset.MinValue;
        _refreshTokenExpiry = DateTimeOffset.MinValue;
    }
}
