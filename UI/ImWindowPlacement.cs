using System;
using Imui.Core;

namespace TNRD.Zeepkist.GTR.UI;

public enum ImWindowAnchor
{
    MiddleLeft,
    BottomRight
}

public static class ImWindowPlacement
{
    private const float Margin = 16f;

    public static ImRect GetRect(ImGui gui, ReadOnlySpan<char> title, float width, float height, ImWindowAnchor anchor)
    {
        var windowId = gui.GetControlId(title);
        if (gui.WindowManager.TryFindWindow(windowId) >= 0)
        {
            ref readonly ImRect existing = ref gui.WindowManager.GetWindowState(windowId).Rect;
            return new ImRect(existing.X, existing.Y, width, height);
        }

        ImRect screen = gui.Canvas.SafeScreenRect;
        return anchor switch
        {
            ImWindowAnchor.BottomRight => new ImRect(
                screen.Right - width - Margin,
                screen.Bottom + Margin,
                width,
                height),
            ImWindowAnchor.MiddleLeft => new ImRect(
                screen.Left + Margin,
                screen.Bottom + (screen.H - height) * 0.5f,
                width,
                height),
            _ => new ImRect(screen.Left + Margin, screen.Bottom + Margin, width, height)
        };
    }
}
