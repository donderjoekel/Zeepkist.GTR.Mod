using System;
using Imui.Controls;
using Imui.Core;
using Imui.Style;
using UnityEngine;
using ZeepSDK.Controls;
using ZeepSDK.UI;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Dialogs;

internal sealed class UnsupportedVersionDialog : IZeepGUIDrawer, IDisposable
{
    public const string MoreInformationUrl = "https://zeepki.st/wiki/setup-modkist";

    private readonly Action _onDecision;
    private readonly DisposableBag _inputOverride;
    private bool _active = true;
    private readonly TNRD.Zeepkist.GTR.Core.GtrVersionStatus _version;

    public UnsupportedVersionDialog(TNRD.Zeepkist.GTR.Core.GtrVersionStatus version, Action onDecision)
    {
        _version = version;
        _onDecision = onDecision ?? throw new ArgumentNullException(nameof(onDecision));
        _inputOverride = ControlsApi.DisableAllInputExceptEventSystem(() => _active);
    }

    public void OnZeepGUI(ImGui gui)
    {
        if (!_active)
            return;

        uint id = gui.PushId("GtrUnsupportedVersionPopup");
        try
        {
            gui.BeginPopup();
            ImRect rect = ImWindow.GetInitialWindowRect(gui, new ImSize(840, 540));

            gui.Canvas.PushClipRect(rect);
            gui.Canvas.PushRectMask(rect, 0);
            gui.Box(rect, gui.Style.Window.Box);
            gui.RegisterControl(id, rect);
            gui.RegisterGroup(id, rect);
            gui.Layout.Push(ImAxis.Vertical, rect.WithPadding(24));

            gui.Text("GTR update required", new ImTextSettings(36, 0.5f));
            gui.AddSpacing(14);
            DrawParagraph(gui, "Your GTR version is no longer supported. Close Zeepkist, open Modkist MKII and update GTR.");
            gui.AddSpacing(14);
            DrawParagraph(gui, $"Current version: {_version.Current}");
            DrawParagraph(gui, $"Minimum supported version: {_version.Minimum}");
            DrawParagraph(gui, $"Latest version: {_version.Latest ?? "Unavailable"}");
            gui.AddSpacing(14);
            if (gui.Button("Open update guide", new ImSize(320, 42)))
                Application.OpenURL(MoreInformationUrl);

            gui.AddSpacing(14);
            DrawParagraph(gui,
                "GTR connects to ZeepCentraal (https://zeepki.st) to send your race times and show personal bests and world records. " +
                "When ZeepCentraal changes, GTR sometimes needs an update too. " +
                "You can still play, but GTR won't send your times or show these records until you update it.");

            const float closeButtonWidth = 220;
            const float continueButtonWidth = 280;
            const float buttonHeight = 52;
            gui.AddSpacing(Mathf.Max(12, gui.GetLayoutHeight() - buttonHeight));

            using (gui.Horizontal(gui.GetLayoutWidth(), buttonHeight))
            {
                if (gui.Button("Close Zeepkist", new ImSize(closeButtonWidth, buttonHeight)))
                    Application.Quit();
                gui.AddSpacing(Mathf.Max(0, gui.GetLayoutWidth() - continueButtonWidth));
                if (gui.Button("Continue without GTR", new ImSize(continueButtonWidth, buttonHeight)))
                    Decide();
            }

            gui.Layout.Pop();
            gui.Canvas.PopClipRect();
            gui.Canvas.PopClipRect();
            gui.EndPopup();
        }
        finally
        {
            gui.PopId();
        }
    }

    public void Dispose()
    {
        if (!_active)
            return;

        _active = false;
        _inputOverride.Dispose();
    }

    private static void DrawParagraph(ImGui gui, string text)
    {
        gui.Text(text, new ImTextSettings(18, 0f, 0f, true));
    }

    private void Decide()
    {
        if (!_active)
            return;

        _active = false;
        _inputOverride.Dispose();
        _onDecision();
    }
}
