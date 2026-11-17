using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
namespace Cinema_Ticket_Management_System.Models;

public class Hall
{
    public int Id { get; set; }
    [Required, StringLength(50)] public string Name { get; set; } = "";
    // Serializes schedule changes to protect against overlapping shows created concurrently.
    [ConcurrencyCheck] public Guid Version { get; set; } = Guid.NewGuid();
    public List<Seat> Seats { get; set; } = [];
    public List<Showtime> Showtimes { get; set; } = [];
}
public class Seat
{
    public int Id { get; set; }
    public int HallId { get; set; }
    public Hall Hall { get; set; } = null!;
    [Required, StringLength(1)] public string Row { get; set; } = "";
    public int Number { get; set; }
    [NotMapped] public string Label => $"{Row}{Number}";
}
