using System;

namespace TNRD.Zeepkist.GTR.UI.Medals;

public sealed class LevelMedalTargets
{
    public double? Bronze { get; set; }
    public double? Silver { get; set; }
    public double? Gold { get; set; }
    public double? Author { get; set; }
    public double? Expert { get; set; }
    public double? Apex { get; set; }

    public static long? Milliseconds(double? seconds) => seconds is > 0 and < 1e12
        ? (long)Math.Floor(seconds.Value * 1000 + 0.5)
        : null;

    public int? Classify(double? seconds, bool completed = true)
    {
        long? time = completed ? Milliseconds(seconds) : null;
        if (!(time > 0)) return null;
        if (time <= Milliseconds(Apex)) return (int)MedalTier.Apex;
        if (time <= Milliseconds(Expert)) return (int)MedalTier.Expert;
        if (time <= Milliseconds(Author)) return (int)MedalTier.Author;
        if (time <= Milliseconds(Gold)) return (int)MedalTier.Gold;
        if (time <= Milliseconds(Silver)) return (int)MedalTier.Silver;
        if (time <= Milliseconds(Bronze)) return (int)MedalTier.Bronze;
        return null;
    }
}
