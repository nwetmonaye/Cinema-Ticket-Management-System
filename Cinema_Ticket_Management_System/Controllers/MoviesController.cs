using Cinema_Ticket_Management_System.Data;
using Cinema_Ticket_Management_System.Models;
using Cinema_Ticket_Management_System.Services;
using Cinema_Ticket_Management_System.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace Cinema_Ticket_Management_System.Controllers;

public class MoviesController(CinemaDbContext db, CinemaDisplay display) : Controller
{
    // show: "current" (default: now showing + coming soon), "now", "soon", "ended", "all".
    // Ended movies are hidden from the list, not deleted, so their tickets and booking history stay intact.
    public async Task<IActionResult> Index(string? search, string? genre, string show = "current")
    {
        var today = DateOnly.FromDateTime(display.LocalNow);
        var query = db.Movies.AsNoTracking().Include(m => m.Showtimes).AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim(); var term = SearchText.Fold(search);
            query = query.Where(m => EF.Functions.Collate(m.Title.Replace("Đ", "D").Replace("đ", "d"), SearchText.Collation).Contains(term));
        }
        if (!string.IsNullOrWhiteSpace(genre)) query = query.Where(m => m.Genre == genre);
        query = show switch {
            "now" => query.Where(m => m.ShowingStart <= today && m.ShowingEnd >= today),
            "soon" => query.Where(m => m.ShowingStart > today),
            "ended" => query.Where(m => m.ShowingEnd < today),
            "all" => query,
            _ => query.Where(m => m.ShowingEnd >= today)
        };
        if (show is not ("now" or "soon" or "ended" or "all")) show = "current";
        var movies = await query.OrderBy(m => m.ShowingStart > today).ThenBy(m => m.Title).ToListAsync();
        return View(new MovieListViewModel(movies, search, genre,
            await db.Movies.AsNoTracking().Where(m => m.ShowingEnd >= today).Select(m => m.Genre).Distinct().OrderBy(g => g).ToListAsync(),
            show, today, await db.Movies.CountAsync(m => m.ShowingEnd < today)));
    }
    public IActionResult Create()
    {
        var today = DateOnly.FromDateTime(display.LocalNow);
        return View("Form", new MovieForm { ShowingStart = today, ShowingEnd = today.AddDays(28) });
    }
    [HttpPost]
    public async Task<IActionResult> Create(MovieForm form) => await Save(form, null);
    public async Task<IActionResult> Edit(int id)
    {
        var movie = await db.Movies.AsNoTracking().SingleOrDefaultAsync(m => m.Id == id);
        return movie is null ? NotFound() : View("Form", new MovieForm {
            Id = movie.Id, Title = movie.Title, Genre = movie.Genre, Description = movie.Description,
            DurationMinutes = movie.DurationMinutes, PosterUrl = movie.PosterUrl,
            ShowingStart = movie.ShowingStart, ShowingEnd = movie.ShowingEnd, Version = movie.Version
        });
    }
    [HttpPost]
    public async Task<IActionResult> Edit(int id, MovieForm form)
    {
        if (id != form.Id) return BadRequest();
        var movie = await db.Movies.FindAsync(id);
        return movie is null ? NotFound() : await Save(form, movie);
    }
    private async Task<IActionResult> Save(MovieForm form, Movie? movie)
    {
        if (!string.IsNullOrWhiteSpace(form.Title) && await db.Movies.AnyAsync(m => m.Id != form.Id && m.Title == form.Title.Trim()))
            ModelState.AddModelError(nameof(form.Title), "A movie with this title already exists.");
        // Shortening a run must not leave upcoming showings outside it.
        if (movie is not null && form.ShowingStart is { } runStart && form.ShowingEnd is { } runEnd)
        {
            var upcoming = await db.Showtimes.AsNoTracking().Where(s => s.MovieId == movie.Id && s.StartsAtUtc > DateTime.UtcNow).Select(s => s.StartsAtUtc).ToListAsync();
            var outside = upcoming.Select(u => DateOnly.FromDateTime(display.Local(u))).Where(d => d < runStart || d > runEnd).Order().ToList();
            if (outside.Count > 0)
                ModelState.AddModelError(nameof(form.ShowingEnd), $"{outside.Count} upcoming showing(s) fall outside this period ({outside[0]:dd MMM} – {outside[^1]:dd MMM}). Delete or move them first, or keep the period wider.");
        }
        if (ModelState.IsValid)
        {
            if (movie is null) { movie = new Movie(); db.Movies.Add(movie); }
            else db.Entry(movie).Property(m => m.Version).OriginalValue = form.Version;
            movie.Title = form.Title.Trim(); movie.Genre = form.Genre.Trim(); movie.Description = form.Description?.Trim() ?? "";
            movie.DurationMinutes = form.DurationMinutes; movie.PosterUrl = string.IsNullOrWhiteSpace(form.PosterUrl) ? null : form.PosterUrl.Trim();
            movie.ShowingStart = form.ShowingStart!.Value; movie.ShowingEnd = form.ShowingEnd!.Value; movie.Version = Guid.NewGuid();
            try { await db.SaveChangesAsync(); TempData["Success"] = "Movie saved to the collection."; return RedirectToAction(nameof(Index)); }
            catch (DbUpdateException) { ModelState.AddModelError("", "This movie changed or its title is already in use. Reopen the form before trying again."); }
        }
        return View("Form", form);
    }
    public async Task<IActionResult> Delete(int id)
    {
        var movie = await db.Movies.AsNoTracking().Include(m => m.Showtimes).SingleOrDefaultAsync(m => m.Id == id);
        return movie is null ? NotFound() : View(movie);
    }
    [HttpPost, ActionName("Delete")]
    public async Task<IActionResult> DeleteConfirmed(int id, Guid version)
    {
        var movie = await db.Movies.FindAsync(id);
        if (movie is null) return NotFound();
        if (await db.Showtimes.AnyAsync(s => s.MovieId == id)) { TempData["Error"] = "Remove this movie's unused showtimes first. Showtimes with bookings must remain in history."; return RedirectToAction(nameof(Index)); }
        db.Entry(movie).Property(m => m.Version).OriginalValue = version;
        db.Movies.Remove(movie);
        try { await db.SaveChangesAsync(); TempData["Success"] = "Movie deleted."; }
        catch (DbUpdateException) { TempData["Error"] = "The movie changed or now has a showing. Reload and try again."; }
        return RedirectToAction(nameof(Index));
    }
}
