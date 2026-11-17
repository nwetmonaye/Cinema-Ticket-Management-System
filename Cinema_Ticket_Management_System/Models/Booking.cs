using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Cinema_Ticket_Management_System.Models;

public enum BookingStatus { Confirmed, Cancelled }
public class Booking
{
    public int Id { get; set; }
    public int ShowtimeId { get; set; }
    public Showtime Showtime { get; set; } = null!;
    [Required, StringLength(80)] public string CustomerName { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public BookingStatus Status { get; set; } = BookingStatus.Confirmed;
    // These snapshots keep an existing ticket independent of later movie/price edits.
    [Required, StringLength(100)] public string MovieTitle { get; set; } = "";
    [Required, StringLength(50)] public string HallName { get; set; } = "";
    public DateTime StartsAtUtc { get; set; }
    [ConcurrencyCheck] public Guid Version { get; set; } = Guid.NewGuid();
    public List<BookingSeat> Seats { get; set; } = [];
    [NotMapped] public decimal Total => Seats.Sum(s => s.UnitPrice);
    [NotMapped] public string Reference => $"CD-{Id:D5}";
}
public class BookingSeat
{
    public int Id { get; set; }
    public int BookingId { get; set; }
    public Booking Booking { get; set; } = null!;
    public int ShowtimeId { get; set; }
    public Showtime Showtime { get; set; } = null!;
    public int SeatId { get; set; }
    public Seat Seat { get; set; } = null!;
    [Required, StringLength(5)] public string SeatLabel { get; set; } = "";
    public decimal UnitPrice { get; set; }
    // Cancelled ticket rows remain in history but stop occupying the seat.
    public bool IsActive { get; set; } = true;
}
