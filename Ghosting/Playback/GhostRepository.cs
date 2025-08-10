using System;
using System.Net.Http;
using Newtonsoft.Json;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Ghosting.Readers;
using TNRD.Zeepkist.GTR.Json;
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
        HttpClient httpClient, ConfigService configService)
    {
        _modStorage = modStorage;
        _ghostReaderFactory = ghostReaderFactory;
        _httpClient = httpClient;
        _configService = configService;
    }

    public async UniTask<Result<IGhost>> GetGhost(int recordId, string ghostUrl)
    {
        if (TryGetGhostFromDisk(recordId, out IGhost ghost))
        {
            return Result.Ok(ghost);
        }

        HttpResponseMessage response;

        try
        {
            response = await _httpClient.GetAsync(TransformGhostUrl(ghostUrl));
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        try
        {
            response.EnsureSuccessStatusCode();
        }
        catch (HttpRequestException e)
        {
            return Result.Fail(new ExceptionalError(e));
        }

        byte[] buffer = await response.Content.ReadAsByteArrayAsync();
        _modStorage.WriteBlob("ghosts/" + recordId, buffer);
        return Result.Ok(_ghostReaderFactory.GetReader(buffer).Read(buffer));
    }

    private bool TryGetGhostFromDisk(int recordId, out IGhost ghost)
    {
        if (!_modStorage.BlobFileExists("ghosts/" + recordId))
        {
            ghost = null;
            return false;
        }

        byte[] buffer = _modStorage.ReadBlob("ghosts/" + recordId);
        ghost = _ghostReaderFactory.GetReader(buffer).Read(buffer);
        return ghost != null;
    }

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
