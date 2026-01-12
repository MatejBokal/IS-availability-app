using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Data;

public class ApplicationDbContext : IdentityDbContext<IdentityUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // Business tables
    public DbSet<AvailabilityMonth> AvailabilityMonths => Set<AvailabilityMonth>();
    public DbSet<AvailabilitySubmission> AvailabilitySubmissions => Set<AvailabilitySubmission>();
    public DbSet<AvailabilityEntry> AvailabilityEntries => Set<AvailabilityEntry>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Unique constraints
        modelBuilder.Entity<AvailabilityMonth>()
            .HasIndex(m => m.MonthKey)
            .IsUnique();

        modelBuilder.Entity<AvailabilitySubmission>()
            .HasIndex(s => new { s.UserId, s.AvailabilityMonthId })
            .IsUnique();

        modelBuilder.Entity<AvailabilitySubmission>()
            .HasOne<IdentityUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
