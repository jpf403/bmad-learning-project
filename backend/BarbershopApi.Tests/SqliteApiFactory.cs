using BarbershopApi.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace BarbershopApi.Tests;

public class SqliteApiFactory : WebApplicationFactory<Program>
{
    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"barbershop-test-{Guid.NewGuid()}.db");

    private string ConnectionString => $"Data Source={_dbPath}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<BarbershopDbContext>>();
            services.AddDbContext<BarbershopDbContext>(options => options.UseSqlite(ConnectionString));
        });
    }

    public BarbershopDbContext CreateDbContext()
    {
        _ = Server; // forces host startup so Program.cs's Database.Migrate() runs against _dbPath first
        return new BarbershopDbContext(new DbContextOptionsBuilder<BarbershopDbContext>().UseSqlite(ConnectionString).Options);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        SqliteConnection.ClearAllPools();

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
