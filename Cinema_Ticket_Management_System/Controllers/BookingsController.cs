using Cinema_Ticket_Management_System.Data;
using Cinema_Ticket_Management_System.Models;
using Cinema_Ticket_Management_System.Services;
using Cinema_Ticket_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Cinema_Ticket_Management_System.Controllers;

public class BookingsController(CinemaDbContext db, BookingService bookings) : Controller
{
    public async Task<IActionResult> Index(string? search, string? status)
    {
        var query = db.Bookings.AsNoTracking().Include(b => b.Seats).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim(); var term = SearchText.Fold(search);
            var numeric = int.TryParse(search.Replace("CD-", "", StringComparison.OrdinalIgnoreCase), out var id);
            query = query.Where(b =>
                EF.Functions.Collate(b.CustomerName.Replace("Đ", "D").Replace("đ", "d"), SearchText.Collation).Contains(term) ||
                EF.Functions.Collate(b.MovieTitle, SearchText.Collation).Contains(term) || (numeric && b.Id == id));
        }
        if (Enum.TryParse<BookingStatus>(status, out var parsed) && Enum.IsDefined(parsed)) query = query.Where(b => b.Status == parsed);
        return View(new BookingsViewModel(await query.OrderByDescending(b => b.CreatedAtUtc).ThenByDescending(b => b.Id).Take(200).ToListAsync(), search, status));
    }
    public async Task<IActionResult> Create(int showtimeId)
    {
        var form = await Populate(new BookingForm { ShowtimeId = showtimeId });
        return form is null ? NotFound() : View(form);
    }
    [HttpPost]
    public async Task<IActionResult> Create(BookingForm form)
    {
        if (ModelState.IsValid)
        {
            try { var id = await bookings.CreateAsync(form.ShowtimeId, form.Version, form.CustomerName, form.SeatIds); TempData["Success"] = "Booking confirmed. Your seats are reserved."; return RedirectToAction(nameof(Details), new { id }); }
            catch (CinemaRuleException e) { ModelState.AddModelError("", e.Message); }
            catch (DbUpdateException) { ModelState.AddModelError("", "Availability changed during checkout. Review the seats and current price, then try again."); }
        }
        db.ChangeTracker.Clear();
        var current = await Populate(form);
        // A stale form gets the latest version. Keep the customer and still-available selections.
        ModelState.Remove(nameof(BookingForm.Version));
        return current is null ? NotFound() : View(current);
    }
    private async Task<BookingForm?> Populate(BookingForm form)
    {
        var show = await db.Showtimes.AsNoTracking().Include(s => s.Movie).Include(s => s.Hall).SingleOrDefaultAsync(s => s.Id == form.ShowtimeId);
        if (show is null) return null;
        form.Showtime = show; form.Version = show.Version;
        form.Seats = await db.Seats.AsNoTracking().Where(s => s.HallId == show.HallId).OrderBy(s => s.Row).ThenBy(s => s.Number).ToListAsync();
        form.TakenSeatIds = (await db.BookingSeats.AsNoTracking().Where(s => s.ShowtimeId == show.Id && s.IsActive).Select(s => s.SeatId).ToListAsync()).ToHashSet();
        form.SeatIds = form.SeatIds.Where(id => form.Seats.Any(s => s.Id == id) && !form.TakenSeatIds.Contains(id)).Distinct().ToList();
        return form;
    }
    public async Task<IActionResult> Details(int id)
    {
        var booking = await db.Bookings.AsNoTracking().Include(b => b.Seats).Include(b => b.Showtime).ThenInclude(s => s.Movie).SingleOrDefaultAsync(b => b.Id == id);
        return booking is null ? NotFound() : View(booking);
    }
    public async Task<IActionResult> Cancel(int id)
    {
        var booking = await db.Bookings.AsNoTracking().Include(b => b.Seats).SingleOrDefaultAsync(b => b.Id == id);
        return booking is null ? NotFound() : View(booking);
    }
    [HttpPost, ActionName("Cancel")]
    public async Task<IActionResult> CancelConfirmed(int id)
    {
        try { await bookings.CancelAsync(id); TempData["Success"] = "Booking cancelled. The seats are available again."; }
        catch (CinemaRuleException e) { TempData["Error"] = e.Message; }
        catch (DbUpdateException) { TempData["Error"] = "This booking changed in another window. Reload before trying again."; }
        return RedirectToAction(nameof(Details), new { id });
    }
}
