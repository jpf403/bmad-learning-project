using BarbershopApi.Data;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using BarbershopApi.Services;

namespace BarbershopApi.Tests;

public class BookingServiceTests : IDisposable
{
    private readonly SqliteApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static async Task<Account> SeedAccount(BarbershopDbContext context, string email, Role role, string firstName = "John", string lastName = "Smith")
    {
        var repository = new AccountRepository(context);
        return await repository.Create(new Account
        {
            Email = email,
            PasswordHash = "hashed-password",
            FirstName = firstName,
            LastName = lastName,
            Role = role,
        });
    }

    private static BookingService NewService(BarbershopDbContext context) =>
        new(new AppointmentRepository(context), new AccountRepository(context));

    [Fact]
    public async Task Create_throws_BookingConflictException_when_barber_slot_already_booked()
    {
        await using var context = _factory.CreateDbContext();
        var customerA = await SeedAccount(context, "customerA@example.com", Role.Customer);
        var customerB = await SeedAccount(context, "customerB@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);

        await service.Create(customerA.Id, barber.Id, "2026-09-01", "09:00");

        await Assert.ThrowsAsync<BookingConflictException>(
            () => service.Create(customerB.Id, barber.Id, "2026-09-01", "09:00"));
    }

    [Fact]
    public async Task Create_throws_BookingConflictException_when_customer_already_booked_a_different_barber_at_same_time()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barberA = await SeedAccount(context, "barberA@example.com", Role.Barber);
        var barberB = await SeedAccount(context, "barberB@example.com", Role.Barber);
        var service = NewService(context);

        await service.Create(customer.Id, barberA.Id, "2026-09-01", "09:00");

        await Assert.ThrowsAsync<BookingConflictException>(
            () => service.Create(customer.Id, barberB.Id, "2026-09-01", "09:00"));
    }

    [Fact]
    public async Task FindByBarberAndDate_computes_Finished_correctly_at_the_EST_boundary()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var appointmentRepository = new AppointmentRepository(context);
        var service = new BookingService(appointmentRepository, new AccountRepository(context));

        var estNow = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("America/New_York"));
        var today = estNow.ToString("yyyy-MM-dd");

        var before = await appointmentRepository.Create(new Appointment
        {
            CustomerId = customer.Id,
            BarberId = barber.Id,
            Date = today,
            StartTime = estNow.AddMinutes(-5).ToString("HH:mm"),
        });
        var at = await appointmentRepository.Create(new Appointment
        {
            CustomerId = customer.Id,
            BarberId = barber.Id,
            Date = today,
            StartTime = estNow.ToString("HH:mm"),
        });
        var after = await appointmentRepository.Create(new Appointment
        {
            CustomerId = customer.Id,
            BarberId = barber.Id,
            Date = today,
            StartTime = estNow.AddMinutes(5).ToString("HH:mm"),
        });

        var found = await service.FindByBarberAndDate(barber.Id, today);

        Assert.True(found.Single(a => a.Id == before.Id).Finished);
        Assert.True(found.Single(a => a.Id == at.Id).Finished);
        Assert.False(found.Single(a => a.Id == after.Id).Finished);
    }

    [Fact]
    public async Task FindByBarberAndDate_resolves_customer_and_barber_names()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer, firstName: "Jane", lastName: "Doe");
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber, firstName: "Bob", lastName: "Barbington");
        var appointmentRepository = new AppointmentRepository(context);
        var service = new BookingService(appointmentRepository, new AccountRepository(context));
        await appointmentRepository.Create(new Appointment
        {
            CustomerId = customer.Id,
            BarberId = barber.Id,
            Date = "2026-09-01",
            StartTime = "09:00",
        });

        var found = await service.FindByBarberAndDate(barber.Id, "2026-09-01");

        var view = Assert.Single(found);
        Assert.Equal("Jane Doe", view.CustomerName);
        Assert.Equal("Bob Barbington", view.BarberName);
    }
}
