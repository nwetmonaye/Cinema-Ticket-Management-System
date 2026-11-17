using Cinema_Ticket_Management_System.Models;
namespace Cinema_Ticket_Management_System.Services;

// Time-of-day ticket pricing, configured under "Cinema:Pricing" in appsettings.json.
public class PricingOptions
{
    public TimeSpan AfternoonFrom { get; set; } = new(12, 0, 0);
    public TimeSpan EveningFrom { get; set; } = new(18, 0, 0);
    public decimal Morning { get; set; } = 75000m;
    public decimal Afternoon { get; set; } = 90000m;
    public decimal Evening { get; set; } = 110000m;
    public decimal ImaxSurcharge { get; set; } = 70000m;
}

public record PriceTier(string Name, string Range, decimal Standard, decimal Imax);

public class PricingService
{
    public PricingOptions Options { get; }
    public PricingService(IConfiguration config)
        => Options = config.GetSection("Cinema:Pricing").Get<PricingOptions>() ?? new PricingOptions();

    public static bool IsImax(Hall hall) => hall.Name.Contains("IMAX", StringComparison.OrdinalIgnoreCase);

    public string TierName(TimeSpan localTime) =>
        localTime < Options.AfternoonFrom ? "Morning" : localTime < Options.EveningFrom ? "Afternoon" : "Evening";

    // Suggested price for a showing starting at this local time of day in this hall.
    public decimal Suggest(TimeSpan localTime, bool imax)
    {
        var basePrice = localTime < Options.AfternoonFrom ? Options.Morning
            : localTime < Options.EveningFrom ? Options.Afternoon : Options.Evening;
        return basePrice + (imax ? Options.ImaxSurcharge : 0m);
    }

    public List<PriceTier> Tiers()
    {
        string T(TimeSpan t) => t.ToString(@"hh\:mm");
        return [
            new("Morning", $"before {T(Options.AfternoonFrom)}", Options.Morning, Options.Morning + Options.ImaxSurcharge),
            new("Afternoon", $"{T(Options.AfternoonFrom)} – {T(Options.EveningFrom)}", Options.Afternoon, Options.Afternoon + Options.ImaxSurcharge),
            new("Evening", $"from {T(Options.EveningFrom)}", Options.Evening, Options.Evening + Options.ImaxSurcharge)
        ];
    }
}
