namespace ScripOx.Desktop.Models;

/// <summary>Maps to the `extraction_jobs` table.</summary>
public class ExtractionJob
{
    public int      Id          { get; set; }
    public int      SourceId    { get; set; }
    public string   SourceName  { get; set; } = string.Empty;
    public int      TriggeredBy { get; set; }
    public string   Status      { get; set; } = "pending";
    public int      TotalFound  { get; set; }
    public int      TotalSaved  { get; set; }
    public int      TotalDupes  { get; set; }
    public int      TotalErrors { get; set; }
    public int      ProgressPct { get; set; }
    public DateTime? StartedAt  { get; set; }
    public DateTime? EndedAt    { get; set; }
    public string?  ErrorMsg    { get; set; }
    public DateTime CreatedAt   { get; set; }

    // Display helpers
    public string StatusDisplay => Status switch {
        "pending"   => "⏳ Pending",
        "running"   => "🔄 Running",
        "completed" => "✅ Completed",
        "failed"    => "❌ Failed",
        "cancelled" => "🚫 Cancelled",
        _           => Status
    };

    public TimeSpan? Duration => StartedAt.HasValue && EndedAt.HasValue
        ? EndedAt - StartedAt
        : null;
}
