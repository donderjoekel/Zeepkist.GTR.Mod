namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public interface IGhostReader
{
    /// <summary>
    /// 
    /// </summary>
    int Version { get; }

    /// <summary>
    /// 
    /// </summary>
    ulong SteamId { get; }

    /// <summary>
    /// 
    /// </summary>
    int SoapboxId { get; }

    /// <summary>
    /// 
    /// </summary>
    int HatId { get; }

    /// <summary>
    /// 
    /// </summary>
    int ColorId { get; }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="buffer"></param>
    void Read(byte[] buffer);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="time"></param>
    /// <returns></returns>
    FrameData GetFrameData(float time);
}
