using HAGSS.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HAGSS.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Reservation> Reservations => Set<Reservation>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(256).IsRequired();
        });

        modelBuilder.Entity<Seat>(entity =>
        {
            entity.HasKey(s => s.Id);
            entity.Property(s => s.Row).HasMaxLength(8).IsRequired();
            entity.Property(s => s.Number).HasMaxLength(8).IsRequired();
            entity.Property(s => s.RowVersion).IsRowVersion();
            entity.HasIndex(s => new { s.EventId, s.Row, s.Number }).IsUnique();
            entity.HasOne(s => s.Event)
                .WithMany(e => e.Seats)
                .HasForeignKey(s => s.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Reservation>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.CustomerEmail).HasMaxLength(320).IsRequired();
            entity.HasIndex(r => r.SeatId)
                .IsUnique()
                .HasFilter("\"Status\" IN (0, 1)");
            entity.HasOne(r => r.Seat)
                .WithMany()
                .HasForeignKey(r => r.SeatId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
