using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Api;

public class RecordPostResource
{
    public string Level { get; set; } = null!;
    public float Time { get; set; }
    public List<float> Splits { get; set; } = null!;
    public List<float> Speeds { get; set; } = null!;
    public string GhostData { get; set; } = null!;
    public string GameVersion { get; set; } = null!;
    public string ModVersion { get; set; } = null!;
}
