using System;

namespace ScripOx.Desktop.Models;

public class Source
{
    public long Id { get; set; }
    public string Name { get; set; } = "";
    public string Type { get; set; } = "web";
    public string? BaseUrl { get; set; }
    public string? Connector { get; set; }
    public string? ConfigJson { get; set; }
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string InspectButtonText => Services.LocalizationService.IsRtl ? "معاينة المخطط" : "Inspect Schema";
    public string UpdatedAtDisplay => UpdatedAt != DateTime.MinValue ? UpdatedAt.ToString("yyyy-MM-dd HH:mm") : "-";
}
