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
    Task<List<Account>> Search(string query);
    Task AdminUpdate(Account account);
    Task SoftDelete(Account account);
    Task<Account?> FindBySsoIdentity(string provider, string subjectId);
    Task<Account> CreateOrLinkSsoAccount(string email, string firstName, string lastName, string provider, string subjectId);
}
