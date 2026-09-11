using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;

namespace TNRD.Zeepkist.GTR.Connectivity;

public sealed class SpainRoutingService : IEagerService, ISpainRoutingState
{
    public const string TraceClientKey = "ZeepCentraal Country Trace";

    private readonly ConfigService _configService;
    private readonly Task<SpainTraceResult> _traceTask;
    private int _useAlternativeDomainsForSession;

    public string CountryCode { get; private set; }

    public string SelectedBackendUrl => ServiceUrlSelector.Select(
        _configService.UseLocalDevelopmentBackend.Value,
        _configService.UseAlternativeDomainsInSpain.Value,
        Volatile.Read(ref _useAlternativeDomainsForSession) == 1,
        ConfigService.ProductionBackendUrl,
        ConfigService.AlternativeSpainBackendUrl,
        ConfigService.LocalDevelopmentBackendUrl);

    public string SelectedGraphQLUrl => ServiceUrlSelector.Select(
        _configService.UseLocalDevelopmentGraphQL.Value,
        _configService.UseAlternativeDomainsInSpain.Value,
        Volatile.Read(ref _useAlternativeDomainsForSession) == 1,
        ConfigService.ProductionGraphQLUrl,
        ConfigService.AlternativeSpainGraphQLUrl,
        ConfigService.LocalDevelopmentGraphQLUrl);

    public SpainRoutingService(
        IHttpClientFactory httpClientFactory,
        ConfigService configService,
        ILogger<SpainRoutingService> logger)
    {
        _configService = configService;
        HttpClient traceClient = httpClientFactory.CreateClient(TraceClientKey);
        CloudflareTraceRequest traceRequest = new(
            traceClient,
            exception => logger.LogWarning(exception, "ZeepCentraal country trace did not respond"));
        _traceTask = StoreTraceResultAsync(traceRequest.Result, logger);
    }

    internal Task<SpainTraceResult> GetTraceResultAsync()
    {
        return _traceTask;
    }

    Task<SpainTraceResult> ISpainRoutingState.GetTraceResultAsync()
    {
        return GetTraceResultAsync();
    }

    bool ISpainRoutingState.IsProductionRequest(ServiceEndpoint endpoint, Uri requestUri)
    {
        Uri productionUri = GetBaseUri(
            endpoint,
            ConfigService.ProductionBackendUrl,
            ConfigService.ProductionGraphQLUrl);
        return HasSameAuthority(requestUri, productionUri);
    }

    Uri ISpainRoutingState.GetAlternativeRequestUri(ServiceEndpoint endpoint, Uri requestUri)
    {
        Uri alternativeUri = GetBaseUri(
            endpoint,
            ConfigService.AlternativeSpainBackendUrl,
            ConfigService.AlternativeSpainGraphQLUrl);
        UriBuilder builder = new(alternativeUri)
        {
            Path = requestUri.AbsolutePath,
            Query = requestUri.Query.Length > 0 ? requestUri.Query.Substring(1) : string.Empty,
            Fragment = requestUri.Fragment.Length > 0 ? requestUri.Fragment.Substring(1) : string.Empty
        };
        return builder.Uri;
    }

    void ISpainRoutingState.PromoteAlternativeDomains()
    {
        Interlocked.Exchange(ref _useAlternativeDomainsForSession, 1);
    }

    private async Task<SpainTraceResult> StoreTraceResultAsync(
        Task<SpainTraceResult> traceTask,
        ILogger<SpainRoutingService> logger)
    {
        SpainTraceResult result = await traceTask;
        CountryCode = result.CountryCode;

        if (result.HasHttpResponse &&
            ((int)result.StatusCode.Value < 200 || (int)result.StatusCode.Value >= 300))
            logger.LogWarning("ZeepCentraal country trace failed with status {StatusCode}", result.StatusCode);
        else if (result.HasHttpResponse && string.IsNullOrEmpty(result.CountryCode))
            logger.LogWarning("ZeepCentraal country trace response did not contain a valid loc value");

        return result;
    }

    private static Uri GetBaseUri(ServiceEndpoint endpoint, string backendUrl, string graphQlUrl)
    {
        return new Uri(endpoint == ServiceEndpoint.Backend ? backendUrl : graphQlUrl);
    }

    private static bool HasSameAuthority(Uri left, Uri right)
    {
        return string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
               left.Port == right.Port;
    }
}
