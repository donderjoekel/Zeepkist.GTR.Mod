using System.Collections.Generic;
using Newtonsoft.Json;
using ProtoBuf;

namespace TNRD.Zeepkist.GTR.Ghosting.Recording;

[ProtoContract]
public class GhostStatistics
{
    [ProtoMember(1), JsonProperty("frameCount")] public int FrameCount { get; set; }
    [ProtoMember(2), JsonProperty("duration")] public float Duration { get; set; }
    [ProtoMember(3), JsonProperty("distanceTravelled")] public float DistanceTravelled { get; set; }
    [ProtoMember(4), JsonProperty("distanceInAir")] public float DistanceInAir { get; set; }
    [ProtoMember(5), JsonProperty("distanceOnGround")] public float DistanceOnGround { get; set; }
    [ProtoMember(6), JsonProperty("timeInAir")] public float TimeInAir { get; set; }
    [ProtoMember(7), JsonProperty("timeOnGround")] public float TimeOnGround { get; set; }
    [ProtoMember(8), JsonProperty("averageSpeed")] public float AverageSpeed { get; set; }
    [ProtoMember(9), JsonProperty("topSpeed")] public float TopSpeed { get; set; }
    [ProtoMember(10), JsonProperty("armsUpCount")] public int ArmsUpCount { get; set; }
    [ProtoMember(11), JsonProperty("armsUpTime")] public float ArmsUpTime { get; set; }
    [ProtoMember(12), JsonProperty("brakeCount")] public int BrakeCount { get; set; }
    [ProtoMember(13), JsonProperty("brakeTime")] public float BrakeTime { get; set; }
    [ProtoMember(14), JsonProperty("turnLeftCount")] public int TurnLeftCount { get; set; }
    [ProtoMember(15), JsonProperty("turnLeftTime")] public float TurnLeftTime { get; set; }
    [ProtoMember(16), JsonProperty("turnRightCount")] public int TurnRightCount { get; set; }
    [ProtoMember(17), JsonProperty("turnRightTime")] public float TurnRightTime { get; set; }
    [ProtoMember(18), JsonProperty("hornCount")] public int HornCount { get; set; }
    [ProtoMember(19), JsonProperty("hornTime")] public float HornTime { get; set; }
    [ProtoMember(20), JsonProperty("soapTime")] public float SoapTime { get; set; }
    [ProtoMember(21), JsonProperty("offroadTime")] public float OffroadTime { get; set; }
    [ProtoMember(22), JsonProperty("paragliderTime")] public float ParagliderTime { get; set; }
    [ProtoMember(23), JsonProperty("surfaceDistance")] public Dictionary<string, float> SurfaceDistance { get; set; } = new();
    [ProtoMember(24), JsonProperty("surfaceTime")] public Dictionary<string, float> SurfaceTime { get; set; } = new();
}
