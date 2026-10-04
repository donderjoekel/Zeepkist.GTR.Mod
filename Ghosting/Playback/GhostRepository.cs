extern alias BuffersAlias;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TNRD.Zeepkist.GTR.Configuration;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Ghosting.Readers;
using TNRD.Zeepkist.GTR.Utilities;
using ZeepSDK.External.Cysharp.Threading.Tasks;
using ZeepSDK.External.FluentResults;
using ZeepSDK.Storage;

namespace TNRD.Zeepkist.GTR.Ghosting.Playback;

public class GhostRepository : IDisposable
{
    public const string ClientKey = "Ghosts";
    private const string CacheIndexKey = "ghost-cache-index";
    private const int MaxConcurrentDownloads = 15;
    private const int MaxConcurrentParses = 5;

    private readonly IModStorage _modStorage;
    private readonly GhostReaderFactory _ghostReaderFactory;
    private readonly HttpClient _httpClient;
    private readonly ILogger<GhostRepository> _logger;
    private readonly long _maximumCacheBytes;
    private readonly object _cacheLock = new();
    private readonly Dictionary<int, GhostCacheEntry> _cacheEntries = new();
    private readonly SemaphoreSlim _downloadSlots = new(MaxConcurrentDownloads, MaxConcurrentDownloads);
    private readonly SemaphoreSlim _parseSlots = new(MaxConcurrentParses, MaxConcurrentParses);
    private readonly SharedAsyncLoad<(int RecordId, string Url), GhostLoad> _loads;
    private readonly Task _cacheReady;

    public GhostRepository(
        IModStorage modStorage,
        GhostReaderFactory ghostReaderFactory,
        HttpClient httpClient,
        ILogger<GhostRepository> logger,
        long maximumCacheBytes)
    {
        _modStorage = modStorage;
        _ghostReaderFactory = ghostReaderFactory;
        _httpClient = httpClient;
        _logger = logger;
        _maximumCacheBytes = maximumCacheBytes;
        _cacheReady = Task.Run(LoadCacheIndex);
        _loads = new SharedAsyncLoad<(int RecordId, string Url), GhostLoad>(
            (key, token) => LoadSource(key.RecordId, key.Url, token));
    }

    internal sealed class GhostLoad
    {
        private readonly Lazy<Task<Result<IGhost>>> _decoded;
        internal GhostLoad(Func<Task<Result<IGhost>>> decode) =>
            _decoded = new Lazy<Task<Result<IGhost>>>(() => Task.Run(decode));

        internal async Task<Result<IGhost>> GetGhost(CancellationToken token)
        {
            Result<IGhost> result = await TaskCancellation.WaitAsync(_decoded.Value, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return result.IsSuccess
                ? Result.Ok<IGhost>(((GhostBase)result.Value).CreatePlayback())
                : result;
        }
    }

    // Keep source lease until creation is queued. Concurrent consumers share both disk/download and decode.
    internal Task<SharedAsyncLoad<(int RecordId, string Url), GhostLoad>.Lease> PreloadGhostAsync(
        int recordId, string ghostUrl, CancellationToken token) => _loads.RentAsync((recordId, ghostUrl), token);

    public async UniTask<Result<IGhost>> GetGhost(
        int recordId, string ghostUrl, CancellationToken cancellationToken = default)
    {
        try
        {
            using var source = await PreloadGhostAsync(recordId, ghostUrl, cancellationToken);
            return await source.Value.GetGhost(cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return Result.Fail("Ghost download wait was cancelled.");
        }
    }

    private async Task<GhostLoad> LoadSource(int recordId, string url, CancellationToken token)
    {
        await _downloadSlots.WaitAsync(token).ConfigureAwait(false);
        try
        {
            await _cacheReady.ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            byte[] buffer = await Task.Run(() => ReadCachedBytes(recordId), token).ConfigureAwait(false);
            bool cached = buffer != null;
            buffer ??= await DownloadBytes(url, token).ConfigureAwait(false);
            return new GhostLoad(() => DecodeSource(recordId, url, buffer, cached, token));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Unable to fetch ghost {RecordId}", recordId);
            return new GhostLoad(() => Task.FromResult((Result<IGhost>)Result.Fail(new ExceptionalError(error))));
        }
        finally { _downloadSlots.Release(); }
    }

    private async Task<byte[]> DownloadBytes(string url, CancellationToken token)
    {
        using HttpRequestMessage request = new(HttpMethod.Get, TransformGhostUrl(url));
        using HttpResponseMessage response = await _httpClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > GhostLimits.MaxCompressedBytes)
            throw new InvalidDataException($"Ghost exceeds {GhostLimits.MaxCompressedBytes} byte compressed limit.");
        return await ReadLimitedAsync(response.Content, token).ConfigureAwait(false);
    }

    private async Task<Result<IGhost>> DecodeSource(
        int recordId, string url, byte[] buffer, bool cached, CancellationToken token)
    {
        try
        {
            if (cached)
            {
                try
                {
                    IGhost ghost = await ParseGhostAsync(buffer, token).ConfigureAwait(false);
                    token.ThrowIfCancellationRequested();
                    return Result.Ok(ghost);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception error)
                {
                    _logger.LogWarning(error, "Cached ghost {RecordId} is invalid; deleting it", recordId);
                    await Task.Run(() => DeleteInvalidCache(recordId), token).ConfigureAwait(false);
                }
                await _downloadSlots.WaitAsync(token).ConfigureAwait(false);
                try { buffer = await DownloadBytes(url, token).ConfigureAwait(false); }
                finally { _downloadSlots.Release(); }
            }

            IGhost downloaded = await ParseGhostAsync(buffer, token).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            try { await Task.Run(() => WriteCachedGhost(recordId, buffer), token).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (Exception error)
            {
                _logger.LogWarning(error, "Unable to cache ghost {RecordId}; using downloaded ghost", recordId);
            }
            return Result.Ok(downloaded);
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Unable to decode ghost {RecordId}", recordId);
            return Result.Fail(new ExceptionalError(error));
        }
    }

    private byte[] ReadCachedBytes(int recordId)
    {
        try
        {
            lock (_cacheLock)
            {
                string storageKey = GetStorageKey(recordId);
                if (!_modStorage.BlobFileExists(storageKey)) return null;
                byte[] buffer = _modStorage.ReadBlob(storageKey);
                if (TouchCacheEntry(recordId, buffer.Length))
                {
                    try { SaveCacheIndex(); }
                    catch (Exception error)
                    {
                        _logger.LogWarning(error, "Unable to update ghost cache index; using cached ghost {RecordId}", recordId);
                    }
                }
                return buffer;
            }
        }
        catch (Exception error)
        {
            _logger.LogWarning(error, "Unable to read cached ghost {RecordId}", recordId);
            return null;
        }
    }

    private void DeleteInvalidCache(int recordId)
    {
        lock (_cacheLock)
        {
            try
            {
                _modStorage.DeleteBlob(GetStorageKey(recordId));
                _cacheEntries.Remove(recordId);
                SaveCacheIndex();
            }
            catch (Exception error)
            {
                _logger.LogWarning(error, "Unable to delete invalid cached ghost {RecordId}", recordId);
            }
        }
    }

    private async Task<IGhost> ParseGhostAsync(byte[] buffer, CancellationToken cancellationToken)
    {
        await _parseSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return await Task.Run(() => ReadGhost(buffer), cancellationToken).ConfigureAwait(false);
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
        using Stream input = await content.ReadAsStreamAsync().ConfigureAwait(false);
        using LimitedMemoryStream output = new(GhostLimits.MaxCompressedBytes);
        byte[] copyBuffer = BuffersAlias::System.Buffers.ArrayPool<byte>.Shared.Rent(81_920);
        try
        {
            int read;
            while ((read = await input.ReadAsync(copyBuffer, 0, copyBuffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                output.Write(copyBuffer, 0, read);
            return output.ToArray();
        }
        finally
        {
            BuffersAlias::System.Buffers.ArrayPool<byte>.Shared.Return(copyBuffer);
        }
    }

    private static string GetStorageKey(int recordId) => "ghosts/" + recordId;

    private void LoadCacheIndex()
    {
        lock (_cacheLock)
        {
            try
            {
                if (!_modStorage.JsonFileExists(CacheIndexKey))
                    return;
                GhostCacheIndex index = _modStorage.LoadFromJson<GhostCacheIndex>(CacheIndexKey);
                if (index?.Entries == null)
                    return;

                foreach (GhostCacheEntry entry in index.Entries)
                {
                    if (entry.Size > 0 && _modStorage.BlobFileExists(GetStorageKey(entry.RecordId)))
                        _cacheEntries[entry.RecordId] = entry;
                }
            }
            catch (Exception exception)
            {
                _logger.LogWarning(exception, "Ghost cache index is invalid; rebuilding it");
                _cacheEntries.Clear();
                try
                {
                    _modStorage.DeleteJsonFile(CacheIndexKey);
                }
                catch (Exception deleteException)
                {
                    _logger.LogWarning(deleteException, "Unable to delete invalid ghost cache index");
                }
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

    public void Dispose() => _loads.Dispose();

    private Uri TransformGhostUrl(string input) =>
        ServiceUriValidator.ResolveCdnPath(ConfigService.CdnUrl, input);
}
