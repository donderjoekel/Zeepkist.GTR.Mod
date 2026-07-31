using System;
using Imui.Controls;
using Imui.Core;
using TNRD.Zeepkist.GTR.UI;
using UnityEngine;
using ZeepSDK.UI;

namespace TNRD.Zeepkist.GTR.UI.Tournaments;

public class TournamentJoinLoadingDrawer : IZeepGUIDrawer
{
    private const string WindowTitle = "Tournament Matchmaking";
    private const float WindowWidth = 420f;
    private const float WindowHeight = 160f;
    private const ImWindowFlag WindowFlags =
        ImWindowFlag.NoCloseButton | ImWindowFlag.NoResizing;

    private readonly TournamentJoinOrchestrator _orchestrator;
    private bool _mouseOverWindow;

    public TournamentJoinLoadingDrawer(TournamentJoinOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
    }

    public void OnZeepGUI(ImGui gui)
    {
        if (!_orchestrator.IsLoadingVisible)
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
            gui.Text((_orchestrator.StatusMessage ?? string.Empty).AsSpan());

            if (!string.IsNullOrEmpty(_orchestrator.ErrorMessage))
            {
                gui.Text(_orchestrator.ErrorMessage.AsSpan(), new Color32(220, 80, 80, 255));
                if (gui.Button("Close", ImSizeMode.Fit))
                    _orchestrator.DismissError();
            }
            else if (_orchestrator.IsBusy)
            {
                if (gui.Button("Cancel", ImSizeMode.Fit) || Input.GetKeyDown(KeyCode.Escape))
                    _orchestrator.Cancel();
            }
        }
        finally
        {
            gui.EndWindow();
        }
    }
}
