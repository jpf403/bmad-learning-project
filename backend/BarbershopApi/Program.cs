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
builder.Services.AddScoped<IPasswordHasher<Account>, PasswordHasher<Account>>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddHostedService<AdminBootstrapService>();

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

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
});

var app = builder.Build();

var jwtKey = app.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey))
{
    throw new InvalidOperationException("Jwt:Key is not configured. Set the Jwt__Key environment variable.");
}
if (jwtKey.Length < 32)
{
    throw new InvalidOperationException("Jwt:Key must be at least 32 characters.");
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

app.UseRateLimiter();

app.UseAuthentication();                            // Who are you
app.UseMiddleware<SessionLivenessMiddleware>();     // Has the session been killed (logout, password changed by admin)
app.UseAuthorization();                             // Do you have access to page

app.MapControllers();

app.Run();

public partial class Program;
