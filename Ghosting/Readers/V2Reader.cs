using System;
using System.Collections.Generic;
using System.IO;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

public class V2Reader : GhostReaderBase<V2Ghost>
{
    public V2Reader(IServiceProvider provider) : base(provider)
    {
    }

    public override IGhost Read(byte[] data)
    {
        List<V2Ghost.Frame> frames = new();
        ulong steamId;
        int soapboxId;
        int hatId;
        int colorId;

        using MemoryStream ms = new(data);
        using (BinaryReader reader = new(ms))
        {
            reader.ReadInt32();
            steamId = reader.ReadUInt64();
            soapboxId = reader.ReadInt32();
            hatId = reader.ReadInt32();
            colorId = reader.ReadInt32();
            int frameCount = reader.ReadInt32();
            for (int i = 0; i < frameCount; i++)
            {
                float time = reader.ReadSingle();
                Vector3 position = new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Quaternion rotation = Quaternion.Euler(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                frames.Add(new V2Ghost.Frame(time, position, rotation));
            }
        }

        return CreateGhost(steamId, soapboxId, hatId, colorId, frames);
    }
}
