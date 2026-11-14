using System;
using TNRD.Zeepkist.GTR.UI.Medals;

namespace TNRD.Zeepkist.GTR.Leaderboard;

/// <summary>One target lookup per loaded level; presentation baselines belong to the visible page.</summary>
public sealed class RoundMedalState
{
    public const string OfficialRoundTab = "ZeepSDK.Leaderboard.Pages.RoundLeaderboardTab";
    public const string OfficialChampionshipTab = "ZeepSDK.Leaderboard.Pages.ChampionshipLeaderboardTab";

    public static bool IsOfficialTab(string typeName, bool championship) =>
        typeName == (championship ? OfficialChampionshipTab : OfficialRoundTab);

    private readonly MedalUpgradeTracker _localTracker = new();
    private int? _page;
    public string LevelKey { get; private set; }
    public int Generation { get; private set; }
    public LevelMedalTargets Targets { get; private set; }

    public bool BeginLevel(string key)
    {
        if (key == null || string.Equals(key, LevelKey, StringComparison.Ordinal)) return false;
        ResetLevel();
        LevelKey = key;
        return true;
    }

    public bool SetTargets(int generation, LevelMedalTargets targets)
    {
        if (generation != Generation || LevelKey == null) return false;
        Targets = targets;
        ResetPresentation();
        return true;
    }

    public void ResetPresentation()
    {
        _localTracker.Reset();
        _page = null;
    }

    public void ResetLevel()
    {
        Generation++;
        LevelKey = null;
        Targets = null;
        ResetPresentation();
    }

    public MedalUpgrade ObserveLocal(int page, bool visible, double? time, bool completed)
    {
        if (!visible || Targets == null)
        {
            ResetPresentation();
            return null;
        }
        bool baseline = _page != page;
        _page = page;
        return _localTracker.Update(LevelKey, completed ? time : null, Targets.Classify(time, completed), baseline);
    }
}
