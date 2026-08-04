namespace BarbershopApi.Dtos;

public class AppointmentView
{
    public int Id { get; set; }
    public int CustomerId { get; set; }
    public string CustomerName { get; set; } = string.Empty;
    public int BarberId { get; set; }
    public string BarberName { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public bool Finished { get; set; }
    public DateTime? CancelledAt { get; set; }
}
