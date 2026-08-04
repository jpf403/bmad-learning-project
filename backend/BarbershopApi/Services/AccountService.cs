using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Services;

public class AccountService(IAccountRepository accountRepository, IPasswordHasher<Account> passwordHasher) : IAccountService
{
    public async Task<Account> UpdateOwnProfile(int accountId, string firstName, string lastName, string? newPassword, string? currentPassword)
    {
        var account = await accountRepository.FindById(accountId)
            ?? throw new InvalidOperationException("Account not found for an authenticated caller.");

        string? newPasswordHash = null;
        if (!string.IsNullOrEmpty(newPassword))
        {
            if (string.IsNullOrEmpty(currentPassword) ||
                passwordHasher.VerifyHashedPassword(account, account.PasswordHash, currentPassword) == PasswordVerificationResult.Failed)
            {
                throw new InvalidCurrentPasswordException();
            }

            if (passwordHasher.VerifyHashedPassword(account, account.PasswordHash, newPassword) != PasswordVerificationResult.Failed)
            {
                throw new SameAsCurrentPasswordException();
            }

            newPasswordHash = passwordHasher.HashPassword(account, newPassword);
        }

        account.FirstName = firstName.Trim();
        account.LastName = lastName.Trim();
        if (newPasswordHash is not null)
        {
            account.PasswordHash = newPasswordHash;
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
