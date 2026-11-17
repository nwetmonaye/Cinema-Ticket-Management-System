using Cinema_Ticket_Management_System.Data;
using Cinema_Ticket_Management_System.Models;
using Cinema_Ticket_Management_System.Services;
using Cinema_Ticket_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Cinema_Ticket_Management_System.Controllers;

public class DashboardController(CinemaDbContext db, CinemaDisplay display) : Controller
{
    public async Task<IActionResult> Index()
    {
        var now = DateTime.UtcNow; var today = display.LocalNow.Date;
        var from = display.Utc(today.AddDays(-6)); var until = display.Utc(today.AddDays(1));
        var weekBookings = await db.Bookings.AsNoTracking().Include(b => b.Seats)
            .Where(b => b.CreatedAtUtc >= from && b.CreatedAtUtc < until && b.Status == BookingStatus.Confirmed).ToListAsync();
        var todays = weekBookings.Where(b => display.Local(b.CreatedAtUtc).Date == today).ToList();

        // Shows that are playing now or start within the next 3 days.
        var earliest = now.AddMinutes(-300);
        var soon = await db.Showtimes.AsNoTracking().Include(s => s.Movie).Include(s => s.Hall).ThenInclude(h => h.Seats)
            .Include(s => s.Reservations.Where(r => r.IsActive)).AsSplitQuery()
            .Where(s => s.StartsAtUtc > earliest && s.StartsAtUtc < now.AddDays(3)).OrderBy(s => s.StartsAtUtc).ToListAsync();
        soon = soon.Where(s => s.EndsAtUtc > now).ToList();
        var upcoming = soon.Where(s => s.StartsAtUtc > now).ToList();
        // Only movies whose showing period includes today appear as "Now Showing"; ended ones drop off automatically.
        var runDay = DateOnly.FromDateTime(display.LocalNow);
        var movies = await db.Movies.AsNoTracking().Where(m => m.ShowingStart <= runDay && m.ShowingEnd >= runDay).OrderBy(m => m.Title).ToListAsync();

        // Featured = the movie with the most upcoming showings in the next 3 days.
        var featured = upcoming.GroupBy(s => s.MovieId).OrderByDescending(g => g.Count()).ThenBy(g => g.First().StartsAtUtc).FirstOrDefault();
        var preview = upcoming.FirstOrDefault(s => s.Reservations.Count < s.Hall.Seats.Count);
        var tomorrow = display.Utc(today.AddDays(1));

        return View(new DashboardViewModel {
            TodayRevenue = todays.Sum(b => b.Total), TodayTickets = todays.Sum(b => b.Seats.Count),
            UpcomingCount = await db.Showtimes.CountAsync(s => s.StartsAtUtc > now), MovieCount = movies.Count,
            Featured = featured?.First().Movie,
            FeaturedShows = featured?.Take(5).ToList() ?? [],
            NowShowing = movies.Select(m => new NowShowingItem(m, upcoming.FirstOrDefault(s => s.MovieId == m.Id)))
                .OrderBy(x => x.NextShow is null).ThenBy(x => x.NextShow?.StartsAtUtc).ToList(),
            TodayShows = soon.Where(s => s.StartsAtUtc < tomorrow).Take(7).ToList(),
            Preview = preview,
            RecentBookings = await db.Bookings.AsNoTracking().Include(b => b.Seats).OrderByDescending(b => b.CreatedAtUtc).ThenByDescending(b => b.Id).Take(6).ToListAsync(),
            Week = Enumerable.Range(-6, 7).Select(offset => new DailySales(today.AddDays(offset),
                weekBookings.Where(b => display.Local(b.CreatedAtUtc).Date == today.AddDays(offset)).Sum(b => b.Total))).ToList()
        });
    }
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() { Response.StatusCode = 500; return View(); }
}
