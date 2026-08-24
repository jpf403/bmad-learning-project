using System.Text.Json;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace BarbershopApi.Services;

public class ZPaxSsoClient(HttpClient httpClient, IOptions<ZPaxSsoOptions> ssoOptions, ILogger<ZPaxSsoClient> logger) : ISsoClient
{
    private const string Scope = "api";

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
            ["fallback_uri"] = SsoRedirects.Failure,
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
        if (!tokenResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("z-pax token endpoint returned {StatusCode}.", (int)tokenResponse.StatusCode);
        }
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<ZPaxTokenResponse>(JsonOptions)
            ?? throw new InvalidOperationException("z-pax token endpoint returned an empty response.");

        using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, options.UserInfoEndpoint);
        userInfoRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        userInfoRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var userInfoResponse = await httpClient.SendAsync(userInfoRequest);
        if (!userInfoResponse.IsSuccessStatusCode)
        {
            logger.LogWarning("z-pax userinfo endpoint returned {StatusCode}.", (int)userInfoResponse.StatusCode);
        }
        userInfoResponse.EnsureSuccessStatusCode();
        var userInfo = await userInfoResponse.Content.ReadFromJsonAsync<ZPaxUserInfoResponse>(JsonOptions)
            ?? throw new InvalidOperationException("z-pax userinfo endpoint returned an empty response.");

        if (userInfo.Id is null ||
            string.IsNullOrWhiteSpace(userInfo.Email) ||
            string.IsNullOrWhiteSpace(userInfo.FirstName) ||
            string.IsNullOrWhiteSpace(userInfo.LastName))
        {
            logger.LogWarning("z-pax userinfo response is missing a required field.");
            throw new InvalidOperationException("z-pax userinfo response is missing a required field.");
        }

        if (!userInfo.EmailVerified)
        {
            logger.LogWarning("z-pax account email is not verified for subject id {SubjectId}.", userInfo.Id);
            throw new InvalidOperationException("z-pax account email is not verified.");
        }

        if (userInfo.IsLocked)
        {
            logger.LogWarning("z-pax account is locked for subject id {SubjectId}.", userInfo.Id);
            throw new InvalidOperationException("z-pax account is locked.");
        }

        return new SsoIdentity(userInfo.Email, userInfo.FirstName, userInfo.LastName, userInfo.Id.Value.ToString());
    }

    private class ZPaxTokenResponse
    {
        public string AccessToken { get; set; } = string.Empty;
    }

    private class ZPaxUserInfoResponse
    {
        public int? Id { get; set; }
        public string? Email { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public bool EmailVerified { get; set; }
        public bool IsLocked { get; set; }
    }
}
