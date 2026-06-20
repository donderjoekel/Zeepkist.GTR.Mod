using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Ghosting.Readers;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Storage;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostRepository
{
    private readonly IModStorage _modStorage;
    private readonly GhostReaderFactory _ghostReaderFactory;
    private readonly HttpClient _httpClient;
    private readonly ConfigService _configService;

    public GhostRepository(
        IModStorage modStorage,
        GhostReaderFactory ghostReaderFactory,
        HttpClient httpClient,
        ConfigService configService)
    {
        _modStorage = modStorage;
        _ghostReaderFactory = ghostReaderFactory;
        _httpClient = httpClient;
        _configService = configService;
    }

    public async UniTask<Result<IGhost>> GetGhost(int recordId, string ghostUrl)
    {
        if (TryGetGhostFromDisk(recordId, out IGhost ghost))
            return Result.Ok(ghost);

        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, TransformGhostUrl(ghostUrl));
            using HttpResponseMessage response =
                await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength > GhostLimits.MaxCompressedBytes)
                return Result.Fail($"Ghost exceeds {GhostLimits.MaxCompressedBytes} byte compressed limit.");

            byte[] buffer = await ReadLimitedAsync(response.Content);
            IGhost downloadedGhost = ReadGhost(buffer);
            _modStorage.WriteBlob(GetStorageKey(recordId), buffer);
            return Result.Ok(downloadedGhost);
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }
    }

    private bool TryGetGhostFromDisk(int recordId, out IGhost ghost)
    {
        string storageKey = GetStorageKey(recordId);
        if (!_modStorage.BlobFileExists(storageKey))
        {
            ghost = null;
            return false;
        }

        try
        {
            byte[] buffer = _modStorage.ReadBlob(storageKey);
            ghost = ReadGhost(buffer);
            return true;
        }
        catch
        {
            _modStorage.DeleteBlob(storageKey);
            ghost = null;
            return false;
        }
    }

    private IGhost ReadGhost(byte[] buffer)
    {
        if (buffer == null || buffer.Length == 0 || buffer.Length > GhostLimits.MaxCompressedBytes)
            throw new InvalidDataException("Ghost data has invalid compressed size.");

        IGhost ghost = _ghostReaderFactory.GetReader(buffer).Read(buffer);
        return ghost ?? throw new InvalidDataException("Ghost reader returned no ghost.");
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content)
    {
        using Stream input = await content.ReadAsStreamAsync();
        using LimitedMemoryStream output = new(GhostLimits.MaxCompressedBytes);
        byte[] copyBuffer = new byte[81_920];
        int read;
        while ((read = await input.ReadAsync(copyBuffer, 0, copyBuffer.Length)) > 0)
            output.Write(copyBuffer, 0, read);
        return output.ToArray();
    }

    private static string GetStorageKey(int recordId) => "ghosts/" + recordId;

    private string TransformGhostUrl(string input)
    {
        if (input.StartsWith("http"))
            return input;
        string output = _configService.CdnUrl.Value;
        if (!input.StartsWith('/'))
            output += '/';
        output += input;
        return output;
    }
}
