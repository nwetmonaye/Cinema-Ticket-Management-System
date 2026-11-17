using System.Globalization;
using System.Text;
namespace Cinema_Ticket_Management_System.Services;

// Accent-insensitive search: "nguyen" finds "Nguyễn", "do khanh" finds "Đỗ Khánh".
public static class SearchText
{
    // SQL Server collation that ignores case (CI) and accents (AI).
    public const string Collation = "Latin1_General_100_CI_AI";

    // Removes diacritics in C# so the search term matches the collated column.
    // Đ/đ is a separate letter (not an accent), so it is mapped to D/d explicitly here and in the query.
    public static string Fold(string text)
    {
        var decomposed = text.Trim().Replace('Đ', 'D').Replace('đ', 'd').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark) builder.Append(c);
        return builder.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
    }
}
