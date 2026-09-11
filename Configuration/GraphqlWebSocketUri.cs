using System;

namespace TNRD.Zeepkist.GTR.Configuration;

public static class GraphqlWebSocketUri
{
    public static Uri FromHttp(Uri uri)
    {
        if (uri == null)
            throw new ArgumentNullException(nameof(uri));

        UriBuilder builder = new(uri)
        {
            Scheme = uri.Scheme switch
            {
                "https" => "wss",
                "http" => "ws",
                _ => throw new ArgumentException("GraphQL URL must use HTTP or HTTPS", nameof(uri))
            }
        };

        return builder.Uri;
    }
}
