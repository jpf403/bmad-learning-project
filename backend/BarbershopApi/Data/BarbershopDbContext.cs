using BarbershopApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Data;

public class BarbershopDbContext(DbContextOptions<BarbershopDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Account>(entity =>
        {
            entity.Property(a => a.Role).HasConversion<string>();

            entity.HasIndex(a => a.Email)
                .IsUnique()
                .HasFilter("DeletedAt IS NULL");

            entity.Property(a => a.RowVersion)
                .IsConcurrencyToken()
                .HasDefaultValue(0);
        });
    }
}
