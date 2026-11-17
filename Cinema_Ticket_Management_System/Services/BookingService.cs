using Cinema_Ticket_Management_System.Data;
using Cinema_Ticket_Management_System.Models;
using Microsoft.EntityFrameworkCore;
namespace Cinema_Ticket_Management_System.Services;

public class BookingService(CinemaDbContext db)
{
    public async Task<int> CreateAsync(int showtimeId, Guid expectedVersion, string customer, List<int> seatIds)
    {
        if (string.IsNullOrWhiteSpace(customer) || customer.Trim().Length > 80)
            throw new CinemaRuleException("Enter a customer name of up to 80 characters.");
        if (seatIds.Count is < 1 or > 8) throw new CinemaRuleException("Choose between 1 and 8 seats.");
        if (seatIds.Distinct().Count() != seatIds.Count) throw new CinemaRuleException("Each seat can be selected only once.");
        var show = await db.Showtimes.Include(s => s.Movie).Include(s => s.Hall).SingleOrDefaultAsync(s => s.Id == showtimeId);
        if (show is null) throw new CinemaRuleException("This showing no longer exists.");
        if (show.StartsAtUtc <= DateTime.UtcNow) throw new CinemaRuleException("Booking is closed because this showing has already started.");
        var seats = await db.Seats.Where(s => s.HallId == show.HallId && seatIds.Contains(s.Id)).OrderBy(s => s.Row).ThenBy(s => s.Number).ToListAsync();
        if (seats.Count != seatIds.Count) throw new CinemaRuleException("Choose seats belonging to this showing's hall.");
        if (await db.BookingSeats.AnyAsync(s => s.ShowtimeId == showtimeId && s.IsActive && seatIds.Contains(s.SeatId)))
            throw new CinemaRuleException("One or more selected seats are already booked. Choose from the available seats below.");
        if (show.Version != expectedVersion)
            throw new CinemaRuleException("This showing changed. Review the current price and availability, then confirm again.");
        var booking = new Booking {
            ShowtimeId = show.Id, CustomerName = customer.Trim(), MovieTitle = show.Movie.Title,
            HallName = show.Hall.Name, StartsAtUtc = show.StartsAtUtc,
            Seats = seats.Select(seat => new BookingSeat {
                ShowtimeId = show.Id, SeatId = seat.Id, SeatLabel = seat.Label, UnitPrice = show.TicketPrice
            }).ToList()
        };
        db.Bookings.Add(booking);
        // Updating the show token also prevents an admin from moving/deleting a show during checkout.
        show.Version = Guid.NewGuid();
        // EF saves the header, seats, and show update atomically. Database uniqueness is the final guard.
        await db.SaveChangesAsync();
        return booking.Id;
    }

    public async Task CancelAsync(int id)
    {
        var booking = await db.Bookings.Include(b => b.Seats).Include(b => b.Showtime).SingleOrDefaultAsync(b => b.Id == id);
        if (booking is null) throw new CinemaRuleException("Booking not found.");
        if (booking.Status == BookingStatus.Cancelled) throw new CinemaRuleException("This booking is already cancelled. Seats were not changed.");
        if (booking.StartsAtUtc <= DateTime.UtcNow) throw new CinemaRuleException("Bookings cannot be cancelled after the showing has started.");
        booking.Status = BookingStatus.Cancelled;
        booking.Version = Guid.NewGuid();
        booking.Showtime.Version = Guid.NewGuid();
        foreach (var seat in booking.Seats) seat.IsActive = false;
        // Old rows stay as a receipt, but the filtered unique index permits a new active booking.
        await db.SaveChangesAsync();
    }
}
public class CinemaRuleException(string message) : Exception(message);
