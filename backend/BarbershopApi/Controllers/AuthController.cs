using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BarbershopApi.Dtos;
using BarbershopApi.Entities;
using BarbershopApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BarbershopApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
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
    }
}
