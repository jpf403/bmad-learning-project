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
        Assert.Contains("scope=profile", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("redirect_uri=", url);
        // [DEBUG-TEMP] state omitted from the outgoing request while debugging with z-pax --
        // asserted explicitly (not just un-asserted) so a future accidental re-add is caught
        // the same way an accidental removal would be, while this is deferred (see deferred-work.md).
        Assert.DoesNotContain("state=", url);
    }

    [Fact]
    public async Task ExchangeCodeForIdentity_maps_token_and_userinfo_responses_to_SsoIdentity()
    {
        var handler = new FakeHttpMessageHandler(request =>
        {
            if (request.RequestUri!.ToString() == "https://fake-zpax.test/connect/token")
            {
                return JsonResponse(new { access_token = "the-access-token" });
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

    private class FakeHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            responder(request);
    }
}
