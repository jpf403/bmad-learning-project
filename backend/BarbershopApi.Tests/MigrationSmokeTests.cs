using BarbershopApi.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BarbershopApi.Tests;

public class MigrationSmokeTests : IDisposable
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"barbershop-test-{Guid.NewGuid()}.db");

    [Fact]
    public async Task App_boots_and_migrates_a_fresh_temp_database_without_throwing()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Key"] = "test-signing-key-at-least-32-characters-long",
                }));

            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<BarbershopDbContext>>();
                services.AddDbContext<BarbershopDbContext>(options =>
                    options.UseSqlite($"Data Source={_dbPath}"));
            });
        });

        using var client = factory.CreateClient();

        Assert.True(File.Exists(_dbPath));
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        foreach (var suffix in new[] { "-wal", "-shm" })
        {
            var sidecar = _dbPath + suffix;
            if (File.Exists(sidecar))
            {
                File.Delete(sidecar);
            }
        }
    }
}
