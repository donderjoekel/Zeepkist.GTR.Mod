using System;

namespace TNRD.Zeepkist.GTR.Leaderboard;

public static class LeaderboardPagination
{
    public static int GetMaxPageIndex(int totalRecords, int pageSize)
    {
        if (pageSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(pageSize));

        return totalRecords <= 0 ? 0 : (totalRecords - 1) / pageSize;
    }
}
