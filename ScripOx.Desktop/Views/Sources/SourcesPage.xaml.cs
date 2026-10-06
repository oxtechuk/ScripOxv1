using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScripOx.Desktop.Models;
using ScripOx.Desktop.Services;

namespace ScripOx.Desktop.Views.Sources;

public partial class SourcesPage : Page
{
    private readonly SourceService _sourceService;
    private List<Source> _sources = new();

    public SourcesPage()
    {
        InitializeComponent();
        _sourceService = new SourceService(AppSettings.ConnectionString);
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        await LoadSourcesAsync();
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await LoadSourcesAsync();
    }

    private async Task LoadSourcesAsync()
    {
        _sources = await _sourceService.GetSourcesAsync();
        SourcesList.ItemsSource = _sources;

        TxtNoSources.Visibility = _sources.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        PanelConfigDetails.Visibility = Visibility.Collapsed;
        PanelEmptyInspector.Visibility = Visibility.Visible;
    }

    // ── Show Config Details ───────────────────────────────────

    private void BtnViewConfig_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Source src)
        {
            TxtInspectorTitle.Text = src.Name;
            TxtInspectorUrl.Text   = src.BaseUrl;

            // Load selectors and mapping from config_json
            string bestSelector = "—";
            string xpath = "—";
            Dictionary<string, string> mapping = new();

            if (!string.IsNullOrEmpty(src.ConfigJson))
            {
                try
                {
                    using var doc = JsonDocument.Parse(src.ConfigJson);
                    var root = doc.RootElement;

                    if (root.TryGetProperty("selector", out var selProp))
                        bestSelector = selProp.GetString() ?? "—";

                    if (root.TryGetProperty("xpath", out var xpProp))
                        xpath = xpProp.GetString() ?? "—";

                    if (root.TryGetProperty("mapping", out var mapProp) && mapProp.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in mapProp.EnumerateObject())
                        {
                            mapping[prop.Name] = prop.Value.GetString() ?? "";
                        }
                    }
                }
                catch
                {
                    // JSON parsing failed
                }
            }

            TxtBestSelector.Text = bestSelector;
            TxtXPath.Text        = xpath;

            // Populate Field Mappings UI
            PanelFieldMappings.Children.Clear();
            if (mapping.Count == 0)
            {
                PanelFieldMappings.Children.Add(new TextBlock
                {
                    Text       = "No mapped fields",
                    FontSize   = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(74, 127, 160)),
                    FontStyle  = FontStyles.Italic
                });
            }
            else
            {
                foreach (var kvp in mapping)
                {
                    var item = new Border
                    {
                        Background      = new SolidColorBrush(Color.FromRgb(13, 27, 42)),
                        BorderBrush     = new SolidColorBrush(Color.FromRgb(30, 58, 95)),
                        BorderThickness = new Thickness(1),
                        CornerRadius    = new CornerRadius(6),
                        Padding         = new Thickness(10, 6, 10, 6),
                        Margin          = new Thickness(0, 0, 0, 6)
                    };

                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(80) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                    var keyLbl = new TextBlock
                    {
                        Text              = kvp.Key,
                        FontSize          = 11,
                        FontWeight        = FontWeights.SemiBold,
                        Foreground        = new SolidColorBrush(Color.FromRgb(96, 165, 250)),
                        VerticalAlignment = VerticalAlignment.Center
                    };
                    Grid.SetColumn(keyLbl, 0);
                    grid.Children.Add(keyLbl);

                    var valLbl = new TextBlock
                    {
                        Text              = kvp.Value,
                        FontSize          = 11,
                        FontFamily        = new FontFamily("Consolas"),
                        Foreground        = Brushes.White,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextWrapping      = TextWrapping.Wrap
                    };
                    Grid.SetColumn(valLbl, 1);
                    grid.Children.Add(valLbl);

                    item.Child = grid;
                    PanelFieldMappings.Children.Add(item);
                }
            }

            PanelEmptyInspector.Visibility = Visibility.Collapsed;
            PanelConfigDetails.Visibility  = Visibility.Visible;
        }
    }

    // ── Delete Script ─────────────────────────────────────────

    private async void BtnDelete_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Source src)
        {
            var res = MessageBox.Show(
                $"Are you sure you want to delete script '{src.Name}'?\nAll associated jobs and raw records will be deleted as well.",
                "ScripOx",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning
            );

            if (res == MessageBoxResult.Yes)
            {
                var ok = await _sourceService.DeleteSourceAsync(src.Id);
                if (ok)
                {
                    await LoadSourcesAsync();
                }
                else
                {
                    MessageBox.Show("Could not delete the script.", "ScripOx", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }
    }
}
