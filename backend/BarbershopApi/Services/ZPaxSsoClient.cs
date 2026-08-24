using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace BarbershopApi.Services;

public class ZPaxSsoClient(HttpClient httpClient, IOptions<ZPaxSsoOptions> ssoOptions) : ISsoClient
{
    private const string Scope = "api";
    private const string FallbackUri = "https://localhost:5173/login?error=sso_failed";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public string BuildAuthorizationUrl(string state)
    {
        var options = ssoOptions.Value;
        return QueryHelpers.AddQueryString(options.AuthorizationEndpoint, new Dictionary<string, string?>
        {
            ["client_id"] = options.ClientId,
            ["scope"] = Scope,
            ["response_type"] = "code",
            ["redirect_uri"] = options.RedirectUri,
            ["fallback_uri"] = FallbackUri,
            ["state"] = state,
        });
    }

    public async Task<SsoIdentity> ExchangeCodeForIdentity(string code)
    {
        var options = ssoOptions.Value;

        using var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = options.RedirectUri,
            ["client_id"] = options.ClientId,
            ["client_secret"] = options.ClientSecret,
            ["scope"] = Scope,
        });

        var tokenResponse = await httpClient.PostAsync(options.TokenEndpoint, tokenRequest);
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<ZPaxTokenResponse>(JsonOptions)
            ?? throw new InvalidOperationException("z-pax token endpoint returned an empty response.");

        using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, options.UserInfoEndpoint);
        userInfoRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        userInfoRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var userInfoResponse = await httpClient.SendAsync(userInfoRequest);
        userInfoResponse.EnsureSuccessStatusCode();
        var userInfo = await userInfoResponse.Content.ReadFromJsonAsync<ZPaxUserInfoResponse>(JsonOptions)
            ?? throw new InvalidOperationException("z-pax userinfo endpoint returned an empty response.");

        if (userInfo.Email is null || userInfo.FirstName is null || userInfo.LastName is null)
        {
            throw new InvalidOperationException("z-pax userinfo response is missing a required field.");
        }

        return new SsoIdentity(userInfo.Email, userInfo.FirstName, userInfo.LastName, userInfo.Id.ToString());
    }

    private class ZPaxTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
    }

    private class ZPaxUserInfoResponse
    {
        public int Id { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}
