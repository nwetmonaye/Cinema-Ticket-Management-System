using System.ComponentModel.DataAnnotations;
using Cinema_Ticket_Management_System.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
namespace Cinema_Ticket_Management_System.ViewModels;

public class MovieForm : IValidatableObject
{
    public int Id { get; set; }
    [Required, StringLength(100)] public string Title { get; set; } = "";
    [Required, StringLength(40)] public string Genre { get; set; } = "";
    [StringLength(600)] public string? Description { get; set; }
    [Range(30, 300)] [Display(Name = "Duration (minutes)")] public int DurationMinutes { get; set; } = 120;
    [StringLength(500), Url] [Display(Name = "Poster image URL")] public string? PosterUrl { get; set; }
    [Required(ErrorMessage = "Choose the first day of the run.")] [Display(Name = "Showing from")] public DateOnly? ShowingStart { get; set; }
    [Required(ErrorMessage = "Choose the last day of the run.")] [Display(Name = "Showing until")] public DateOnly? ShowingEnd { get; set; }
    public Guid Version { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (ShowingStart is { } start && ShowingEnd is { } end && end < start)
            yield return new ValidationResult("The end date must be on or after the start date.", [nameof(ShowingEnd)]);
    }
}
public class ShowtimeForm
{
    public int Id { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Choose a movie.")] [Display(Name = "Movie")] public int MovieId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Choose a hall.")] [Display(Name = "Hall")] public int HallId { get; set; }
    [Required] [Display(Name = "Start date and time")] public DateTime? StartsAtLocal { get; set; }
    [Range(typeof(decimal), "1", "10000000"), WholeDong] [Display(Name = "Ticket price")] public decimal TicketPrice { get; set; } = 80000m;
    public Guid Version { get; set; }
    public bool HasBookings { get; set; }
    public List<Movie> Movies { get; set; } = [];
    public List<Hall> Halls { get; set; } = [];
}
// Add showing: one movie in one hall on several dates, at several start times, each time with its own price.
public class ShowtimeBatchForm
{
    [Range(1, int.MaxValue, ErrorMessage = "Choose a movie.")] [Display(Name = "Movie")] public int MovieId { get; set; }
    [Range(1, int.MaxValue, ErrorMessage = "Choose a hall.")] [Display(Name = "Hall")] public int HallId { get; set; }
    // Dates as yyyy-MM-dd, times as HH:mm. Times[i] is sold at Prices[i].
    public List<string> Dates { get; set; } = [];
    public List<string> Times { get; set; } = [];
    public List<decimal> Prices { get; set; } = [];
    [Display(Name = "Skip conflicting slots")] public bool SkipConflicts { get; set; }
    [BindNever, ValidateNever] public List<Movie> Movies { get; set; } = [];
    [BindNever, ValidateNever] public List<Hall> Halls { get; set; } = [];
}
public record BatchResult(int Created, List<string> Skipped);
public class BookingForm
{
    public int ShowtimeId { get; set; }
    public Guid Version { get; set; }
    [Required, StringLength(80)] [Display(Name = "Customer name")] public string CustomerName { get; set; } = "";
    [MinLength(1, ErrorMessage = "Select at least one seat."), MaxLength(8, ErrorMessage = "Choose up to 8 seats per booking.")]
    public List<int> SeatIds { get; set; } = [];
    [BindNever, ValidateNever] public Showtime Showtime { get; set; } = null!;
    [BindNever, ValidateNever] public List<Seat> Seats { get; set; } = [];
    [BindNever, ValidateNever] public HashSet<int> TakenSeatIds { get; set; } = [];
}
public class WholeDongAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not decimal number || decimal.Truncate(number) == number;
    public override string FormatErrorMessage(string name) => $"{name} must be a whole amount in VND (no decimal places).";
}
public record MovieListViewModel(List<Movie> Movies, string? Search, string? Genre, List<string> Genres, string Show, DateOnly Today, int EndedCount);
public record ShowtimesViewModel(List<Showtime> Showtimes, List<Movie> Movies, int? MovieId, string? Date, string Scope);
public record BookingsViewModel(List<Booking> Bookings, string? Search, string? Status);
public class DashboardViewModel
{
    public decimal TodayRevenue { get; set; }
    public int TodayTickets { get; set; }
    public int UpcomingCount { get; set; }
    public int MovieCount { get; set; }
    public Movie? Featured { get; set; }
    public List<Showtime> FeaturedShows { get; set; } = [];
    public List<NowShowingItem> NowShowing { get; set; } = [];
    public List<Showtime> TodayShows { get; set; } = [];
    public Showtime? Preview { get; set; }
    public List<Booking> RecentBookings { get; set; } = [];
    public List<DailySales> Week { get; set; } = [];
}
public record DailySales(DateTime Day, decimal Revenue);
public record NowShowingItem(Movie Movie, Showtime? NextShow);
