using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using RedisDemoApplication.Caching;
using RedisDemoApplication.Contracts;
using RedisDemoApplication.Entities;

namespace RedisDemoApplication.Tests;

public sealed class ProductServiceCachingTests : IDisposable
{
    // Product 1 is part of the seed data in AppDbContext.
    private const int SeededProductId = 1;

    private readonly ProductTestContext _ctx = new();
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public void Dispose() => _ctx.Dispose();

    [Fact]
    public async Task GetById_WhenCacheIsEmpty_ReadsFromDatabase()
    {
        var product = await _ctx.Service.GetByIdAsync(SeededProductId, _ct);

        Assert.NotNull(product);
        Assert.Equal("Mechanical Keyboard", product.Name);
        Assert.Equal(1, _ctx.DatabaseQueryCount);
    }

    [Fact]
    public async Task GetById_AfterDatabaseLookup_StoresProductInCache()
    {
        await _ctx.Service.GetByIdAsync(SeededProductId, _ct);

        var cached = await _ctx.GetCachedAsync<Product>(CacheKeys.Product(SeededProductId));
        Assert.NotNull(cached);
        Assert.Equal(SeededProductId, cached.Id);
        Assert.Equal("Mechanical Keyboard", cached.Name);
        Assert.Equal(89.99m, cached.Price);
    }

    [Fact]
    public async Task GetById_WhenCached_ReturnsCachedProductWithoutQueryingDatabase()
    {
        // The cached copy deliberately differs from the database row, proving where the data came from.
        var cachedProduct = new Product { Id = SeededProductId, Name = "From cache", Description = "", Price = 1m };
        await _ctx.SetCachedAsync(CacheKeys.Product(SeededProductId), cachedProduct);

        var product = await _ctx.Service.GetByIdAsync(SeededProductId, _ct);

        Assert.NotNull(product);
        Assert.Equal("From cache", product.Name);
        Assert.Equal(0, _ctx.DatabaseQueryCount);
    }

    [Fact]
    public async Task GetById_SecondIdenticalCall_DoesNotQueryDatabaseAgain()
    {
        await _ctx.Service.GetByIdAsync(SeededProductId, _ct);   // miss -> database
        _ctx.ResetQueryCount();

        var product = await _ctx.Service.GetByIdAsync(SeededProductId, _ct); // hit -> cache

        Assert.NotNull(product);
        Assert.Equal(0, _ctx.DatabaseQueryCount);
    }

    [Fact]
    public async Task GetById_WhenProductDoesNotExist_ReturnsNullAndCachesNothing()
    {
        var product = await _ctx.Service.GetByIdAsync(9999, _ct);

        Assert.Null(product);
        Assert.Null(await _ctx.Cache.GetStringAsync(CacheKeys.Product(9999), _ct));
    }

    [Fact]
    public async Task GetAll_CachesListAndServesSecondCallFromCache()
    {
        var first = await _ctx.Service.GetAllAsync(_ct);
        Assert.Equal(3, first.Count);
        Assert.Equal(1, _ctx.DatabaseQueryCount);

        var cached = await _ctx.GetCachedAsync<List<Product>>(CacheKeys.AllProducts);
        Assert.NotNull(cached);
        Assert.Equal(first.Select(p => p.Id), cached.Select(p => p.Id));

        _ctx.ResetQueryCount();
        var second = await _ctx.Service.GetAllAsync(_ct);

        Assert.Equal(3, second.Count);
        Assert.Equal(0, _ctx.DatabaseQueryCount);
    }

    [Fact]
    public async Task Create_InvalidatesProductListCache()
    {
        await _ctx.Service.GetAllAsync(_ct); // populate products:all

        var created = await _ctx.Service.CreateAsync(
            new ProductRequest { Name = "USB-C Hub", Description = "7-in-1", Price = 49.90m }, _ct);

        Assert.Null(await _ctx.Cache.GetStringAsync(CacheKeys.AllProducts, _ct));
        Assert.True(created.Id > 0);
        Assert.NotEqual(default, created.CreatedAt);

        // The next list read goes to the database and includes the new product (no stale data).
        var all = await _ctx.Service.GetAllAsync(_ct);
        Assert.Contains(all, p => p.Id == created.Id);
    }

    [Fact]
    public async Task Update_InvalidatesProductAndListCaches()
    {
        await _ctx.Service.GetByIdAsync(SeededProductId, _ct); // populate product:1
        await _ctx.Service.GetAllAsync(_ct);                   // populate products:all

        var updated = await _ctx.Service.UpdateAsync(
            SeededProductId, new ProductRequest { Name = "Renamed", Description = "d", Price = 10m }, _ct);

        Assert.NotNull(updated);
        Assert.Null(await _ctx.Cache.GetStringAsync(CacheKeys.Product(SeededProductId), _ct));
        Assert.Null(await _ctx.Cache.GetStringAsync(CacheKeys.AllProducts, _ct));

        var reread = await _ctx.Service.GetByIdAsync(SeededProductId, _ct);
        Assert.Equal("Renamed", reread!.Name);
    }

    [Fact]
    public async Task Delete_InvalidatesProductAndListCaches()
    {
        await _ctx.Service.GetByIdAsync(SeededProductId, _ct);
        await _ctx.Service.GetAllAsync(_ct);

        var deleted = await _ctx.Service.DeleteAsync(SeededProductId, _ct);

        Assert.True(deleted);
        Assert.Null(await _ctx.Cache.GetStringAsync(CacheKeys.Product(SeededProductId), _ct));
        Assert.Null(await _ctx.Cache.GetStringAsync(CacheKeys.AllProducts, _ct));
        Assert.Null(await _ctx.Service.GetByIdAsync(SeededProductId, _ct));
        Assert.False(await _ctx.Db.Products.AnyAsync(p => p.Id == SeededProductId, _ct));
    }

    [Fact]
    public async Task Update_WhenProductDoesNotExist_ReturnsNull()
    {
        var result = await _ctx.Service.UpdateAsync(
            9999, new ProductRequest { Name = "x", Description = "", Price = 1m }, _ct);

        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_WhenProductDoesNotExist_ReturnsFalse()
    {
        Assert.False(await _ctx.Service.DeleteAsync(9999, _ct));
    }
}
