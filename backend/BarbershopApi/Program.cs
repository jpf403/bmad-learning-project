using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using BarbershopApi.Data;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using BarbershopApi.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

const string VitePolicy = "VitePolicy";

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

builder.Services.AddCors(options =>
{
    options.AddPolicy(VitePolicy, policy =>
        policy.WithOrigins("http://localhost:5173", "https://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials());
});

var connectionString = builder.Configuration.GetConnectionString("BarbershopDb")
    ?? $"Data Source={Path.Combine(builder.Environment.ContentRootPath, "App_Data", "barbershop.db")}";

builder.Services.AddDbContext<BarbershopDbContext>(options => options.UseSqlite(connectionString));
builder.Services.AddScoped<IAccountRepository, AccountRepository>();
builder.Services.AddScoped<IAppointmentRepository, AppointmentRepository>();
builder.Services.AddScoped<IPasswordHasher<Account>, PasswordHasher<Account>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAccountService, AccountService>();
builder.Services.AddScoped<IBookingService, BookingService>();
builder.Services.AddHttpClient<ISsoClient, ZPaxSsoClient>(client => client.Timeout = TimeSpan.FromSeconds(10));
builder.Services.AddSingleton<ISsoStateStore, InMemorySsoStateStore>();
builder.Services.AddHostedService<AdminBootstrapService>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

// ClientId/ClientSecret are env-var-only secrets (never in appsettings.json, same convention
// as AdminSeed) and are legitimately blank in dev/test/CI where FakeSsoClient stands in for
// ZPaxSsoClient -- so they're checked with a non-fatal startup warning (AdminBootstrapService's
// pattern) below, not ValidateOnStart. The other four fields always ship a real value in
// appsettings.json across every environment, so failing fast on a blank one is safe.
builder.Services.AddOptions<ZPaxSsoOptions>()
    .Bind(builder.Configuration.GetSection("ZPaxSso"))
    .Validate(o => Uri.TryCreate(o.AuthorizationEndpoint, UriKind.Absolute, out _), "ZPaxSso:AuthorizationEndpoint is not a valid absolute URL.")
    .Validate(o => Uri.TryCreate(o.TokenEndpoint, UriKind.Absolute, out _), "ZPaxSso:TokenEndpoint is not a valid absolute URL.")
    .Validate(o => Uri.TryCreate(o.UserInfoEndpoint, UriKind.Absolute, out _), "ZPaxSso:UserInfoEndpoint is not a valid absolute URL.")
    .Validate(o => Uri.TryCreate(o.RedirectUri, UriKind.Absolute, out _), "ZPaxSso:RedirectUri is not a valid absolute URL.")
    .ValidateOnStart();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

// Configured via IOptions<JwtOptions> (not a captured builder.Configuration read) so the
// signing key resolves from the FINAL merged configuration at options-materialization time
// (post-Build) — under WebApplicationFactory, test-provided config overrides only land in
// builder.Configuration after Build() completes, so an eager read here would silently see
// the empty appsettings.json placeholder instead of the test's injected key.
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((options, jwtOptions) =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = "BarbershopApi",
            ValidateAudience = true,
            ValidAudience = TokenAudiences.Access,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Key)),
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddProblemDetails();

builder.Services.AddRateLimiter(options =>
{
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(
            new { title = "Too many attempts. Try again in a few minutes." }, token);
    };
    options.AddPolicy("LoginPolicy", httpContext =>
    {
        httpContext.Request.EnableBuffering();
        using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
        var body = reader.ReadToEndAsync().GetAwaiter().GetResult();
        httpContext.Request.Body.Position = 0;

        var email = "unknown";
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("email", out var emailProp) &&
                emailProp.ValueKind == JsonValueKind.String)
            {
                email = emailProp.GetString()?.Trim().ToLowerInvariant() ?? "unknown";
            }
        }
        catch (JsonException) { /* malformed body — fall through to "unknown" bucket, model binding will 400 it anyway */ }

        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter($"{ip}:{email}", _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            SegmentsPerWindow = 3,
            QueueLimit = 0,
        });
    });
    options.AddPolicy("RefreshPolicy", httpContext =>
    {
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter(ip, _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 60,
            Window = TimeSpan.FromMinutes(15),
            SegmentsPerWindow = 3,
            QueueLimit = 0,
        });
    });
    options.AddPolicy("PasswordChangePolicy", httpContext =>
    {
        httpContext.Request.EnableBuffering();
        using var reader = new StreamReader(httpContext.Request.Body, leaveOpen: true);
        var body = reader.ReadToEndAsync().GetAwaiter().GetResult();
        httpContext.Request.Body.Position = 0;

        var isPasswordChangeAttempt = false;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                // Case-insensitive lookup — model binding is case-insensitive by default,
                // so a differently-cased "NewPassword" key still succeeds as a real
                // password change and must still count toward the rate limit.
                var newPasswordProp = doc.RootElement.EnumerateObject()
                    .FirstOrDefault(p => string.Equals(p.Name, "newPassword", StringComparison.OrdinalIgnoreCase));
                if (newPasswordProp.Value.ValueKind == JsonValueKind.String &&
                    !string.IsNullOrEmpty(newPasswordProp.Value.GetString()))
                {
                    isPasswordChangeAttempt = true;
                }
            }
        }
        catch (JsonException) { /* malformed body — not a password-change attempt as far as rate limiting cares; model binding will 400 it anyway */ }

        // Only requests that actually attempt a password change count toward this limit —
        // plain name-only edits go through GetNoLimiter and are never throttled here.
        if (!isPasswordChangeAttempt)
        {
            return RateLimitPartition.GetNoLimiter("no-password-change-attempt");
        }

        // Requires UseRateLimiter() to run after SessionLivenessMiddleware so
        // HttpContext.Items["Account"] is already populated with the authenticated caller.
        var account = httpContext.Items["Account"] as Account;
        var accountKey = account?.Id.ToString() ?? "unknown";
        var ip = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetSlidingWindowLimiter($"{ip}:{accountKey}", _ => new SlidingWindowRateLimiterOptions
        {
            PermitLimit = 5,
            Window = TimeSpan.FromMinutes(15),
            SegmentsPerWindow = 3,
            QueueLimit = 0,
        });
    });
});

var app = builder.Build();

// JWT Key from env var
var jwtKey = app.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("Jwt:Key is not configured. Set the Jwt__Key environment variable.");
}
if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
}

var zPaxSsoOptions = app.Services.GetRequiredService<IOptions<ZPaxSsoOptions>>().Value;
if (string.IsNullOrWhiteSpace(zPaxSsoOptions.ClientId) || string.IsNullOrWhiteSpace(zPaxSsoOptions.ClientSecret))
{
    app.Logger.LogWarning("ZPaxSso:ClientId/ZPaxSso:ClientSecret not configured — z-pax SSO login will fail until these are set.");
}

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<BarbershopDbContext>().Database.Migrate();
}

// Configure the HTTP request pipeline.
app.UseHttpsRedirection();

app.UseCors(VitePolicy);

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();                            // Who are you
app.UseMiddleware<SessionLivenessMiddleware>();     // Has the session been killed (logout, password changed by admin)

// Runs after SessionLivenessMiddleware (not before, like Login/Refresh's policies could
// afford) so PasswordChangePolicy's partition resolver can key off the authenticated
// caller's account id via HttpContext.Items["Account"] — Login/Refresh remain unaffected
// since neither carries a bearer token for UseAuthentication()/SessionLivenessMiddleware to act on.
app.UseRateLimiter();

app.UseAuthorization();                             // Do you have access to page

app.MapControllers();

app.Run();

public partial class Program;
