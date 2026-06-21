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
            int frameCount = GhostReaderValidation.ReadFrameCount(reader);
            for (int i = 0; i < frameCount; i++)
            {
                float time = reader.ReadSingle();
                float positionX = reader.ReadSingle();
                float positionY = reader.ReadSingle();
                float positionZ = reader.ReadSingle();
                float rotationX = reader.ReadSingle();
                float rotationY = reader.ReadSingle();
                float rotationZ = reader.ReadSingle();
                GhostReaderValidation.RequireFinite(
                    time, positionX, positionY, positionZ, rotationX, rotationY, rotationZ);
                Vector3 position = new(positionX, positionY, positionZ);
                Quaternion rotation = Quaternion.Euler(rotationX, rotationY, rotationZ);
                frames.Add(new V1Ghost.Frame(time, position, rotation));
            }
        }

        return CreateGhost(frames);
    }
}
