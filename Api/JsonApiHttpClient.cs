using System;
using System.Net.Http;
using JsonApiSerializer;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using TNRD.Zeepkist.GTR.Configuration;
using ZeepSDK.External.Cysharp.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Api;

public class JsonApiHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JsonApiHttpClient> _logger;
    private readonly JsonApiSerializerSettings _jsonSettings;
    private readonly AsyncPolicy<HttpResponseMessage> _policy;

    public JsonApiHttpClient(HttpClient httpClient, ILogger<JsonApiHttpClient> logger, ConfigService configService)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(configService.JsonApiUrl.Value);
        _logger = logger;
        _jsonSettings = new JsonApiSerializerSettings();
        _policy = Policy
            .Handle<Exception>()
            .OrResult<HttpResponseMessage>(x => !x.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                3,
                retryCount =>
                {
                    _logger.LogWarning("Request failed, retrying ({RetryCount})", retryCount);
                    return TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                });
    }

    public async UniTask<HttpResponseMessage> GetAsync(string url)
    {
        return await _policy.ExecuteAsync(() => _httpClient.SendAsync(new HttpRequestMessage(HttpMethod.Get, url)));
    }

    public async UniTask<T> GetAsync<T>(string url)
    {
        HttpResponseMessage response = await GetAsync(url);
        if (!response.IsSuccessStatusCode)
            return default;

        string content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(content, _jsonSettings);
    }
}
