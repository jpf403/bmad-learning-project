using BarbershopApi.Dtos;
using BarbershopApi.Entities;

namespace BarbershopApi.Services;

public interface IAuthService
{
    Task<Account> Register(RegisterRequest request);
    Task<(Account Account, string AccessToken, string RefreshToken)> Login(LoginRequest request);
    Task<(Account Account, string AccessToken, string RefreshToken)> LoginViaSso(string email, string firstName, string lastName, string subjectId);
    Task Logout(int accountId);
    Task<(Account Account, string AccessToken)> Refresh(string refreshToken);
}
