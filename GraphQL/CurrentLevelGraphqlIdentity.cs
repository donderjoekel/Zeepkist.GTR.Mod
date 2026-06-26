using ZeepSDK.Level;

namespace TNRD.Zeepkist.GTR.GraphQL;

public static class CurrentLevelGraphqlIdentity
{
    public static LevelGraphqlIdentity Create()
    {
        LevelScriptableObject currentLevel = LevelApi.CurrentLevel;
        LevelHashV2 currentHash = LevelApi.CurrentHashV2;
        return LevelGraphqlIdentity.FromValues(
            currentHash?.Hash,
            currentHash?.ZeepHash,
            currentLevel?.IsAdventureLevel ?? false,
            currentLevel?.UseAvonturenLevel ?? false);
    }
}
