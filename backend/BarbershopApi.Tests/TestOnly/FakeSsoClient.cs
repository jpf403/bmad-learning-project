using BarbershopApi.Services;

namespace BarbershopApi.Tests.TestOnly;

public class FakeSsoClient : ISsoClient
{
    public SsoIdentity NextIdentity { get; set; } = new("john@example.com", "John", "Smith", "1001", "fake-zpax-access-token", "fake-zpax-id-token", "fake-zpax-refresh-token");
    public Exception? ThrowOnExchange { get; set; }
    public SsoRefreshResult NextRefreshResult { get; set; } = new("fake-refreshed-zpax-access-token", null);
    public Exception? ThrowOnRefresh { get; set; }

    public string BuildAuthorizationUrl(string state) =>
        $"https://fake-zpax.test/authorize?state={Uri.EscapeDataString(state)}";

    public Task<SsoIdentity> ExchangeCodeForIdentity(string code)
    {
        if (ThrowOnExchange is not null)
        {
            throw ThrowOnExchange;
        }

        return Task.FromResult(NextIdentity);
    }

    public string BuildLogoutUrl(string idTokenHint) =>
        $"https://fake-zpax.test/logout?id_token_hint={Uri.EscapeDataString(idTokenHint)}";

    public Task<SsoRefreshResult> RefreshAccessToken(string refreshToken)
    {
        if (ThrowOnRefresh is not null)
        {
            throw ThrowOnRefresh;
        }

        return Task.FromResult(NextRefreshResult);
    }
}
