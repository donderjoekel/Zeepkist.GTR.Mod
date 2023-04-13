using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Mod.Api.Records.Models;

public class SubmitRecordRequestModel
{
    public int Level { get; set; }
    public int User { get; set; }
    public float Time { get; set; }
    public List<float> Splits { get; set; }
    public string GhostData { get; set; }
    public string ScreenshotData { get; set; }
    public string GameVersion { get; set; }
    public bool IsValid { get; set; }
}
