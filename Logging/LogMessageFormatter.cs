using System;

namespace TNRD.Zeepkist.GTR.Logging;

public static class LogMessageFormatter
{
    public static string Format(string timestamp, string message, Exception exception)
    {
        string formatted = timestamp + " " + message;
        return exception == null
            ? formatted
            : formatted + Environment.NewLine + exception;
    }
}
