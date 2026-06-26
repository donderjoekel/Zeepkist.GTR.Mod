using ZeepSDK.Level;

namespace TNRD.Zeepkist.GTR.GraphQL;

public static class CurrentLevelGraphqlIdentity
{
    public static LevelGraphqlIdentity Create()
    {
        LevelScriptableObject currentLevel = LevelApi.CurrentLevel;
        return LevelGraphqlIdentity.FromValues(
            LevelApi.CurrentHashV2?.Hash,
            LevelApi.CurrentHash,
            currentLevel?.IsAdventureLevel ?? false,
            currentLevel?.UseAvonturenLevel ?? false);
    }
}
