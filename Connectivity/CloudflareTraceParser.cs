using System;

namespace TNRD.Zeepkist.GTR.Connectivity;

internal static class CloudflareTraceParser
{
    public static string ParseCountryCode(string response)
    {
        if (string.IsNullOrWhiteSpace(response))
            return null;

        string[] lines = response.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
        foreach (string line in lines)
        {
            if (!line.StartsWith("loc=", StringComparison.Ordinal))
                continue;

            string countryCode = line.Substring("loc=".Length).Trim();
            if (countryCode.Length != 2 ||
                !IsAsciiLetter(countryCode[0]) ||
                !IsAsciiLetter(countryCode[1]))
                return null;

            return countryCode.ToUpperInvariant();
        }

        return null;
    }

    private static bool IsAsciiLetter(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }
}
