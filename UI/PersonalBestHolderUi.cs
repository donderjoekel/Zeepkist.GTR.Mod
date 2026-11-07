using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Utilities;
using Steamworks;
using TMPro;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.UI;

public class PersonalBestHolderUi : MonoBehaviour
{
    private TextMeshProUGUI _positionText;
    private TextMeshProUGUI _playerNameText;
    private TextMeshProUGUI _timeText;

    public void InitializeUi()
    {
        TextMeshProUGUI[] texts = GetComponentsInChildren<TextMeshProUGUI>();
        TextMeshProUGUI headerText = texts.First(
            x => string.Equals(x.name, "Your Time Title", StringComparison.OrdinalIgnoreCase));
        headerText.text = "Personal Best";

        _positionText = texts.First(x => string.Equals(x.name, "Position", StringComparison.OrdinalIgnoreCase));
        _playerNameText = texts.First(x => string.Equals(x.name, "Player", StringComparison.OrdinalIgnoreCase));
        _timeText = texts.First(x => string.Equals(x.name, "Time", StringComparison.OrdinalIgnoreCase));
    }

    public void SetPersonalBestHolder(PersonalBestHolder personalBestHolder)
    {
        var version = ServiceHelper.Instance.GetRequiredService<GtrVersionStatus>();
        if (version.IsUnsupported)
        {
            _positionText.text = string.Empty;
            _playerNameText.text = version.UpdateMessage;
            _timeText.text = string.Empty;
            _playerNameText.enableAutoSizing = true;
            _playerNameText.fontSizeMax = _playerNameText.fontSize;
            _playerNameText.fontSizeMin = 8;
            _playerNameText.enableWordWrapping = false;
            return;
        }

        if (personalBestHolder == null)
        {
            _positionText.text = string.Empty;
            _playerNameText.text = SteamClient.Name;
            _timeText.text = "--:--.---";
        }
        else
        {
            _positionText.text = personalBestHolder.Rank?.ToString() ?? string.Empty;
            double time = personalBestHolder.Time;
            _playerNameText.text = SteamClient.Name;
            _timeText.text = time <= 0 ? "--:--.---" : time.GetFormattedTime();
        }
    }
}
