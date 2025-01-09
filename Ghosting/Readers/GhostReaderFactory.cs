using System;
using System.IO;
using System.IO.Compression;
using EasyCompressor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace TNRD.Zeepkist.GTR.Ghosting.Readers;

public class GhostReaderFactory
{
    private readonly ILogger<GhostReaderFactory> _logger;
    private readonly IServiceProvider _provider;

    public GhostReaderFactory(ILogger<GhostReaderFactory> logger, IServiceProvider provider)
    {
        _logger = logger;
        _provider = provider;
    }

    public IGhostReader GetReader(byte[] buffer)
    {
        int version = GetVersion(buffer);
        _logger.LogInformation("Ghost version: {Version}", version);

        switch (version)
        {
            case 1:
                return _provider.GetService<V1Reader>();
            case 2:
                return _provider.GetService<V2Reader>();
            case 3:
                return _provider.GetService<V3Reader>();
            case 4:
                return _provider.GetService<V4Reader>();
            case 5:
                return _provider.GetService<V5Reader>();
            default:
                throw new NotSupportedException($"Version {version} is not supported.");
        }
    }

    private int GetVersion(byte[] buffer)
    {
        if (IsLZMA(buffer, out _))
        {
            return 5;
        }

        if (IsGZipped(buffer, out byte[] decompressed))
        {
            using BinaryReader reader = new(new MemoryStream(decompressed));
            return reader.ReadInt32();
        }
        else
        {
            using MemoryStream stream = new(buffer);
            using BinaryReader reader = new(stream);
            return reader.ReadInt32();
        }
    }

    private static bool IsLZMA(byte[] buffer, out byte[] decompressed)
    {
        try
        {
            decompressed = LZMACompressor.Shared.Decompress(buffer);
            return true;
        }
        catch
        {
            decompressed = null;
            return false;
        }
    }

    private static bool IsGZipped(byte[] buffer, out byte[] decompressed)
    {
        try
        {
            decompressed = GZipCompressor.Shared.Decompress(buffer);
            return true;
        }
        catch
        {
            decompressed = null;
            return false;
        }
    }
}
