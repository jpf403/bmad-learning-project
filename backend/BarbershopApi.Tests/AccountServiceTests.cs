using BarbershopApi.Data;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using BarbershopApi.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Tests;

public class AccountServiceTests : IDisposable
{
    private readonly SqliteApiFactory _factory = new();
    private readonly IPasswordHasher<Account> _passwordHasher = new PasswordHasher<Account>();

    public void Dispose() => _factory.Dispose();

    private const string ExistingPassword = "correct-horse-battery-staple";

    private Account NewAccount(string email = "john@example.com", Role role = Role.Customer, string firstName = "John", string lastName = "Smith")
    {
        var account = new Account
        {
            Email = email,
            FirstName = firstName,
            LastName = lastName,
            Role = role,
        };
        account.PasswordHash = _passwordHasher.HashPassword(account, ExistingPassword);
        return account;
    }

    private static IBookingService NewBookingService(BarbershopDbContext context, IAccountRepository accountRepository) =>
        new BookingService(new AppointmentRepository(context), accountRepository);

    [Fact]
    public async Task UpdateOwnProfile_updates_first_and_last_name()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));
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
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));
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
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));
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
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));
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
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));
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
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));
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
        var serviceB = new AccountService(repositoryB, _passwordHasher, NewBookingService(contextB, repositoryB));
        var staleCopy = await repositoryB.FindById(created.Id);
        Assert.NotNull(staleCopy);

        created.FirstName = "First update";
        await repositoryA.Update(created);

        await Assert.ThrowsAsync<AccountConflictException>(
            () => serviceB.UpdateOwnProfile(staleCopy.Id, "Second update", "Smith", null, null));
    }

    [Fact]
    public async Task AdminCreateBarber_creates_account_with_Role_Barber()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));

        var created = await service.AdminCreateBarber("barber@example.com", "Bob", "Barbington", ExistingPassword);

        Assert.Equal(Role.Barber, created.Role);
        Assert.True(created.Id > 0);
    }

    [Fact]
    public async Task AdminCreateBarber_rejects_duplicate_email()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));
        await repository.Create(NewAccount(email: "barber@example.com"));

        await Assert.ThrowsAsync<DuplicateEmailException>(
            () => service.AdminCreateBarber("barber@example.com", "Bob", "Barbington", ExistingPassword));
    }

    [Fact]
    public async Task AdminUpdateAccount_password_change_increments_SessionVersion()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));
        var created = await repository.Create(NewAccount());
        var admin = await repository.Create(NewAccount(email: "admin@example.com", role: Role.Admin));
        var initialSessionVersion = created.SessionVersion;

        var updated = await service.AdminUpdateAccount(created.Id, created.Email, created.FirstName, created.LastName, created.Role, "new-correct-horse-battery", admin.Id);

        Assert.Equal(initialSessionVersion + 1, updated.SessionVersion);
        var result = _passwordHasher.VerifyHashedPassword(updated, updated.PasswordHash, "new-correct-horse-battery");
        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public async Task AdminUpdateAccount_permission_only_change_does_not_increment_SessionVersion()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));
        var created = await repository.Create(NewAccount(role: Role.Customer));
        var admin = await repository.Create(NewAccount(email: "admin@example.com", role: Role.Admin));

        var updated = await service.AdminUpdateAccount(created.Id, created.Email, created.FirstName, created.LastName, Role.Barber, null, admin.Id);

        Assert.Equal(0, updated.SessionVersion);
        Assert.Equal(Role.Barber, updated.Role);
    }

    [Fact]
    public async Task AdminUpdateAccount_rejects_role_Admin_value_with_InvalidRoleAssignmentException()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));
        var created = await repository.Create(NewAccount());
        var admin = await repository.Create(NewAccount(email: "admin@example.com", role: Role.Admin));

        await Assert.ThrowsAsync<InvalidRoleAssignmentException>(
            () => service.AdminUpdateAccount(created.Id, created.Email, created.FirstName, created.LastName, Role.Admin, null, admin.Id));
    }

    [Fact]
    public async Task AdminUpdateAccount_on_admin_account_throws_AdminAccountProtectedException()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));
        var admin = await repository.Create(NewAccount(email: "admin@example.com", role: Role.Admin));

        await Assert.ThrowsAsync<AdminAccountProtectedException>(
            () => service.AdminUpdateAccount(admin.Id, admin.Email, "Attempted", admin.LastName, Role.Customer, null, admin.Id));
    }

    [Fact]
    public async Task AdminUpdateAccount_on_missing_account_throws_AccountNotFoundException()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));

        await Assert.ThrowsAsync<AccountNotFoundException>(
            () => service.AdminUpdateAccount(999999, "nobody@example.com", "Nobody", "Nowhere", Role.Customer, null, 1));
    }

    [Fact]
    public async Task AdminUpdateAccount_on_stale_RowVersion_throws_AccountConflictException()
    {
        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AccountRepository(contextA);
        var created = await repositoryA.Create(NewAccount());
        var admin = await repositoryA.Create(NewAccount(email: "admin@example.com", role: Role.Admin));

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AccountRepository(contextB);
        var serviceB = new AccountService(repositoryB, _passwordHasher, NewBookingService(contextB, repositoryB));
        var staleCopy = await repositoryB.FindById(created.Id);
        Assert.NotNull(staleCopy);

        created.FirstName = "First update";
        await repositoryA.AdminUpdate(created);

        await Assert.ThrowsAsync<AccountConflictException>(
            () => serviceB.AdminUpdateAccount(staleCopy.Id, staleCopy.Email, "Second update", staleCopy.LastName, staleCopy.Role, null, admin.Id));
    }

    [Fact]
    public async Task AdminUpdateAccount_racing_a_self_service_UpdateOwnProfile_throws_AccountConflictException()
    {
        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AccountRepository(contextA);
        var serviceA = new AccountService(repositoryA, _passwordHasher, NewBookingService(contextA, repositoryA));
        var created = await repositoryA.Create(NewAccount());
        var admin = await repositoryA.Create(NewAccount(email: "admin@example.com", role: Role.Admin));

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AccountRepository(contextB);
        var serviceB = new AccountService(repositoryB, _passwordHasher, NewBookingService(contextB, repositoryB));
        var staleCopy = await repositoryB.FindById(created.Id);
        Assert.NotNull(staleCopy);

        await serviceA.UpdateOwnProfile(created.Id, "Self-service update", created.LastName, null, null);

        await Assert.ThrowsAsync<AccountConflictException>(
            () => serviceB.AdminUpdateAccount(staleCopy!.Id, staleCopy.Email, "Admin update", staleCopy.LastName, staleCopy.Role, null, admin.Id));
    }

    [Fact]
    public async Task AdminUpdateAccount_demoting_barber_to_customer_cancels_future_appointments_but_retains_past()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var appointmentRepository = new AppointmentRepository(context);
        var bookingService = NewBookingService(context, repository);
        var service = new AccountService(repository, _passwordHasher, bookingService);
        var barber = await repository.Create(NewAccount(email: "barber@example.com", role: Role.Barber));
        var customer = await repository.Create(NewAccount(email: "customer@example.com"));
        var admin = await repository.Create(NewAccount(email: "admin@example.com", role: Role.Admin));
        var future = await appointmentRepository.Create(new Appointment
        {
            CustomerId = customer.Id,
            BarberId = barber.Id,
            Date = "2099-01-01",
            StartTime = "09:00",
        });
        var past = await appointmentRepository.Create(new Appointment
        {
            CustomerId = customer.Id,
            BarberId = barber.Id,
            Date = "2020-01-01",
            StartTime = "09:00",
        });

        var updated = await service.AdminUpdateAccount(barber.Id, barber.Email, barber.FirstName, barber.LastName, Role.Customer, null, admin.Id);

        await using var verifyContext = _factory.CreateDbContext();
        var futureReloaded = await verifyContext.Appointments.FirstOrDefaultAsync(a => a.Id == future.Id, TestContext.Current.CancellationToken);
        var pastReloaded = await verifyContext.Appointments.FirstOrDefaultAsync(a => a.Id == past.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(futureReloaded);
        Assert.NotNull(futureReloaded!.CancelledAt);
        Assert.NotNull(pastReloaded);
        Assert.Null(pastReloaded!.CancelledAt);
        Assert.Equal(0, updated.SessionVersion);
    }

    [Fact]
    public async Task AdminUpdateAccount_editing_a_customer_account_leaves_its_appointments_untouched()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var appointmentRepository = new AppointmentRepository(context);
        var bookingService = NewBookingService(context, repository);
        var service = new AccountService(repository, _passwordHasher, bookingService);
        var barber = await repository.Create(NewAccount(email: "barber@example.com", role: Role.Barber));
        var customer = await repository.Create(NewAccount(email: "customer@example.com"));
        var admin = await repository.Create(NewAccount(email: "admin@example.com", role: Role.Admin));
        var appointment = await appointmentRepository.Create(new Appointment
        {
            CustomerId = customer.Id,
            BarberId = barber.Id,
            Date = "2099-01-01",
            StartTime = "09:00",
        });

        await service.AdminUpdateAccount(customer.Id, customer.Email, "Updated Name", customer.LastName, Role.Customer, null, admin.Id);

        await using var verifyContext = _factory.CreateDbContext();
        var reloaded = await verifyContext.Appointments.FirstOrDefaultAsync(a => a.Id == appointment.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(reloaded);
        Assert.Null(reloaded!.CancelledAt);
    }

    [Fact]
    public async Task AdminSoftDeleteAccount_on_barber_cancels_future_appointments_but_retains_past()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var appointmentRepository = new AppointmentRepository(context);
        var bookingService = NewBookingService(context, repository);
        var service = new AccountService(repository, _passwordHasher, bookingService);
        var barber = await repository.Create(NewAccount(email: "barber@example.com", role: Role.Barber));
        var customer = await repository.Create(NewAccount(email: "customer@example.com"));
        var admin = await repository.Create(NewAccount(email: "admin@example.com", role: Role.Admin));
        var future = await appointmentRepository.Create(new Appointment
        {
            CustomerId = customer.Id,
            BarberId = barber.Id,
            Date = "2099-01-01",
            StartTime = "09:00",
        });
        var past = await appointmentRepository.Create(new Appointment
        {
            CustomerId = customer.Id,
            BarberId = barber.Id,
            Date = "2020-01-01",
            StartTime = "09:00",
        });

        await service.AdminSoftDeleteAccount(barber.Id, admin.Id);

        await using var verifyContext = _factory.CreateDbContext();
        var futureReloaded = await verifyContext.Appointments.FirstOrDefaultAsync(a => a.Id == future.Id, TestContext.Current.CancellationToken);
        var pastReloaded = await verifyContext.Appointments.FirstOrDefaultAsync(a => a.Id == past.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(futureReloaded);
        Assert.NotNull(futureReloaded!.CancelledAt);
        Assert.NotNull(pastReloaded);
        Assert.Null(pastReloaded!.CancelledAt);
    }

    [Fact]
    public async Task AdminSoftDeleteAccount_on_admin_account_throws_AdminAccountProtectedException()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var service = new AccountService(repository, _passwordHasher, NewBookingService(context, repository));
        var admin = await repository.Create(NewAccount(email: "admin@example.com", role: Role.Admin));

        await Assert.ThrowsAsync<AdminAccountProtectedException>(() => service.AdminSoftDeleteAccount(admin.Id, admin.Id));
    }

    [Fact]
    public async Task AdminSoftDeleteAccount_on_stale_RowVersion_throws_AccountConflictException()
    {
        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AccountRepository(contextA);
        var created = await repositoryA.Create(NewAccount());
        var admin = await repositoryA.Create(NewAccount(email: "admin@example.com", role: Role.Admin));

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AccountRepository(contextB);
        var serviceB = new AccountService(repositoryB, _passwordHasher, NewBookingService(contextB, repositoryB));
        var staleCopy = await repositoryB.FindById(created.Id);
        Assert.NotNull(staleCopy);

        created.FirstName = "First update";
        await repositoryA.AdminUpdate(created);

        await Assert.ThrowsAsync<AccountConflictException>(() => serviceB.AdminSoftDeleteAccount(staleCopy.Id, admin.Id));
    }
}
