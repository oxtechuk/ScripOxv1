using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Serilog;

namespace ScripOx.Desktop.Services;

public record UpdateReleaseInfo(
    bool IsUpdateAvailable,
    Version CurrentVersion,
    Version LatestVersion,
    string TagName,
    string ReleaseTitle,
    string ReleaseNotes,
    string HtmlUrl,
    string? DownloadUrl,
    string? AssetName,
    long AssetSizeBytes,
    DateTimeOffset PublishedAt
);

public static class UpdateService
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    static UpdateService()
    {
        // GitHub API requires a User-Agent header
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ScripOx-Desktop-App");
        HttpClient.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github.v3+json");
    }

    /// <summary>
    /// Gets the running assembly version (e.g. 1.0.0).
    /// </summary>
    public static Version GetCurrentVersion()
    {
        var ver = Assembly.GetExecutingAssembly().GetName().Version;
        return ver ?? new Version(1, 0, 0);
    }

    /// <summary>
    /// Checks GitHub Releases for the latest version.
    /// </summary>
    public static async Task<UpdateReleaseInfo?> CheckForUpdatesAsync(string? owner = null, string? repo = null)
    {
        owner ??= AppSettings.GitHubOwner;
        repo ??= AppSettings.GitHubRepo;

        var currentVersion = GetCurrentVersion();

        try
        {
            var url = $"https://api.github.com/repos/{owner}/{repo}/releases/latest";
            Log.Information("Checking for updates at {Url}...", url);

            using var response = await HttpClient.GetAsync(url);
            if (!response.IsSuccessStatusCode)
            {
                Log.Warning("Update check received HTTP {StatusCode}", response.StatusCode);
                return null;
            }

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var tagName = root.GetProperty("tag_name").GetString() ?? "";
            var releaseTitle = root.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? tagName : tagName;
            var body = root.TryGetProperty("body", out var bodyEl) ? bodyEl.GetString() ?? "" : "";
            var htmlUrl = root.TryGetProperty("html_url", out var htmlEl) ? htmlEl.GetString() ?? "" : "";
            var publishedAt = root.TryGetProperty("published_at", out var pubEl) && pubEl.TryGetDateTimeOffset(out var dt)
                ? dt
                : DateTimeOffset.UtcNow;

            // Normalize version string (strip 'v' or 'V' prefix)
            var cleanTag = tagName.TrimStart('v', 'V');
            if (!Version.TryParse(cleanTag, out var latestVersion))
            {
                // Fallback for 2-part or 3-part versions with extra metadata
                var parts = cleanTag.Split(['-', '+'])[0].Split('.');
                if (parts.Length == 2)
                    latestVersion = new Version(int.Parse(parts[0]), int.Parse(parts[1]), 0);
                else if (parts.Length >= 3 && int.TryParse(parts[0], out var maj) && int.TryParse(parts[1], out var min) && int.TryParse(parts[2], out var rev))
                    latestVersion = new Version(maj, min, rev);
                else
                    latestVersion = new Version(1, 0, 0);
            }

            // Find executable or zip asset
            string? downloadUrl = null;
            string? assetName = null;
            long assetSize = 0;

            if (root.TryGetProperty("assets", out var assetsEl) && assetsEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assetsEl.EnumerateArray())
                {
                    var name = asset.GetProperty("name").GetString() ?? "";
                    if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                    {
                        assetName = name;
                        downloadUrl = asset.GetProperty("browser_download_url").GetString();
                        assetSize = asset.TryGetProperty("size", out var s) ? s.GetInt64() : 0;
                        break;
                    }
                }
            }

            // Fallback to zipball or release url if no explicit asset
            if (string.IsNullOrEmpty(downloadUrl))
            {
                downloadUrl = htmlUrl;
            }

            var isUpdateAvailable = latestVersion > currentVersion;

            Log.Information("Update check complete. Current: {Current}, Latest: {Latest}, Available: {Available}",
                currentVersion, latestVersion, isUpdateAvailable);

            return new UpdateReleaseInfo(
                IsUpdateAvailable: isUpdateAvailable,
                CurrentVersion: currentVersion,
                LatestVersion: latestVersion,
                TagName: tagName,
                ReleaseTitle: releaseTitle,
                ReleaseNotes: body,
                HtmlUrl: htmlUrl,
                DownloadUrl: downloadUrl,
                AssetName: assetName,
                AssetSizeBytes: assetSize,
                PublishedAt: publishedAt
            );
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error while checking for application updates.");
            return null;
        }
    }

    /// <summary>
    /// Downloads the update file with progress reporting.
    /// </summary>
    public static async Task<string> DownloadUpdateAsync(
        string downloadUrl,
        string fileName,
        IProgress<double>? progress = null,
        CancellationToken ct = default)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "ScripOxUpdates");
        Directory.CreateDirectory(tempDir);
        var targetPath = Path.Combine(tempDir, fileName);

        Log.Information("Downloading update from {Url} to {Path}...", downloadUrl, targetPath);

        using var response = await HttpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;

        await using var contentStream = await response.Content.ReadAsStreamAsync(ct);
        await using var fileStream = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[8192];
        long totalRead = 0;
        int bytesRead;

        while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, ct)) > 0)
        {
            await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), ct);
            totalRead += bytesRead;

            if (totalBytes > 0 && progress != null)
            {
                var percentage = (double)totalRead / totalBytes * 100.0;
                progress.Report(percentage);
            }
        }

        Log.Information("Download complete: {Path} ({Bytes} bytes)", targetPath, totalRead);
        return targetPath;
    }

    /// <summary>
    /// Launches the installer or applies the update script and restarts.
    /// </summary>
    public static void ApplyUpdateAndRestart(string downloadedFilePath)
    {
        try
        {
            var ext = Path.GetExtension(downloadedFilePath).ToLowerInvariant();

            if (ext is ".exe" or ".msi")
            {
                // Run the downloaded installer
                Log.Information("Launching installer: {Path}", downloadedFilePath);
                Process.Start(new ProcessStartInfo
                {
                    FileName = downloadedFilePath,
                    UseShellExecute = true
                });

                Application.Current.Shutdown();
            }
            else if (ext is ".zip")
            {
                // Unpack portable zip via an independent updater batch script
                var appDir = AppDomain.CurrentDomain.BaseDirectory;
                var currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? Path.Combine(appDir, "ScripOx.exe");
                var scriptPath = Path.Combine(Path.GetTempPath(), "ScripOxUpdates", "apply_update.bat");

                var script = $@"@echo off
timeout /t 2 /nobreak > nul
echo Updating ScripOx...
powershell -Command ""Expand-Archive -Path '{downloadedFilePath}' -DestinationPath '{appDir}' -Force""
start """" ""{currentExe}""
del ""{downloadedFilePath}""
del ""%~f0""
";
                File.WriteAllText(scriptPath, script);

                Process.Start(new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = $"/c \"{scriptPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                });

                Application.Current.Shutdown();
            }
            else
            {
                // Fallback: Open in browser
                Process.Start(new ProcessStartInfo
                {
                    FileName = downloadedFilePath,
                    UseShellExecute = true
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply update automatically.");
            throw;
        }
    }
}
