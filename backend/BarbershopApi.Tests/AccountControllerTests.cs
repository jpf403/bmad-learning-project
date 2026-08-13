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
using Microsoft.EntityFrameworkCore;
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

    private static HttpRequestMessage SearchRequest(string? query, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, $"/api/account/search?query={Uri.EscapeDataString(query ?? string.Empty)}");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return request;
    }

    private static HttpRequestMessage AdminUpdateRequest(int id, object body, string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Put, $"/api/account/{id}")
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
        var bookingService = new BookingService(new AppointmentRepository(contextB), repositoryB);
        var controller = new AccountController(new AccountService(repositoryB, passwordHasher, bookingService))
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

    [Fact]
    public async Task Search_as_admin_returns_matching_accounts()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync(
            "/api/auth/register",
            NewRegisterRequest(email: "anderson.customer@example.com", firstName: "Anderson", lastName: "Cooper"),
            TestContext.Current.CancellationToken);
        await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Barber, "anderson.barber@example.com");
        await using (var context = _factory.CreateDbContext())
        {
            var repository = new AccountRepository(context);
            var barber = await repository.FindByEmail("anderson.barber@example.com");
            barber!.FirstName = "Andersonia";
            barber.LastName = "Barberton";
            await repository.Update(barber);
        }
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-search@example.com");

        var response = await client.SendAsync(SearchRequest("anderson", adminToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<AccountSummary>>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(2, body.Count);
        Assert.Contains(body, a => a.Email == "anderson.customer@example.com" && a.FirstName == "Anderson" && a.LastName == "Cooper" && a.Role == Role.Customer);
        Assert.Contains(body, a => a.Email == "anderson.barber@example.com" && a.FirstName == "Andersonia" && a.LastName == "Barberton" && a.Role == Role.Barber);
    }

    [Fact]
    public async Task Search_excludes_the_admin_account_from_results()
    {
        using var client = _factory.CreateClient();
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-excluded@example.com");
        await using (var context = _factory.CreateDbContext())
        {
            var repository = new AccountRepository(context);
            var admin = await repository.FindByEmail("admin-excluded@example.com");
            admin!.FirstName = "Excludo";
            admin.LastName = "Adminson";
            await repository.Update(admin);
        }

        var response = await client.SendAsync(SearchRequest("excludo", adminToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<AccountSummary>>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task Search_with_blank_query_returns_empty_array()
    {
        using var client = _factory.CreateClient();
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-blank@example.com");

        var response = await client.SendAsync(SearchRequest("   ", adminToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<AccountSummary>>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task Search_with_no_matches_returns_empty_array()
    {
        using var client = _factory.CreateClient();
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-nomatch@example.com");

        var response = await client.SendAsync(SearchRequest("zzz-no-such-account", adminToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<AccountSummary>>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Theory]
    [InlineData(Role.Customer)]
    [InlineData(Role.Barber)]
    public async Task Search_as_non_admin_returns_403(Role role)
    {
        using var client = _factory.CreateClient();
        var accessToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, role, $"search-{role}@example.com");

        var response = await client.SendAsync(SearchRequest("anything", accessToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Search_without_access_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsync(SearchRequest("anything"), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<int> AccountIdFor(string email)
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        var account = await repository.FindByEmail(email);
        return account!.Id;
    }

    [Fact]
    public async Task AdminUpdate_as_admin_updates_account_and_returns_summary()
    {
        using var client = _factory.CreateClient();
        await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Customer, "target-update@example.com");
        var targetId = await AccountIdFor("target-update@example.com");
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-update@example.com");

        var response = await client.SendAsync(
            AdminUpdateRequest(
                targetId,
                new { Email = "updated-target@example.com", FirstName = "Updated", LastName = "Target", Role = "Barber" },
                adminToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<AccountSummary>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal("updated-target@example.com", body.Email);
        Assert.Equal("Updated", body.FirstName);
        Assert.Equal("Target", body.LastName);
        Assert.Equal(Role.Barber, body.Role);
    }

    [Fact]
    public async Task AdminUpdate_password_change_terminates_target_accounts_existing_session()
    {
        using var client = _factory.CreateClient();
        var targetToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Customer, "target-password@example.com");
        var targetId = await AccountIdFor("target-password@example.com");
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-password@example.com");

        var response = await client.SendAsync(
            AdminUpdateRequest(
                targetId,
                new { Email = "target-password@example.com", FirstName = "John", LastName = "Smith", Role = "Customer", NewPassword = "new-correct-horse-battery" },
                adminToken),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", targetToken);
        var meResponse = await client.SendAsync(meRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, meResponse.StatusCode);
    }

    [Fact]
    public async Task AdminUpdate_permission_only_change_does_not_terminate_target_accounts_session()
    {
        using var client = _factory.CreateClient();
        var targetToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Customer, "target-permission@example.com");
        var targetId = await AccountIdFor("target-permission@example.com");
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-permission@example.com");

        var response = await client.SendAsync(
            AdminUpdateRequest(
                targetId,
                new { Email = "target-permission@example.com", FirstName = "John", LastName = "Smith", Role = "Barber" },
                adminToken),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", targetToken);
        var meResponse = await client.SendAsync(meRequest, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
        var meBody = await meResponse.Content.ReadFromJsonAsync<MeResponse>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.Equal(Role.Barber, meBody!.Role);
    }

    [Fact]
    public async Task AdminUpdate_demoting_barber_to_customer_cancels_future_appointments_via_http()
    {
        using var client = _factory.CreateClient();
        await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Barber, "demote-barber@example.com");
        var barberId = await AccountIdFor("demote-barber@example.com");
        await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Customer, "demote-customer@example.com");
        var customerId = await AccountIdFor("demote-customer@example.com");
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-demote@example.com");

        int futureId;
        int pastId;
        await using (var context = _factory.CreateDbContext())
        {
            var appointmentRepository = new AppointmentRepository(context);
            var future = await appointmentRepository.Create(new Appointment
            {
                CustomerId = customerId,
                BarberId = barberId,
                Date = "2099-01-01",
                StartTime = "09:00",
            });
            var past = await appointmentRepository.Create(new Appointment
            {
                CustomerId = customerId,
                BarberId = barberId,
                Date = "2020-01-01",
                StartTime = "09:00",
            });
            futureId = future.Id;
            pastId = past.Id;
        }

        var response = await client.SendAsync(
            AdminUpdateRequest(
                barberId,
                new { Email = "demote-barber@example.com", FirstName = "John", LastName = "Smith", Role = "Customer" },
                adminToken),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var verifyContext = _factory.CreateDbContext();
        var futureReloaded = await verifyContext.Appointments.FirstOrDefaultAsync(a => a.Id == futureId, TestContext.Current.CancellationToken);
        var pastReloaded = await verifyContext.Appointments.FirstOrDefaultAsync(a => a.Id == pastId, TestContext.Current.CancellationToken);
        Assert.NotNull(futureReloaded);
        Assert.NotNull(futureReloaded!.CancelledAt);
        Assert.NotNull(pastReloaded);
        Assert.Null(pastReloaded!.CancelledAt);

        var accountRepository = new AccountRepository(verifyContext);
        var barberReloaded = await accountRepository.FindById(barberId);
        Assert.Equal(Role.Customer, barberReloaded!.Role);
    }

    [Fact]
    public async Task AdminUpdate_with_duplicate_email_returns_409()
    {
        using var client = _factory.CreateClient();
        await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Customer, "existing@example.com");
        await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Customer, "duplicate-target@example.com");
        var targetId = await AccountIdFor("duplicate-target@example.com");
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-duplicate@example.com");

        var response = await client.SendAsync(
            AdminUpdateRequest(
                targetId,
                new { Email = "existing@example.com", FirstName = "John", LastName = "Smith", Role = "Customer" },
                adminToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("That email is already in use.", body);
    }

    [Fact]
    public async Task AdminUpdate_with_implausible_email_returns_400()
    {
        using var client = _factory.CreateClient();
        await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Customer, "implausible-target@example.com");
        var targetId = await AccountIdFor("implausible-target@example.com");
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-implausible@example.com");

        var response = await client.SendAsync(
            AdminUpdateRequest(
                targetId,
                new { Email = "testbademail", FirstName = "John", LastName = "Smith", Role = "Customer" },
                adminToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminUpdate_with_blank_first_name_returns_400()
    {
        using var client = _factory.CreateClient();
        await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Customer, "blank-name-target@example.com");
        var targetId = await AccountIdFor("blank-name-target@example.com");
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-blankname@example.com");

        var response = await client.SendAsync(
            AdminUpdateRequest(
                targetId,
                new { Email = "blank-name-target@example.com", FirstName = "   ", LastName = "Smith", Role = "Customer" },
                adminToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminUpdate_with_blank_last_name_returns_400()
    {
        using var client = _factory.CreateClient();
        await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Customer, "blank-lastname-target@example.com");
        var targetId = await AccountIdFor("blank-lastname-target@example.com");
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-blanklastname@example.com");

        var response = await client.SendAsync(
            AdminUpdateRequest(
                targetId,
                new { Email = "blank-lastname-target@example.com", FirstName = "John", LastName = "   ", Role = "Customer" },
                adminToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminUpdate_on_missing_account_returns_404()
    {
        using var client = _factory.CreateClient();
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-missing@example.com");

        var response = await client.SendAsync(
            AdminUpdateRequest(
                999999,
                new { Email = "nobody@example.com", FirstName = "John", LastName = "Smith", Role = "Customer" },
                adminToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task AdminUpdate_on_the_admin_account_returns_400()
    {
        using var client = _factory.CreateClient();
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-self@example.com");
        var adminId = await AccountIdFor("admin-self@example.com");

        var response = await client.SendAsync(
            AdminUpdateRequest(
                adminId,
                new { Email = "admin-self@example.com", FirstName = "John", LastName = "Smith", Role = "Customer" },
                adminToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminUpdate_promoting_to_Role_Admin_returns_400()
    {
        using var client = _factory.CreateClient();
        await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Customer, "promote-target@example.com");
        var targetId = await AccountIdFor("promote-target@example.com");
        var adminToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, Role.Admin, "admin-promote@example.com");

        var response = await client.SendAsync(
            AdminUpdateRequest(
                targetId,
                new { Email = "promote-target@example.com", FirstName = "John", LastName = "Smith", Role = "Admin" },
                adminToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AdminUpdate_on_stale_RowVersion_returns_409()
    {
        // Same deterministic two-DbContext pattern as UpdateMe_on_stale_RowVersion_returns_409
        // above (standing practice since Stories 1.2/1.7's flaky-test fixes) -- never a real
        // concurrent-HTTP race.
        await using var contextA = _factory.CreateDbContext();
        var repositoryA = new AccountRepository(contextA);
        var passwordHasher = new PasswordHasher<Account>();
        var target = new Account
        {
            Email = "stale-target@example.com",
            FirstName = "John",
            LastName = "Smith",
            Role = Role.Customer,
        };
        target.PasswordHash = passwordHasher.HashPassword(target, "correct-horse-battery-staple");
        var created = await repositoryA.Create(target);
        var admin = new Account
        {
            Email = "stale-admin@example.com",
            FirstName = "Admina",
            LastName = "Adminson",
            Role = Role.Admin,
        };
        admin.PasswordHash = passwordHasher.HashPassword(admin, "correct-horse-battery-staple");
        var createdAdmin = await repositoryA.Create(admin);

        await using var contextB = _factory.CreateDbContext();
        var repositoryB = new AccountRepository(contextB);
        var staleCopy = await repositoryB.FindById(created.Id);
        Assert.NotNull(staleCopy);

        created.FirstName = "First Edit";
        await repositoryA.Update(created);

        using var scope = _factory.Services.CreateScope();
        var bookingService = new BookingService(new AppointmentRepository(contextB), repositoryB);
        var controller = new AccountController(new AccountService(repositoryB, passwordHasher, bookingService))
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = scope.ServiceProvider },
            },
        };
        controller.HttpContext.Items["Account"] = new Account { Id = createdAdmin.Id, Role = Role.Admin };

        var result = await controller.AdminUpdate(staleCopy!.Id, new AdminUpdateAccountRequest
        {
            Email = staleCopy.Email,
            FirstName = "Second Edit",
            LastName = staleCopy.LastName,
            Role = staleCopy.Role,
        });

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status409Conflict, objectResult.StatusCode);
    }

    [Theory]
    [InlineData(Role.Customer)]
    [InlineData(Role.Barber)]
    public async Task AdminUpdate_as_non_admin_returns_403(Role role)
    {
        using var client = _factory.CreateClient();
        var accessToken = await RoleGatingTests.RegisterAndLoginAs(_factory, client, role, $"admin-update-{role}@example.com");
        var targetId = await AccountIdFor($"admin-update-{role}@example.com");

        var response = await client.SendAsync(
            AdminUpdateRequest(
                targetId,
                new { Email = $"admin-update-{role}@example.com", FirstName = "John", LastName = "Smith", Role = "Customer" },
                accessToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminUpdate_without_access_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsync(
            AdminUpdateRequest(1, new { Email = "nobody@example.com", FirstName = "John", LastName = "Smith", Role = "Customer" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
