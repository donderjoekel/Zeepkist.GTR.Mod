using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Messaging;
using TNRD.Zeepkist.GTR.PlayerLoop;
using UnityEngine;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Level;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class OfflineGhostsService : IEagerService, System.IDisposable
{
    private readonly ILogger<OfflineGhostsService> _logger;
    private readonly OfflineGhostGraphqlService _graphqlService;
    private readonly GhostRepository _ghostRepository;
    private readonly GhostPlayer _ghostPlayer;
    private readonly ConfigService _configService;
    private readonly MessengerService _messengerService;
    private readonly PlayerLoopService _playerLoopService;
    private readonly PlayerLoopSubscription _updateSubscription;

    private readonly List<string> _additionalGhosts = new();
    private readonly HashSet<int> _bulkGhostIds = new();

    private CancellationTokenSource _cts;
    private string _bulkLevelHash;

    public bool IsShowingAllGhosts { get; private set; }
    public event System.Action BulkModeChanged;

    public OfflineGhostsService(
        ILogger<OfflineGhostsService> logger,
        OfflineGhostGraphqlService graphqlService,
        GhostRepository ghostRepository,
        GhostPlayer ghostPlayer,
        ConfigService configService,
        PlayerLoopService playerLoopService,
        MessengerService messengerService)
    {
        _logger = logger;
        _graphqlService = graphqlService;
        _ghostRepository = ghostRepository;
        _ghostPlayer = ghostPlayer;
        _configService = configService;
        _messengerService = messengerService;
        _playerLoopService = playerLoopService;

        _updateSubscription = playerLoopService.SubscribeUpdate(OnUpdate);

        RacingApi.PlayerSpawned += OnPlayerSpawned;
        RacingApi.RoundEnded += OnRoundEnded;
        RacingApi.Quit += OnQuit;
        // TODO: Figure out when quitting to main menu
        // TODO: Figure out when pause menu opened
    }

    private void OnUpdate()
    {
        if (MultiplayerApi.IsPlayingOnline)
            return;

        if (PlayerManager.Instance == null) return;
        if (PlayerManager.Instance.currentMaster == null) return;
        if (PlayerManager.Instance.currentMaster.OnlineGameplayUI == null) return;

        if (PlayerManager.Instance.currentMaster.OnlineGameplayUI.LeaderboardAction.buttonDown)
        {
            PlayerManager.Instance.currentMaster.OnlineGameplayUI.OnlineTabLeaderboard.Open(true);
        }
    }

    protected virtual void OnPlayerSpawned()
    {
        if (MultiplayerApi.IsPlayingOnline)
            return;

        if (_configService.EnableGhosts.Value)
        {
            if (IsShowingAllGhosts && _bulkLevelHash == LevelApi.CurrentHash)
                LoadAllGhosts();
            else
                ClearAllGhosts();
        }
    }

    private void OnQuit()
    {
        if (MultiplayerApi.IsPlayingOnline)
            return;

        CancelLoad();
        SetBulkMode(false);
        _ghostPlayer.ClearGhosts();
    }

    private void OnRoundEnded()
    {
        if (MultiplayerApi.IsPlayingOnline)
            return;

        CancelLoad();
        _ghostPlayer.ClearGhosts();
    }

    private void LoadGhosts()
    {
        CancelLoad();
        _cts = new CancellationTokenSource();
        LoadGhostsAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid LoadGhostsAsync(CancellationToken ct)
    {
        (Result<IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes>> pbResult,
                Result<IReadOnlyList<IGetAdditionalGhosts_PersonalBestGlobals_Nodes>> additionalResult) =
            await UniTask.WhenAll(
                LoadPersonalBestsAsync(ct),
                LoadAdditionalGhostsAsync(ct));

        List<IGhostRecordFrag> personalBests = new();

        if (pbResult.IsSuccess)
        {
            personalBests.AddRange(pbResult.Value.Select(x => x.Record));
        }
        else
        {
            _logger.LogWarning("Loading Personal Bests failed: {Result}", pbResult);
        }

        if (additionalResult.IsSuccess)
        {
            personalBests.AddRange(additionalResult.Value.Select(x => x.Record));
        }
        else
        {
            _logger.LogWarning("Loading AdditionalGhosts failed: {Result}", additionalResult);
        }

        IReadOnlyList<int> loadedGhostIds = _ghostPlayer.GetLoadedGhostIds();

        foreach (int loadedGhostId in loadedGhostIds)
        {
            if (personalBests.All(x => x.Id != loadedGhostId))
            {
                _ghostPlayer.RemoveGhost(loadedGhostId);
            }
        }

        IEnumerable<UniTask> loads = personalBests.Select(personalBest => LoadGhost(personalBest, ct));
        await UniTask.WhenAll(loads);
    }

    public void ShowAllGhosts()
    {
        if (IsShowingAllGhosts)
            return;

        _bulkLevelHash = LevelApi.CurrentHash;
        SetBulkMode(true);
        LoadAllGhosts();
    }

    public void ClearAllGhosts()
    {
        CancelLoad();
        _bulkGhostIds.Clear();
        _bulkLevelHash = null;
        SetBulkMode(false);
        _ghostPlayer.ClearGhosts();

        if (_configService.EnableGhosts.Value && !string.IsNullOrEmpty(LevelApi.CurrentHash))
            LoadGhosts();
    }

    private void LoadAllGhosts()
    {
        CancelLoad();
        _cts = new CancellationTokenSource();
        LoadAllGhostsAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid LoadAllGhostsAsync(CancellationToken ct)
    {
        int? first = OfflineGhostLimit.ToGraphQlFirst(_configService.MaximumVisibleOfflineGhosts.Value);
        (
            Result<IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes>> pbResult,
            Result<IReadOnlyList<IGetAdditionalGhosts_PersonalBestGlobals_Nodes>> additionalResult,
            Result<IReadOnlyList<IGetAllPersonalBestGhosts_Records_Nodes>> allResult
        ) = await UniTask.WhenAll(
            LoadPersonalBestsAsync(ct),
            LoadAdditionalGhostsAsync(ct),
            _graphqlService.GetAllPersonalBestGhosts(LevelApi.CurrentHash, first, ct));

        if (ct.IsCancellationRequested || !IsShowingAllGhosts)
            return;

        List<IGhostRecordFrag> protectedGhosts = new();
        if (pbResult.IsSuccess)
            protectedGhosts.AddRange(pbResult.Value.Select(x => x.Record));
        else
            _logger.LogWarning("Loading Personal Bests failed: {Result}", pbResult);

        if (additionalResult.IsSuccess)
            protectedGhosts.AddRange(additionalResult.Value.Select(x => x.Record));
        else
            _logger.LogWarning("Loading AdditionalGhosts failed: {Result}", additionalResult);

        IReadOnlyList<IGetAllPersonalBestGhosts_Records_Nodes> bulkGhosts = [];
        if (allResult.IsSuccess)
            bulkGhosts = allResult.Value;
        else
            _logger.LogWarning("Loading all Personal Bests failed: {Result}", allResult);

        _bulkGhostIds.Clear();
        foreach (IGetAllPersonalBestGhosts_Records_Nodes record in bulkGhosts)
            _bulkGhostIds.Add(record.Id);

        HashSet<int> desiredIds = protectedGhosts.Select(x => x.Id).ToHashSet();
        desiredIds.UnionWith(_bulkGhostIds);
        foreach (int loadedGhostId in _ghostPlayer.GetLoadedGhostIds())
        {
            if (!desiredIds.Contains(loadedGhostId))
                _ghostPlayer.RemoveGhost(loadedGhostId);
        }

        IEnumerable<IGhostRecordFrag> recordsToLoad = protectedGhosts
            .Concat(bulkGhosts)
            .GroupBy(record => record.Id)
            .Select(group => group.First());
        await UniTask.WhenAll(recordsToLoad.Select(record => LoadGhost(record, ct)));
    }

    private async UniTask<Result<IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes>>>
        LoadPersonalBestsAsync(
        CancellationToken ct)
    {
        Result<IReadOnlyList<IGetPersonalBestGhosts_PersonalBestGlobals_Nodes>> result
            = await _graphqlService.GetPersonalBests(LevelApi.CurrentHash, ct);

        if (ct.IsCancellationRequested)
            return Result.Ok();

        if (result.IsFailed)
            _logger.LogError("Failed to load personal bests: {Result}", result);

        return result;
    }

    private async UniTask<Result<IReadOnlyList<IGetAdditionalGhosts_PersonalBestGlobals_Nodes>>>
        LoadAdditionalGhostsAsync(
        CancellationToken ct)
    {
        Result<IReadOnlyList<IGetAdditionalGhosts_PersonalBestGlobals_Nodes>> result
            = await _graphqlService.GetAdditionalGhosts(_additionalGhosts, LevelApi.CurrentHash, ct);

        if (ct.IsCancellationRequested)
            return Result.Ok();

        if (result.IsFailed)
            _logger.LogError("Failed to load additional ghosts: {Result}", result);

        return result;
    }

    public void AddAdditionalGhost(string steamId)
    {
        if (IsShowingAllGhosts)
            return;
        _additionalGhosts.Add(steamId);
        _ghostPlayer.ClearGhosts();
        LoadGhosts();
    }

    public bool ContainsAdditionalGhost(string steamId)
    {
        return _additionalGhosts.Contains(steamId);
    }

    public void RemoveAdditionalGhost(string steamId)
    {
        if (IsShowingAllGhosts)
            return;
        _additionalGhosts.Remove(steamId);
        _ghostPlayer.ClearGhosts();
        LoadGhosts();
    }

    private async UniTask LoadGhost(IGhostRecordFrag personalBest, CancellationToken cancellationToken)
    {
        Result<IGhost> ghost = await _ghostRepository.GetGhost(
            personalBest.Id,
            personalBest.RecordMedia.GhostUrl,
            cancellationToken);

        if (cancellationToken.IsCancellationRequested)
            return;
        if (ghost.IsFailed)
        {
            _logger.LogError("Unable to get ghost from repository: {Result}", ghost.ToString());
            return;
        }

        _ghostPlayer.AddGhost(GhostType.Global, personalBest.Id, personalBest.User.SteamName, ghost.Value);
    }

    private void SetBulkMode(bool enabled)
    {
        if (IsShowingAllGhosts == enabled)
            return;

        IsShowingAllGhosts = enabled;
        BulkModeChanged?.Invoke();
    }

    private void CancelLoad()
    {
        CancellationTokenSource cts = _cts;
        _cts = null;
        if (cts == null)
            return;
        cts.Cancel();
        cts.Dispose();
    }

    public void Dispose()
    {
        CancelLoad();
        BulkModeChanged = null;
        _playerLoopService.UnsubscribeUpdate(_updateSubscription);
        RacingApi.PlayerSpawned -= OnPlayerSpawned;
        RacingApi.RoundEnded -= OnRoundEnded;
        RacingApi.Quit -= OnQuit;
    }
}
