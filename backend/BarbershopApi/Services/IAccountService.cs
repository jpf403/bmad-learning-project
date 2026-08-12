using BarbershopApi.Entities;

namespace BarbershopApi.Services;

public interface IAccountService
{
    Task<Account> UpdateOwnProfile(int accountId, string firstName, string lastName, string? newPassword, string? currentPassword);
    Task<List<Account>> SearchAccounts(string query);
    Task<Account> AdminCreateBarber(string email, string firstName, string lastName, string password);
    Task<Account> AdminUpdateAccount(int accountId, string email, string firstName, string lastName, Role role, string? newPassword, int actingAdminId);
    Task AdminSoftDeleteAccount(int accountId, int actingAdminId);
}
