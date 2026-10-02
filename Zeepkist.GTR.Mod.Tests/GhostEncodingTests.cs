using EasyCompressor;
using ProtoBuf;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Ghosting.Recording;
using Data = TNRD.Zeepkist.GTR.Ghosting.Recording.Data;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostEncodingTests
{
    [Fact]
    public void Snapshot_PreservesWireFields_AndRagdollTransitionDeltas()
    {
        var header = Header("first", 42);
        var frames = Frames();
        using var snapshot = new GhostRecorder.Snapshot(header, frames);
        using var compressed = new MemoryStream();
        snapshot.Write(compressed);
        compressed.Position = 0;
        using var decoded = new MemoryStream();
        new LZMACompressor().Decompress(compressed, decoded);
        decoded.Position = 0;
        Data.Ghost ghost = Serializer.Deserialize<Data.Ghost>(decoded);

        Assert.Equal(7, ghost.Version);
        Assert.Equal(42UL, ghost.SteamId);
        Assert.Equal("first", ghost.TaggedUsername);
        Assert.Equal("#abcdef", ghost.Color);
        Assert.Equal(123, ghost.Cosmetics.Hat);
        Assert.Equal(1, ghost.InitialFrame.Position.X);
        Assert.Equal(255, ghost.InitialFrame.Speed);
        Assert.Equal(127, ghost.InitialFrame.Steering);
        Assert.Equal(Data.InputFlags.Horn, ghost.InitialFrame.InputFlags);
        Assert.Equal(Data.SoapboxFlags.FrontLeft | Data.SoapboxFlags.RearRight, ghost.InitialFrame.SoapboxFlags);
        Assert.Equal(GroundedWheelState.HasFront, ghost.InitialFrame.GroundedWheelState);
        Assert.Equal(SlippingWheelState.HasRearRight, ghost.InitialFrame.SlippingWheelState);
        Assert.Equal(MaterialPhysicsState.Tarmac, ghost.InitialFrame.MaterialPhysicsState);
        Assert.Equal(200_000, ghost.InitialFrame.LocalVelocity.X);
        Assert.Equal(300, ghost.InitialFrame.LocalAngularVelocity.Y);
        Assert.Equal(-100_000, ghost.InitialFrame.LocalGForce.Y);
        Assert.True(ghost.InitialFrame.ParkingBlockState);
        Assert.True(ghost.InitialFrame.MonorailState);
        Assert.False(ghost.InitialFrame.RagdollState);
        Assert.Equal(2, ghost.DeltaFrames.Count);
        Data.DeltaFrame transition = ghost.DeltaFrames[0];
        Assert.Equal(0.02f, transition.Time);
        Assert.Equal(100_000, transition.Position.X);
        Assert.Equal(9_000, transition.Rotation.Y);
        Assert.Equal(Data.InputFlags.ArmsUp | Data.InputFlags.Braking, transition.InputFlags);
        Assert.Equal(0, transition.Speed);
        Assert.Equal(255, transition.Steering);
        Assert.True(transition.RagdollState);
        Assert.Equal(1_000_000, transition.RagdollPosition.X); // Transition stores absolute ragdoll transform.
        Assert.Equal(4_500, transition.RagdollRotation.Y);
        Data.DeltaFrame continuation = ghost.DeltaFrames[1];
        Assert.Equal(0.037f, continuation.Time); // Exact finish sample survives serialization.
        Assert.Equal(Data.InputFlags.Horn, continuation.InputFlags);
        Assert.Equal(100_000, continuation.RagdollPosition.X);
        Assert.Equal(100, continuation.RagdollRotation.Y);
    }

    [Fact]
    public async Task SeparateSnapshots_PreserveMetadata_AndUseIndependentCompressors()
    {
        using var first = new GhostRecorder.Snapshot(Header("first", 1), Frames());
        using var second = new GhostRecorder.Snapshot(Header("second", 2), Frames());
        var ghosts = await Task.WhenAll(Task.Run(() => Decode(first)), Task.Run(() => Decode(second)));
        Assert.Equal("first", ghosts[0].TaggedUsername);
        Assert.Equal(1UL, ghosts[0].SteamId);
        Assert.Equal("second", ghosts[1].TaggedUsername);
        Assert.Equal(2UL, ghosts[1].SteamId);
        Assert.Equal(ghosts[0].DeltaFrames[1].Time, ghosts[1].DeltaFrames[1].Time);
    }

    [Fact]
    public void Snapshot_CannotEncodeTwice_AndDisposeReleasesFrames()
    {
        var header = Header("player", 1);
        using var snapshot = new GhostRecorder.Snapshot(header, Frames());
        using var output = new MemoryStream();
        snapshot.Write(output);
        Assert.Throws<InvalidOperationException>(() => snapshot.Write(output));
        snapshot.Dispose();
        Assert.Null(header.DeltaFrames);
        Assert.Throws<ObjectDisposedException>(() => snapshot.Write(output));
    }

    [Theory]
    [InlineData(typeof(V1Ghost.Frame))]
    [InlineData(typeof(V2Ghost.Frame))]
    [InlineData(typeof(V3Ghost.Frame))]
    [InlineData(typeof(V4Ghost.Frame))]
    [InlineData(typeof(V5Ghost.Frame))]
    [InlineData(typeof(V6Ghost.Frame))] // V7 uses V6 playback frames.
    public void DecodedFrames_AreReadonlyValues(Type frameType)
    {
        Assert.True(frameType.IsValueType);
        Assert.Contains(frameType.CustomAttributes,
            attribute => attribute.AttributeType.FullName == "System.Runtime.CompilerServices.IsReadOnlyAttribute");
        Assert.All(frameType.GetProperties(), property => Assert.Null(property.SetMethod));
    }

    private static Data.Ghost Header(string name, ulong steamId) => new()
    {
        Version = 7,
        SteamId = steamId,
        TaggedUsername = name,
        Color = "#abcdef",
        Cosmetics = new Data.Cosmetics { Hat = 123 }
    };

    private static List<GhostRecorder.Frame> Frames()
    {
        GhostRecorder.Frame first = new()
        {
            Position = new(1, 2, 3),
            Speed = 300,
            Horn = true,
            WheelState = WheelState.HasFrontLeft | WheelState.HasRearRight,
            GroundedWheelState = GroundedWheelState.HasFront,
            SlippingWheelState = SlippingWheelState.HasRearRight,
            MaterialPhysicsState = MaterialPhysicsState.Tarmac,
            LocalVelocity = new(2, 0, 0),
            LocalAngularVelocity = new(0, 3, 0),
            LocalGForce = new(1, -1),
            ParkingBlockState = true,
            MonorailState = true
        };
        GhostRecorder.Frame transition = first;
        transition.Time = 0.02f;
        transition.Position = new(2, 2, 3);
        transition.Rotation = new(0, 90, 0);
        transition.Speed = -5;
        transition.Steering = 1;
        transition.Horn = false;
        transition.ArmsUp = true;
        transition.Braking = true;
        transition.RagdollState = true;
        transition.RagdollPosition = new(10, 20, 30);
        transition.RagdollRotation = new(0, 45, 0);
        GhostRecorder.Frame finish = transition;
        finish.Time = 0.037f;
        finish.ArmsUp = false;
        finish.Braking = false;
        finish.Horn = true;
        finish.RagdollPosition = new(11, 20, 30);
        finish.RagdollRotation = new(0, 46, 0);
        return [first, transition, finish];
    }

    private static Data.Ghost Decode(GhostRecorder.Snapshot snapshot)
    {
        using var compressed = new MemoryStream();
        snapshot.Write(compressed);
        compressed.Position = 0;
        using var decoded = new MemoryStream();
        new LZMACompressor().Decompress(compressed, decoded);
        decoded.Position = 0;
        return Serializer.Deserialize<Data.Ghost>(decoded);
    }
}
