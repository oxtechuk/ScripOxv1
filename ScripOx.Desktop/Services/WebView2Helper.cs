using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Web.WebView2.Core;
using Serilog;

namespace ScripOx.Desktop.Services;

/// <summary>
/// Provides a shared CoreWebView2Environment for all WebView2 controls in the application.
/// Ensures consistent user data folder in %LOCALAPPDATA%\ScripOx\WebView2 to prevent
/// conflicts, permission issues, or mismatched environment exceptions.
/// </summary>
public static class WebView2Helper
{
    private static CoreWebView2Environment? _sharedEnvironment;
    private static readonly SemaphoreSlim _lock = new(1, 1);

    public static bool IsWebView2Available()
    {
        try
        {
            var version = CoreWebView2Environment.GetAvailableBrowserVersionString();
            return !string.IsNullOrEmpty(version);
        }
        catch
        {
            return false;
        }
    }

    public static async Task EnsureWebView2RuntimeInstalledAsync()
    {
        if (IsWebView2Available()) return;

        Log.Information("WebView2 Runtime not found. Attempting automatic internal installation...");
        try
        {
            var tempInstaller = Path.Combine(Path.GetTempPath(), "MicrosoftEdgeWebview2Setup.exe");
            using (var client = new System.Net.Http.HttpClient())
            {
                var bytes = await client.GetByteArrayAsync("https://go.microsoft.com/fwlink/p/?LinkId=2124703");
                await File.WriteAllBytesAsync(tempInstaller, bytes);
            }

            var psi = new ProcessStartInfo
            {
                FileName = tempInstaller,
                Arguments = "/silent /install",
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using var proc = Process.Start(psi);
            if (proc != null)
            {
                await proc.WaitForExitAsync();
            }

            try { File.Delete(tempInstaller); } catch { }
            Log.Information("WebView2 Runtime installed automatically.");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to automatically install WebView2 Runtime");
        }
    }

    public static async Task<CoreWebView2Environment> GetSharedEnvironmentAsync()
    {
        if (_sharedEnvironment != null) return _sharedEnvironment;

        await _lock.WaitAsync();
        try
        {
            if (_sharedEnvironment != null) return _sharedEnvironment;

            if (!IsWebView2Available())
            {
                await EnsureWebView2RuntimeInstalledAsync();
            }

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var webViewUserData = Path.Combine(localAppData, "ScripOx", "WebView2");
            Directory.CreateDirectory(webViewUserData);

            _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, webViewUserData);
            Log.Information("CoreWebView2Environment initialized successfully at {UserDataFolder}", webViewUserData);
            return _sharedEnvironment;
        }
        catch (WebView2RuntimeNotFoundException ex)
        {
            Log.Error(ex, "Microsoft Edge WebView2 Runtime is not installed. Interactive maps require WebView2 Runtime.");
            throw;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize shared CoreWebView2Environment");
            throw;
        }
        finally
        {
            _lock.Release();
        }
    }
}
