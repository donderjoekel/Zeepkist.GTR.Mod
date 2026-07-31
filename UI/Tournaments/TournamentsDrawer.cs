using System;
using Imui.Controls;
using Imui.Core;
using Imui.Style;
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
        Color32 secondary = SecondaryTextColor(gui);
        using (gui.Vertical(ListWidth, height))
        {
            gui.Text("Active".AsSpan(), secondary);
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
        Color32 secondary = SecondaryTextColor(gui);
        using (gui.Vertical(width, height))
        {
            if (tournament == null)
            {
                gui.Text("Select a tournament".AsSpan(), secondary);
                return;
            }

            gui.Text(tournament.LevelName.AsSpan());
            gui.Text($"by {tournament.LevelAuthor}".AsSpan(), secondary);
            gui.AddSpacing(4f);
            gui.Text($"{tournament.TypeLabel}  ·  {tournament.Slug}".AsSpan(), secondary);
            gui.Text(
                $"Ends {FormatUtc(tournament.EndAt)}  ({FormatRemaining(tournament.EndAt)})".AsSpan(),
                secondary);

            gui.Separator();
            gui.Text("Medal times".AsSpan(), secondary);
            DrawMedalTimes(gui, tournament);

            gui.Separator();
            string standingsHeader = tournament.StandingCount > 0
                ? $"Standings ({tournament.StandingCount})"
                : "Standings";
            gui.Text(standingsHeader.AsSpan(), secondary);
            gui.AddSpacing(4f);

            if (tournament.Standings.Count == 0)
            {
                gui.Text("No results yet — be the first!".AsSpan(), secondary);
                return;
            }

            float tableHeight = Mathf.Max(80f, gui.GetLayoutHeight());
            DrawStandingsTable(gui, tournament, tableHeight);
        }
    }

    private static void DrawMedalTimes(ImGui gui, TournamentViewModel tournament)
    {
        float columnHeight = gui.GetRowsHeightWithSpacing(2);
        using (gui.Horizontal(gui.GetLayoutWidth(), columnHeight))
        {
            DrawMedalColumn(gui, "Author", tournament.ValidationTimeAuthor, AuthorMedal, columnHeight);
            gui.AddSpacing(16f);
            DrawMedalColumn(gui, "Gold", tournament.ValidationTimeGold, GoldMedal, columnHeight);
            gui.AddSpacing(16f);
            DrawMedalColumn(gui, "Silver", tournament.ValidationTimeSilver, SilverMedal, columnHeight);
            gui.AddSpacing(16f);
            DrawMedalColumn(gui, "Bronze", tournament.ValidationTimeBronze, BronzeMedal, columnHeight);
        }
    }

    private static void DrawMedalColumn(ImGui gui, string label, float time, Color32 color, float height)
    {
        using (gui.Vertical(0f, height))
        {
            gui.Text(label.AsSpan(), color);
            gui.Text(FormatTime(time).AsSpan());
        }
    }

    private static void DrawStandingsTable(ImGui gui, TournamentViewModel tournament, float tableHeight)
    {
        gui.BeginTable(4, (gui.GetLayoutWidth(), tableHeight));

        gui.TableNextRow();
        gui.TableNextColumn();
        gui.Text("Position".AsSpan());
        gui.TableNextColumn();
        gui.Text("Player".AsSpan());
        gui.TableNextColumn();
        gui.Text("Time".AsSpan());
        gui.TableNextColumn();
        gui.Text("Points".AsSpan());

        for (int i = 0; i < tournament.Standings.Count; i++)
        {
            TournamentStandingEntry entry = tournament.Standings[i];
            gui.TableNextRow();
            gui.TableNextColumn();
            gui.Text(entry.Rank.ToString().AsSpan());
            gui.TableNextColumn();
            gui.Text(entry.PlayerName.AsSpan());
            gui.TableNextColumn();
            gui.Text(FormatTime(entry.Time).AsSpan());
            gui.TableNextColumn();
            gui.Text(entry.Points.ToString().AsSpan());
        }

        gui.EndTable();
    }

    private static Color32 SecondaryTextColor(ImGui gui) =>
        gui.Style.Text.Color.WithAlpha(0.75f);

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
