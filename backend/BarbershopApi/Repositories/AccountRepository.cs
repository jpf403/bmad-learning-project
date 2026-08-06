using BarbershopApi.Data;
using BarbershopApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Repositories;

public class AccountRepository(BarbershopDbContext context) : IAccountRepository
{
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
}
