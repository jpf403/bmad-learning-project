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

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var email = configuration["BarberSeed:Email"];
        var password = configuration["BarberSeed:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return;
        }

        using var scope = scopeFactory.CreateScope();
        var accountRepository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();
        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Account>>();

        var barber = new Account
        {
            Email = email,
            FirstName = "Barber",
            LastName = "One",
            Role = Role.Barber,
        };
        barber.PasswordHash = passwordHasher.HashPassword(barber, password);

        try
        {
            await accountRepository.Create(barber);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintViolation })
        {
            logger.LogWarning(
                "BarberSeed:Email {Email} collides with an existing account — skipping barber seed.", barber.Email);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
