using BarbershopApi.Data;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace BarbershopApi.Tests;

public class AdminBootstrapServiceTests : IDisposable
{
    private readonly SqliteApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Admin_seeds_on_first_startup()
    {
        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AdminSeed:Email"] = "admin@test.local",
                    ["AdminSeed:Password"] = "TestPassword123!",
                })));

        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BarbershopDbContext>();
        var admins = await context.Accounts.Where(a => a.Role == Role.Admin).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(admins);
        Assert.Equal("admin@test.local", admins[0].Email);
    }

    [Fact]
    public async Task Admin_not_reseeded_if_one_exists()
    {
        await using (var seedContext = _factory.CreateDbContext())
        {
            var repository = new AccountRepository(seedContext);
            var passwordHasher = new PasswordHasher<Account>();
            var existingAdmin = new Account
            {
                Email = "existing-admin@test.local",
                FirstName = "Admin",
                LastName = "Admin",
                Role = Role.Admin,
            };
            existingAdmin.PasswordHash = passwordHasher.HashPassword(existingAdmin, "ExistingPassword123!");
            await repository.Create(existingAdmin);
        }

        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AdminSeed:Email"] = "admin@test.local",
                    ["AdminSeed:Password"] = "TestPassword123!",
                })));

        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BarbershopDbContext>();
        var admins = await context.Accounts.Where(a => a.Role == Role.Admin).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Single(admins);
        Assert.Equal("existing-admin@test.local", admins[0].Email);
    }

    [Fact]
    public async Task Admin_bootstrap_skips_without_throwing_when_unconfigured()
    {
        using var client = _factory.CreateClient();

        await using var context = _factory.CreateDbContext();
        var admins = await context.Accounts.Where(a => a.Role == Role.Admin).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(admins);
    }

    [Fact]
    public async Task Admin_bootstrap_skips_without_throwing_when_seed_email_collides_with_existing_customer()
    {
        await using (var seedContext = _factory.CreateDbContext())
        {
            var repository = new AccountRepository(seedContext);
            var passwordHasher = new PasswordHasher<Account>();
            var existingCustomer = new Account
            {
                Email = "admin@test.local",
                FirstName = "John",
                LastName = "Smith",
                Role = Role.Customer,
            };
            existingCustomer.PasswordHash = passwordHasher.HashPassword(existingCustomer, "ExistingPassword123!");
            await repository.Create(existingCustomer);
        }

        using var factory = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["AdminSeed:Email"] = "admin@test.local",
                    ["AdminSeed:Password"] = "TestPassword123!",
                })));

        using var client = factory.CreateClient();

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<BarbershopDbContext>();
        var admins = await context.Accounts.Where(a => a.Role == Role.Admin).ToListAsync(TestContext.Current.CancellationToken);

        Assert.Empty(admins);
    }
}
