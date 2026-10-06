using System;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using ScripOx.Desktop.Services;
using Serilog;

namespace ScripOx.Desktop.Views.Updates;

public partial class UpdateDialog : Window
{
    private readonly UpdateReleaseInfo _info;
    private CancellationTokenSource? _cts;

    public UpdateDialog(UpdateReleaseInfo info)
    {
        InitializeComponent();
        _info = info;

        TxtCurrentVersion.Text = $"v{info.CurrentVersion.Major}.{info.CurrentVersion.Minor}.{info.CurrentVersion.Build}";
        TxtNewVersion.Text = info.TagName.StartsWith('v') ? info.TagName : $"v{info.TagName}";
        TxtReleaseNotes.Text = string.IsNullOrWhiteSpace(info.ReleaseNotes)
            ? (LocalizationService.CurrentLanguage == "ar" ? "لا توجد ملاحظات إصدار متاحة." : "No detailed release notes provided.")
            : info.ReleaseNotes;

        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var isAr = LocalizationService.CurrentLanguage == "ar";
        if (isAr)
        {
            TxtTitle.Text = "يتوفر تحديث جديد لـ ScripOx";
            TxtSubtitle.Text = "تم إصدار نسخة أحدث تتضمن تحسينات ومزايا جديدة.";
            BtnUpdateNow.Content = "تحديث الآن";
            BtnLater.Content = "لاحقاً";
            BtnViewGitHub.Content = "عرض على GitHub";
            TxtProgressStatus.Text = "جاري تحميل التحديث...";
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }

    private void BtnLater_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }

    private void BtnViewGitHub_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var url = string.IsNullOrEmpty(_info.HtmlUrl)
                ? $"https://github.com/{AppSettings.GitHubOwner}/{AppSettings.GitHubRepo}/releases"
                : _info.HtmlUrl;

            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to open GitHub release URL.");
        }
    }

    private async void BtnUpdateNow_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_info.DownloadUrl) ||
            _info.DownloadUrl.Equals(_info.HtmlUrl, StringComparison.OrdinalIgnoreCase))
        {
            // If there is no binary asset attached directly, take user to the release webpage
            BtnViewGitHub_Click(sender, e);
            Close();
            return;
        }

        // Disable buttons and show progress
        BtnUpdateNow.IsEnabled = false;
        BtnLater.IsEnabled = false;
        PanelProgress.Visibility = Visibility.Visible;

        _cts = new CancellationTokenSource();
        var fileName = _info.AssetName ?? $"ScripOx-{_info.TagName}.zip";

        var progress = new Progress<double>(p =>
        {
            ProgressBarDownload.Value = p;
            TxtProgressPercent.Text = $"{(int)p}%";
        });

        try
        {
            var downloadedFile = await UpdateService.DownloadUpdateAsync(
                _info.DownloadUrl,
                fileName,
                progress,
                _cts.Token
            );

            // Apply and restart
            UpdateService.ApplyUpdateAndRestart(downloadedFile);
        }
        catch (OperationCanceledException)
        {
            Log.Information("Update download was cancelled.");
            PanelProgress.Visibility = Visibility.Collapsed;
            BtnUpdateNow.IsEnabled = true;
            BtnLater.IsEnabled = true;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to download update asset.");
            var msg = LocalizationService.CurrentLanguage == "ar"
                ? "تعذر تنزيل التحديث تلقائياً. يمكنك تنزيله يدوياً من GitHub."
                : "Failed to download update automatically. You can download it directly from GitHub.";
            MessageBox.Show(msg, "ScripOx Update", MessageBoxButton.OK, MessageBoxImage.Warning);

            BtnViewGitHub_Click(sender, e);
            Close();
        }
    }
}
