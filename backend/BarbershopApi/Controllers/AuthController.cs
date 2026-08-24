using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using BarbershopApi.Dtos;
using BarbershopApi.Entities;
using BarbershopApi.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BarbershopApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService, ISsoClient ssoClient) : ControllerBase
{
    private const string SsoFailureRedirect = "https://localhost:5173/login?error=sso_failed";
    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        try
        {
            var account = await authService.Register(request);
            return StatusCode(201, new RegisterResponse(account.Id, account.Email, account.FirstName, account.LastName));
        }
        catch (DuplicateEmailException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "That email is already in use.");
        }
        catch (Exception)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Something went wrong. Please try again.");
        }
    }

    [HttpPost("login")]
    [EnableRateLimiting("LoginPolicy")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        try
        {
            var (account, accessToken, refreshToken) = await authService.Login(request);
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UtcNow.AddDays(15),
            });
            return Ok(new LoginResponse(accessToken, account.Id, account.Email, account.FirstName, account.LastName, account.Role));
        }
        catch (InvalidCredentialsException)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
        }
        catch (Exception)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Something went wrong. Please try again.");
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        var accountId = int.Parse(User.FindFirstValue(JwtRegisteredClaimNames.Sub)!);
        await authService.Logout(accountId);
        Response.Cookies.Delete("refreshToken");
        return NoContent();
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var account = (Account)HttpContext.Items["Account"]!;
        return Ok(new MeResponse(account.Id, account.Email, account.FirstName, account.LastName, account.Role));
    }

    [HttpPost("refresh")]
    [EnableRateLimiting("RefreshPolicy")]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = Request.Cookies["refreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Session expired. Please sign in again.");
        }

        try
        {
            var (_, accessToken) = await authService.Refresh(refreshToken);
            return Ok(new RefreshResponse(accessToken));
        }
        catch (InvalidSessionException)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Session expired. Please sign in again.");
        }
        catch (Exception)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Something went wrong. Please try again.");
        }
    }

    [HttpGet("sso/login")]
    public IActionResult SsoLogin()
    {
        var state = Base64UrlTextEncoder.Encode(RandomNumberGenerator.GetBytes(32));

        Response.Cookies.Append("ssoState", state, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.AddMinutes(10),
        });

        return Redirect(ssoClient.BuildAuthorizationUrl(state));
    }

    [HttpGet("sso/callback")]
    public async Task<IActionResult> SsoCallback(string? code, string? state)
    {
        var cookieState = Request.Cookies["ssoState"];
        Response.Cookies.Delete("ssoState");

        if (string.IsNullOrEmpty(cookieState) || string.IsNullOrEmpty(state) || cookieState != state)
        {
            return Redirect(SsoFailureRedirect);
        }

        if (string.IsNullOrEmpty(code))
        {
            return Redirect(SsoFailureRedirect);
        }

        SsoIdentity identity;
        try
        {
            identity = await ssoClient.ExchangeCodeForIdentity(code);
        }
        catch (Exception)
        {
            return Redirect(SsoFailureRedirect);
        }

        Account account;
        string accessToken;
        string refreshToken;
        try
        {
            (account, accessToken, refreshToken) = await authService.LoginViaSso(
                identity.Email, identity.FirstName, identity.LastName, identity.SubjectId);
        }
        catch (AdminAccountProtectedException)
        {
            return Redirect(SsoFailureRedirect);
        }
        catch (SsoIdentityConflictException)
        {
            return Redirect(SsoFailureRedirect);
        }

        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(15),
        });

        var landingRoute = account.Role == Role.Customer ? "schedule-appointment" : "my-schedule";
        return Redirect($"https://localhost:5173/{landingRoute}");
    }
}
