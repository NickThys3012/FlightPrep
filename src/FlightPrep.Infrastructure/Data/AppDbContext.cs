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
    public DbSet<FlightImage> FlightImages => Set<FlightImage>();
    public DbSet<WindLevel> WindLevels => Set<WindLevel>();
    public DbSet<GoNoGoSettings> GoNoGoSettings => Set<GoNoGoSettings>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FlightPreparation>()
            .HasMany(f => f.Passengers)
            .WithOne(p => p.FlightPreparation)
            .HasForeignKey(p => p.FlightPreparationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FlightPreparation>()
            .HasMany(f => f.Images)
            .WithOne(i => i.FlightPreparation)
            .HasForeignKey(i => i.FlightPreparationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FlightPreparation>()
            .HasMany(f => f.WindLevels)
            .WithOne(w => w.FlightPreparation)
            .HasForeignKey(w => w.FlightPreparationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FlightImage>()
            .HasIndex(i => new { i.FlightPreparationId, i.Section, i.Order });

        modelBuilder.Entity<Passenger>()
            .HasIndex(p => new { p.FlightPreparationId, p.Order });

        modelBuilder.Entity<WindLevel>()
            .HasIndex(w => new { w.FlightPreparationId, w.Order });

        // Ignore computed properties
        modelBuilder.Entity<FlightPreparation>()
            .Ignore(f => f.TotaalGewicht)
            .Ignore(f => f.LiftVoldoende);

        // GoNoGo is ignored separately to suppress the [Obsolete] warning at the call site
#pragma warning disable CS0618 // GoNoGo is deliberately ignored here; use GoNoGoService.Compute() at runtime
        modelBuilder.Entity<FlightPreparation>().Ignore(f => f.GoNoGo);
#pragma warning restore CS0618

        modelBuilder.Entity<Balloon>().HasData(
            new Balloon
            {
                Id = 1,
                Registration = "OO-BUT",
                Type = "BB22N",
                VolumeM3 = 2200,
                InternalEnvelopeTempC = 80,
                EmptyWeightKg = 323.4
            }
        );
    }
}
