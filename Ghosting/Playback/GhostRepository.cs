using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Ghosting.Readers;
using TNRD.Zeepkist.GTR.Utilities;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Storage;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostRepository
{
    private sealed class SharedDownload : IDisposable
    {
        public CancellationTokenSource Cancellation { get; } = new();
        public Task<Result<IGhost>> Task { get; set; }
        public int WaiterCount { get; set; }

        public void Dispose()
        {
            Cancellation.Dispose();
        }
    }

    public const string ClientKey = "Ghosts";
    private const string CacheIndexKey = "ghost-cache-index";
    private const int MaxConcurrentDownloads = 4;
    private const int MaxConcurrentParses = 1;

    private readonly IModStorage _modStorage;
    private readonly GhostReaderFactory _ghostReaderFactory;
    private readonly HttpClient _httpClient;
    private readonly long _maximumCacheBytes;
    private readonly object _cacheLock = new();
    private readonly Dictionary<int, GhostCacheEntry> _cacheEntries = new();
    private readonly SemaphoreSlim _downloadSlots = new(MaxConcurrentDownloads, MaxConcurrentDownloads);
    private readonly SemaphoreSlim _parseSlots = new(MaxConcurrentParses, MaxConcurrentParses);
    private readonly Dictionary<int, SharedDownload> _downloads = new();
    private readonly object _downloadsLock = new();

    public GhostRepository(
        IModStorage modStorage,
        GhostReaderFactory ghostReaderFactory,
        HttpClient httpClient,
        long maximumCacheBytes)
    {
        _modStorage = modStorage;
        _ghostReaderFactory = ghostReaderFactory;
        _httpClient = httpClient;
        _maximumCacheBytes = maximumCacheBytes;
        LoadCacheIndex();
    }

    public async UniTask<Result<IGhost>> GetGhost(
        int recordId,
        string ghostUrl,
        CancellationToken cancellationToken = default)
    {
        IGhost cachedGhost = await ReadGhostFromDiskAsync(recordId, cancellationToken);
        if (cachedGhost != null)
            return Result.Ok(cachedGhost);

        SharedDownload download;
        lock (_downloadsLock)
        {
            if (!_downloads.TryGetValue(recordId, out download))
            {
                download = new SharedDownload();
                download.Task = DownloadGhost(recordId, ghostUrl, download.Cancellation.Token);
                _downloads.Add(recordId, download);
            }

            download.WaiterCount++;
        }

        try
        {
            return await TaskCancellation.WaitAsync(download.Task, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail("Ghost download wait was cancelled.");
        }
        finally
        {
            lock (_downloadsLock)
            {
                download.WaiterCount--;
                if (download.WaiterCount == 0 &&
                    _downloads.TryGetValue(recordId, out SharedDownload current) &&
                    ReferenceEquals(current, download))
                {
                    if (!download.Task.IsCompleted)
                        download.Cancellation.Cancel();
                    _downloads.Remove(recordId);
                    download.Dispose();
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
            WriteCachedGhost(recordId, buffer);
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
                try
                {
                    byte[] buffer;
                    lock (_cacheLock)
                    {
                        if (!_modStorage.BlobFileExists(storageKey))
                            return null;
                        buffer = _modStorage.ReadBlob(storageKey);
                        if (TouchCacheEntry(recordId, buffer.Length))
                            SaveCacheIndex();
                    }
                    cancellationToken.ThrowIfCancellationRequested();
                    return ReadGhost(buffer);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                    lock (_cacheLock)
                    {
                        _modStorage.DeleteBlob(storageKey);
                        _cacheEntries.Remove(recordId);
                        SaveCacheIndex();
                    }
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

    private void LoadCacheIndex()
    {
        lock (_cacheLock)
        {
            if (!_modStorage.JsonFileExists(CacheIndexKey))
                return;

            try
            {
                GhostCacheIndex index = _modStorage.LoadFromJson<GhostCacheIndex>(CacheIndexKey);
                if (index?.Entries == null)
                    return;

                foreach (GhostCacheEntry entry in index.Entries)
                {
                    if (entry.Size > 0 && _modStorage.BlobFileExists(GetStorageKey(entry.RecordId)))
                        _cacheEntries[entry.RecordId] = entry;
                }
            }
            catch
            {
                _cacheEntries.Clear();
                _modStorage.DeleteJsonFile(CacheIndexKey);
            }
        }
    }

    private void WriteCachedGhost(int recordId, byte[] buffer)
    {
        lock (_cacheLock)
        {
            _modStorage.WriteBlob(GetStorageKey(recordId), buffer);
            TouchCacheEntry(recordId, buffer.Length);

            foreach (int evictionId in GhostCachePolicy.GetEvictionCandidates(
                         _cacheEntries.Values,
                         _maximumCacheBytes))
            {
                _modStorage.DeleteBlob(GetStorageKey(evictionId));
                _cacheEntries.Remove(evictionId);
            }

            SaveCacheIndex();
        }
    }

    private bool TouchCacheEntry(int recordId, int size)
    {
        bool isNew = !_cacheEntries.TryGetValue(recordId, out GhostCacheEntry entry);
        if (isNew)
        {
            entry = new GhostCacheEntry { RecordId = recordId };
            _cacheEntries.Add(recordId, entry);
        }

        entry.Size = size;
        entry.LastAccess = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        return isNew;
    }

    private void SaveCacheIndex()
    {
        _modStorage.SaveToJson(
            CacheIndexKey,
            new GhostCacheIndex { Entries = _cacheEntries.Values.ToList() });
    }

    private Uri TransformGhostUrl(string input) =>
        ServiceUriValidator.ResolveCdnPath(ConfigService.CdnUrl, input);
}
