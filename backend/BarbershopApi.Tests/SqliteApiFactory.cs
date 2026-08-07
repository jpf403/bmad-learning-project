using BarbershopApi.Data;
using BarbershopApi.Tests.TestOnly;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BarbershopApi.Tests;

public class SqliteApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"barbershop-test-{Guid.NewGuid()}.db");

    private string ConnectionString => $"Data Source={_dbPath}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "test-signing-key-at-least-32-characters-long",
            }));

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BarbershopDbContext>>();
            services.AddDbContext<BarbershopDbContext>(options => options.UseSqlite(ConnectionString));
            services.AddControllers().AddApplicationPart(typeof(RoleGateTestController).Assembly);
        });
    }

    public new HttpClient CreateClient()
    {
        // The login endpoint sets the refresh cookie with Secure=true; HttpClient's
        // CookieContainer withholds Secure cookies from non-https requests, so the
        // default http://localhost base address would silently drop it on refresh.
        return CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
    }

    public BarbershopDbContext CreateDbContext()
    {
        _ = Server; // forces host startup so Program.cs's Database.Migrate() runs against _dbPath first
        return new BarbershopDbContext(new DbContextOptionsBuilder<BarbershopDbContext>().UseSqlite(ConnectionString).Options);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        // Scoped to this factory's own connection string -- ClearAllPools() clears every
        // SQLite connection pool process-wide, which corrupts other tests' still-in-flight
        // connections when test classes run in parallel (surfaces as a random
        // ObjectDisposedException on SQLitePCL.sqlite3 in an unrelated test).
        using var connection = new SqliteConnection(ConnectionString);
        SqliteConnection.ClearPool(connection);

        TryDelete(_dbPath);
        foreach (var suffix in new[] { "-wal", "-shm", "-journal" })
        {
            TryDelete(_dbPath + suffix);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // best-effort cleanup of the temp DB file; a lingering handle on Windows
            // shouldn't fail the test itself (ClearAllPools already ran above).
        }
    }
}
