using System;
using System.Text.Json;

namespace ScripOx.Desktop.Models;

public class RawRecord
{
    public long Id { get; set; }
    public long JobId { get; set; }
    public string RawJson { get; set; } = "{}";
    public string Status { get; set; } = "pending";
    public int? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? RejectReason { get; set; }
    public DateTime CreatedAt { get; set; }

    public bool IsSelected { get; set; }

    // Parsed JSON properties for data binding
    private JsonElement? _jsonElement;
    private JsonElement JsonElement
    {
        get
        {
            if (_jsonElement == null)
            {
                try
                {
                    _jsonElement = JsonDocument.Parse(RawJson).RootElement;
                }
                catch
                {
                    _jsonElement = JsonDocument.Parse("{}").RootElement;
                }
            }
            return _jsonElement.Value;
        }
    }

    public string GetJsonVal(params string[] keys)
    {
        var root = JsonElement;
        foreach (var key in keys)
        {
            if (root.TryGetProperty(key, out var prop))
            {
                var val = ExtractStringValue(prop);
                if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
            }
        }
        // Case-insensitive fallback
        foreach (var prop in root.EnumerateObject())
        {
            foreach (var key in keys)
            {
                if (string.Equals(prop.Name, key, StringComparison.OrdinalIgnoreCase))
                {
                    var val = ExtractStringValue(prop.Value);
                    if (!string.IsNullOrWhiteSpace(val)) return val.Trim();
                }
            }
        }
        return "";
    }

    private static string ExtractStringValue(JsonElement elem)
    {
        return elem.ValueKind switch
        {
            JsonValueKind.String => elem.GetString() ?? "",
            JsonValueKind.Number => elem.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            _ => ""
        };
    }

    public string Name => GetJsonVal("name", "title", "business_name", "company_name");
    public string Phone => GetJsonVal("phone", "phone_number", "tel");
    public string Website => GetJsonVal("website", "url", "site");
    public string Email => GetJsonVal("email", "mail");
    public string Address => GetJsonVal("address", "location", "formatted_address");
    public string Category => GetJsonVal("category", "type", "industry", "segment", "classification");
    public string Rating => GetJsonVal("rating", "stars", "score");
    public string Reviews => GetJsonVal("reviews", "review_count", "user_ratings_total");
    public string RatingDisplay => !string.IsNullOrWhiteSpace(Rating) 
        ? $"★ {Rating}" + (!string.IsNullOrWhiteSpace(Reviews) ? $" ({Reviews})" : "") 
        : "—";
    public string Latitude => GetJsonVal("latitude", "lat");
    public string Longitude => GetJsonVal("longitude", "lng", "lon");
}
