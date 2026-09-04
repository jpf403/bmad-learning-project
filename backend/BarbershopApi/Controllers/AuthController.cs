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
public class AuthController(
    IAuthService authService,
    ISsoClient ssoClient,
    ISsoStateStore ssoStateStore,
    ILogger<AuthController> logger) : ControllerBase
{
    private const string SsoStateCookiePath = "/api/auth/sso";
    private static readonly CookieOptions SsoStateCookieDeleteOptions = new() { Path = SsoStateCookiePath };

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
        Response.Cookies.Delete("zpaxAccessToken", SsoStateCookieDeleteOptions);
        Response.Cookies.Delete("zpaxIdToken", SsoStateCookieDeleteOptions);
        Response.Cookies.Delete("zpaxRefreshToken", SsoStateCookieDeleteOptions);
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

        string authorizationUrl;
        try
        {
            authorizationUrl = ssoClient.BuildAuthorizationUrl(state);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SSO login failed while building the authorization URL.");
            return Redirect(SsoRedirects.Failure);
        }

        Response.Cookies.Append("ssoState", state, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = SsoStateCookiePath,
            Expires = DateTimeOffset.UtcNow.AddMinutes(10),
        });

        return Redirect(authorizationUrl);
    }

    [HttpGet("sso/callback")]
    public async Task<IActionResult> SsoCallback(string? code, string? state)
    {
        var cookieState = Request.Cookies["ssoState"];
        var hadActiveLoginAttempt = cookieState is not null;
        Response.Cookies.Delete("ssoState", SsoStateCookieDeleteOptions);

        if (string.IsNullOrEmpty(code) && !hadActiveLoginAttempt)
        {
            // No code and no login attempt in flight. ZPaxSso:LogoutRedirectUri
            // points at z-pax's own page (not back here), so this isn't the
            // post-logout redirect -- kept as a defensive catch-all for any
            // other benign no-code arrival (e.g. a stale bookmark), landing
            // cleanly rather than surfacing a failure banner for something
            // that was never a login attempt to begin with.
            return Redirect(SsoRedirects.Login);
        }

        if (string.IsNullOrEmpty(cookieState) || string.IsNullOrEmpty(state) || cookieState != state)
        {
            logger.LogWarning("SSO callback rejected: missing or mismatched state.");
            return Redirect(SsoRedirects.Failure);
        }

        if (string.IsNullOrEmpty(code))
        {
            logger.LogWarning("SSO callback rejected: missing authorization code.");
            return Redirect(SsoRedirects.Failure);
        }

        if (!ssoStateStore.TryConsume(state))
        {
            logger.LogWarning("SSO callback rejected: state already consumed.");
            return Redirect(SsoRedirects.Failure);
        }

        SsoIdentity identity;
        try
        {
            identity = await ssoClient.ExchangeCodeForIdentity(code);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "SSO callback failed while exchanging code for identity.");
            return Redirect(SsoRedirects.Failure);
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
            logger.LogWarning("SSO callback rejected: matched account is admin-protected.");
            return Redirect(SsoRedirects.Failure);
        }
        catch (SsoIdentityConflictException)
        {
            logger.LogWarning("SSO callback rejected: SSO identity conflict.");
            return Redirect(SsoRedirects.Failure);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "SSO callback failed while resolving the local account.");
            return Redirect(SsoRedirects.Failure);
        }

        Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Expires = DateTimeOffset.UtcNow.AddDays(15),
        });

        Response.Cookies.Append("zpaxAccessToken", identity.AccessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = SsoStateCookiePath,
            Expires = DateTimeOffset.UtcNow.AddMinutes(2),
        });

        if (!string.IsNullOrEmpty(identity.IdToken))
        {
            Response.Cookies.Append("zpaxIdToken", identity.IdToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = SsoStateCookiePath,
                Expires = DateTimeOffset.UtcNow.AddDays(15),
            });
        }
        else
        {
            Response.Cookies.Delete("zpaxIdToken", SsoStateCookieDeleteOptions);
        }

        if (!string.IsNullOrEmpty(identity.RefreshToken))
        {
            Response.Cookies.Append("zpaxRefreshToken", identity.RefreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = SsoStateCookiePath,
                Expires = DateTimeOffset.UtcNow.AddDays(15),
            });
        }
        else
        {
            Response.Cookies.Delete("zpaxRefreshToken", SsoStateCookieDeleteOptions);
        }

        var landingRoute = account.Role == Role.Customer ? "schedule-appointment" : "my-schedule";
        return Redirect($"https://localhost:5173/{landingRoute}");
    }

    [HttpGet("sso/logout")]
    public IActionResult SsoLogout()
    {
        var idToken = Request.Cookies["zpaxIdToken"];
        Response.Cookies.Delete("zpaxIdToken", SsoStateCookieDeleteOptions);

        if (string.IsNullOrEmpty(idToken))
        {
            return Redirect(SsoRedirects.Login);
        }

        return Redirect(ssoClient.BuildLogoutUrl(idToken));
    }

    [HttpGet("sso/zpax-token")]
    [Authorize]
    public IActionResult ZpaxToken()
    {
        var token = Request.Cookies["zpaxAccessToken"];
        if (string.IsNullOrEmpty(token))
        {
            return NotFound();
        }

        Response.Cookies.Delete("zpaxAccessToken", SsoStateCookieDeleteOptions);
        return Ok(new ZpaxTokenResponse(token));
    }

    [HttpGet("sso/zpax-refresh")]
    [Authorize]
    public async Task<IActionResult> ZpaxRefresh()
    {
        var refreshToken = Request.Cookies["zpaxRefreshToken"];
        if (string.IsNullOrEmpty(refreshToken))
        {
            return NotFound();
        }

        try
        {
            var result = await ssoClient.RefreshAccessToken(refreshToken);
            if (result.RefreshToken is not null)
            {
                Response.Cookies.Append("zpaxRefreshToken", result.RefreshToken, new CookieOptions
                {
                    HttpOnly = true,
                    Secure = true,
                    SameSite = SameSiteMode.Strict,
                    Path = SsoStateCookiePath,
                    Expires = DateTimeOffset.UtcNow.AddDays(15),
                });
            }

            return Ok(new ZpaxTokenResponse(result.AccessToken));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "z-pax token refresh failed; treating the refresh token as invalid.");
            Response.Cookies.Delete("zpaxRefreshToken", SsoStateCookieDeleteOptions);
            return NotFound();
        }
    }
}
