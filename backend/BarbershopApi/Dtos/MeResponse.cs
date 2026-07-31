using BarbershopApi.Entities;

namespace BarbershopApi.Dtos;

public record MeResponse(int Id, string Email, string FirstName, string LastName, Role Role);
