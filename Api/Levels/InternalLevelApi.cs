using System;
using BepInEx.Logging;
using TNRD.Zeepkist.GTR.FluentResults;
using ZeepSDK.Level;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Mod.Api.Levels;

public static class InternalLevelApi
{
    private static readonly ManualLogSource logger = LoggerFactory.GetLogger(typeof(InternalLevelApi));

    public static string CurrentLevelHash { get; private set; }

    public static event Action LevelCreating;
    public static event Action LevelCreated;

    public static Result Create()
    {
        LevelCreating?.Invoke();
        CurrentLevelHash = null;
        LevelScriptableObject level = PlayerManager.Instance.currentMaster.GlobalLevel;

        if (level == null)
        {
            logger.LogError("No level available!");
            return Result.Fail("No level available");
        }

        try
        {
            CurrentLevelHash = LevelApi.GetLevelHash(level);
        }
        catch (Exception e)
        {
            logger.LogError("Unable to hash level: " + e);
            return Result.Fail("Unable to hash level");
        }

        LevelCreated?.Invoke();
        return Result.Ok();
    }
}
