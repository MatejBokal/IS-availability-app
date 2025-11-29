namespace AvailabilityCollector.Data;

using Microsoft.EntityFrameworkCore;
using AvailabilityCollector.Models;

public class AppContextDb : DbContext
{
    public AppContextDb(DbContextOptions<AppContextDb> options)
        : base(options)
    {
    }

    // DbSets = tables in the database
    public DbSet<Worker> Workers { get; set; }
    public DbSet<Razpolozljivost> Razpolozljivosti { get; set; }
    public DbSet<Matrica> Matrice { get; set; }
    public DbSet<UrnikRazpolozljivost> UrnikiRazpolozljivosti { get; set; }
    public DbSet<UrnikKoncni> UrnikiKoncni { get; set; }
    public DbSet<Status> Statusi { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Maps classes to table names
        modelBuilder.Entity<Worker>().ToTable("Worker");
        modelBuilder.Entity<Razpolozljivost>().ToTable("Razpolozljivost");
        modelBuilder.Entity<Matrica>().ToTable("Matrica");
        modelBuilder.Entity<UrnikRazpolozljivost>().ToTable("UrnikRazpolozljivost");
        modelBuilder.Entity<UrnikKoncni>().ToTable("UrnikKoncni");
        modelBuilder.Entity<Status>().ToTable("Status");
    }
}
