using System;
using System.Net.Http;

namespace TNRD.Zeepkist.GTR.Connectivity;

internal sealed class AlternativeDomainFallbackException : HttpRequestException
{
    public AlternativeDomainFallbackException(Exception innerException)
        : base("Alternative domain fallback request failed", innerException)
    {
    }
}
