using System;
using System.IO;
using System.IO.Compression;
using ZeepSDK.Storage;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

public class GhostReaderFactory
{
    private readonly IModStorage _modStorage;

    public GhostReaderFactory(IModStorage modStorage)
    {
        _modStorage = modStorage;
    }

    public IGhostReader GetReader(byte[] buffer)
    {
        int version = GetVersion(buffer);

        switch (version)
        {
            case 1:
                return new V1Reader();
            case 2:
                return new V2Reader();
            case 3:
                return new V3Reader();
            case 4:
                return new V4Reader();
            case 5:
                return new V5Reader(null);
            default:
                throw new NotSupportedException($"Version {version} is not supported.");
        }
    }

    private static int GetVersion(byte[] buffer)
    {
        if (IsLZMA(buffer))
        {
            return 5; // Hardcoded right now since there's nothing above 5. I'll deal with that if that happens
        }

        if (IsGZipped(buffer))
        {
            using MemoryStream ms = new(buffer);
            using GZipStream zip = new(ms, CompressionMode.Decompress);
            using BinaryReader reader = new(zip);
            return reader.ReadInt32();
        }
        else
        {
            using MemoryStream ms = new(buffer);
            using BinaryReader reader = new(ms);
            return reader.ReadInt32();
        }
    }

    private static bool IsGZipped(byte[] buffer)
    {
        return buffer[0] == 0x1f && buffer[1] == 0x8b;
    }

    private static bool IsLZMA(byte[] buffer)
    {
        if (buffer.Length < 13) return false;

        try
        {
            // Check LZMA header (first 5 bytes + 8 bytes for uncompressed size)
            using (MemoryStream memoryStream = new MemoryStream(buffer))
            {
                byte[] properties = new byte[5];
                byte[] uncompressedSizeBytes = new byte[8];

                memoryStream.Read(properties, 0, 5);
                memoryStream.Read(uncompressedSizeBytes, 0, 8);

                // Optionally, you can further validate the properties and uncompressed size if needed.

                // Try to create an LZMA decoder with the properties
                SevenZip.Compression.LZMA.Decoder decoder = new SevenZip.Compression.LZMA.Decoder();
                decoder.SetDecoderProperties(properties);

                // If no exceptions, the header is likely correct
                return true;
            }
        }
        catch
        {
            // If any exceptions are thrown, it is not valid LZMA compressed data
            return false;
        }
    }
}
