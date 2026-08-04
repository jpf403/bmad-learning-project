using BarbershopApi.Entities;

namespace BarbershopApi.Repositories;

public interface IAppointmentRepository
{
    Task<Appointment> Create(Appointment appointment);
    Task<List<Appointment>> FindByBarberAndDate(int barberId, string date);
    Task<List<Appointment>> FindUpcomingByCustomer(int customerId, DateTime nowEst);
    Task Cancel(int appointmentId);
    Task<bool> ExistsConflict(int barberId, int customerId, string date, string startTime);
}
