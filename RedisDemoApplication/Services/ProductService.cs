using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using RedisDemoApplication.Caching;
using RedisDemoApplication.Contracts;
using RedisDemoApplication.Data;
using RedisDemoApplication.Entities;

namespace RedisDemoApplication.Services;

/// <summary>
/// Product catalog operations. Reads use the Cache-Aside pattern on top of
/// <see cref="IDistributedCache"/> (Redis in production); writes go to SQL Server
/// and then invalidate the affected cache keys.
/// </summary>
public class ProductService(
    AppDbContext db,
    IDistributedCache cache,
    IOptions<CacheSettings> cacheSettings,
    ILogger<ProductService> logger)
{
    private readonly DistributedCacheEntryOptions _cacheEntryOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(cacheSettings.Value.ProductCacheExpirationMinutes)
    };

    public async Task<List<Product>> GetAllAsync(CancellationToken cancellationToken) =>
        await GetOrLoadAsync(CacheKeys.AllProducts, async ct =>
        {
            logger.LogInformation("DATABASE QUERY - Loading all products from SQL Server");
            return await db.Products.AsNoTracking().OrderBy(p => p.Id).ToListAsync(ct);
        }, cancellationToken) ?? [];

    public Task<Product?> GetByIdAsync(int id, CancellationToken cancellationToken) =>
        GetOrLoadAsync(CacheKeys.Product(id), async ct =>
        {
            logger.LogInformation("DATABASE QUERY - Loading product {ProductId} from SQL Server", id);
            return await db.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == id, ct);
        }, cancellationToken);

    public async Task<Product> CreateAsync(ProductRequest request, CancellationToken cancellationToken)
    {
        var product = new Product
        {
            Name = request.Name,
            Description = request.Description,
            Price = request.Price,
            CreatedAt = DateTime.UtcNow
        };

        db.Products.Add(product);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("DATABASE WRITE - Created product {ProductId}", product.Id);

        // The new product is not in any cached single-product entry yet, but the cached list is now stale.
        await InvalidateAsync(cancellationToken, CacheKeys.AllProducts);
        return product;
    }

    /// <returns>The updated product, or <c>null</c> when it does not exist.</returns>
    public async Task<Product?> UpdateAsync(int id, ProductRequest request, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([id], cancellationToken);
        if (product is null)
        {
            return null;
        }

        product.Name = request.Name;
        product.Description = request.Description;
        product.Price = request.Price;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("DATABASE WRITE - Updated product {ProductId}", id);

        await InvalidateAsync(cancellationToken, CacheKeys.Product(id), CacheKeys.AllProducts);
        return product;
    }

    /// <returns><c>true</c> when the product was deleted, <c>false</c> when it does not exist.</returns>
    public async Task<bool> DeleteAsync(int id, CancellationToken cancellationToken)
    {
        var product = await db.Products.FindAsync([id], cancellationToken);
        if (product is null)
        {
            return false;
        }

        db.Products.Remove(product);
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation("DATABASE WRITE - Deleted product {ProductId}", id);

        await InvalidateAsync(cancellationToken, CacheKeys.Product(id), CacheKeys.AllProducts);
        return true;
    }

    /// <summary>
    /// Cache-Aside read: try the cache first; on a miss load from the database,
    /// store the result with a TTL and return it. Missing values (null) are not cached.
    /// </summary>
    private async Task<T?> GetOrLoadAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> loadFromDatabase,
        CancellationToken cancellationToken) where T : class
    {
        var cachedJson = await cache.GetStringAsync(key, cancellationToken);
        if (cachedJson is not null)
        {
            logger.LogInformation("CACHE HIT - {CacheKey} served from Redis, database not queried", key);
            return JsonSerializer.Deserialize<T>(cachedJson);
        }

        logger.LogInformation("CACHE MISS - {CacheKey} not found in Redis", key);

        var value = await loadFromDatabase(cancellationToken);
        if (value is null)
        {
            return null;
        }

        await cache.SetStringAsync(key, JsonSerializer.Serialize(value), _cacheEntryOptions, cancellationToken);
        logger.LogInformation(
            "CACHE SET - {CacheKey} stored in Redis with TTL {TtlMinutes} min",
            key, _cacheEntryOptions.AbsoluteExpirationRelativeToNow!.Value.TotalMinutes);

        return value;
    }

    private async Task InvalidateAsync(CancellationToken cancellationToken, params string[] keys)
    {
        foreach (var key in keys)
        {
            await cache.RemoveAsync(key, cancellationToken);
            logger.LogInformation("CACHE INVALIDATION - {CacheKey} removed from Redis", key);
        }
    }
}
