using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Tests;

public class AccountRepositoryTests : IDisposable
{
    private readonly SqliteApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static Account NewAccount(string email = "jack@example.com", Role role = Role.Customer) => new()
    {
        Email = email,
        PasswordHash = "hashed-password",
        FirstName = "Jack",
        LastName = "Formato",
        Role = role,
    };

    [Fact]
    public async Task Create_persists_account_with_expected_defaults()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);

        var created = await repository.Create(NewAccount());

        Assert.True(created.Id > 0);
        Assert.Equal(0, created.SessionVersion);
        Assert.Null(created.DeletedAt);
        Assert.Equal(0, created.RowVersion);
    }

    [Fact]
    public async Task Create_lowercases_email_before_persisting()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);

        var created = await repository.Create(NewAccount(email: "Jack@Example.com"));

        Assert.Equal("jack@example.com", created.Email);
    }

    [Fact]
    public async Task Create_trims_whitespace_before_persisting()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);

        var created = await repository.Create(NewAccount(email: "  jack@example.com  "));

        await using var verifyContext = _factory.CreateDbContext();
        var verifyRepository = new AccountRepository(verifyContext);
        var reloaded = await verifyRepository.FindById(created.Id);

        Assert.NotNull(reloaded);
        Assert.Equal("jack@example.com", reloaded.Email);
    }

    [Fact]
    public async Task Create_with_duplicate_active_email_throws()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        await repository.Create(NewAccount(email: "jack@example.com"));

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.Create(NewAccount(email: "jack@example.com")));
    }

    [Fact]
    public async Task Create_with_differently_cased_duplicate_email_throws()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        await repository.Create(NewAccount(email: "jack@example.com"));

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.Create(NewAccount(email: "Jack@Example.com")));
    }

    [Fact]
    public async Task Create_after_soft_delete_of_duplicate_email_succeeds()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var first = await repository.Create(NewAccount(email: "jack@example.com"));

        first.DeletedAt = DateTime.UtcNow;
        await repository.Update(first);

        var second = await repository.Create(NewAccount(email: "jack@example.com"));

        Assert.True(second.Id > 0);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task FindByEmail_matches_regardless_of_input_casing()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.Create(NewAccount(email: "jack@example.com"));

        var found = await repository.FindByEmail("Jack@Example.com");

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public async Task FindByEmail_matches_despite_surrounding_whitespace()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.Create(NewAccount(email: "jack@example.com"));

        var found = await repository.FindByEmail("  jack@example.com  ");

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public async Task FindByEmail_returns_null_for_soft_deleted_account()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.Create(NewAccount(email: "jack@example.com"));
        created.DeletedAt = DateTime.UtcNow;
        await repository.Update(created);

        var found = await repository.FindByEmail("jack@example.com");

        Assert.Null(found);
    }

    [Fact]
    public async Task FindByEmail_returns_null_when_no_match()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);

        var found = await repository.FindByEmail("nobody@example.com");

        Assert.Null(found);
    }

    [Fact]
    public async Task FindById_returns_null_for_soft_deleted_account()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.Create(NewAccount());
        created.DeletedAt = DateTime.UtcNow;
        await repository.Update(created);

        var found = await repository.FindById(created.Id);

        Assert.Null(found);
    }

    [Fact]
    public async Task FindById_returns_null_when_no_match()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);

        var found = await repository.FindById(999);

        Assert.Null(found);
    }

    [Fact]
    public async Task Update_normalizes_email_before_persisting()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.Create(NewAccount(email: "jack@example.com"));

        created.Email = " Jack@Example.com ";
        await repository.Update(created);

        Assert.Equal("jack@example.com", created.Email);
    }

    [Fact]
    public async Task Update_with_duplicate_active_email_throws()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        await repository.Create(NewAccount(email: "jack@example.com"));
        var second = await repository.Create(NewAccount(email: "someone-else@example.com"));

        second.Email = "Jack@Example.com";

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.Update(second));
    }

    [Fact]
    public async Task Update_twice_on_same_instance_does_not_throw_spurious_concurrency_exception()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.Create(NewAccount());

        created.FirstName = "First update";
        await repository.Update(created);

        created.FirstName = "Second update";
        await repository.Update(created);

        await using var verifyContext = _factory.CreateDbContext();
        var verifyRepository = new AccountRepository(verifyContext);
        var reloaded = await verifyRepository.FindById(created.Id);

        Assert.NotNull(reloaded);
        Assert.Equal("Second update", reloaded.FirstName);
        Assert.Equal(created.RowVersion, reloaded.RowVersion);
    }

    [Fact]
    public async Task Update_increments_RowVersion()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.Create(NewAccount());
        var initialRowVersion = created.RowVersion;

        created.FirstName = "Updated";
        await repository.Update(created);

        // Reload via a separate context: the same context's identity map would just
        // hand back the in-memory tracked instance without re-querying RowVersion,
        // masking a missing/broken trigger (see Task 3's SQLite RowVersion note).
        await using var verifyContext = _factory.CreateDbContext();
        var verifyRepository = new AccountRepository(verifyContext);
        var reloaded = await verifyRepository.FindById(created.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(initialRowVersion + 1, reloaded.RowVersion);
    }

    [Fact]
    public async Task Update_with_stale_RowVersion_throws_DbUpdateConcurrencyException()
    {
        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AccountRepository(contextA);
        var created = await repositoryA.Create(NewAccount());

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AccountRepository(contextB);
        var staleCopy = await repositoryB.FindById(created.Id);
        Assert.NotNull(staleCopy);

        created.FirstName = "First update";
        await repositoryA.Update(created);

        staleCopy.FirstName = "Second update";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repositoryB.Update(staleCopy));
    }
}
