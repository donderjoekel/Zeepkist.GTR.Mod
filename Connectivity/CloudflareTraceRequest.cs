using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Connectivity;

internal sealed class CloudflareTraceRequest
{
    public const string Path = "cdn-cgi/trace";
    public static readonly Uri BaseAddress = new("https://zeepki.st/");
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(1);

    public Task<SpainTraceResult> Result { get; }

    public CloudflareTraceRequest(HttpClient httpClient, Action<Exception> onNoResponse = null)
    {
        Result = SendAsync(httpClient ?? throw new ArgumentNullException(nameof(httpClient)), onNoResponse);
    }

    private static async Task<SpainTraceResult> SendAsync(HttpClient httpClient, Action<Exception> onNoResponse)
    {
        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(Path);
            if (!response.IsSuccessStatusCode)
                return SpainTraceResult.FromResponse(response.StatusCode, null);

            string content = response.Content == null
                ? null
                : await response.Content.ReadAsStringAsync();
            return SpainTraceResult.FromResponse(
                response.StatusCode,
                CloudflareTraceParser.ParseCountryCode(content));
        }
        catch (Exception exception) when (exception is HttpRequestException || exception is OperationCanceledException)
        {
            onNoResponse?.Invoke(exception);
            return SpainTraceResult.NoResponse();
        }
        finally
        {
            httpClient.Dispose();
        }
    }
}
