namespace TNRD.Zeepkist.GTR.GraphQL;

public readonly struct LevelGraphqlIdentity
{
    public const string NoXxHashSentinel = "__GTR_NO_XXHASH__";
    public const string NoLegacyHashSentinel = "__GTR_NO_LEGACY_HASH__";

    private LevelGraphqlIdentity(string xxHash, string hash, string cacheKey)
    {
        XxHash = xxHash;
        Hash = hash;
        CacheKey = cacheKey;
    }

    public string XxHash { get; }
    public string Hash { get; }
    public string CacheKey { get; }
    public bool IsAvailable => CacheKey != null;

    public static LevelGraphqlIdentity Unavailable => default;

    public static LevelGraphqlIdentity FromValues(
        string xxHash,
        string hash,
        bool isAdventureLevel,
        bool useAvonturenLevel)
    {
        if (isAdventureLevel || useAvonturenLevel)
        {
            if (string.IsNullOrEmpty(hash))
                return Unavailable;

            string resolvedXxHash = string.IsNullOrEmpty(xxHash) ? NoXxHashSentinel : xxHash;
            string cacheKey = string.IsNullOrEmpty(xxHash) ? $"hash:{hash}" : $"xxHash:{xxHash}:hash:{hash}";
            return new LevelGraphqlIdentity(resolvedXxHash, hash, cacheKey);
        }

        if (!string.IsNullOrEmpty(xxHash))
        {
            return new LevelGraphqlIdentity(xxHash, NoLegacyHashSentinel, $"xxHash:{xxHash}");
        }

        return Unavailable;
    }
}
