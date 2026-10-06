namespace ScripOx.Desktop;

/// <summary>
/// Central application settings (loaded from environment variables or defaults).
/// </summary>
public static class AppSettings
{
    // ── Connection String ────────────────────────────────────
    private static string? _connectionString;

    public static string ConnectionString
    {
        get => _connectionString ??= BuildConnectionString();
        set => _connectionString = value;
    }

    private static string BuildConnectionString()
    {
        var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripox.db");
        return $"Data Source={dbPath};";
    }

    // ── Python API ───────────────────────────────────────────
    public static string ExtractorApiBaseUrl { get; set; } = "http://127.0.0.1:8765";

    // ── UI preferences (persisted per-session) ───────────────
    public static string Language { get; set; } = "en";
    public static string Theme    { get; set; } = "dark";

    // ── GitHub Auto-Update ───────────────────────────────────
    public static string GitHubOwner { get; set; } = "oxtechuk";
    public static string GitHubRepo  { get; set; } = "Scripox";
    public static bool AutoCheckUpdatesOnStartup { get; set; } = true;
}

