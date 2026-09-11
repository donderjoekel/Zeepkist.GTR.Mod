using TNRD.Zeepkist.GTR.Configuration;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class ServiceUrlSelectorTests
{
    [Theory]
    [InlineData(false, false, false, "production")]
    [InlineData(false, false, true, "alternative")]
    [InlineData(false, true, false, "alternative")]
    [InlineData(false, true, true, "alternative")]
    [InlineData(true, false, false, "local")]
    [InlineData(true, false, true, "local")]
    [InlineData(true, true, false, "local")]
    [InlineData(true, true, true, "local")]
    public void SelectUsesExpectedPrecedence(
        bool useLocal,
        bool useConfiguredAlternative,
        bool useSessionAlternative,
        string expected)
    {
        string result = ServiceUrlSelector.Select(
            useLocal,
            useConfiguredAlternative,
            useSessionAlternative,
            "production",
            "alternative",
            "local");

        Assert.Equal(expected, result);
    }
}
