using System.Net.Http;
using System.Threading.Tasks;

namespace TNRD.Zeepkist.GTR.Api;

public static class ApiResponseErrors
{
    public const int MaxBodyLength = 1000;

    /// <summary>Like EnsureSuccessStatusCode, but keeps the response body so the server's reason ends up in the log.</summary>
    public static async Task EnsureSuccessWithBodyAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
            return;

        string body = response.Content == null ? null : await response.Content.ReadAsStringAsync();
        throw new HttpRequestException(Describe((int)response.StatusCode, response.ReasonPhrase, body));
    }

    public static string Describe(int statusCode, string reasonPhrase, string body)
    {
        string status = $"{statusCode} ({reasonPhrase})";
        if (string.IsNullOrWhiteSpace(body))
            return status + " with an empty response body";

        body = body.Trim();
        if (body.Length > MaxBodyLength)
            body = body.Substring(0, MaxBodyLength) + "…";

        return status + ": " + body;
    }
}
