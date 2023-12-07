using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BepInEx.Logging;
using TNRD.Zeepkist.GTR.Cysharp.Threading.Tasks;
using TNRD.Zeepkist.GTR.DTOs.ResponseDTOs;
using TNRD.Zeepkist.GTR.FluentResults;
using TNRD.Zeepkist.GTR.Mod.Api.Levels.Models;
using TNRD.Zeepkist.GTR.SDK;
using TNRD.Zeepkist.GTR.SDK.Client;
using TNRD.Zeepkist.GTR.SDK.Models.Response;
using UnityEngine;
using ZeepSDK.Utilities;

namespace TNRD.Zeepkist.GTR.Mod.Api.Levels;

public static class InternalLevelApi
{
    private static readonly ManualLogSource logger = LoggerFactory.GetLogger(typeof(InternalLevelApi));

    public static string CurrentLevelHash { get; private set; }

    public static event Action LevelCreating;
    public static event Action LevelCreated;

    public static Result Create()
    {
        LevelCreating?.Invoke();
        CurrentLevelHash = null;
        LevelScriptableObject level = PlayerManager.Instance.currentMaster.GlobalLevel;

        if (level == null)
        {
            logger.LogError("No level available!");
            return Result.Fail("No level available");
        }

        try
        {
            string textToHash = GetTextToHash(level.LevelData);
            CurrentLevelHash = Hash(textToHash);
        }
        catch (Exception e)
        {
            logger.LogError("Unable to hash level: " + e);
            return Result.Fail("Unable to hash level");
        }

        LevelCreated?.Invoke();
        return Result.Ok();
    }

    public static string GetTextToHash(string[] lines)
    {
        string[] splits = lines[2].Split(',');

        string skyboxAndBasePlate = splits.Length != 6
            ? "unknown,unknown"
            : splits[^2] + "," + splits[^1];

        return string.Join("\n", lines.Skip(3).Prepend(skyboxAndBasePlate));
    }

    public static string Hash(string input)
    {
        using (SHA1 sha1 = SHA1.Create())
        {
            byte[] hash = sha1.ComputeHash(Encoding.UTF8.GetBytes(input));
            StringBuilder sb = new(hash.Length * 2);

            foreach (byte b in hash)
            {
                // can be "x2" if you want lowercase
                sb.Append(b.ToString("X2"));
            }

            return sb.ToString();
        }
    }
}
