using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using AvailabilityCollector.Models;

namespace AvailabilityCollector.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

           // Business tables
           public DbSet<AvailabilityMonth> AvailabilityMonths => Set<AvailabilityMonth>();
           public DbSet<AvailabilitySubmission> AvailabilitySubmissions => Set<AvailabilitySubmission>();
           public DbSet<AvailabilityEntry> AvailabilityEntries => Set<AvailabilityEntry>();
           public DbSet<Position> Positions => Set<Position>();
           public DbSet<Holiday> Holidays => Set<Holiday>();
           public DbSet<ShiftMatrix> ShiftMatrices => Set<ShiftMatrix>();
           public DbSet<PositionShift> PositionShifts => Set<PositionShift>();
           public DbSet<ShiftEntry> ShiftEntries => Set<ShiftEntry>();
           public DbSet<AppSettings> AppSettings => Set<AppSettings>();

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
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Unique constraint for Position name
        modelBuilder.Entity<Position>()
            .HasIndex(p => p.Name)
            .IsUnique();
        
        // Index for Holiday date lookups
               modelBuilder.Entity<Holiday>()
                   .HasIndex(h => new { h.Year, h.Date });

        // Unique constraint for AppSettings key
        modelBuilder.Entity<AppSettings>()
            .HasIndex(s => s.Key)
            .IsUnique();

               // ShiftMatrix relationships
               modelBuilder.Entity<PositionShift>()
                   .HasOne(ps => ps.ShiftMatrix)
                   .WithMany(sm => sm.PositionShifts)
                   .HasForeignKey(ps => ps.ShiftMatrixId)
                   .OnDelete(DeleteBehavior.Cascade);

               modelBuilder.Entity<PositionShift>()
                   .HasOne(ps => ps.Position)
                   .WithMany()
                   .HasForeignKey(ps => ps.PositionId)
                   .OnDelete(DeleteBehavior.Restrict);

               modelBuilder.Entity<ShiftEntry>()
                   .HasOne(se => se.PositionShift)
                   .WithMany(ps => ps.ShiftEntries)
                   .HasForeignKey(se => se.PositionShiftId)
                   .OnDelete(DeleteBehavior.Cascade);
    }
}
