namespace TNRD.Zeepkist.GTR.Mod.Api.Levels.Models;

public class CreateLevelRequestModel
{
    public string Uid { get; set; }
    public string Wid { get; set; }

    public string Name { get; set; }
    public string Author { get; set; }
    
    public bool IsValid { get; set; }

    public float TimeAuthor { get; set; }
    public float TimeGold { get; set; }
    public float TimeSilver { get; set; }
    public float TimeBronze { get; set; }
    
    public string Thumbnail { get; set; }
}
