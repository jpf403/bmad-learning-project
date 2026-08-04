using BarbershopApi.Entities;

namespace BarbershopApi.Repositories;

public interface IAppointmentRepository
{
    Task<Appointment> Create(Appointment appointment);
    Task<Appointment?> FindById(int id);
    Task<List<Appointment>> FindByBarberAndDate(int barberId, string date);
    Task<List<Appointment>> FindUpcomingByCustomer(int customerId, DateTime nowEst);
    Task Cancel(Appointment appointment);
    Task<bool> ExistsConflict(int barberId, int customerId, string date, string startTime);
}
