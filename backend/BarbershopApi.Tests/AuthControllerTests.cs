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
}
