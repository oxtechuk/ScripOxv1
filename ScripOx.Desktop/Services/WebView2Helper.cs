using System;
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

    public static async Task<CoreWebView2Environment> GetSharedEnvironmentAsync()
    {
        if (_sharedEnvironment != null) return _sharedEnvironment;

        await _lock.WaitAsync();
        try
        {
            if (_sharedEnvironment != null) return _sharedEnvironment;

            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var webViewUserData = Path.Combine(localAppData, "ScripOx", "WebView2");
            Directory.CreateDirectory(webViewUserData);

            _sharedEnvironment = await CoreWebView2Environment.CreateAsync(null, webViewUserData);
            Log.Information("CoreWebView2Environment initialized successfully at {UserDataFolder}", webViewUserData);
            return _sharedEnvironment;
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
