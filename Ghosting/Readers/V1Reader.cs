using System;
using System.Collections.Generic;
using System.IO;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using UnityEngine;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

public class V1Reader : GhostReaderBase<V1Ghost>
{
    public V1Reader(IServiceProvider provider) : base(provider)
    {
    }

    public override IGhost Read(byte[] data)
    {
        List<V1Ghost.Frame> frames = new();

        using MemoryStream ms = new(data);
        using (BinaryReader reader = new(ms))
        {
            reader.ReadInt32();
            int frameCount = reader.ReadInt32();
            for (int i = 0; i < frameCount; i++)
            {
                float time = reader.ReadSingle();
                Vector3 position = new(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                Quaternion rotation = Quaternion.Euler(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                frames.Add(new V1Ghost.Frame(time, position, rotation));
            }
        }

        return CreateGhost(frames);
    }
}
