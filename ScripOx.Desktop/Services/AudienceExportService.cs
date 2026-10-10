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
public class MetaAudienceRecord
{
    public string Email { get; set; } = "";
    public string Email2 { get; set; } = "";
    public string Email3 { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Phone2 { get; set; } = "";
    public string Phone3 { get; set; } = "";
    public string Madid { get; set; } = "";
    public string Fn { get; set; } = "";
    public string Ln { get; set; } = "";
    public string Zip { get; set; } = "";
    public string Ct { get; set; } = "";
    public string St { get; set; } = "";
    public string Country { get; set; } = "";
    public string Dob { get; set; } = "";
    public string Doby { get; set; } = "";
    public string Gen { get; set; } = "";
    public string Age { get; set; } = "";
    public string Uid { get; set; } = "";
    public double Value { get; set; } = 150.0;
    public string ValueDisplay => $"${Value:N1}";
}

public class GoogleAudienceRecord
{
    public string Email { get; set; } = "";
    public string Phone { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string Country { get; set; } = "";
    public double Value { get; set; } = 150.0;
    public string ValueDisplay => $"${Value:N1}";
}

public class MarketingAudienceRecord
{
    public string Email { get; set; } = "";
    public string FirstName { get; set; } = "";
    public string Company { get; set; } = "";
    public string Phone { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
    public string Segment { get; set; } = "";
    public string Status { get; set; } = "";
}

public class ExcelAudienceRecord
{
    public long Id { get; set; }
    public string CompanyName { get; set; } = "";
    public string Category { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Email { get; set; } = "";
    public string City { get; set; } = "";
    public string Country { get; set; } = "";
    public string Priority { get; set; } = "";
    public string Status { get; set; } = "";
    public double Value { get; set; } = 150.0;
    public string ValueDisplay => $"${Value:N1}";
}

public class AudienceExportService
{
    public static MetaAudienceRecord MapToMetaRecord(Company c)
    {
        var (fn, ln) = SplitName(c.DisplayName);
        if (string.IsNullOrEmpty(ln)) ln = c.Category ?? "Lead";

        var cleanPh = CleanPhone(c.Phone);
        if (!string.IsNullOrEmpty(cleanPh) && !cleanPh.StartsWith("+"))
        {
            cleanPh = "+" + cleanPh;
        }

        var cleanPh2 = CleanPhone(c.Phone2);
        if (!string.IsNullOrEmpty(cleanPh2) && !cleanPh2.StartsWith("+"))
        {
            cleanPh2 = "+" + cleanPh2;
        }

        double val = 150.0;
        if (c.StatusId == 4) val = 1500.0;      // Deal won
        else if (c.StatusId == 2) val = 650.0; // Interested
        else if (c.Priority?.Equals("high", StringComparison.OrdinalIgnoreCase) == true) val = 850.0;

        var zip = !string.IsNullOrWhiteSpace(c.PostalCode) ? c.PostalCode.Trim() : ExtractZip(c.Address);

        return new MetaAudienceRecord
        {
            Email = (c.Email ?? "").Trim().ToLowerInvariant(),
            Email2 = "",
            Email3 = "",
            Phone = cleanPh,
            Phone2 = cleanPh2,
            Phone3 = "",
            Madid = "",
            Fn = fn,
            Ln = ln,
            Zip = zip,
            Ct = c.City ?? "",
            St = c.Region ?? "",
            Country = StandardizeCountry(c.Country),
            Dob = "",
            Doby = "",
            Gen = "",
            Age = "",
            Uid = c.Id.ToString(),
            Value = val
        };
    }

    public static GoogleAudienceRecord MapToGoogleRecord(Company c)
    {
        var (fn, _) = SplitName(c.DisplayName);
        var cleanPh = CleanPhone(c.Phone);
        if (!string.IsNullOrEmpty(cleanPh) && !cleanPh.StartsWith("+")) cleanPh = "+" + cleanPh;

        double val = 150.0;
        if (c.StatusId == 4) val = 1500.0;
        else if (c.StatusId == 2) val = 650.0;
        else if (c.Priority?.Equals("high", StringComparison.OrdinalIgnoreCase) == true) val = 850.0;

        return new GoogleAudienceRecord
        {
            Email = (c.Email ?? "").Trim().ToLowerInvariant(),
            Phone = cleanPh,
            FirstName = fn,
            Country = StandardizeCountry(c.Country),
            Value = val
        };
    }

    public static MarketingAudienceRecord MapToMarketingRecord(Company c)
    {
        var (fn, _) = SplitName(c.DisplayName);
        var cleanPh = CleanPhone(c.Phone);
        if (!string.IsNullOrEmpty(cleanPh) && !cleanPh.StartsWith("+")) cleanPh = "+" + cleanPh;

        return new MarketingAudienceRecord
        {
            Email = (c.Email ?? "").Trim().ToLowerInvariant(),
            FirstName = fn,
            Company = c.DisplayName,
            Phone = cleanPh,
            City = c.City ?? "",
            Country = StandardizeCountry(c.Country),
            Segment = c.Category ?? "General",
            Status = c.StatusName ?? "New"
        };
    }

    public static ExcelAudienceRecord MapToExcelRecord(Company c)
    {
        double val = 150.0;
        if (c.StatusId == 4) val = 1500.0;
        else if (c.StatusId == 2) val = 650.0;
        else if (c.Priority?.Equals("high", StringComparison.OrdinalIgnoreCase) == true) val = 850.0;

        return new ExcelAudienceRecord
        {
            Id = c.Id,
            CompanyName = c.DisplayName,
            Category = c.Category ?? "-",
            Phone = c.Phone ?? "-",
            Email = c.Email ?? "-",
            City = c.City ?? "-",
            Country = c.Country,
            Priority = c.PriorityDisplay,
            Status = c.StatusName ?? "New",
            Value = val
        };
    }

    public static string GenerateMetaAdsCsv(IEnumerable<Company> companies)
    {
        var sb = new StringBuilder();
        sb.AppendLine("email,email,email,phone,phone,phone,madid,fn,ln,zip,ct,st,country,dob,doby,gen,age,uid,value");
        foreach (var c in companies)
        {
            var meta = MapToMetaRecord(c);
            if (!string.IsNullOrEmpty(meta.Email) || !string.IsNullOrEmpty(meta.Phone))
            {
                sb.AppendLine($"{EscapeCsv(meta.Email)},{EscapeCsv(meta.Email2)},{EscapeCsv(meta.Email3)},{EscapeCsv(meta.Phone)},{EscapeCsv(meta.Phone2)},{EscapeCsv(meta.Phone3)},{meta.Madid},{EscapeCsv(meta.Fn)},{EscapeCsv(meta.Ln)},{EscapeCsv(meta.Zip)},{EscapeCsv(meta.Ct)},{EscapeCsv(meta.St)},{meta.Country},{meta.Dob},{meta.Doby},{meta.Gen},{meta.Age},{meta.Uid},{meta.Value.ToString("F1", System.Globalization.CultureInfo.InvariantCulture)}");
            }
        }
        return sb.ToString();
    }

    public static string GenerateGoogleAdsCsv(IEnumerable<Company> companies)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Email,Phone,First Name,Country");
        foreach (var c in companies)
        {
            var g = MapToGoogleRecord(c);
            if (!string.IsNullOrEmpty(g.Email) || !string.IsNullOrEmpty(g.Phone))
            {
                sb.AppendLine($"{EscapeCsv(g.Email)},{EscapeCsv(g.Phone)},{EscapeCsv(g.FirstName)},{g.Country}");
            }
        }
        return sb.ToString();
    }

    public static string GenerateMarketingListCsv(IEnumerable<Company> companies)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Email Address,First Name,Company,Phone Number,City,Country,Segment,Status");
        foreach (var c in companies)
        {
            var m = MapToMarketingRecord(c);
            if (!string.IsNullOrEmpty(m.Email) || !string.IsNullOrEmpty(m.Phone))
            {
                sb.AppendLine($"{EscapeCsv(m.Email)},{EscapeCsv(m.FirstName)},{EscapeCsv(m.Company)},{EscapeCsv(m.Phone)},{EscapeCsv(m.City)},{EscapeCsv(m.Country)},{EscapeCsv(m.Segment)},{EscapeCsv(m.Status)}");
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Meta (Facebook) Value-Based Custom Audience CSV specification:
    /// Matching exact template in example_value_based_audience_file.csv
    /// </summary>
    public async Task ExportMetaAdsCsvAsync(IEnumerable<Company> companies, string filePath)
    {
        var csv = GenerateMetaAdsCsv(companies);
        await File.WriteAllTextAsync(filePath, csv, Encoding.UTF8);
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

    private static string ExtractZip(string? address)
    {
        if (string.IsNullOrWhiteSpace(address)) return "";
        var match = Regex.Match(address, @"\b\d{4,6}\b");
        return match.Success ? match.Value : "";
    }
}
