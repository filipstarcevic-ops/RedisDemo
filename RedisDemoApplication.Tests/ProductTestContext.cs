using System.Data.Common;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RedisDemoApplication.Caching;
using RedisDemoApplication.Data;
using RedisDemoApplication.Services;

namespace RedisDemoApplication.Tests;

/// <summary>
/// Real <see cref="ProductService"/> wired to an in-memory SQLite database (stand-in for SQL Server)
/// and an in-memory <see cref="IDistributedCache"/> (stand-in for Redis). Every SQL command that
/// reaches the database is counted so tests can prove whether the database was queried.
/// </summary>
public sealed class ProductTestContext : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly QueryCounter _queryCounter = new();

    public ProductTestContext()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        Db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(_queryCounter)
            .Options);
        Db.Database.EnsureCreated(); // creates schema + the 3 seeded products

        Cache = new MemoryDistributedCache(Options.Create(new MemoryDistributedCacheOptions()));
        Service = new ProductService(
            Db,
            Cache,
            Options.Create(new CacheSettings { ProductCacheExpirationMinutes = 5 }),
            NullLogger<ProductService>.Instance);

        _queryCounter.Reset();
    }

    public AppDbContext Db { get; }

    public IDistributedCache Cache { get; }

    public ProductService Service { get; }

    /// <summary>Number of SQL commands executed since construction or the last <see cref="ResetQueryCount"/>.</summary>
    public int DatabaseQueryCount => _queryCounter.Count;

    public void ResetQueryCount() => _queryCounter.Reset();

    public async Task<T?> GetCachedAsync<T>(string key)
    {
        var json = await Cache.GetStringAsync(key);
        return json is null ? default : JsonSerializer.Deserialize<T>(json);
    }

    public Task SetCachedAsync<T>(string key, T value) =>
        Cache.SetStringAsync(key, JsonSerializer.Serialize(value));

    public void Dispose()
    {
        Db.Dispose();
        _connection.Dispose();
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        private int _count;

        public int Count => _count;

        public void Reset() => _count = 0;

        public override InterceptionResult<DbDataReader> ReaderExecuting(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
        {
            Interlocked.Increment(ref _count);
            return result;
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref _count);
            return ValueTask.FromResult(result);
        }
    }
}
