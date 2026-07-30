using BarbershopApi.Dtos;
using BarbershopApi.Services;
using Microsoft.AspNetCore.Mvc;

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
}
