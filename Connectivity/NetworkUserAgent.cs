using System;
using System.Net.Http;

namespace TNRD.Zeepkist.GTR.Connectivity;

internal sealed class NetworkUserAgent
{
    public const string HeaderName = "User-Agent";

    public string Value { get; }

    public NetworkUserAgent(string gtrVersion, string zeepSdkVersion)
    {
        if (string.IsNullOrWhiteSpace(gtrVersion))
            throw new ArgumentException("GTR version is required.", nameof(gtrVersion));
        if (string.IsNullOrWhiteSpace(zeepSdkVersion))
            throw new ArgumentException("ZeepSDK version is required.", nameof(zeepSdkVersion));

        Value = $"ZeepkistGTR/{gtrVersion} (ZeepSDK {zeepSdkVersion})";
    }

    public void Apply(HttpClient client)
    {
        if (client == null)
            throw new ArgumentNullException(nameof(client));

        client.DefaultRequestHeaders.UserAgent.Clear();
        client.DefaultRequestHeaders.UserAgent.ParseAdd(Value);
    }

    public void Apply(Action<string, string> setHeader)
    {
        if (setHeader == null)
            throw new ArgumentNullException(nameof(setHeader));

        setHeader(HeaderName, Value);
    }
}
