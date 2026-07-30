using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace BarbershopApi.Dtos;

public partial class PlausibleEmailAttribute : ValidationAttribute
{
    public PlausibleEmailAttribute()
    {
        ErrorMessage = "Enter a valid email address.";
    }

    public override bool IsValid(object? value)
    {
        return value is string email && EmailPattern().IsMatch(email.Trim());
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex EmailPattern();
}
