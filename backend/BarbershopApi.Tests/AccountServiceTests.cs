using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using BarbershopApi.Services;
using Microsoft.AspNetCore.Identity;

namespace BarbershopApi.Tests;

public class AccountServiceTests : IDisposable
{
    private readonly SqliteApiFactory _factory = new();
    private readonly IPasswordHasher<Account> _passwordHasher = new PasswordHasher<Account>();

    public void Dispose() => _factory.Dispose();

    private const string ExistingPassword = "correct-horse-battery-staple";

    private Account NewAccount(string email = "john@example.com")
    {
        var account = new Account
        {
            Email = email,
            FirstName = "John",
            LastName = "Smith",
            Role = Role.Customer,
        };
        account.PasswordHash = _passwordHasher.HashPassword(account, ExistingPassword);
        return account;
    }

    [Fact]
    public async Task UpdateOwnProfile_updates_first_and_last_name()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher);
        var created = await repository.Create(NewAccount());

        var updated = await service.UpdateOwnProfile(created.Id, "Updated First", "Updated Last", null, null);

        Assert.Equal("Updated First", updated.FirstName);
        Assert.Equal("Updated Last", updated.LastName);
    }

    [Fact]
    public async Task UpdateOwnProfile_with_new_password_and_correct_current_password_hashes_it_and_does_not_change_SessionVersion()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher);
        var created = await repository.Create(NewAccount());
        var initialSessionVersion = created.SessionVersion;

        var updated = await service.UpdateOwnProfile(created.Id, "John", "Smith", "new-correct-horse-battery", ExistingPassword);

        Assert.Equal(initialSessionVersion, updated.SessionVersion);
        var result = _passwordHasher.VerifyHashedPassword(updated, updated.PasswordHash, "new-correct-horse-battery");
        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public async Task UpdateOwnProfile_without_new_password_leaves_PasswordHash_unchanged()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher);
        var created = await repository.Create(NewAccount());
        var originalPasswordHash = created.PasswordHash;

        var updated = await service.UpdateOwnProfile(created.Id, "John", "Smith", null, null);

        Assert.Equal(originalPasswordHash, updated.PasswordHash);
    }

    [Fact]
    public async Task UpdateOwnProfile_with_new_password_and_wrong_current_password_throws_InvalidCurrentPasswordException_and_does_not_change_name()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher);
        var created = await repository.Create(NewAccount());

        await Assert.ThrowsAsync<InvalidCurrentPasswordException>(
            () => service.UpdateOwnProfile(created.Id, "Attempted Update", "Smith", "new-correct-horse-battery", "wrong-password"));

        await using var verifyContext = _factory.CreateDbContext();
        var reloaded = await new AccountRepository(verifyContext).FindById(created.Id);
        Assert.Equal("John", reloaded!.FirstName);
    }

    [Fact]
    public async Task UpdateOwnProfile_with_new_password_and_missing_current_password_throws_InvalidCurrentPasswordException_and_does_not_change_name()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher);
        var created = await repository.Create(NewAccount());

        await Assert.ThrowsAsync<InvalidCurrentPasswordException>(
            () => service.UpdateOwnProfile(created.Id, "Attempted Update", "Smith", "new-correct-horse-battery", null));

        await using var verifyContext = _factory.CreateDbContext();
        var reloaded = await new AccountRepository(verifyContext).FindById(created.Id);
        Assert.Equal("John", reloaded!.FirstName);
    }

    [Fact]
    public async Task UpdateOwnProfile_with_new_password_same_as_current_throws_SameAsCurrentPasswordException_and_does_not_change_name()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher);
        var created = await repository.Create(NewAccount());

        await Assert.ThrowsAsync<SameAsCurrentPasswordException>(
            () => service.UpdateOwnProfile(created.Id, "Attempted Update", "Smith", ExistingPassword, ExistingPassword));

        await using var verifyContext = _factory.CreateDbContext();
        var reloaded = await new AccountRepository(verifyContext).FindById(created.Id);
        Assert.Equal("John", reloaded!.FirstName);
    }

    [Fact]
    public async Task UpdateOwnProfile_on_stale_RowVersion_throws_AccountConflictException()
    {
        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AccountRepository(contextA);
        var created = await repositoryA.Create(NewAccount());

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AccountRepository(contextB);
        var serviceB = new AccountService(repositoryB, _passwordHasher);
        var staleCopy = await repositoryB.FindById(created.Id);
        Assert.NotNull(staleCopy);

        created.FirstName = "First update";
        await repositoryA.Update(created);

        await Assert.ThrowsAsync<AccountConflictException>(
            () => serviceB.UpdateOwnProfile(staleCopy.Id, "Second update", "Smith", null, null));
    }
}
