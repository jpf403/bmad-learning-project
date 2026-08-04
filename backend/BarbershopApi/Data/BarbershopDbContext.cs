using BarbershopApi.Entities;
using Microsoft.EntityFrameworkCore;

namespace BarbershopApi.Data;

public class BarbershopDbContext(DbContextOptions<BarbershopDbContext> options) : DbContext(options)
{
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<Appointment> Appointments => Set<Appointment>();

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

        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasIndex(a => new { a.BarberId, a.Date, a.StartTime })
                .IsUnique()
                .HasFilter("CancelledAt IS NULL");

            entity.HasIndex(a => new { a.CustomerId, a.Date, a.StartTime })
                .IsUnique()
                .HasFilter("CancelledAt IS NULL");

            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(a => a.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<Account>()
                .WithMany()
                .HasForeignKey(a => a.BarberId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
