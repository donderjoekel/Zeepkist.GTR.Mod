using Newtonsoft.Json.Linq;
using ProtoBuf;
using TNRD.Zeepkist.GTR.Ghosting.Recording.Data;
using TNRD.Zeepkist.GTR.Ghosting.Readers;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostV8Tests
{
    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public void SamplingRevisionPreservesLegacyJsonAndMarksCorrectedCaptures(int revision)
    {
        var evidence = new TNRD.Zeepkist.GTR.Ghosting.Recording.RunEvidence();
        Assert.Null(JObject.Parse(Newtonsoft.Json.JsonConvert.SerializeObject(evidence))["sphereSamplingVersion"]);
        evidence.SphereSamplingVersion = revision;
        string json = Newtonsoft.Json.JsonConvert.SerializeObject(evidence);
        Assert.Equal(revision, (int)JObject.Parse(json)["sphereSamplingVersion"]);
        Assert.Equal(revision, Newtonsoft.Json.JsonConvert.DeserializeObject<TNRD.Zeepkist.GTR.Ghosting.Recording.RunEvidence>(json).SphereSamplingVersion);
    }
    [Theory]
    [InlineData(503,true)] [InlineData(429,true)] [InlineData(408,true)]
    [InlineData(200,false)] [InlineData(400,false)] [InlineData(401,false)]
    public void RetryPolicyPreservesPermanentRejections(int status,bool retry)
    {
        Assert.Equal(retry,TNRD.Zeepkist.GTR.Ghosting.Recording.RecordingSubmissionRetry.ShouldRetry(status));
        Assert.Equal(2000,TNRD.Zeepkist.GTR.Ghosting.Recording.RecordingSubmissionRetry.DelayMilliseconds(99));
    }
    [Fact]
    public void SharedWireFixturePreservesHeaderAndInitialTimestamp()
    {
        var fixture = JObject.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "ghost-v8.json")));
        byte[] bytes = Convert.FromHexString((string)fixture["protobufHex"]);
        using var stream = new MemoryStream(bytes);
        var header = Serializer.Deserialize<Ghost>(stream);
        Assert.Equal(8, header.Version);
        Assert.Equal(42UL, header.SteamId);
        Assert.True(JToken.DeepEquals(fixture["evidence"], JObject.Parse(header.EvidenceJson)));
        var ghost = (ICapturedFrames)new V8Reader(null, NullLogger<V8Reader>.Instance).Read(bytes);
        Assert.Equal(1.2f, ((IFrame)ghost.Frames.GetValue(0)).Time);
        Assert.Equal(1f, ((IFrame)ghost.Frames.GetValue(1)).Position.x);
    }
}
