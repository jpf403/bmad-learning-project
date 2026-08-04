using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BarbershopApi.Controllers;
using BarbershopApi.Dtos;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using BarbershopApi.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace BarbershopApi.Tests;

public class AccountControllerTests : IDisposable
{
    private readonly SqliteApiFactory _factory = new();

    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Dispose() => _factory.Dispose();

    private static RegisterRequest NewRegisterRequest(
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

    private static HttpRequestMessage UpdateMeRequest(object body, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/account/me")
        {
            Content = JsonContent.Create(body),
        };
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return request;
    }

    private async Task<string> RegisterAndLogin(HttpClient client, string email = "john@example.com", string password = "correct-horse-battery-staple")
    {
        await client.PostAsJsonAsync("/api/auth/register", NewRegisterRequest(email: email, password: password), TestContext.Current.CancellationToken);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = password },
            TestContext.Current.CancellationToken);
        var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        return session!.AccessToken;
    }

    [Fact]
    public async Task UpdateMe_without_access_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsync(
            UpdateMeRequest(new { FirstName = "John", LastName = "Smith" }), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_updates_profile_and_returns_MeResponse()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            UpdateMeRequest(new { FirstName = "Updated", LastName = "Name" }, accessToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeResponse>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal("Updated", body.FirstName);
        Assert.Equal("Name", body.LastName);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var meResponse = await client.SendAsync(meRequest, TestContext.Current.CancellationToken);
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal("Updated", meBody!.FirstName);
        Assert.Equal("Name", meBody.LastName);
    }

    [Fact]
    public async Task UpdateMe_with_new_password_allows_login_with_new_password_and_rejects_old()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            UpdateMeRequest(
                new { FirstName = "John", LastName = "Smith", NewPassword = "new-correct-horse-battery", CurrentPassword = "correct-horse-battery-staple" },
                accessToken),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var oldPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "john@example.com", Password = "correct-horse-battery-staple" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        var newPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "john@example.com", Password = "new-correct-horse-battery" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        var meResponse = await client.SendAsync(meRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_with_blank_first_name_returns_400_with_PascalCase_error_key()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            UpdateMeRequest(new { FirstName = "   ", LastName = "Smith" }, accessToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        var errors = document.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("FirstName", out _));
    }

    [Fact]
    public async Task UpdateMe_with_short_new_password_returns_400()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            UpdateMeRequest(
                new { FirstName = "John", LastName = "Smith", NewPassword = "short1", CurrentPassword = "correct-horse-battery-staple" },
                accessToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_with_new_password_and_missing_current_password_returns_400_and_does_not_change_password()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            UpdateMeRequest(new { FirstName = "John", LastName = "Smith", NewPassword = "new-correct-horse-battery" }, accessToken),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var oldPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "john@example.com", Password = "correct-horse-battery-staple" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, oldPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_with_new_password_and_wrong_current_password_returns_400_and_does_not_change_password()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            UpdateMeRequest(
                new { FirstName = "John", LastName = "Smith", NewPassword = "new-correct-horse-battery", CurrentPassword = "wrong-password" },
                accessToken),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var oldPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "john@example.com", Password = "correct-horse-battery-staple" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, oldPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_with_new_password_same_as_current_returns_400_and_does_not_change_password()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            UpdateMeRequest(
                new { FirstName = "John", LastName = "Smith", NewPassword = "correct-horse-battery-staple", CurrentPassword = "correct-horse-battery-staple" },
                accessToken),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        var oldPasswordLogin = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "john@example.com", Password = "correct-horse-battery-staple" },
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, oldPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_on_stale_RowVersion_returns_409()
    {
        // Reproduces the same optimistic-concurrency race as
        // AccountServiceTests.UpdateOwnProfile_on_stale_RowVersion_throws_AccountConflictException,
        // but calls the real AccountController.UpdateMe directly (bypassing routing,
        // [Authorize], [EnableRateLimiting], and SessionLivenessMiddleware -- those are
        // covered by other tests) to prove the concurrency mechanism AND the controller's
        // AccountConflictException -> 409 mapping together. A prior version of this test
        // fired two real concurrent HTTP requests hoping they'd race on the DB row -- whether
        // their read/write cycles actually overlap depends entirely on ASP.NET Core/thread-pool
        // scheduling, not anything the test controls, so it was intermittently flaky. This
        // version forces the exact same staleness deterministically via two separate
        // DbContexts, with no mocking -- real repository, service, and controller classes.
        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AccountRepository(contextA);
        var passwordHasher = new PasswordHasher<Account>();
        var account = new Account
        {
            Email = "john@example.com",
            FirstName = "John",
            LastName = "Smith",
            Role = Role.Customer,
        };
        account.PasswordHash = passwordHasher.HashPassword(account, "correct-horse-battery-staple");
        var created = await repositoryA.Create(account);

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AccountRepository(contextB);
        var staleCopy = await repositoryB.FindById(created.Id);
        Assert.NotNull(staleCopy);

        created.FirstName = "First Edit";
        await repositoryA.Update(created);

        using var scope = _factory.Services.CreateScope();
        var controller = new AccountController(new AccountService(repositoryB, passwordHasher))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider },
            },
        };
        controller.HttpContext.Items["Account"] = staleCopy;

        var result = await controller.UpdateMe(new UpdateAccountRequest { FirstName = "Second Edit", LastName = "Smith" });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_sixth_password_change_attempt_within_window_returns_429_with_rate_limit_message()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            response = await client.SendAsync(
                UpdateMeRequest(
                    new { FirstName = "John", LastName = "Smith", NewPassword = "new-correct-horse-battery", CurrentPassword = "wrong-password" },
                    accessToken),
                TestContext.Current.CancellationToken);

            if (attempt < 5)
            {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Too many attempts. Try again in a few minutes.", body);
    }

    [Fact]
    public async Task UpdateMe_name_only_edits_are_not_rate_limited_by_password_change_policy()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        for (var attempt = 0; attempt < 6; attempt++)
        {
            var response = await client.SendAsync(
                UpdateMeRequest(new { FirstName = $"Edit {attempt}", LastName = "Smith" }, accessToken),
                TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task UpdateMe_rate_limit_is_scoped_per_account_not_shared_across_accounts()
    {
        using var client = _factory.CreateClient();
        var accessTokenA = await RegisterAndLogin(client, email: "accounta@example.com");
        var accessTokenB = await RegisterAndLogin(client, email: "accountb@example.com");

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await client.SendAsync(
                UpdateMeRequest(
                    new { FirstName = "John", LastName = "Smith", NewPassword = "new-correct-horse-battery", CurrentPassword = "wrong-password" },
                    accessTokenA),
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var lockedOut = await client.SendAsync(
            UpdateMeRequest(
                new { FirstName = "John", LastName = "Smith", NewPassword = "new-correct-horse-battery", CurrentPassword = "wrong-password" },
                accessTokenA),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.TooManyRequests, lockedOut.StatusCode);

        var stillAllowed = await client.SendAsync(
            UpdateMeRequest(
                new { FirstName = "John", LastName = "Smith", NewPassword = "new-correct-horse-battery", CurrentPassword = "wrong-password" },
                accessTokenB),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.BadRequest, stillAllowed.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_successful_password_changes_also_count_toward_the_rate_limit()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var currentPassword = "correct-horse-battery-staple";
        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt < 6; attempt++)
        {
            var newPassword = $"new-correct-horse-battery-{attempt}";
            response = await client.SendAsync(
                UpdateMeRequest(
                    new { FirstName = "John", LastName = "Smith", NewPassword = newPassword, CurrentPassword = currentPassword },
                    accessToken),
                TestContext.Current.CancellationToken);

            if (attempt < 5)
            {
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
                currentPassword = newPassword;
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
    }
}
