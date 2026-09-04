using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BarbershopApi.Dtos;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using BarbershopApi.Services;
using BarbershopApi.Tests.TestOnly;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;

namespace BarbershopApi.Tests;

public class AuthControllerTests : IDisposable
{
    private readonly SqliteApiFactory _factory = new();

    private static readonly JsonSerializerOptions LoginResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Dispose() => _factory.Dispose();

    private static RegisterRequest NewRequest(
        string email = "john@example.com",
        string password = "correct-horse-battery-staple",
        string firstName = "John",
        string lastName = "Smith") => new()
    {
        Email = email,
        Password = password,
        FirstName = firstName,
        LastName = lastName,
    };

    private static LoginRequest NewLoginRequest(
        string email = "john@example.com",
        string password = "correct-horse-battery-staple") => new()
    {
        Email = email,
        Password = password,
    };

    [Fact]
    public async Task Register_with_new_email_creates_customer_account()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<RegisterResponse>(TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.True(body.Id > 0);
        Assert.Equal("john@example.com", body.Email);
        Assert.Equal("John", body.FirstName);
        Assert.Equal("Smith", body.LastName);

        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var account = await repository.FindByEmail("john@example.com");

        Assert.NotNull(account);
        Assert.Equal(Role.Customer, account.Role);
    }

    [Fact]
    public async Task Register_hashes_password_not_stored_plaintext()
    {
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", NewRequest(password: "the-plaintext-password"), TestContext.Current.CancellationToken);

        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var account = await repository.FindByEmail("john@example.com");

        Assert.NotNull(account);
        Assert.NotEqual("the-plaintext-password", account.PasswordHash);

        var result = new PasswordHasher<Account>()
            .VerifyHashedPassword(account, account.PasswordHash!, "the-plaintext-password");
        Assert.Equal(PasswordVerificationResult.Success, result);
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_409()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_differently_cased_duplicate_email_returns_409()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(email: "john@example.com"), TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(email: "John@Example.com"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_missing_at_sign_returns_400()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(email: "testbademail"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_no_domain_dot_returns_400()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(email: "test@bademail"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_missing_required_field_returns_400()
    {
        using var client = _factory.CreateClient();

        var payload = new { Email = "john@example.com", Password = "the-plaintext-password", LastName = "Smith" };
        var response = await client.PostAsJsonAsync("/api/auth/register", payload, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_400_error_body_uses_PascalCase_field_keys()
    {
        // Confirms the actual JSON key casing of the validation `errors` dictionary
        // (ModelState keys come from the C# property name, not the camelCase JSON
        // naming policy applied to normal response bodies) -- Register.jsx's
        // field-error lookup (Task 6) must key off this exact casing.
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(email: "testbademail"), TestContext.Current.CancellationToken);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var errors = document.RootElement.GetProperty("errors");

        Assert.True(errors.TryGetProperty("Email", out _));
    }

    [Fact]
    public async Task Register_with_leading_or_trailing_whitespace_in_email_succeeds()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(email: "  john@example.com  "), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_short_password_returns_400()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(password: "short1"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_whitespace_in_password_returns_400()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(password: "has a space"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_whitespace_only_first_name_returns_400()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(firstName: "   "), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_overlong_email_returns_400()
    {
        using var client = _factory.CreateClient();

        var overlongEmail = $"{new string('a', 250)}@example.com";
        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(email: overlongEmail), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_overlong_password_returns_400()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(password: new string('a', 129)), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_overlong_first_name_returns_400()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(firstName: new string('a', 101)), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_with_overlong_last_name_returns_400()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(lastName: new string('a', 101)), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_trims_first_and_last_name_before_persisting()
    {
        using var client = _factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/register", NewRequest(firstName: "  John  ", lastName: "  Smith  "), TestContext.Current.CancellationToken);

        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var account = await repository.FindByEmail("john@example.com");

        Assert.NotNull(account);
        Assert.Equal("John", account.FirstName);
        Assert.Equal("Smith", account.LastName);
    }

    [Fact]
    public async Task Login_with_valid_credentials_returns_200_with_access_token_and_sets_refresh_cookie()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync("/api/auth/login", NewLoginRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(LoginResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.Equal("john@example.com", body.Email);

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.StartsWith("refreshToken=") && c.Contains("httponly", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Login_with_unregistered_email_returns_401_generic_message()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", NewLoginRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Invalid email or password.", body);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401_generic_message()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", NewLoginRequest(password: "wrong-password"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Invalid email or password.", body);
    }

    [Fact]
    public async Task Login_unregistered_email_and_wrong_password_produce_identical_response_bodies()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);

        var unregisteredResponse = await client.PostAsJsonAsync(
            "/api/auth/login", NewLoginRequest(email: "unregistered@example.com"), TestContext.Current.CancellationToken);
        var wrongPasswordResponse = await client.PostAsJsonAsync(
            "/api/auth/login", NewLoginRequest(password: "wrong-password"), TestContext.Current.CancellationToken);

        using var unregisteredBody = JsonDocument.Parse(
            await unregisteredResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        using var wrongPasswordBody = JsonDocument.Parse(
            await wrongPasswordResponse.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));

        // Compare status/title only — ProblemDetails also carries a per-request traceId,
        // which legitimately differs between the two calls.
        Assert.Equal(
            wrongPasswordBody.RootElement.GetProperty("status").GetInt32(),
            unregisteredBody.RootElement.GetProperty("status").GetInt32());
        Assert.Equal(
            wrongPasswordBody.RootElement.GetProperty("title").GetString(),
            unregisteredBody.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Login_sixth_attempt_within_window_returns_429_with_rate_limit_message()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);

        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            response = await client.PostAsJsonAsync(
                "/api/auth/login", NewLoginRequest(password: "wrong-password"), TestContext.Current.CancellationToken);

            if (attempt < 5)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Too many attempts. Try again in a few minutes.", body);
    }

    [Fact]
    public async Task Login_still_succeeds_and_still_rate_limits_when_request_carries_a_bearer_token()
    {
        // UseRateLimiter() runs after UseAuthentication()/SessionLivenessMiddleware (needed so
        // PasswordChangePolicy can key off the authenticated caller) -- this proves that
        // reordering didn't couple Login's own behavior to whether the request happens to
        // carry a (here, valid but irrelevant) bearer token, since Login never reads one.
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);
        var firstLogin = await client.PostAsJsonAsync(
            "/api/auth/login", NewLoginRequest(), TestContext.Current.CancellationToken);
        var session = await firstLogin.Content.ReadFromJsonAsync<LoginResponse>(LoginResponseJsonOptions, TestContext.Current.CancellationToken);

        // The initial successful login above already consumed one of the 5 permits.
        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
            {
                Content = JsonContent.Create(NewLoginRequest(password: "wrong-password")),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);

            response = await client.SendAsync(request, TestContext.Current.CancellationToken);

            if (attempt < 4)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }

    [Fact]
    public async Task Logout_with_valid_access_token_returns_204_and_increments_session_version()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", NewLoginRequest(), TestContext.Current.CancellationToken);
        var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(LoginResponseJsonOptions, TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var account = await repository.FindByEmail("john@example.com");
        Assert.NotNull(account);
        Assert.Equal(1, account.SessionVersion);
    }

    [Fact]
    public async Task Login_against_account_with_null_PasswordHash_returns_401_generic_message()
    {
        using var client = _factory.CreateClient();
        await using (var context = _factory.CreateDbContext())
        {
            context.Accounts.Add(new Account
            {
                Email = "sso-only@example.com",
                PasswordHash = null,
                FirstName = "Sso",
                LastName = "Only",
                Role = Role.Customer,
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var response = await client.PostAsJsonAsync(
            "/api/auth/login", NewLoginRequest(email: "sso-only@example.com"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Invalid email or password.", body);
    }

    [Fact]
    public async Task Logout_without_access_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/auth/logout", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_clears_any_pending_zpaxAccessToken_cookie()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", NewLoginRequest(), TestContext.Current.CancellationToken);
        var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(LoginResponseJsonOptions, TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.StartsWith("zpaxAccessToken="));
    }

    [Fact]
    public async Task Logout_clears_any_pending_zpaxIdToken_cookie()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", NewLoginRequest(), TestContext.Current.CancellationToken);
        var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(LoginResponseJsonOptions, TestContext.Current.CancellationToken);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var response = await client.SendAsync(request, TestContext.Current.CancellationToken);

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.StartsWith("zpaxIdToken="));
    }

    private HttpClient NewSsoClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
    });

    private FakeSsoClient FakeSsoClient => _factory.Services.GetRequiredService<FakeSsoClient>();

    private static string ExtractState(HttpResponseMessage ssoLoginResponse) =>
        QueryHelpers.ParseQuery(ssoLoginResponse.Headers.Location!.Query)["state"].ToString();

    [Fact]
    public async Task SsoLogin_redirects_to_authorize_endpoint_with_state_cookie()
    {
        using var client = NewSsoClient();

        var response = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.StartsWith("https://fake-zpax.test/authorize?state=", response.Headers.Location!.ToString());
        Assert.False(string.IsNullOrEmpty(ExtractState(response)));

        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.StartsWith("ssoState=") &&
            c.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("samesite=lax", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("path=/api/auth/sso", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task SsoCallback_with_valid_code_and_state_creates_new_customer_account_and_redirects_to_schedule_appointment()
    {
        using var client = NewSsoClient();
        var loginResponse = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);
        var state = ExtractState(loginResponse);

        var response = await client.GetAsync($"/api/auth/sso/callback?code=anything&state={state}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://localhost:5173/schedule-appointment", response.Headers.Location!.ToString());
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.StartsWith("refreshToken="));
        Assert.Contains(cookies!, c => c.StartsWith($"zpaxAccessToken={FakeSsoClient.NextIdentity.AccessToken}") &&
            c.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("path=/api/auth/sso", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cookies!, c => c.StartsWith($"zpaxIdToken={FakeSsoClient.NextIdentity.IdToken}") &&
            c.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("path=/api/auth/sso", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(cookies!, c => c.StartsWith($"zpaxRefreshToken={FakeSsoClient.NextIdentity.RefreshToken}") &&
            c.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("path=/api/auth/sso", StringComparison.OrdinalIgnoreCase));

        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var account = await repository.FindByEmail(FakeSsoClient.NextIdentity.Email);

        Assert.NotNull(account);
        Assert.Equal(Role.Customer, account.Role);
        Assert.Null(account.PasswordHash);
        Assert.Equal(SsoProviders.ZPax, account.SsoProvider);
    }

    [Fact]
    public async Task SsoCallback_with_valid_code_links_to_existing_barber_account_by_email_preserving_role_and_password()
    {
        const string seededPasswordHash = "seeded-password-hash";
        await using (var context = _factory.CreateDbContext())
        {
            context.Accounts.Add(new Account
            {
                Email = FakeSsoClient.NextIdentity.Email,
                PasswordHash = seededPasswordHash,
                FirstName = "John",
                LastName = "Smith",
                Role = Role.Barber,
            });
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        using var client = NewSsoClient();
        var loginResponse = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);
        var state = ExtractState(loginResponse);

        var response = await client.GetAsync($"/api/auth/sso/callback?code=anything&state={state}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://localhost:5173/my-schedule", response.Headers.Location!.ToString());
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.StartsWith($"zpaxAccessToken={FakeSsoClient.NextIdentity.AccessToken}") &&
            c.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("secure", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase) &&
            c.Contains("path=/api/auth/sso", StringComparison.OrdinalIgnoreCase));

        await using var context2 = _factory.CreateDbContext();
        var repository = new AccountRepository(context2);
        var account = await repository.FindByEmail(FakeSsoClient.NextIdentity.Email);

        Assert.NotNull(account);
        Assert.Equal(Role.Barber, account.Role);
        Assert.Equal(seededPasswordHash, account.PasswordHash);
    }

    [Fact]
    public async Task SsoCallback_missing_state_cookie_redirects_to_login_with_error_and_creates_no_account()
    {
        using var client = NewSsoClient();

        var response = await client.GetAsync("/api/auth/sso/callback?code=x&state=y", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://localhost:5173/login?error=sso_failed", response.Headers.Location!.ToString());

        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        Assert.Null(await repository.FindByEmail(FakeSsoClient.NextIdentity.Email));
    }

    [Fact]
    public async Task SsoCallback_mismatched_state_redirects_to_login_with_error_and_creates_no_account()
    {
        using var client = NewSsoClient();
        await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);

        var response = await client.GetAsync("/api/auth/sso/callback?code=x&state=a-different-state", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://localhost:5173/login?error=sso_failed", response.Headers.Location!.ToString());

        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        Assert.Null(await repository.FindByEmail(FakeSsoClient.NextIdentity.Email));
    }

    [Fact]
    public async Task SsoCallback_missing_code_redirects_to_login_with_error()
    {
        using var client = NewSsoClient();
        var loginResponse = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);
        var state = ExtractState(loginResponse);

        var response = await client.GetAsync($"/api/auth/sso/callback?state={state}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://localhost:5173/login?error=sso_failed", response.Headers.Location!.ToString());

        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        Assert.Null(await repository.FindByEmail(FakeSsoClient.NextIdentity.Email));
    }

    [Fact]
    public async Task SsoCallback_with_no_code_and_no_active_login_attempt_redirects_to_login_without_error()
    {
        using var client = NewSsoClient();

        // No prior call to /api/auth/sso/login -- no ssoState cookie in flight, so
        // this isn't a failed sign-in, it's presumed to be z-pax's own /connect/logout
        // landing here via post_logout_redirect_uri after ending the SSO session.
        var response = await client.GetAsync("/api/auth/sso/callback", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://localhost:5173/login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task SsoCallback_state_already_consumed_by_a_concurrent_request_redirects_to_login_with_error()
    {
        using var client = NewSsoClient();
        var loginResponse = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);
        var state = ExtractState(loginResponse);

        // Simulate a concurrent request that already consumed this state server-side,
        // ahead of this attempt's cookie-deletion response ever reaching the browser.
        var stateStore = _factory.Services.GetRequiredService<ISsoStateStore>();
        Assert.True(stateStore.TryConsume(state));

        var response = await client.GetAsync($"/api/auth/sso/callback?code=anything&state={state}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://localhost:5173/login?error=sso_failed", response.Headers.Location!.ToString());

        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        Assert.Null(await repository.FindByEmail(FakeSsoClient.NextIdentity.Email));
    }

    [Fact]
    public async Task SsoCallback_when_ssoClient_throws_redirects_to_login_with_error_and_creates_no_account()
    {
        FakeSsoClient.ThrowOnExchange = new InvalidOperationException("z-pax is unavailable");

        using var client = NewSsoClient();
        var loginResponse = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);
        var state = ExtractState(loginResponse);

        var response = await client.GetAsync($"/api/auth/sso/callback?code=anything&state={state}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://localhost:5173/login?error=sso_failed", response.Headers.Location!.ToString());

        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        Assert.Null(await repository.FindByEmail(FakeSsoClient.NextIdentity.Email));
    }

    [Fact]
    public async Task SsoCallback_reuses_ssoState_cookie_only_once()
    {
        using var client = NewSsoClient();
        var loginResponse = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);
        var state = ExtractState(loginResponse);

        var firstAttempt = await client.GetAsync($"/api/auth/sso/callback?code=anything&state={state}", TestContext.Current.CancellationToken);
        Assert.Equal("https://localhost:5173/schedule-appointment", firstAttempt.Headers.Location!.ToString());

        var secondAttempt = await client.GetAsync($"/api/auth/sso/callback?code=anything&state={state}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, secondAttempt.StatusCode);
        Assert.Equal("https://localhost:5173/login?error=sso_failed", secondAttempt.Headers.Location!.ToString());

        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var account = await repository.FindByEmail(FakeSsoClient.NextIdentity.Email);
        Assert.NotNull(account);
    }

    [Fact]
    public async Task ZpaxToken_right_after_SsoCallback_returns_200_with_token_and_consumes_cookie()
    {
        using var client = NewSsoClient();
        var loginResponse = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);
        var state = ExtractState(loginResponse);
        await client.GetAsync($"/api/auth/sso/callback?code=anything&state={state}", TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var identity = FakeSsoClient.NextIdentity;
        var (_, accessToken, _) = await authService.LoginViaSso(identity.Email, identity.FirstName, identity.LastName, identity.SubjectId);

        using var zpaxRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sso/zpax-token");
        zpaxRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(zpaxRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ZpaxTokenResponse>(LoginResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(identity.AccessToken, body.ZpaxAccessToken);
    }

    [Fact]
    public async Task ZpaxToken_second_call_after_first_consumption_returns_404()
    {
        using var client = NewSsoClient();
        var loginResponse = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);
        var state = ExtractState(loginResponse);
        await client.GetAsync($"/api/auth/sso/callback?code=anything&state={state}", TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var identity = FakeSsoClient.NextIdentity;
        var (_, accessToken, _) = await authService.LoginViaSso(identity.Email, identity.FirstName, identity.LastName, identity.SubjectId);

        using var firstRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sso/zpax-token");
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        await client.SendAsync(firstRequest, TestContext.Current.CancellationToken);

        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sso/zpax-token");
        secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var response = await client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ZpaxToken_from_a_password_only_session_with_no_pending_cookie_returns_404()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", NewLoginRequest(), TestContext.Current.CancellationToken);
        var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(LoginResponseJsonOptions, TestContext.Current.CancellationToken);

        using var zpaxRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sso/zpax-token");
        zpaxRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var response = await client.SendAsync(zpaxRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ZpaxToken_without_bearer_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/sso/zpax-token", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ZpaxRefresh_with_a_valid_cookie_returns_200_with_the_new_access_token()
    {
        using var client = NewSsoClient();
        var loginResponse = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);
        var state = ExtractState(loginResponse);
        await client.GetAsync($"/api/auth/sso/callback?code=anything&state={state}", TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var identity = FakeSsoClient.NextIdentity;
        var (_, bearerToken, _) = await authService.LoginViaSso(identity.Email, identity.FirstName, identity.LastName, identity.SubjectId);

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sso/zpax-refresh");
        refreshRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        var response = await client.SendAsync(refreshRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ZpaxTokenResponse>(LoginResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(FakeSsoClient.NextRefreshResult.AccessToken, body.ZpaxAccessToken);
    }

    [Fact]
    public async Task ZpaxRefresh_with_no_pending_cookie_returns_404()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);
        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", NewLoginRequest(), TestContext.Current.CancellationToken);
        var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(LoginResponseJsonOptions, TestContext.Current.CancellationToken);

        using var refreshRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sso/zpax-refresh");
        refreshRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        var response = await client.SendAsync(refreshRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ZpaxRefresh_when_zpax_rejects_the_refresh_token_returns_404_and_clears_the_cookie()
    {
        using var client = NewSsoClient();
        var loginResponse = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);
        var state = ExtractState(loginResponse);
        await client.GetAsync($"/api/auth/sso/callback?code=anything&state={state}", TestContext.Current.CancellationToken);

        using var scope = _factory.Services.CreateScope();
        var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
        var identity = FakeSsoClient.NextIdentity;
        var (_, bearerToken, _) = await authService.LoginViaSso(identity.Email, identity.FirstName, identity.LastName, identity.SubjectId);

        FakeSsoClient.ThrowOnRefresh = new InvalidOperationException("z-pax rejected the refresh token");

        using var firstRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sso/zpax-refresh");
        firstRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        var firstResponse = await client.SendAsync(firstRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, firstResponse.StatusCode);

        using var secondRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/sso/zpax-refresh");
        secondRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        var secondResponse = await client.SendAsync(secondRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, secondResponse.StatusCode);
    }

    [Fact]
    public async Task ZpaxRefresh_without_bearer_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/auth/sso/zpax-refresh", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SsoLogout_after_an_sso_session_redirects_to_the_zpax_logout_endpoint_and_clears_the_cookie()
    {
        using var client = NewSsoClient();
        var loginResponse = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);
        var state = ExtractState(loginResponse);
        await client.GetAsync($"/api/auth/sso/callback?code=anything&state={state}", TestContext.Current.CancellationToken);

        var response = await client.GetAsync("/api/auth/sso/logout", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal(
            $"https://fake-zpax.test/logout?id_token_hint={FakeSsoClient.NextIdentity.IdToken}",
            response.Headers.Location!.ToString());
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        Assert.Contains(cookies!, c => c.StartsWith("zpaxIdToken="));
    }

    [Fact]
    public async Task SsoLogout_with_no_pending_zpaxIdToken_cookie_redirects_to_login()
    {
        using var client = NewSsoClient();

        var response = await client.GetAsync("/api/auth/sso/logout", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://localhost:5173/login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task SsoCallback_clears_a_stale_zpaxIdToken_cookie_when_a_later_login_has_no_id_token()
    {
        using var client = NewSsoClient();
        var firstLogin = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);
        var firstState = ExtractState(firstLogin);
        await client.GetAsync($"/api/auth/sso/callback?code=anything&state={firstState}", TestContext.Current.CancellationToken);

        var originalIdentity = FakeSsoClient.NextIdentity;
        FakeSsoClient.NextIdentity = originalIdentity with { IdToken = string.Empty };
        try
        {
            var secondLogin = await client.GetAsync("/api/auth/sso/login", TestContext.Current.CancellationToken);
            var secondState = ExtractState(secondLogin);
            var response = await client.GetAsync($"/api/auth/sso/callback?code=anything&state={secondState}", TestContext.Current.CancellationToken);

            Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
            Assert.Contains(cookies!, c => c.StartsWith("zpaxIdToken=") &&
                c.Contains("expires=", StringComparison.OrdinalIgnoreCase));
        }
        finally
        {
            FakeSsoClient.NextIdentity = originalIdentity;
        }
    }
}
