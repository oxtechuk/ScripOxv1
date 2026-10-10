using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Web.WebView2.Core;
using ScripOx.Desktop.Services;
using ScripOx.Desktop.Views.Companies;
using Serilog;

namespace ScripOx.Desktop.Views.Map;

public partial class MapPage : Page
{
    private readonly CompanyService _companyService;
    private bool _isMapReady = false;

    public MapPage()
    {
        InitializeComponent();
        _companyService = new CompanyService(AppSettings.ConnectionString);
        Loaded += MapPage_Loaded;
    }

    private bool _isInitialized = false;

    private async void MapPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isInitialized) return;
        _isInitialized = true;
        await InitializeWebViewAsync();
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var env = await WebView2Helper.GetSharedEnvironmentAsync();
            await MapWebView.EnsureCoreWebView2Async(env);
            
            MapWebView.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;

            string mapFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "map");
            string mapPath = Path.Combine(mapFolder, "map.html");

            if (File.Exists(mapPath))
            {
                try
                {
                    MapWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "appassets.example",
                        mapFolder,
                        CoreWebView2HostResourceAccessKind.Allow
                    );
                    MapWebView.CoreWebView2.Navigate("https://appassets.example/map.html");
                }
                catch (Exception ex)
                {
                    Log.Warning(ex, "SetVirtualHostNameToFolderMapping failed in MapPage, falling back to direct URI");
                    MapWebView.CoreWebView2.Navigate(new Uri(mapPath).AbsoluteUri);
                }
            }
            else
            {
                Log.Warning("Map HTML file not found at {MapPath}", mapPath);
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize WebView2 in MapPage");
        }
    }

    private async void CoreWebView2_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        var msg = e.TryGetWebMessageAsString();
        if (string.IsNullOrEmpty(msg))
        {
            msg = e.WebMessageAsJson;
        }
        if (string.IsNullOrEmpty(msg)) return;

        try
        {
            var json = JsonDocument.Parse(msg);
            if (json.RootElement.TryGetProperty("action", out var actionEl))
            {
                var action = actionEl.GetString();
                if (action == "mapReady")
                {
                    _isMapReady = true;
                    await LoadMapDataAsync();
                }
                else if (action == "openCompany" && json.RootElement.TryGetProperty("id", out var idEl))
                {
                    var id = idEl.GetInt64();
                    var details = new CompanyDetailsWindow(id);
                    details.ShowDialog();
                }
                else if (action == "jsError" && json.RootElement.TryGetProperty("message", out var msgEl))
                {
                    Log.Warning("Map JavaScript warning: {Message}", msgEl.GetString());
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error handling WebView2 message");
        }
    }

    private async Task LoadMapDataAsync()
    {
        if (!_isMapReady) return;

        var points = await _companyService.GetMapPointsAsync();
        
        var jsonStr = JsonSerializer.Serialize(points, new JsonSerializerOptions 
        { 
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
        });

        var script = $"loadPoints({JsonSerializer.Serialize(jsonStr)});";
        await MapWebView.CoreWebView2.ExecuteScriptAsync(script);
    }
}
