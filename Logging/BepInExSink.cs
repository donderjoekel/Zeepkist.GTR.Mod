using System;
using BepInEx.Logging;
using Serilog.Core;
using Serilog.Events;

namespace TNRD.Zeepkist.GTR.Logging;

public class BepInExSink : ILogEventSink
{
    private readonly ManualLogSource _logSource;
    private readonly IFormatProvider _formatProvider;

    public BepInExSink(ManualLogSource logSource, IFormatProvider formatProvider)
    {
        _logSource = logSource;
        _formatProvider = formatProvider;
    }

    public void Emit(LogEvent logEvent)
    {
        string message = logEvent.RenderMessage(_formatProvider);
        message = DateTimeOffset.Now.ToString("HH:mm:ss") + " " + message;
        switch (logEvent.Level)
        {
            case LogEventLevel.Verbose:
                _logSource.Log(LogLevel.Debug, message);
                break;
            case LogEventLevel.Debug:
                _logSource.Log(LogLevel.Debug, message);
                break;
            case LogEventLevel.Information:
                _logSource.Log(LogLevel.Info, message);
                break;
            case LogEventLevel.Warning:
                _logSource.Log(LogLevel.Warning, message);
                break;
            case LogEventLevel.Error:
                _logSource.Log(LogLevel.Error, message);
                break;
            case LogEventLevel.Fatal:
                _logSource.Log(LogLevel.Fatal, message);
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
    }
}
