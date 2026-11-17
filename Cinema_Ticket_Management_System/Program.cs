using Cinema_Ticket_Management_System.Data;
using Cinema_Ticket_Management_System.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews(options => options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
// Local SQL Server Express — connection string "CinemaDb" in appsettings.json.
builder.Services.AddDbContext<CinemaDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("CinemaDb")));
builder.Services.AddSingleton<CinemaDisplay>();
builder.Services.AddSingleton<PricingService>();
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<ScheduleService>();

var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CinemaDbContext>();
    if (!await db.Database.CanConnectAsync())
        throw new InvalidOperationException(
            "Cannot connect to SQL Server database 'CinemaTicketDb'. Run Database/CinemaDb.sql on localhost\\SQLEXPRESS first, " +
            "and check ConnectionStrings:CinemaDb in appsettings.json.");
    await SeedData.InitializeAsync(db, scope.ServiceProvider.GetRequiredService<CinemaDisplay>());
}
if (!app.Environment.IsDevelopment()) app.UseExceptionHandler("/Dashboard/Error");
app.UseStaticFiles();
app.UseRouting();
app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");
app.Run();
