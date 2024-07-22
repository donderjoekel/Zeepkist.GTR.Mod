using System;
using BepInEx.Logging;
using Serilog;
using Serilog.Configuration;

namespace TNRD.Zeepkist.GTR.Logging;

public static class BepInExSinkExtensions
{
    public static LoggerConfiguration BepInEx(
        this LoggerSinkConfiguration loggerConfiguration,
        ManualLogSource logSource,
        IFormatProvider formatProvider = null)
    {
        return loggerConfiguration.Sink(new BepInExSink(logSource, formatProvider));
    }
}
