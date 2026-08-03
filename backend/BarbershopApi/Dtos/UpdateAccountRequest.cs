using System.ComponentModel.DataAnnotations;

namespace BarbershopApi.Dtos;

public class UpdateAccountRequest
{
    [Required]
    [StringLength(100)]
    [RegularExpression(@"(?s).*\S.*", ErrorMessage = "First name is required.")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [RegularExpression(@"(?s).*\S.*", ErrorMessage = "Last name is required.")]
    public string LastName { get; set; } = string.Empty;

    [MinLength(8)]
    [StringLength(128)]
    [RegularExpression(@"^\S+$", ErrorMessage = "Password cannot contain whitespace.")]
    public string? NewPassword { get; set; }
}
