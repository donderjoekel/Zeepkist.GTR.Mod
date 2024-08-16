using System;
using System.Collections.Generic;
using ZeepSDK.Leaderboard.Pages;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public abstract class BaseMultiplayerLeaderboardTab<TItem> : BaseMultiplayerLeaderboardTab
{
    private readonly List<TItem> _items = new();

    public int Count => _items.Count;

    private void UpdateMaxPages()
    {
        MaxPages = _items.Count / Instance.leaderboard_tab_positions.Count;
        UpdatePageNumber();
    }

    protected void AddItem(TItem item)
    {
        _items.Add(item);
        UpdateMaxPages();
    }

    protected void AddItems(IEnumerable<TItem> items)
    {
        _items.AddRange(items);
        UpdateMaxPages();
    }

    protected void ClearItems()
    {
        _items.Clear();
        UpdateMaxPages();
    }

    protected void RemoveItem(TItem item)
    {
        _items.Remove(item);
        UpdateMaxPages();
    }

    protected void SortItems(Comparison<TItem> comparison)
    {
        _items.Sort(comparison);
    }

    protected sealed override void OnDraw()
    {
        for (int i = 0; i < Instance.leaderboard_tab_positions.Count; i++)
        {
            int j = CurrentPage * Instance.leaderboard_tab_positions.Count + i;
            GUI_OnlineLeaderboardPosition gui = Instance.leaderboard_tab_positions[i];

            if (j >= _items.Count)
            {
                gui.gameObject.SetActive(false);
                continue;
            }

            gui.gameObject.SetActive(true);
            TItem item = _items[j];
            OnDrawItem(gui, item, j);
        }
    }

    protected abstract void OnDrawItem(GUI_OnlineLeaderboardPosition gui, TItem item, int index);
}
