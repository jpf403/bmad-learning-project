using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Services;

// Local dev-only convenience, mirroring AdminBootstrapService's env-var-gated
// pattern (AD-6): a no-op everywhere BarberSeed:Email/BarberSeed:Password
// aren't configured. There is no barber-creation API yet (that's Epic 3) --
// this exists only because the dev machine can't run one-off scripts/
// executables to seed a test barber directly against the SQLite file.
public class BarberSeedService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<BarberSeedService> logger) : IHostedService
{
    private const int SqliteConstraintViolation = 19;

    // Second slot is a temporary, manual-testing-only convenience added for Story 2.6
    // (Admin Schedule Oversight) -- verifying the Select Barber dropdown actually
    // switches between two different barbers' schedules requires a second barber
    // account to exist locally. No-op unless BarberSeed2:Email/BarberSeed2:Password
    // are set, same as the original slot -- safe to leave configured indefinitely,
    // and safe to just stop setting the env vars once manual testing is done (no
    // code needs reverting either way).
    private static readonly (string ConfigPrefix, string FirstName, string LastName)[] Seeds =
    [
        ("BarberSeed", "Barber", "One"),
        ("BarberSeed2", "Barber", "Two"),
    ];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        foreach (var (configPrefix, firstName, lastName) in Seeds)
        {
            await SeedOne(configPrefix, firstName, lastName);
        }
    }

    private async Task SeedOne(string configPrefix, string firstName, string lastName)
    {
        var email = configuration[$"{configPrefix}:Email"];
        var password = configuration[$"{configPrefix}:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        try
        {
            using var scope = scopeFactory.CreateScope();
            var accountRepository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
            var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Account>>();

            var barber = new Account
            {
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                Role = Role.Barber,
            };
            barber.PasswordHash = passwordHasher.HashPassword(barber, password);

            await accountRepository.Create(barber);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintViolation })
        {
            logger.LogWarning(
                "{ConfigPrefix}:Email {Email} collides with an existing account — skipping barber seed.", configPrefix, email);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Barber seed failed for {ConfigPrefix} — skipping.", configPrefix);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
