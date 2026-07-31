using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BarbershopApi.Tests.TestOnly;

[ApiController]
[Route("api/test-only")]
public class RoleGateTestController : ControllerBase
{
    [HttpGet("admin")]
    [Authorize(Roles = "Admin")]
    public IActionResult AdminOnly() => Ok(new { message = "ok" });
}
