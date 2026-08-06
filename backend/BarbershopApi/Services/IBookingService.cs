using BarbershopApi.Dtos;
using BarbershopApi.Entities;

namespace BarbershopApi.Services;

public interface IBookingService
{
    Task<Appointment> Create(int customerId, int barberId, string date, string startTime);
    Task<List<AppointmentView>> FindByBarberAndDate(int barberId, string date);
    Task<List<AppointmentView>> FindUpcomingByCustomer(int customerId);
    Task Cancel(int appointmentId);
    Task<List<string>> GetAvailableSlots(int barberId, string date);
}
