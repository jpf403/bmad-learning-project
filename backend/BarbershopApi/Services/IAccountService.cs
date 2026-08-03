using BarbershopApi.Entities;

namespace BarbershopApi.Services;

public interface IAccountService
{
    Task<Account> UpdateOwnProfile(int accountId, string firstName, string lastName, string? newPassword);
}
