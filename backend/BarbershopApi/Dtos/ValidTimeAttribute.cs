using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BarbershopApi.Dtos;

public class ValidTimeAttribute : ValidationAttribute
{
    private const string Format = "HH:mm";

    public ValidTimeAttribute()
    {
        ErrorMessage = "Time must be in HH:mm format.";
    }

    public override bool IsValid(object? value)
    {
        return value is string time && IsValidTime(time);
    }

    public static bool IsValidTime(string value) =>
        TimeOnly.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
