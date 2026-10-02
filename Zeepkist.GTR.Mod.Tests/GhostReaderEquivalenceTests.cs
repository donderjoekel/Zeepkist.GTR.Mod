using Microsoft.Extensions.Logging.Abstractions;
using ProtoBuf;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Ghosting.Readers;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using Data = TNRD.Zeepkist.GTR.Ghosting.Recording.Data;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostReaderEquivalenceTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void LegacyBinaryReaders_PreserveAllFrameValues(int version)
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(version);
            if (version > 1) WriteIdentity(writer);
            writer.Write(3);
            for (int i = 0; i < 3; i++)
            {
                writer.Write(i * 0.02f);
                writer.Write(i + 1f); writer.Write(i + 2f); writer.Write(i + 3f);
                writer.Write(i + 10f); writer.Write(i + 20f); writer.Write(i + 30f);
                if (version == 3)
                {
                    writer.Write(-0.5f); writer.Write(i == 1); writer.Write(i == 2);
                }
            }
        }
        IGhost ghost = version switch
        {
            1 => new V1Reader(null).Read(stream.ToArray()),
            2 => new V2Reader(null).Read(stream.ToArray()),
            _ => new V3Reader(null).Read(stream.ToArray())
        };
        Array frames = ((ICapturedFrames)ghost).Frames;
        Assert.Equal(3, frames.Length);
        for (int i = 0; i < 3; i++)
        {
            var frame = (IFrame)frames.GetValue(i);
            Assert.Equal(i * 0.02f, frame.Time);
            Assert.Equal(i + 1f, frame.Position.x);
            Assert.Equal(i + 2f, frame.Position.y);
            Assert.Equal(i + 3f, frame.Position.z);
            Assert.Equal(i + 10f, frame.Rotation.x); // Headless substitute records Euler input.
        }
        if (version == 3)
        {
            var typed = (V3Ghost.Frame[])frames;
            Assert.Equal(-0.5f, typed[1].Steering);
            Assert.True(typed[1].ArmsUp);
            Assert.True(typed[2].IsBraking);
        }
    }

    [Fact]
    public void V4Reader_PreservesResetAndLegacyDeltaSequence()
    {
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true))
        {
            writer.Write(4); WriteIdentity(writer); writer.Write((byte)4); writer.Write(4);
            WriteV4Frame(writer, 0, true);
            WriteV4Frame(writer, 1, false);
            WriteV4Frame(writer, 2, false);
            WriteV4Frame(writer, 3, true);
        }
        var ghost = (V4Ghost)new V4Reader(null).Read(stream.ToArray());
        var frames = (V4Ghost.Frame[])ghost.Frames;
        // Legacy V4 repeats earlier deltas within each reset group. Preserve existing playback sequence.
        Assert.Equal(new[] { 0f, 0.02f, 0.02f, 0.04f, 0.06f }, frames.Select(frame => frame.Time));
        Assert.Equal(new[] { 1f, 2f, 2f, 3f, 4f }, frames.Select(frame => frame.Position.x));
        Assert.All(frames, frame => Assert.True(frame.ArmsUp && frame.IsBraking));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void ProtobufReaders_PreservePositionsInputsAndRagdoll(int version)
    {
        var payload = new Data.Ghost
        {
            Version = version,
            SteamId = 42,
            Cosmetics = new(),
            TaggedUsername = "player",
            Color = "#ffffffff",
            InitialFrame = new(new(1, 2, 3), new(0, 90, 0), 40, 127, Data.InputFlags.Horn, Data.SoapboxFlags.FrontLeft),
            DeltaFrames =
            [
                new(0.02f, new(100_000, -100_000, 0), new(0, 4_500, 0), 50, 255,
                    Data.InputFlags.ArmsUp, Data.SoapboxFlags.Paraglider,
                    GroundedWheelState.HasFront, SlippingWheelState.HasRear, MaterialPhysicsState.Tarmac,
                    new(), new(), new(), false, false, version >= 6, new(1_000_000, 0, 0), new(0, 9_000, 0)),
                new(0.037f, new(100_000, 0, 0), new(0, 4_600, 0), 60, 0,
                    Data.InputFlags.Horn, Data.SoapboxFlags.RearRight,
                    GroundedWheelState.HasFront, SlippingWheelState.HasRear, MaterialPhysicsState.Tarmac,
                    new(), new(), new(), false, false, version >= 6, new(100_000, 0, 0), new(0, 100, 0))
            ]
        };
        using var stream = new MemoryStream();
        Serializer.Serialize(stream, payload);
        IGhost ghost = version switch
        {
            5 => new V5Reader(null, NullLogger<V5Reader>.Instance).Read(stream.ToArray()),
            6 => new V6Reader(null, NullLogger<V6Reader>.Instance).Read(stream.ToArray()),
            _ => new V7Reader(null, NullLogger<V7Reader>.Instance).Read(stream.ToArray())
        };
        Array frames = ((ICapturedFrames)ghost).Frames;
        Assert.Equal(3, frames.Length);
        var last = (IFrame)frames.GetValue(2);
        Assert.Equal(0.037f, last.Time);
        Assert.Equal(3, last.Position.x);
        Assert.Equal(1, last.Position.y);
        Assert.Equal(46, last.Rotation.y);
        if (version == 5)
        {
            var typed = (V5Ghost.Frame[])frames;
            Assert.Equal(-1, typed[2].Steering);
            Assert.Equal(InputFlags.Horn, typed[2].InputFlags);
        }
        else
        {
            var typed = (V6Ghost.Frame[])frames;
            Assert.False(typed[0].RagdollState);
            Assert.True(typed[1].RagdollState);
            Assert.Equal(10, typed[1].RagdollPosition.Value.x);
            Assert.Equal(11, typed[2].RagdollPosition.Value.x);
            Assert.Equal(91, typed[2].RagdollRotation.Value.y);
            Assert.Equal(InputFlags.Horn, typed[2].InputFlags);
            Assert.Equal(0, typed[2].Steering); // V6/V7 preserve legacy raw steering byte storage.
        }
    }

    private static void WriteIdentity(BinaryWriter writer)
    {
        writer.Write(42UL); writer.Write(1); writer.Write(2); writer.Write(3);
    }

    private static void WriteV4Frame(BinaryWriter writer, int index, bool reset)
    {
        writer.Write(index * 0.02f);
        if (reset)
        {
            writer.Write(index + 1f); writer.Write(2f); writer.Write(3f);
        }
        else
        {
            writer.Write((short)10_000); writer.Write((short)0); writer.Write((short)0);
        }
        writer.Write((short)0); writer.Write((short)0); writer.Write((short)0); writer.Write((short)10_000);
        writer.Write((byte)255); writer.Write((byte)3);
    }
}
