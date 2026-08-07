using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using BarbershopApi.Dtos;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;

namespace BarbershopApi.Tests;

public class BookingControllerTests : IDisposable
{
    private readonly SqliteApiFactory _factory = new();

    private static readonly JsonSerializerOptions ResponseJsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public void Dispose() => _factory.Dispose();

    private async Task<string> RegisterAndLogin(HttpClient client, string email = "customer@example.com", string password = "correct-horse-battery-staple")
    {
        await client.PostAsJsonAsync(
            "/api/auth/register",
            new RegisterRequest { Email = email, Password = password, FirstName = "John", LastName = "Smith" },
            TestContext.Current.CancellationToken);
        var loginResponse = await client.PostAsJsonAsync(
            "/api/auth/login",
            new LoginRequest { Email = email, Password = password },
            TestContext.Current.CancellationToken);
        var session = await loginResponse.Content.ReadFromJsonAsync<LoginResponse>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        return session!.AccessToken;
    }

    private async Task<Account> SeedAccount(string email, Role role, string firstName = "John", string lastName = "Smith")
    {
        await using var context = _factory.CreateDbContext();
        var repository = new AccountRepository(context);
        return await repository.Create(new Account
        {
            Email = email,
            PasswordHash = "hashed-password",
            FirstName = firstName,
            LastName = lastName,
            Role = role,
        });
    }

    private static HttpRequestMessage AuthedRequest(HttpMethod method, string url, string? accessToken)
    {
        var request = new HttpRequestMessage(method, url);
        if (accessToken is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }
        return request;
    }

    private static string NextBookableWeekday(int minDaysAhead = 1)
    {
        var date = DateTime.Today.AddDays(minDaysAhead);
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }
        return date.ToString("yyyy-MM-dd");
    }

    private static string NextWeekendDate()
    {
        var date = DateTime.Today.AddDays(1);
        while (date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
        {
            date = date.AddDays(1);
        }
        return date.ToString("yyyy-MM-dd");
    }

    [Fact]
    public async Task GetBarbers_returns_empty_list_when_none_exist()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/booking/barbers", accessToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<BarberSummary>>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task GetBarbers_returns_seeded_barbers_only_not_customers()
    {
        await SeedAccount("barber@example.com", Role.Barber, firstName: "Amy", lastName: "Barber");
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/booking/barbers", accessToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<BarberSummary>>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        var barber = Assert.Single(body);
        Assert.Equal("Amy", barber.FirstName);
        Assert.Equal("Barber", barber.LastName);
    }

    [Fact]
    public async Task GetAvailability_excludes_already_booked_slot()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);
        var date = NextBookableWeekday();

        var bookResponse = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessToken)
                .WithJsonBody(new { BarberId = barber.Id, Date = date, StartTime = "09:30" }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, bookResponse.StatusCode);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Get, $"/api/booking/availability?barberId={barber.Id}&date={date}", accessToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var slots = await response.Content.ReadFromJsonAsync<List<string>>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(slots);
        Assert.DoesNotContain("09:30", slots);
    }

    [Fact]
    public async Task GetAvailability_with_nonexistent_barberId_returns_400()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/booking/availability?barberId=999999&date=2026-09-01", accessToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailability_with_malformed_date_returns_400()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Get, $"/api/booking/availability?barberId={barber.Id}&date=09-01-2026", accessToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailability_with_oversized_year_in_date_returns_400()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Get, $"/api/booking/availability?barberId={barber.Id}&date=12026-01-01", accessToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailability_with_missing_date_returns_400()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Get, $"/api/booking/availability?barberId={barber.Id}", accessToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetAvailability_with_nonexistent_calendar_date_returns_400()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Get, $"/api/booking/availability?barberId={barber.Id}&date=2026-02-31", accessToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_without_access_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", null)
                .WithJsonBody(new { BarberId = 1, Date = "2026-09-01", StartTime = "09:00" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_with_valid_request_returns_201_with_BookingConfirmation()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber, firstName: "Amy", lastName: "Barber");
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);
        var date = NextBookableWeekday();

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessToken)
                .WithJsonBody(new { BarberId = barber.Id, Date = date, StartTime = "09:00" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<BookingConfirmation>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal("Amy Barber", body.BarberName);
        Assert.Equal(date, body.Date);
        Assert.Equal("09:00", body.StartTime);
    }

    [Fact]
    public async Task CreateBooking_with_nonexistent_barberId_returns_400()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessToken)
                .WithJsonBody(new { BarberId = 999999, Date = NextBookableWeekday(), StartTime = "09:00" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_second_request_for_same_slot_returns_409()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessTokenA = await RegisterAndLogin(client, email: "customerA@example.com");
        var accessTokenB = await RegisterAndLogin(client, email: "customerB@example.com");
        var date = NextBookableWeekday();

        var firstResponse = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessTokenA)
                .WithJsonBody(new { BarberId = barber.Id, Date = date, StartTime = "09:00" }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessTokenB)
                .WithJsonBody(new { BarberId = barber.Id, Date = date, StartTime = "09:00" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_when_customer_already_holds_a_different_barber_at_the_same_time_returns_409()
    {
        var barberA = await SeedAccount("barberA@example.com", Role.Barber);
        var barberB = await SeedAccount("barberB@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);
        var date = NextBookableWeekday();

        var firstResponse = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessToken)
                .WithJsonBody(new { BarberId = barberA.Id, Date = date, StartTime = "09:00" }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, firstResponse.StatusCode);

        var secondResponse = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessToken)
                .WithJsonBody(new { BarberId = barberB.Id, Date = date, StartTime = "09:00" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, secondResponse.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_with_a_weekend_date_returns_400()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessToken)
                .WithJsonBody(new { BarberId = barber.Id, Date = NextWeekendDate(), StartTime = "09:00" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_with_malformed_date_returns_400()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessToken)
                .WithJsonBody(new { BarberId = barber.Id, Date = "09-01-2026", StartTime = "09:00" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_with_nonexistent_calendar_date_returns_400()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessToken)
                .WithJsonBody(new { BarberId = barber.Id, Date = "2026-02-31", StartTime = "09:00" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_with_malformed_startTime_returns_400()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessToken)
                .WithJsonBody(new { BarberId = barber.Id, Date = "2026-09-01", StartTime = "9am" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_with_nonexistent_time_returns_400()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessToken)
                .WithJsonBody(new { BarberId = barber.Id, Date = "2026-09-01", StartTime = "25:99" }),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetMyAppointments_returns_only_the_callers_upcoming_appointments_ordered_soonest_first()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber, firstName: "Amy", lastName: "Barber");
        using var client = _factory.CreateClient();
        var accessTokenA = await RegisterAndLogin(client, email: "customerA@example.com");
        var accessTokenB = await RegisterAndLogin(client, email: "customerB@example.com");
        var laterDate = NextBookableWeekday(10);
        var soonerDate = NextBookableWeekday(1);

        await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessTokenA)
                .WithJsonBody(new { BarberId = barber.Id, Date = laterDate, StartTime = "09:00" }),
            TestContext.Current.CancellationToken);
        await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessTokenA)
                .WithJsonBody(new { BarberId = barber.Id, Date = soonerDate, StartTime = "09:00" }),
            TestContext.Current.CancellationToken);
        await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessTokenB)
                .WithJsonBody(new { BarberId = barber.Id, Date = soonerDate, StartTime = "09:30" }),
            TestContext.Current.CancellationToken);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/booking/mine", accessTokenA), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<AppointmentView>>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Equal(2, body.Count);
        Assert.Equal(soonerDate, body[0].Date);
        Assert.Equal(laterDate, body[1].Date);
    }

    [Fact]
    public async Task GetMyAppointments_returns_empty_list_when_none_exist()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Get, "/api/booking/mine", accessToken), TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<List<AppointmentView>>(ResponseJsonOptions, TestContext.Current.CancellationToken);
        Assert.NotNull(body);
        Assert.Empty(body);
    }

    [Fact]
    public async Task CancelBooking_returns_204_and_frees_the_slot_for_rebooking()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessTokenA = await RegisterAndLogin(client, email: "customerA@example.com");
        var accessTokenB = await RegisterAndLogin(client, email: "customerB@example.com");
        var date = NextBookableWeekday();
        var bookResponse = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessTokenA)
                .WithJsonBody(new { BarberId = barber.Id, Date = date, StartTime = "09:00" }),
            TestContext.Current.CancellationToken);
        var created = await bookResponse.Content.ReadFromJsonAsync<BookingConfirmation>(ResponseJsonOptions, TestContext.Current.CancellationToken);

        var cancelResponse = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, $"/api/booking/{created!.Id}/cancel", accessTokenA),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NoContent, cancelResponse.StatusCode);

        var rebookResponse = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessTokenB)
                .WithJsonBody(new { BarberId = barber.Id, Date = date, StartTime = "09:00" }),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Created, rebookResponse.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_on_someone_elses_appointment_returns_404()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessTokenA = await RegisterAndLogin(client, email: "customerA@example.com");
        var accessTokenB = await RegisterAndLogin(client, email: "customerB@example.com");
        var date = NextBookableWeekday();
        var bookResponse = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessTokenA)
                .WithJsonBody(new { BarberId = barber.Id, Date = date, StartTime = "09:00" }),
            TestContext.Current.CancellationToken);
        var created = await bookResponse.Content.ReadFromJsonAsync<BookingConfirmation>(ResponseJsonOptions, TestContext.Current.CancellationToken);

        var cancelResponse = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, $"/api/booking/{created!.Id}/cancel", accessTokenB),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, cancelResponse.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_on_nonexistent_id_returns_404()
    {
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking/999999/cancel", accessToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_on_already_cancelled_appointment_returns_409()
    {
        var barber = await SeedAccount("barber@example.com", Role.Barber);
        using var client = _factory.CreateClient();
        var accessToken = await RegisterAndLogin(client);
        var date = NextBookableWeekday();
        var bookResponse = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking", accessToken)
                .WithJsonBody(new { BarberId = barber.Id, Date = date, StartTime = "09:00" }),
            TestContext.Current.CancellationToken);
        var created = await bookResponse.Content.ReadFromJsonAsync<BookingConfirmation>(ResponseJsonOptions, TestContext.Current.CancellationToken);

        var firstCancel = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, $"/api/booking/{created!.Id}/cancel", accessToken),
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NoContent, firstCancel.StatusCode);

        var secondCancel = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, $"/api/booking/{created.Id}/cancel", accessToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, secondCancel.StatusCode);
    }

    [Fact]
    public async Task CancelBooking_without_access_token_returns_401()
    {
        using var client = _factory.CreateClient();

        var response = await client.SendAsync(
            AuthedRequest(HttpMethod.Post, "/api/booking/1/cancel", null),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

internal static class HttpRequestMessageExtensions
{
    public static HttpRequestMessage WithJsonBody(this HttpRequestMessage request, object body)
    {
        request.Content = JsonContent.Create(body);
        return request;
    }
}
