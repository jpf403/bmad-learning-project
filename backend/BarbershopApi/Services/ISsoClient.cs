namespace BarbershopApi.Services;

public record SsoIdentity(string Email, string FirstName, string LastName, string SubjectId);

public interface ISsoClient
{
    string BuildAuthorizationUrl(string state);
    Task<SsoIdentity> ExchangeCodeForIdentity(string code);
}
