using BepInEx.Configuration;
using Imui.Controls;
using Imui.Core;
using TNRD.Zeepkist.GTR.Configuration;

namespace TNRD.Zeepkist.GTR.UI.Config;

public class GtrConfigToolbarDrawer
{
    private readonly ConfigService _config;

    public GtrConfigToolbarDrawer(ConfigService config)
    {
        _config = config;
    }

    public void Draw(ImGui gui)
    {
        DrawRecordsMenu(gui);
        DrawGhostsMenu(gui);
        DrawRecordHolderMenu(gui);
    }

    private void DrawRecordsMenu(ImGui gui)
    {
        if (!gui.BeginMenu("Records"))
            return;

        using (gui.Vertical())
        {
            DrawBool(gui, _config.SubmitRecords, "Submit Records");
            gui.Separator();
            DrawBool(gui, _config.ShowRecordSubmitMessage, "Show Record Submit Message");
        }

        gui.EndMenu();
    }

    private void DrawGhostsMenu(ImGui gui)
    {
        if (!gui.BeginMenu("Ghosts"))
            return;

        using (gui.Vertical())
        {
            DrawBool(gui, _config.EnableGhosts, "Enable Ghosts");
            gui.Separator();
            DrawBool(gui, _config.ShowGhosts, "Show Ghosts");
            DrawBool(gui, _config.ShowGhostNames, "Show Ghost Names");
            DrawBool(gui, _config.ShowGhostTransparent, "Show Ghost Transparent");
            DrawBool(gui, _config.ShowGlobalPersonalBest, "Show Global Personal Best");
        }

        gui.EndMenu();
    }

    private void DrawRecordHolderMenu(ImGui gui)
    {
        if (!gui.BeginMenu("Record Holder"))
            return;

        using (gui.Vertical())
        {
            DrawBool(gui, _config.ShowRecordHolder, "Show Record Holder");
            DrawBool(gui, _config.ShowWorldRecordOnHolder, "Show World Record On Holder");
            DrawBool(gui, _config.ShowPersonalBestOnHolder, "Show Personal Best On Holder");
            DrawBool(gui, _config.ShowWorldRecordHolder, "Show World Record Holder");
            DrawBool(gui, _config.ShowPersonalBestHolder, "Show Personal Best Holder");
        }

        gui.EndMenu();
    }

    private static void DrawBool(ImGui gui, ConfigEntry<bool> entry, string label)
    {
        entry.Value = gui.Checkbox(entry.Value, label);
    }
}
