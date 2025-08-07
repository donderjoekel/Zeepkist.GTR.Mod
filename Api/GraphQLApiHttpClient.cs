using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Polly;
using Steamworks;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Json;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using Result = ZeepSDK.External.FluentResults.Result;

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
        _httpClient.BaseAddress = new Uri(configService.GraphQlUrl.Value);
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
        return await _policy.ExecuteAsync(async () =>
    {
        var request = new HttpRequestMessage(HttpMethod.Post, string.Empty)
        {
            Content = new StringContent(
                JsonConvert.SerializeObject(new { query, variables }),
                Encoding.UTF8,
                "application/json")
        };

        AddHeaders(request);

        return await _httpClient.SendAsync(request, ct);
    });
    }

    public async UniTask<Result<T>> PostAsync<T>(string query, object variables = null, CancellationToken ct = default)
    {
        HttpResponseMessage response = await PostAsync(query, variables, ct);

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        string content = string.Empty;

        try
        {
            content = await response.Content.ReadAsStringAsync();
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        try
        {
            return JsonConvert.DeserializeObject<T>(content, _settings);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }
    }

    private void AddHeaders(HttpRequestMessage request)
    {
        string gameMajorVersion = PlayerManager.Instance?.version?.version.ToString();
        string gameVersion = $"{gameMajorVersion}.{PlayerManager.Instance?.version?.patch}";
        string modVersion = MyPluginInfo.PLUGIN_VERSION ?? "unknown";
        string steamId = SteamClient.SteamId.ToString();

        request.Headers.Add("X-Zeepkist-Version", gameVersion);
        request.Headers.Add("X-Zeepkist-Major-Version", gameMajorVersion);
        request.Headers.Add("X-GTR-Version", modVersion);
        request.Headers.Add("X-Steam-ID", steamId);
    }
}
