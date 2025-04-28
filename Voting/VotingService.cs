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
            ChatApi.AddLocalMessage("Time to cast your vote! (-- - + ++)");
        }

        _previousTimeLeft = currentTimeLeft;
    }

    public void DoubleDownvote()
    {
        VoteAsync("ddownvote",
                () => { MessengerApi.LogSuccess("--'d successfully"); },
                () => { MessengerApi.LogError("Failed to --"); })
            .Forget();
    }

    public void DoubleUpvote()
    {
        VoteAsync("dupvote",
                () => { MessengerApi.LogSuccess("++'d successfully"); },
                () => { MessengerApi.LogError("Failed to ++"); })
            .Forget();
    }

    public void Downvote()
    {
        VoteAsync("downvote",
                () => { MessengerApi.LogSuccess("-'d successfully"); },
                () => { MessengerApi.LogError("Failed to -"); })
            .Forget();
    }

    public void Upvote()
    {
        VoteAsync("upvote",
                () => { MessengerApi.LogSuccess("+'d successfully"); },
                () => { MessengerApi.LogError("Failed to +"); })
            .Forget();
    }

    private async UniTaskVoid VoteAsync(string path, Action onSuccess, Action onFail)
    {
        string currentHash = LevelApi.CurrentHash;
        if (string.IsNullOrEmpty(currentHash))
        {
            _logger.LogError("Unable to vote because current level hash is empty");
            onFail();
            return;
        }

        HttpResponseMessage response =
            await _apiHttpClient.PostAsync($"vote/{path}", new VoteResource { Level = currentHash });

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
