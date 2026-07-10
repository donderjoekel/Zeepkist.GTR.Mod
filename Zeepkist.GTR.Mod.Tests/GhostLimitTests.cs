using System.Text;
using TNRD.Zeepkist.GTR.Ghosting;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
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
    [InlineData(0, 0)]
    [InlineData(-1, 0)]
    [InlineData(GhostLimits.MaxCompressedBytes + 1, 0)]
    [InlineData(1024, 1024 * GhostLimits.MaxDecompressionRatio)]
    [InlineData(GhostLimits.MaxCompressedBytes, GhostLimits.MaxDecompressedBytes)]
    public void DecompressedLimitBoundsSizeAndRatio(int compressedBytes, int expected)
    {
        Assert.Equal(expected, GhostLimits.GetMaxDecompressedBytes(compressedBytes));
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

    [Fact]
    public void ReadProtobufVersionReadsV5PayloadHeader()
    {
        byte[] payload = { 0x08, 0x05, 0x10, 0xfc, 0xb0, 0x95 };

        Assert.Equal(5, GhostVersionReader.ReadProtobuf(payload));
    }

    [Fact]
    public void ReadBinaryVersionReadsLegacyPayloadHeader()
    {
        byte[] payload = BitConverter.GetBytes(4);

        Assert.Equal(4, GhostVersionReader.ReadBinary(payload));
    }

    [Theory]
    [InlineData(-1, null)]
    [InlineData(-100, null)]
    [InlineData(0, 0)]
    [InlineData(200, 200)]
    public void OfflineGhostLimitMapsConfiguredValueToGraphQlFirst(int configuredValue, int? expected)
    {
        Assert.Equal(expected, OfflineGhostLimit.ToGraphQlFirst(configuredValue));
    }
}
