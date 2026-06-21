using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;
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
    public const string ClientKey = "Ghosts";
    private const int MaxConcurrentDownloads = 20;

    private readonly IModStorage _modStorage;
    private readonly GhostReaderFactory _ghostReaderFactory;
    private readonly HttpClient _httpClient;
    private readonly SemaphoreSlim _downloadSlots = new(MaxConcurrentDownloads, MaxConcurrentDownloads);
    private readonly SemaphoreSlim _parseSlots = new(4, 4);
    private readonly Dictionary<int, Task<Result<IGhost>>> _downloads = new();
    private readonly object _downloadsLock = new();

    public GhostRepository(
        IModStorage modStorage,
        GhostReaderFactory ghostReaderFactory,
        HttpClient httpClient)
    {
        _modStorage = modStorage;
        _ghostReaderFactory = ghostReaderFactory;
        _httpClient = httpClient;
    }

    public async UniTask<Result<IGhost>> GetGhost(
        int recordId,
        string ghostUrl,
        CancellationToken cancellationToken = default)
    {
        IGhost cachedGhost = await ReadGhostFromDiskAsync(recordId, cancellationToken);
        if (cachedGhost != null)
            return Result.Ok(cachedGhost);

        Task<Result<IGhost>> download;
        lock (_downloadsLock)
        {
            if (!_downloads.TryGetValue(recordId, out download))
            {
                download = DownloadGhost(recordId, ghostUrl, cancellationToken);
                _downloads.Add(recordId, download);
            }
        }

        try
        {
            return await download;
        }
        finally
        {
            lock (_downloadsLock)
            {
                if (_downloads.TryGetValue(recordId, out Task<Result<IGhost>> current) &&
                    ReferenceEquals(current, download))
                {
                    _downloads.Remove(recordId);
                }
            }
        }
    }

    private async Task<Result<IGhost>> DownloadGhost(
        int recordId,
        string ghostUrl,
        CancellationToken cancellationToken)
    {
        await _downloadSlots.WaitAsync(cancellationToken);
        try
        {
            using HttpRequestMessage request = new(HttpMethod.Get, TransformGhostUrl(ghostUrl));
            using HttpResponseMessage response = await _httpClient.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength > GhostLimits.MaxCompressedBytes)
                return Result.Fail($"Ghost exceeds {GhostLimits.MaxCompressedBytes} byte compressed limit.");

            byte[] buffer = await ReadLimitedAsync(response.Content, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            IGhost downloadedGhost = await ParseGhostAsync(buffer, cancellationToken);
            _modStorage.WriteBlob(GetStorageKey(recordId), buffer);
            return Result.Ok(downloadedGhost);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail("Ghost download was cancelled.");
        }
        catch (Exception e)
        {
            return Result.Fail(new ExceptionalError(e));
        }
        finally
        {
            _downloadSlots.Release();
        }
    }

    private async Task<IGhost> ReadGhostFromDiskAsync(int recordId, CancellationToken cancellationToken)
    {
        await _parseSlots.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() =>
            {
                string storageKey = GetStorageKey(recordId);
                if (!_modStorage.BlobFileExists(storageKey))
                    return null;

                try
                {
                    byte[] buffer = _modStorage.ReadBlob(storageKey);
                    cancellationToken.ThrowIfCancellationRequested();
                    return ReadGhost(buffer);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    _modStorage.DeleteBlob(storageKey);
                    return null;
                }
            }, cancellationToken);
        }
        finally
        {
            _parseSlots.Release();
        }
    }

    private async Task<IGhost> ParseGhostAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        await _parseSlots.WaitAsync(cancellationToken);
        try
        {
            return await Task.Run(() => ReadGhost(buffer), cancellationToken);
        }
        finally
        {
            _parseSlots.Release();
        }
    }

    private IGhost ReadGhost(byte[] buffer)
    {
        if (buffer == null || buffer.Length == 0 || buffer.Length > GhostLimits.MaxCompressedBytes)
            throw new InvalidDataException("Ghost data has invalid compressed size.");

        IGhost ghost = _ghostReaderFactory.Read(buffer);
        return ghost ?? throw new InvalidDataException("Ghost reader returned no ghost.");
    }

    private static async Task<byte[]> ReadLimitedAsync(HttpContent content, CancellationToken cancellationToken)
    {
        using Stream input = await content.ReadAsStreamAsync();
        using LimitedMemoryStream output = new(GhostLimits.MaxCompressedBytes);
        byte[] copyBuffer = new byte[81_920];
        int read;
        while ((read = await input.ReadAsync(copyBuffer, 0, copyBuffer.Length, cancellationToken)) > 0)
            output.Write(copyBuffer, 0, read);
        return output.ToArray();
    }

    private static string GetStorageKey(int recordId) => "ghosts/" + recordId;

    private Uri TransformGhostUrl(string input) =>
        ServiceUriValidator.ResolveCdnPath(ConfigService.CdnUrl, input);
}
