using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BarbershopApi.Dtos;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;

namespace BarbershopApi.Tests;

public class RoleGatingTests : IDisposable
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

    private async Task<string> RegisterAndLoginAs(HttpClient client, Role role)
    {
        await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);

        await using (var context = _factory.CreateDbContext())
        {
            var repository = new AccountRepository(context);
            var account = await repository.FindByEmail("john@example.com");
            account!.Role = role;
            await repository.Update(account);
        }

        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = "john@example.com", Password = "correct-horse-battery-staple" },
            TestContext.Current.CancellationToken);
        var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(LoginResponseJsonOptions, TestContext.Current.CancellationToken);
        return session!.AccessToken;
    }

    private static HttpRequestMessage AdminOnlyRequest(string? accessToken = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/test-only/admin");
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return request;
    }

    [Fact]
    public async Task AdminOnlyEndpoint_without_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsync(AdminOnlyRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_with_customer_token_returns_403()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLoginAs(client, Role.Customer);

        var response = await client.SendAsync(AdminOnlyRequest(accessToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_with_admin_token_returns_200()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLoginAs(client, Role.Admin);

        var response = await client.SendAsync(AdminOnlyRequest(accessToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_reflects_db_role_change_without_new_login()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLoginAs(client, Role.Customer);

        await using (var context = _factory.CreateDbContext())
        {
            var repository = new AccountRepository(context);
            var account = await repository.FindByEmail("john@example.com");
            account!.Role = Role.Admin;
            await repository.Update(account);
        }

        var response = await client.SendAsync(AdminOnlyRequest(accessToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
