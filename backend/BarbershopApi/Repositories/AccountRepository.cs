using BarbershopApi.Data;
using BarbershopApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Repositories;

public class AccountRepository(BarbershopDbContext context) : IAccountRepository
{
    public async Task<Account> Create(Account account)
    {
        account.Email = account.Email.ToLowerInvariant();
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        return account;
    }

    public async Task<Account?> FindByEmail(string email)
    {
        var normalizedEmail = email.ToLowerInvariant();
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
        context.Update(account);
        await context.SaveChangesAsync();
    }
}
