using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using TNRD.Zeepkist.GTR.Ghosting.Ghosts;
using TNRD.Zeepkist.GTR.Ghosting.Playback;
using TNRD.Zeepkist.GTR.Ghosting.Readers;
using ZeepSDK.Storage;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class GhostRepositoryTests
{
    private sealed class Playback(byte[] frames) : GhostBase
    {
        public byte[] Frames { get; } = frames;
        internal override GhostBase CreatePlayback() => new Playback(Frames);
    }

    private sealed class Storage : IModStorage
    {
        public byte[] Blob;
        public bool WriteFails;
        public bool IndexFails;
        public int Reads;
        public bool BlobFileExists(string key) => Blob != null;
        public byte[] ReadBlob(string key) { Interlocked.Increment(ref Reads); return Blob; }
        public void WriteBlob(string key, byte[] bytes)
        {
            if (WriteFails) throw new IOException("cache unavailable");
            Blob = bytes;
        }
        public void DeleteBlob(string key) => Blob = null;
        public bool JsonFileExists(string key) => false;
        public T LoadFromJson<T>(string key) => default;
        public void SaveToJson<T>(string key, T value)
        {
            if (IndexFails) throw new IOException("index unavailable");
        }
        public void DeleteJsonFile(string key) { }
    }

    private sealed class Transport(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(token);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CacheWriteFailure_DoesNotDiscardDownloadedGhost(bool indexFailure)
    {
        var storage = new Storage { WriteFails = !indexFailure, IndexFails = indexFailure };
        using var client = new HttpClient(new Transport(_ => Task.FromResult(Response())));
        using var repository = Create(storage, client);
        var result = await repository.GetGhost(1, "ghost");
        Assert.True(result.IsSuccess);
        Assert.Equal(new byte[] { 1, 2, 3 }, ((Playback)result.Value).Frames);
    }

    [Fact]
    public async Task CacheIndexWriteFailure_DoesNotDiscardCachedGhost()
    {
        var storage = new Storage { Blob = [1, 2, 3], IndexFails = true };
        using var client = new HttpClient(new Transport(_ => throw new InvalidOperationException("Unexpected download")));
        using var repository = Create(storage, client);
        var result = await repository.GetGhost(1, "ghost");
        Assert.True(result.IsSuccess);
        Assert.Equal(1, storage.Reads);
    }

    [Fact]
    public async Task ConcurrentLoads_DeduplicateBeforeDiskReadAndDecode_AndReturnSeparatePlayback()
    {
        var storage = new Storage { Blob = [1, 2, 3] };
        int decodes = 0;
        using var entered = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var client = new HttpClient(new Transport(_ => throw new InvalidOperationException("Unexpected download")));
        var decoder = new GhostReaderFactory(bytes =>
        {
            Interlocked.Increment(ref decodes);
            entered.Set();
            if (!release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException();
            return new Playback(bytes);
        });
        using var repository = new GhostRepository(storage, decoder, client, NullLogger<GhostRepository>.Instance, 1_000);
        var first = repository.GetGhost(1, "ghost").AsTask();
        Assert.True(entered.Wait(TimeSpan.FromSeconds(5)));
        var second = repository.GetGhost(1, "ghost").AsTask();
        release.Set();
        var results = await Task.WhenAll(first, second);
        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(1, decodes);
        Assert.Equal(1, storage.Reads);
        Assert.NotSame(results[0].Value, results[1].Value);
        Assert.Same(((Playback)results[0].Value).Frames, ((Playback)results[1].Value).Frames);
    }

    private static GhostRepository Create(Storage storage, HttpClient client) =>
        new(storage, new GhostReaderFactory(bytes => new Playback(bytes)), client,
            NullLogger<GhostRepository>.Instance, 1_000);

    private static HttpResponseMessage Response() => new(HttpStatusCode.OK) { Content = new ByteArrayContent([1, 2, 3]) };

    [Fact]
    public async Task SourceStage_AllowsFifteenDownloads_AndDefersDecodeUntilCapacityIsReserved()
    {
        int active = 0, maximum = 0, decodes = 0;
        var full = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var client = new HttpClient(new Transport(async token =>
        {
            int count = Interlocked.Increment(ref active);
            int observed;
            do { observed = Volatile.Read(ref maximum); }
            while (count > observed && Interlocked.CompareExchange(ref maximum, count, observed) != observed);
            if (count == 15) full.TrySetResult();
            try { await release.Task.WaitAsync(token); return Response(); }
            finally { Interlocked.Decrement(ref active); }
        }));
        using var repository = new GhostRepository(new Storage(), new GhostReaderFactory(bytes =>
        {
            Interlocked.Increment(ref decodes);
            return new Playback(bytes);
        }), client, NullLogger<GhostRepository>.Instance, 1_000);
        var fetching = Enumerable.Range(0, 20).Select(id => repository.PreloadGhostAsync(id, "ghost", default)).ToArray();
        await full.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(15, maximum);
        Assert.Equal(0, decodes);
        release.SetResult();
        var sources = await Task.WhenAll(fetching);
        try
        {
            Assert.Equal(15, maximum);
            Assert.Equal(0, decodes);
            Assert.True((await sources[0].Value.GetGhost(default)).IsSuccess);
            Assert.Equal(1, decodes);
        }
        finally { foreach (var source in sources) source.Dispose(); }
    }
}
