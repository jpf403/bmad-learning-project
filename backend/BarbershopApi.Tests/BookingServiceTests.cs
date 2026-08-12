using BarbershopApi.Data;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using BarbershopApi.Services;
using Microsoft.EntityFrameworkCore;

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

    private static readonly DateTime FixedNow = new(2026, 9, 1, 8, 0, 0);

    [Fact]
    public async Task Create_throws_BookingConflictException_when_barber_slot_already_booked()
    {
        await using var context = _factory.CreateDbContext();
        var customerA = await SeedAccount(context, "customerA@example.com", Role.Customer);
        var customerB = await SeedAccount(context, "customerB@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);

        await service.Create(customerA.Id, barber.Id, "2026-09-01", "09:00", FixedNow);

        await Assert.ThrowsAsync<BookingConflictException>(
            () => service.Create(customerB.Id, barber.Id, "2026-09-01", "09:00", FixedNow));
    }

    [Fact]
    public async Task Create_throws_BookingConflictException_when_customer_already_booked_a_different_barber_at_same_time()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barberA = await SeedAccount(context, "barberA@example.com", Role.Barber);
        var barberB = await SeedAccount(context, "barberB@example.com", Role.Barber);
        var service = NewService(context);

        await service.Create(customer.Id, barberA.Id, "2026-09-01", "09:00", FixedNow);

        await Assert.ThrowsAsync<BookingConflictException>(
            () => service.Create(customer.Id, barberB.Id, "2026-09-01", "09:00", FixedNow));
    }

    [Fact]
    public async Task Create_throws_InvalidBookingWindowException_for_a_past_date()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);

        await Assert.ThrowsAsync<InvalidBookingWindowException>(
            () => service.Create(customer.Id, barber.Id, "2026-08-31", "09:00", FixedNow));
    }

    [Fact]
    public async Task Create_throws_InvalidBookingWindowException_within_30_minutes_of_now_same_day()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        var now = new DateTime(2026, 9, 1, 8, 45, 0);

        await Assert.ThrowsAsync<InvalidBookingWindowException>(
            () => service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", now));
    }

    [Fact]
    public async Task Create_throws_InvalidBookingWindowException_when_the_30_minute_cutoff_rolls_into_the_next_day()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        var now = new DateTime(2026, 9, 1, 23, 45, 0);

        await Assert.ThrowsAsync<InvalidBookingWindowException>(
            () => service.Create(customer.Id, barber.Id, "2026-09-01", "23:59", now));
    }

    [Theory]
    [InlineData("2026-09-05")] // Saturday
    [InlineData("2026-09-06")] // Sunday
    public async Task Create_throws_InvalidBookingWindowException_for_a_weekend_date(string date)
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);

        await Assert.ThrowsAsync<InvalidBookingWindowException>(
            () => service.Create(customer.Id, barber.Id, date, "09:00", FixedNow));
    }

    [Fact]
    public async Task Create_throws_InvalidBookingWindowException_beyond_the_30_day_cap()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);

        await Assert.ThrowsAsync<InvalidBookingWindowException>(
            () => service.Create(customer.Id, barber.Id, "2026-10-02", "09:00", FixedNow));
    }

    [Fact]
    public async Task Create_succeeds_exactly_30_days_out()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);

        var created = await service.Create(customer.Id, barber.Id, "2026-10-01", "09:00", FixedNow);

        Assert.True(created.Id > 0);
    }

    [Fact]
    public async Task Create_succeeds_exactly_30_minutes_before_start()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        var now = new DateTime(2026, 9, 1, 8, 30, 0);

        var created = await service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", now);

        Assert.True(created.Id > 0);
    }

    [Fact]
    public async Task Create_throws_InvalidBookingWindowException_for_a_startTime_not_on_the_fixed_slot_grid()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);

        await Assert.ThrowsAsync<InvalidBookingWindowException>(
            () => service.Create(customer.Id, barber.Id, "2026-09-01", "09:07", FixedNow));
    }

    [Fact]
    public async Task Create_throws_ArgumentException_when_now_has_a_non_Unspecified_DateTimeKind()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        var utcNow = DateTime.SpecifyKind(FixedNow, DateTimeKind.Utc);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", utcNow));
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
    public async Task Cancel_throws_AppointmentNotFoundException_when_appointment_does_not_exist()
    {
        await using var context = _factory.CreateDbContext();
        var service = NewService(context);

        await Assert.ThrowsAsync<AppointmentNotFoundException>(() => service.Cancel(999999, 1, Role.Customer));
    }

    [Fact]
    public async Task Cancel_on_already_cancelled_appointment_throws_AppointmentAlreadyCancelledException()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        var created = await service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", FixedNow);
        await service.Cancel(created.Id, customer.Id, Role.Customer);

        await Assert.ThrowsAsync<AppointmentAlreadyCancelledException>(() => service.Cancel(created.Id, customer.Id, Role.Customer));
    }

    [Fact]
    public async Task Cancel_throws_AppointmentAlreadyFinishedException_when_the_appointment_has_already_happened()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        var created = await service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", FixedNow);
        var afterAppointment = new DateTime(2026, 9, 1, 9, 0, 0);

        await Assert.ThrowsAsync<AppointmentAlreadyFinishedException>(
            () => service.Cancel(created.Id, customer.Id, Role.Customer, afterAppointment));
    }

    [Fact]
    public async Task Cancel_throws_AppointmentAlreadyFinishedException_even_when_caller_is_admin()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var admin = await SeedAccount(context, "admin@example.com", Role.Admin);
        var service = NewService(context);
        var created = await service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", FixedNow);
        var afterAppointment = new DateTime(2026, 9, 1, 9, 0, 0);

        await Assert.ThrowsAsync<AppointmentAlreadyFinishedException>(
            () => service.Cancel(created.Id, admin.Id, Role.Admin, afterAppointment));
    }

    [Fact]
    public async Task Cancel_succeeds_for_a_not_yet_finished_appointment_when_now_is_explicitly_passed()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        var created = await service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", FixedNow);

        await service.Cancel(created.Id, customer.Id, Role.Customer, FixedNow);
    }

    [Fact]
    public async Task Cancel_throws_ArgumentException_when_now_has_a_non_Unspecified_DateTimeKind()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        var created = await service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", FixedNow);
        var utcNow = DateTime.SpecifyKind(FixedNow, DateTimeKind.Utc);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.Cancel(created.Id, customer.Id, Role.Customer, utcNow));
    }

    [Fact]
    public async Task Cancel_throws_AppointmentNotFoundException_when_caller_is_not_the_owning_customer()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var otherCustomer = await SeedAccount(context, "other-customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        var created = await service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", FixedNow);

        await Assert.ThrowsAsync<AppointmentNotFoundException>(
            () => service.Cancel(created.Id, otherCustomer.Id, Role.Customer));
    }

    [Fact]
    public async Task Cancel_succeeds_when_caller_is_the_appointments_barber()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        var created = await service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", FixedNow);

        await service.Cancel(created.Id, barber.Id, Role.Barber);
    }

    [Fact]
    public async Task Cancel_throws_AppointmentNotFoundException_when_caller_is_a_different_barber()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var otherBarber = await SeedAccount(context, "other-barber@example.com", Role.Barber);
        var service = NewService(context);
        var created = await service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", FixedNow);

        await Assert.ThrowsAsync<AppointmentNotFoundException>(
            () => service.Cancel(created.Id, otherBarber.Id, Role.Barber));
    }

    [Fact]
    public async Task Cancel_succeeds_when_caller_is_admin_regardless_of_owner()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var admin = await SeedAccount(context, "admin@example.com", Role.Admin);
        var service = NewService(context);
        var created = await service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", FixedNow);

        await service.Cancel(created.Id, admin.Id, Role.Admin);
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

    [Fact]
    public async Task GetAvailableSlots_returns_full_fixed_range_for_a_future_date_with_no_bookings()
    {
        await using var context = _factory.CreateDbContext();
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);

        var slots = await service.GetAvailableSlots(barber.Id, "2026-09-01");

        Assert.Equal(16, slots.Count);
        Assert.Equal("09:00", slots.First());
        Assert.Equal("16:30", slots.Last());
    }

    [Fact]
    public async Task GetAvailableSlots_excludes_already_booked_slots()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var appointmentRepository = new AppointmentRepository(context);
        var service = new BookingService(appointmentRepository, new AccountRepository(context));
        await appointmentRepository.Create(new Appointment
        {
            CustomerId = customer.Id,
            BarberId = barber.Id,
            Date = "2026-09-01",
            StartTime = "09:30",
        });

        var slots = await service.GetAvailableSlots(barber.Id, "2026-09-01");

        Assert.DoesNotContain("09:30", slots);
        Assert.Equal(15, slots.Count);
    }

    [Fact]
    public async Task GetAvailableSlots_excludes_slots_within_30_minutes_of_an_explicit_now()
    {
        await using var context = _factory.CreateDbContext();
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);

        var now = new DateTime(2026, 9, 1, 13, 45, 0);

        var slots = await service.GetAvailableSlots(barber.Id, "2026-09-01", now);

        Assert.DoesNotContain("13:30", slots);
        Assert.DoesNotContain("14:00", slots);
        Assert.Contains("14:30", slots);
    }

    [Fact]
    public async Task GetAvailableSlots_excludes_all_slots_when_the_30_minute_cutoff_rolls_into_the_next_day()
    {
        await using var context = _factory.CreateDbContext();
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);

        var now = new DateTime(2026, 9, 1, 23, 45, 0);

        var slots = await service.GetAvailableSlots(barber.Id, "2026-09-01", now);

        Assert.Empty(slots);
    }

    [Fact]
    public async Task GetAvailableSlots_throws_ArgumentException_when_now_has_a_non_Unspecified_DateTimeKind()
    {
        await using var context = _factory.CreateDbContext();
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        var utcNow = DateTime.SpecifyKind(new DateTime(2026, 9, 1, 13, 45, 0), DateTimeKind.Utc);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetAvailableSlots(barber.Id, "2026-09-01", utcNow));
    }

    [Fact]
    public async Task GetDaySchedule_returns_all_sixteen_fixed_slots_as_available_when_nothing_booked()
    {
        await using var context = _factory.CreateDbContext();
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);

        var schedule = await service.GetDaySchedule(barber.Id, "2026-09-01", FixedNow);

        Assert.Equal(16, schedule.Slots.Count);
        Assert.All(schedule.Slots, slot => Assert.Null(slot.Appointment));
    }

    [Fact]
    public async Task GetDaySchedule_attaches_the_booked_appointment_to_its_matching_slot_and_leaves_others_available()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer, firstName: "Jane", lastName: "Doe");
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        await service.Create(customer.Id, barber.Id, "2026-09-01", "10:00", FixedNow);

        var schedule = await service.GetDaySchedule(barber.Id, "2026-09-01", FixedNow);

        var booked = Assert.Single(schedule.Slots, s => s.Appointment is not null);
        Assert.Equal("10:00", booked.StartTime);
        Assert.Equal("Jane Doe", booked.Appointment!.CustomerName);
        Assert.Equal(15, schedule.Slots.Count(s => s.Appointment is null));
    }

    [Fact]
    public async Task GetDaySchedule_only_includes_this_barbers_own_appointments()
    {
        await using var context = _factory.CreateDbContext();
        var customerA = await SeedAccount(context, "customerA@example.com", Role.Customer);
        var customerB = await SeedAccount(context, "customerB@example.com", Role.Customer);
        var barberA = await SeedAccount(context, "barberA@example.com", Role.Barber);
        var barberB = await SeedAccount(context, "barberB@example.com", Role.Barber);
        var service = NewService(context);
        await service.Create(customerA.Id, barberA.Id, "2026-09-01", "09:00", FixedNow);
        await service.Create(customerB.Id, barberB.Id, "2026-09-01", "09:00", FixedNow);

        var schedule = await service.GetDaySchedule(barberA.Id, "2026-09-01", FixedNow);

        var booked = Assert.Single(schedule.Slots, s => s.Appointment is not null);
        Assert.Equal(customerA.Id, booked.Appointment!.CustomerId);
    }

    [Fact]
    public async Task GetDaySchedule_excludes_a_cancelled_appointment_from_the_booked_slot()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        var created = await service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", FixedNow);
        await service.Cancel(created.Id, customer.Id, Role.Customer);

        var schedule = await service.GetDaySchedule(barber.Id, "2026-09-01", FixedNow);

        Assert.All(schedule.Slots, slot => Assert.Null(slot.Appointment));
    }

    [Fact]
    public async Task GetDaySchedule_defaults_to_todays_EST_date_when_date_is_omitted()
    {
        await using var context = _factory.CreateDbContext();
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);

        var schedule = await service.GetDaySchedule(barber.Id, date: null, now: FixedNow);

        Assert.Equal(FixedNow.ToString("yyyy-MM-dd"), schedule.Date);
    }

    [Fact]
    public async Task GetDaySchedule_returns_a_full_available_slot_list_for_a_weekend_date_with_no_special_casing()
    {
        await using var context = _factory.CreateDbContext();
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);

        var schedule = await service.GetDaySchedule(barber.Id, "2026-09-05", FixedNow); // Saturday

        Assert.Equal(16, schedule.Slots.Count);
        Assert.All(schedule.Slots, slot => Assert.Null(slot.Appointment));
    }

    [Fact]
    public async Task GetDaySchedule_throws_ArgumentException_when_now_has_a_non_Unspecified_DateTimeKind()
    {
        await using var context = _factory.CreateDbContext();
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        var utcNow = DateTime.SpecifyKind(FixedNow, DateTimeKind.Utc);

        await Assert.ThrowsAsync<ArgumentException>(
            () => service.GetDaySchedule(barber.Id, "2026-09-01", utcNow));
    }

    [Fact]
    public async Task GetDaySchedule_computes_Finished_using_the_caller_supplied_now_not_real_wall_clock_time()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var service = NewService(context);
        await service.Create(customer.Id, barber.Id, "2026-09-01", "09:00", FixedNow);
        var wellAfter = FixedNow.AddDays(1);

        var schedule = await service.GetDaySchedule(barber.Id, "2026-09-01", wellAfter);

        var booked = Assert.Single(schedule.Slots, s => s.Appointment is not null);
        Assert.True(booked.Appointment!.Finished);
    }

    [Fact]
    public async Task CancelAllFutureForBarber_cancels_all_future_appointments_for_that_barber_only()
    {
        await using var context = _factory.CreateDbContext();
        var customerA = await SeedAccount(context, "customerA@example.com", Role.Customer);
        var customerB = await SeedAccount(context, "customerB@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var otherBarber = await SeedAccount(context, "other-barber@example.com", Role.Barber);
        var admin = await SeedAccount(context, "admin@example.com", Role.Admin);
        var appointmentRepository = new AppointmentRepository(context);
        var service = NewService(context);
        var future = await appointmentRepository.Create(new Appointment
        {
            CustomerId = customerA.Id,
            BarberId = barber.Id,
            Date = "2099-01-01",
            StartTime = "09:00",
        });
        var otherBarberFuture = await appointmentRepository.Create(new Appointment
        {
            CustomerId = customerB.Id,
            BarberId = otherBarber.Id,
            Date = "2099-01-01",
            StartTime = "09:00",
        });

        await service.CancelAllFutureForBarber(barber.Id, admin.Id, Role.Admin, FixedNow);

        await using var verifyContext = _factory.CreateDbContext();
        var cancelled = await verifyContext.Appointments.FirstOrDefaultAsync(a => a.Id == future.Id, TestContext.Current.CancellationToken);
        var untouched = await verifyContext.Appointments.FirstOrDefaultAsync(a => a.Id == otherBarberFuture.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(cancelled);
        Assert.NotNull(cancelled!.CancelledAt);
        Assert.NotNull(untouched);
        Assert.Null(untouched!.CancelledAt);
    }

    [Fact]
    public async Task CancelAllFutureForBarber_tolerates_an_already_cancelled_appointment_without_aborting_the_rest()
    {
        await using var context = _factory.CreateDbContext();
        var customer = await SeedAccount(context, "customer@example.com", Role.Customer);
        var barber = await SeedAccount(context, "barber@example.com", Role.Barber);
        var admin = await SeedAccount(context, "admin@example.com", Role.Admin);
        var appointmentRepository = new AppointmentRepository(context);
        var service = NewService(context);
        var alreadyCancelled = await appointmentRepository.Create(new Appointment
        {
            CustomerId = customer.Id,
            BarberId = barber.Id,
            Date = "2099-01-01",
            StartTime = "09:00",
        });
        await appointmentRepository.TryCancel(alreadyCancelled.Id, DateTime.UtcNow);
        var stillPending = await appointmentRepository.Create(new Appointment
        {
            CustomerId = customer.Id,
            BarberId = barber.Id,
            Date = "2099-01-01",
            StartTime = "10:00",
        });

        await service.CancelAllFutureForBarber(barber.Id, admin.Id, Role.Admin, FixedNow);

        await using var verifyContext = _factory.CreateDbContext();
        var reloaded = await verifyContext.Appointments.FirstOrDefaultAsync(a => a.Id == stillPending.Id, TestContext.Current.CancellationToken);

        Assert.NotNull(reloaded);
        Assert.NotNull(reloaded!.CancelledAt);
    }
}
