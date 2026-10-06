using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using ScripOx.Desktop.Models;

namespace ScripOx.Desktop.Services;

/// <summary>
/// Exports qualified CRM leads directly into ad platform audience formats:
/// - Meta Ads (Facebook/Instagram) Custom Audiences
/// - Google Ads Customer Match
/// - Mailchimp / Brevo / ActiveCampaign Lead Lists
/// </summary>
public class AudienceExportService
{
    /// <summary>
    /// Meta (Facebook) Custom Audience CSV specification:
    /// Headers: email,phone,fn,ln,ct,st,country
    /// Phone digits only, lowercase trimmed emails.
    /// </summary>
    public async Task ExportMetaAdsCsvAsync(IEnumerable<Company> companies, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("email,phone,fn,ln,ct,st,country");

        foreach (var c in companies)
        {
            var email = (c.Email ?? "").Trim().ToLowerInvariant();
            var phone = CleanPhone(c.Phone);
            var (fn, ln) = SplitName(c.DisplayName);
            var city = EscapeCsv(c.City ?? "");
            var state = EscapeCsv(c.Region ?? "");
            var country = StandardizeCountry(c.Country);

            // Only export if we have at least email or phone (Meta requirement)
            if (!string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(phone))
            {
                sb.AppendLine($"{EscapeCsv(email)},{EscapeCsv(phone)},{fn},{ln},{city},{state},{country}");
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// Google Ads Customer Match CSV specification:
    /// Headers: Email,Phone,First Name,Country
    /// </summary>
    public async Task ExportGoogleAdsCsvAsync(IEnumerable<Company> companies, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Email,Phone,First Name,Country");

        foreach (var c in companies)
        {
            var email = (c.Email ?? "").Trim().ToLowerInvariant();
            var phone = CleanPhone(c.Phone);
            var (fn, _) = SplitName(c.DisplayName);
            var country = StandardizeCountry(c.Country);

            if (!string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(phone))
            {
                var formattedPhone = string.IsNullOrEmpty(phone) ? "" : "+" + phone;
                sb.AppendLine($"{EscapeCsv(email)},{EscapeCsv(formattedPhone)},{fn},{country}");
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    /// <summary>
    /// Mailchimp / Brevo Marketing CRM CSV specification:
    /// Headers: Email Address,First Name,Company,Phone Number,City,Country,Segment,Status
    /// </summary>
    public async Task ExportMarketingListCsvAsync(IEnumerable<Company> companies, string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Email Address,First Name,Company,Phone Number,City,Country,Segment,Status");

        foreach (var c in companies)
        {
            var email = (c.Email ?? "").Trim().ToLowerInvariant();
            var (fn, _) = SplitName(c.DisplayName);
            var company = EscapeCsv(c.DisplayName);
            var phone = EscapeCsv(c.Phone ?? "");
            var city = EscapeCsv(c.City ?? "");
            var country = EscapeCsv(c.Country ?? "");
            var segment = EscapeCsv(c.Category ?? "General");
            var status = EscapeCsv(c.StatusName ?? "New");

            if (!string.IsNullOrEmpty(email) || !string.IsNullOrEmpty(phone))
            {
                sb.AppendLine($"{EscapeCsv(email)},{fn},{company},{phone},{city},{country},{segment},{status}");
            }
        }

        await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
    }

    private static string CleanPhone(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        return Regex.Replace(raw, @"[^\d]", "");
    }

    private static (string First, string Last) SplitName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName)) return ("", "");
        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return (EscapeCsv(parts[0]), "");
        return (EscapeCsv(parts[0]), EscapeCsv(string.Join(" ", parts.Skip(1))));
    }

    private static string StandardizeCountry(string? country)
    {
        if (string.IsNullOrWhiteSpace(country)) return "AE";
        var c = country.Trim().ToUpperInvariant();
        if (c.Contains("GERMAN") || c.Contains("DEUTSCH") || c == "DE") return "DE";
        if (c.Contains("EMIRAT") || c.Contains("DUBAI") || c.Contains("UAE") || c == "AE") return "AE";
        if (c.Contains("UK") || c.Contains("BRITAIN")) return "GB";
        return c.Length == 2 ? c : "AE";
    }

    private static string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }
        return value;
    }
}
