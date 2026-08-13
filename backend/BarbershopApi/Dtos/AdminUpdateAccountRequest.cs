using System.ComponentModel.DataAnnotations;
using BarbershopApi.Entities;

namespace BarbershopApi.Dtos;

public class AdminUpdateAccountRequest
{
    [Required] [PlausibleEmail] [StringLength(254)]
    public string Email { get; set; } = string.Empty;

    [Required] [StringLength(100)] [RegularExpression(@"(?s).*\S.*", ErrorMessage = "First name is required.")]
    public string FirstName { get; set; } = string.Empty;

    [Required] [StringLength(100)] [RegularExpression(@"(?s).*\S.*", ErrorMessage = "Last name is required.")]
    public string LastName { get; set; } = string.Empty;

    [Required]
    public Role Role { get; set; }

    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [StringLength(128)]
    [RegularExpression(@"^\S+$", ErrorMessage = "Password cannot contain spaces.")]
    public string? NewPassword { get; set; }
}
