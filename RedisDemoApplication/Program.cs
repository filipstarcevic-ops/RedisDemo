using Microsoft.EntityFrameworkCore;
using RedisDemoApplication.Caching;
using RedisDemoApplication.Data;
using RedisDemoApplication.Services;

var builder = WebApplication.CreateBuilder(args);

// Connection strings point to Azure SQL Database and Azure Managed Redis and come from
// configuration only: user secrets when running on a developer machine, App Service
// environment variables / Key Vault references in Azure. Nothing is hardcoded.
var sqlConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
var redisConnectionString = builder.Configuration.GetConnectionString("Redis");
if (string.IsNullOrWhiteSpace(sqlConnectionString) || string.IsNullOrWhiteSpace(redisConnectionString))
{
    throw new InvalidOperationException(
        "Connection strings 'DefaultConnection' and 'Redis' must be configured " +
        "(user secrets or environment variables) with the Azure SQL and Azure Redis values.");
}

var cacheSection = builder.Configuration.GetSection(CacheSettings.SectionName);
builder.Services.Configure<CacheSettings>(cacheSection);

// Azure SQL Database (retry on transient errors).
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(sqlConnectionString, sql => sql.EnableRetryOnFailure()));

// Redis registered behind the standard IDistributedCache abstraction.
// Azure Managed Redis (TLS, port 10000).
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
