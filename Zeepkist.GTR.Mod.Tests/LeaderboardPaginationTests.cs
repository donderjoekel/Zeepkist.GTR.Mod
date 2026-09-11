using System;
using TNRD.Zeepkist.GTR.Leaderboard;
using Xunit;

namespace TNRD.Zeepkist.GTR.Tests;

public class LeaderboardPaginationTests
{
    [Theory]
    [InlineData(0, 16, 0)]
    [InlineData(1, 16, 0)]
    [InlineData(16, 16, 0)]
    [InlineData(17, 16, 1)]
    [InlineData(32, 16, 1)]
    [InlineData(14, 14, 0)]
    [InlineData(28, 14, 1)]
    public void CalculatesZeroBasedMaximumPage(int totalRecords, int pageSize, int expected)
    {
        Assert.Equal(expected, LeaderboardPagination.GetMaxPageIndex(totalRecords, pageSize));
    }

    [Fact]
    public void RejectsInvalidPageSize()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => LeaderboardPagination.GetMaxPageIndex(1, 0));
    }
}
