using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using BarbershopApi.Dtos;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace BarbershopApi.Services;

public class AuthService(
    IAccountRepository accountRepository,
    IPasswordHasher<Account> passwordHasher,
    IOptions<JwtOptions> jwtOptions) : IAuthService
{
    private const int SqliteConstraintViolation = 19;

    public async Task<Account> Register(RegisterRequest request)
    {
        var existing = await accountRepository.FindByEmail(request.Email);
        if (existing is not null)
        {
            throw new DuplicateEmailException();
        }

        var account = new Account
        {
            Email = request.Email,
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Role = Role.Customer,
        };
        account.PasswordHash = passwordHasher.HashPassword(account, request.Password);

        try
        {
            return await accountRepository.Create(account);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintViolation })
        {
            throw new DuplicateEmailException();
        }
    }

    public async Task<(Account Account, string AccessToken, string RefreshToken)> Login(LoginRequest request)
    {
        var account = await accountRepository.FindByEmail(request.Email);
        if (account is null || account.PasswordHash is null)
        {
            throw new InvalidCredentialsException();
        }

        var result = passwordHasher.VerifyHashedPassword(account, account.PasswordHash, request.Password);
        if (result == PasswordVerificationResult.Failed)
        {
            throw new InvalidCredentialsException();
        }

        var accessToken = GenerateAccessToken(account);
        var refreshToken = GenerateRefreshToken(account);

        return (account, accessToken, refreshToken);
    }

    public async Task Logout(int accountId)
    {
        var account = await accountRepository.FindById(accountId);
        if (account is null)
        {
            return;
        }

        account.SessionVersion++;
        await accountRepository.Update(account);
    }

    public async Task<(Account Account, string AccessToken)> Refresh(string refreshToken)
    {
        var handler = new JwtSecurityTokenHandler { MapInboundClaims = false };
        ClaimsPrincipal principal;
        try
        {
            principal = handler.ValidateToken(refreshToken, ValidationParameters(), out _);
        }
        catch (SecurityTokenException)
        {
            throw new InvalidSessionException();
        }

        var subClaim = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        var sessionVersionClaim = principal.FindFirstValue("sessionVersion");
        if (subClaim is null || !int.TryParse(subClaim, out var accountId) ||
            sessionVersionClaim is null || !int.TryParse(sessionVersionClaim, out var tokenSessionVersion))
        {
            throw new InvalidSessionException();
        }

        var account = await accountRepository.FindById(accountId);
        if (account is null || account.SessionVersion != tokenSessionVersion)
        {
            throw new InvalidSessionException();
        }

        return (account, GenerateAccessToken(account));
    }

    private TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuer = true,
        ValidIssuer = "BarbershopApi",
        ValidateAudience = true,
        ValidAudience = TokenAudiences.Refresh,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Key)),
    };

    private string GenerateAccessToken(Account account)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new Claim("sessionVersion", account.SessionVersion.ToString()),
        };
        var token = new JwtSecurityToken(
            issuer: "BarbershopApi",
            audience: TokenAudiences.Access,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(60),
            signingCredentials: SigningCredentials());
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private string GenerateRefreshToken(Account account)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new Claim("sessionVersion", account.SessionVersion.ToString()),
        };
        var token = new JwtSecurityToken(
            issuer: "BarbershopApi",
            audience: TokenAudiences.Refresh,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(15),
            signingCredentials: SigningCredentials());
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private SigningCredentials SigningCredentials()
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.Value.Key));
        return new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }
}
