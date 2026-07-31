using System;
using Imui.Controls;
using Imui.Core;
using TNRD.Zeepkist.GTR.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Tournaments;

public class TournamentsDrawer : IZeepGUIDrawer
{
    private const string WindowTitle = "Tournaments";
    private const float WindowWidth = 920f;
    private const float WindowHeight = 540f;
    private const float ListWidth = 280f;
    private const ImWindowFlag WindowFlags = ImWindowFlag.None;

    private static readonly Color32 MutedText = new(180, 180, 180, 255);
    private static readonly Color32 AuthorMedal = new(120, 200, 255, 255);
    private static readonly Color32 GoldMedal = new(255, 210, 70, 255);
    private static readonly Color32 SilverMedal = new(200, 200, 210, 255);
    private static readonly Color32 BronzeMedal = new(210, 150, 90, 255);

    private readonly TournamentsWindowState _state;
    private readonly TournamentJoinOrchestrator _joinOrchestrator;

    private bool _mouseOverWindow;

    public TournamentsDrawer(
        TournamentsWindowState state,
        TournamentJoinOrchestrator joinOrchestrator)
    {
        _state = state;
        _joinOrchestrator = joinOrchestrator;
    }

    public void OnZeepGUI(ImGui gui)
    {
        if (!_state.IsOpen || !IsMainMenuScene())
            return;

        if (_joinOrchestrator.IsBusy)
            return;

        var open = true;
        ImRect windowRect = ImWindowPlacement.GetRect(
            gui,
            WindowTitle.AsSpan(),
            WindowWidth,
            WindowHeight,
            ImWindowAnchor.BottomCenter);

        if (!gui.BeginWindow(WindowTitle, ref open, ref _mouseOverWindow, windowRect, WindowFlags))
            return;

        try
        {
            DrawContent(gui);

            if (Input.GetKeyDown(KeyCode.Escape))
                _state.Close();
        }
        finally
        {
            gui.EndWindow();
        }

        if (!open)
            _state.Close();
    }

    private void DrawContent(ImGui gui)
    {
        using (gui.Horizontal())
        {
            if (gui.Button("Refresh", ImSizeMode.Fit))
                _state.Refresh();

            gui.AddSpacing(8f);

            TournamentViewModel selectedForPlay = _state.SelectedTournament;
            gui.BeginReadOnly(selectedForPlay == null || _joinOrchestrator.IsBusy);
            if (gui.Button("Play", ImSizeMode.Fit))
            {
                if (selectedForPlay != null)
                {
                    _state.Close();
                    _joinOrchestrator.Start(selectedForPlay);
                }
            }

            gui.EndReadOnly();

            gui.AddSpacing(8f);

            if (gui.Button("Close", ImSizeMode.Fit))
                _state.Close();
        }

        gui.Separator();

        if (_state.IsLoading)
        {
            gui.Text("Loading tournaments...".AsSpan());
            return;
        }

        if (!string.IsNullOrEmpty(_state.Error))
        {
            gui.Text(_state.Error.AsSpan(), new Color32(220, 80, 80, 255));
            return;
        }

        if (_state.Tournaments.Count == 0)
        {
            gui.Text("No active tournaments right now.".AsSpan());
            return;
        }

        float bodyHeight = Mathf.Max(160f, gui.GetLayoutHeight() - gui.GetRowsHeightWithSpacing(1));
        float detailWidth = Mathf.Max(200f, gui.GetLayoutWidth() - ListWidth - 12f);

        using (gui.Horizontal(gui.GetLayoutWidth(), bodyHeight))
        {
            DrawTournamentList(gui, bodyHeight);
            gui.AddSpacing(12f);
            DrawDetailPanel(gui, detailWidth, bodyHeight);
        }
    }

    private void DrawTournamentList(ImGui gui, float height)
    {
        using (gui.Vertical(ListWidth, height))
        {
            gui.Text("Active".AsSpan(), MutedText);
            gui.AddSpacing(4f);

            using (gui.List((ListWidth, Mathf.Max(80f, height - gui.GetRowsHeightWithSpacing(1)))))
            {
                for (int i = 0; i < _state.Tournaments.Count; i++)
                {
                    TournamentViewModel tournament = _state.Tournaments[i];
                    bool selected = _state.SelectedId == tournament.Id;
                    string label = $"{tournament.Slug}  ·  {tournament.LevelName}";
                    if (gui.ListItem(selected, label))
                        _state.Select(tournament.Id);
                }
            }
        }
    }

    private void DrawDetailPanel(ImGui gui, float width, float height)
    {
        TournamentViewModel tournament = _state.SelectedTournament;
        using (gui.Vertical(width, height))
        {
            if (tournament == null)
            {
                gui.Text("Select a tournament".AsSpan(), MutedText);
                return;
            }

            gui.Text(tournament.LevelName.AsSpan());
            gui.Text($"by {tournament.LevelAuthor}".AsSpan(), MutedText);
            gui.AddSpacing(4f);
            gui.Text($"{tournament.TypeLabel}  ·  {tournament.Slug}".AsSpan(), MutedText);
            gui.Text(
                $"Ends {FormatUtc(tournament.EndAt)}  ({FormatRemaining(tournament.EndAt)})".AsSpan(),
                MutedText);

            gui.Separator();
            gui.Text("Medal times".AsSpan(), MutedText);
            using (gui.Horizontal())
            {
                DrawMedal(gui, "Author", tournament.ValidationTimeAuthor, AuthorMedal);
                gui.AddSpacing(16f);
                DrawMedal(gui, "Gold", tournament.ValidationTimeGold, GoldMedal);
                gui.AddSpacing(16f);
                DrawMedal(gui, "Silver", tournament.ValidationTimeSilver, SilverMedal);
                gui.AddSpacing(16f);
                DrawMedal(gui, "Bronze", tournament.ValidationTimeBronze, BronzeMedal);
            }

            gui.Separator();
            string standingsHeader = tournament.StandingCount > 0
                ? $"Standings (top {tournament.Standings.Count} of {tournament.StandingCount})"
                : "Standings";
            gui.Text(standingsHeader.AsSpan(), MutedText);
            gui.AddSpacing(4f);

            if (tournament.Standings.Count == 0)
            {
                gui.Text("No results yet — be the first!".AsSpan(), MutedText);
                return;
            }

            using (gui.Horizontal())
            {
                gui.Text(" #".AsSpan(), MutedText);
                gui.AddSpacing(8f);
                gui.Text("Player".AsSpan(), MutedText);
            }

            float standingsHeight = Mathf.Max(80f, gui.GetLayoutHeight() - gui.GetRowsHeightWithSpacing(1));
            using (gui.List((width, standingsHeight)))
            {
                for (int i = 0; i < tournament.Standings.Count; i++)
                {
                    TournamentStandingEntry entry = tournament.Standings[i];
                    string row =
                        $"{entry.Rank,2}   {entry.PlayerName}   {FormatTime(entry.Time)}   {entry.Points} pts";
                    gui.ListItem(false, row);
                }
            }
        }
    }

    private static void DrawMedal(ImGui gui, string label, float time, Color32 color)
    {
        using (gui.Vertical())
        {
            gui.Text(label.AsSpan(), color);
            gui.Text(FormatTime(time).AsSpan());
        }
    }

    private static string FormatTime(float seconds)
    {
        if (seconds < 0f)
            seconds = 0f;

        int minutes = (int)(seconds / 60f);
        float remainingSeconds = seconds - minutes * 60f;
        return $"{minutes:00}:{remainingSeconds:00.000}";
    }

    private static string FormatUtc(DateTime utc)
    {
        if (utc == DateTime.MinValue)
            return "unknown";

        return utc.ToUniversalTime().ToString("yyyy-MM-dd HH:mm") + " UTC";
    }

    private static string FormatRemaining(DateTime endAtUtc)
    {
        if (endAtUtc == DateTime.MinValue)
            return "unknown";

        TimeSpan remaining = endAtUtc.ToUniversalTime() - DateTime.UtcNow;
        if (remaining.TotalSeconds <= 0)
            return "ended";

        if (remaining.TotalDays >= 1)
            return $"{(int)remaining.TotalDays}d {remaining.Hours}h left";

        if (remaining.TotalHours >= 1)
            return $"{(int)remaining.TotalHours}h {remaining.Minutes}m left";

        return $"{Math.Max(1, (int)remaining.TotalMinutes)}m left";
    }

    private static bool IsMainMenuScene()
    {
        string name = SceneManager.GetActiveScene().name;
        return name == "3D_MainMenu" || name.Contains("MainMenu");
    }
}
