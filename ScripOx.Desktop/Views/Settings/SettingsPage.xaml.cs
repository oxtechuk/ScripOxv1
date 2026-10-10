using System;
using System.Windows;
using System.Windows.Controls;
using ScripOx.Desktop.Services;
using ScripOx.Desktop.Views.Updates;

namespace ScripOx.Desktop.Views.Settings;

public partial class SettingsPage : Page
{
    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
        Unloaded += SettingsPage_Unloaded;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        LocalizationService.LanguageChanged += OnLanguageChanged;
        ApplyLocalization();

        ChkAutoUpdate.IsChecked = AppSettings.AutoCheckUpdatesOnStartup;
        TxtApiPort.Text = AppSettings.ApiPort.ToString();
    }

    private void SettingsPage_Unloaded(object sender, RoutedEventArgs e)
    {
        LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, string lang)
    {
        Dispatcher.Invoke(ApplyLocalization);
    }

    private void ApplyLocalization()
    {
        FlowDirection = LocalizationService.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        TxtSettingsTitle.Text    = LocalizationService.Get("settings.title");
        TxtSettingsSubtitle.Text = LocalizationService.Get("settings.subtitle");

        TxtMarketTitle.Text      = LocalizationService.Get("settings.market_title");
        TxtGdprTitle.Text        = LocalizationService.Get("settings.gdpr_title");
        TxtGdprDesc.Text         = LocalizationService.Get("settings.gdpr_desc");
        TxtFtaTitle.Text         = LocalizationService.Get("settings.fta_title");
        TxtFtaDesc.Text          = LocalizationService.Get("settings.fta_desc");

        TxtApiTitle.Text         = LocalizationService.Get("settings.api_title");
        TxtApiPortLbl.Text       = LocalizationService.Get("settings.api_port_lbl");
        BtnRestartServer.Content = LocalizationService.Get("settings.btn_save_restart");
        BtnTestConnection.Content = LocalizationService.Get("settings.btn_test_conn");
        TxtCurrencyLbl.Text      = LocalizationService.Get("settings.currency_lbl");

        TxtUpdatesTitle.Text     = LocalizationService.Get("settings.updates_title");
        TxtAutoUpdateTitle.Text  = LocalizationService.Get("settings.auto_update_title");
        TxtAutoUpdateDesc.Text   = LocalizationService.Get("settings.auto_update_desc");
        BtnCheckUpdates.Content  = LocalizationService.Get("settings.btn_check_updates");

        TxtSupportTitle.Text     = LocalizationService.Get("settings.support_title");
        TxtSupportSub.Text       = LocalizationService.Get("settings.support_sub");
        TxtSupportDesc.Text      = LocalizationService.Get("settings.support_desc");
        TxtWebBtnText.Text       = $"{LocalizationService.Get("settings.support_web")}: oxtech.uk";
        TxtWaBtnText.Text        = $"{LocalizationService.Get("settings.support_wa")}: +201008616682";
        TxtCallBtnText.Text      = $"{LocalizationService.Get("settings.support_call")}: +201008616682";

        var curVer = UpdateService.GetCurrentVersion();
        var lang = LocalizationService.CurrentLanguage;
        TxtUpdateStatus.Text = lang switch
        {
            "ar" => $"الإصدار الحالي: v{curVer.Major}.{curVer.Minor}.{curVer.Build}",
            "de" => $"Aktuelle Version: v{curVer.Major}.{curVer.Minor}.{curVer.Build}",
            _    => $"Current Version: v{curVer.Major}.{curVer.Minor}.{curVer.Build}"
        };
    }

    private void ChkAutoUpdate_Changed(object sender, RoutedEventArgs e)
    {
        AppSettings.AutoCheckUpdatesOnStartup = ChkAutoUpdate.IsChecked == true;
    }

    private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        AppSettings.GitHubOwner = "oxtechuk";
        AppSettings.GitHubRepo = "ScripOxv1";
        BtnCheckUpdates.IsEnabled = false;

        var isAr = LocalizationService.CurrentLanguage == "ar";
        TxtUpdateStatus.Text = isAr ? "جاري التحقق من التحديثات..." : "Checking for updates...";

        try
        {
            var update = await UpdateService.CheckForUpdatesAsync();
            var curVer = UpdateService.GetCurrentVersion();

            if (update == null)
            {
                TxtUpdateStatus.Text = isAr
                    ? "تعذر الاتصال بخوادم التحديث حالياً، يرجى المحاولة لاحقاً."
                    : "Could not connect to update servers currently, please try again later.";
            }
            else if (update.IsUpdateAvailable)
            {
                TxtUpdateStatus.Text = isAr
                    ? $"يتوفر إصدار أحدث ({update.TagName})!"
                    : $"New update available ({update.TagName})!";

                var window = Window.GetWindow(this);
                var dialog = new UpdateDialog(update) { Owner = window };
                dialog.ShowDialog();
            }
            else
            {
                TxtUpdateStatus.Text = isAr
                    ? $"أنت تعمل بأحدث إصدار (v{curVer.Major}.{curVer.Minor}.{curVer.Build}) ✓"
                    : $"You are running the latest version (v{curVer.Major}.{curVer.Minor}.{curVer.Build}) ✓";
            }
        }
        catch (Exception ex)
        {
            TxtUpdateStatus.Text = isAr ? $"خطأ: {ex.Message}" : $"Error: {ex.Message}";
        }
        finally
        {
            BtnCheckUpdates.IsEnabled = true;
        }
    }

    private void BtnRestartServer_Click(object sender, RoutedEventArgs e)
    {
        if (int.TryParse(TxtApiPort.Text.Trim(), out int port) && port > 0 && port < 65536)
        {
            AppSettings.ApiPort = port;
            ((App)Application.Current).RestartPythonBackend();
            TxtApiStatus.Text = "Server restarted on port " + port;
        }
        else
        {
            MessageBox.Show("Please enter a valid port number (1-65535).", "Invalid Port", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnTestConnection_Click(object sender, RoutedEventArgs e)
    {
        TxtApiStatus.Text = "Testing connection...";
        BtnTestConnection.IsEnabled = false;

        try
        {
            var apiService = new ExtractorApiService();
            bool isHealthy = await apiService.IsHealthyAsync();
            TxtApiStatus.Text = isHealthy ? "Status: Connected (OK)" : "Status: Disconnected / Not Running";
        }
        catch (Exception ex)
        {
            TxtApiStatus.Text = "Status: Error - " + ex.Message;
        }
        finally
        {
            BtnTestConnection.IsEnabled = true;
        }
    }

    private void BtnOxTechWebsite_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://oxtech.uk")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open website: {ex.Message}", "OX Tech", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnOxTechWhatsApp_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://wa.me/201008616682")
            {
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Could not open WhatsApp: {ex.Message}", "OX Tech", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private void BtnOxTechCall_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText("+201008616682");
            var isAr = LocalizationService.CurrentLanguage == "ar";
            var msg = isAr
                ? "تم نسخ الرقم إلى الحافظة (+201008616682)\nيمكنك الاتصال مباشرة أو إرسال رسالة."
                : "Phone number copied to clipboard (+201008616682)\nYou can call or message directly.";
            MessageBox.Show(msg, "OX Tech Support", MessageBoxButton.OK, MessageBoxImage.Information);

            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("tel:+201008616682")
            {
                UseShellExecute = true
            });
        }
        catch
        {
            // tel protocol might not have a default handler on Windows, clipboard is already copied
        }
    }
}
