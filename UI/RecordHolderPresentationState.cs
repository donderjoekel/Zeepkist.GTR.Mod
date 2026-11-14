using System;
using TNRD.Zeepkist.GTR.UI.Medals;

namespace TNRD.Zeepkist.GTR.UI;

internal readonly struct RecordHolderUpdate
{
    public bool PersonalBestChanged { get; }
    public MedalUpgrade MedalUpgrade { get; }

    public RecordHolderUpdate(bool personalBestChanged, MedalUpgrade medalUpgrade)
    {
        PersonalBestChanged = personalBestChanged;
        MedalUpgrade = medalUpgrade;
    }
}

internal sealed class RecordHolderPresentationState
{
    private bool _initialized;
    private string _levelKey;
    private double? _time;
    private int? _rank;
    private int? _medal;
    public float RemainingTime { get; private set; }

    public RecordHolderUpdate Observe(string levelKey, PersonalBestHolder personalBest)
    {
        bool sameLevel = _initialized && string.Equals(levelKey, _levelKey, StringComparison.Ordinal);
        int? medal = MedalUpgradeTracker.Normalize(personalBest?.Medal);
        bool changed = personalBest != null && (!sameLevel || personalBest.Time != _time ||
                                                personalBest.Rank != _rank || medal != _medal);
        MedalUpgrade upgrade = sameLevel && changed && medal.HasValue && medal > (_medal ?? -1)
            ? personalBest.MedalUpgrade ?? new MedalUpgrade((_medal ?? -1) + 1, medal.Value)
            : null;
        _initialized = true;
        _levelKey = levelKey;
        _time = personalBest?.Time;
        _rank = personalBest?.Rank;
        _medal = medal;
        return new RecordHolderUpdate(changed, upgrade);
    }

    public void ResetTimer(float interval) => RemainingTime = interval;

    public bool Tick(float elapsed)
    {
        RemainingTime -= elapsed;
        return RemainingTime <= 0;
    }

    public void Reset()
    {
        _initialized = false;
        RemainingTime = 0;
    }
}
