using BarbershopApi.Dtos;
using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Services;

public class AuthService(IAccountRepository accountRepository, IPasswordHasher<Account> passwordHasher) : IAuthService
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
}
