using System;
using System.Net.Http;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Core;
using TNRD.Zeepkist.GTR.PlayerLoop;
using ZeepkistClient;
using ZeepSDK.Chat;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.Level;
using ZeepSDK.Messaging;
using ZeepSDK.Multiplayer;
using ZeepSDK.Racing;

namespace TNRD.Zeepkist.GTR.Voting;

[UsedImplicitly]
public class VotingService : IEagerService
{
    private const string TIME_LEFT = "00:30";

    private readonly PlayerLoopService _playerLoopService;
    private readonly ILogger<VotingService> _logger;
    private readonly ApiHttpClient _apiHttpClient;

    private string _previousTimeLeft;

    public VotingService(PlayerLoopService playerLoopService, ILogger<VotingService> logger,
        ApiHttpClient apiHttpClient)
    {
        _playerLoopService = playerLoopService;
        _logger = logger;
        _apiHttpClient = apiHttpClient;
        _playerLoopService.SubscribeUpdate(OnUpdate);
    }

    private void OnUpdate()
    {
        if (!MultiplayerApi.IsPlayingOnline)
            return;

        string currentTimeLeft = ZeepkistNetwork.CurrentLobby.timeLeftString;

        if (currentTimeLeft == TIME_LEFT && _previousTimeLeft != TIME_LEFT)
        {
            ChatApi.AddLocalMessage(
                "<color=#FFFF00>Cast your vote for ZeepCentraal:</color><br>" +
                "<size=75%>" +
                "<size=50%><i>(hated it)</i></size> " +
                "<b><color=#FF0000>--</color></b> " +
                "<b><color=#FF8000>-</color></b> " +
                "<b><color=#FFFF00>-+</color>/<color=#FFFF00>+-</color></b> " +
                "<b><color=#80FF00>+</color></b> " +
                "<b><color=#00FF00>++</color></b> " +
                "<size=50%><i>(loved it)</i></size>" +
                "</size>"
            );

        }

        _previousTimeLeft = currentTimeLeft;
    }

    private void OnVoteSuccess(string vote)
    {
        MessengerApi.LogSuccess($"Voted {vote}");
    }

    private void OnVoteFail(string vote)
    {
        MessengerApi.LogError($"Vote {vote} failed");
    }

    public void DoubleDownvote()
    {
        VoteAsync(
            -2,
            () => { OnVoteSuccess("--"); },
            () => { OnVoteFail("--"); }
        ).Forget();
    }

    public void DoubleUpvote()
    {
        VoteAsync(
            2,
            () => { OnVoteSuccess("++"); },
            () => { OnVoteFail("++"); }
        ).Forget();
    }

    public void Downvote()
    {
        VoteAsync(
            -1,
            () => { OnVoteSuccess("-"); },
            () => { OnVoteFail("-"); }
        ).Forget();
    }

    public void Upvote()
    {
        VoteAsync(
            1,
            () => { OnVoteSuccess("+"); },
            () => { OnVoteFail("+"); }
        ).Forget();
    }

    public void NeutralVote()
    {
        VoteAsync(
            0,
            () => { OnVoteSuccess("-+/+-"); },
            () => { OnVoteFail("-+/+-"); }
        ).Forget();
    }

    private async UniTaskVoid VoteAsync(int voteValue, Action onSuccess, Action onFail)
    {
        string currentHash = LevelApi.CurrentHash;

        if (string.IsNullOrEmpty(currentHash))
        {
            _logger.LogError("Unable to vote because current level hash is empty");
            onFail();
            return;
        }

        HttpResponseMessage response = await _apiHttpClient.PostAsync(
            $"vote/submit",
            new VoteResource
            {
                Level = currentHash,
                Value = voteValue
            }
        );

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to vote");
            onFail();
            return;
        }

        onSuccess();
    }
}
