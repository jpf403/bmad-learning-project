using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace BarbershopApi.Dtos;

public class ValidCalendarDateAttribute : ValidationAttribute
{
    private const string Format = "yyyy-MM-dd";

    public ValidCalendarDateAttribute()
    {
        ErrorMessage = "Date must be in yyyy-MM-dd format.";
    }

    public override bool IsValid(object? value)
    {
        return value is string date && IsValidDate(date);
    }

    public static bool IsValidDate(string value) =>
        DateOnly.TryParseExact(value, Format, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
}
