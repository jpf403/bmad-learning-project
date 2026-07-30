using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using Microsoft.AspNetCore.Identity;

namespace BarbershopApi.Services;

public class AdminBootstrapService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<AdminBootstrapService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var accountRepository = scope.ServiceProvider.GetRequiredService<IAccountRepository>();

        if (await accountRepository.AdminExists())
        {
            return;
        }

        var email = configuration["AdminSeed:Email"];
        var password = configuration["AdminSeed:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogWarning("AdminSeed:Email/AdminSeed:Password not configured — skipping admin bootstrap.");
            return;
        }

        var passwordHasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<Account>>();
        var admin = new Account
        {
            Email = email,
            FirstName = "Admin",
            LastName = "Admin",
            Role = Role.Admin,
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, password);

        await accountRepository.Create(admin);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
