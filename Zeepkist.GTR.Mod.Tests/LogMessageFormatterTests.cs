using TNRD.Zeepkist.GTR.Logging;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class LogMessageFormatterTests
{
    [Fact]
    public void Format_WithoutException_PrefixesTimestamp()
    {
        Assert.Equal("12:34:56 Failed to submit record", LogMessageFormatter.Format("12:34:56", "Failed to submit record", null));
    }

    [Fact]
    public void Format_WithException_AppendsTypeMessageAndStackTrace()
    {
        Exception exception;
        try { throw new InvalidOperationException("ghost was empty"); }
        catch (InvalidOperationException e) { exception = e; }

        string formatted = LogMessageFormatter.Format("12:34:56", "Error while creating ghost", exception);

        Assert.StartsWith("12:34:56 Error while creating ghost" + Environment.NewLine, formatted);
        Assert.Contains("System.InvalidOperationException: ghost was empty", formatted);
        Assert.Contains(nameof(Format_WithException_AppendsTypeMessageAndStackTrace), formatted);
    }
}
