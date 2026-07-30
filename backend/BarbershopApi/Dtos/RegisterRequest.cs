using System.ComponentModel.DataAnnotations;

namespace BarbershopApi.Dtos;

public class RegisterRequest
{
    [Required]
    [PlausibleEmail]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;

    [Required]
    public string FirstName { get; set; } = string.Empty;

    [Required]
    public string LastName { get; set; } = string.Empty;
}
