using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Options;

namespace BarbershopApi.Services;

public class ZPaxSsoClient(HttpClient httpClient, IOptions<ZPaxSsoOptions> ssoOptions, ILogger<ZPaxSsoClient> logger) : ISsoClient
{
    private const string Scope = "openid profile";

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
            // [DEBUG-TEMP] scope omitted from the token request while debugging with z-pax
        });

        var tokenResponse = await httpClient.PostAsync(options.TokenEndpoint, tokenRequest);
        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync();
            logger.LogWarning("z-pax token endpoint returned {StatusCode}: {Body}", (int)tokenResponse.StatusCode, errorBody);
        }
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<ZPaxTokenResponse>(JsonOptions)
            ?? throw new InvalidOperationException("z-pax token endpoint returned an empty response.");

        if (string.IsNullOrWhiteSpace(token.AccessToken))
        {
            logger.LogWarning("z-pax token endpoint response is missing an access token.");
            throw new InvalidOperationException("z-pax token endpoint response is missing an access token.");
        }

        using var userInfoRequest = new HttpRequestMessage(HttpMethod.Get, options.UserInfoEndpoint);
        userInfoRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        userInfoRequest.Headers.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));

        var userInfoResponse = await httpClient.SendAsync(userInfoRequest);
        if (!userInfoResponse.IsSuccessStatusCode)
        {
            var errorBody = await userInfoResponse.Content.ReadAsStringAsync();
            logger.LogWarning("z-pax userinfo endpoint returned {StatusCode}: {Body}", (int)userInfoResponse.StatusCode, errorBody);
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

        if (string.IsNullOrEmpty(token.IdToken))
        {
            logger.LogWarning("z-pax token endpoint response is missing an id_token.");
        }

        return new SsoIdentity(userInfo.Email, userInfo.FirstName, userInfo.LastName, userInfo.Id.Value.ToString(), token.AccessToken, token.IdToken ?? string.Empty);
    }

    public string BuildLogoutUrl(string idTokenHint)
    {
        var options = ssoOptions.Value;
        // ZPaxSso:LogoutRedirectUri is registered with z-pax as this app's logout
        // redirect and live-verified end-to-end (2026-09-02): the visitor's z-pax
        // session ends and the browser lands cleanly on that page, no error page.
        return QueryHelpers.AddQueryString(options.LogoutEndpoint, new Dictionary<string, string?>
        {
            ["id_token_hint"] = idTokenHint,
            ["post_logout_redirect_uri"] = options.LogoutRedirectUri,
        });
    }

    private class ZPaxTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        [JsonPropertyName("id_token")]
        public string? IdToken { get; set; }
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
