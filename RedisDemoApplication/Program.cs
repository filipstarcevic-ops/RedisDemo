using Microsoft.EntityFrameworkCore;
using RedisDemoApplication.Caching;
using RedisDemoApplication.Data;
using RedisDemoApplication.Services;

var builder = WebApplication.CreateBuilder(args);

// Connection strings come from configuration only (appsettings, user secrets or
// environment variables such as ConnectionStrings__Redis). Nothing is hardcoded.
var sqlConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrWhiteSpace(sqlConnectionString) || string.IsNullOrWhiteSpace(redisConnectionString))
{
    throw new InvalidOperationException(
        "Connection strings 'DefaultConnection' and 'Redis' must be configured " +
        "(appsettings.{Environment}.json, user secrets or environment variables).");
}

var cacheSection = builder.Configuration.GetSection(CacheSettings.SectionName);
builder.Services.Configure<CacheSettings>(cacheSection);

// SQL Server: local SQL Server / LocalDB in development, Azure SQL Database in production.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(sqlConnectionString, sql => sql.EnableRetryOnFailure()));

// Redis registered behind the standard IDistributedCache abstraction.
// Local Redis in development, Azure Managed Redis in production - only the connection string changes.
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = (cacheSection.Get<CacheSettings>() ?? new CacheSettings()).InstanceName;
});

builder.Services.AddScoped<ProductService>();

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Unhandled exceptions (e.g. Redis or SQL unreachable) are logged and returned as RFC 7807 problem details.
app.UseExceptionHandler();

// Must run before the Swagger UI middleware: otherwise the UI page is served over HTTP while
// /openapi/v1.json is redirected to HTTPS, and the browser blocks that cross-origin fetch.
app.UseHttpsRedirection();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "Redis Demo API"));
}

app.UseAuthorization();

app.MapControllers();

app.Run();
