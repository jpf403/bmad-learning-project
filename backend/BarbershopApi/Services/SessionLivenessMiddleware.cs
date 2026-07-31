using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using BarbershopApi.Repositories;

namespace BarbershopApi.Services;

public class SessionLivenessMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IAccountRepository accountRepository)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var subClaim = context.User.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var sessionVersionClaim = context.User.FindFirstValue("sessionVersion");

            if (subClaim is null || !int.TryParse(subClaim, out var accountId) ||
                sessionVersionClaim is null || !int.TryParse(sessionVersionClaim, out var tokenSessionVersion))
            {
                await Reject(context);
                return;
            }

            var account = await accountRepository.FindById(accountId);
            if (account is null || account.SessionVersion != tokenSessionVersion)
            {
                await Reject(context);
                return;
            }

            var existingIdentity = (ClaimsIdentity)context.User.Identity;
            var refreshedIdentity = new ClaimsIdentity(existingIdentity.Claims, existingIdentity.AuthenticationType);
            refreshedIdentity.AddClaim(new Claim(ClaimTypes.Role, account.Role.ToString()));
            context.User = new ClaimsPrincipal(refreshedIdentity);
            context.Items["Account"] = account;
        }

        await next(context);
    }

    private static async Task Reject(HttpContext context)
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        await context.Response.WriteAsJsonAsync(new { title = "Session expired. Please sign in again." });
    }
}
