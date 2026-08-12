using BarbershopApi.Entities;

namespace BarbershopApi.Dtos;

public record AccountSummary(int Id, string Email, string FirstName, string LastName, Role Role);
