using System;
using BepInEx.Logging;
using Serilog;
using Serilog.Configuration;
using Serilog.Events;

namespace TNRD.Zeepkist.GTR.Logging;

public static class BepInExSinkExtensions
{
    public static LoggerConfiguration BepInEx(
        this LoggerSinkConfiguration loggerConfiguration,
        ManualLogSource logSource,
        IFormatProvider formatProvider = null,
        LogEventLevel restrictedToMinimumLevel = LogEventLevel.Information)
    {
        return loggerConfiguration.Sink(new BepInExSink(logSource, formatProvider), restrictedToMinimumLevel);
    }
}
