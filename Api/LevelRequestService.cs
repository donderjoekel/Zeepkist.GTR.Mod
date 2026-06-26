using System;
using System.Net.Http;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Core;
using ZeepkistClient;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Level;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Api;

public class LevelRequestService : IEagerService
{
    private readonly ApiHttpClient _apiHttpClient;
    private readonly ILogger<LevelRequestService> _logger;

    private string _sentKey;

    public LevelRequestService(
        ApiHttpClient apiHttpClient,
        ILogger<LevelRequestService> logger)
    {
        _apiHttpClient = apiHttpClient;
        _logger = logger;

        RacingApi.LevelLoaded += OnLevelLoaded;
        RacingApi.Quit += OnQuit;
        MultiplayerApi.DisconnectedFromGame += OnDisconnectedFromGame;
    }

    private void OnLevelLoaded()
    {
        if (!MultiplayerApi.IsPlayingOnline)
            return;

        SendLevelRequest().Forget();
    }

    private async UniTaskVoid SendLevelRequest()
    {
        LevelScriptableObject currentLevel = LevelApi.CurrentLevel;
        string workshopId = RecordWorkshopId.ToWireValue(
            ZeepkistNetwork.CurrentLobby?.WorkshopID ?? 0,
            currentLevel?.WorkshopID ?? 0,
            currentLevel?.IsAdventureLevel ?? false,
            currentLevel?.UseAvonturenLevel ?? false);
        string hash = LevelApi.CurrentHashV2?.Hash;

        if (string.IsNullOrEmpty(workshopId) || string.IsNullOrEmpty(hash))
        {
            _logger.LogInformation("Skipping level request because workshop ID or hash is missing");
            return;
        }

        string key = $"{workshopId}:{hash}";
        if (string.Equals(_sentKey, key, StringComparison.Ordinal))
            return;

        _sentKey = key;

        LevelRequestResource resource = new()
        {
            WorkshopId = workshopId,
            Hash = hash
        };

        try
        {
            using HttpResponseMessage response = await _apiHttpClient.PostAsync("level/request", resource);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Failed to request level scan for workshop {WorkshopId} and hash {Hash}: {StatusCode}",
                    workshopId,
                    hash,
                    response.StatusCode);
            }
        }
        catch (Exception e)
        {
            _logger.LogError(
                e,
                "Failed to request level scan for workshop {WorkshopId} and hash {Hash}",
                workshopId,
                hash);
        }
    }

    private void OnQuit()
    {
        _sentKey = null;
    }

    private void OnDisconnectedFromGame()
    {
        _sentKey = null;
    }
}
