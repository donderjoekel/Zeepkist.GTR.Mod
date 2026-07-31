using System;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Core;
using UnityEngine;
using UnityEngine.SceneManagement;
using ZeepkistClient;
using ZeepkistNetworking;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Multiplayer;

namespace TNRD.Zeepkist.GTR.UI.Tournaments;

public class TournamentJoinOrchestrator : IEagerService
{
    private const int MaxPlayers = 16;
    private const float LobbyListWaitSeconds = 3f;

    private readonly ILogger<TournamentJoinOrchestrator> _logger;

    private CancellationTokenSource _cts;
    private TournamentViewModel _pending;
    private string _expectedLobbyName;
    private bool _createdLobby;
    private bool _joinOrCreateRequested;
    private bool _subscribed;

    public bool IsBusy { get; private set; }
    public bool IsLoadingVisible { get; private set; }
    public string StatusMessage { get; private set; } = string.Empty;
    public string ErrorMessage { get; private set; }

    public TournamentJoinOrchestrator(ILogger<TournamentJoinOrchestrator> logger)
    {
        _logger = logger;
    }

    public void Start(TournamentViewModel tournament)
    {
        if (tournament == null || IsBusy)
            return;

        CancelInternal(disconnect: false);
        _cts = new CancellationTokenSource();
        _pending = tournament;
        _expectedLobbyName = FilterLobbyName(tournament.LobbyName);
        _createdLobby = false;
        _joinOrCreateRequested = false;
        IsBusy = true;
        IsLoadingVisible = true;
        ErrorMessage = null;
        StatusMessage = "Preparing...";

        RunAsync(_cts.Token).Forget();
    }

    public void Cancel()
    {
        if (!IsBusy)
            return;

        _logger.LogInformation("Tournament join cancelled by user");
        CancelInternal(disconnect: true);
        ErrorMessage = "Cancelled";
        StatusMessage = "Cancelled";
        IsLoadingVisible = false;
        IsBusy = false;
        ReturnToMainMenu();
    }

    public void DismissError()
    {
        ErrorMessage = null;
        IsLoadingVisible = false;
    }

    private async UniTaskVoid RunAsync(CancellationToken ct)
    {
        try
        {
            StatusMessage = "Connecting to Zeepkist Online...";
            Subscribe();

            await TransitionToOnlineLobbyAsync(ct);
            if (ct.IsCancellationRequested)
                return;

            StatusMessage = "Waiting for lobby list...";
            await WaitForMasterAndLobbyListAsync(ct);
            if (ct.IsCancellationRequested)
                return;

            TryJoinOrCreate();
        }
        catch (OperationCanceledException)
        {
            // handled by Cancel
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Tournament join failed");
            Fail(e.Message);
        }
    }

    private async UniTask TransitionToOnlineLobbyAsync(CancellationToken ct)
    {
        StartGameUI startGameUi = UnityEngine.Object.FindObjectOfType<StartGameUI>(true);
        if (startGameUi?.buttonsToDisableWhenGoingIntoAthing != null)
        {
            foreach (GenericButton button in startGameUi.buttonsToDisableWhenGoingIntoAthing)
            {
                if (button != null)
                    button.disabled = true;
            }
        }

        AnimateWhitePanel.AnimateTheCircle(true, .9f, 0, false);
        await UniTask.Delay(TimeSpan.FromSeconds(1), DelayType.UnscaledDeltaTime, cancellationToken: ct);

        PlayerManager.Instance.amountOfPlayers = 1;
        PlayerManager.Instance.singlePlayer = true;
        SceneManager.LoadScene("Online Lobby");
        // LobbyManager.Start connects to master on scene load.

        await UniTask.WaitUntil(() => SceneManager.GetActiveScene().name == "Online Lobby",
            cancellationToken: ct);
    }

    private async UniTask WaitForMasterAndLobbyListAsync(CancellationToken ct)
    {
        float deadline = Time.realtimeSinceStartup + LobbyListWaitSeconds;

        while (!ct.IsCancellationRequested && Time.realtimeSinceStartup < deadline)
        {
            if (ZeepkistNetwork.IsConnected && ZeepkistNetwork.LobbyType == LobbyType.Master)
            {
                // Give the initial lobby list a moment to arrive.
                await UniTask.Delay(TimeSpan.FromMilliseconds(500), DelayType.UnscaledDeltaTime,
                    cancellationToken: ct);
                return;
            }

            await UniTask.Yield(PlayerLoopTiming.Update, ct);
        }

        if (!ZeepkistNetwork.IsConnected || ZeepkistNetwork.LobbyType != LobbyType.Master)
            throw new InvalidOperationException("Timed out connecting to the master server");
    }

    private void TryJoinOrCreate()
    {
        if (_joinOrCreateRequested || _pending == null)
            return;

        ZeepkistLobby match = FindMatchingLobby();
        if (match != null)
        {
            StatusMessage = $"Joining {match.Name}...";
            _joinOrCreateRequested = true;
            _createdLobby = false;
            _logger.LogInformation("Joining existing tournament lobby {Id} ({Name})", match.ID, match.Name);
            ZeepkistNetwork.JoinLobby(match.ID);
            return;
        }

        StatusMessage = $"Creating {_expectedLobbyName}...";
        _joinOrCreateRequested = true;
        _createdLobby = true;
        _logger.LogInformation("Creating tournament lobby {Name}", _expectedLobbyName);
        ZeepkistNetwork.CreateLobby(_expectedLobbyName, MaxPlayers, true);
    }

    private ZeepkistLobby FindMatchingLobby()
    {
        return ZeepkistNetwork.AllLobbies
            .Where(lobby => lobby != null)
            .Where(lobby => string.Equals(lobby.Name, _expectedLobbyName, StringComparison.Ordinal))
            .Where(lobby => lobby.PlayerCount < lobby.MaxPlayerCount)
            .OrderByDescending(lobby => lobby.IsPublic)
            .ThenByDescending(lobby => lobby.PlayerCount)
            .FirstOrDefault();
    }

    private void OnCreatedRoom()
    {
        if (!IsBusy || !_createdLobby || _pending == null)
            return;

        if (ZeepkistNetwork.CurrentLobby == null)
            return;

        try
        {
            StatusMessage = "Setting tournament map...";
            ZeepkistNetwork.CurrentLobby.Playlist.Clear();
            var item = new PlaylistItem(
                _pending.LevelUid,
                _pending.WorkshopId,
                _pending.LevelName,
                _pending.LevelAuthor);
            MultiplayerApi.AddLevelToPlaylist(item, setAsPlayNext: true);
            ZeepkistNetwork.CurrentLobby.CurrentPlaylistIndex = 0;
            MultiplayerApi.UpdateServerPlaylist();
            MultiplayerApi.SetNextLevelIndex(0);
            _logger.LogInformation("Set tournament playlist to {Level} ({WorkshopId})",
                _pending.LevelName, _pending.WorkshopId);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to set tournament playlist");
        }

        CompleteSuccessfully();
    }

    private void OnJoinedRoom()
    {
        if (!IsBusy || _createdLobby)
            return;

        CompleteSuccessfully();
    }

    private void OnJoinLobbyFailed(JoinLobbyResult result)
    {
        if (!IsBusy)
            return;

        _logger.LogWarning("Join lobby failed: {Result}", result);
        if (!_createdLobby)
        {
            StatusMessage = $"Join failed ({result}), creating lobby...";
            _joinOrCreateRequested = true;
            _createdLobby = true;
            ZeepkistNetwork.CreateLobby(_expectedLobbyName, MaxPlayers, true);
            return;
        }

        Fail($"Failed to join lobby: {result}");
    }

    private void CompleteSuccessfully()
    {
        StatusMessage = "Entering game...";
        Unsubscribe();
        IsBusy = false;
        IsLoadingVisible = false;
        _pending = null;
        CancelTokenOnly();
    }

    private void Fail(string message)
    {
        ErrorMessage = message;
        StatusMessage = message;
        CancelInternal(disconnect: true);
        IsBusy = false;
        IsLoadingVisible = true;
        ReturnToMainMenu();
    }

    private void Subscribe()
    {
        if (_subscribed)
            return;

        MultiplayerApi.CreatedRoom += OnCreatedRoom;
        MultiplayerApi.JoinedRoom += OnJoinedRoom;
        ZeepkistNetwork.JoinLobbyFailed += OnJoinLobbyFailed;
        ZeepkistNetwork.LobbyListUpdated += OnLobbyListUpdated;
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed)
            return;

        MultiplayerApi.CreatedRoom -= OnCreatedRoom;
        MultiplayerApi.JoinedRoom -= OnJoinedRoom;
        ZeepkistNetwork.JoinLobbyFailed -= OnJoinLobbyFailed;
        ZeepkistNetwork.LobbyListUpdated -= OnLobbyListUpdated;
        _subscribed = false;
    }

    private void OnLobbyListUpdated()
    {
        if (!IsBusy || _joinOrCreateRequested)
            return;

        if (ZeepkistNetwork.IsConnected && ZeepkistNetwork.LobbyType == LobbyType.Master)
            TryJoinOrCreate();
    }

    private void CancelInternal(bool disconnect)
    {
        Unsubscribe();
        CancelTokenOnly();
        _pending = null;
        _joinOrCreateRequested = false;
        _createdLobby = false;

        if (disconnect && ZeepkistNetwork.IsConnected)
            ZeepkistNetwork.Disconnect("TournamentJoinCancelled");
    }

    private void CancelTokenOnly()
    {
        if (_cts == null)
            return;

        _cts.Cancel();
        _cts.Dispose();
        _cts = null;
    }

    private static void ReturnToMainMenu()
    {
        string scene = SceneManager.GetActiveScene().name;
        if (scene != "3D_MainMenu" && !scene.Contains("MainMenu"))
            SceneManager.LoadScene("3D_MainMenu");
    }

    private static string FilterLobbyName(string name)
    {
        if (PlayerManager.Instance?.steamAchiever == null)
            return name;

        return PlayerManager.Instance.steamAchiever.BWF_FilterString(
            name,
            Steam_TheAchiever.FilterPurpose.level);
    }
}
