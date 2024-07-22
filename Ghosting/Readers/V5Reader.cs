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
using Decoder = SevenZip.Compression.LZMA.Decoder;
using InputFlags = TNRD.Zeepkist.GTR.Ghosting.Recording.InputFlags;
using SoapboxFlags = TNRD.Zeepkist.GTR.Ghosting.Recording.SoapboxFlags;
using Vector3 = UnityEngine.Vector3;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

public class V5Reader : IGhostReader
{
    private readonly ILogger<V5Reader> _logger;

    public V5Reader(ILogger<V5Reader> logger)
    {
        _logger = logger;
    }

    public IGhost Read(byte[] data)
    {
        byte[] decompressed;
        try
        {
            decompressed = Decode(data);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to decompress ghost data");
            return null;
        }

        Ghost deserializedGhost;
        try
        {
            MemoryAlias::System.ReadOnlyMemory<byte> memory = new(decompressed);
            deserializedGhost = Serializer.Deserialize<Ghost>(memory);
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

        List<V5Ghost.Frame> frames = new();

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

        return new V5Ghost(
            deserializedGhost.TaggedUsername,
            ColorUtilities.FromHexString(deserializedGhost.Color),
            deserializedGhost.SteamId,
            cosmetics,
            frames);
    }

    private static byte[] Decode(byte[] buffer)
    {
        using MemoryStream inStream = new(buffer);
        using MemoryStream outStream = new();
        byte[] properties = new byte[5];
        inStream.Read(properties, 0, 5);
        byte[] lengthBuffer = new byte[8];
        inStream.Read(lengthBuffer, 0, 8);
        long length = BitConverter.ToInt64(lengthBuffer, 0);
        Decoder decoder = new();
        decoder.SetDecoderProperties(properties);
        decoder.Code(inStream, outStream, inStream.Length, length, null);
        outStream.Close();
        return outStream.ToArray();
    }

    private static bool IsGZipped(byte[] buffer)
    {
        return buffer[0] == 0x1f && buffer[1] == 0x8b;
    }

    private CosmeticIDs ReadCosmetics(BinaryReader reader)
    {
        CosmeticIDs ids = new();
        ids.zeepkist = reader.ReadInt32();
        ids.hat = reader.ReadInt32();
        ids.glasses = reader.ReadInt32();
        ids.paraglider = reader.ReadInt32();
        ids.horn = reader.ReadInt32();
        ids.color = reader.ReadInt32();
        ids.color_body = reader.ReadInt32();
        ids.color_leftArm = reader.ReadInt32();
        ids.color_rightArm = reader.ReadInt32();
        ids.color_leftLeg = reader.ReadInt32();
        ids.color_rightLeg = reader.ReadInt32();
        ids.frontWheels = reader.ReadInt32();
        ids.rearWheels = reader.ReadInt32();
        return ids;
    }

    private static float RemapToFloat(byte input, float min, float max)
    {
        float normalized = input / 255f;
        return Mathf.Lerp(min, max, normalized);
    }
}
