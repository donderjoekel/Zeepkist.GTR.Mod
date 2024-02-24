namespace TNRD.Zeepkist.GTR.Mod;

internal class SdkWrapper
{
    private static SDK.Sdk instance;

    public static SDK.Sdk Instance => instance ??= SDK.Sdk.Initialize(
        Plugin.ConfigApiUrl.Value,
        Plugin.ConfigAuthUrl.Value,
        Plugin.ConfigGraphQLUrl.Value,
        1106610501674348554,
        false,
        false);
}
