using BarbershopApi.Data;
using BarbershopApi.Entities;
using BarbershopApi.Services;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Repositories;

public class AppointmentRepository(BarbershopDbContext context) : IAppointmentRepository
{
    public async Task<Appointment> Create(Appointment appointment)
    {
        context.Appointments.Add(appointment);
        await context.SaveChangesAsync();
        return appointment;
    }

    public async Task<List<Appointment>> FindByBarberAndDate(int barberId, string date)
    {
        return await context.Appointments
            .Where(a => a.BarberId == barberId && a.Date == date && a.CancelledAt == null)
            .ToListAsync();
    }

    public async Task<List<Appointment>> FindUpcomingByCustomer(int customerId, DateTime nowEst)
    {
        var nowDate = nowEst.ToString("yyyy-MM-dd");
        var nowStartTime = nowEst.ToString("HH:mm");

        return await context.Appointments
            .Where(a => a.CustomerId == customerId && a.CancelledAt == null &&
                (a.Date.CompareTo(nowDate) > 0 ||
                 (a.Date == nowDate && a.StartTime.CompareTo(nowStartTime) > 0)))
            .ToListAsync();
    }

    public async Task Cancel(int appointmentId)
    {
        var appointment = await context.Appointments.FirstOrDefaultAsync(a => a.Id == appointmentId);
        if (appointment is null || appointment.CancelledAt is not null)
        {
            throw new AppointmentAlreadyCancelledException();
        }

        appointment.CancelledAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }

    public async Task<bool> ExistsConflict(int barberId, int customerId, string date, string startTime)
    {
        return await context.Appointments.AnyAsync(a =>
            a.CancelledAt == null && a.Date == date && a.StartTime == startTime &&
            (a.BarberId == barberId || a.CustomerId == customerId));
    }
}
