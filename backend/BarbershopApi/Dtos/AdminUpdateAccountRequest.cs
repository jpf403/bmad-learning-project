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

    [Required] [EnumDataType(typeof(Role))]
    public Role Role { get; set; }

    private string? _newPassword;

    // Blank/omitted means "keep current password" (AC #1). An explicit empty
    // string must be treated identically to a missing field -- normalize here,
    // before [MinLength(8)] runs, rather than rejecting "" as a too-short password.
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters.")]
    [StringLength(128)]
    [RegularExpression(@"^\S+$", ErrorMessage = "Password cannot contain spaces.")]
    public string? NewPassword
    {
        get => _newPassword;
        set => _newPassword = string.IsNullOrEmpty(value) ? null : value;
    }
}
