namespace TNRD.Zeepkist.GTR.Configuration;

internal static class ServiceUrlSelector
{
    public static string Select(
        bool useLocalDevelopment,
        bool useConfiguredAlternativeDomains,
        bool useSessionAlternativeDomains,
        string productionUrl,
        string alternativeUrl,
        string localDevelopmentUrl)
    {
        if (useLocalDevelopment)
            return localDevelopmentUrl;

        return useConfiguredAlternativeDomains || useSessionAlternativeDomains
            ? alternativeUrl
            : productionUrl;
    }
}
