using BarbershopApi.Entities;

namespace BarbershopApi.Dtos;

public record LoginResponse(string AccessToken, int Id, string Email, string FirstName, string LastName, Role Role);
