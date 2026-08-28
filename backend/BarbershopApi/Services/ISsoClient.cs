namespace BarbershopApi.Services;

public record SsoIdentity(string Email, string FirstName, string LastName, string SubjectId, string AccessToken);

public interface ISsoClient
{
    string BuildAuthorizationUrl(string state);
    Task<SsoIdentity> ExchangeCodeForIdentity(string code);
}

public static class SsoRedirects
{
    public const string Failure = "https://localhost:5173/login?error=sso_failed";
}
