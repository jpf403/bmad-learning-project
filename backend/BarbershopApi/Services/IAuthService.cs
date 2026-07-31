using BarbershopApi.Dtos;
using BarbershopApi.Entities;

namespace BarbershopApi.Services;

public interface IAuthService
{
    Task<Account> Register(RegisterRequest request);
    Task<(Account Account, string AccessToken, string RefreshToken)> Login(LoginRequest request);
    Task Logout(int accountId);
    Task<(Account Account, string AccessToken)> Refresh(string refreshToken);
}
