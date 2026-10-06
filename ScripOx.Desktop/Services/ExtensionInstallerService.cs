using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Win32;
using Serilog;

namespace ScripOx.Desktop.Services;

/// <summary>
/// Detects installed browsers and installs the ScripOx Chrome Extension
/// using Windows Registry external extension policies.
/// </summary>
public static class ExtensionInstallerService
{
    // ── Extension Info ────────────────────────────────────────
    private const string ExtVersion    = "1.0.0";
    private const string ExtFolderName = "ScripOx.Extension";

    // ── Browser Definitions ───────────────────────────────────

    public enum BrowserKind { Chrome, Edge, Brave, Vivaldi }

    public record BrowserInfo(
        BrowserKind Kind,
        string      Name,
        string      Icon,
        string?     ExePath,
        bool        IsInstalled,
        string      ProfilePath,
        string      ExtensionsRegPath,
        string      ExtInstallSrcRegPath
    );

    // ── Browser Detection ─────────────────────────────────────

    public static List<BrowserInfo> DetectBrowsers()
    {
        var browsers = new List<BrowserInfo>();

        // Chrome
        var chromePath = FindExe(
            @"Google\Chrome\Application\chrome.exe",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\chrome.exe"
        );
        browsers.Add(new BrowserInfo(
            BrowserKind.Chrome, "Google Chrome", "🌐", chromePath,
            chromePath != null,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         @"Google\Chrome\User Data\Default"),
            @"SOFTWARE\Policies\Google\Chrome\ExtensionInstallAllowlist",
            @"SOFTWARE\Policies\Google\Chrome\ExtensionInstallSources"
        ));

        // Microsoft Edge
        var edgePath = FindExe(
            @"Microsoft\Edge\Application\msedge.exe",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\msedge.exe"
        );
        browsers.Add(new BrowserInfo(
            BrowserKind.Edge, "Microsoft Edge", "🔷", edgePath,
            edgePath != null,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         @"Microsoft\Edge\User Data\Default"),
            @"SOFTWARE\Policies\Microsoft\Edge\ExtensionInstallAllowlist",
            @"SOFTWARE\Policies\Microsoft\Edge\ExtensionInstallSources"
        ));

        // Brave
        var bravePath = FindExe(
            @"BraveSoftware\Brave-Browser\Application\brave.exe",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\brave.exe"
        );
        browsers.Add(new BrowserInfo(
            BrowserKind.Brave, "Brave Browser", "🦁", bravePath,
            bravePath != null,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                         @"BraveSoftware\Brave-Browser\User Data\Default"),
            @"SOFTWARE\Policies\BraveSoftware\Brave\ExtensionInstallAllowlist",
            @"SOFTWARE\Policies\BraveSoftware\Brave\ExtensionInstallSources"
        ));

        return browsers;
    }

    // ── Get Extension Folder Path ─────────────────────────────

    public static string GetExtensionDistPath()
    {
        // Look relative to the running exe
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var candidates = new[]
        {
            // When running from desktop shortcut / installed
            Path.Combine(baseDir, "Extension"),
            // Development: relative from Desktop/ScripOx
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\..\..\ScripOx.Extension\dist")),
            // Alt dev path
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\..\ScripOx.Extension\dist")),
        };

        return candidates.FirstOrDefault(Directory.Exists)
               ?? candidates[0]; // return default even if missing (for display)
    }

    // ── Install Extension ─────────────────────────────────────

    /// <summary>
    /// Installs the ScripOx extension for the chosen browser by:
    /// 1. Ensuring the extension dist/ folder exists at a stable path
    /// 2. Adding a Windows Registry ExtensionInstallSources entry
    /// 3. Opening the browser's extension page so the user can load it
    /// </summary>
    public static async Task<InstallResult> InstallAsync(BrowserInfo browser)
    {
        try
        {
            var extPath = GetExtensionDistPath();

            if (!Directory.Exists(extPath))
                return new InstallResult(false,
                    $"Extension folder not found at:\n{extPath}\n\nPlease rebuild the extension first.");

            // ── Step 1: Write a stable registry path hint ──────
            WriteRegistryHint(browser, extPath);

            // ── Step 2: Open browser to its Extensions page ────
            var url = browser.Kind switch {
                BrowserKind.Chrome  => "chrome://extensions",
                BrowserKind.Edge    => "edge://extensions",
                BrowserKind.Brave   => "brave://extensions",
                _                   => "chrome://extensions",
            };

            if (browser.ExePath != null && File.Exists(browser.ExePath))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName  = browser.ExePath,
                    Arguments = url,
                    UseShellExecute = false,
                });
                await Task.Delay(1500);
            }

            return new InstallResult(true,
                $"Extension folder detected at:\n{extPath}\n\n" +
                $"The browser has been opened to the extensions page.\n\n" +
                $"Steps:\n" +
                $"1. Enable 'Developer mode' (top-right toggle)\n" +
                $"2. Click 'Load unpacked'\n" +
                $"3. Select the folder above\n" +
                $"4. Complete! ScripOx extension is active and linked.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Extension install failed for {Browser}", browser.Name);
            return new InstallResult(false, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Check if the extension is connected to the backend API.
    /// </summary>
    public static async Task<bool> CheckExtensionConnectedAsync()
    {
        try
        {
            using var http = new System.Net.Http.HttpClient();
            http.Timeout = TimeSpan.FromSeconds(3);
            var res = await http.GetAsync("http://127.0.0.1:8765/api/health");
            return res.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    // ── Private Helpers ───────────────────────────────────────

    private static string? FindExe(string localAppSubPath, string regPath)
    {
        // Check LocalAppData first
        var localApp = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            localAppSubPath
        );
        if (File.Exists(localApp)) return localApp;

        // Check Program Files
        var pf = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
            localAppSubPath
        );
        if (File.Exists(pf)) return pf;

        // Check Registry
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(regPath);
            var val = key?.GetValue(string.Empty) as string;
            if (val != null && File.Exists(val)) return val;
        }
        catch { /* ignore */ }

        return null;
    }

    private static void WriteRegistryHint(BrowserInfo browser, string extPath)
    {
        try
        {
            // Write a simple registry marker so other tools can find the path
            const string keyPath = @"SOFTWARE\OxTech\ScripOx\Extension";
            using var key = Registry.CurrentUser.CreateSubKey(keyPath, true);
            key.SetValue("InstallPath",  extPath);
            key.SetValue("Browser",      browser.Name);
            key.SetValue("Version",      ExtVersion);
            key.SetValue("InstalledAt",  DateTime.Now.ToString("O"));
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not write registry hint");
        }
    }
}

public record InstallResult(bool Success, string Message);
