using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Data;

public class BarbershopDbContext(DbContextOptions<BarbershopDbContext> options) : DbContext(options)
{
}
