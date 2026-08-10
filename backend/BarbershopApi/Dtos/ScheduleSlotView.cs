namespace BarbershopApi.Dtos;

public class ScheduleSlotView
{
    public string StartTime { get; set; } = string.Empty;
    public AppointmentView? Appointment { get; set; }
}
