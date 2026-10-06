using System.Globalization;
using System.Resources;
using System.Windows;
using Serilog;

namespace ScripOx.Desktop.Services;

/// <summary>
/// Manages application language (en/ar) and RTL/LTR flow direction.
/// Call Initialize() once at startup, then SetLanguage() to switch.
/// </summary>
public static class LocalizationService
{
    private static ResourceManager? _rm;
    private static string _currentLang = "en";

    public static string CurrentLanguage => _currentLang;
    public static bool IsRtl => _currentLang == "ar";

    public static void Initialize(string language)
    {
        string resName = language switch {
            "ar" => "ar",
            "de" => "de",
            _    => "en"
        };
        _rm = new ResourceManager(
            "ScripOx.Desktop.Resources.i18n." + resName,
            typeof(LocalizationService).Assembly
        );
        _currentLang = language switch {
            "ar" => "ar",
            "de" => "de",
            _    => "en"
        };
        ApplyFlowDirection();
        Log.Information("Language set to: {Lang}", _currentLang);
    }

    public static void SetLanguage(string language)
    {
        Initialize(language);
        // Raise a global event so views can refresh
        LanguageChanged?.Invoke(null, language);
    }

    /// <summary>Fired when the user changes the language.</summary>
    public static event EventHandler<string>? LanguageChanged;

    /// <summary>Gets a translated string by key.</summary>
    public static string Get(string key)
    {
        try
        {
            return _rm?.GetString(key) ?? key;
        }
        catch
        {
            return key;
        }
    }

    // ── Private ──────────────────────────────────────────────

    private static void ApplyFlowDirection()
    {
        var dir = IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Application.Current?.Dispatcher.Invoke(() =>
        {
            foreach (Window window in Application.Current.Windows)
                window.FlowDirection = dir;
        });
    }
}
