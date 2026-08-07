using System.ComponentModel.DataAnnotations;

namespace BarbershopApi.Dtos;

public class BookAppointmentRequest
{
    [Required]
    public int BarberId { get; set; }

    [Required]
    [ValidCalendarDate]
    public string Date { get; set; } = string.Empty;

    [Required]
    [ValidTime]
    public string StartTime { get; set; } = string.Empty;
}
