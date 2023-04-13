using System.Collections.Generic;

namespace TNRD.Zeepkist.GTR.Mod.Api.Records.Models;

public class SubmitRecordResponseModel
{
    public int Level { get; set; }
    public int User { get; set; }
    public float Time { get; set; }
    public List<float> Splits { get; set; }
    public string GhostUrl { get; set; }
    public string ScreenshotUrl { get; set; }
    public string GameVersion { get; set; }
}
