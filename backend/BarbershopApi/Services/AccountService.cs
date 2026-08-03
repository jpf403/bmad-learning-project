using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Services;

public class AccountService(IAccountRepository accountRepository, IPasswordHasher<Account> passwordHasher) : IAccountService
{
    public async Task<Account> UpdateOwnProfile(int accountId, string firstName, string lastName, string? newPassword)
    {
        var account = await accountRepository.FindById(accountId)
            ?? throw new InvalidOperationException("Account not found for an authenticated caller.");

        account.FirstName = firstName.Trim();
        account.LastName = lastName.Trim();

        if (!string.IsNullOrEmpty(newPassword))
        {
            account.PasswordHash = passwordHasher.HashPassword(account, newPassword);
        }

        try
        {
            await accountRepository.Update(account);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AccountConflictException();
        }

        return account;
    }
}
