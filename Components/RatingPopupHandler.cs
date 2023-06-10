using System.Collections;
using System.Linq;
using BepInEx.Logging;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.DTOs.ResponseModels;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels;
using TNRD.Zeepkist.GTR.Mod.Patches;
using TNRD.Zeepkist.GTR.SDK;
using TNRD.Zeepkist.GTR.SDK.Models;
using TNRD.Zeepkist.GTR.SDK.Models.Response;
using TNRD.Zeepkist.GTR.UI;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Components;

public class RatingPopupHandler : MonoBehaviour
{
    private readonly ManualLogSource logger = EntryPoint.CreateLogger(nameof(RatingPopupHandler));

    private GameObject ratingCanvasPrefab;
    private int lastScore;

    private void Awake()
    {
        ratingCanvasPrefab = Plugin.AssetBundle.LoadAsset<GameObject>("RatingCanvas");
        PhotonZeepkist_DoShowBuffer.DoShowBuffer += OnDoShowBuffer;
    }

    private void OnDestroy()
    {
        PhotonZeepkist_DoShowBuffer.DoShowBuffer -= OnDoShowBuffer;
    }

    private void OnDoShowBuffer()
    {
        if (!Plugin.ConfigEnableVoting.Value)
            return;

        if (InternalLevelApi.CurrentLevelId == -1)
            return;

        LoadAndShow().Forget();
    }

    private async UniTaskVoid LoadAndShow()
    {
        lastScore = -1;

        PlayerManager.Instance.cursorManager.SetCursorEnabled(true);
        GameObject instance = Instantiate(ratingCanvasPrefab);
        StarButtons starButtons = instance.GetComponentInChildren<StarButtons>();
        FavoriteButton favoriteButton = instance.GetComponentInChildren<FavoriteButton>();

        starButtons.Clicked += OnStarButtonClicked;
        starButtons.AllDeactivated += OnAllDeactivated;
        favoriteButton.Clicked += OnFavoriteButtonClicked;

        Result<FavoritesGetAllResponseDTO> getFavoriteResult = await Sdk.Instance.FavoritesApi.Get(builder =>
            builder.WithLevelId(InternalLevelApi.CurrentLevelId).WithUserId(Sdk.Instance.UsersApi.UserId));
        Result<VotesGetResponseDTO> getVotesResult = await Sdk.Instance.VotesApi.Get(builder =>
            builder.WithLevelId(InternalLevelApi.CurrentLevelId).WithUserId(Sdk.Instance.UsersApi.UserId));

        if (getFavoriteResult.IsFailed)
        {
            PlayerManager.Instance.messenger.LogError("[GTR] Unable to get favorite status", 2.5f);
            logger.LogError(getFavoriteResult.ToString());
            return;
        }

        if (getFavoriteResult.Value.Favorites.Count > 0)
        {
            favoriteButton.SetActive();
        }
        else
        {
            favoriteButton.SetInactive();
        }

        if (getVotesResult.IsFailed)
        {
            PlayerManager.Instance.messenger.LogError("[GTR] Unable to get vote status", 2.5f);
            logger.LogError(getVotesResult.ToString());
            return;
        }

        if (getVotesResult.Value.Votes.Count > 0)
        {
            VoteResponseModel vote = getVotesResult.Value.Votes.FirstOrDefault(x => x.Category == 0);
            if (vote == null)
            {
                starButtons.DeactivateAll();
            }
            else
            {
                starButtons.ActivateUntil(vote.Score.Value - 1);
                lastScore = vote.Score.Value - 1;
            }
        }
        else
        {
            starButtons.DeactivateAll();
        }

        StartCoroutine(MoveIn(starButtons.transform as RectTransform));
    }

    private void OnAllDeactivated(StarButtons starButtons)
    {
        starButtons.ActivateUntil(lastScore);
    }

    private void OnStarButtonClicked(StarButtons starButtons, int index)
    {
        lastScore = index;
        starButtons.ActivateUntil(index);
        SubmitVote(index).Forget();
    }

    private async UniTaskVoid SubmitVote(int index)
    {
        int score = index + 1;

        Result submitResult = await Sdk.Instance.VotesApi.Submit(builder =>
            builder.WithLevel(InternalLevelApi.CurrentLevelId).WithScore(score));

        if (submitResult.IsFailed)
        {
            PlayerManager.Instance.messenger.LogError("[GTR] Failed to submit vote", 2.5f);
            logger.LogError(submitResult.ToString());
            return;
        }

        PlayerManager.Instance.messenger.Log("[GTR] Submitted vote", 2.5f);

        Result upvoteResult = null;
        if (score > 3)
        {
            Result<GenericIdResponseDTO> result =
                await Sdk.Instance.UpvotesApi.Add(builder => builder.WithLevelId(InternalLevelApi.CurrentLevelId));
            upvoteResult = result.ToResult();
        }
        else if (score < 3)
        {
            Result<UpvotesGetResponseDTO> getUpvoteResult = await Sdk.Instance.UpvotesApi.Get(builder =>
            {
                builder
                    .WithLevelId(InternalLevelApi.CurrentLevelId)
                    .WithUserId(Sdk.Instance.UsersApi.UserId);
            });

            if (getUpvoteResult.IsFailed)
            {
                upvoteResult = getUpvoteResult.ToResult();
            }
            else if (getUpvoteResult.Value != null && getUpvoteResult.Value.Upvotes.Count > 0)
            {
                upvoteResult = await Sdk.Instance.UpvotesApi.Remove(getUpvoteResult.Value.Upvotes.First().Id);
            }
            else
            {
                upvoteResult = Result.Ok();
            }
        }

        if (upvoteResult != null && upvoteResult.IsFailed)
            logger.LogError(upvoteResult.ToString());
    }

    private void OnFavoriteButtonClicked(FavoriteButton favoriteButton)
    {
        HandleFavorite(favoriteButton).Forget();
    }

    private async UniTaskVoid HandleFavorite(FavoriteButton favoriteButton)
    {
        Result result;

        if (favoriteButton.IsActive)
        {
            Result<GenericIdResponseDTO> addResult =
                await Sdk.Instance.FavoritesApi.Add(builder => builder.WithLevelId(InternalLevelApi.CurrentLevelId));
            result = addResult.ToResult();
        }
        else
        {
            Result<FavoritesGetAllResponseDTO> getFavoriteResult = await Sdk.Instance.FavoritesApi.Get(builder =>
            {
                builder
                    .WithLevelId(InternalLevelApi.CurrentLevelId)
                    .WithUserId(Sdk.Instance.UsersApi.UserId);
            });

            if (getFavoriteResult.IsFailed)
            {
                result = getFavoriteResult.ToResult();
            }
            else if (getFavoriteResult.Value != null && getFavoriteResult.Value.Favorites.Count > 0)
            {
                result = await Sdk.Instance.FavoritesApi.Remove(getFavoriteResult.Value.Favorites.First().Id);
            }
            else
            {
                result = Result.Ok();
            }
        }

        if (result.IsFailed)
        {
            PlayerManager.Instance.messenger.LogError("[GTR] Failed to submit favorite", 2.5f);
            logger.LogError(result.ToString());
        }
        else
        {
            PlayerManager.Instance.messenger.Log("[GTR] Submitted favorite", 2.5f);
        }
    }

    private IEnumerator MoveIn(RectTransform rectTransform)
    {
        Vector2 velocity = Vector2.zero;

        while (Vector2.Distance(rectTransform.anchoredPosition, Vector2.zero) > 0.0f)
        {
            rectTransform.anchoredPosition =
                Vector2.SmoothDamp(rectTransform.anchoredPosition, Vector2.zero, ref velocity, 0.25f);
            yield return null;
        }
    }
}
