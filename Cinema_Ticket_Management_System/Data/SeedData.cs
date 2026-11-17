using Cinema_Ticket_Management_System.Models;
using Cinema_Ticket_Management_System.Services;
using Microsoft.EntityFrameworkCore;
namespace Cinema_Ticket_Management_System.Data;

// Runs on startup. Database/CinemaDb.sql creates the halls, seats and movies;
// this builds a realistic schedule (last 6 days + next 3 days) and sample bookings,
// once, while the Showtimes table is still empty.
public static class SeedData
{
    static readonly string[] Customers = [
        "Nguyễn Minh Anh", "Trần Bảo Ngọc", "Lê Hoàng Nam", "Phạm Thu Hà", "Vũ Đức Huy", "Đỗ Khánh Linh",
        "Hoàng Gia Bảo", "Bùi Thanh Tâm", "Ngô Phương Thảo", "Đặng Quốc Việt", "Phan Mai Chi", "Huỳnh Tuấn Kiệt",
        "Võ Ngọc Hân", "Trịnh Hải Đăng", "Lý Thảo Vy", "Dương Minh Khôi", "Mai Anh Thư", "Tạ Quang Vinh"
    ];

    public static async Task InitializeAsync(CinemaDbContext db, CinemaDisplay display)
    {
        if (await db.Showtimes.AnyAsync()) return;
        var movies = await db.Movies.OrderBy(m => m.Id).ToListAsync();
        var halls = await db.Halls.Include(h => h.Seats).OrderBy(h => h.Id).ToListAsync();
        if (movies.Count == 0 || halls.Count == 0) return;

        var random = new Random(2026);
        var now = DateTime.UtcNow;
        var today = display.LocalNow.Date;
        var shows = new List<Showtime>();
        for (var day = -6; day <= 3; day++)
            for (var h = 0; h < halls.Count; h++)
            {
                var running = movies.Where(m => m.IsRunningOn(DateOnly.FromDateTime(today.AddDays(day)))).ToList();
                if (running.Count == 0) continue;
                var hall = halls[h];
                var imax = hall.Name.Contains("IMAX", StringComparison.OrdinalIgnoreCase);
                var local = today.AddDays(day).AddHours(9).AddMinutes(h == 0 ? 30 : 0);
                var lastStart = today.AddDays(day).AddHours(22).AddMinutes(30);
                var k = 0;
                while (local <= lastStart)
                {
                    var movie = running[(k + h * 3 + day + 60) % running.Count];
                    var evening = local.Hour >= 18;
                    shows.Add(new Showtime {
                        Movie = movie, Hall = hall, StartsAtUtc = display.Utc(local), DurationMinutes = movie.DurationMinutes,
                        TicketPrice = imax ? (evening ? 180000m : 160000m) : (evening ? 110000m : 90000m)
                    });
                    // 20 minutes for cleaning, then round up to the next quarter hour.
                    var next = local.AddMinutes(movie.DurationMinutes + 20);
                    local = next.Date.AddMinutes(Math.Ceiling(next.TimeOfDay.TotalMinutes / 15) * 15);
                    k++;
                }
            }
        db.Showtimes.AddRange(shows);

        foreach (var show in shows.Where(s => s.StartsAtUtc <= now.AddDays(3)))
        {
            var past = show.StartsAtUtc <= now;
            var occupancy = past ? 0.25 + random.NextDouble() * 0.5 : 0.05 + random.NextDouble() * 0.4;
            var rows = show.Hall.Seats.GroupBy(s => s.Row).Select(g => g.OrderBy(s => s.Number).ToList()).ToList();
            var taken = new HashSet<Seat>();
            var target = (int)(show.Hall.Seats.Count * occupancy);
            for (var attempt = 0; taken.Count < target && attempt < 200; attempt++)
            {
                // Guests usually sit together: pick 1-4 neighbouring seats in one row.
                var size = random.Next(1, 5);
                var row = rows[random.Next(rows.Count)];
                var first = random.Next(0, row.Count - size + 1);
                var group = row.Skip(first).Take(size).ToList();
                if (group.Any(taken.Contains)) continue;
                var created = show.StartsAtUtc.AddMinutes(-random.Next(30, 72 * 60));
                if (created > now) created = now.AddMinutes(-random.Next(1, 120));
                var cancelled = random.NextDouble() < 0.05;
                var booking = new Booking {
                    Showtime = show, CustomerName = Customers[random.Next(Customers.Length)], CreatedAtUtc = created,
                    MovieTitle = show.Movie.Title, HallName = show.Hall.Name, StartsAtUtc = show.StartsAtUtc,
                    Status = cancelled ? BookingStatus.Cancelled : BookingStatus.Confirmed
                };
                foreach (var seat in group)
                    booking.Seats.Add(new BookingSeat { Showtime = show, Seat = seat, SeatLabel = seat.Label, UnitPrice = show.TicketPrice, IsActive = !cancelled });
                if (!cancelled) taken.UnionWith(group);
                db.Bookings.Add(booking);
            }
        }
        await db.SaveChangesAsync();
    }
}
