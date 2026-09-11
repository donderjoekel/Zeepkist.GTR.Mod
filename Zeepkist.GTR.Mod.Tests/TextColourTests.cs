using TNRD.Zeepkist.GTR.Utilities;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class TextColourTests
{
    [Fact]
    public void ProducesHexAndRichText()
    {
        Assert.Equal("#FACC15", TextColour.Yellow.ToHex());
        Assert.Equal("<color=#FB64B6>value</color>", TextColour.Pink.Wrap("value"));
    }

    [Fact]
    public void InterpolatesAndClampsColours()
    {
        Assert.Equal(TextColour.Yellow.ToHex(), TextColour.Yellow.InterpolateTo(TextColour.White, -1));
        Assert.Equal("#FDE68A", TextColour.Yellow.InterpolateTo(TextColour.White, 0.5));
        Assert.Equal(TextColour.White.ToHex(), TextColour.Yellow.InterpolateTo(TextColour.White, 2));
    }
}
