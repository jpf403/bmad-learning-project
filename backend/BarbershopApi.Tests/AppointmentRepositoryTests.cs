using BarbershopApi.Data;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using BarbershopApi.Services;
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
    public async Task Create_second_appointment_for_same_barber_slot_throws()
    {
        await using var seedContext = _factory.CreateDbContext();
        var customerA = await SeedAccount(seedContext, "customerA@example.com", Role.Customer);
        var customerB = await SeedAccount(seedContext, "customerB@example.com", Role.Customer);
        var barber = await SeedAccount(seedContext, "barber@example.com", Role.Barber);

        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AppointmentRepository(contextA);
        await repositoryA.Create(NewAppointment(customerA.Id, barber.Id));

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AppointmentRepository(contextB);

        await Assert.ThrowsAsync<DbUpdateException>(() => repositoryB.Create(NewAppointment(customerB.Id, barber.Id)));
    }

    [Fact]
    public async Task Create_second_appointment_for_same_customer_slot_across_different_barbers_throws()
    {
        await using var seedContext = _factory.CreateDbContext();
        var customer = await SeedAccount(seedContext, "customer@example.com", Role.Customer);
        var barberA = await SeedAccount(seedContext, "barberA@example.com", Role.Barber);
        var barberB = await SeedAccount(seedContext, "barberB@example.com", Role.Barber);

        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AppointmentRepository(contextA);
        await repositoryA.Create(NewAppointment(customer.Id, barberA.Id));

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AppointmentRepository(contextB);

        await Assert.ThrowsAsync<DbUpdateException>(() => repositoryB.Create(NewAppointment(customer.Id, barberB.Id)));
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
        await repository.Cancel(cancelled.Id);

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
        await repository.Cancel(toCancel.Id);

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

        await repository.Cancel(created.Id);

        await using var verifyContext = _factory.CreateDbContext();
        var reloaded = await verifyContext.Appointments.FirstOrDefaultAsync(a => a.Id == created.Id, TestContext.Current.CancellationToken);
        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded!.CancelledAt);
    }

    [Fact]
    public async Task Cancel_on_already_cancelled_appointment_throws_AppointmentAlreadyCancelledException()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var repository = new AppointmentRepository(context);
        var created = await repository.Create(NewAppointment(customer.Id, barber.Id));
        await repository.Cancel(created.Id);

        await Assert.ThrowsAsync<AppointmentAlreadyCancelledException>(() => repository.Cancel(created.Id));
    }
}
