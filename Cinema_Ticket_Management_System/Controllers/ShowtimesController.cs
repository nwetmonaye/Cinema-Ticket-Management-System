using System.Globalization;
using Cinema_Ticket_Management_System.Data;
using Cinema_Ticket_Management_System.Models;
using Cinema_Ticket_Management_System.Services;
using Cinema_Ticket_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Cinema_Ticket_Management_System.Controllers;

public class ShowtimesController(CinemaDbContext db, ScheduleService schedule, CinemaDisplay display, PricingService pricing) : Controller
{
    public async Task<IActionResult> Index(int? movieId, string? date, string scope = "upcoming")
    {
        var query = db.Showtimes.AsNoTracking().Include(s => s.Movie).Include(s => s.Hall).ThenInclude(h => h.Seats)
            .Include(s => s.Reservations).AsSplitQuery().AsQueryable();
        var now = DateTime.UtcNow;
        // A specific date shows that whole day (past and upcoming), so the scope only applies without a date.
        var hasDate = DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
        if (hasDate) scope = "all";
        else if (scope == "past") query = query.Where(s => s.StartsAtUtc <= now);
        else if (scope != "all") { scope = "upcoming"; query = query.Where(s => s.StartsAtUtc > now); }
        if (movieId.HasValue) query = query.Where(s => s.MovieId == movieId);
        if (DateTime.TryParseExact(date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var day))
        {
            var from = display.Utc(day); var until = display.Utc(day.AddDays(1));
            query = query.Where(s => s.StartsAtUtc >= from && s.StartsAtUtc < until);
        }
        return View(new ShowtimesViewModel(await query.OrderBy(s => s.StartsAtUtc).ToListAsync(),
            await db.Movies.AsNoTracking().OrderBy(m => m.Title).ToListAsync(), movieId, date, scope));
    }
    public async Task<IActionResult> Create(int? movieId)
    {
        var hall = await db.Halls.AsNoTracking().OrderBy(h => h.Id).FirstOrDefaultAsync();
        var imax = hall is not null && PricingService.IsImax(hall);
        // Start with a typical day: matinee, afternoon and two evening shows, priced by time of day.
        string[] times = ["10:00", "14:00", "18:30", "21:30"];
        return View("Create", await PopulateBatch(new ShowtimeBatchForm {
            MovieId = movieId ?? 0, HallId = hall?.Id ?? 0,
            Dates = [display.LocalNow.Date.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)],
            Times = [.. times], Prices = [.. times.Select(t => pricing.Suggest(TimeSpan.Parse(t, CultureInfo.InvariantCulture), imax))]
        }));
    }
    [HttpPost]
    public async Task<IActionResult> Create(ShowtimeBatchForm form)
    {
        if (ModelState.IsValid)
        {
            try
            {
                var result = await schedule.CreateManyAsync(form);
                TempData["Success"] = $"Added {result.Created} showing(s) to the schedule." +
                    (result.Skipped.Count > 0 ? $" Skipped {result.Skipped.Count} conflicting slot(s)." : "");
                return RedirectToAction(nameof(Index), new { movieId = form.MovieId });
            }
            catch (CinemaRuleException e) { ModelState.AddModelError("", e.Message); }
            catch (DbUpdateException) { ModelState.AddModelError("", "The schedule for this hall changed in another window. Review the slots and try again."); }
        }
        db.ChangeTracker.Clear();
        return View("Create", await PopulateBatch(form));
    }
    private async Task<ShowtimeBatchForm> PopulateBatch(ShowtimeBatchForm form)
    {
        // Only movies whose run hasn't ended can be scheduled.
        var today = DateOnly.FromDateTime(display.LocalNow);
        form.Movies = await db.Movies.AsNoTracking().Where(m => m.ShowingEnd >= today).OrderBy(m => m.Title).ToListAsync();
        form.Halls = await db.Halls.AsNoTracking().Include(h => h.Seats).OrderBy(h => h.Name).ToListAsync();
        return form;
    }
    public async Task<IActionResult> Edit(int id)
    {
        var show = await db.Showtimes.AsNoTracking().SingleOrDefaultAsync(s => s.Id == id);
        return show is null ? NotFound() : View("Form", await Populate(new ShowtimeForm {
            Id = show.Id, MovieId = show.MovieId, HallId = show.HallId, TicketPrice = show.TicketPrice,
            StartsAtLocal = display.Local(show.StartsAtUtc), Version = show.Version
        }));
    }
    [HttpPost]
    public async Task<IActionResult> Edit(int id, ShowtimeForm form)
    {
        if (id != form.Id) return BadRequest();
        if (!await db.Showtimes.AnyAsync(s => s.Id == id)) return NotFound();
        return await Save(form);
    }
    private async Task<IActionResult> Save(ShowtimeForm form)
    {
        if (ModelState.IsValid)
        {
            try { await schedule.SaveAsync(form); TempData["Success"] = "Showing saved to the schedule."; return RedirectToAction(nameof(Index)); }
            catch (CinemaRuleException e) { ModelState.AddModelError("", e.Message); }
            catch (DbUpdateException) { ModelState.AddModelError("", "The schedule changed in another window. Reload this form and review availability before saving."); }
        }
        return View("Form", await Populate(form));
    }
    private async Task<ShowtimeForm> Populate(ShowtimeForm form)
    {
        form.Movies = await db.Movies.AsNoTracking().OrderBy(m => m.Title).ToListAsync();
        form.Halls = await db.Halls.AsNoTracking().Include(h => h.Seats).OrderBy(h => h.Name).ToListAsync();
        form.HasBookings = form.Id != 0 && await db.Bookings.AnyAsync(b => b.ShowtimeId == form.Id);
        return form;
    }
    public async Task<IActionResult> Delete(int id)
    {
        var show = await db.Showtimes.AsNoTracking().Include(s => s.Movie).Include(s => s.Hall).Include(s => s.Bookings).SingleOrDefaultAsync(s => s.Id == id);
        return show is null ? NotFound() : View(show);
    }
    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id, Guid version)
    {
        try { await schedule.DeleteAsync(id, version); TempData["Success"] = "Showing deleted."; }
        catch (CinemaRuleException e) { TempData["Error"] = e.Message; }
        catch (DbUpdateException) { TempData["Error"] = "This showing changed or now has booking history. Reload and try again."; }
        return RedirectToAction(nameof(Index));
    }
}
