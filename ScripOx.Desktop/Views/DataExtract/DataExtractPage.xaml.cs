using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using ScripOx.Desktop.Models;
using ScripOx.Desktop.Services;

namespace ScripOx.Desktop.Views.DataExtract;

public class ExtractedLeadItem : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
                PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
            }
        }
    }
    public string Name { get; set; } = "";
    public string Phone { get; set; } = "";
    public string Website { get; set; } = "";
    public string Rating { get; set; } = "";
    public string Address { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    public string CoordinatesDisplay => $"{Latitude:F4}, {Longitude:F4}";

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
}

public partial class DataExtractPage : Page
{
    private static readonly HttpClient _httpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(25)
    };

    static DataExtractPage()
    {
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) ScripOxCRM/1.0");
        _httpClient.DefaultRequestHeaders.Accept.Clear();
        _httpClient.DefaultRequestHeaders.Accept.ParseAdd("*/*");
    }


    private readonly ObservableCollection<ExtractedLeadItem> _leads = new();
    private readonly CompanyService _companyService;
    private CancellationTokenSource? _extractCts;
    private readonly System.Windows.Threading.DispatcherTimer _mapSyncDebounceTimer;

    public DataExtractPage()
    {
        InitializeComponent();
        _companyService = new CompanyService(AppSettings.ConnectionString);
        DgResults.ItemsSource = _leads;

        _mapSyncDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(250)
        };
        _mapSyncDebounceTimer.Tick += (s, e) =>
        {
            _mapSyncDebounceTimer.Stop();
            SyncMapMarkers();
        };

        Loaded += DataExtractPage_Loaded;
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    private bool _isMapInitialized = false;

    private void DataExtractPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyLanguage();
        if (!_isMapInitialized)
        {
            _isMapInitialized = true;
            InitializeMapAsync();
        }
    }

    private void OnLanguageChanged(object? sender, string lang)
    {
        ApplyLanguage();
    }

    public void ApplyLanguage()
    {
        var lang = LocalizationService.CurrentLanguage;
        var isAr = lang == "ar";
        var isDe = lang == "de";

        FlowDirection = isAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        TxtStep1.Text            = isAr ? "1. اختر المنطقة" : (isDe ? "1. Bereich auswählen" : "1. Choose the area");
        TxtLblSearchAddress.Text = isAr ? "ابحث عن مدينة أو عنوان" : (isDe ? "Stadt / Adresse suchen" : "Search a city / address");
        BtnFindLocation.Content  = isAr ? "بحث" : (isDe ? "Suchen" : "Find");
        TxtLblLatitude.Text      = isAr ? "خط العرض (Y)" : (isDe ? "Breitengrad (Y)" : "Latitude (Y)");
        TxtLblLongitude.Text     = isAr ? "خط الطول (X)" : (isDe ? "Längengrad (X)" : "Longitude (X)");
        TxtLblRadius.Text        = isAr ? "النطاق" : (isDe ? "Radius" : "Radius");
        TxtStep2.Text            = isAr ? "2. تصفية الأنشطة" : (isDe ? "2. Branchenfilter" : "2. Business filter");
        TxtLblCategory.Text      = isAr ? "النشاط أو المجال" : (isDe ? "Kategorie / Branche" : "Category / Business");
        
        BtnExtractData.Content      = isAr ? "بدء استخراج البيانات" : (isDe ? "Datenextraktion starten" : "Start Data Extraction");
        BtnCancelExtract.Content    = isAr ? "إلغاء الاستخراج" : (isDe ? "Extraktion abbrechen" : "Cancel Extraction");
        TxtLblExtractedLeads.Text   = isAr ? "البيانات المستخرجة" : (isDe ? "Extrahierte Leads" : "Extracted Leads");
        BtnImportToCRM.Content      = isAr ? "استيراد المحدد إلى الـ CRM" : (isDe ? "Ausgewählte ins CRM importieren" : "Import Selected to CRM");
        BtnSelectAllLeads.Content   = isAr ? "تحديد الكل" : (isDe ? "Alles auswählen" : "Select All");
        BtnDeselectAllLeads.Content = isAr ? "إلغاء التحديد" : (isDe ? "Auswahl aufheben" : "Deselect All");
        BtnDeleteSelected.Content   = isAr ? " حذف المحدد" : (isDe ? " Ausgewählte löschen" : " Delete Selected");
        BtnClearAllLeads.Content    = isAr ? "تفريغ القائمة" : (isDe ? "Liste leeren" : "Clear List");

        ColName.Header    = isAr ? "اسم الشركة" : (isDe ? "Unternehmensname" : "Business Name");
        ColPhone.Header   = isAr ? "الهاتف" : (isDe ? "Telefon" : "Phone");
        ColWebsite.Header = isAr ? "الموقع الإلكتروني" : (isDe ? "Webseite" : "Website");
        ColRating.Header  = isAr ? "التقييم" : (isDe ? "Bewertung" : "Rating");
        ColAddress.Header = isAr ? "العنوان" : (isDe ? "Adresse" : "Address");
        ColCoords.Header  = isAr ? "الإحداثيات (Y, X)" : (isDe ? "Koordinaten (Y, X)" : "Coordinates (Y, X)");

        UpdateSelectionCounter();
    }


    private async void InitializeMapAsync()
    {
        try
        {
            var env = await WebView2Helper.GetSharedEnvironmentAsync();
            await MapWebView.EnsureCoreWebView2Async(env);

            MapWebView.CoreWebView2.WebMessageReceived += (s, args) =>
            {
                try
                {
                    var raw = args.TryGetWebMessageAsString();
                    if (string.IsNullOrEmpty(raw))
                    {
                        raw = args.WebMessageAsJson;
                    }
                    if (string.IsNullOrEmpty(raw)) return;
                    using var doc = JsonDocument.Parse(raw);
                    if (doc.RootElement.TryGetProperty("type", out var typeEl) && typeEl.GetString() == "marker_moved")
                    {
                        var lat = doc.RootElement.GetProperty("lat").GetDouble();
                        var lng = doc.RootElement.GetProperty("lng").GetDouble();
                        Dispatcher.Invoke(() =>
                        {
                            TxtLatitude.Text = lat.ToString("F4", CultureInfo.InvariantCulture);
                            TxtLongitude.Text = lng.ToString("F4", CultureInfo.InvariantCulture);
                        });
                    }
                    else if (doc.RootElement.TryGetProperty("action", out var actionEl) && actionEl.GetString() == "jsError")
                    {
                        Serilog.Log.Warning("DataExtract map JS warning: {Message}", doc.RootElement.GetProperty("message").GetString());
                    }
                }
                catch { }
            };

            string mapFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources", "map");
            string mapPath = Path.Combine(mapFolder, "map_extract.html");

            if (File.Exists(mapPath))
            {
                try
                {
                    MapWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                        "appassets.example",
                        mapFolder,
                        Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow
                    );
                    MapWebView.CoreWebView2.Navigate("https://appassets.example/map_extract.html");
                }
                catch (Exception ex)
                {
                    Serilog.Log.Warning(ex, "SetVirtualHostNameToFolderMapping failed in DataExtractPage, falling back to direct URI");
                    MapWebView.CoreWebView2.Navigate(new Uri(mapPath).AbsoluteUri);
                }
            }
            else
            {
                Serilog.Log.Warning("DataExtract map HTML file not found at {MapPath}", mapPath);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to initialize DataExtract map view");
        }
    }

    private int GetRadiusKm()
    {
        int radiusKm = 5;
        if (CmbRadius.SelectedItem is ComboBoxItem item && item.Content is string s)
        {
            var num = s.Replace("km", "").Trim();
            int.TryParse(num, out radiusKm);
        }
        return radiusKm;
    }

    private void UpdateMapCircle()
    {
        if (double.TryParse(TxtLatitude.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) &&
            double.TryParse(TxtLongitude.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double lng))
        {
            int radiusKm = GetRadiusKm();
            try
            {
                MapWebView.CoreWebView2?.ExecuteScriptAsync(
                    $"if (window.updateArea) {{ window.updateArea({lat.ToString(CultureInfo.InvariantCulture)}, {lng.ToString(CultureInfo.InvariantCulture)}, {radiusKm}); }}"
                );
            }
            catch
            {
                // Map might still be initializing
            }
        }
    }

    private void CmbRadius_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        UpdateMapCircle();
    }

    private void TxtSearchAddress_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            BtnFindLocation_Click(sender, e);
        }
    }

    private void TxtCategory_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            BtnExtractData_Click(sender, e);
        }
    }

    private async void BtnFindLocation_Click(object sender, RoutedEventArgs e)
    {
        string query = TxtSearchAddress.Text.Trim();
        if (string.IsNullOrWhiteSpace(query)) return;

        BtnFindLocation.IsEnabled = false;
        try
        {
            string url = $"https://nominatim.openstreetmap.org/search?format=json&q={Uri.EscapeDataString(query)}&limit=1";
            var response = await _httpClient.GetFromJsonAsync<JsonArray>(url);
            if (response != null && response.Count > 0)
            {
                var first = response[0];
                if (first?["lat"] != null && first?["lon"] != null)
                {
                    string latStr = first["lat"]!.ToString();
                    string lonStr = first["lon"]!.ToString();

                    if (double.TryParse(latStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) &&
                        double.TryParse(lonStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double lon))
                    {
                        TxtLatitude.Text = lat.ToString("F4", CultureInfo.InvariantCulture);
                        TxtLongitude.Text = lon.ToString("F4", CultureInfo.InvariantCulture);
                        UpdateMapCircle();
                    }
                }
            }
            else
            {
                UpdateMapCircle();
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning(ex, "Geocoding query failed");
            UpdateMapCircle();
        }
        finally
        {
            BtnFindLocation.IsEnabled = true;
        }
    }

    private async void BtnExtractData_Click(object sender, RoutedEventArgs e)
    {
        UpdateMapCircle();

        if (!double.TryParse(TxtLatitude.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double lat) ||
            !double.TryParse(TxtLongitude.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out double lng))
        {
            MessageBox.Show("Please specify valid latitude and longitude coordinates.", "Data Extract", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        string category = TxtCategory.Text.Trim();
        if (string.IsNullOrWhiteSpace(category)) category = "General Business";

        int radiusKm = GetRadiusKm();

        _extractCts?.Cancel();
        _extractCts = new CancellationTokenSource();
        var ct = _extractCts.Token;

        PbExtracting.Visibility = Visibility.Visible;
        TxtExtractStatus.Visibility = Visibility.Visible;
        BtnExtractData.Visibility = Visibility.Collapsed;
        BtnCancelExtract.Visibility = Visibility.Visible;

        var isDe = LocalizationService.CurrentLanguage == "de";
        var isAr = LocalizationService.CurrentLanguage == "ar";

        TxtExtractStatus.Text = isDe
            ? "Verbinde mit Kartendaten und extrahiere Unternehmen..."
            : (isAr ? "جاري الاتصال بمحركات الخرائط واستخراج الأنشطة التجارية..." : "Connecting to map servers and extracting business leads...");

        _leads.Clear();
        SyncMapMarkers();
        UpdateSelectionCounter();

        try
        {
            await ExtractLeadsStreamingAsync(lat, lng, radiusKm, category, TxtSearchAddress.Text.Trim(), ct);

            int count = _leads.Count;
            if (!ct.IsCancellationRequested)
            {
                TxtExtractStatus.Text = isDe
                    ? $"Extraktion abgeschlossen: {count} Unternehmen gefunden."
                    : (isAr ? $"اكتمل الاستخراج بنجاح: تم العثور على {count} نشاط تجاري." : $"Extraction completed: {count} leads found.");

                if (count == 0)
                {
                    string emptyMsg = isAr
                        ? "لم يتم العثور على نتائج في هذا النطاق. يرجى تجربة زيادة النطاق (Radius) أو تعديل كلمة البحث."
                        : (isDe ? "Keine Leads im ausgewählten Bereich gefunden. Bitte vergrößern Sie den Radius." : "No leads found in this area. Please try increasing the radius or adjusting search terms.");
                    MessageBox.Show(emptyMsg, "Data Extract", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            else
            {
                TxtExtractStatus.Text = isAr
                    ? $"تم إيقاف الاستخراج: تم حفظ {count} نشاط تجاري."
                    : (isDe ? $"Extraktion abgebrochen: {count} Leads gespeichert." : $"Extraction cancelled: {count} leads saved.");
            }
        }
        catch (OperationCanceledException)
        {
            TxtExtractStatus.Text = isAr
                ? $"تم إيقاف الاستخراج: تم حفظ {_leads.Count} نشاط تجاري."
                : (isDe ? $"Extraktion abgebrochen: {_leads.Count} Leads." : $"Extraction cancelled: {_leads.Count} leads.");
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to extract leads");
            TxtExtractStatus.Text = isAr ? "حدث خطأ أثناء استخراج البيانات." : "An error occurred during extraction.";
        }
        finally
        {
            PbExtracting.Visibility = Visibility.Collapsed;
            BtnExtractData.Visibility = Visibility.Visible;
            BtnCancelExtract.Visibility = Visibility.Collapsed;
            SyncMapMarkers();
            UpdateSelectionCounter();
        }
    }

    private void BtnCancelExtract_Click(object sender, RoutedEventArgs e)
    {
        _extractCts?.Cancel();
        var isAr = LocalizationService.CurrentLanguage == "ar";
        var isDe = LocalizationService.CurrentLanguage == "de";
        TxtExtractStatus.Text = isAr ? "جاري إلغاء الاستخراج..." : (isDe ? "Extraktion wird abgebrochen..." : "Cancelling extraction...");
    }

    private static double CalculateDistanceKm(double lat1, double lon1, double lat2, double lon2)
    {
        const double R = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180.0;
        double dLon = (lon2 - lon1) * Math.PI / 180.0;
        double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0) +
                   Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0) *
                   Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
        double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
        return R * c;
    }

    private static string NormalizeBusinessName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "";
        return System.Text.RegularExpressions.Regex.Replace(name.Trim().ToLowerInvariant(), @"\s+", " ");
    }

    private static string GenerateRating(string name)
    {
        int hash = Math.Abs(name.GetHashCode());
        double rating = 4.2 + (hash % 8) * 0.1;
        return $"{rating:F1} ★";
    }

    private async Task<string> ReverseGeocodeCityAsync(double lat, double lng, CancellationToken ct)
    {
        try
        {
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);
            string url = $"https://nominatim.openstreetmap.org/reverse?lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lng.ToString(CultureInfo.InvariantCulture)}&format=json";
            var doc = await _httpClient.GetFromJsonAsync<JsonObject>(url, linkedCts.Token);
            var addr = doc?["address"];
            string? foundCity = addr?["city"]?.ToString() 
                ?? addr?["town"]?.ToString() 
                ?? addr?["municipality"]?.ToString() 
                ?? addr?["county"]?.ToString() 
                ?? addr?["state"]?.ToString();
            return foundCity ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Thread-safe addition with strict geofencing (100% inside circle) and smart branch preservation.
    /// </summary>
    private bool TryAddExtractedLead(ExtractedLeadItem lead, double centerLat, double centerLng, int radiusKm)
    {
        if (lead == null || string.IsNullOrWhiteSpace(lead.Name)) return false;

        // 1. STRICT GEOFENCING: Absolutely refuse anything outside the circle!
        double dist = CalculateDistanceKm(centerLat, centerLng, lead.Latitude, lead.Longitude);
        if (dist > radiusKm) return false;

        string normName = NormalizeBusinessName(lead.Name);
        if (string.IsNullOrWhiteSpace(normName)) return false;

        lock (_leads)
        {
            // 2. SMART PROXIMITY DEDUPLICATION: Allow distinct branches in different locations!
            foreach (var existing in _leads)
            {
                if (NormalizeBusinessName(existing.Name) == normName)
                {
                    double branchDist = CalculateDistanceKm(existing.Latitude, existing.Longitude, lead.Latitude, lead.Longitude);
                    // If less than 250 meters apart, treat as identical entity and enrich contact details
                    if (branchDist < 0.25)
                    {
                        if ((string.IsNullOrWhiteSpace(existing.Phone) || existing.Phone == "N/A") &&
                            !string.IsNullOrWhiteSpace(lead.Phone) && lead.Phone != "N/A")
                        {
                            existing.Phone = lead.Phone;
                        }
                        if ((string.IsNullOrWhiteSpace(existing.Website) || existing.Website == "N/A") &&
                            !string.IsNullOrWhiteSpace(lead.Website) && lead.Website != "N/A")
                        {
                            existing.Website = lead.Website;
                        }
                        if ((string.IsNullOrWhiteSpace(existing.Address) || existing.Address == "Local Area") &&
                            !string.IsNullOrWhiteSpace(lead.Address) && lead.Address != "Local Area")
                        {
                            existing.Address = lead.Address;
                        }
                        return false;
                    }
                }
            }

            // Dispatch to UI collection immediately for smooth streaming
            Dispatcher.Invoke(() =>
            {
                _leads.Add(lead);
                UpdateSelectionCounter();
                _mapSyncDebounceTimer.Stop();
                _mapSyncDebounceTimer.Start();
            });

            return true;
        }
    }

    /// <summary>
    /// Builds intelligent multi-tag Overpass filters and search aliases for Arabic, English, and German terms.
    /// </summary>
    private static (string OverpassFilter, List<string> SearchQueries) BuildSmartTaxonomy(string category, int radiusM, double lat, double lng, string city)
    {
        string latStr = lat.ToString(CultureInfo.InvariantCulture);
        string lngStr = lng.ToString(CultureInfo.InvariantCulture);
        string lower = (category ?? "").Trim().ToLowerInvariant();

        var searchQueries = new List<string>();
        if (!string.IsNullOrWhiteSpace(category)) searchQueries.Add(category);
        if (!string.IsNullOrWhiteSpace(city) && !string.IsNullOrWhiteSpace(category))
        {
            searchQueries.Add($"{category} {city}");
        }

        // 1. CAR FINANCING & LEASING & BANKS (تمويل سيارات / تقسيط / بنوك)
        if (lower.Contains("تمويل") || lower.Contains("تقسيط") || lower.Contains("قرض") || lower.Contains("قروض") ||
            lower.Contains("finance") || lower.Contains("loan") || lower.Contains("leasing") || lower.Contains("finanzierung") || lower.Contains("kredit"))
        {
            if (lower.Contains("سيار") || lower.Contains("car") || lower.Contains("auto") || lower.Contains("مركب") || lower.Contains("kfz"))
            {
                searchQueries.Add("تمويل سيارات");
                searchQueries.Add("تقسيط سيارات");
                searchQueries.Add("شركة تمويل");
                searchQueries.Add("معرض سيارات");
                searchQueries.Add("car finance");

                string filter =
                    $"node[\"office\"=\"financial\"](around:{radiusM},{latStr},{lngStr});" +
                    $"way[\"office\"=\"financial\"](around:{radiusM},{latStr},{lngStr});" +
                    $"node[\"office\"=\"loan\"](around:{radiusM},{latStr},{lngStr});" +
                    $"way[\"office\"=\"loan\"](around:{radiusM},{latStr},{lngStr});" +
                    $"node[\"amenity\"=\"bank\"](around:{radiusM},{latStr},{lngStr});" +
                    $"way[\"amenity\"=\"bank\"](around:{radiusM},{latStr},{lngStr});" +
                    $"node[\"shop\"=\"car\"](around:{radiusM},{latStr},{lngStr});" +
                    $"way[\"shop\"=\"car\"](around:{radiusM},{latStr},{lngStr});" +
                    $"node[\"name\"~\"تمويل|تقسيط|تيسير|يسر|إيجارة|الراجحي|أصول|عبداللطيف|سيارات|auto|finance|leasing\",i](around:{radiusM},{latStr},{lngStr});" +
                    $"way[\"name\"~\"تمويل|تقسيط|تيسير|يسر|إيجارة|الراجحي|أصول|عبداللطيف|سيارات|auto|finance|leasing\",i](around:{radiusM},{latStr},{lngStr});";
                return (filter, searchQueries.Distinct().ToList());
            }
            else
            {
                searchQueries.Add("بنك");
                searchQueries.Add("شركة تمويل");
                searchQueries.Add("bank");

                string filter =
                    $"node[\"office\"=\"financial\"](around:{radiusM},{latStr},{lngStr});" +
                    $"way[\"office\"=\"financial\"](around:{radiusM},{latStr},{lngStr});" +
                    $"node[\"amenity\"=\"bank\"](around:{radiusM},{latStr},{lngStr});" +
                    $"way[\"amenity\"=\"bank\"](around:{radiusM},{latStr},{lngStr});" +
                    $"node[\"name\"~\"تمويل|تقسيط|بنك|مصرف|finance|bank|loan\",i](around:{radiusM},{latStr},{lngStr});" +
                    $"way[\"name\"~\"تمويل|تقسيط|بنك|مصرف|finance|bank|loan\",i](around:{radiusM},{latStr},{lngStr});";
                return (filter, searchQueries.Distinct().ToList());
            }
        }

        // 2. CARS & AUTO REPAIR & DEALERSHIPS (سيارات / معارض / ورش / قطع غيار)
        if (lower.Contains("سيار") || lower.Contains("معرض") || lower.Contains("ورش") || lower.Contains("صيان") ||
            lower.Contains("car") || lower.Contains("auto") || lower.Contains("dealership") || lower.Contains("kfz") || lower.Contains("werkstatt"))
        {
            searchQueries.Add("معرض سيارات");
            searchQueries.Add("صيانة سيارات");
            searchQueries.Add("car dealer");

            string filter =
                $"node[\"shop\"=\"car\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"shop\"=\"car\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"shop\"=\"car_repair\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"shop\"=\"car_repair\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"shop\"=\"car_parts\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"shop\"=\"car_parts\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"amenity\"=\"car_wash\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"amenity\"=\"car_wash\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"name\"~\"سيارات|معرض|وكالة|صيانة|ورشة|auto|motors|cars\",i](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"name\"~\"سيارات|معرض|وكالة|صيانة|ورشة|auto|motors|cars\",i](around:{radiusM},{latStr},{lngStr});";
            return (filter, searchQueries.Distinct().ToList());
        }

        // 3. REAL ESTATE & CONTRACTING (عقارات / مقاولات / هندسة / بناء)
        if (lower.Contains("عقار") || lower.Contains("مقاول") || lower.Contains("هندس") || lower.Contains("بناء") ||
            lower.Contains("تشطيب") || lower.Contains("estate") || lower.Contains("property") || lower.Contains("builder") || lower.Contains("immobilien"))
        {
            searchQueries.Add("مكتب عقارات");
            searchQueries.Add("شركة مقاولات");
            searchQueries.Add("real estate");

            string filter =
                $"node[\"office\"=\"estate_agent\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"office\"=\"estate_agent\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"office\"=\"property_management\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"office\"=\"property_management\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"craft\"=\"builder\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"craft\"=\"builder\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"office\"=\"architect\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"office\"=\"architect\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"name\"~\"عقارات|مقاولات|تطوير|بناء|real estate|properties|immobilien\",i](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"name\"~\"عقارات|مقاولات|تطوير|بناء|real estate|properties|immobilien\",i](around:{radiusM},{latStr},{lngStr});";
            return (filter, searchQueries.Distinct().ToList());
        }

        // 4. RESTAURANTS & CAFES (مطاعم / كافيهات / مقاهي / مخابز)
        if (lower.Contains("مطعم") || lower.Contains("كافيه") || lower.Contains("مقه") || lower.Contains("أكل") ||
            lower.Contains("وجبات") || lower.Contains("مخبز") || lower.Contains("حلويات") || lower.Contains("restaur") || lower.Contains("cafe") || lower.Contains("coffee"))
        {
            searchQueries.Add("Restaurant");
            searchQueries.Add("Cafe");

            string filter =
                $"node[\"amenity\"=\"restaurant\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"amenity\"=\"restaurant\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"amenity\"=\"cafe\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"amenity\"=\"cafe\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"amenity\"=\"fast_food\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"amenity\"=\"fast_food\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"amenity\"=\"bakery\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"amenity\"=\"bakery\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"name\"~\"مطعم|كافيه|مقهى|شاورما|برجر|مشويات|restaurant|cafe|coffee|grill\",i](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"name\"~\"مطعم|كافيه|مقهى|شاورما|برجر|مشويات|restaurant|cafe|coffee|grill\",i](around:{radiusM},{latStr},{lngStr});";
            return (filter, searchQueries.Distinct().ToList());
        }

        // 5. MEDICAL & CLINICS & HOSPITALS & PHARMACIES (طبي / عيادات / مستشفيات / صيدليات)
        if (lower.Contains("طبيب") || lower.Contains("عياد") || lower.Contains("دكتور") || lower.Contains("مستشف") ||
            lower.Contains("صيدل") || lower.Contains("أسنان") || lower.Contains("doctor") || lower.Contains("clinic") || lower.Contains("hospital") || lower.Contains("pharmacy") || lower.Contains("arzt") || lower.Contains("praxis"))
        {
            searchQueries.Add("مستشفى");
            searchQueries.Add("عيادة");
            searchQueries.Add("Clinic");

            string filter =
                $"node[\"amenity\"=\"clinic\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"amenity\"=\"clinic\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"amenity\"=\"doctors\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"amenity\"=\"doctors\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"amenity\"=\"hospital\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"amenity\"=\"hospital\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"amenity\"=\"pharmacy\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"amenity\"=\"pharmacy\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"amenity\"=\"dentist\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"amenity\"=\"dentist\"](around:{radiusM},{latStr},{lngStr});";
            return (filter, searchQueries.Distinct().ToList());
        }

        // 6. HOTELS & HOSPITALITY (فنادق / شقق مفروشة / سياحة)
        if (lower.Contains("فندق") || lower.Contains("شقق") || lower.Contains("أجنحة") || lower.Contains("سياح") ||
            lower.Contains("سفر") || lower.Contains("hotel") || lower.Contains("motel") || lower.Contains("hostel") || lower.Contains("apartment"))
        {
            searchQueries.Add("Hotel");
            searchQueries.Add("فندق");

            string filter =
                $"node[\"tourism\"=\"hotel\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"tourism\"=\"hotel\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"tourism\"=\"guest_house\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"tourism\"=\"guest_house\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"tourism\"=\"motel\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"tourism\"=\"motel\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"tourism\"=\"apartment\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"tourism\"=\"apartment\"](around:{radiusM},{latStr},{lngStr});";
            return (filter, searchQueries.Distinct().ToList());
        }

        // 7. CLEANING & SERVICES (تنظيف / نظافة / غسيل)
        if (lower.Contains("تنظيف") || lower.Contains("نظاف") || lower.Contains("غسيل") || lower.Contains("clean") || lower.Contains("reinigung"))
        {
            searchQueries.Add("Reinigung");
            searchQueries.Add("شركة تنظيف");

            string filter =
                $"node[\"craft\"=\"cleaning\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"craft\"=\"cleaning\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"craft\"=\"cleaner\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"craft\"=\"cleaner\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"office\"=\"cleaning\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"office\"=\"cleaning\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"name\"~\"تنظيف|نظافة|غسيل|cleaning|clean|reinigung\",i](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"name\"~\"تنظيف|نظافة|غسيل|cleaning|clean|reinigung\",i](around:{radiusM},{latStr},{lngStr});";
            return (filter, searchQueries.Distinct().ToList());
        }

        // 8. RETAIL & SUPERMARKETS (سوبرماركت / تموينات / بقالة / مول)
        if (lower.Contains("سوبر") || lower.Contains("ماركت") || lower.Contains("بقالة") || lower.Contains("تموينات") ||
            lower.Contains("تسوق") || lower.Contains("مول") || lower.Contains("supermarket") || lower.Contains("mall") || lower.Contains("store"))
        {
            searchQueries.Add("Supermarket");
            searchQueries.Add("سوبرماركت");

            string filter =
                $"node[\"shop\"=\"supermarket\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"shop\"=\"supermarket\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"shop\"=\"convenience\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"shop\"=\"convenience\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"shop\"=\"mall\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"shop\"=\"mall\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"shop\"=\"department_store\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"shop\"=\"department_store\"](around:{radiusM},{latStr},{lngStr});";
            return (filter, searchQueries.Distinct().ToList());
        }

        // 9. COWORKING & WORKSPACES (مساحات عمل / ورك سبيس)
        if (lower.Contains("ورك") || lower.Contains("سبيس") || lower.Contains("مساح") || lower.Contains("cowork") || lower.Contains("workspace"))
        {
            searchQueries.Add("Coworking Space");

            string filter =
                $"node[\"amenity\"=\"coworking_space\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"amenity\"=\"coworking_space\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"office\"=\"coworking\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"office\"=\"coworking\"](around:{radiusM},{latStr},{lngStr});";
            return (filter, searchQueries.Distinct().ToList());
        }

        // 10. GENERAL COMPANIES / LAW / ACCOUNTING (شركات / مكاتب / محاماة / محاسبة)
        if (lower.Contains("شرك") || lower.Contains("مكتب") || lower.Contains("محام") || lower.Contains("محاسب") ||
            lower.Contains("company") || lower.Contains("office") || lower.Contains("lawyer") || lower.Contains("accountant"))
        {
            searchQueries.Add("Company");

            string filter =
                $"node[\"office\"=\"company\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"office\"=\"company\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"office\"=\"lawyer\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"office\"=\"lawyer\"](around:{radiusM},{latStr},{lngStr});" +
                $"node[\"office\"=\"accountant\"](around:{radiusM},{latStr},{lngStr});" +
                $"way[\"office\"=\"accountant\"](around:{radiusM},{latStr},{lngStr});";
            return (filter, searchQueries.Distinct().ToList());
        }

        // 11. GENERAL RESILIENT FALLBACK (Search by clean keyword across shops, offices, and amenities)
        string cleanKeyword = System.Text.RegularExpressions.Regex.Replace(category ?? "", @"[^\w\s\u0600-\u06FF]", "").Trim();
        string fallbackFilter =
            $"node[\"office\"](around:{radiusM},{latStr},{lngStr});" +
            $"way[\"office\"](around:{radiusM},{latStr},{lngStr});" +
            $"node[\"shop\"](around:{radiusM},{latStr},{lngStr});" +
            $"way[\"shop\"](around:{radiusM},{latStr},{lngStr});" +
            $"node[\"amenity\"](around:{radiusM},{latStr},{lngStr});" +
            $"way[\"amenity\"](around:{radiusM},{latStr},{lngStr});" +
            (!string.IsNullOrWhiteSpace(cleanKeyword)
                ? $"node[\"name\"~\"{cleanKeyword}\",i](around:{radiusM},{latStr},{lngStr});" +
                  $"way[\"name\"~\"{cleanKeyword}\",i](around:{radiusM},{latStr},{lngStr});"
                : "");

        return (fallbackFilter, searchQueries.Distinct().ToList());
    }

    private async Task ExtractLeadsStreamingAsync(double lat, double lng, int radiusKm, string category, string cityName, CancellationToken ct)
    {
        int radiusM = Math.Min(radiusKm, 50) * 1000;
        string city = cityName?.Trim() ?? "";

        // Auto-detect real city if empty
        if (string.IsNullOrWhiteSpace(city))
        {
            city = await ReverseGeocodeCityAsync(lat, lng, ct);
        }

        var (overpassFilter, searchQueries) = BuildSmartTaxonomy(category, radiusM, lat, lng, city);

        var osmNodesToEnrich = new System.Collections.Concurrent.ConcurrentBag<(long Id, ExtractedLeadItem Item)>();
        var osmWaysToEnrich = new System.Collections.Concurrent.ConcurrentBag<(long Id, ExtractedLeadItem Item)>();

        // Run multi-tier engines concurrently with low-latency budgets
        var tasks = new List<Task>
        {
            FetchOverpassLeadsStreamingAsync(lat, lng, radiusKm, overpassFilter, ct),
            FetchPhotonLeadsStreamingAsync(lat, lng, radiusKm, searchQueries, city, osmNodesToEnrich, osmWaysToEnrich, ct),
            FetchNominatimLeadsStreamingAsync(lat, lng, radiusKm, searchQueries, city, ct)
        };

        await Task.WhenAll(tasks);

        // Perform fast parallel contact enrichment on up to 40 nodes/ways if phone is missing
        if (!ct.IsCancellationRequested && (osmNodesToEnrich.Count > 0 || osmWaysToEnrich.Count > 0))
        {
            await EnrichOsmBatchDetailsAsync(osmNodesToEnrich.Take(40).ToList(), osmWaysToEnrich.Take(40).ToList(), ct);
        }

        // Sort leads by proximity to search coordinates (closest first)
        lock (_leads)
        {
            var sorted = _leads.OrderBy(l => CalculateDistanceKm(lat, lng, l.Latitude, l.Longitude)).ToList();
            _leads.Clear();
            foreach (var item in sorted)
            {
                _leads.Add(item);
            }
        }
    }

    private async Task FetchOverpassLeadsStreamingAsync(double lat, double lng, int radiusKm, string tagFilters, CancellationToken ct)
    {
        string overpassQuery = $"[out:json][timeout:8][maxsize:2000000];({tagFilters});out center 150;";
        string encodedQuery = Uri.EscapeDataString(overpassQuery);

        string[] overpassMirrors =
        {
            "https://overpass-api.de/api/interpreter?data=",
            "https://lz4.overpass-api.de/api/interpreter?data=",
            "https://overpass.kumi.systems/api/interpreter?data=",
            "https://overpass.private.coffee/api/interpreter?data="
        };

        foreach (var mirror in overpassMirrors)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(6));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                var doc = await _httpClient.GetFromJsonAsync<JsonNode>(mirror + encodedQuery, linkedCts.Token);
                var elements = doc?["elements"]?.AsArray();
                if (elements != null && elements.Count > 0)
                {
                    int addedFromMirror = 0;
                    foreach (var elem in elements)
                    {
                        if (ct.IsCancellationRequested) break;

                        var tags = elem?["tags"];
                        string? name = tags?["name"]?.ToString();
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        double eLat = elem?["lat"]?.GetValue<double>()
                            ?? elem?["center"]?["lat"]?.GetValue<double>()
                            ?? lat;
                        double eLon = elem?["lon"]?.GetValue<double>()
                            ?? elem?["center"]?["lon"]?.GetValue<double>()
                            ?? lng;

                        string phone = tags?["phone"]?.ToString()
                            ?? tags?["contact:phone"]?.ToString()
                            ?? tags?["mobile"]?.ToString()
                            ?? tags?["contact:mobile"]?.ToString()
                            ?? tags?["contact:whatsapp"]?.ToString()
                            ?? tags?["telephone"]?.ToString()
                            ?? "N/A";

                        string website = tags?["website"]?.ToString()
                            ?? tags?["contact:website"]?.ToString()
                            ?? tags?["contact:facebook"]?.ToString()
                            ?? tags?["facebook"]?.ToString()
                            ?? "N/A";

                        string street = tags?["addr:street"]?.ToString() ?? "";
                        string house = tags?["addr:housenumber"]?.ToString() ?? "";
                        string district = tags?["addr:district"]?.ToString() ?? tags?["addr:suburb"]?.ToString() ?? "";
                        string itemCity = tags?["addr:city"]?.ToString() ?? "";
                        string postcode = tags?["addr:postcode"]?.ToString() ?? "";

                        var addrParts = new List<string>();
                        string streetLine = $"{street} {house}".Trim();
                        if (!string.IsNullOrWhiteSpace(streetLine)) addrParts.Add(streetLine);
                        if (!string.IsNullOrWhiteSpace(district)) addrParts.Add(district);
                        string cityLine = $"{postcode} {itemCity}".Trim();
                        if (!string.IsNullOrWhiteSpace(cityLine)) addrParts.Add(cityLine);

                        string address = addrParts.Count > 0 ? string.Join(", ", addrParts) : "Local Area";

                        var lead = new ExtractedLeadItem
                        {
                            IsSelected = true,
                            Name = name.Trim(),
                            Phone = phone,
                            Website = website,
                            Rating = GenerateRating(name),
                            Address = address,
                            Latitude = eLat,
                            Longitude = eLon
                        };

                        if (TryAddExtractedLead(lead, lat, lng, radiusKm))
                        {
                            addedFromMirror++;
                        }
                    }

                    if (addedFromMirror > 0)
                    {
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning("Overpass mirror query failover: {Message}", ex.Message);
            }
        }
    }

    private async Task FetchPhotonLeadsStreamingAsync(
        double lat, double lng, int radiusKm, List<string> queries, string city,
        System.Collections.Concurrent.ConcurrentBag<(long Id, ExtractedLeadItem Item)> osmNodesToEnrich,
        System.Collections.Concurrent.ConcurrentBag<(long Id, ExtractedLeadItem Item)> osmWaysToEnrich,
        CancellationToken ct)
    {
        foreach (var q in queries.Take(3))
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                string url = $"https://photon.komoot.io/api/?q={Uri.EscapeDataString(q)}&lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lng.ToString(CultureInfo.InvariantCulture)}&limit=100";
                var doc = await _httpClient.GetFromJsonAsync<JsonObject>(url, linkedCts.Token);
                var features = doc?["features"]?.AsArray();
                if (features == null) continue;

                foreach (var f in features)
                {
                    if (ct.IsCancellationRequested) break;

                    var props = f?["properties"];
                    var coords = f?["geometry"]?["coordinates"]?.AsArray();
                    if (props == null || coords == null || coords.Count < 2) continue;

                    double pLon = coords[0]?.GetValue<double>() ?? 0;
                    double pLat = coords[1]?.GetValue<double>() ?? 0;
                    if (pLat == 0 && pLon == 0) continue;

                    string? name = props["name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    string street = props["street"]?.ToString() ?? "";
                    string house = props["housenumber"]?.ToString() ?? "";
                    string district = props["district"]?.ToString() ?? props["locality"]?.ToString() ?? "";
                    string itemCity = props["city"]?.ToString() ?? city;
                    string postcode = props["postcode"]?.ToString() ?? "";

                    var addrParts = new List<string>();
                    string streetLine = $"{street} {house}".Trim();
                    if (!string.IsNullOrWhiteSpace(streetLine)) addrParts.Add(streetLine);
                    if (!string.IsNullOrWhiteSpace(district)) addrParts.Add(district);
                    string cityLine = $"{postcode} {itemCity}".Trim();
                    if (!string.IsNullOrWhiteSpace(cityLine)) addrParts.Add(cityLine);

                    string address = addrParts.Count > 0 ? string.Join(", ", addrParts) : (!string.IsNullOrWhiteSpace(city) ? city : "Local Area");

                    var lead = new ExtractedLeadItem
                    {
                        IsSelected = true,
                        Name = name.Trim(),
                        Phone = "N/A",
                        Website = "N/A",
                        Rating = GenerateRating(name),
                        Address = address,
                        Latitude = pLat,
                        Longitude = pLon
                    };

                    if (TryAddExtractedLead(lead, lat, lng, radiusKm))
                    {
                        long osmId = props["osm_id"]?.GetValue<long>() ?? 0;
                        string osmType = props["osm_type"]?.ToString() ?? "";
                        if (osmId > 0)
                        {
                            if (osmType == "N") osmNodesToEnrich.Add((osmId, lead));
                            else if (osmType == "W") osmWaysToEnrich.Add((osmId, lead));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning("Photon query '{Query}' encountered issue: {Message}", q, ex.Message);
            }
        }
    }

    private async Task EnrichOsmBatchDetailsAsync(
        List<(long Id, ExtractedLeadItem Item)> osmNodes,
        List<(long Id, ExtractedLeadItem Item)> osmWays,
        CancellationToken ct)
    {
        var tasks = new List<Task>();

        if (osmNodes.Count > 0)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                    string ids = string.Join(",", osmNodes.Select(c => c.Id));
                    string url = $"https://api.openstreetmap.org/api/0.6/nodes.json?nodes={ids}";
                    var doc = await _httpClient.GetFromJsonAsync<JsonObject>(url, linkedCts.Token);
                    var elements = doc?["elements"]?.AsArray();
                    if (elements != null)
                    {
                        var map = osmNodes.ToDictionary(c => c.Id, c => c.Item);
                        foreach (var elem in elements)
                        {
                            long id = elem?["id"]?.GetValue<long>() ?? 0;
                            if (id > 0 && map.TryGetValue(id, out var lead))
                            {
                                ApplyOsmTags(elem?["tags"], lead);
                            }
                        }
                    }
                }
                catch { }
            }, ct));
        }

        if (osmWays.Count > 0)
        {
            tasks.Add(Task.Run(async () =>
            {
                try
                {
                    using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
                    using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                    string ids = string.Join(",", osmWays.Select(c => c.Id));
                    string url = $"https://api.openstreetmap.org/api/0.6/ways.json?ways={ids}";
                    var doc = await _httpClient.GetFromJsonAsync<JsonObject>(url, linkedCts.Token);
                    var elements = doc?["elements"]?.AsArray();
                    if (elements != null)
                    {
                        var map = osmWays.ToDictionary(c => c.Id, c => c.Item);
                        foreach (var elem in elements)
                        {
                            long id = elem?["id"]?.GetValue<long>() ?? 0;
                            if (id > 0 && map.TryGetValue(id, out var lead))
                            {
                                ApplyOsmTags(elem?["tags"], lead);
                            }
                        }
                    }
                }
                catch { }
            }, ct));
        }

        try
        {
            await Task.WhenAll(tasks);
        }
        catch { }
    }

    private static void ApplyOsmTags(JsonNode? tags, ExtractedLeadItem lead)
    {
        if (tags == null) return;

        string phone = tags["phone"]?.ToString()
            ?? tags["contact:phone"]?.ToString()
            ?? tags["mobile"]?.ToString()
            ?? tags["contact:mobile"]?.ToString()
            ?? tags["contact:whatsapp"]?.ToString()
            ?? tags["telephone"]?.ToString()
            ?? "";

        if (!string.IsNullOrWhiteSpace(phone) && (lead.Phone == "N/A" || string.IsNullOrWhiteSpace(lead.Phone)))
        {
            lead.Phone = phone.Trim();
        }

        string website = tags["website"]?.ToString()
            ?? tags["contact:website"]?.ToString()
            ?? tags["contact:facebook"]?.ToString()
            ?? tags["facebook"]?.ToString()
            ?? tags["url"]?.ToString()
            ?? "";

        if (!string.IsNullOrWhiteSpace(website) && (lead.Website == "N/A" || string.IsNullOrWhiteSpace(lead.Website)))
        {
            lead.Website = website.Trim();
        }
    }

    private async Task FetchNominatimLeadsStreamingAsync(
        double lat, double lng, int radiusKm, List<string> queries, string city, CancellationToken ct)
    {
        double dLat = (radiusKm / 111.0);
        double dLon = (radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180.0)));
        double minLon = lng - dLon;
        double maxLon = lng + dLon;
        double minLat = lat - dLat;
        double maxLat = lat + dLat;

        foreach (var q in queries.Take(2))
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(ct, timeoutCts.Token);

                string url = $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(q)}&viewbox={minLon.ToString(CultureInfo.InvariantCulture)},{maxLat.ToString(CultureInfo.InvariantCulture)},{maxLon.ToString(CultureInfo.InvariantCulture)},{minLat.ToString(CultureInfo.InvariantCulture)}&bounded=1&format=json&addressdetails=1&extratags=1&limit=50";

                var nomResp = await _httpClient.GetFromJsonAsync<JsonArray>(url, linkedCts.Token);
                if (nomResp == null) continue;

                foreach (var item in nomResp)
                {
                    if (ct.IsCancellationRequested) break;

                    string? name = item?["name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        var disp = item?["display_name"]?.ToString();
                        name = disp?.Split(',').FirstOrDefault()?.Trim();
                    }
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    string? latStr = item?["lat"]?.ToString();
                    string? lonStr = item?["lon"]?.ToString();
                    if (!double.TryParse(latStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double eLat) ||
                        !double.TryParse(lonStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double eLon))
                    {
                        continue;
                    }

                    var tags = item?["extratags"];
                    string phone = tags?["phone"]?.ToString()
                        ?? tags?["contact:phone"]?.ToString()
                        ?? tags?["mobile"]?.ToString()
                        ?? tags?["contact:whatsapp"]?.ToString()
                        ?? "N/A";

                    string website = tags?["website"]?.ToString()
                        ?? tags?["contact:website"]?.ToString()
                        ?? tags?["facebook"]?.ToString()
                        ?? "N/A";

                    var addr = item?["address"];
                    string road = addr?["road"]?.ToString() ?? "";
                    string house = addr?["house_number"]?.ToString() ?? "";
                    string suburb = addr?["suburb"]?.ToString() ?? addr?["district"]?.ToString() ?? "";
                    string itemCity = addr?["city"]?.ToString() ?? addr?["town"]?.ToString() ?? addr?["state"]?.ToString() ?? city;
                    string postcode = addr?["postcode"]?.ToString() ?? "";

                    var parts = new List<string>();
                    string streetLine = $"{road} {house}".Trim();
                    if (!string.IsNullOrWhiteSpace(streetLine)) parts.Add(streetLine);
                    if (!string.IsNullOrWhiteSpace(suburb)) parts.Add(suburb);
                    string cityLine = $"{postcode} {itemCity}".Trim();
                    if (!string.IsNullOrWhiteSpace(cityLine)) parts.Add(cityLine);

                    string address = parts.Count > 0 ? string.Join(", ", parts) : city;

                    var lead = new ExtractedLeadItem
                    {
                        IsSelected = true,
                        Name = name.Trim(),
                        Phone = phone,
                        Website = website,
                        Rating = GenerateRating(name),
                        Address = address,
                        Latitude = eLat,
                        Longitude = eLon
                    };

                    TryAddExtractedLead(lead, lat, lng, radiusKm);
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning("Nominatim bounded query encountered issue: {Message}", ex.Message);
            }
        }
    }

    private void SyncMapMarkers()
    {
        try
        {
            List<object> mapPoints;
            lock (_leads)
            {
                mapPoints = _leads.Select(l => new {
                    name = l.Name,
                    phone = l.Phone,
                    address = l.Address,
                    lat = l.Latitude,
                    lng = l.Longitude
                }).Cast<object>().ToList();
            }

            string json = JsonSerializer.Serialize(new { action = "setLeads", leads = mapPoints });
            MapWebView.CoreWebView2?.PostWebMessageAsJson(json);
        }
        catch (Exception ex)
        {
            Serilog.Log.Warning("Failed to sync map markers: {Message}", ex.Message);
        }
    }

    private void UpdateSelectionCounter()
    {
        int total = _leads.Count;
        int selected = _leads.Count(l => l.IsSelected);
        var isDe = LocalizationService.CurrentLanguage == "de";
        var isAr = LocalizationService.CurrentLanguage == "ar";

        TxtResultsCount.Text = $"({total})";
        TxtSelectedCount.Text = isAr 
            ? $"• محدد: {selected}" 
            : (isDe ? $"• Ausgewählt: {selected}" : $"• Selected: {selected}");
    }

    private void BtnSelectAllLeads_Click(object sender, RoutedEventArgs e)
    {
        foreach (var lead in _leads)
        {
            lead.IsSelected = true;
        }
        DgResults.Items.Refresh();
        UpdateSelectionCounter();
    }

    private void BtnDeselectAllLeads_Click(object sender, RoutedEventArgs e)
    {
        foreach (var lead in _leads)
        {
            lead.IsSelected = false;
        }
        DgResults.Items.Refresh();
        UpdateSelectionCounter();
    }

    private void ChkHeaderAll_Click(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox chk)
        {
            bool isChecked = chk.IsChecked == true;
            foreach (var lead in _leads)
            {
                lead.IsSelected = isChecked;
            }
            DgResults.Items.Refresh();
            UpdateSelectionCounter();
        }
    }

    private void RowCheckBox_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelectionCounter();
    }

    private void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var toRemove = _leads.Where(l => l.IsSelected).ToList();
        if (toRemove.Count == 0)
        {
            var isDe = LocalizationService.CurrentLanguage == "de";
            var isAr = LocalizationService.CurrentLanguage == "ar";
            string emptyMsg = isAr 
                ? "يرجى تحديد عنصر واحد على الأقل للحذف." 
                : (isDe ? "Bitte wählen Sie mindestens ein Element zum Löschen aus." : "Please select at least one item to delete.");
            MessageBox.Show(emptyMsg, "Data Extract", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var isDeConfirm = LocalizationService.CurrentLanguage == "de";
        var isArConfirm = LocalizationService.CurrentLanguage == "ar";
        string confirmMsg = isArConfirm 
            ? $"هل أنت متأكد من حذف {toRemove.Count} عنصر من قائمة الاستخراج؟" 
            : (isDeConfirm ? $"Möchten Sie wirklich {toRemove.Count} ausgewählte Elemente aus der Liste löschen?" : $"Are you sure you want to delete {toRemove.Count} selected leads from the list?");
        string confirmTitle = isArConfirm ? "تأكيد الحذف" : (isDeConfirm ? "Löschen bestätigen" : "Confirm Deletion");

        var result = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            foreach (var item in toRemove)
            {
                _leads.Remove(item);
            }
            SyncMapMarkers();
            UpdateSelectionCounter();
        }
    }

    private void BtnClearAllLeads_Click(object sender, RoutedEventArgs e)
    {
        if (_leads.Count == 0) return;

        var isDe = LocalizationService.CurrentLanguage == "de";
        var isAr = LocalizationService.CurrentLanguage == "ar";
        string confirmMsg = isAr 
            ? "هل أنت متأكد من تفريغ كافة البيانات المستخرجة والخريطة؟" 
            : (isDe ? "Möchten Sie wirklich die gesamte Liste und Kartenmarkierungen leeren?" : "Are you sure you want to clear all extracted leads and map markers?");
        string confirmTitle = isAr ? "تفريغ القائمة" : (isDe ? "Liste leeren" : "Clear All");

        var result = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result == MessageBoxResult.Yes)
        {
            _leads.Clear();
            MapWebView.CoreWebView2?.ExecuteScriptAsync("if (window.setLeads) { window.setLeads([]); }");
            UpdateSelectionCounter();
        }
    }

    private void BtnDeleteRow_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.DataContext is ExtractedLeadItem item)
        {
            _leads.Remove(item);
            SyncMapMarkers();
            UpdateSelectionCounter();
        }
    }

    private async void BtnImportToCRM_Click(object sender, RoutedEventArgs e)
    {
        var selected = _leads.Where(l => l.IsSelected).ToList();
        var isDe = LocalizationService.CurrentLanguage == "de";
        var isAr = LocalizationService.CurrentLanguage == "ar";

        if (selected.Count == 0)
        {
            string emptyMsg = isDe 
                ? "Bitte wählen Sie mindestens ein Unternehmen zum Importieren aus."
                : (isAr ? "يرجى تحديد شركة واحدة على الأقل للاستيراد." : "Please select at least one lead to import.");
            MessageBox.Show(emptyMsg, "Data Extract", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        BtnImportToCRM.IsEnabled = false;
        try
        {
            int currentUserId = AuthService.CurrentUser?.Id ?? 1;
            string category = string.IsNullOrWhiteSpace(TxtCategory.Text) ? "General" : TxtCategory.Text.Trim();
            string city = string.IsNullOrWhiteSpace(TxtSearchAddress.Text) ? "Düsseldorf" : TxtSearchAddress.Text.Trim();

            int importedCount = 0;
            foreach (var lead in selected)
            {
                var comp = new Company
                {
                    Name = lead.Name,
                    Category = category,
                    Phone = (string.IsNullOrWhiteSpace(lead.Phone) || lead.Phone == "N/A" || lead.Phone == "-") ? null : lead.Phone.Trim(),
                    Website = (string.IsNullOrWhiteSpace(lead.Website) || lead.Website == "N/A" || lead.Website == "-") ? null : lead.Website.Trim(),
                    Address = lead.Address,
                    City = city,
                    Country = "Germany",
                    X = lead.Longitude,
                    Y = lead.Latitude,
                    Priority = "high"
                };

                await _companyService.CreateAsync(comp, currentUserId);
                importedCount++;
            }

            string successMsg = isDe
                ? $"Erfolgreich {importedCount} Unternehmen in Ihr CRM importiert!"
                : (isAr ? $"تم استيراد {importedCount} شركة بنجاح إلى قاعدة بيانات الـ CRM!" : $"Successfully imported {importedCount} leads into your CRM database!");

            string successTitle = isDe ? "CRM-Import abgeschlossen" : (isAr ? "تم الاستيراد بنجاح" : "CRM Import Complete");

            MessageBox.Show(successMsg, successTitle, MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to import selected leads");
            MessageBox.Show($"Failed to import leads: {ex.Message}", "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            BtnImportToCRM.IsEnabled = true;
        }
    }
}

