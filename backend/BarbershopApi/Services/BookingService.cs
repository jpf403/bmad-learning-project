using System.Globalization;
using BarbershopApi.Dtos;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Services;

public class BookingService(IAppointmentRepository appointmentRepository, IAccountRepository accountRepository) : IBookingService
{
    private const int SqliteConstraintViolation = 19;
    private static readonly TimeZoneInfo EasternTimeZone = TimeZoneInfo.FindSystemTimeZoneById("America/New_York");
    private static readonly List<string> FixedSlots = BuildFixedSlots();

    public async Task<Appointment> Create(int customerId, int barberId, string date, string startTime, DateTime? now = null)
    {
        var nowEst = now ?? GetNowEst();
        var appointmentDate = DateOnly.ParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var appointmentTime = TimeOnly.ParseExact(startTime, "HH:mm", CultureInfo.InvariantCulture);
        var appointmentDateTime = appointmentDate.ToDateTime(appointmentTime);

        var isPastOrTooSoon = appointmentDateTime < nowEst.AddMinutes(30);
        var isWeekend = appointmentDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        var isBeyondCap = appointmentDate > DateOnly.FromDateTime(nowEst).AddDays(30);
        if (isPastOrTooSoon || isWeekend || isBeyondCap)
        {
            throw new InvalidBookingWindowException();
        }

        var conflict = await appointmentRepository.ExistsConflict(barberId, customerId, date, startTime);
        if (conflict)
        {
            throw new BookingConflictException();
        }

        var appointment = new Appointment
        {
            CustomerId = customerId,
            BarberId = barberId,
            Date = date,
            StartTime = startTime,
        };

        try
        {
            return await appointmentRepository.Create(appointment);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintViolation })
        {
            throw new BookingConflictException();
        }
    }

    public async Task<List<AppointmentView>> FindByBarberAndDate(int barberId, string date)
    {
        var appointments = await appointmentRepository.FindByBarberAndDate(barberId, date);
        var nowEst = GetNowEst();

        var views = new List<AppointmentView>();
        foreach (var appointment in appointments)
        {
            views.Add(await ToView(appointment, nowEst));
        }
        return views;
    }

    public async Task<List<AppointmentView>> FindUpcomingByCustomer(int customerId)
    {
        var nowEst = GetNowEst();
        var appointments = await appointmentRepository.FindUpcomingByCustomer(customerId, nowEst);

        var views = new List<AppointmentView>();
        foreach (var appointment in appointments)
        {
            views.Add(await ToView(appointment, nowEst));
        }
        return views;
    }

    public async Task<List<string>> GetAvailableSlots(int barberId, string date, DateTime? now = null)
    {
        var booked = await appointmentRepository.FindByBarberAndDate(barberId, date);
        var bookedTimes = booked.Select(a => a.StartTime).ToHashSet();

        var available = FixedSlots.Where(slot => !bookedTimes.Contains(slot)).ToList();

        var nowEst = now ?? GetNowEst();
        if (date == nowEst.ToString("yyyy-MM-dd"))
        {
            var cutoffEst = nowEst.AddMinutes(30);
            if (cutoffEst.ToString("yyyy-MM-dd") != date)
            {
                // The 30-minute cutoff rolled past midnight -- every fixed slot on `date`
                // (09:00-16:30) is necessarily already in the past relative to a cutoff
                // that's now on the following calendar day.
                return [];
            }

            var cutoff = cutoffEst.ToString("HH:mm");
            available = available.Where(slot => string.CompareOrdinal(slot, cutoff) >= 0).ToList();
        }

        return available;
    }

    public async Task Cancel(int appointmentId)
    {
        var appointment = await appointmentRepository.FindById(appointmentId);
        if (appointment is null)
        {
            throw new AppointmentNotFoundException();
        }
        if (appointment.CancelledAt is not null)
        {
            throw new AppointmentAlreadyCancelledException();
        }

        await appointmentRepository.Cancel(appointment);
    }

    private static DateTime GetNowEst() => TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, EasternTimeZone);

    private static List<string> BuildFixedSlots()
    {
        var slots = new List<string>();
        var time = new TimeOnly(9, 0);
        var end = new TimeOnly(16, 30);
        while (time <= end)
        {
            slots.Add(time.ToString("HH:mm"));
            time = time.AddMinutes(30);
        }
        return slots;
    }

    private async Task<AppointmentView> ToView(Appointment appointment, DateTime nowEst)
    {
        var customer = await accountRepository.FindById(appointment.CustomerId);
        var barber = await accountRepository.FindById(appointment.BarberId);

        return new AppointmentView
        {
            Id = appointment.Id,
            CustomerId = appointment.CustomerId,
            CustomerName = FullName(customer),
            BarberId = appointment.BarberId,
            BarberName = FullName(barber),
            Date = appointment.Date,
            StartTime = appointment.StartTime,
            Finished = IsFinished(appointment, nowEst),
            CancelledAt = appointment.CancelledAt,
        };
    }

    // An appointment is Finished the instant Date+StartTime (interpreted in America/New_York)
    // is at or before "now" — there's no EndTime/duration field, so a 9:00 AM appointment is
    // Finished starting at 9:00:00 AM sharp, not after some elapsed duration.
    private static bool IsFinished(Appointment appointment, DateTime nowEst)
    {
        var nowDate = nowEst.ToString("yyyy-MM-dd");
        var nowStartTime = nowEst.ToString("HH:mm");

        return string.CompareOrdinal(appointment.Date, nowDate) < 0 ||
            (appointment.Date == nowDate && string.CompareOrdinal(appointment.StartTime, nowStartTime) <= 0);
    }

    private static string FullName(Account? account) => account is null ? string.Empty : $"{account.FirstName} {account.LastName}";
}
