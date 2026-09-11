using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

#pragma warning disable CS0618 // HttpRequestMessage.Options is unavailable on target framework net472.

namespace TNRD.Zeepkist.GTR.Connectivity;

internal sealed class AlternativeDomainFallbackHandler : DelegatingHandler
{
    public const string TransportClientKey = "ZeepCentraal HTTP Transport";
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(10);

    private const string FallbackAttemptedProperty = "TNRD.Zeepkist.GTR.AlternativeFallbackAttempted";

    private readonly ISpainRoutingState _routingState;
    private readonly ServiceEndpoint _endpoint;
    private readonly Func<HttpClient> _transportClientFactory;

    public AlternativeDomainFallbackHandler(
        ISpainRoutingState routingState,
        ServiceEndpoint endpoint,
        Func<HttpClient> transportClientFactory)
    {
        _routingState = routingState ?? throw new ArgumentNullException(nameof(routingState));
        _endpoint = endpoint;
        _transportClientFactory = transportClientFactory ??
                                  throw new ArgumentNullException(nameof(transportClientFactory));
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        SpainTraceResult traceResult = await _routingState.GetTraceResultAsync();
        cancellationToken.ThrowIfCancellationRequested();
        BufferedRequest bufferedRequest = await BufferedRequest.CreateAsync(request);

        try
        {
            return await SendBufferedAsync(bufferedRequest, request.RequestUri, request, cancellationToken);
        }
        catch (OperationCanceledException) when (
            !cancellationToken.IsCancellationRequested &&
            traceResult.IsFallbackEligible &&
            _routingState.IsProductionRequest(_endpoint, request.RequestUri))
        {
            Uri alternativeUri = _routingState.GetAlternativeRequestUri(_endpoint, request.RequestUri);
            request.Properties[FallbackAttemptedProperty] = true;

            try
            {
                HttpResponseMessage response = await SendBufferedAsync(
                    bufferedRequest,
                    alternativeUri,
                    request,
                    cancellationToken);
                if (response.IsSuccessStatusCode)
                    _routingState.PromoteAlternativeDomains();
                return response;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is HttpRequestException || exception is OperationCanceledException)
            {
                throw new AlternativeDomainFallbackException(exception);
            }
        }
    }

    public static bool WasFallbackAttempted(HttpResponseMessage response)
    {
        return response?.RequestMessage != null &&
               response.RequestMessage.Properties.TryGetValue(FallbackAttemptedProperty, out object value) &&
               value is true;
    }

    private async Task<HttpResponseMessage> SendBufferedAsync(
        BufferedRequest bufferedRequest,
        Uri requestUri,
        HttpRequestMessage originalRequest,
        CancellationToken cancellationToken)
    {
        using HttpRequestMessage request = bufferedRequest.Create(requestUri);
        using HttpClient transportClient = _transportClientFactory();
        HttpResponseMessage response = await transportClient.SendAsync(request, cancellationToken);
        response.RequestMessage = originalRequest;
        return response;
    }

    private sealed class BufferedRequest
    {
        private readonly HttpMethod _method;
        private readonly Version _version;
        private readonly IReadOnlyCollection<KeyValuePair<string, IEnumerable<string>>> _headers;
        private readonly IReadOnlyCollection<KeyValuePair<string, object>> _properties;
        private readonly byte[] _content;
        private readonly IReadOnlyCollection<KeyValuePair<string, IEnumerable<string>>> _contentHeaders;

        private BufferedRequest(
            HttpMethod method,
            Version version,
            IReadOnlyCollection<KeyValuePair<string, IEnumerable<string>>> headers,
            IReadOnlyCollection<KeyValuePair<string, object>> properties,
            byte[] content,
            IReadOnlyCollection<KeyValuePair<string, IEnumerable<string>>> contentHeaders)
        {
            _method = method;
            _version = version;
            _headers = headers;
            _properties = properties;
            _content = content;
            _contentHeaders = contentHeaders;
        }

        public static async Task<BufferedRequest> CreateAsync(HttpRequestMessage request)
        {
            byte[] content = null;
            IReadOnlyCollection<KeyValuePair<string, IEnumerable<string>>> contentHeaders =
                Array.Empty<KeyValuePair<string, IEnumerable<string>>>();
            if (request.Content != null)
            {
                content = await request.Content.ReadAsByteArrayAsync();
                contentHeaders = CopyHeaders(request.Content.Headers);
            }

            return new BufferedRequest(
                request.Method,
                request.Version,
                CopyHeaders(request.Headers),
                CopyProperties(request.Properties),
                content,
                contentHeaders);
        }

        public HttpRequestMessage Create(Uri requestUri)
        {
            HttpRequestMessage request = new(_method, requestUri)
            {
                Version = _version
            };
            CopyHeaders(_headers, request.Headers);
            foreach (KeyValuePair<string, object> property in _properties)
                request.Properties[property.Key] = property.Value;

            if (_content == null)
                return request;

            request.Content = new ByteArrayContent(_content);
            CopyHeaders(_contentHeaders, request.Content.Headers);
            return request;
        }

        private static IReadOnlyCollection<KeyValuePair<string, IEnumerable<string>>> CopyHeaders(
            System.Net.Http.Headers.HttpHeaders headers)
        {
            List<KeyValuePair<string, IEnumerable<string>>> copy = new();
            foreach (KeyValuePair<string, IEnumerable<string>> header in headers)
                copy.Add(new KeyValuePair<string, IEnumerable<string>>(header.Key, new List<string>(header.Value)));
            return copy;
        }

        private static IReadOnlyCollection<KeyValuePair<string, object>> CopyProperties(
            IDictionary<string, object> properties)
        {
            List<KeyValuePair<string, object>> copy = new();
            foreach (KeyValuePair<string, object> property in properties)
                copy.Add(property);
            return copy;
        }

        private static void CopyHeaders(
            IEnumerable<KeyValuePair<string, IEnumerable<string>>> source,
            System.Net.Http.Headers.HttpHeaders destination)
        {
            foreach (KeyValuePair<string, IEnumerable<string>> header in source)
                destination.TryAddWithoutValidation(header.Key, header.Value);
        }
    }
}

#pragma warning restore CS0618
