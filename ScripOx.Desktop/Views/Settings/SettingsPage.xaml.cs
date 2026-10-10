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
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        ChkAutoUpdate.IsChecked = AppSettings.AutoCheckUpdatesOnStartup;
        TxtGithubOwner.Text = AppSettings.GitHubOwner;
        TxtGithubRepo.Text = AppSettings.GitHubRepo;
        TxtApiPort.Text = AppSettings.ApiPort.ToString();

        var curVer = UpdateService.GetCurrentVersion();
        TxtUpdateStatus.Text = LocalizationService.CurrentLanguage == "ar"
            ? $"الإصدار الحالي: v{curVer.Major}.{curVer.Minor}.{curVer.Build}"
            : $"Current Version: v{curVer.Major}.{curVer.Minor}.{curVer.Build}";
    }

    private void ChkAutoUpdate_Changed(object sender, RoutedEventArgs e)
    {
        AppSettings.AutoCheckUpdatesOnStartup = ChkAutoUpdate.IsChecked == true;
    }

    private void TxtGithubRepo_LostFocus(object sender, RoutedEventArgs e)
    {
        AppSettings.GitHubOwner = TxtGithubOwner.Text.Trim();
        AppSettings.GitHubRepo = TxtGithubRepo.Text.Trim();
    }

    private async void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
    {
        TxtGithubRepo_LostFocus(sender, e);
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
                    ? "تعذر الاتصال بخوادم GitHub أو لا يوجد إصدار منشور بعد."
                    : "Could not connect to GitHub or no releases published yet.";
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
}
