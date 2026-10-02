using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using TNRD.Zeepkist.GTR.Api;
using TNRD.Zeepkist.GTR.Connectivity;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public class RecordingUploadTests
{
    private sealed class Transport(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler, IHttpClientFactory
    {
        private HttpClient _client;
        public HttpClient CreateClient(string name) => _client ??= new(this, false) { BaseAddress = new("https://fixture.invalid/") };
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) => send(request, token);
        protected override void Dispose(bool disposing) { _client?.Dispose(); base.Dispose(disposing); }
    }

    [Fact]
    public async Task UnauthorizedSubmission_RefreshesAuthentication_AndReusesExactBody()
    {
        var bodies = new List<string>();
        var tokens = new List<string>();
        int login = 0, refresh = 0;
        using var transport = new Transport(async (request, _) =>
        {
            string body = await request.Content.ReadAsStringAsync();
            if (request.RequestUri.AbsolutePath == "/auth/login")
            {
                login++;
                Assert.Equal("1234", JObject.Parse(body)["AuthenticationTicket"]);
                return Authentication("first");
            }
            if (request.RequestUri.AbsolutePath == "/auth/refresh")
            {
                refresh++;
                Assert.Equal("first", JObject.Parse(body)["LoginToken"]);
                return Authentication("second");
            }
            bodies.Add(body);
            tokens.Add(request.Headers.Authorization.ToString());
            return new(bodies.Count == 1 ? HttpStatusCode.Unauthorized : HttpStatusCode.OK);
        });
        var client = new ApiHttpClient(transport, NullLogger<ApiHttpClient>.Instance, new SpainRoutingService());
        string json = JsonConvert.SerializeObject(new RecordPostResource
        {
            Level = "legacy",
            Hash = "canonical",
            Time = 1.234f,
            Splits = [0.5f],
            Speeds = [42],
            GhostData = "AQID",
            WorkshopId = "123",
            GameVersion = "17.32",
            ModVersion = "test"
        });
        using var response = await client.PostJsonAsync("record/submit", json);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(1, login);
        Assert.Equal(1, refresh);
        Assert.Equal(new[] { json, json }, bodies);
        Assert.Equal(new[] { "Bearer first", "Bearer second" }, tokens);
    }

    [Theory]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Submission_DoesNotAddTransientPostRetries(int status)
    {
        int submissions = 0;
        using var transport = new Transport((request, _) =>
        {
            if (request.RequestUri.AbsolutePath == "/auth/login") return Task.FromResult(Authentication("token"));
            submissions++;
            return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status));
        });
        var client = new ApiHttpClient(transport, NullLogger<ApiHttpClient>.Instance, new SpainRoutingService());
        using var response = await client.PostJsonAsync("record/submit", "{}");
        Assert.Equal(status, (int)response.StatusCode);
        Assert.Equal(1, submissions);
    }

    [Fact]
    public async Task Shutdown_CancelsInFlightUpload()
    {
        var uploading = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var cancellation = new CancellationTokenSource();
        using var transport = new Transport(async (request, token) =>
        {
            if (request.RequestUri.AbsolutePath == "/auth/login") return Authentication("token");
            uploading.SetResult();
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException();
        });
        var client = new ApiHttpClient(transport, NullLogger<ApiHttpClient>.Instance, new SpainRoutingService());
        Task<HttpResponseMessage> upload = client.PostJsonAsync("record/submit", "{}", cancellation.Token).AsTask();
        await uploading.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => upload);
    }

    private static HttpResponseMessage Authentication(string token) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonConvert.SerializeObject(new AuthenticationResource
        {
            AccessToken = token,
            RefreshToken = "refresh",
            AccessTokenExpiry = DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds().ToString(),
            RefreshTokenExpiry = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString()
        }))
    };
}
