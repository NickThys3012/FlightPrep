using FlightPrep.Models;
using Microsoft.EntityFrameworkCore;

namespace FlightPrep.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Balloon> Balloons => Set<Balloon>();
    public DbSet<Pilot> Pilots => Set<Pilot>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Passenger> Passengers => Set<Passenger>();
    public DbSet<FlightPreparation> FlightPreparations => Set<FlightPreparation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FlightPreparation>()
            .HasMany(f => f.Passengers)
            .WithOne(p => p.FlightPreparation)
            .HasForeignKey(p => p.FlightPreparationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Passenger>()
            .HasIndex(p => new { p.FlightPreparationId, p.Order });

        // Ignore computed properties
        modelBuilder.Entity<FlightPreparation>()
            .Ignore(f => f.TotaalGewicht)
            .Ignore(f => f.LiftVoldoende);

        modelBuilder.Entity<Balloon>().HasData(
            new Balloon
            {
                Id = 1,
                Registration = "OO-BUT",
                Type = "BB22N",
                Volume = "2200M³",
                EmptyWeightKg = 323.4
            }
        );
    }
}
