using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BarbershopApi.Dtos;

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
            UpdateMeRequest(new { FirstName = "John", LastName = "Smith", NewPassword = "new-correct-horse-battery" }, accessToken),
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
            UpdateMeRequest(new { FirstName = "John", LastName = "Smith", NewPassword = "short1" }, accessToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task UpdateMe_two_concurrent_edits_to_same_account_one_succeeds_one_returns_409()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var firstEdit = client.SendAsync(
            UpdateMeRequest(new { FirstName = "First Edit", LastName = "Smith" }, accessToken), TestContext.Current.CancellationToken);
        var secondEdit = client.SendAsync(
            UpdateMeRequest(new { FirstName = "Second Edit", LastName = "Smith" }, accessToken), TestContext.Current.CancellationToken);

        var responses = await Task.WhenAll(firstEdit, secondEdit);

        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.OK);
        Assert.Contains(responses, r => r.StatusCode == HttpStatusCode.Conflict);
    }
}
