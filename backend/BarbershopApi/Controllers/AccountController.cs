using BarbershopApi.Dtos;
using BarbershopApi.Entities;
using BarbershopApi.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarbershopApi.Controllers;

[ApiController]
[Route("api/account")]
[Authorize]
public class AccountController(IAccountService accountService) : ControllerBase
{
    [HttpPut("me")]
    public async Task<IActionResult> UpdateMe(UpdateAccountRequest request)
    {
        var account = (Account)HttpContext.Items["Account"]!;
        try
        {
            var updated = await accountService.UpdateOwnProfile(account.Id, request.FirstName, request.LastName, request.NewPassword);
            return Ok(new MeResponse(updated.Id, updated.Email, updated.FirstName, updated.LastName, updated.Role));
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
}
