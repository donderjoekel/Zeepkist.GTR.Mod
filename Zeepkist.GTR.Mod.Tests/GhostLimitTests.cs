using System.Text;
using TNRD.Zeepkist.GTR.Ghosting;
using TNRD.Zeepkist.GTR.Ghosting.Readers;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class GhostLimitTests
{
    [Fact]
    public void LimitedMemoryStreamRejectsDataPastLimit()
    {
        using LimitedMemoryStream stream = new(4);
        stream.Write(Encoding.UTF8.GetBytes("1234"), 0, 4);

        Assert.Throws<InvalidDataException>(() => stream.WriteByte(0));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(GhostLimits.MaxFrames + 1)]
    public void ReadFrameCountRejectsInvalidCounts(int count)
    {
        using MemoryStream stream = new();
        using (BinaryWriter writer = new(stream, Encoding.UTF8, true))
            writer.Write(count);
        stream.Position = 0;
        using BinaryReader reader = new(stream);

        Assert.Throws<InvalidDataException>(() => GhostReaderValidation.ReadFrameCount(reader));
    }

    [Theory]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    [InlineData(float.NegativeInfinity)]
    public void RequireFiniteRejectsInvalidValues(float value)
    {
        Assert.Throws<InvalidDataException>(() => GhostReaderValidation.RequireFinite(value));
    }
}
