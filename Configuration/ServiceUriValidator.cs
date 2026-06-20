using System;

namespace TNRD.Zeepkist.GTR.Configuration;

public static class ServiceUriValidator
{
    public static Uri ParseBaseAddress(string value, string settingName)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri))
            throw new InvalidOperationException($"{settingName} must be an absolute URL.");
        if (!string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidOperationException($"{settingName} must not contain credentials.");
        if (!IsAllowedScheme(uri))
            throw new InvalidOperationException($"{settingName} must use HTTPS. HTTP is allowed only for loopback.");
        return uri;
    }

    public static Uri ResolveCdnPath(string baseAddress, string path)
    {
        Uri cdnUri = ParseBaseAddress(baseAddress, "CDN URL");
        if (string.IsNullOrWhiteSpace(path) ||
            path.StartsWith("//", StringComparison.Ordinal) ||
            path.Contains("..") ||
            Uri.TryCreate(path, UriKind.Absolute, out _))
        {
            throw new InvalidOperationException("Ghost URL must be a relative CDN path.");
        }

        Uri cdnDirectory = cdnUri.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? cdnUri
            : new Uri(cdnUri.AbsoluteUri + "/");
        Uri resolved = new(cdnDirectory, path.TrimStart('/'));
        if (!SameOrigin(cdnUri, resolved))
            throw new InvalidOperationException("Ghost URL escaped configured CDN origin.");
        return resolved;
    }

    private static bool IsAllowedScheme(Uri uri)
    {
        return uri.Scheme == Uri.UriSchemeHttps ||
               (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback);
    }

    private static bool SameOrigin(Uri left, Uri right)
    {
        return string.Equals(left.Scheme, right.Scheme, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(left.Host, right.Host, StringComparison.OrdinalIgnoreCase) &&
               left.Port == right.Port;
    }
}
