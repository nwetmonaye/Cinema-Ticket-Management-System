using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Cinema_Ticket_Management_System.Models;

public class Showtime
{
    public int Id { get; set; }
    public int MovieId { get; set; }
    public Movie Movie { get; set; } = null!;
    public int HallId { get; set; }
    public Hall Hall { get; set; } = null!;
    public DateTime StartsAtUtc { get; set; }
    // Snapshot the duration: editing the movie later must not change an existing schedule.
    public int DurationMinutes { get; set; }
    public decimal TicketPrice { get; set; }
    [ConcurrencyCheck] public Guid Version { get; set; } = Guid.NewGuid();
    public List<Booking> Bookings { get; set; } = [];
    public List<BookingSeat> Reservations { get; set; } = [];
    [NotMapped] public DateTime EndsAtUtc => StartsAtUtc.AddMinutes(DurationMinutes);
}
