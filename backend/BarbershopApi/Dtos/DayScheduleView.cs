namespace BarbershopApi.Dtos;

public class DayScheduleView
{
    public string Date { get; set; } = string.Empty;
    public List<ScheduleSlotView> Slots { get; set; } = [];
}
