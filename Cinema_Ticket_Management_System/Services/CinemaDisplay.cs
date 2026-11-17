using System.Globalization;
namespace Cinema_Ticket_Management_System.Services;

public class CinemaDisplay
{
    public TimeZoneInfo Zone { get; }
    public CinemaDisplay(IConfiguration config) => Zone = TimeZoneInfo.FindSystemTimeZoneById(config["Cinema:TimeZone"] ?? "UTC");
    public DateTime LocalNow => Local(DateTime.UtcNow);
    public DateTime Local(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    public DateTime Utc(DateTime local) => TimeZoneInfo.ConvertTimeToUtc(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), Zone);
    public string Money(decimal price) => price.ToString("C0", CultureInfo.GetCultureInfo("vi-VN"));
    public string Date(DateTime utc) => Local(utc).ToString("ddd, dd MMM · HH:mm", CultureInfo.InvariantCulture);
    public string Time(DateTime utc) => Local(utc).ToString("HH:mm", CultureInfo.InvariantCulture);
    public string Day(DateTime utc) => Local(utc).ToString("dd MMM", CultureInfo.InvariantCulture);
    public string Runtime(int minutes) => $"{minutes / 60}h {minutes % 60:D2}m";
}
