using FlightPrep.Domain.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FlightPrep.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Balloon> Balloons => Set<Balloon>();
    public DbSet<Pilot> Pilots => Set<Pilot>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<Passenger> Passengers => Set<Passenger>();
    public DbSet<FlightPreparation> FlightPreparations => Set<FlightPreparation>();
    public DbSet<FlightImage> FlightImages => Set<FlightImage>();
    public DbSet<WindLevel> WindLevels => Set<WindLevel>();
    public DbSet<GoNoGoSettings> GoNoGoSettings => Set<GoNoGoSettings>();
    public DbSet<LoginEvent> LoginEvents { get; set; }
    public DbSet<OFPSettings> OfpSettings => Set<OFPSettings>();
    public DbSet<FlightPreparationShare> FlightPreparationShares => Set<FlightPreparationShare>();

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

        modelBuilder.Entity<Balloon>().Ignore(b => b.EmptyWeightKg);

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
                InternalEnvelopeTempC = 80
            }
        );

        modelBuilder.Entity<FlightPreparation>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(f => f.CreatedByUserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Balloon>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(b => b.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Location>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(l => l.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<Pilot>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(p => p.OwnerId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<GoNoGoSettings>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(g => g.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<GoNoGoSettings>()
            .HasIndex(g => g.UserId)
            .IsUnique();

        modelBuilder.Entity<OFPSettings>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(o => o.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<OFPSettings>()
            .HasIndex(o => o.UserId)
            .IsUnique();

        modelBuilder.Entity<FlightPreparationShare>()
            .HasOne<FlightPreparation>()
            .WithMany(f => f.Shares)
            .HasForeignKey(s => s.FlightPreparationId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FlightPreparationShare>()
            .HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(s => s.SharedWithUserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<FlightPreparationShare>()
            .HasIndex(s => new { s.FlightPreparationId, s.SharedWithUserId })
            .IsUnique();

        modelBuilder.Entity<LoginEvent>(e =>
        {
            e.HasIndex(x => x.Email);
            e.HasIndex(x => x.Timestamp);
            e.Property(x => x.FailureReason).HasMaxLength(50);
            e.Property(x => x.IpAddress).HasMaxLength(45);
            e.Property(x => x.Email).HasMaxLength(256);
        });
    }
}
