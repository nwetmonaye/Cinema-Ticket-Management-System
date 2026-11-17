using Cinema_Ticket_Management_System.Data;
using Cinema_Ticket_Management_System.Models;
using Cinema_Ticket_Management_System.ViewModels;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
namespace Cinema_Ticket_Management_System.Services;

public class ScheduleService(CinemaDbContext db, CinemaDisplay display)
{
    public async Task SaveAsync(ShowtimeForm form)
    {
        var movie = await db.Movies.FindAsync(form.MovieId) ?? throw new CinemaRuleException("Choose an existing movie.");
        var hall = await db.Halls.FindAsync(form.HallId) ?? throw new CinemaRuleException("Choose an existing hall.");
        if (form.StartsAtLocal is null) throw new CinemaRuleException("Choose a start date and time.");
        if (display.Zone.IsInvalidTime(form.StartsAtLocal.Value) || display.Zone.IsAmbiguousTime(form.StartsAtLocal.Value))
            throw new CinemaRuleException("This time is invalid or ambiguous in the cinema timezone. Choose another time.");
        if (!movie.IsRunningOn(DateOnly.FromDateTime(form.StartsAtLocal.Value)))
            throw new CinemaRuleException($"{movie.Title} is only showing from {movie.ShowingStart:dd MMM yyyy} to {movie.ShowingEnd:dd MMM yyyy}. Pick a date in that period, or extend the run on the movie page.");
        var start = display.Utc(form.StartsAtLocal.Value);
        if (start <= DateTime.UtcNow) throw new CinemaRuleException("A showing must start in the future.");
        var show = form.Id == 0 ? new Showtime() : await db.Showtimes.FindAsync(form.Id)
            ?? throw new CinemaRuleException("Showing not found.");
        if (show.Id != 0 && show.StartsAtUtc <= DateTime.UtcNow) throw new CinemaRuleException("A past showing cannot be edited.");
        var duration = movie.DurationMinutes;
        if (show.Id != 0 && await db.Bookings.AnyAsync(b => b.ShowtimeId == show.Id))
        {
            if (show.MovieId != form.MovieId || show.HallId != form.HallId || show.StartsAtUtc != start)
                throw new CinemaRuleException("This showing has booking history. Only its ticket price can be changed.");
            duration = show.DurationMinutes;
        }
        var end = start.AddMinutes(duration);
        var earliest = start.AddMinutes(-300);
        var neighbours = await db.Showtimes.AsNoTracking().Where(s => s.Id != form.Id && s.HallId == hall.Id && s.StartsAtUtc < end && s.StartsAtUtc > earliest).ToListAsync();
        if (neighbours.Any(s => s.EndsAtUtc > start)) throw new CinemaRuleException("This hall already has a showing during that time. Choose a non-overlapping slot.");
        if (show.Id == 0) db.Showtimes.Add(show);
        else db.Entry(show).Property(s => s.Version).OriginalValue = form.Version;
        show.MovieId = movie.Id; show.HallId = hall.Id; show.StartsAtUtc = start;
        show.DurationMinutes = duration; show.TicketPrice = form.TicketPrice; show.Version = Guid.NewGuid();
        hall.Version = Guid.NewGuid();
        // A conflicting schedule update changes Hall.Version, making this entire save fail safely.
        await db.SaveChangesAsync();
    }

    // Creates one showing per (date, time) pair. All-or-nothing unless SkipConflicts is set,
    // in which case past or overlapping slots are left out and the rest are saved.
    public async Task<BatchResult> CreateManyAsync(ShowtimeBatchForm form)
    {
        var movie = await db.Movies.FindAsync(form.MovieId) ?? throw new CinemaRuleException("Choose an existing movie.");
        var hall = await db.Halls.FindAsync(form.HallId) ?? throw new CinemaRuleException("Choose an existing hall.");
        var dates = new SortedSet<DateTime>();
        foreach (var text in form.Dates)
            if (DateTime.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) dates.Add(d);
            else throw new CinemaRuleException($"“{text}” is not a valid date.");
        if (dates.Count == 0) throw new CinemaRuleException("Select at least one date.");
        if (dates.Count > 31) throw new CinemaRuleException("Select up to 31 dates at a time.");
        if (form.Times.Count == 0) throw new CinemaRuleException("Add at least one start time.");
        if (form.Times.Count != form.Prices.Count) throw new CinemaRuleException("Each start time needs a ticket price.");
        if (form.Times.Count > 12) throw new CinemaRuleException("Add up to 12 start times at a time.");
        var slots = new SortedDictionary<TimeSpan, decimal>();
        for (var i = 0; i < form.Times.Count; i++)
        {
            if (!TimeSpan.TryParseExact(form.Times[i], @"hh\:mm", CultureInfo.InvariantCulture, out var time) || time >= TimeSpan.FromDays(1))
                throw new CinemaRuleException($"“{form.Times[i]}” is not a valid start time.");
            var price = form.Prices[i];
            if (price < 1 || price > 10000000 || decimal.Truncate(price) != price)
                throw new CinemaRuleException($"The price for {form.Times[i]} must be a whole amount between 1 and 10,000,000 VND.");
            if (!slots.TryAdd(time, price)) throw new CinemaRuleException($"{form.Times[i]} is listed twice.");
        }

        var duration = movie.DurationMinutes;
        var now = DateTime.UtcNow;
        var candidates = new List<(DateTime Local, DateTime Start, DateTime End, decimal Price)>();
        var skipped = new List<string>();
        foreach (var date in dates)
            foreach (var (time, price) in slots)
            {
                var local = date + time;
                if (!movie.IsRunningOn(DateOnly.FromDateTime(date))) { skipped.Add($"{local.ToString("ddd dd MMM HH:mm", CultureInfo.InvariantCulture)} – outside {movie.Title}'s run ({movie.ShowingStart:dd MMM} – {movie.ShowingEnd:dd MMM})"); continue; }
                var label = local.ToString("ddd dd MMM HH:mm", CultureInfo.InvariantCulture);
                if (display.Zone.IsInvalidTime(local) || display.Zone.IsAmbiguousTime(local)) { skipped.Add($"{label} – not a valid local time"); continue; }
                var start = display.Utc(local);
                if (start <= now) { skipped.Add($"{label} – already in the past"); continue; }
                candidates.Add((local, start, start.AddMinutes(duration), price));
            }

        // Existing shows in this hall that could overlap any candidate (longest movie is 300 minutes).
        var accepted = new List<(DateTime Local, DateTime Start, DateTime End, decimal Price)>();
        if (candidates.Count > 0)
        {
            var from = candidates.Min(c => c.Start).AddMinutes(-300); var until = candidates.Max(c => c.End);
            var existing = await db.Showtimes.AsNoTracking().Include(s => s.Movie)
                .Where(s => s.HallId == hall.Id && s.StartsAtUtc > from && s.StartsAtUtc < until).ToListAsync();
            foreach (var c in candidates)
            {
                var label = c.Local.ToString("ddd dd MMM HH:mm", CultureInfo.InvariantCulture);
                var clash = existing.FirstOrDefault(s => s.StartsAtUtc < c.End && s.EndsAtUtc > c.Start);
                if (clash is not null) { skipped.Add($"{label} – overlaps {clash.Movie.Title} ({display.Time(clash.StartsAtUtc)}–{display.Time(clash.EndsAtUtc)})"); continue; }
                var own = accepted.FirstOrDefault(a => a.Start < c.End && a.End > c.Start);
                if (own != default) { skipped.Add($"{label} – overlaps the {own.Local:HH:mm} slot in this batch (movie runs {display.Runtime(duration)})"); continue; }
                accepted.Add(c);
            }
        }
        if (skipped.Count > 0 && !form.SkipConflicts)
            throw new CinemaRuleException($"{skipped.Count} slot(s) can't be scheduled: " + string.Join("; ", skipped.Take(6)) +
                (skipped.Count > 6 ? $"; and {skipped.Count - 6} more" : "") + ". Change those dates or times, or tick “Skip slots that overlap…” to save the rest.");
        if (accepted.Count == 0) throw new CinemaRuleException("None of the selected slots can be scheduled. Choose other dates or times.");

        foreach (var a in accepted)
            db.Showtimes.Add(new Showtime { MovieId = movie.Id, HallId = hall.Id, StartsAtUtc = a.Start, DurationMinutes = duration, TicketPrice = a.Price });
        // Changing the hall's version makes a concurrent schedule change in this hall fail instead of double-booking the screen.
        hall.Version = Guid.NewGuid();
        await db.SaveChangesAsync();
        return new BatchResult(accepted.Count, skipped);
    }

    public async Task DeleteAsync(int id, Guid version)
    {
        var show = await db.Showtimes.Include(s => s.Hall).SingleOrDefaultAsync(s => s.Id == id)
            ?? throw new CinemaRuleException("Showing not found.");
        if (await db.Bookings.AnyAsync(b => b.ShowtimeId == id)) throw new CinemaRuleException("Showings with booking history cannot be deleted.");
        db.Entry(show).Property(s => s.Version).OriginalValue = version;
        show.Hall.Version = Guid.NewGuid();
        db.Showtimes.Remove(show);
        await db.SaveChangesAsync();
    }
}
