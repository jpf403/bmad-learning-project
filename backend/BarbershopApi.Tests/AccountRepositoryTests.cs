using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using BarbershopApi.Services;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Tests;

public class AccountRepositoryTests : IDisposable
{
    private readonly SqliteApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static Account NewAccount(string email = "john@example.com", Role role = Role.Customer, string firstName = "John", string lastName = "Smith") => new()
    {
        Email = email,
        PasswordHash = "hashed-password",
        FirstName = firstName,
        LastName = lastName,
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

        var created = await repository.Create(NewAccount(email: "John@Example.com"));

        Assert.Equal("john@example.com", created.Email);
    }

    [Fact]
    public async Task Create_trims_whitespace_before_persisting()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);

        var created = await repository.Create(NewAccount(email: "  john@example.com  "));

        await using var verifyContext = _factory.CreateDbContext();
        var verifyRepository = new AccountRepository(verifyContext);
        var reloaded = await verifyRepository.FindById(created.Id);

        Assert.NotNull(reloaded);
        Assert.Equal("john@example.com", reloaded.Email);
    }

    [Fact]
    public async Task Create_with_duplicate_active_email_throws()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        await repository.Create(NewAccount(email: "john@example.com"));

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.Create(NewAccount(email: "john@example.com")));
    }

    [Fact]
    public async Task Create_with_differently_cased_duplicate_email_throws()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        await repository.Create(NewAccount(email: "john@example.com"));

        await Assert.ThrowsAsync<DbUpdateException>(() => repository.Create(NewAccount(email: "John@Example.com")));
    }

    [Fact]
    public async Task Create_after_soft_delete_of_duplicate_email_succeeds()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var first = await repository.Create(NewAccount(email: "john@example.com"));

        await repository.SoftDelete(first);

        var second = await repository.Create(NewAccount(email: "john@example.com"));

        Assert.True(second.Id > 0);
        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task FindByEmail_matches_regardless_of_input_casing()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.Create(NewAccount(email: "john@example.com"));

        var found = await repository.FindByEmail("John@Example.com");

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public async Task FindByEmail_matches_despite_surrounding_whitespace()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.Create(NewAccount(email: "john@example.com"));

        var found = await repository.FindByEmail("  john@example.com  ");

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public async Task FindByEmail_returns_null_for_soft_deleted_account()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.Create(NewAccount(email: "john@example.com"));
        await repository.SoftDelete(created);

        var found = await repository.FindByEmail("john@example.com");

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
        await repository.SoftDelete(created);

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
        var created = await repository.Create(NewAccount(email: "john@example.com"));

        created.Email = " John@Example.com ";
        await repository.Update(created);

        Assert.Equal("john@example.com", created.Email);
    }

    [Fact]
    public async Task Update_with_duplicate_active_email_throws()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        await repository.Create(NewAccount(email: "john@example.com"));
        var second = await repository.Create(NewAccount(email: "someone-else@example.com"));

        second.Email = "John@Example.com";

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

    [Fact]
    public async Task FindAllByRole_returns_only_matching_role_ordered_by_name()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        await repository.Create(NewAccount(email: "zack@example.com", role: Role.Barber, firstName: "Zack"));
        await repository.Create(NewAccount(email: "amy@example.com", role: Role.Barber, firstName: "Amy"));
        await repository.Create(NewAccount(email: "customer@example.com", role: Role.Customer));

        var barbers = await repository.FindAllByRole(Role.Barber);

        Assert.Equal(2, barbers.Count);
        Assert.Equal("Amy", barbers[0].FirstName);
        Assert.Equal("Zack", barbers[1].FirstName);
    }

    [Fact]
    public async Task FindAllByRole_excludes_soft_deleted_accounts()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var barber = await repository.Create(NewAccount(email: "barber@example.com", role: Role.Barber));
        await repository.SoftDelete(barber);

        var barbers = await repository.FindAllByRole(Role.Barber);

        Assert.Empty(barbers);
    }

    [Fact]
    public async Task FindAllByRole_returns_empty_list_when_none_exist()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);

        var barbers = await repository.FindAllByRole(Role.Barber);

        Assert.Empty(barbers);
    }

    [Fact]
    public async Task Search_matches_partial_name_or_email_case_insensitive()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var nameMatch = await repository.Create(NewAccount(email: "jane@example.com", firstName: "Jane", lastName: "Doeling"));
        var emailMatch = await repository.Create(NewAccount(email: "doe-family@example.com", firstName: "Other", lastName: "Person"));
        var noMatch = await repository.Create(NewAccount(email: "nomatch@example.com", firstName: "Zed", lastName: "Zephyr"));

        var results = await repository.Search("DOE");

        Assert.Equal(2, results.Count);
        Assert.Contains(results, a => a.Id == nameMatch.Id);
        Assert.Contains(results, a => a.Id == emailMatch.Id);
        Assert.DoesNotContain(results, a => a.Id == noMatch.Id);
    }

    [Fact]
    public async Task Search_excludes_admin_account()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        await repository.Create(NewAccount(email: "admin@example.com", role: Role.Admin, firstName: "Adminstrator", lastName: "Root"));

        var results = await repository.Search("adminstrator");

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_excludes_soft_deleted_accounts()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var deleted = await repository.Create(NewAccount(email: "deleted@example.com", firstName: "Deleted", lastName: "Person"));
        await repository.SoftDelete(deleted);

        var results = await repository.Search("deleted");

        Assert.Empty(results);
    }

    [Fact]
    public async Task Search_matches_combined_first_and_last_name()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var nameMatch = await repository.Create(NewAccount(email: "jane@example.com", firstName: "Jane", lastName: "Doeling"));
        var noMatch = await repository.Create(NewAccount(email: "nomatch@example.com", firstName: "Zed", lastName: "Zephyr"));

        var results = await repository.Search("jane doeling");

        Assert.Equal([nameMatch.Id], results.Select(a => a.Id));
        Assert.DoesNotContain(results, a => a.Id == noMatch.Id);
    }

    [Fact]
    public async Task Search_with_blank_query_returns_empty_list()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        await repository.Create(NewAccount());

        var results = await repository.Search("   ");

        Assert.Empty(results);
    }

    [Fact]
    public async Task AdminUpdate_updates_fields_and_increments_RowVersion()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.Create(NewAccount(role: Role.Customer));
        var initialRowVersion = created.RowVersion;

        created.FirstName = "Updated";
        created.Role = Role.Barber;
        await repository.AdminUpdate(created);

        await using var verifyContext = _factory.CreateDbContext();
        var verifyRepository = new AccountRepository(verifyContext);
        var reloaded = await verifyRepository.FindById(created.Id);

        Assert.NotNull(reloaded);
        Assert.Equal("Updated", reloaded.FirstName);
        Assert.Equal(Role.Barber, reloaded.Role);
        Assert.Equal(initialRowVersion + 1, reloaded.RowVersion);
    }

    [Fact]
    public async Task AdminUpdate_on_admin_account_throws_AdminAccountProtectedException()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var admin = await repository.Create(NewAccount(email: "admin@example.com", role: Role.Admin));

        admin.FirstName = "Attempted Update";
        await Assert.ThrowsAsync<AdminAccountProtectedException>(() => repository.AdminUpdate(admin));
    }

    [Fact]
    public async Task AdminUpdate_on_stale_RowVersion_throws_DbUpdateConcurrencyException()
    {
        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AccountRepository(contextA);
        var created = await repositoryA.Create(NewAccount());

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AccountRepository(contextB);
        var staleCopy = await repositoryB.FindById(created.Id);
        Assert.NotNull(staleCopy);

        created.FirstName = "First update";
        await repositoryA.AdminUpdate(created);

        staleCopy.FirstName = "Second update";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repositoryB.AdminUpdate(staleCopy));
    }

    [Fact]
    public async Task AdminUpdate_racing_a_self_service_Update_throws_DbUpdateConcurrencyException()
    {
        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AccountRepository(contextA);
        var created = await repositoryA.Create(NewAccount());

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AccountRepository(contextB);
        var staleCopy = await repositoryB.FindById(created.Id);
        Assert.NotNull(staleCopy);

        created.FirstName = "Self-service update";
        await repositoryA.Update(created);

        staleCopy!.FirstName = "Admin update";
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repositoryB.AdminUpdate(staleCopy));
    }

    [Fact]
    public async Task AdminUpdate_promoting_a_non_admin_account_to_Admin_throws_InvalidRoleAssignmentException()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.Create(NewAccount(role: Role.Barber));

        created.Role = Role.Admin;
        await Assert.ThrowsAsync<InvalidRoleAssignmentException>(() => repository.AdminUpdate(created));
    }

    [Fact]
    public async Task SoftDelete_sets_DeletedAt()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.Create(NewAccount());

        await repository.SoftDelete(created);

        await using var verifyContext = _factory.CreateDbContext();
        var reloaded = await verifyContext.Accounts.FirstOrDefaultAsync(a => a.Id == created.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded!.DeletedAt);
    }

    [Fact]
    public async Task SoftDelete_on_admin_account_throws_AdminAccountProtectedException()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var admin = await repository.Create(NewAccount(email: "admin@example.com", role: Role.Admin));

        await Assert.ThrowsAsync<AdminAccountProtectedException>(() => repository.SoftDelete(admin));
    }

    [Fact]
    public async Task SoftDelete_on_stale_RowVersion_throws_DbUpdateConcurrencyException()
    {
        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AccountRepository(contextA);
        var created = await repositoryA.Create(NewAccount());

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AccountRepository(contextB);
        var staleCopy = await repositoryB.FindById(created.Id);
        Assert.NotNull(staleCopy);

        created.FirstName = "First update";
        await repositoryA.AdminUpdate(created);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => repositoryB.SoftDelete(staleCopy));
    }

    [Fact]
    public async Task FindBySsoIdentity_matches_provider_and_subject_id()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.CreateOrLinkSsoAccount("jane@example.com", "Jane", "Doe", "z-pax", "subject-123");

        var found = await repository.FindBySsoIdentity("z-pax", "subject-123");

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
    }

    [Fact]
    public async Task FindBySsoIdentity_returns_null_when_no_match()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);

        var found = await repository.FindBySsoIdentity("z-pax", "nonexistent-subject");

        Assert.Null(found);
    }

    [Fact]
    public async Task FindBySsoIdentity_excludes_soft_deleted_accounts()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var created = await repository.CreateOrLinkSsoAccount("jane@example.com", "Jane", "Doe", "z-pax", "subject-123");
        await repository.SoftDelete(created);

        var found = await repository.FindBySsoIdentity("z-pax", "subject-123");

        Assert.Null(found);
    }

    [Fact]
    public async Task CreateOrLinkSsoAccount_creates_new_account_with_Role_Customer_and_null_PasswordHash_when_no_email_match()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);

        var created = await repository.CreateOrLinkSsoAccount("jane@example.com", "Jane", "Doe", "z-pax", "subject-123");

        Assert.True(created.Id > 0);
        Assert.Equal(Role.Customer, created.Role);
        Assert.Null(created.PasswordHash);
        Assert.Equal("jane@example.com", created.Email);
        Assert.Equal("z-pax", created.SsoProvider);
        Assert.Equal("subject-123", created.SsoSubjectId);
    }

    [Fact]
    public async Task CreateOrLinkSsoAccount_links_to_existing_account_by_email_without_altering_PasswordHash()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var existing = await repository.Create(NewAccount(email: "jane@example.com"));
        var originalPasswordHash = existing.PasswordHash;

        var linked = await repository.CreateOrLinkSsoAccount("jane@example.com", "Jane", "Doe", "z-pax", "subject-123");

        Assert.Equal(existing.Id, linked.Id);
        Assert.Equal(originalPasswordHash, linked.PasswordHash);
        Assert.Equal("z-pax", linked.SsoProvider);
        Assert.Equal("subject-123", linked.SsoSubjectId);
    }

    [Fact]
    public async Task CreateOrLinkSsoAccount_linking_preserves_the_existing_account_Role()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        await repository.Create(NewAccount(email: "barber@example.com", role: Role.Barber));

        var linked = await repository.CreateOrLinkSsoAccount("barber@example.com", "Jane", "Doe", "z-pax", "subject-123");

        Assert.Equal(Role.Barber, linked.Role);
    }

    [Fact]
    public async Task CreateOrLinkSsoAccount_on_existing_admin_account_throws_AdminAccountProtectedException()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        await repository.Create(NewAccount(email: "admin@example.com", role: Role.Admin));

        await Assert.ThrowsAsync<AdminAccountProtectedException>(
            () => repository.CreateOrLinkSsoAccount("admin@example.com", "Jane", "Doe", "z-pax", "subject-123"));
    }
}
