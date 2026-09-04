namespace BarbershopApi.Services;

public record SsoIdentity(string Email, string FirstName, string LastName, string SubjectId, string AccessToken, string IdToken, string RefreshToken);

public record SsoRefreshResult(string AccessToken, string? RefreshToken);

public interface ISsoClient
{
    string BuildAuthorizationUrl(string state);
    Task<SsoIdentity> ExchangeCodeForIdentity(string code);
    string BuildLogoutUrl(string idTokenHint);
    Task<SsoRefreshResult> RefreshAccessToken(string refreshToken);
}

public static class SsoRedirects
{
    public const string Failure = "https://localhost:5173/login?error=sso_failed";
    public const string Login = "https://localhost:5173/login";
}
