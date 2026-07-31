using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BarbershopApi.Dtos;

namespace BarbershopApi.Tests;

public class RefreshEndpointTests : IDisposable
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

    [Fact]
    public async Task Refresh_without_cookie_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/auth/refresh", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_valid_cookie_returns_new_access_token()
    {
        using var client = _factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "john@example.com", Password = "correct-horse-battery-staple" },
            TestContext.Current.CancellationToken);
        var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(ResponseJsonOptions, TestContext.Current.CancellationToken);

        var refreshResponse = await client.PostAsync("/api/auth/refresh", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var body = await refreshResponse.Content.ReadFromJsonAsync<RefreshResponse>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body.AccessToken));
        Assert.NotNull(session);

        using var meRequest = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        meRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);
        var meResponse = await client.SendAsync(meRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, meResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_with_access_token_as_cookie_returns_401()
    {
        using var loginClient = _factory.CreateClient();
        await loginClient.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);
        var loginResponse = await loginClient.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "john@example.com", Password = "correct-horse-battery-staple" },
            TestContext.Current.CancellationToken);
        var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(ResponseJsonOptions, TestContext.Current.CancellationToken);

        using var attackClient = _factory.CreateClient();
        using var refreshRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/refresh");
        refreshRequest.Headers.Add("Cookie", $"refreshToken={session!.AccessToken}");

        var response = await attackClient.SendAsync(refreshRequest, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Refresh_61st_attempt_within_window_returns_429()
    {
        using var client = _factory.CreateClient();

        HttpResponseMessage response = null!;
        for (var attempt = 0; attempt < 61; attempt++)
        {
            response = await client.PostAsync("/api/auth/refresh", null, TestContext.Current.CancellationToken);

            if (attempt < 60)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            }
        }

        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("Too many attempts. Try again in a few minutes.", body);
    }

    [Fact]
    public async Task Refresh_after_logout_returns_401()
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

        var response = await client.PostAsync("/api/auth/refresh", null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
