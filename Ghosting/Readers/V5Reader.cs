extern alias MemoryAlias;
using System;
using System.Collections.Generic;
using System.IO;
using EasyCompressor;
using Microsoft.Extensions.Logging;
using ProtoBuf;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Ghosting.Recording.Data;
using TNRD.Zeepkist.GTR.Utilities;
using UnityEngine;
using ZeepkistNetworking;
using Decoder = SevenZip.Compression.LZMA.Decoder;
using InputFlags = TNRD.Zeepkist.GTR.Ghosting.Recording.InputFlags;
using SoapboxFlags = TNRD.Zeepkist.GTR.Ghosting.Recording.SoapboxFlags;
using Vector3 = UnityEngine.Vector3;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

public class V5Reader : GhostReaderBase<V5Ghost>
{
    private readonly ILogger<V5Reader> _logger;

    public V5Reader(IServiceProvider provider, ILogger<V5Reader> logger) : base(provider)
    {
        _logger = logger;
    }

    public override IGhost Read(byte[] data)
    {
        Ghost deserializedGhost;
        try
        {
            MemoryAlias::System.ReadOnlyMemory<byte> memory = new(data);
            deserializedGhost = Serializer.Deserialize<Ghost>(memory);
            if (deserializedGhost?.InitialFrame == null ||
                deserializedGhost.Cosmetics == null ||
                deserializedGhost.DeltaFrames == null ||
                deserializedGhost.DeltaFrames.Count > GhostLimits.MaxFrames ||
                deserializedGhost.Version != 5)
            {
                throw new InvalidDataException("Ghost payload is incomplete or contains too many frames.");
            }
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to deserialize ghost data");
            return null;
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

        List<V5Ghost.Frame> frames = new(deserializedGhost.DeltaFrames.Count + 1);

        V5Ghost.Frame previousFrame = new(
            0,
            deserializedGhost.InitialFrame.Position,
            Quaternion.Euler(deserializedGhost.InitialFrame.Rotation),
            deserializedGhost.InitialFrame.Speed,
            deserializedGhost.InitialFrame.Steering,
            (InputFlags)(byte)deserializedGhost.InitialFrame.InputFlags,
            (SoapboxFlags)(byte)deserializedGhost.InitialFrame.SoapboxFlags);

        frames.Add(previousFrame);

        foreach (DeltaFrame deltaFrame in deserializedGhost.DeltaFrames)
        {
            const float positionMultiplier = 100_000;
            const float rotationMultiplier = 100;
            Vector3 deltaPosition = new(
                deltaFrame.Position.X / positionMultiplier,
                deltaFrame.Position.Y / positionMultiplier,
                deltaFrame.Position.Z / positionMultiplier);
            Vector3 totalPosition = previousFrame.Position + deltaPosition;
            V5Ghost.Frame frame = new(
                deltaFrame.Time,
                totalPosition,
                Quaternion.Euler(
                    new Vector3(
                        deltaFrame.Rotation.X / rotationMultiplier,
                        deltaFrame.Rotation.Y / rotationMultiplier,
                        deltaFrame.Rotation.Z / rotationMultiplier)),
                deltaFrame.Speed,
                deltaFrame.Steering,
                (InputFlags)(byte)deltaFrame.InputFlags,
                (SoapboxFlags)(byte)deltaFrame.SoapboxFlags);
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

}
