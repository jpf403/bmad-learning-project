namespace BarbershopApi.Entities;

public class Account
{
    public int Id { get; set; }
    public string Email { get; set; } = string.Empty;
    public string? PasswordHash { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public Role Role { get; set; }
    public int SessionVersion { get; set; }
    public DateTime? DeletedAt { get; set; }
    public int RowVersion { get; set; }
    public string? SsoProvider { get; set; }
    public string? SsoSubjectId { get; set; }
}
