using System.Threading;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Steamworks;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Json;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Level;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class OnlineGhostsService : IEagerService
{
    private const string GetPersonalBestQuery
        = "query($steamId:BigFloat,$hash:String){allPersonalBestGlobals(filter:{userByIdUser:{steamId:{equalTo:$steamId}}levelByIdLevel:{hash:{equalTo:$hash}}}){nodes{recordByIdRecord{id userByIdUser{steamName} recordMediasByIdRecord{nodes{ghostUrl}}}}}}";

    private readonly ILogger<OnlineGhostsService> _logger;
    private readonly GraphQLApiHttpClient _client;
    private readonly GhostRepository _ghostRepository;
    private readonly GhostPlayer _ghostPlayer;

    private CancellationTokenSource _cts;
    private string _levelHash;

    public OnlineGhostsService(
        ILogger<OnlineGhostsService> logger,
        GraphQLApiHttpClient client,
        GhostRepository ghostRepository,
        GhostPlayer ghostPlayer)
    {
        _logger = logger;
        _client = client;
        _ghostRepository = ghostRepository;
        _ghostPlayer = ghostPlayer;

        RacingApi.LevelLoaded += OnLevelLoaded;
        RacingApi.PlayerSpawned += OnPlayerSpawned;
        RacingApi.RoundEnded += OnRoundEnded;
        MultiplayerApi.DisconnectedFromGame += OnDisconnectedFromGame;
    }

    private void OnDisconnectedFromGame()
    {
        _ghostPlayer.ClearGhosts();
    }

    private void OnLevelLoaded()
    {
        _levelHash = LevelApi.GetLevelHash(LevelApi.CurrentLevel);
    }

    private void OnPlayerSpawned()
    {
        LoadPersonalBest();
    }

    private void OnRoundEnded()
    {
        _ghostPlayer.ClearGhosts();
    }

    private void LoadPersonalBest()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        LoadPersonalBestAsync(_cts.Token).Forget();
    }

    private async UniTaskVoid LoadPersonalBestAsync(CancellationToken ct)
    {
        _logger.LogInformation("Loading personal best...");
        Result<PersonalBestGhostData> result = await _client.PostAsync<PersonalBestGhostData>(
            GetPersonalBestQuery,
            new
            {
                steamId = SteamClient.SteamId.ToString(),
                hash = _levelHash
            },
            ct);

        if (ct.IsCancellationRequested)
            return;

        if (result.IsFailed)
        {
            _logger.LogError("Failed to load personal best: {Result}", result.ToString());
            return;
        }

        if (result.Value?.Id == null)
        {
            return;
        }

        IGhost ghost = await _ghostRepository.GetGhost(result.Value.Id.Value, result.Value.GhostUrl);
        if (!_ghostPlayer.HasGhost(result.Value.Id.Value))
        {
            _ghostPlayer.ClearGhosts();
            _ghostPlayer.AddGhost(result.Value.Id.Value, result.Value.SteamName, ghost);
        }
    }

    [JsonConverter(typeof(JsonPathConverter))]
    private class PersonalBestGhostData
    {
        [JsonProperty("data.allPersonalBestGlobals.nodes[0].recordByIdRecord.id")]
        public int? Id { get; set; }

        [JsonProperty("data.allPersonalBestGlobals.nodes[0].recordByIdRecord.userByIdUser.steamName")]
        public string SteamName { get; set; }

        [JsonProperty("data.allPersonalBestGlobals.nodes[0].recordByIdRecord.recordMediasByIdRecord.nodes[0].ghostUrl")]
        public string GhostUrl { get; set; }
    }
}
