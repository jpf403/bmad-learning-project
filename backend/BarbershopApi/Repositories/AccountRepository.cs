using BarbershopApi.Data;
using BarbershopApi.Entities;
using BarbershopApi.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Repositories;

public class AccountRepository(BarbershopDbContext context) : IAccountRepository
{
    private const int SqliteConstraintViolation = 19;

    public async Task<Account> Create(Account account)
    {
        account.Email = account.Email.Trim().ToLowerInvariant();
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    public async Task<Account?> FindByEmail(string email)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        return await context.Accounts
            .FirstOrDefaultAsync(a => a.Email == normalizedEmail && a.DeletedAt == null);
    }

    public async Task<Account?> FindById(int id)
    {
        return await context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.DeletedAt == null);
    }

    public async Task Update(Account account)
    {
        account.Email = account.Email.Trim().ToLowerInvariant();
        context.Update(account);
        await context.SaveChangesAsync();
        await context.Entry(account).ReloadAsync();
    }

    public async Task<bool> AdminExists()
    {
        return await context.Accounts.AnyAsync(a => a.Role == Role.Admin && a.DeletedAt == null);
    }

    public async Task<List<Account>> FindAllByRole(Role role)
    {
        return await context.Accounts
            .Where(a => a.Role == role && a.DeletedAt == null)
            .OrderBy(a => a.FirstName)
            .ThenBy(a => a.LastName)
            .ThenBy(a => a.Id)
            .ToListAsync();
    }

    public async Task<List<Account>> Search(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return [];
        }

        var normalizedQuery = query.Trim().ToLowerInvariant();
        return await context.Accounts
            .Where(a => a.Role != Role.Admin && a.DeletedAt == null)
            .Where(a => a.FirstName.ToLower().Contains(normalizedQuery) ||
                a.LastName.ToLower().Contains(normalizedQuery) ||
                (a.FirstName + " " + a.LastName).ToLower().Contains(normalizedQuery) ||
                a.Email.Contains(normalizedQuery))
            .ToListAsync();
    }

    public async Task AdminUpdate(Account account)
    {
        await EnsureNotCurrentlyAdmin(account.Id);
        if (account.Role == Role.Admin)
        {
            throw new InvalidRoleAssignmentException();
        }

        account.Email = account.Email.Trim().ToLowerInvariant();
        context.Update(account);
        await context.SaveChangesAsync();
        await context.Entry(account).ReloadAsync();
    }

    public async Task SoftDelete(Account account)
    {
        await EnsureNotCurrentlyAdmin(account.Id);

        account.DeletedAt = DateTime.UtcNow;
        account.SsoProvider = null;
        account.SsoSubjectId = null;
        context.Update(account);
        await context.SaveChangesAsync();
    }

    public async Task<Account?> FindBySsoIdentity(string provider, string subjectId)
    {
        return await context.Accounts
            .FirstOrDefaultAsync(a => a.SsoProvider == provider && a.SsoSubjectId == subjectId && a.DeletedAt == null);
    }

    public async Task<Account> CreateOrLinkSsoAccount(string email, string firstName, string lastName, string provider, string subjectId)
    {
        var existing = await FindByEmail(email);
        if (existing is null)
        {
            var account = new Account
            {
                Email = email,
                FirstName = firstName.Trim(),
                LastName = lastName.Trim(),
                Role = Role.Customer,
                PasswordHash = null,
                SsoProvider = provider,
                SsoSubjectId = subjectId,
            };
            try
            {
                return await Create(account);
            }
            catch (DbUpdateException ex) when (ex.InnerException is SqliteException { SqliteErrorCode: SqliteConstraintViolation })
            {
                throw new SsoIdentityConflictException();
            }
        }

        if (existing.Role == Role.Admin)
        {
            throw new AdminAccountProtectedException();
        }

        existing.SsoProvider = provider;
        existing.SsoSubjectId = subjectId;
        context.Update(existing);
        await context.SaveChangesAsync();
        await context.Entry(existing).ReloadAsync();
        return existing;
    }

    private async Task EnsureNotCurrentlyAdmin(int accountId)
    {
        var currentRole = await context.Accounts.AsNoTracking()
            .Where(a => a.Id == accountId)
            .Select(a => a.Role)
            .FirstOrDefaultAsync();
        if (currentRole == Role.Admin)
        {
            throw new AdminAccountProtectedException();
        }
    }
}
