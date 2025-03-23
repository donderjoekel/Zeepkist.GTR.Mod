using System;
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

    public void SetPersonalBestHolder(IGetPersonalBest_AllPersonalBestGlobals_Nodes personalBestHolder)
    {
        if (personalBestHolder == null)
        {
            _positionText.text = string.Empty;
            _playerNameText.text = SteamClient.Name;
            _timeText.text = "--:--.---";
        }
        else
        {
            // Currently we don't have a rank
            _positionText.text = string.Empty;

            double time = personalBestHolder.RecordByIdRecord.Time;
            _playerNameText.text = SteamClient.Name;
            _timeText.text = time <= 0 ? "--:--.---" : time.GetFormattedTime();
        }
    }
}
