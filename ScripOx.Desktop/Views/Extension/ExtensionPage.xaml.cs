using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScripOx.Desktop.Services;

namespace ScripOx.Desktop.Views.Extension;

public partial class ExtensionPage : Page
{
    private ExtensionInstallerService.BrowserInfo? _selectedBrowser;
    private List<ExtensionInstallerService.BrowserInfo> _browsers = new();

    public ExtensionPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        LoadBrowsers();
        DetectExtensionPath();
        BuildStepsPanel();
        await RefreshApiStatusAsync();
    }

    private void LoadBrowsers()
    {
        _browsers = ExtensionInstallerService.DetectBrowsers();

        BrowserList.Children.Clear();
        foreach (var browser in _browsers)
        {
            var card = BuildBrowserCard(browser);
            BrowserList.Children.Add(card);
        }
    }

    private Border BuildBrowserCard(ExtensionInstallerService.BrowserInfo browser)
    {
        var card = new Border
        {
            Background      = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF")),
            BorderBrush     = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0")),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(14),
            Padding         = new Thickness(18, 14, 18, 14),
            Margin          = new Thickness(0, 0, 0, 10),
            Cursor          = System.Windows.Input.Cursors.Hand,
            Tag             = browser,
        };

        var grid = new Grid();
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(42) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

        // Clean initial letter badge instead of emojis
        var iconBadge = new Border
        {
            Width = 32, Height = 32,
            CornerRadius = new CornerRadius(8),
            Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
            VerticalAlignment = VerticalAlignment.Center,
        };
        iconBadge.Child = new TextBlock
        {
            Text = string.IsNullOrEmpty(browser.Name) ? "B" : browser.Name.Substring(0, 1).ToUpper(),
            FontSize = 13,
            FontWeight = FontWeights.Bold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(iconBadge, 0);
        grid.Children.Add(iconBadge);

        // Name + path
        var info = new StackPanel { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0, 0, 0) };
        info.Children.Add(new TextBlock
        {
            Text       = browser.Name,
            FontSize   = 13,
            FontWeight = FontWeights.SemiBold,
            Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
        });
        info.Children.Add(new TextBlock
        {
            Text          = browser.IsInstalled ? (browser.ExePath ?? "Available") : "Not installed on this machine",
            FontSize      = 11,
            Foreground    = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
            TextTrimming  = TextTrimming.CharacterEllipsis,
        });
        Grid.SetColumn(info, 1);
        grid.Children.Add(info);

        // Status Badge (Apple pill)
        var badge = new Border
        {
            CornerRadius      = new CornerRadius(12),
            Padding           = new Thickness(10, 4, 10, 4),
            VerticalAlignment = VerticalAlignment.Center,
            Background        = browser.IsInstalled
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
        };
        badge.Child = new TextBlock
        {
            Text       = browser.IsInstalled ? "Detected" : "Not Found",
            FontSize   = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = browser.IsInstalled
                ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"))
                : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
        };
        Grid.SetColumn(badge, 2);
        grid.Children.Add(badge);

        card.Child = grid;
        card.MouseLeftButtonDown += (_, _) => SelectBrowser(browser, card);
        return card;
    }

    private void SelectBrowser(ExtensionInstallerService.BrowserInfo browser, Border card)
    {
        foreach (Border item in BrowserList.Children)
        {
            item.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#E2E8F0"));
            item.Background  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FFFFFF"));
        }

        card.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A"));
        card.Background  = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F8FAFC"));

        _selectedBrowser            = browser;
        BdrSelectedBrowser.Visibility = Visibility.Visible;
        TxtSelectedIcon.Text        = browser.Name.Length > 0 ? browser.Name.Substring(0, 1).ToUpper() : "";
        TxtSelectedName.Text        = browser.Name;
        BtnInstall.IsEnabled        = true;
    }

    private void DetectExtensionPath()
    {
        var path = ExtensionInstallerService.GetExtensionDistPath();
        TxtExtPath.Text     = path;
        TxtExtPath.Foreground = Directory.Exists(path)
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B91C1C"));
    }

    private void BuildStepsPanel()
    {
        StepsPanel.Children.Clear();
        var steps = new[]
        {
            ("1", "Click 'Open Browser Extensions Page' below"),
            ("2", "Browser opens to extensions manager"),
            ("3", "Enable Developer Mode (top-right toggle)"),
            ("4", "Click 'Load unpacked' button"),
            ("5", "Select the extension folder shown above"),
            ("6", "Extension is active and linked to ScripOx"),
        };

        foreach (var (num, text) in steps)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(26) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var badge = new Border
            {
                Background    = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F1F5F9")),
                CornerRadius  = new CornerRadius(11),
                Width = 22, Height = 22,
                VerticalAlignment = VerticalAlignment.Top,
            };
            badge.Child = new TextBlock
            {
                Text                = num,
                FontSize            = 11,
                FontWeight          = FontWeights.Bold,
                Foreground          = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0F172A")),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            };

            Grid.SetColumn(badge, 0);
            row.Children.Add(badge);

            var label = new TextBlock
            {
                Text         = text,
                FontSize     = 12,
                Foreground   = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
                TextWrapping = TextWrapping.Wrap,
                Margin       = new Thickness(8, 2, 0, 0),
            };
            Grid.SetColumn(label, 1);
            row.Children.Add(label);

            StepsPanel.Children.Add(row);
        }
    }

    private async void BtnInstall_Click(object sender, RoutedEventArgs e)
    {
        if (_selectedBrowser == null) return;

        BtnInstall.IsEnabled = false;
        BtnInstall.Content   = "Opening Browser...";

        var result = await ExtensionInstallerService.InstallAsync(_selectedBrowser);

        BdrResult.Visibility = Visibility.Visible;
        TxtResult.Text       = result.Message;

        BdrResult.Background = result.Success
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEE2E2"));
        BdrResult.BorderBrush = result.Success
            ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#86EFAC"))
            : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FCA5A5"));
        BdrResult.BorderThickness = new Thickness(1);

        BtnInstall.IsEnabled = true;
        BtnInstall.Content   = "Open Browser Extensions Page";
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        var path = ExtensionInstallerService.GetExtensionDistPath();
        if (Directory.Exists(path))
            Process.Start("explorer.exe", path);
        else
            MessageBox.Show($"Folder not found:\n{path}", "ScripOx",
                            MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private async void BtnRefreshApi_Click(object sender, RoutedEventArgs e)
        => await RefreshApiStatusAsync();

    private async Task RefreshApiStatusAsync()
    {
        var connected = await ExtensionInstallerService.CheckExtensionConnectedAsync();

        if (connected)
        {
            TxtApiStatus.Text      = "Local API Service is Online";
            TxtApiStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#15803D"));
            EllApiDot.Fill         = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#22C55E"));
            BannerApiStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#DCFCE7"));
            BannerApiStatus.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#86EFAC"));
        }
        else
        {
            TxtApiStatus.Text      = "Local API Service is in Standby";
            TxtApiStatus.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#B45309"));
            EllApiDot.Fill         = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F59E0B"));
            BannerApiStatus.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FEF3C7"));
            BannerApiStatus.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FDE68A"));
        }
    }
}
