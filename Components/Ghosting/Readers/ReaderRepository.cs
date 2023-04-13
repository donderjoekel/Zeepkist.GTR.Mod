using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace TNRD.Zeepkist.GTR.Mod.Components.Ghosting.Readers;

public class ReaderRepository
{
    private static readonly Dictionary<int, Type> versionToReaderType = new Dictionary<int, Type>()
    {
        { 1, typeof(V1Reader) },
        { 2, typeof(V2Reader) },
        { 3, typeof(V3Reader) },
        { 4, typeof(V4Reader) }
    };

    public static IGhostReader GetReader(byte[] buffer)
    {
        int version = GetVersion(buffer);

        return versionToReaderType.TryGetValue(version, out Type readerType)
            ? (IGhostReader)Activator.CreateInstance(readerType)
            : null;
    }

    private static int GetVersion(byte[] buffer)
    {
        if (IsGZipped(buffer))
        {
            using (MemoryStream ms = new MemoryStream(buffer))
            {
                using (GZipStream zip = new GZipStream(ms, CompressionMode.Decompress))
                {
                    using (BinaryReader reader = new BinaryReader(zip))
                    {
                        return reader.ReadInt32();
                    }
                }
            }
        }
        else
        {
            using (MemoryStream ms = new MemoryStream(buffer))
            {
                using (BinaryReader reader = new BinaryReader(ms))
                {
                    return reader.ReadInt32();
                }
            }
        }
    }

    private static bool IsGZipped(byte[] buffer)
    {
        return buffer[0] == 0x1f && buffer[1] == 0x8b;
    }
}
