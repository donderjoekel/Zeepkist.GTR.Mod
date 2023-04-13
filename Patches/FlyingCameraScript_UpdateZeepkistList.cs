// This has been commented as this was causing a bug in the game


// using System.Collections.Generic;
// using HarmonyLib;
// using TNRD.Zeepkist.GTR.Mod.Components;
// using TNRD.Zeepkist.GTR.SDK.Models;
// using UnityEngine;
// using ZeepkistClient;
//
// namespace TNRD.Zeepkist.GTR.Mod.Patches;
//
// [HarmonyPatch(typeof(FlyingCameraScript), nameof(FlyingCameraScript.UpdateZeepkistList))]
// public class FlyingCameraScript_UpdateZeepkistList
// {
//     private static bool Prefix(FlyingCameraScript __instance)
//     {
//         __instance.targetList.Clear();
//
//         if (ZeepkistNetwork.IsConnected)
//             HandleOnlineGhosts(__instance);
//         else
//             HandleOfflineGhosts(__instance);
//
//         UpdateCurrentTarget(__instance);
//
//         __instance.ResetHelper();
//         return false;
//     }
//
//     private static void HandleOnlineGhosts(FlyingCameraScript instance)
//     {
//         foreach (ZeepkistNetworkPlayer player in ZeepkistNetwork.PlayerList)
//         {
//             NetworkedZeepkistGhost zeepkist = player.Zeepkist;
//             if (zeepkist != null && (int)zeepkist.player.UID != (int)ZeepkistNetwork.LocalPlayer.UID)
//                 instance.targetList.Add(new SpectatorZeepkistTarget()
//                 {
//                     transform = zeepkist.ghostModel.transform,
//                     name = zeepkist.player.Username
//                 });
//         }
//
//         foreach (KeyValuePair<RecordModel, GameObject> kvp in OnlineGhostLoader.RecordToGhost)
//         {
//             SpectatorZeepkistTarget szt = new SpectatorZeepkistTarget()
//             {
//                 name = $"PB - {kvp.Key.Time.GetFormattedTime()}",
//                 transform = kvp.Value.transform
//             };
//         
//             instance.targetList.Add(szt);
//         }
//     }
//
//     private static void HandleOfflineGhosts(FlyingCameraScript instance)
//     {
//         for (int index = 0; index < instance.GameMaster.carSetups.Count; ++index)
//         {
//             instance.targetList.Add(new SpectatorZeepkistTarget()
//             {
//                 transform = PlayerManager.Instance.currentMaster.carSetups[index].transform,
//                 name = PlayerManager.Instance.playersNames[index]
//             });
//         }
//
//         // TODO: Add offline loaded ghosts
//     }
//
//     private static void UpdateCurrentTarget(FlyingCameraScript instance)
//     {
//         for (int index = 0; index < instance.targetList.Count; ++index)
//         {
//             if (instance.currentTarget.transform == instance.targetList[index].transform)
//                 return;
//         }
//
//         instance.currentTarget = new SpectatorZeepkistTarget();
//         if (instance.targetList.Count != 0)
//             instance.currentTarget = instance.targetList[0];
//     }
// }
