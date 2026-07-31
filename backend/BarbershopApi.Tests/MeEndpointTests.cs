using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BarbershopApi.Dtos;

namespace BarbershopApi.Tests;

public class MeEndpointTests : IDisposable
{
    private readonly SqliteApiFactory _factory = new();

    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
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

    private static HttpRequestMessage MeRequest(string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return request;
    }

    [Fact]
    public async Task Me_without_access_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsync(MeRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_with_valid_access_token_returns_identity()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "john@example.com", Password = "correct-horse-battery-staple" },
            TestContext.Current.CancellationToken);
        var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(ResponseJsonOptions, TestContext.Current.CancellationToken);

        var response = await client.SendAsync(MeRequest(session!.AccessToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<MeResponse>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(session.Id, body.Id);
        Assert.Equal("john@example.com", body.Email);
        Assert.Equal("John", body.FirstName);
        Assert.Equal("Smith", body.LastName);
        Assert.Equal(session.Role, body.Role);
    }

    [Fact]
    public async Task Me_with_refresh_token_as_bearer_returns_401()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "john@example.com", Password = "correct-horse-battery-staple" },
            TestContext.Current.CancellationToken);

        Assert.True(loginResponse.Headers.TryGetValues("Set-Cookie", out var cookies));
        var refreshCookie = cookies!.Single(c => c.StartsWith("refreshToken="));
        var refreshToken = refreshCookie["refreshToken=".Length..refreshCookie.IndexOf(';')];

        var response = await client.SendAsync(MeRequest(refreshToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_after_logout_returns_401()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "john@example.com", Password = "correct-horse-battery-staple" },
            TestContext.Current.CancellationToken);
        var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(ResponseJsonOptions, TestContext.Current.CancellationToken);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        await client.SendAsync(logoutRequest, TestContext.Current.CancellationToken);

        var response = await client.SendAsync(MeRequest(session.AccessToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_after_logout_returns_problem_details_body()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "john@example.com", Password = "correct-horse-battery-staple" },
            TestContext.Current.CancellationToken);
        var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(ResponseJsonOptions, TestContext.Current.CancellationToken);

        using var logoutRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logoutRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session!.AccessToken);
        await client.SendAsync(logoutRequest, TestContext.Current.CancellationToken);

        var response = await client.SendAsync(MeRequest(session.AccessToken), TestContext.Current.CancellationToken);

        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(401, body.RootElement.GetProperty("status").GetInt32());
        Assert.Equal("Session expired. Please sign in again.", body.RootElement.GetProperty("title").GetString());
    }
}
