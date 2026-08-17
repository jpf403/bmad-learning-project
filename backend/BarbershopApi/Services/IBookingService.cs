using BarbershopApi.Dtos;
using BarbershopApi.Entities;

namespace BarbershopApi.Services;

public interface IBookingService
{
    Task<Appointment> Create(int customerId, int barberId, string date, string startTime, DateTime? now = null);
    Task<List<AppointmentView>> FindByBarberAndDate(int barberId, string date, DateTime? now = null);
    Task<List<AppointmentView>> FindUpcomingByCustomer(int customerId);
    Task Cancel(int appointmentId, int callerAccountId, Role callerRole, DateTime? now = null);
    Task CancelAllFutureForBarber(int barberId, int callerAccountId, Role callerRole, DateTime? now = null);
    Task CancelAllFutureForCustomer(int customerId, int callerAccountId, Role callerRole, DateTime? now = null);
    Task<List<string>> GetAvailableSlots(int barberId, string date, DateTime? now = null);
    Task<DayScheduleView> GetDaySchedule(int barberId, string? date = null, DateTime? now = null);
}
