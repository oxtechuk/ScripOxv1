using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
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

            // ── Step 1: Copy extension directory path to Windows Clipboard ──
            try
            {
                Clipboard.SetText(extPath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to copy extension path to clipboard");
            }

            // ── Step 2: Write Registry policies & external extension hints ──
            WriteRegistryHint(browser, extPath);

            var url = browser.Kind switch {
                BrowserKind.Chrome  => "chrome://extensions",
                BrowserKind.Edge    => "edge://extensions",
                BrowserKind.Brave   => "brave://extensions",
                _                   => "chrome://extensions",
            };

            if (browser.ExePath == null || !File.Exists(browser.ExePath))
            {
                return new InstallResult(false, $"Browser executable not found: {browser.Name}");
            }

            bool isAr = LocalizationService.CurrentLanguage == "ar";
            var procName = Path.GetFileNameWithoutExtension(browser.ExePath);
            var runningProcs = Process.GetProcessesByName(procName);

            if (runningProcs.Length > 0)
            {
                var promptMsg = isAr
                    ? $"متصفح {browser.Name} قيد التشغيل حالياً.\n\nلتثبيت وتحميل الإضافة تلقائياً، يحتاج المتصفح إلى إعادة التشغيل مع تحميل الإضافة.\n\nهل ترغب في إعادة تشغيل {browser.Name} الآن وتثبيت الإضافة فوراً؟\n\n(ملاحظة: تم نسخ مسار الإضافة إلى الحافظة Clipboard أيضاً)"
                    : $"{browser.Name} is currently running.\n\nTo automatically attach and load the extension, the browser needs to restart with the extension loaded.\n\nWould you like to restart {browser.Name} now and install the extension?\n\n(Note: The extension folder path is also copied to your clipboard)";

                var dialogResult = MessageBox.Show(promptMsg, isAr ? "تثبيت إضافة ScripOx" : "Install ScripOx Extension", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (dialogResult == MessageBoxResult.Yes)
                {
                    foreach (var p in runningProcs)
                    {
                        try { p.CloseMainWindow(); } catch { }
                    }
                    await Task.Delay(800);
                    foreach (var p in Process.GetProcessesByName(procName))
                    {
                        try { p.Kill(); } catch { }
                    }
                    await Task.Delay(600);
                }
            }

            // ── Step 3: Launch browser with --load-extension ──────
            var args = $"--load-extension=\"{extPath}\" --enable-extensions \"{url}\"";
            Process.Start(new ProcessStartInfo
            {
                FileName        = browser.ExePath,
                Arguments       = args,
                UseShellExecute = false,
            });

            var successMsg = isAr
                ? $"✅ تم إطلاق {browser.Name} وتحميل إضافة ScripOx بنجاح!\n\n" +
                  $"• تم إرفاق الإضافة تلقائياً عبر أمر: --load-extension\n" +
                  $"• مسار الإضافة محفوظ في الحافظة (Clipboard): {extPath}\n\n" +
                  $"يمكنك أيضاً الضغط على زر 'تحميل حزمة غير مضغوطة (Load unpacked)' في صفحة الإضافات ولصق المسار مباشرة (Ctrl + V) لتثبيتها بصورة دائمة."
                : $"✅ {browser.Name} launched with ScripOx Extension loaded!\n\n" +
                  $"• Extension automatically attached via --load-extension\n" +
                  $"• Extension path copied to Clipboard: {extPath}\n\n" +
                  $"You can also click 'Load unpacked' on the opened extensions page and paste (Ctrl+V) the path to persist permanently.";

            return new InstallResult(true, successMsg);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Extension install failed for {Browser}", browser.Name);
            return new InstallResult(false, $"Error: {ex.Message}");
        }
    }

    /// <summary>
    /// Creates a desktop shortcut for the browser that always launches with ScripOx extension loaded.
    /// </summary>
    public static bool CreateDesktopShortcut(BrowserInfo browser)
    {
        try
        {
            if (browser.ExePath == null || !File.Exists(browser.ExePath)) return false;
            var extPath = GetExtensionDistPath();
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutFile = Path.Combine(desktop, $"{browser.Name} (ScripOx Extractor).lnk");

            var script = $"$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('{shortcutFile.Replace("'", "''")}'); $s.TargetPath = '{browser.ExePath.Replace("'", "''")}'; $s.Arguments = '--load-extension=\"\"{extPath.Replace("'", "''")}\"\" --enable-extensions'; $s.Description = 'Launch {browser.Name} with ScripOx Visual Extractor'; $s.Save()";

            var psi = new ProcessStartInfo
            {
                FileName        = "powershell.exe",
                Arguments       = $"-NoProfile -Command \"{script}\"",
                CreateNoWindow  = true,
                UseShellExecute = false
            };
            var p = Process.Start(psi);
            p?.WaitForExit(3000);
            return File.Exists(shortcutFile);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create desktop shortcut");
            return false;
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
            // 1. Write application registry hint
            const string keyPath = @"SOFTWARE\OxTech\ScripOx\Extension";
            using (var key = Registry.CurrentUser.CreateSubKey(keyPath, true))
            {
                key?.SetValue("InstallPath",  extPath);
                key?.SetValue("Browser",      browser.Name);
                key?.SetValue("Version",      ExtVersion);
                key?.SetValue("InstalledAt",  DateTime.Now.ToString("O"));
            }

            // 2. Write Chrome / Chromium Policy keys (allow file:// extension sources)
            string? policyBase = browser.Kind switch
            {
                BrowserKind.Chrome => @"Software\Policies\Google\Chrome",
                BrowserKind.Edge   => @"Software\Policies\Microsoft\Edge",
                BrowserKind.Brave  => @"Software\Policies\BraveSoftware\Brave",
                _                  => null
            };

            if (policyBase != null)
            {
                using var srcKey = Registry.CurrentUser.CreateSubKey($@"{policyBase}\ExtensionInstallSources", true);
                srcKey?.SetValue("1", "file:///*");
                srcKey?.SetValue("2", "http://127.0.0.1:*/*");
            }

            // 3. Register external extension in HKCU
            string? extRegBase = browser.Kind switch
            {
                BrowserKind.Chrome => @"Software\Google\Chrome\Extensions\scripox_extractor",
                BrowserKind.Edge   => @"Software\Microsoft\Edge\Extensions\scripox_extractor",
                BrowserKind.Brave  => @"Software\BraveSoftware\Brave-Browser\Extensions\scripox_extractor",
                _                  => null
            };

            if (extRegBase != null)
            {
                using var extKey = Registry.CurrentUser.CreateSubKey(extRegBase, true);
                extKey?.SetValue("path", extPath);
                extKey?.SetValue("version", ExtVersion);
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not write registry hints or policies");
        }
    }
}

public record InstallResult(bool Success, string Message);
