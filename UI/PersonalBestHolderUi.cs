﻿using System;
using System.Linq;
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
        if (personalBestHolder == null)
        {
            _positionText.text = string.Empty;
            _playerNameText.text = SteamClient.Name;
            _timeText.text = "--:--.---";
        }
        else
        {
            _positionText.text = personalBestHolder.Rank <= 0 ? string.Empty : personalBestHolder.Rank.ToString();
            _playerNameText.text = SteamClient.Name;
            _timeText.text = personalBestHolder.Time <= 0 ? "--:--.---" : personalBestHolder.Time.GetFormattedTime();
        }
    }
}
