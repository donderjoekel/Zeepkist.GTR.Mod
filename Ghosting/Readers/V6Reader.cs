extern alias MemoryAlias;
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Ghosting.Recording.Data;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;
using ZeepkistNetworking;
using InputFlags = TNRD.Zeepkist.GTR.Ghosting.Recording.InputFlags;
using SoapboxFlags = TNRD.Zeepkist.GTR.Ghosting.Recording.SoapboxFlags;
using Vector3 = UnityEngine.Vector3;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

public class V6Reader : GhostReaderBase<V6Ghost>
{
    private const float PositionMultiplier = 100_000;
    private const float RotationMultiplier = 100;

    private readonly ILogger<V6Reader> _logger;

    public V6Reader(IServiceProvider provider, ILogger<V6Reader> logger) : base(provider)
    {
        _logger = logger;
    }

    public override IGhost Read(byte[] data)
    {
        try
        {
            MemoryAlias::System.ReadOnlyMemory<byte> memory = new(data);
            Ghost deserializedGhost = Serializer.Deserialize<Ghost>(memory);
            if (deserializedGhost?.InitialFrame == null ||
                deserializedGhost.Cosmetics == null ||
                deserializedGhost.DeltaFrames == null ||
                deserializedGhost.DeltaFrames.Count > GhostLimits.MaxFrames ||
                deserializedGhost.Version != 6)
            {
                throw new InvalidDataException("Ghost payload is incomplete or contains too many frames.");
            }

            CosmeticIDs cosmetics = new()
            {
                color = deserializedGhost.Cosmetics.Color,
                color_body = deserializedGhost.Cosmetics.ColorBody,
                color_leftArm = deserializedGhost.Cosmetics.ColorLeftArm,
                color_leftLeg = deserializedGhost.Cosmetics.ColorLeftLeg,
                color_rightArm = deserializedGhost.Cosmetics.ColorRightArm,
                color_rightLeg = deserializedGhost.Cosmetics.ColorRightLeg,
                frontWheels = deserializedGhost.Cosmetics.FrontWheels,
                glasses = deserializedGhost.Cosmetics.Glasses,
                hat = deserializedGhost.Cosmetics.Hat,
                horn = deserializedGhost.Cosmetics.Horn,
                paraglider = deserializedGhost.Cosmetics.Paraglider,
                rearWheels = deserializedGhost.Cosmetics.RearWheels,
                zeepkist = deserializedGhost.Cosmetics.Zeepkist
            };

            var frames = new List<V6Ghost.Frame>(deserializedGhost.DeltaFrames.Count + 1);
            bool ragdollActive = deserializedGhost.InitialFrame.RagdollState;
            Vector3? ragdollPosition = null;
            Vector3? ragdollRotationEuler = null;

            if (ragdollActive)
            {
                ragdollPosition = RequireScaledVector3Int(
                    deserializedGhost.InitialFrame.RagdollPosition,
                    PositionMultiplier);
                ragdollRotationEuler = RequireScaledVector3Int(
                    deserializedGhost.InitialFrame.RagdollRotation,
                    RotationMultiplier);
            }

            V6Ghost.Frame previousFrame = new(
                0,
                deserializedGhost.InitialFrame.Position,
                Quaternion.Euler(deserializedGhost.InitialFrame.Rotation),
                deserializedGhost.InitialFrame.Speed,
                deserializedGhost.InitialFrame.Steering,
                (InputFlags)(byte)deserializedGhost.InitialFrame.InputFlags,
                (SoapboxFlags)(byte)deserializedGhost.InitialFrame.SoapboxFlags,
                ragdollActive,
                ragdollPosition,
                ragdollRotationEuler.HasValue ? Quaternion.Euler(ragdollRotationEuler.Value) : null);

            frames.Add(previousFrame);

            foreach (DeltaFrame deltaFrame in deserializedGhost.DeltaFrames)
            {
                Vector3 deltaPosition = FromScaledVector3Int(deltaFrame.Position, PositionMultiplier);
                Vector3 totalPosition = previousFrame.Position + deltaPosition;

                if (ragdollActive && !deltaFrame.RagdollState)
                    throw new InvalidDataException("Ragdoll state cannot leave ragdoll mode.");

                if (deltaFrame.RagdollState)
                {
                    Vector3 decodedRagdollPosition = RequireScaledVector3Int(
                        deltaFrame.RagdollPosition,
                        PositionMultiplier);
                    Vector3 decodedRagdollRotation = RequireScaledVector3Int(
                        deltaFrame.RagdollRotation,
                        RotationMultiplier);

                    ragdollPosition = ragdollActive
                        ? ragdollPosition.GetValueOrDefault() + decodedRagdollPosition
                        : decodedRagdollPosition;
                    ragdollRotationEuler = ragdollActive
                        ? ragdollRotationEuler.GetValueOrDefault() + decodedRagdollRotation
                        : decodedRagdollRotation;
                    ragdollActive = true;
                }

                V6Ghost.Frame frame = new(
                    deltaFrame.Time,
                    totalPosition,
                    Quaternion.Euler(FromScaledVector3Int(deltaFrame.Rotation, RotationMultiplier)),
                    deltaFrame.Speed,
                    deltaFrame.Steering,
                    (InputFlags)(byte)deltaFrame.InputFlags,
                    (SoapboxFlags)(byte)deltaFrame.SoapboxFlags,
                    ragdollActive,
                    ragdollPosition,
                    ragdollRotationEuler.HasValue ? Quaternion.Euler(ragdollRotationEuler.Value) : null);
                frames.Add(frame);
                previousFrame = frame;
            }

            return CreateGhost(
                deserializedGhost.TaggedUsername,
                ColorUtilities.FromHexString(deserializedGhost.Color),
                deserializedGhost.SteamId,
                cosmetics,
                frames);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to deserialize ghost data");
            return null;
        }
    }

    private static Vector3 FromScaledVector3Int(TNRD.Zeepkist.GTR.Ghosting.Recording.Data.Vector3Int value, float multiplier)
    {
        return new Vector3(
            value.X / multiplier,
            value.Y / multiplier,
            value.Z / multiplier);
    }

    private static Vector3 RequireScaledVector3Int(
        TNRD.Zeepkist.GTR.Ghosting.Recording.Data.Vector3Int value,
        float multiplier)
    {
        return FromScaledVector3Int(value, multiplier);
    }
}
