using BarbershopApi.Dtos;
using BarbershopApi.Entities;

namespace BarbershopApi.Services;

public interface IAuthService
{
    Task<Account> Register(RegisterRequest request);
}
