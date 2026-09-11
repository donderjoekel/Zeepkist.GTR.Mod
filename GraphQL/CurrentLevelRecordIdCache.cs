namespace TNRD.Zeepkist.GTR.GraphQL;

public readonly struct CurrentLevelRecordIds
{
    public CurrentLevelRecordIds(int levelId, int userId)
    {
        LevelId = levelId;
        UserId = userId;
    }

    public int LevelId { get; }
    public int UserId { get; }
}

public sealed class CurrentLevelRecordIdCache
{
    public const int MissingUserId = -1;

    private string _levelKey;
    private string _steamId;
    private CurrentLevelRecordIds _ids;

    public bool TryGet(string levelKey, string steamId, out CurrentLevelRecordIds ids)
    {
        if (_levelKey == levelKey && _steamId == steamId)
        {
            ids = _ids;
            return true;
        }

        ids = default;
        return false;
    }

    public void Set(string levelKey, string steamId, CurrentLevelRecordIds ids)
    {
        _levelKey = levelKey;
        _steamId = steamId;
        _ids = ids;
    }
}
