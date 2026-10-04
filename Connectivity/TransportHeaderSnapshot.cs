using Steamworks;

namespace TNRD.Zeepkist.GTR.Connectivity;

/// <summary>Capture on Unity thread only after startup data is ready. Transport workers reuse strings.</summary>
internal static class TransportHeaderSnapshot
{
    internal static bool TryCapture(string gtrVersion, out (string Name, string Value)[] headers)
    {
        headers = null;
        if (!SteamClient.IsValid || !SteamClient.IsLoggedOn)
            return false;

        PlayerManager playerManager = PlayerManager.Instance;
        if (playerManager == null)
            return false;
        var gameVersion = playerManager.version;
        if (gameVersion == null)
            return false;

        headers = new[]
        {
            ("X-Zeepkist-Version", $"{gameVersion.version}.{gameVersion.patch}"),
            ("X-Zeepkist-Major-Version", gameVersion.version.ToString()),
            ("X-GTR-Version", gtrVersion),
            ("X-Steam-ID", SteamClient.SteamId.ToString())
        };
        return true;
    }
}
