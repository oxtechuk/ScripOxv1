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
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var appFolder = System.IO.Path.Combine(localAppData, "ScripOx");
        if (!System.IO.Directory.Exists(appFolder))
        {
            System.IO.Directory.CreateDirectory(appFolder);
        }
        var dbPath = System.IO.Path.Combine(appFolder, "scripox.db");

        // If local dev db exists in BaseDirectory and none exists in LocalAppData (or localappdata db is empty), copy it over
        var devDb = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripox.db");
        if (System.IO.File.Exists(devDb))
        {
            if (!System.IO.File.Exists(dbPath) || new System.IO.FileInfo(dbPath).Length < 2000)
            {
                try { System.IO.File.Copy(devDb, dbPath, true); } catch { }
            }
        }

        return $"Data Source={dbPath};";
    }

    // ── Python API ───────────────────────────────────────────
    public static int ApiPort { get; set; } = 8765;
    public static string ExtractorApiBaseUrl { get => $"http://127.0.0.1:{ApiPort}"; }

    // ── UI preferences (persisted per-session) ───────────────
    public static string Language { get; set; } = "en";
    public static string Theme    { get; set; } = "dark";

    // ── GitHub Auto-Update ───────────────────────────────────
    public static string GitHubOwner { get; set; } = "oxtechuk";
    public static string GitHubRepo  { get; set; } = "ScripOxv1";
    public static bool AutoCheckUpdatesOnStartup { get; set; } = true;
}

