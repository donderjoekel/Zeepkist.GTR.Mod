using System.Collections.Generic;
using Newtonsoft.Json;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

internal sealed class SphereSample
{
    [JsonProperty("time")] public double Time { get; set; }
    [JsonProperty("position")] public double[] Position { get; set; }
    [JsonProperty("radius")] public double Radius { get; set; }
}
internal sealed class TriggerEvidence
{
    [JsonProperty("blockUid")] public string BlockUid { get; set; }
    [JsonProperty("shape")] public string Shape { get; set; }
    [JsonProperty("finish")] public bool Finish { get; set; }
    [JsonProperty("rawTime")] public double RawTime { get; set; }
    [JsonProperty("adjustedTime")] public double AdjustedTime { get; set; }
    [JsonProperty("velocityKmh")] public double VelocityKmh { get; set; }
    [JsonProperty("sample")] public SphereSample Sample { get; set; }
}
internal sealed class RunEvidence
{
    [JsonProperty("sphereSamplingVersion", DefaultValueHandling = DefaultValueHandling.Ignore)] public int SphereSamplingVersion { get; set; }
    [JsonProperty("runUuid")] public string RunUuid { get; set; }
    [JsonProperty("submissionLevel")] public string SubmissionLevel { get; set; }
    [JsonProperty("levelUid")] public string LevelUid { get; set; }
    [JsonProperty("canonicalHash")] public string CanonicalHash { get; set; }
    [JsonProperty("initialTime")] public double InitialTime { get; set; }
    [JsonProperty("physicsInterval")] public double PhysicsInterval { get; set; }
    [JsonProperty("samples")] public List<SphereSample> Samples = new();
    [JsonProperty("events")] public List<TriggerEvidence> Events = new();
}
