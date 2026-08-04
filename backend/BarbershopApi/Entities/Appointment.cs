namespace BarbershopApi.Entities;

public class Appointment
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public int BarberId { get; set; }
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public DateTime? CancelledAt { get; set; }
}
