using BarbershopApi.Data;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Tests;

public class AppointmentRepositoryTests : IDisposable
{
    private readonly SqliteApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static async Task<Account> SeedAccount(BarbershopDbContext context, string email, Role role)
    {
        var repository = new AccountRepository(context);
        return await repository.Create(new Account
        {
            Email = email,
            PasswordHash = "hashed-password",
            FirstName = "John",
            LastName = "Smith",
            Role = role,
        });
    }

    private static Appointment NewAppointment(int customerId, int barberId, string date = "2026-09-01", string startTime = "09:00") => new()
    {
        CustomerId = customerId,
        BarberId = barberId,
        Date = date,
        StartTime = startTime,
    };

    [Fact]
    public async Task Create_persists_appointment_with_expected_defaults()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var repository = new AppointmentRepository(context);

        var created = await repository.Create(NewAppointment(customer.Id, barber.Id));

        Assert.True(created.Id > 0);
        Assert.Null(created.CancelledAt);
    }

    [Fact]
    public async Task Create_throws_when_a_second_context_inserts_the_same_barber_slot_after_the_first_commits()
    {
        await using var seedContext = _factory.CreateDbContext();
        var customerA = await SeedAccount(seedContext, "customerA@example.com", Role.Customer);
        var customerB = await SeedAccount(seedContext, "customerB@example.com", Role.Customer);
        var barber = await SeedAccount(seedContext, "barber@example.com", Role.Barber);

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AppointmentRepository(contextB);
        Assert.False(await repositoryB.ExistsConflict(barber.Id, customerB.Id, "2026-09-01", "09:00"));

        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AppointmentRepository(contextA);
        await repositoryA.Create(NewAppointment(customerA.Id, barber.Id));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => repositoryB.Create(NewAppointment(customerB.Id, barber.Id)));
        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);
        Assert.Equal(19, sqliteException.SqliteErrorCode);
    }

    [Fact]
    public async Task Create_throws_when_a_second_context_inserts_the_same_customer_slot_after_the_first_commits()
    {
        await using var seedContext = _factory.CreateDbContext();
        var customer = await SeedAccount(seedContext, "customer@example.com", Role.Customer);
        var barberA = await SeedAccount(seedContext, "barberA@example.com", Role.Barber);
        var barberB = await SeedAccount(seedContext, "barberB@example.com", Role.Barber);

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AppointmentRepository(contextB);
        Assert.False(await repositoryB.ExistsConflict(barberB.Id, customer.Id, "2026-09-01", "09:00"));

        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AppointmentRepository(contextA);
        await repositoryA.Create(NewAppointment(customer.Id, barberA.Id));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => repositoryB.Create(NewAppointment(customer.Id, barberB.Id)));
        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);
        Assert.Equal(19, sqliteException.SqliteErrorCode);
    }

    [Fact]
    public async Task Create_with_nonexistent_CustomerId_throws_DbUpdateException()
    {
        await using var context = _factory.CreateDbContext();
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var repository = new AppointmentRepository(context);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => repository.Create(NewAppointment(customerId: 999999, barber.Id)));
        var sqliteException = Assert.IsType<SqliteException>(exception.InnerException);
        Assert.Equal(19, sqliteException.SqliteErrorCode);
    }

    [Fact]
    public async Task FindByBarberAndDate_excludes_cancelled_appointments()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var repository = new AppointmentRepository(context);
        var active = await repository.Create(NewAppointment(customer.Id, barber.Id, startTime: "09:00"));
        var cancelled = await repository.Create(NewAppointment(customer.Id, barber.Id, startTime: "10:00"));
        await repository.Cancel(cancelled);

        var found = await repository.FindByBarberAndDate(barber.Id, "2026-09-01");

        var result = Assert.Single(found);
        Assert.Equal(active.Id, result.Id);
    }

    [Fact]
    public async Task FindUpcomingByCustomer_excludes_past_and_cancelled_appointments()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barberA = await SeedAccount(context, "barberA@example.com", Role.Barber);
        var barberB = await SeedAccount(context, "barberB@example.com", Role.Barber);
        var barberC = await SeedAccount(context, "barberC@example.com", Role.Barber);
        var repository = new AppointmentRepository(context);
        var nowEst = new DateTime(2026, 9, 1, 10, 0, 0);

        await repository.Create(NewAppointment(customer.Id, barberA.Id, date: "2026-08-31", startTime: "09:00"));
        var upcoming = await repository.Create(NewAppointment(customer.Id, barberB.Id, date: "2026-09-01", startTime: "11:00"));
        var toCancel = await repository.Create(NewAppointment(customer.Id, barberC.Id, date: "2026-09-02", startTime: "09:00"));
        await repository.Cancel(toCancel);

        var found = await repository.FindUpcomingByCustomer(customer.Id, nowEst);

        var result = Assert.Single(found);
        Assert.Equal(upcoming.Id, result.Id);
    }

    [Fact]
    public async Task Cancel_sets_CancelledAt()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var repository = new AppointmentRepository(context);
        var created = await repository.Create(NewAppointment(customer.Id, barber.Id));

        await repository.Cancel(created);

        await using var verifyContext = _factory.CreateDbContext();
        var reloaded = await verifyContext.Appointments.FirstOrDefaultAsync(a => a.Id == created.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded!.CancelledAt);
    }

    [Fact]
    public async Task FindById_returns_null_when_appointment_does_not_exist()
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AppointmentRepository(context);

        var found = await repository.FindById(999999);

        Assert.Null(found);
    }

    [Fact]
    public async Task ExistsConflict_true_when_barber_already_booked_that_slot()
    {
        await using var context = _factory.CreateDbContext();
        var customerA = await SeedAccount(context, "customerA@example.com", Role.Customer);
        var customerB = await SeedAccount(context, "customerB@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var repository = new AppointmentRepository(context);
        await repository.Create(NewAppointment(customerA.Id, barber.Id));

        var conflict = await repository.ExistsConflict(barber.Id, customerB.Id, "2026-09-01", "09:00");

        Assert.True(conflict);
    }

    [Fact]
    public async Task ExistsConflict_true_when_customer_already_booked_a_different_barber_at_same_time()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barberA = await SeedAccount(context, "barberA@example.com", Role.Barber);
        var barberB = await SeedAccount(context, "barberB@example.com", Role.Barber);
        var repository = new AppointmentRepository(context);
        await repository.Create(NewAppointment(customer.Id, barberA.Id));

        var conflict = await repository.ExistsConflict(barberB.Id, customer.Id, "2026-09-01", "09:00");

        Assert.True(conflict);
    }

    [Fact]
    public async Task ExistsConflict_false_when_no_matching_appointment_exists()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var repository = new AppointmentRepository(context);

        var conflict = await repository.ExistsConflict(barber.Id, customer.Id, "2026-09-01", "09:00");

        Assert.False(conflict);
    }

    [Fact]
    public async Task ExistsConflict_false_when_matching_slot_is_cancelled()
    {
        await using var context = _factory.CreateDbContext();
        var customerA = await SeedAccount(context, "customerA@example.com", Role.Customer);
        var customerB = await SeedAccount(context, "customerB@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var repository = new AppointmentRepository(context);
        var appointment = await repository.Create(NewAppointment(customerA.Id, barber.Id));
        await repository.Cancel(appointment);

        var conflict = await repository.ExistsConflict(barber.Id, customerB.Id, "2026-09-01", "09:00");

        Assert.False(conflict);
    }
}
