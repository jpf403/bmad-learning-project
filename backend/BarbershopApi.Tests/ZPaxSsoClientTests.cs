using System.Net;
using System.Text;
using System.Text.Json;
using BarbershopApi.Services;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace BarbershopApi.Tests;

public class ZPaxSsoClientTests
{
    private static ZPaxSsoOptions NewOptions() => new()
    {
        AuthorizationEndpoint = "https://fake-zpax.test/connect/authorize",
        TokenEndpoint = "https://fake-zpax.test/connect/token",
        UserInfoEndpoint = "https://fake-zpax.test/connect/userinfo",
        LogoutEndpoint = "https://fake-zpax.test/connect/logout",
        LogoutRedirectUri = "https://fake-zpax.test/home",
        RedirectUri = "https://localhost:7113/api/auth/sso/callback",
        ClientId = "test-client-id",
        ClientSecret = "test-client-secret",
    };

    private static ZPaxSsoClient NewClient(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), Options.Create(NewOptions()), NullLogger<ZPaxSsoClient>.Instance);

    private static Task<HttpResponseMessage> JsonResponse(object body) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
    {
        Content = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json"),
    });

    [Fact]
    public void BuildAuthorizationUrl_includes_all_required_query_parameters()
    {
        var client = NewClient(new FakeHttpMessageHandler(_ => throw new InvalidOperationException("no HTTP call expected")));

        var url = client.BuildAuthorizationUrl("the-state-value");

        Assert.StartsWith("https://fake-zpax.test/connect/authorize?", url);
        Assert.Contains("client_id=test-client-id", url);
        Assert.Equal("openid profile offline_access", QueryHelpers.ParseQuery(new Uri(url).Query)["scope"].ToString());
        Assert.Contains("response_type=code", url);
        Assert.Contains("redirect_uri=", url);
        Assert.Contains("state=the-state-value", url);
    }

    [Fact]
    public async Task ExchangeCodeForIdentity_maps_token_and_userinfo_responses_to_SsoIdentity()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString() == "https://fake-zpax.test/connect/token")
            {
                return JsonResponse(new { access_token = "the-access-token", id_token = "the-id-token" });
            }

            Assert.Equal("https://fake-zpax.test/connect/userinfo", request.RequestUri!.ToString());
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("the-access-token", request.Headers.Authorization!.Parameter);

            return JsonResponse(new { id = 42, email = "john@example.com", firstName = "John", lastName = "Smith", emailVerified = true });
        });

        var identity = await NewClient(handler).ExchangeCodeForIdentity("the-code");

        Assert.Equal("john@example.com", identity.Email);
        Assert.Equal("John", identity.FirstName);
        Assert.Equal("Smith", identity.LastName);
        Assert.Equal("42", identity.SubjectId);
        Assert.Equal("the-access-token", identity.AccessToken);
        Assert.Equal("the-id-token", identity.IdToken);
    }

    [Fact]
    public async Task ExchangeCodeForIdentity_returns_empty_id_token_when_token_response_omits_it()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString() == "https://fake-zpax.test/connect/token")
            {
                // No "id_token" key at all -- e.g. if the "openid" scope were ever dropped again.
                return JsonResponse(new { access_token = "the-access-token" });
            }

            return JsonResponse(new { id = 1, email = "john@example.com", firstName = "John", lastName = "Smith", emailVerified = true });
        });

        var identity = await NewClient(handler).ExchangeCodeForIdentity("the-code");

        Assert.Equal(string.Empty, identity.IdToken);
    }

    [Fact]
    public void BuildLogoutUrl_targets_the_logout_endpoint_with_id_token_hint_and_post_logout_redirect_uri()
    {
        var client = NewClient(new FakeHttpMessageHandler(_ => throw new InvalidOperationException("no HTTP call expected")));

        var url = client.BuildLogoutUrl("the-id-token");

        Assert.StartsWith("https://fake-zpax.test/connect/logout?", url);
        var query = QueryHelpers.ParseQuery(new Uri(url).Query);
        Assert.Equal("the-id-token", query["id_token_hint"].ToString());
        // Matches ZPaxSsoClient.BuildLogoutUrl's current toggle state (ZPaxSso:LogoutRedirectUri
        // as post_logout_redirect_uri) -- update this alongside that toggle if it's flipped back
        // to the plain fallback (no post_logout_redirect_uri at all).
        Assert.Equal("https://fake-zpax.test/home", query["post_logout_redirect_uri"].ToString());
    }

    [Fact]
    public async Task ExchangeCodeForIdentity_resends_the_same_redirect_uri_used_on_the_authorize_request()
    {
        string? tokenRequestBody = null;
        var client = NewClient(new FakeHttpMessageHandler(async request =>
        {
            if (request.RequestUri!.ToString() == "https://fake-zpax.test/connect/token")
            {
                tokenRequestBody = await request.Content!.ReadAsStringAsync();
                return await JsonResponse(new { access_token = "the-access-token" });
            }

            return await JsonResponse(new { id = 1, email = "a@b.com", firstName = "A", lastName = "B", emailVerified = true });
        }));

        // Derive the expected value from the authorize call itself (not from NewOptions() directly)
        // so this actually catches the two call sites diverging, rather than trivially re-reading
        // the same options instance both the test and the client already share.
        var authorizeUrl = client.BuildAuthorizationUrl("irrelevant-state");
        var expectedRedirectUri = QueryHelpers.ParseQuery(new Uri(authorizeUrl).Query)["redirect_uri"].ToString();

        await client.ExchangeCodeForIdentity("the-code");

        Assert.NotNull(tokenRequestBody);
        Assert.Contains($"redirect_uri={Uri.EscapeDataString(expectedRedirectUri)}", tokenRequestBody);
        // [DEBUG-TEMP] scope omitted from the token request while debugging with z-pax --
        // asserted explicitly (not just un-asserted) so a future accidental re-add is caught
        // the same way an accidental removal would be, while this is deferred (see deferred-work.md).
        Assert.DoesNotContain("scope=", tokenRequestBody);
    }

    [Theory]
    [InlineData(null, "John", "Smith")]
    [InlineData("john@example.com", "", "Smith")]
    [InlineData("john@example.com", "John", "")]
    public async Task ExchangeCodeForIdentity_throws_when_a_required_identity_field_is_missing_or_empty(
        string? email, string firstName, string lastName)
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString() == "https://fake-zpax.test/connect/token")
            {
                return JsonResponse(new { access_token = "the-access-token" });
            }

            return JsonResponse(new { id = 1, email, firstName, lastName, emailVerified = true });
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => NewClient(handler).ExchangeCodeForIdentity("the-code"));
    }

    [Fact]
    public async Task ExchangeCodeForIdentity_throws_when_email_is_not_verified()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString() == "https://fake-zpax.test/connect/token")
            {
                return JsonResponse(new { access_token = "the-access-token" });
            }

            return JsonResponse(new { id = 1, email = "john@example.com", firstName = "John", lastName = "Smith", emailVerified = false });
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => NewClient(handler).ExchangeCodeForIdentity("the-code"));
    }

    [Fact]
    public async Task ExchangeCodeForIdentity_throws_when_token_response_omits_access_token()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString() == "https://fake-zpax.test/connect/token")
            {
                // Raw JSON with no "access_token" key at all -- distinct from an empty string,
                // and the exact shape that would otherwise silently default to string.Empty.
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                });
            }

            throw new InvalidOperationException("userinfo endpoint should never be called when the access token is missing");
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => NewClient(handler).ExchangeCodeForIdentity("the-code"));
    }

    [Fact]
    public async Task ExchangeCodeForIdentity_throws_when_token_endpoint_returns_an_error_status()
    {
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

        await Assert.ThrowsAsync<HttpRequestException>(() => NewClient(handler).ExchangeCodeForIdentity("the-code"));
    }

    [Fact]
    public async Task ExchangeCodeForIdentity_throws_when_userinfo_endpoint_returns_an_error_status()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString() == "https://fake-zpax.test/connect/token")
            {
                return JsonResponse(new { access_token = "the-access-token" });
            }

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Unauthorized));
        });

        await Assert.ThrowsAsync<HttpRequestException>(() => NewClient(handler).ExchangeCodeForIdentity("the-code"));
    }

    [Fact]
    public async Task ExchangeCodeForIdentity_throws_when_id_field_is_omitted_from_userinfo_response()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString() == "https://fake-zpax.test/connect/token")
            {
                return JsonResponse(new { access_token = "the-access-token" });
            }

            // Raw JSON with no "id" key at all -- distinct from `id: null`, and the exact shape
            // that originally slipped past a non-nullable `int Id` defaulting silently to 0.
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    """{"email":"john@example.com","firstName":"John","lastName":"Smith","emailVerified":true}""",
                    Encoding.UTF8, "application/json"),
            });
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => NewClient(handler).ExchangeCodeForIdentity("the-code"));
    }

    [Fact]
    public async Task ExchangeCodeForIdentity_throws_when_account_is_locked()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString() == "https://fake-zpax.test/connect/token")
            {
                return JsonResponse(new { access_token = "the-access-token" });
            }

            return JsonResponse(new { id = 1, email = "john@example.com", firstName = "John", lastName = "Smith", emailVerified = true, isLocked = true });
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => NewClient(handler).ExchangeCodeForIdentity("the-code"));
    }

    [Fact]
    public async Task RefreshAccessToken_posts_grant_type_refresh_token_and_returns_the_new_access_token()
    {
        string? tokenRequestBody = null;
        var handler = new FakeHttpMessageHandler(async request =>
        {
            tokenRequestBody = await request.Content!.ReadAsStringAsync();
            return await JsonResponse(new { access_token = "the-refreshed-access-token", refresh_token = "the-rotated-refresh-token" });
        });

        var result = await NewClient(handler).RefreshAccessToken("the-old-refresh-token");

        Assert.Equal("the-refreshed-access-token", result.AccessToken);
        Assert.Equal("the-rotated-refresh-token", result.RefreshToken);
        Assert.NotNull(tokenRequestBody);
        Assert.Contains("grant_type=refresh_token", tokenRequestBody);
        Assert.Contains("refresh_token=the-old-refresh-token", tokenRequestBody);
    }

    [Fact]
    public async Task RefreshAccessToken_throws_when_zpax_rejects_the_token()
    {
        var handler = new FakeHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.BadRequest)));

        await Assert.ThrowsAsync<HttpRequestException>(() => NewClient(handler).RefreshAccessToken("the-old-refresh-token"));
    }

    private class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request);
    }
}
