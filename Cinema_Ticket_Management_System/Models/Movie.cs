using System.ComponentModel.DataAnnotations;
namespace Cinema_Ticket_Management_System.Models;

public enum RunStatus { ComingSoon, NowShowing, Ended }

public class Movie
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Title { get; set; } = "";
    [Required, StringLength(40)] public string Genre { get; set; } = "";
    [StringLength(600)] public string Description { get; set; } = "";
    [Range(30, 300)] public int DurationMinutes { get; set; }
    // Poster image URL (e.g. official poster). Optional: a styled placeholder is shown when empty.
    [StringLength(500)] public string? PosterUrl { get; set; }
    // The movie's run at this cinema (cinema-local dates, inclusive). Showings can only be scheduled inside it,
    // and the movie drops off the movie list once ShowingEnd has passed.
    public DateOnly ShowingStart { get; set; }
    public DateOnly ShowingEnd { get; set; }
    [ConcurrencyCheck] public Guid Version { get; set; } = Guid.NewGuid();
    public List<Showtime> Showtimes { get; set; } = [];

    public bool IsRunningOn(DateOnly day) => day >= ShowingStart && day <= ShowingEnd;
    public RunStatus StatusOn(DateOnly today) =>
        today < ShowingStart ? RunStatus.ComingSoon : today > ShowingEnd ? RunStatus.Ended : RunStatus.NowShowing;
}
