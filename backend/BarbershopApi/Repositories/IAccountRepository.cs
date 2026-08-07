using BarbershopApi.Entities;

namespace BarbershopApi.Repositories;

public interface IAccountRepository
{
    Task<Account> Create(Account account);
    Task<Account?> FindByEmail(string email);
    Task<Account?> FindById(int id);
    Task Update(Account account);
    Task<bool> AdminExists();
    Task<List<Account>> FindAllByRole(Role role);
}
