namespace RedisDemoApplication.Caching;

/// <summary>Single place that defines how product cache keys are named.</summary>
public static class CacheKeys
{
    public const string AllProducts = "products:all";

    public static string Product(int id) => $"product:{id}";
}
