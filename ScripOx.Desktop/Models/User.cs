namespace ScripOx.Desktop.Models;

/// <summary>
/// Represents an authenticated system user.
/// </summary>
public class User
{
    public int     Id           { get; set; }
    public string  Username     { get; set; } = string.Empty;
    public string  Email        { get; set; } = string.Empty;
    public string  FullName     { get; set; } = string.Empty;
    public int     RoleId       { get; set; }
    public string  RoleName     { get; set; } = string.Empty;
    public bool    IsActive     { get; set; }
    public string  Language     { get; set; } = "en";
    public DateTime? LastLogin  { get; set; }

    // Convenience permission helpers
    public bool CanRunJobs      => RoleName is "super_admin" or "admin" or "data_operator";
    public bool CanApproveData  => RoleName is "super_admin" or "admin" or "reviewer";
    public bool CanExport       => RoleName is not "viewer";
    public bool CanManageUsers  => RoleName is "super_admin" or "admin";
}
