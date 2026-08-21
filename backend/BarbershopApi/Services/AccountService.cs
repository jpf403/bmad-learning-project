using BarbershopApi.Entities;
using BarbershopApi.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Services;

public class AccountService(IAccountRepository accountRepository, IPasswordHasher<Account> passwordHasher, IBookingService bookingService) : IAccountService
{
    private const int SqliteConstraintViolation = 19;
    private const int MinPasswordLength = 8;
    private const int MaxPasswordLength = 128;

    // Same shape as RegisterRequest's/UpdateAccountRequest's DTO-level password validation
    // attributes -- this story has no Controller/DTO layer yet, so the admin-only methods
    // below enforce the same rule directly rather than accepting anything unvalidated.
    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrEmpty(password) ||
            password.Length < MinPasswordLength ||
            password.Length > MaxPasswordLength ||
            password.Any(char.IsWhiteSpace))
        {
            throw new InvalidPasswordException();
        }
    }

    public async Task<Account> UpdateOwnProfile(int accountId, string firstName, string lastName, string? newPassword, string? currentPassword)
    {
        var account = await accountRepository.FindById(accountId)
            ?? throw new InvalidOperationException("Account not found for an authenticated caller.");

        string? newPasswordHash = null;
        if (!string.IsNullOrEmpty(newPassword))
        {
            if (string.IsNullOrEmpty(currentPassword) || account.PasswordHash is null ||
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

    public async Task<List<Account>> SearchAccounts(string query) => await accountRepository.Search(query);

    public async Task<Account> AdminCreateBarber(string email, string firstName, string lastName, string password)
    {
        ValidatePassword(password);

        var existing = await accountRepository.FindByEmail(email);
        if (existing is not null)
        {
            throw new DuplicateEmailException();
        }

        var account = new Account
        {
            Email = email,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Role = Role.Barber,
        };
        account.PasswordHash = passwordHasher.HashPassword(account, password);

        try
        {
            return await accountRepository.Create(account);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintViolation })
        {
            throw new DuplicateEmailException();
        }
    }

    public async Task<Account> AdminUpdateAccount(int accountId, string email, string firstName, string lastName, Role role, string? newPassword, int actingAdminId)
    {
        var account = await accountRepository.FindById(accountId)
            ?? throw new AccountNotFoundException();

        if (role == Role.Admin)
        {
            throw new InvalidRoleAssignmentException();
        }

        if (!string.Equals(account.Email, email.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            var existing = await accountRepository.FindByEmail(email);
            if (existing is not null)
            {
                throw new DuplicateEmailException();
            }
        }

        var isDemotion = account.Role == Role.Barber && role == Role.Customer;

        account.Email = email;
        account.FirstName = firstName.Trim();
        account.LastName = lastName.Trim();
        account.Role = role;
        if (newPassword is not null)
        {
            ValidatePassword(newPassword);
            account.PasswordHash = passwordHasher.HashPassword(account, newPassword);
            account.SessionVersion++;
        }

        try
        {
            await accountRepository.AdminUpdate(account);
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintViolation })
        {
            throw new DuplicateEmailException();
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AccountConflictException();
        }

        if (isDemotion)
        {
            await bookingService.CancelAllFutureForBarber(accountId, actingAdminId, Role.Admin);
        }

        return account;
    }

    public async Task AdminSoftDeleteAccount(int accountId, int actingAdminId)
    {
        var account = await accountRepository.FindById(accountId)
            ?? throw new AccountNotFoundException();

        try
        {
            await accountRepository.SoftDelete(account);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new AccountConflictException();
        }

        // Role.Admin never reaches here: SoftDelete's EnsureNotCurrentlyAdmin already
        // threw AdminAccountProtectedException above for that role.
        if (account.Role == Role.Barber)
        {
            await bookingService.CancelAllFutureForBarber(accountId, actingAdminId, Role.Admin);
        }
        else if (account.Role == Role.Customer)
        {
            await bookingService.CancelAllFutureForCustomer(accountId, actingAdminId, Role.Admin);
        }
    }
}
