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

    [HttpPut("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminUpdate(int id, AdminUpdateAccountRequest request)
    {
        var admin = (Account)HttpContext.Items["Account"]!;
        try
        {
            var updated = await accountService.AdminUpdateAccount(id, request.Email, request.FirstName, request.LastName, request.Role, request.NewPassword, admin.Id);
            return Ok(new AccountSummary(updated.Id, updated.Email, updated.FirstName, updated.LastName, updated.Role));
        }
        catch (AccountNotFoundException)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Account not found.");
        }
        catch (AdminAccountProtectedException)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "The admin account cannot be edited.");
        }
        catch (InvalidRoleAssignmentException)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "An account cannot be promoted to admin.");
        }
        catch (InvalidPasswordException)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Password must be at least 8 characters and cannot contain spaces.");
        }
        catch (DuplicateEmailException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "That email is already in use.");
        }
        catch (AccountConflictException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This account was changed elsewhere. Refresh and try again.");
        }
        catch (Exception)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Something went wrong. Please try again.");
        }
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminCreate(AdminCreateBarberRequest request)
    {
        try
        {
            var created = await accountService.AdminCreateBarber(request.Email, request.FirstName, request.LastName, request.Password);
            return StatusCode(201, new AccountSummary(created.Id, created.Email, created.FirstName, created.LastName, created.Role));
        }
        catch (InvalidPasswordException)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Password must be at least 8 characters and cannot contain spaces.");
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

    [HttpDelete("{id:int}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> AdminDelete(int id)
    {
        var admin = (Account)HttpContext.Items["Account"]!;
        try
        {
            await accountService.AdminSoftDeleteAccount(id, admin.Id);
            return NoContent();
        }
        catch (AccountNotFoundException)
        {
            return Problem(statusCode: StatusCodes.Status404NotFound, title: "Account not found.");
        }
        catch (AdminAccountProtectedException)
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "The admin account cannot be deleted.");
        }
        catch (AccountConflictException)
        {
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "This account was changed elsewhere. Refresh and try again.");
        }
        catch (Exception)
        {
            return Problem(statusCode: StatusCodes.Status500InternalServerError, title: "Something went wrong. Please try again.");
        }
    }
}
