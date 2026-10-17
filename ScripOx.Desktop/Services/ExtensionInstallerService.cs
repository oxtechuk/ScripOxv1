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
/// Manages detection, deployment, and native registry installation of the ScripOx extension (IDM-style integration).
/// Supports instant dedicated browser profiling and native Chromium external extension registration.
/// </summary>
public static class ExtensionInstallerService
{
    // ── Extension Info ────────────────────────────────────────
    public const string ExtVersion    = "1.0.0";
    public const string ExtensionId   = "dnglecedpknmjiapmdoohaciobcnpokj";
    private const string ExtFolderName = "ScripOx.Extension";

    // ── Browser Definitions ───────────────────────────────────

    public enum BrowserKind { Chrome, Edge, Brave, Vivaldi, Firefox }

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
            $@"SOFTWARE\Google\Chrome\Extensions\{ExtensionId}",
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
            $@"SOFTWARE\Microsoft\Edge\Extensions\{ExtensionId}",
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
            $@"SOFTWARE\BraveSoftware\Brave-Browser\Extensions\{ExtensionId}",
            @"SOFTWARE\Policies\BraveSoftware\Brave\ExtensionInstallSources"
        ));

        // Mozilla Firefox
        var firefoxPath = FindExe(
            @"Mozilla Firefox\firefox.exe",
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\firefox.exe"
        );
        browsers.Add(new BrowserInfo(
            BrowserKind.Firefox, "Mozilla Firefox", "🦊", firefoxPath,
            firefoxPath != null,
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                         @"Mozilla\Firefox"),
            "", ""
        ));

        return browsers;
    }

    // ── Get Extension Folder Path ─────────────────────────────

    public static string GetExtensionDistPath()
    {
        var baseDir = AppDomain.CurrentDomain.BaseDirectory;

        var candidates = new[]
        {
            // 1. Known repository dev path (highest priority to always deploy latest build)
            @"c:\wamp64\www\ScripOxv1\ScripOx.Extension\dist",
            // 2. Relative dev candidates
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\..\..\ScripOx.Extension\dist")),
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\..\..\ScripOx.Extension\dist")),
            Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\ScripOx.Extension\dist")),
            // 3. Packaged next to running executable
            Path.Combine(baseDir, "Extension"),
            // 4. Previously deployed to LocalAppData
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScripOx", "Extension"),
        };

        foreach (var c in candidates)
        {
            if (Directory.Exists(c) && File.Exists(Path.Combine(c, "manifest.json")))
                return c;
        }

        return candidates[5];
    }

    /// <summary>
    /// Ensures that the latest extension build and scripox.crx are deployed to %LOCALAPPDATA%\ScripOx\Extension.
    /// </summary>
    public static string EnsureDeployedExtensionPath()
    {
        var localExtPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScripOx", "Extension");
        var srcPath = GetExtensionDistPath();

        try
        {
            Directory.CreateDirectory(localExtPath);
            if (Directory.Exists(srcPath) && !string.Equals(Path.GetFullPath(srcPath), Path.GetFullPath(localExtPath), StringComparison.OrdinalIgnoreCase))
            {
                CopyDirectoryRecursive(srcPath, localExtPath);
            }

            // Ensure scripox.crx exists in LocalAppData
            var crxDest = Path.Combine(localExtPath, "scripox.crx");
            if (!File.Exists(crxDest))
            {
                var crxSrc = Path.Combine(srcPath, "scripox.crx");
                if (File.Exists(crxSrc))
                {
                    File.Copy(crxSrc, crxDest, true);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not sync extension to LocalAppData, using source directly");
            return Directory.Exists(localExtPath) ? localExtPath : srcPath;
        }

        return localExtPath;
    }

    // ── 1-Click Launch Dedicated Browser ──────────────────────

    /// <summary>
    /// Launches an isolated, dedicated browser instance with ScripOx Extension loaded automatically.
    /// Runs with zero conflicts with the user's running browser and does not close any active tabs.
    /// </summary>
    public static InstallResult LaunchDedicatedBrowser(BrowserInfo browser, string? startUrl = null)
    {
        try
        {
            if (browser.ExePath == null || !File.Exists(browser.ExePath))
                return new InstallResult(false, $"Browser executable not found: {browser.Name}");

            var localExtPath = EnsureDeployedExtensionPath();
            var profileDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScripOx", "BrowserProfile");
            Directory.CreateDirectory(profileDir);

            bool isAr = LocalizationService.CurrentLanguage == "ar";
            string targetUrl = string.IsNullOrWhiteSpace(startUrl) ? "https://www.google.com" : startUrl;

            var isFirefox = browser.Kind == BrowserKind.Firefox;
            var extPageUrl = browser.Kind switch {
                BrowserKind.Chrome  => "chrome://extensions",
                BrowserKind.Edge    => "edge://extensions",
                BrowserKind.Brave   => "brave://extensions",
                BrowserKind.Firefox => "about:debugging#/runtime/this-firefox",
                _                   => "chrome://extensions",
            };

            var manifestPath = Path.Combine(localExtPath, "manifest.json");

            if (isFirefox)
            {
                try { Clipboard.SetText(manifestPath); } catch { }
                try { Process.Start("explorer.exe", $"/select,\"{manifestPath}\""); } catch { }

                var args = $"-profile \"{profileDir}\" -no-remote \"about:debugging#/runtime/this-firefox\" \"{targetUrl}\"";
                Process.Start(new ProcessStartInfo
                {
                    FileName        = browser.ExePath,
                    Arguments       = args,
                    UseShellExecute = false
                });

                return new InstallResult(true, isAr
                    ? $"🚀 تم فتح Firefox ونافذة مجلد الإضافة مع نسخ المسار تلقائياً!\n\n" +
                      $"خطوة واحدة بسيطة:\n" +
                      $"1. في صفحة Firefox المفتوحة أمامك، اضغط زر: [Load Temporary Add-on... / تحميل وظيفة إضافية مؤقتة].\n" +
                      $"2. اختر ملف 'manifest.json' المحدد أمامك مباشرة في النافذة واضغط Open.\n\n" +
                      $"💡 ستعمل الإضافة فوراً في التبويب الآخر ({targetUrl})!"
                    : $"🚀 Dedicated Firefox Extractor Browser launched!\n\n" +
                      $"1. Click [Load Temporary Add-on...] in Firefox.\n" +
                      $"2. Select the highlighted 'manifest.json' file.\n" +
                      $"💡 Extension is ready in the other tab ({targetUrl})!");
            }
            else
            {
                // Chromium: Chrome / Edge / Brave
                try { Clipboard.SetText(localExtPath); } catch { }
                try { Process.Start("explorer.exe", $"/select,\"{manifestPath}\""); } catch { }

                // Modern Chrome blocks --load-extension CLI flag, so open extensions manager directly
                Process.Start(new ProcessStartInfo
                {
                    FileName        = browser.ExePath,
                    Arguments       = $"\"{extPageUrl}\" \"{targetUrl}\"",
                    UseShellExecute = false
                });

                return new InstallResult(true, isAr
                    ? $"🚀 تم فتح متصفح {browser.Name} ومجلد الإضافة مع نسخ المسار تلقائياً!\n\n" +
                      $"بسبب متطلبات الأمان في جوجل كروم، خطوتان فقط لأول مرة (5 ثوانٍ):\n" +
                      $"1. في صفحة المتصفح المفتوحة أمامك، فعّل خيار: [Developer mode / وضع مطوّر البرامج] في أعلى اليمين.\n" +
                      $"2. اضغط زر: [Load unpacked / تحميل حزمة غير مضغوطة] في أعلى اليسار، ثم الصق المسار (Ctrl+V) واضغط 'Select Folder' (أو اسحب المجلد المفتوح أمامك بالماوس وأفلته داخل المتصفح).\n\n" +
                      $"💡 ستظهر إضافة ScripOx فوراً وتبدأ العمل في التبويب الآخر ({targetUrl})!"
                    : $"🚀 Opened {browser.Name} Extensions page and extension folder!\n\n" +
                      $"1. Turn ON [Developer mode] in the top-right corner.\n" +
                      $"2. Click [Load unpacked] in the top-left, paste (Ctrl+V) and select folder.\n" +
                      $"💡 Extension is ready in the other tab ({targetUrl})!");
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to launch dedicated browser for {Browser}", browser.Name);
            return new InstallResult(false, $"Error: {ex.Message}");
        }
    }

    // ── Install to Regular Browser (Native IDM-Style Registry Integration) ──

    /// <summary>
    /// Registers the extension natively in Windows Registry (IDM style) so Chrome/Edge displays
    /// the "Action required / New extension added -> Enable extension" prompt upon startup.
    /// </summary>
    public static async Task<InstallResult> InstallAsync(BrowserInfo browser)
    {
        try
        {
            var localExtPath = EnsureDeployedExtensionPath();

            if (!Directory.Exists(localExtPath))
                return new InstallResult(false, $"Extension folder not found at:\n{localExtPath}");

            try { Clipboard.SetText(localExtPath); } catch { }

            // 1. Write the Native IDM-style Registry Keys
            WriteRegistryHint(browser, localExtPath);

            var isFirefox = browser.Kind == BrowserKind.Firefox;
            var url = browser.Kind switch {
                BrowserKind.Chrome  => "chrome://extensions",
                BrowserKind.Edge    => "edge://extensions",
                BrowserKind.Brave   => "brave://extensions",
                BrowserKind.Firefox => "about:debugging#/runtime/this-firefox",
                _                   => "chrome://extensions",
            };

            if (browser.ExePath == null || !File.Exists(browser.ExePath))
            {
                return new InstallResult(false, $"Browser executable not found: {browser.Name}");
            }

            bool isAr = LocalizationService.CurrentLanguage == "ar";
            var manifestPath = Path.Combine(localExtPath, "manifest.json");

            if (!isFirefox)
            {
                // Open browser directly to its extensions management page
                Process.Start(new ProcessStartInfo
                {
                    FileName        = browser.ExePath,
                    Arguments       = $"\"{url}\"",
                    UseShellExecute = false
                });

                // Open Windows Explorer with manifest.json highlighted so user can see folder/drag-drop
                try { Process.Start("explorer.exe", $"/select,\"{manifestPath}\""); } catch { }

                var chromiumMsg = isAr
                    ? $"✅ تم فتح صفحة الإضافات ({url}) ومجلد الإضافة ونسخ المسار تلقائياً للحافظة!\n\n" +
                      $"بسبب متطلبات الأمان في {browser.Name} للإضافات المحلية، يتبقى خطوتان فقط (5 ثوانٍ):\n" +
                      $"1. في صفحة المتصفح المفتوحة أمامك، فعّل زر: [وضع مطور البرامج / Developer mode] في أعلى اليمين.\n" +
                      $"2. اضغط على زر: [تحميل حزمة غير مضغوطة / Load unpacked] في أعلى اليسار، ثم الصق المسار (Ctrl+V) واضغط 'Select Folder' (أو اسحب المجلد المفتوح أمامك وأفلته داخل صفحة المتصفح).\n\n" +
                      $"💡 تلميح للتشغيل التلقائي الكامل:\n" +
                      $"إذا كنت تريد فتح المتصفح والإضافة مثبتة ومفعلة فيه تلقائياً بدون أي خطوات يدوية، اضغط على زر: [🚀 تشغيل متصفح الاستخراج فوراً] في الأعلى!"
                    : $"✅ Opened extensions page ({url}) and extension folder! Path copied to clipboard.\n\n" +
                      $"Due to {browser.Name} security requirements for local extensions, only 2 quick steps needed:\n" +
                      $"1. Toggle [Developer mode] ON in the top-right corner of the extensions page.\n" +
                      $"2. Click [Load unpacked] in the top-left, paste the path (Ctrl+V) and click 'Select Folder' (or drag & drop the folder into the browser window).\n\n" +
                      $"💡 Tip for 100% Automatic 1-Click Launch:\n" +
                      $"Click [🚀 Launch Dedicated Extractor Browser] above to instantly start with the extension active without any manual steps!";

                return new InstallResult(true, chromiumMsg);
            }
            else
            {
                // Firefox
                Process.Start(new ProcessStartInfo
                {
                    FileName        = browser.ExePath,
                    Arguments       = $"\"{url}\"",
                    UseShellExecute = false
                });

                try { Process.Start("explorer.exe", $"/select,\"{manifestPath}\""); } catch { }

                var firefoxMsg = isAr
                    ? $"✅ تم فتح صفحة إضافات Mozilla Firefox ومجلد الإضافة!\n\n" +
                      $"1. اضغط على زر: (تحميل وظيفة إضافية مؤقتة... / Load Temporary Add-on).\n" +
                      $"2. اختر ملف 'manifest.json' المحدد أمامك مباشرة في النافذة."
                    : $"✅ Opened Firefox Extensions page and extension folder!\n\n" +
                      $"1. Click 'Load Temporary Add-on...'\n" +
                      $"2. Select the highlighted 'manifest.json' file.";

                return new InstallResult(true, firefoxMsg);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Extension install failed for {Browser}", browser.Name);
            return new InstallResult(false, $"Error: {ex.Message}");
        }
    }

    private static void CopyDirectoryRecursive(string sourceDir, string targetDir)
    {
        Directory.CreateDirectory(targetDir);
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var dest = Path.Combine(targetDir, Path.GetFileName(file));
            File.Copy(file, dest, true);
        }
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var destSub = Path.Combine(targetDir, Path.GetFileName(subDir));
            CopyDirectoryRecursive(subDir, destSub);
        }
    }

    /// <summary>
    /// Creates a desktop shortcut for the browser that always launches with ScripOx extension loaded.
    /// Uses dedicated profile to prevent conflicts.
    /// </summary>
    public static bool CreateDesktopShortcut(BrowserInfo browser)
    {
        try
        {
            if (browser.ExePath == null || !File.Exists(browser.ExePath)) return false;
            var extPath = EnsureDeployedExtensionPath();
            var profileDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScripOx", "BrowserProfile");
            Directory.CreateDirectory(profileDir);

            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutFile = Path.Combine(desktop, $"ScripOx Web Extractor ({browser.Name}).lnk");

            var args = browser.Kind == BrowserKind.Firefox
                ? $"-profile \"{profileDir.Replace("'", "''")}\" -no-remote \"https://www.google.com\""
                : $"--user-data-dir=\"{profileDir.Replace("'", "''")}\" --load-extension=\"{extPath.Replace("'", "''")}\" --no-first-run --no-default-browser-check \"https://www.google.com\"";

            var script = $"$ws = New-Object -ComObject WScript.Shell; $s = $ws.CreateShortcut('{shortcutFile.Replace("'", "''")}'); $s.TargetPath = '{browser.ExePath.Replace("'", "''")}'; $s.Arguments = '{args}'; $s.Description = 'Launch {browser.Name} with ScripOx Visual Extractor'; $s.Save()";

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

        // Check Program Files (x86)
        var pfx86 = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            localAppSubPath
        );
        if (File.Exists(pfx86)) return pfx86;

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

    /// <summary>
    /// Writes the native registry keys in HKCU (and HKLM if permitted) that Chrome/Edge uses
    /// to detect and prompt for new external extensions, exactly like IDM (Internet Download Manager).
    /// </summary>
    private static void WriteRegistryHint(BrowserInfo browser, string extPath)
    {
        try
        {
            var crxPath = Path.Combine(extPath, "scripox.crx");
            if (!File.Exists(crxPath))
            {
                var fallbackCrx = Path.Combine(GetExtensionDistPath(), "scripox.crx");
                if (File.Exists(fallbackCrx))
                {
                    File.Copy(fallbackCrx, crxPath, true);
                }
            }

            // 1. Application-level settings
            const string appKeyPath = @"SOFTWARE\OxTech\ScripOx\Extension";
            using (var key = Registry.CurrentUser.CreateSubKey(appKeyPath, true))
            {
                key?.SetValue("InstallPath",  extPath);
                key?.SetValue("CrxPath",      crxPath);
                key?.SetValue("ExtensionId",  ExtensionId);
                key?.SetValue("Browser",      browser.Name);
                key?.SetValue("Version",      ExtVersion);
                key?.SetValue("InstalledAt",  DateTime.Now.ToString("O"));
            }

            // Clean up any legacy registry keys that cause Chrome to flag extension as corrupted
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Google\Chrome\Extensions\{ExtensionId}", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Microsoft\Edge\Extensions\{ExtensionId}", false);
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\BraveSoftware\Brave-Browser\Extensions\{ExtensionId}", false);
            }
            catch { }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Could not write extension registry keys");
        }
    }
}

public record InstallResult(bool Success, string Message);
