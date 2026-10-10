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

            // ── Step 1: Deploy extension to a clean, stable LocalAppData directory ──
            var localExtPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScripOx", "Extension");
            try
            {
                Directory.CreateDirectory(localExtPath);
                CopyDirectoryRecursive(extPath, localExtPath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to copy extension to LocalAppData, falling back to original path");
                localExtPath = extPath;
            }

            try
            {
                Clipboard.SetText(localExtPath);
            }
            catch (Exception ex)
            {
                Log.Warning(ex, "Failed to copy extension path to clipboard");
            }

            // ── Step 2: Write Registry policies & external extension hints ──
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
            var procName = Path.GetFileNameWithoutExtension(browser.ExePath);
            var runningProcs = Process.GetProcessesByName(procName);

            if (!isFirefox && runningProcs.Length > 0)
            {
                var promptMsg = isAr
                    ? $"متصفح {browser.Name} قيد التشغيل حالياً (قد تكون هناك نوافذ أو عمليات في الخلفية).\n\nليتم تحميل الإضافة وتفعيلها في المتصفح تلقائياً، يجب إغلاق المتصفح وإعادة فتحه.\n\nهل ترغب في إعادة تشغيل {browser.Name} الآن؟"
                    : $"{browser.Name} is currently running in the background.\n\nTo auto-load and enable the extension, the browser needs to be restarted.\n\nWould you like to restart {browser.Name} now?";

                var dialogResult = MessageBox.Show(promptMsg, isAr ? "تثبيت إضافة ScripOx" : "Install ScripOx Extension", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (dialogResult == MessageBoxResult.Yes)
                {
                    try
                    {
                        var psiKill = new ProcessStartInfo
                        {
                            FileName = "taskkill",
                            Arguments = $"/F /IM {procName}.exe /T",
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        var pKill = Process.Start(psiKill);
                        pKill?.WaitForExit(3000);
                    }
                    catch { }
                    await Task.Delay(1200);
                }
            }

            // ── Step 3: Launch browser & Trigger Extension Load ──────
            if (isFirefox)
            {
                var xpiPath = Path.Combine(localExtPath, "scripox.xpi");
                var manifestPath = Path.Combine(localExtPath, "manifest.json");
                try { Clipboard.SetText(manifestPath); } catch { }

                if (File.Exists(xpiPath))
                {
                    // Launch Firefox pointing directly to the .xpi installer file to prompt native installation!
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = browser.ExePath,
                        Arguments       = $"\"{xpiPath}\"",
                        UseShellExecute = false,
                    });
                }
                else
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName        = browser.ExePath,
                        Arguments       = $"\"{url}\"",
                        UseShellExecute = false,
                    });
                }

                try { Process.Start("explorer.exe", $"/select,\"{manifestPath}\""); } catch { }

                var firefoxSuccessMsg = isAr
                    ? $"✅ تم إطلاق Mozilla Firefox وتوجيه حزمة الإضافة!\n\n" +
                      $"1. سيظهر لك إشعار في متصفح Firefox يطلب الإذن لتثبيت إضافة ScripOx، اضغط: (إضافة / Add).\n" +
                      $"2. أو يمكنك فتح صفحة ({url}) والضغط على 'تحميل وظيفة إضافية مؤقتة' واختيار manifest.json المفتوح أمامك مباشرة."
                    : $"✅ Launched Mozilla Firefox with ScripOx extension package!\n\n" +
                      $"1. Firefox will show a prompt to Add ScripOx extension, click (Add).\n" +
                      $"2. Or open ({url}) and load manifest.json directly.";

                return new InstallResult(true, firefoxSuccessMsg);
            }
            else
            {
                // Chromium: Chrome / Brave / Edge
                var args = $"--load-extension=\"{localExtPath}\" --enable-extensions \"{url}\"";
                Process.Start(new ProcessStartInfo
                {
                    FileName        = browser.ExePath,
                    Arguments       = args,
                    UseShellExecute = false,
                });

                try { Process.Start("explorer.exe", localExtPath); } catch { }

                var successMsg = isAr
                    ? $"✅ تم إطلاق {browser.Name} مع إضافة ScripOx بنجاح!\n\n" +
                      $"• تم تمرير أمر التحميل التلقائي: --load-extension\n" +
                      $"• تم حفظ الإضافة في مسار آمن ونسخ المسار تلقائياً.\n\n" +
                      $"📌 خطوات إضافية لمتصفح {browser.Name}:\n" +
                      $"1. في صفحة الإضافات المفتوحة، تأكد من تفعيل زر: (وضع مطور البرامج / Developer mode) بأعلى اليمين.\n" +
                      $"2. يمكنك سحب مجلد الإضافة المفتوح أمامك وإفلاته داخل نافذة المتصفح (Drag & Drop)، أو الضغط على زر 'تحميل حزمة غير مضغوطة' ولصق المسار (Ctrl + V).\n" +
                      $"3. اضغط على أيقونة الإضافات (🧩) بجوار شريط العنوان في المتصفح، واضغط على زر التثبيت (Pin 📌) لتظهر أيقونة ScripOx دائماً."
                    : $"✅ {browser.Name} launched with ScripOx Extension loaded!\n\n" +
                      $"• Loaded via --load-extension command argument\n" +
                      $"• Extension folder opened in Explorer and path copied to clipboard\n\n" +
                      $"📌 Steps:\n" +
                      $"1. Enable 'Developer mode' in top-right corner of {browser.Name}.\n" +
                      $"2. Drag & drop the extension folder into the browser, or click 'Load unpacked'.\n" +
                      $"3. Pin (📌) the ScripOx icon to your toolbar!";

                return new InstallResult(true, successMsg);
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
    /// </summary>
    public static bool CreateDesktopShortcut(BrowserInfo browser)
    {
        try
        {
            if (browser.ExePath == null || !File.Exists(browser.ExePath)) return false;
            var extPath = GetExtensionDistPath();
            var desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            var shortcutFile = Path.Combine(desktop, $"{browser.Name} (ScripOx Extractor).lnk");

            var args = browser.Kind == BrowserKind.Firefox
                ? "about:debugging#/runtime/this-firefox"
                : $"--load-extension=\"\"{extPath.Replace("'", "''")}\"\" --enable-extensions";

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
