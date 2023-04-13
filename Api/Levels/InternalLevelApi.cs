using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels.Models;
using TNRD.Zeepkist.GTR.SDK;
using TNRD.Zeepkist.GTR.SDK.Client;
using TNRD.Zeepkist.GTR.SDK.Models.Response;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Mod.Api.Levels;

public static class InternalLevelApi
{
    public static int CurrentLevelId { get; private set; }

    private static DirectoryOrLevel FindWorkshopLevel(DirectoryOrLevel directoryOrLevel, string uid)
    {
        if (directoryOrLevel.isLevel)
            return directoryOrLevel.UID == uid ? directoryOrLevel : null;

        foreach (DirectoryOrLevel dir in directoryOrLevel.contents)
        {
            DirectoryOrLevel temp = FindWorkshopLevel(dir, uid);
            if (temp != null)
                return temp;
        }

        return null;
    }

    private static bool IsAdventureLevel(LevelScriptableObject level)
    {
        if (level.IsAdventureLevel)
            return true;

        return LevelManager.Instance.AllAdventureLevels.Levels.Any(x => x.UID == level.UID);
    }

    public static async UniTask<Result> Create()
    {
        CurrentLevelId = -1;
        LevelScriptableObject level = PlayerManager.Instance.currentMaster.GlobalLevel;

        Result<LevelsGetResponseDTO> getLevelResult = await LevelsApi.Get(builder => builder.WithUid(level.UID));
        if (getLevelResult.IsSuccess)
        {
            if (getLevelResult.Value.Levels.Count == 1)
            {
                CurrentLevelId = getLevelResult.Value.Levels.First().Id;
                return Result.Ok();
            }
        }

        Plugin.Log.LogInfo($"Creating level metadata for: {level.UID}");
        if (string.IsNullOrEmpty(level.UID))
        {
            Plugin.Log.LogError("Level UID is empty");
            return Result.Fail("Level UID is empty");
        }

        if (!IsAdventureLevel(level) && level.WorkshopID == 0)
        {
            return Result.Fail("No workshop id available");
        }

        string thumbnailB64 = await CreateThumbnail(level);

        if (!IsAdventureLevel(level) && string.IsNullOrEmpty(thumbnailB64))
            return Result.Fail("No thumbnail available");

        CreateLevelRequestModel createLevelRequestModel = new CreateLevelRequestModel()
        {
            Author = level.Author,
            Name = level.Name,
            Uid = level.UID,
            Wid = level.WorkshopID.ToString(),
            TimeAuthor = level.TimeAuthor,
            TimeBronze = level.TimeBronze,
            TimeGold = level.TimeGold,
            TimeSilver = level.TimeSilver,
            Thumbnail = thumbnailB64,
            IsValid = level.IsValidated
        };

        Result<CreateLevelResponseModel> result =
            await ApiClient.Instance.Post<CreateLevelResponseModel>("levels/create", createLevelRequestModel);

        if (result.IsFailed)
            return result.ToResult();

        CurrentLevelId = result.Value.Id;
        return Result.Ok();
    }

    private static async Task<string> CreateThumbnail(LevelScriptableObject level)
    {
        string thumbnailB64 = string.Empty;

        try
        {
            if (!level.IsAdventureLevel)
            {
                DirectoryOrLevel dirOrLevel = FindWorkshopLevel(LevelManager.Instance.WorkshopDirectory, level.UID);
                if (dirOrLevel != null)
                {
                    string[] files = Directory.GetFiles(dirOrLevel.URL, "*.jpg");
                    if (files.Length == 1)
                    {
                        byte[] bytes = await File.ReadAllBytesAsync(files.First());

                        if (bytes.Length > 500000)
                        {
                            Texture2D texture = new Texture2D(1,1);
                            texture.LoadImage(bytes);
                            bytes = texture.EncodeToJPG(50);
                        }

                        thumbnailB64 = Convert.ToBase64String(bytes);
                    }
                }
            }
        }
        catch (Exception e)
        {
            Plugin.Log.LogError(e);
        }

        return thumbnailB64;
    }
}
