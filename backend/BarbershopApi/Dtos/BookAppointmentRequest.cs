using System.ComponentModel.DataAnnotations;

namespace BarbershopApi.Dtos;

public class BookAppointmentRequest
{
    [Required]
    public int BarberId { get; set; }

    [Required]
    [RegularExpression(@"^\d{4}-\d{2}-\d{2}$")]
    [ValidCalendarDate]
    public string Date { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{2}:\d{2}$")]
    [ValidTime]
    public string StartTime { get; set; } = string.Empty;
}
