using TNRD.Zeepkist.GTR.GraphQL;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class CurrentLevelRecordIdCacheTests
{
    [Fact]
    public void ReusesIdsForSameLevelAndPlayer()
    {
        CurrentLevelRecordIdCache cache = new();
        cache.Set("level", "steam", new CurrentLevelRecordIds(12, 34));

        Assert.True(cache.TryGet("level", "steam", out CurrentLevelRecordIds ids));
        Assert.Equal(12, ids.LevelId);
        Assert.Equal(34, ids.UserId);
    }

    [Theory]
    [InlineData("other-level", "steam")]
    [InlineData("level", "other-steam")]
    public void DoesNotReuseIdsAcrossIdentityChanges(string levelKey, string steamId)
    {
        CurrentLevelRecordIdCache cache = new();
        cache.Set("level", "steam", new CurrentLevelRecordIds(12, 34));

        Assert.False(cache.TryGet(levelKey, steamId, out _));
    }

    [Fact]
    public void MissingUserSentinelCannotMatchDatabaseIdentity()
    {
        Assert.True(CurrentLevelRecordIdCache.MissingUserId < 0);
    }
}
