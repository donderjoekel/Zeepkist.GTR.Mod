using TNRD.Zeepkist.GTR.GraphQL;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class LevelGraphqlIdentityTests
{
    [Fact]
    public void NormalLevelUsesXxHashAndSentinelLegacyHash()
    {
        LevelGraphqlIdentity identity = LevelGraphqlIdentity.FromValues("XXHASH", "LEGACY", false, false);

        Assert.True(identity.IsAvailable);
        Assert.Equal("XXHASH", identity.XxHash);
        Assert.Equal(LevelGraphqlIdentity.NoLegacyHashSentinel, identity.Hash);
        Assert.Equal("xxHash:XXHASH", identity.CacheKey);
    }

    [Fact]
    public void AdventureLevelWithXxHashKeepsLegacyFallbackHash()
    {
        LevelGraphqlIdentity identity = LevelGraphqlIdentity.FromValues("XXHASH", "ea1", true, false);

        Assert.True(identity.IsAvailable);
        Assert.Equal("XXHASH", identity.XxHash);
        Assert.Equal("ea1", identity.Hash);
        Assert.Equal("xxHash:XXHASH:hash:ea1", identity.CacheKey);
    }

    [Fact]
    public void AdventureLevelWithoutXxHashUsesLegacyHashAndSentinelXxHash()
    {
        LevelGraphqlIdentity identity = LevelGraphqlIdentity.FromValues(null, "ea1", true, false);

        Assert.True(identity.IsAvailable);
        Assert.Equal(LevelGraphqlIdentity.NoXxHashSentinel, identity.XxHash);
        Assert.Equal("ea1", identity.Hash);
        Assert.Equal("hash:ea1", identity.CacheKey);
    }

    [Fact]
    public void AvonturenLevelWithoutXxHashUsesLegacyHashAndSentinelXxHash()
    {
        LevelGraphqlIdentity identity = LevelGraphqlIdentity.FromValues(null, "ea1", false, true);

        Assert.True(identity.IsAvailable);
        Assert.Equal(LevelGraphqlIdentity.NoXxHashSentinel, identity.XxHash);
        Assert.Equal("ea1", identity.Hash);
        Assert.Equal("hash:ea1", identity.CacheKey);
    }

    [Fact]
    public void NormalLevelWithoutXxHashIsUnavailable()
    {
        LevelGraphqlIdentity identity = LevelGraphqlIdentity.FromValues(null, "LEGACY", false, false);

        Assert.False(identity.IsAvailable);
        Assert.Null(identity.CacheKey);
    }

    [Fact]
    public void CacheKeyIncludesHashSource()
    {
        LevelGraphqlIdentity normal = LevelGraphqlIdentity.FromValues("SAME", "LEGACY", false, false);
        LevelGraphqlIdentity adventure = LevelGraphqlIdentity.FromValues("XXHASH", "SAME", true, false);

        Assert.NotEqual(normal.CacheKey, adventure.CacheKey);
    }
}
