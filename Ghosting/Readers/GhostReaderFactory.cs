using System;
using System.IO;
using System.IO.Compression;
using EasyCompressor;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;

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

    public IGhost Read(byte[] buffer)
    {
        if (buffer == null || buffer.Length < sizeof(int) || buffer.Length > GhostLimits.MaxCompressedBytes)
            throw new InvalidDataException("Ghost data has invalid compressed size.");

        byte[] payload;
        if (IsGZip(buffer))
        {
            payload = DecompressGZip(buffer);
            return GetReader(GhostVersionReader.ReadBinary(payload)).Read(payload);
        }

        int rawVersion = GhostVersionReader.ReadBinary(buffer);
        if (rawVersion is >= 1 and <= 4)
        {
            return GetReader(rawVersion).Read(buffer);
        }

        payload = DecompressLzma(buffer);
        int version = GhostVersionReader.ReadProtobuf(payload);
        return GetReader(version).Read(payload);
    }

    private IGhostReader GetReader(int version)
    {
        _logger.LogInformation("Ghost version: {Version}", version);
        return version switch
        {
            1 => _provider.GetRequiredService<V1Reader>(),
            2 => _provider.GetRequiredService<V2Reader>(),
            3 => _provider.GetRequiredService<V3Reader>(),
            4 => _provider.GetRequiredService<V4Reader>(),
            5 => _provider.GetRequiredService<V5Reader>(),
            _ => throw new NotSupportedException($"Version {version} is not supported.")
        };
    }

    private static bool IsGZip(byte[] buffer) =>
        buffer.Length >= 2 && buffer[0] == 0x1f && buffer[1] == 0x8b;

    private static byte[] DecompressGZip(byte[] buffer)
    {
        using MemoryStream input = new(buffer, false);
        using GZipStream gzip = new(input, CompressionMode.Decompress);
        using LimitedMemoryStream output = new(GhostLimits.MaxDecompressedBytes);
        gzip.CopyTo(output);
        return output.ToArray();
    }

    private static byte[] DecompressLzma(byte[] buffer)
    {
        using MemoryStream input = new(buffer, false);
        using LimitedMemoryStream output = new(GhostLimits.MaxDecompressedBytes);
        LZMACompressor.Shared.Decompress(input, output);
        return output.ToArray();
    }
}
