using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Json;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;

namespace TNRD.Zeepkist.GTR.Api;

public class GraphQLApiHttpClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GraphQLApiHttpClient> _logger;
    private readonly AsyncPolicy<HttpResponseMessage> _policy;
    private readonly JsonSerializerSettings _settings;

    public GraphQLApiHttpClient(
        HttpClient httpClient,
        ConfigService configService,
        ILogger<GraphQLApiHttpClient> logger)
    {
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(configService.GraphQLApiUrl.Value);
        _logger = logger;
        _settings = new JsonSerializerSettings
        {
            Converters = new List<JsonConverter>()
            {
                new JsonPathConverter()
            }
        };

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

    public async UniTask<HttpResponseMessage> PostAsync(
        string query,
        object variables = null,
        CancellationToken ct = default)
    {
        return await _policy.ExecuteAsync(
            () => _httpClient.PostAsync(
                string.Empty,
                new StringContent(
                    JsonConvert.SerializeObject(new { query, variables }),
                    Encoding.UTF8,
                    "application/json"),
                ct));
    }

    public async UniTask<Result<T>> PostAsync<T>(string query, object variables = null, CancellationToken ct = default)
    {
        HttpResponseMessage response = await PostAsync(query, variables, ct);
        if (!response.IsSuccessStatusCode)
            return Result.Fail(response.ReasonPhrase); // TODO: Improve error

        string content = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<T>(content, _settings);
    }
}
