using System.ComponentModel.DataAnnotations;

namespace BarbershopApi.Dtos;

public class RegisterRequest
{
    [Required]
    [PlausibleEmail]
    [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [StringLength(128)]
    [RegularExpression(@"^\S+$", ErrorMessage = "Password cannot contain spaces.")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [RegularExpression(@"(?s).*\S.*", ErrorMessage = "This field cannot be blank.")]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    [StringLength(100)]
    [RegularExpression(@"(?s).*\S.*", ErrorMessage = "This field cannot be blank.")]
    public string LastName { get; set; } = string.Empty;
}
