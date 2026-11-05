namespace RedisDemoApplication.Caching;

/// <summary>Bound from the "CacheSettings" configuration section.</summary>
public class CacheSettings
{
    public const string SectionName = "CacheSettings";

    /// <summary>Prefix added to every Redis key so several apps can share one Redis instance.</summary>
    public string InstanceName { get; set; } = "RedisDemo:";

    /// <summary>Absolute time-to-live (TTL) for cached product entries.</summary>
    public int ProductCacheExpirationMinutes { get; set; } = 5;
}
