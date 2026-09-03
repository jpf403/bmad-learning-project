namespace BarbershopApi.Services;

public record SsoIdentity(string Email, string FirstName, string LastName, string SubjectId, string AccessToken, string IdToken);

public interface ISsoClient
{
    string BuildAuthorizationUrl(string state);
    Task<SsoIdentity> ExchangeCodeForIdentity(string code);
    string BuildLogoutUrl(string idTokenHint);
}

public static class SsoRedirects
{
    public const string Failure = "https://localhost:5173/login?error=sso_failed";
    public const string Login = "https://localhost:5173/login";
}
