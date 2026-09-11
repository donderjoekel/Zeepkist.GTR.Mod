using System;
using System.Net;

namespace TNRD.Zeepkist.GTR.Connectivity;

internal sealed class SpainTraceResult
{
    public string CountryCode { get; }
    public HttpStatusCode? StatusCode { get; }
    public bool HasHttpResponse => StatusCode.HasValue;
    public bool IsConfirmedSpain => string.Equals(CountryCode, "ES", StringComparison.OrdinalIgnoreCase);
    public bool IsFallbackEligible => IsConfirmedSpain || !HasHttpResponse;

    private SpainTraceResult(string countryCode, HttpStatusCode? statusCode)
    {
        CountryCode = countryCode;
        StatusCode = statusCode;
    }

    public static SpainTraceResult FromResponse(HttpStatusCode statusCode, string countryCode)
    {
        return new SpainTraceResult(countryCode, statusCode);
    }

    public static SpainTraceResult NoResponse()
    {
        return new SpainTraceResult(null, null);
    }
}
