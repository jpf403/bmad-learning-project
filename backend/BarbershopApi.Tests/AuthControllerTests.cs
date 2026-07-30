using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using BarbershopApi.Dtos;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using Microsoft.AspNetCore.Identity;

namespace BarbershopApi.Tests;

public class AuthControllerTests : IDisposable
{
    private readonly SqliteApiFactory _factory = new();

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
    public async Task Register_with_new_email_creates_customer_account()
    {
        using var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/register", NewRequest(), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

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
            .VerifyHashedPassword(account, account.PasswordHash, "the-plaintext-password");
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
}
