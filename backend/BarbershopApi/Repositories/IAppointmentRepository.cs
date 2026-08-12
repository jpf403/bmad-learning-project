using BarbershopApi.Entities;

namespace BarbershopApi.Repositories;

public interface IAppointmentRepository
{
    Task<Appointment> Create(Appointment appointment);
    Task<Appointment?> FindById(int id);
    Task<List<Appointment>> FindByBarberAndDate(int barberId, string date);
    Task<List<Appointment>> FindUpcomingByCustomer(int customerId, DateTime nowEst);
    Task<List<Appointment>> FindFutureByBarber(int barberId, DateTime nowEst);
    Task<bool> TryCancel(int appointmentId, DateTime cancelledAtUtc);
    Task<bool> ExistsConflict(int barberId, int customerId, string date, string startTime);
}
