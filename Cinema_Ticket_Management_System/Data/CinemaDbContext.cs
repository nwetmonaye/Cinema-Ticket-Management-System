using Cinema_Ticket_Management_System.Models;
using Microsoft.EntityFrameworkCore;
namespace Cinema_Ticket_Management_System.Data;

// Maps to the tables created by Database/CinemaDb.sql on SQL Server (localhost\SQLEXPRESS).
public class CinemaDbContext(DbContextOptions<CinemaDbContext> options) : DbContext(options)
{
    public DbSet<Movie> Movies => Set<Movie>();
    public DbSet<Hall> Halls => Set<Hall>();
    public DbSet<Seat> Seats => Set<Seat>();
    public DbSet<Showtime> Showtimes => Set<Showtime>();
    public DbSet<Booking> Bookings => Set<Booking>();
    public DbSet<BookingSeat> BookingSeats => Set<BookingSeat>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Movie>().HasIndex(m => m.Title).IsUnique();
        model.Entity<Seat>().HasIndex(s => new { s.HallId, s.Row, s.Number }).IsUnique();
        model.Entity<Seat>().HasOne(s => s.Hall).WithMany(h => h.Seats).HasForeignKey(s => s.HallId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Showtime>().HasOne(s => s.Movie).WithMany(m => m.Showtimes).HasForeignKey(s => s.MovieId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Showtime>().HasOne(s => s.Hall).WithMany(h => h.Showtimes).HasForeignKey(s => s.HallId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<Showtime>().HasIndex(s => new { s.HallId, s.StartsAtUtc }).IsUnique();
        model.Entity<Showtime>().Property(s => s.TicketPrice).HasColumnName("TicketPriceVnd")
            .HasConversion(value => (long)value, value => (decimal)value);
        model.Entity<Booking>().Property(b => b.Status).HasConversion<string>().HasMaxLength(20);
        model.Entity<Booking>().HasIndex(b => b.CreatedAtUtc);
        model.Entity<Booking>().HasOne(b => b.Showtime).WithMany(s => s.Bookings).HasForeignKey(b => b.ShowtimeId).OnDelete(DeleteBehavior.Restrict);
        // Composite FK also ensures each ticket line belongs to its booking's showtime.
        model.Entity<Booking>().HasAlternateKey(b => new { b.Id, b.ShowtimeId });
        model.Entity<BookingSeat>().HasOne(s => s.Booking).WithMany(b => b.Seats)
            .HasForeignKey(s => new { s.BookingId, s.ShowtimeId }).HasPrincipalKey(b => new { b.Id, b.ShowtimeId }).OnDelete(DeleteBehavior.Cascade);
        model.Entity<BookingSeat>().HasOne(s => s.Seat).WithMany().HasForeignKey(s => s.SeatId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<BookingSeat>().HasOne(s => s.Showtime).WithMany(s => s.Reservations).HasForeignKey(s => s.ShowtimeId).OnDelete(DeleteBehavior.Restrict);
        model.Entity<BookingSeat>().Property(s => s.UnitPrice).HasColumnName("UnitPriceVnd")
            .HasConversion(value => (long)value, value => (decimal)value);
        // The central booking rule: only ONE active ticket for a seat in a particular showing.
        model.Entity<BookingSeat>().HasIndex(s => new { s.ShowtimeId, s.SeatId })
            .IsUnique().HasFilter("[IsActive] = 1");
        model.Entity<Movie>().ToTable(t => {
            t.HasCheckConstraint("CK_Movie_Duration", "DurationMinutes BETWEEN 30 AND 300");
            t.HasCheckConstraint("CK_Movie_ShowingPeriod", "ShowingEnd >= ShowingStart");
        });
        model.Entity<Movie>().HasIndex(m => m.ShowingEnd);
        model.Entity<Showtime>().ToTable(t => {
            t.HasCheckConstraint("CK_Showtime_Price", "TicketPriceVnd BETWEEN 1 AND 10000000");
            t.HasCheckConstraint("CK_Showtime_Duration", "DurationMinutes BETWEEN 30 AND 300");
        });
        model.Entity<BookingSeat>().ToTable(t => t.HasCheckConstraint("CK_Ticket_Price", "UnitPriceVnd BETWEEN 1 AND 10000000"));
    }
}
