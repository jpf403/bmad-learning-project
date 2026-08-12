using BarbershopApi.Dtos;
using BarbershopApi.Entities;
using BarbershopApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BarbershopApi.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController(IAccountService accountService) : ControllerBase
{
    [HttpPut("me")]
    [EnableRateLimiting("PasswordChangePolicy")]
    public async Task<IActionResult> UpdateMe(UpdateAccountRequest request)
    {
        var account = (Account)HttpContext.Items["Account"]!;
        try
        {
            var updated = await accountService.UpdateOwnProfile(account.Id, request.FirstName, request.LastName, request.NewPassword, request.CurrentPassword);
            return Ok(new MeResponse(updated.Id, updated.Email, updated.FirstName, updated.LastName, updated.Role));
        }
        catch (InvalidCurrentPasswordException)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Current password is incorrect.");
        }
        catch (SameAsCurrentPasswordException)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "New password must be different from your current password.");
        }
        catch (AccountConflictException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This account was updated elsewhere. Please refresh and try again.");
        }
        catch (Exception)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Something went wrong. Please try again.");
        }
    }

    [HttpGet("search")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Search([FromQuery] string? query)
    {
        var accounts = await accountService.SearchAccounts(query ?? string.Empty);
        return Ok(accounts.Select(a => new AccountSummary(a.Id, a.Email, a.FirstName, a.LastName, a.Role)));
    }
}
