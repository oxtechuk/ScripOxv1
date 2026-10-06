namespace ScripOx.Desktop.Models;

/// <summary>
/// A verified company in the ScripOx system.
/// Maps to the `companies` table.
/// </summary>
public class Company
{
    public long    Id           { get; set; }
    public string  Name         { get; set; } = string.Empty;
    public string? NameAr       { get; set; }
    public string? Category     { get; set; }
    public string? Description  { get; set; }

    // Contact
    public string? Phone        { get; set; }
    public string? Phone2       { get; set; }
    public string? Email        { get; set; }
    public string? Website      { get; set; }

    // Address
    public string? Address      { get; set; }
    public string? City         { get; set; }
    public string? Region       { get; set; }
    public string  Country      { get; set; } = "UK";
    public string? PostalCode   { get; set; }

    // Geo
    public double? X            { get; set; }   // longitude
    public double? Y            { get; set; }   // latitude

    // Source
    public int?    SourceId     { get; set; }
    public string? SourceName   { get; set; }
    public string? SourceUrl    { get; set; }

    // Workflow
    public int?    StatusId     { get; set; }
    public string? StatusName   { get; set; }
    public string? StatusColor  { get; set; }
    public int?    AssignedTo   { get; set; }
    public string? AssignedName { get; set; }
    public string  Priority     { get; set; } = "medium";
    public bool    IsVerified   { get; set; }
    public DateTime? ReviewedAt { get; set; }

    // Meta
    public int?    CreatedBy    { get; set; }
    public DateTime CreatedAt   { get; set; }
    public DateTime UpdatedAt   { get; set; }

    // Navigation (loaded on demand)
    public List<CompanyNote>    Notes    { get; set; } = [];
    public List<CompanyComment> Comments { get; set; } = [];
    public List<CompanyLink>    Links    { get; set; } = [];

    // Display helpers
    public bool HasCoordinates => X.HasValue && Y.HasValue;
    public string DisplayName  => string.IsNullOrWhiteSpace(NameAr)
                                  ? Name : NameAr;
    public string PriorityDisplay => Priority?.ToLower() switch
    {
        "high" => "★★★ High",
        "medium" => "★★ Medium",
        "low" => "★ Low",
        _ => Priority ?? "Medium"
    };
    public string PriorityColor => Priority?.ToLower() switch
    {
        "high" => "#EF4444",
        "medium" => "#F59E0B",
        "low" => "#64748B",
        _ => "#64748B"
    };
}

public class CompanyNote
{
    public int      Id        { get; set; }
    public long     CompanyId { get; set; }
    public int      UserId    { get; set; }
    public string   UserName  { get; set; } = string.Empty;
    public string   Content   { get; set; } = string.Empty;
    public bool     IsPinned  { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class CompanyComment
{
    public int      Id        { get; set; }
    public long     CompanyId { get; set; }
    public int      UserId    { get; set; }
    public string   UserName  { get; set; } = string.Empty;
    public string   Content   { get; set; } = string.Empty;
    public int?     ParentId  { get; set; }
    public bool     IsEdited  { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<CompanyComment> Replies { get; set; } = [];
}

public class CompanyLink
{
    public int    Id        { get; set; }
    public long   CompanyId { get; set; }
    public string Platform  { get; set; } = string.Empty;
    public string Url       { get; set; } = string.Empty;
}

public class CompanyStatus
{
    public int     Id        { get; set; }
    public string  Name      { get; set; } = string.Empty;
    public string? NameAr    { get; set; }
    public string  Color     { get; set; } = "#64748B";
    public int     SortOrder { get; set; }

    public string DisplayName =>
        Services.LocalizationService.IsRtl && !string.IsNullOrWhiteSpace(NameAr) ? NameAr : Name;
}

