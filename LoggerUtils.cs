using System.Collections.Generic;
using BepInEx.Logging;

namespace TNRD.Zeepkist.GTR.Mod;

public static class LoggerUtils
{
    private static readonly Dictionary<string, ManualLogSource> loggers = new Dictionary<string, ManualLogSource>();

    public static ManualLogSource Logger(this object obj)
    {
        string name = obj.GetType().Name;

        if (!loggers.TryGetValue(name, out ManualLogSource logger))
        {
            logger = Plugin.CreateLogger(name);
            loggers.Add(name, logger);
        }

        return logger;
    }
}
