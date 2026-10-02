using System.Net.WebSockets;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using StrawberryShake;
using StrawberryShake.Transport.WebSockets;
using TNRD.Zeepkist.GTR.GraphQL;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class WebSocketRecoveryIntegrationTests
{
    private sealed class Document : IDocument
    {
        private static readonly byte[] Query = Encoding.UTF8.GetBytes("subscription Records { recordTime }");
        public OperationKind Kind => OperationKind.Subscription;
        public ReadOnlySpan<byte> Body => Query;
        public DocumentHash Hash => new("sha256", "test-record-stream");
    }

    private sealed class TransportWatch : IDisposable
    {
        private readonly CancellationTokenSource _stop = new();
        public TransportWatch(WebSocketConnection connection, IObserver<int> observer)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await foreach (var response in connection.ExecuteAsync(new OperationRequest("Records", new Document()))
                                       .WithCancellation(_stop.Token))
                    {
                        if (response.Exception != null) throw response.Exception;
                        observer.OnNext(response.Body.RootElement.GetProperty("data").GetProperty("recordTime").GetInt32());
                    }
                    observer.OnCompleted();
                }
                catch (OperationCanceledException) when (_stop.IsCancellationRequested) { }
                catch (Exception error) { observer.OnError(error); }
            });
        }
        public void Dispose() => _stop.Cancel();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StrawberryShake_RemoteCloseRestoresFreshSnapshotWithoutPlayerAction(bool abrupt)
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var server = builder.Build();
        server.UseWebSockets();
        var events = new ConcurrentQueue<string>();
        int connections = 0;
        int headers = 0;
        server.Run(async context =>
        {
            if (context.Request.Headers["X-Test-GTR"] == "snapshot-header") Interlocked.Increment(ref headers);
            using WebSocket socket = await context.WebSockets.AcceptWebSocketAsync("graphql-ws");
            int connection = Interlocked.Increment(ref connections);

            using var init = await Receive(socket, context.RequestAborted);

            await Send(socket, new { type = "connection_ack" }, context.RequestAborted);
            using JsonDocument subscribe = await Receive(socket, context.RequestAborted);

            string id = subscribe.RootElement.GetProperty("id").GetString();
            await Send(socket, new { id, type = "data", payload = new { data = new { recordTime = connection } } }, context.RequestAborted);
            if (connection == 1)
            {
                await Task.Delay(50, context.RequestAborted); // Shortened connection lifetime.
                if (abrupt) socket.Abort();
                else await socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "connection lifetime expired", context.RequestAborted);
            }
            else
            {
                try
                {
                    while (socket.State == WebSocketState.Open)
                        using (await Receive(socket, context.RequestAborted)) { }
                }
                catch (Exception error) when (error is WebSocketException or OperationCanceledException or JsonException) { }
            }
        });
        await server.StartAsync();

        var services = new ServiceCollection();
        services.AddWebSocketClient("records", client =>
        {
            client.Uri = new Uri(server.Urls.Single().Replace("http://", "ws://"));
            ((ClientWebSocket)client.Socket).Options.SetRequestHeader("X-Test-GTR", "snapshot-header");
        });
        services.AddWebSocketClientPool();
        await using var provider = services.BuildServiceProvider();
        ISessionPool sessions = provider.GetRequiredService<ISessionPool>();
        var connection = new WebSocketConnection(token => new ValueTask<ISession>(sessions.CreateAsync("records", token)));
        var snapshots = Channel.CreateUnbounded<int>();
        using var recovery = new RecoveringSubscription<int>(observer => new TransportWatch(connection, observer),
            (value, _) => snapshots.Writer.TryWrite(value), error =>
            {
                if (events.Count < 4) events.Enqueue(error.Message);
                if (Volatile.Read(ref connections) == 0) snapshots.Writer.TryComplete(error);
            },
            (_, token) => Task.Delay(20, token));
        recovery.Start();
        try { Assert.Equal(1, await snapshots.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(5))); }
        catch (Exception error) { throw new Exception(string.Join("\n", events), error); }
        Assert.Equal(2, await snapshots.Reader.ReadAsync().AsTask().WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.Equal(2, Volatile.Read(ref connections));
        Assert.Equal(2, Volatile.Read(ref headers));
        recovery.Dispose();
        await recovery.Completion.WaitAsync(TimeSpan.FromSeconds(5));
        await server.StopAsync();
    }

    private static async Task<JsonDocument> Receive(WebSocket socket, CancellationToken token)
    {
        byte[] buffer = new byte[4096];
        using var payload = new MemoryStream();
        WebSocketReceiveResult frame;
        do
        {
            frame = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
            if (frame.MessageType == WebSocketMessageType.Close) throw new WebSocketException("remote close");
            payload.Write(buffer, 0, frame.Count);
        } while (!frame.EndOfMessage);
        return JsonDocument.Parse(payload.ToArray());
    }

    private static Task Send(WebSocket socket, object payload, CancellationToken token) =>
        socket.SendAsync(new ArraySegment<byte>(JsonSerializer.SerializeToUtf8Bytes(payload)), WebSocketMessageType.Text, true, token);
}
