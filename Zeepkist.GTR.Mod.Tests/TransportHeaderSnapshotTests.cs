using Steamworks;
using TNRD.Zeepkist.GTR.Connectivity;
using Xunit;

namespace Zeepkist.GTR.Mod.Tests;

public sealed class TransportHeaderSnapshotTests : IDisposable
{
    public TransportHeaderSnapshotTests()
    {
        SteamClient.IsValid = true;
        SteamClient.IsLoggedOn = true;
        PlayerManager.Instance = null;
    }

    [Fact]
    public void MissingPlayerManager_DefersCapture()
    {
        Assert.False(TransportHeaderSnapshot.TryCapture("1.7.2", out var headers));
        Assert.Null(headers);
    }

    [Fact]
    public void PlayerManagerExistsBeforeVersion_DefersCaptureUntilVersionLoads()
    {
        PlayerManager.Instance = new PlayerManager();
        Assert.False(TransportHeaderSnapshot.TryCapture("1.7.2", out var headers));
        Assert.Null(headers);
        Assert.False(TransportHeaderSnapshot.TryCapture("1.7.2", out headers));

        PlayerManager.Instance.version = new StartupGameVersion { version = 17, patch = 32 };
        Assert.True(TransportHeaderSnapshot.TryCapture("1.7.2", out headers));
        Assert.Equal("17.32", headers.Single(header => header.Name == "X-Zeepkist-Version").Value);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public void SteamNotReady_DefersCapture(bool valid, bool loggedOn)
    {
        SteamClient.IsValid = valid;
        SteamClient.IsLoggedOn = loggedOn;
        PlayerManager.Instance = new PlayerManager { version = new StartupGameVersion { version = 17, patch = 32 } };
        Assert.False(TransportHeaderSnapshot.TryCapture("1.7.2", out var headers));
        Assert.Null(headers);
    }

    [Fact]
    public void ReadySnapshot_PreservesHeadersAfterUnityStateChanges()
    {
        PlayerManager.Instance = new PlayerManager { version = new StartupGameVersion { version = 17, patch = 32 } };
        Assert.True(TransportHeaderSnapshot.TryCapture("1.7.2", out var headers));
        PlayerManager.Instance.version.version = 18;
        PlayerManager.Instance.version.patch = 1;
        PlayerManager.Instance = null;

        Assert.Equal(new[]
        {
            ("X-Zeepkist-Version", "17.32"), ("X-Zeepkist-Major-Version", "17"),
            ("X-GTR-Version", "1.7.2"), ("X-Steam-ID", "42")
        }, headers);
    }

    public void Dispose()
    {
        PlayerManager.Instance = null;
        SteamClient.IsValid = true;
        SteamClient.IsLoggedOn = true;
    }
}
