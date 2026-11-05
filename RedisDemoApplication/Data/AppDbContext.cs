using Microsoft.EntityFrameworkCore;
using RedisDemoApplication.Entities;

namespace RedisDemoApplication.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Product> Products => Set<Product>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Product>(product =>
        {
            product.Property(p => p.Name).HasMaxLength(200).IsRequired();
            product.Property(p => p.Description).HasMaxLength(2000).IsRequired();
            product.Property(p => p.Price).HasPrecision(18, 2);

            // A few rows so the cache can be demonstrated right after "dotnet ef database update".
            product.HasData(
                new Product { Id = 1, Name = "Mechanical Keyboard", Description = "Tenkeyless keyboard with brown switches.", Price = 89.99m, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Product { Id = 2, Name = "Wireless Mouse", Description = "Ergonomic mouse with USB-C charging.", Price = 39.50m, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) },
                new Product { Id = 3, Name = "27\" Monitor", Description = "1440p IPS monitor, 144 Hz.", Price = 299.00m, CreatedAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc) });
        });
    }
}
