using System;

namespace TNRD.Zeepkist.GTR.UI.Medals;

public enum MedalTier
{
    Bronze = 0,
    Silver = 1,
    Gold = 2,
    Author = 3,
    Expert = 4,
    Apex = 5
}

public sealed class MedalUpgrade
{
    public int First { get; }
    public int Last { get; }

    public MedalUpgrade(int first, int last)
    {
        First = first;
        Last = last;
    }
}

/// <summary>Compares confirmed result snapshots.</summary>
public sealed class MedalUpgradeTracker
{
    private bool _initialized;
    private string _context;
    private double? _time;
    private int? _medal;

    public static int? Normalize(int? tier) => tier is >= 0 and <= 5 ? tier : null;

    public MedalUpgrade Update(string context, double? time, int? medal, bool baseline = false)
    {
        medal = Normalize(medal);
        bool improved = time.HasValue && time.Value > 0 && !double.IsInfinity(time.Value) &&
                        (!_time.HasValue || time.Value < _time.Value);
        MedalUpgrade upgrade = !baseline && _initialized &&
                               string.Equals(context, _context, StringComparison.Ordinal) && improved &&
                               medal.HasValue && medal.Value > (_medal ?? -1)
            ? new MedalUpgrade((_medal ?? -1) + 1, medal.Value)
            : null;
        _initialized = true;
        _context = context;
        _time = time;
        _medal = medal;
        return upgrade;
    }

    public void Reset() => _initialized = false;
}
