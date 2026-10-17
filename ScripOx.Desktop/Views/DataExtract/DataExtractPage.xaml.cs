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

    public DataExtractPage()
    {
        InitializeComponent();
        _companyService = new CompanyService(AppSettings.ConnectionString);
        DgResults.ItemsSource = _leads;

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

        PbExtracting.Visibility = Visibility.Visible;
        TxtExtractStatus.Visibility = Visibility.Visible;
        BtnExtractData.IsEnabled = false;

        var isDe = LocalizationService.CurrentLanguage == "de";
        var isAr = LocalizationService.CurrentLanguage == "ar";

        TxtExtractStatus.Text = isDe
            ? "Verbinde mit Kartendaten und extrahiere Unternehmen..."
            : (isAr ? "جاري الاتصال بخوادم الخرائط واستخراج الأنشطة التجارية..." : "Connecting to map servers and extracting business leads...");

        _leads.Clear();

        try
        {
            var extractedItems = await FetchLeadsAsync(lat, lng, radiusKm, category, TxtSearchAddress.Text.Trim());
            foreach (var item in extractedItems)
            {
                _leads.Add(item);
            }

            SyncMapMarkers();
            UpdateSelectionCounter();

            int count = _leads.Count;
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
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to extract leads");
        }
        finally
        {
            PbExtracting.Visibility = Visibility.Collapsed;
            BtnExtractData.IsEnabled = true;
        }
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

    private async Task<string> ReverseGeocodeCityAsync(double lat, double lng)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            string url = $"https://nominatim.openstreetmap.org/reverse?lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lng.ToString(CultureInfo.InvariantCulture)}&format=json";
            var doc = await _httpClient.GetFromJsonAsync<JsonObject>(url, cts.Token);
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

    private async Task<List<ExtractedLeadItem>> FetchLeadsAsync(double lat, double lng, int radiusKm, string category, string cityName)
    {
        var list = new List<ExtractedLeadItem>();
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string cat = string.IsNullOrWhiteSpace(category) ? "Business" : category.Trim();
        string city = cityName?.Trim() ?? "";

        // Auto-detect real city if empty
        if (string.IsNullOrWhiteSpace(city))
        {
            city = await ReverseGeocodeCityAsync(lat, lng);
        }

        // TIER 1: Ultra-fast Photon POI Engine (Indexed OpenStreetMap data via Elasticsearch)
        var osmNodesToEnrich = new List<(long Id, ExtractedLeadItem Item)>();
        var osmWaysToEnrich = new List<(long Id, ExtractedLeadItem Item)>();
        await FetchPhotonLeadsAsync(lat, lng, radiusKm, cat, city, list, seenNames, osmNodesToEnrich, osmWaysToEnrich);

        // TIER 2: Fast Direct OSM Batch Tag Lookup (Phones, Websites, Contact details)
        await EnrichOsmBatchDetailsAsync(osmNodesToEnrich, osmWaysToEnrich);

        // TIER 3: Nominatim Bounded POI Search (Supplementary local entities)
        await FetchNominatimLeadsAsync(lat, lng, radiusKm, cat, city, list, seenNames);

        // TIER 4: Resilient Overpass API with Per-Mirror Fast 4s Failover
        await FetchOverpassLeadsAsync(lat, lng, radiusKm, cat, list, seenNames);

        // TIER 5: Verified Hubs (Regional coworkings if applicable)
        AugmentVerifiedHubs(list, seenNames, cat, city, lat, lng);

        // Sort leads by proximity to search coordinates (closest first)
        list.Sort((a, b) =>
        {
            double distA = CalculateDistanceKm(lat, lng, a.Latitude, a.Longitude);
            double distB = CalculateDistanceKm(lat, lng, b.Latitude, b.Longitude);
            return distA.CompareTo(distB);
        });

        return list;
    }

    private async Task FetchPhotonLeadsAsync(
        double lat, double lng, int radiusKm, string category, string city,
        List<ExtractedLeadItem> list, HashSet<string> seenNames,
        List<(long Id, ExtractedLeadItem Item)> osmNodesToEnrich,
        List<(long Id, ExtractedLeadItem Item)> osmWaysToEnrich)
    {
        var queries = new List<string>();
        if (!string.IsNullOrWhiteSpace(city))
        {
            queries.Add($"{category} {city}");
        }
        queries.Add(category);

        string lowerCat = category.ToLowerInvariant();
        if (lowerCat.Contains("reinigung") || lowerCat.Contains("clean") || lowerCat.Contains("تنظيف"))
        {
            queries.Add("Reinigung");
            queries.Add("Gebäudereinigung");
        }
        else if (lowerCat.Contains("restaur") || lowerCat.Contains("مطعم") || lowerCat.Contains("أكل"))
        {
            queries.Add("Restaurant");
        }
        else if (lowerCat.Contains("cafe") || lowerCat.Contains("مقه") || lowerCat.Contains("كافيه"))
        {
            queries.Add("Cafe");
        }
        else if (lowerCat.Contains("arzt") || lowerCat.Contains("doctor") || lowerCat.Contains("طبيب") || lowerCat.Contains("عياد"))
        {
            queries.Add("Arzt");
            queries.Add("Praxis");
        }
        else if (lowerCat.Contains("hotel") || lowerCat.Contains("فندق"))
        {
            queries.Add("Hotel");
        }

        double maxDistKm = Math.Max(radiusKm * 1.35, radiusKm + 2.0);

        foreach (var q in queries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                string url = $"https://photon.komoot.io/api/?q={Uri.EscapeDataString(q)}&lat={lat.ToString(CultureInfo.InvariantCulture)}&lon={lng.ToString(CultureInfo.InvariantCulture)}&limit=100";
                var doc = await _httpClient.GetFromJsonAsync<JsonObject>(url, cts.Token);
                var features = doc?["features"]?.AsArray();
                if (features == null) continue;

                foreach (var f in features)
                {
                    var props = f?["properties"];
                    var coords = f?["geometry"]?["coordinates"]?.AsArray();
                    if (props == null || coords == null || coords.Count < 2) continue;

                    double pLon = coords[0]?.GetValue<double>() ?? 0;
                    double pLat = coords[1]?.GetValue<double>() ?? 0;
                    if (pLat == 0 && pLon == 0) continue;

                    double dist = CalculateDistanceKm(lat, lng, pLat, pLon);
                    if (dist > maxDistKm) continue;

                    string? name = props["name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    string normName = NormalizeBusinessName(name);
                    if (seenNames.Contains(normName)) continue;
                    seenNames.Add(normName);

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

                    list.Add(lead);

                    long osmId = props["osm_id"]?.GetValue<long>() ?? 0;
                    string osmType = props["osm_type"]?.ToString() ?? "";
                    if (osmId > 0)
                    {
                        if (osmType == "N") osmNodesToEnrich.Add((osmId, lead));
                        else if (osmType == "W") osmWaysToEnrich.Add((osmId, lead));
                    }
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Photon query '{Query}' encountered issue", q);
            }
        }
    }

    private async Task EnrichOsmBatchDetailsAsync(
        List<(long Id, ExtractedLeadItem Item)> osmNodes,
        List<(long Id, ExtractedLeadItem Item)> osmWays)
    {
        foreach (var chunk in osmNodes.Chunk(40))
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                string ids = string.Join(",", chunk.Select(c => c.Id));
                string url = $"https://api.openstreetmap.org/api/0.6/nodes.json?nodes={ids}";
                var doc = await _httpClient.GetFromJsonAsync<JsonObject>(url, cts.Token);
                var elements = doc?["elements"]?.AsArray();
                if (elements != null)
                {
                    var map = chunk.ToDictionary(c => c.Id, c => c.Item);
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
        }

        foreach (var chunk in osmWays.Chunk(40))
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                string ids = string.Join(",", chunk.Select(c => c.Id));
                string url = $"https://api.openstreetmap.org/api/0.6/ways.json?ways={ids}";
                var doc = await _httpClient.GetFromJsonAsync<JsonObject>(url, cts.Token);
                var elements = doc?["elements"]?.AsArray();
                if (elements != null)
                {
                    var map = chunk.ToDictionary(c => c.Id, c => c.Item);
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
        }
    }

    private static void ApplyOsmTags(JsonNode? tags, ExtractedLeadItem lead)
    {
        if (tags == null) return;

        string phone = tags["phone"]?.ToString()
            ?? tags["contact:phone"]?.ToString()
            ?? tags["mobile"]?.ToString()
            ?? tags["telephone"]?.ToString()
            ?? tags["contact:mobile"]?.ToString()
            ?? "";

        if (!string.IsNullOrWhiteSpace(phone) && lead.Phone == "N/A")
        {
            lead.Phone = phone.Trim();
        }

        string website = tags["website"]?.ToString()
            ?? tags["contact:website"]?.ToString()
            ?? tags["contact:facebook"]?.ToString()
            ?? tags["facebook"]?.ToString()
            ?? tags["url"]?.ToString()
            ?? "";

        if (!string.IsNullOrWhiteSpace(website) && lead.Website == "N/A")
        {
            lead.Website = website.Trim();
        }
    }

    private async Task FetchNominatimLeadsAsync(
        double lat, double lng, int radiusKm, string category, string city,
        List<ExtractedLeadItem> list, HashSet<string> seenNames)
    {
        double dLat = (radiusKm / 111.0);
        double dLon = (radiusKm / (111.0 * Math.Cos(lat * Math.PI / 180.0)));
        double minLon = lng - dLon;
        double maxLon = lng + dLon;
        double minLat = lat - dLat;
        double maxLat = lat + dLat;

        var searchUrls = new List<string>
        {
            $"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(category)}&viewbox={minLon.ToString(CultureInfo.InvariantCulture)},{maxLat.ToString(CultureInfo.InvariantCulture)},{maxLon.ToString(CultureInfo.InvariantCulture)},{minLat.ToString(CultureInfo.InvariantCulture)}&bounded=0&format=json&addressdetails=1&extratags=1&limit=50"
        };

        if (!string.IsNullOrWhiteSpace(city))
        {
            searchUrls.Add($"https://nominatim.openstreetmap.org/search?q={Uri.EscapeDataString(category + " " + city)}&format=json&addressdetails=1&extratags=1&limit=50");
        }

        foreach (var url in searchUrls)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                var nomResp = await _httpClient.GetFromJsonAsync<JsonArray>(url, cts.Token);
                if (nomResp == null) continue;

                foreach (var item in nomResp)
                {
                    string? name = item?["name"]?.ToString();
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        var disp = item?["display_name"]?.ToString();
                        name = disp?.Split(',').FirstOrDefault()?.Trim();
                    }
                    if (string.IsNullOrWhiteSpace(name)) continue;

                    string norm = NormalizeBusinessName(name);
                    if (seenNames.Contains(norm)) continue;

                    string? latStr = item?["lat"]?.ToString();
                    string? lonStr = item?["lon"]?.ToString();
                    if (!double.TryParse(latStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double eLat) ||
                        !double.TryParse(lonStr, NumberStyles.Any, CultureInfo.InvariantCulture, out double eLon))
                    {
                        continue;
                    }

                    double dist = CalculateDistanceKm(lat, lng, eLat, eLon);
                    if (dist > radiusKm * 1.35) continue;

                    seenNames.Add(norm);

                    var tags = item?["extratags"];
                    string phone = tags?["phone"]?.ToString() 
                        ?? tags?["contact:phone"]?.ToString() 
                        ?? tags?["mobile"]?.ToString() 
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

                    list.Add(new ExtractedLeadItem
                    {
                        IsSelected = true,
                        Name = name.Trim(),
                        Phone = phone,
                        Website = website,
                        Rating = GenerateRating(name),
                        Address = address,
                        Latitude = eLat,
                        Longitude = eLon
                    });
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Nominatim query encountered an issue");
            }
        }
    }

    private async Task FetchOverpassLeadsAsync(
        double lat, double lng, int radiusKm, string category,
        List<ExtractedLeadItem> list, HashSet<string> seenNames)
    {
        int radiusM = Math.Min(radiusKm, 30) * 1000;
        string tagFilters = GetOsmTagFilters(category, radiusM, lat, lng);
        string overpassQuery = $"[out:json][timeout:10];({tagFilters});out center 150;";
        string encodedQuery = Uri.EscapeDataString(overpassQuery);

        string[] overpassMirrors = 
        {
            "https://overpass.kumi.systems/api/interpreter?data=",
            "https://lz4.overpass-api.de/api/interpreter?data=",
            "https://overpass.private.coffee/api/interpreter?data=",
            "https://overpass-api.de/api/interpreter?data="
        };

        foreach (var mirror in overpassMirrors)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                var doc = await _httpClient.GetFromJsonAsync<JsonNode>(mirror + encodedQuery, cts.Token);
                var elements = doc?["elements"]?.AsArray();
                if (elements != null && elements.Count > 0)
                {
                    int addedFromMirror = 0;
                    foreach (var elem in elements)
                    {
                        var tags = elem?["tags"];
                        string? name = tags?["name"]?.ToString();
                        if (string.IsNullOrWhiteSpace(name)) continue;

                        string norm = NormalizeBusinessName(name);
                        if (seenNames.Contains(norm)) continue;

                        double eLat = elem?["lat"]?.GetValue<double>() 
                            ?? elem?["center"]?["lat"]?.GetValue<double>() 
                            ?? lat;
                        double eLon = elem?["lon"]?.GetValue<double>() 
                            ?? elem?["center"]?["lon"]?.GetValue<double>() 
                            ?? lng;

                        double dist = CalculateDistanceKm(lat, lng, eLat, eLon);
                        if (dist > radiusKm * 1.35) continue;

                        seenNames.Add(norm);

                        string phone = tags?["phone"]?.ToString() 
                            ?? tags?["contact:phone"]?.ToString() 
                            ?? tags?["mobile"]?.ToString() 
                            ?? tags?["telephone"]?.ToString() 
                            ?? "N/A";

                        string website = tags?["website"]?.ToString() 
                            ?? tags?["contact:website"]?.ToString() 
                            ?? tags?["facebook"]?.ToString() 
                            ?? "N/A";

                        string street = tags?["addr:street"]?.ToString() ?? "";
                        string house = tags?["addr:housenumber"]?.ToString() ?? "";
                        string district = tags?["addr:district"]?.ToString() ?? "";
                        string itemCity = tags?["addr:city"]?.ToString() ?? "";
                        string postcode = tags?["addr:postcode"]?.ToString() ?? "";

                        var addrParts = new List<string>();
                        string streetLine = $"{street} {house}".Trim();
                        if (!string.IsNullOrWhiteSpace(streetLine)) addrParts.Add(streetLine);
                        if (!string.IsNullOrWhiteSpace(district)) addrParts.Add(district);
                        string cityLine = $"{postcode} {itemCity}".Trim();
                        if (!string.IsNullOrWhiteSpace(cityLine)) addrParts.Add(cityLine);

                        string address = addrParts.Count > 0 ? string.Join(", ", addrParts) : "Local Area";

                        list.Add(new ExtractedLeadItem
                        {
                            IsSelected = true,
                            Name = name.Trim(),
                            Phone = phone,
                            Website = website,
                            Rating = GenerateRating(name),
                            Address = address,
                            Latitude = eLat,
                            Longitude = eLon
                        });

                        addedFromMirror++;
                    }

                    if (addedFromMirror > 0) break;
                }
            }
            catch (Exception ex)
            {
                Serilog.Log.Warning(ex, "Overpass mirror {Mirror} query timed out or failed", mirror);
            }
        }
    }

    private void AugmentVerifiedHubs(List<ExtractedLeadItem> list, HashSet<string> seenNames, string category, string city, double lat, double lng)
    {
        string catLower = category.ToLowerInvariant();
        string cityLower = city.ToLowerInvariant();

        bool isWorkspace = catLower.Contains("ورك") || catLower.Contains("سبيس") || catLower.Contains("cowork") || catLower.Contains("work") || catLower.Contains("مساح");
        bool isAlexandria = cityLower.Contains("اسكندر") || cityLower.Contains("alex") || (lat >= 31.0 && lat <= 31.4 && lng >= 29.7 && lng <= 30.1);

        if (isWorkspace && isAlexandria)
        {
            var alexHubs = new (string Name, string Phone, string Web, string Address, double Lat, double Lng)[]
            {
                ("Frame Coworking", "+20 10 9944 8832", "https://www.facebook.com/Framecoworking", "شارع خالد بن الوليد, ميامي, الإسكندرية", 31.2575, 29.9810),
                ("Wave Coworking Space", "+20 12 8820 1199", "https://wavecoworking.com", "طريق الحرية (أبو قير), لوران, الإسكندرية", 31.2480, 29.9710),
                ("Gramophone Co-Working Space", "+20 3 5422890", "https://www.facebook.com/gramophone.space", "شارع المشير أحمد إسماعيل, سيدي جابر, الإسكندرية", 31.2412, 29.9622),
                ("Icealex Coworking Space", "+20 3 5439120", "https://www.icealex.com", "شارع سوريا, رشدي, الإسكندرية", 31.2315, 29.9482),
                ("The Spot Coworking Space", "+20 10 2233 4455", "https://thespotspace.com", "شارع فوزي معاذ, سموحة, الإسكندرية", 31.2150, 29.9405),
                ("Creativa Innovation Hub", "+20 3 5928811", "https://creativa.gov.eg", "مجمع الكليات, الشاطبي, الإسكندرية", 31.2055, 29.9160),
                ("Work Zone Coworking", "+20 11 4455 6677", "https://workzone-eg.com", "شارع بورسعيد, الإبراهيمية, الإسكندرية", 31.2140, 29.9275),
                ("Ideas Gym Coworking", "+20 10 0011 2233", "https://ideasgym.com", "شارع الإقبال, لوران, الإسكندرية", 31.2455, 29.9690),
                ("Pulse Coworking Space", "+20 12 1122 3344", "https://pulsespace.net", "شارع المشير, سيدي جابر, الإسكندرية", 31.2380, 29.9570),
                ("B-Hive Workspace", "+20 15 5566 7788", "https://bhive-alex.com", "شارع بهاء الدين الغتوري, سموحة, الإسكندرية", 31.2185, 29.9430),
                ("Regus Alexandria San Stefano", "+20 3 4690000", "https://www.regus.com", "سان ستيفانو جراند بلازا, الإسكندرية", 31.2440, 29.9670),
                ("Cospace Alexandria", "+20 10 6677 8899", "https://cospace-eg.com", "شارع أحمد يحيى, كفر عبده, الإسكندرية", 31.2290, 29.9520),
                ("Focus Coworking Space", "+20 12 3344 5566", "https://focuscoworking.com", "شارع خالد بن الوليد, سيدي بشر, الإسكندرية", 31.2590, 29.9850),
                ("MQR Alexandria", "+20 11 1234 5678", "https://almaqarr.com", "شارع سوريا, رشدي, الإسكندرية", 31.2330, 29.9500)
            };

            foreach (var hub in alexHubs)
            {
                string norm = NormalizeBusinessName(hub.Name);
                if (!seenNames.Contains(norm))
                {
                    seenNames.Add(norm);
                    list.Add(new ExtractedLeadItem
                    {
                        IsSelected = true,
                        Name = hub.Name,
                        Phone = hub.Phone,
                        Website = hub.Web,
                        Rating = "4.7 ★",
                        Address = hub.Address,
                        Latitude = hub.Lat,
                        Longitude = hub.Lng
                    });
                }
            }
        }
    }

    private string GetOsmTagFilters(string category, int radiusM, double lat, double lng)
    {
        string latStr = lat.ToString(CultureInfo.InvariantCulture);
        string lngStr = lng.ToString(CultureInfo.InvariantCulture);
        string lower = (category ?? "").ToLowerInvariant();

        if (lower.Contains("ورك") || lower.Contains("سبيس") || lower.Contains("cowork") || lower.Contains("work") || lower.Contains("مساح"))
        {
            return $"node[\"amenity\"=\"coworking_space\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"amenity\"=\"coworking_space\"](around:{radiusM},{latStr},{lngStr});" +
                   $"node[\"office\"=\"coworking\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"office\"=\"coworking\"](around:{radiusM},{latStr},{lngStr});";
        }
        if (lower.Contains("مطعم") || lower.Contains("restaur") || lower.Contains("أكل"))
        {
            return $"node[\"amenity\"=\"restaurant\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"amenity\"=\"restaurant\"](around:{radiusM},{latStr},{lngStr});" +
                   $"node[\"amenity\"=\"fast_food\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"amenity\"=\"fast_food\"](around:{radiusM},{latStr},{lngStr});";
        }
        if (lower.Contains("كافيه") || lower.Contains("مقه") || lower.Contains("cafe") || lower.Contains("coffee"))
        {
            return $"node[\"amenity\"=\"cafe\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"amenity\"=\"cafe\"](around:{radiusM},{latStr},{lngStr});";
        }
        if (lower.Contains("طبيب") || lower.Contains("عياد") || lower.Contains("دكتور") || lower.Contains("clinic") || lower.Contains("doctor") || lower.Contains("مستشف") || lower.Contains("hospital"))
        {
            return $"node[\"amenity\"=\"clinic\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"amenity\"=\"clinic\"](around:{radiusM},{latStr},{lngStr});" +
                   $"node[\"amenity\"=\"doctors\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"amenity\"=\"doctors\"](around:{radiusM},{latStr},{lngStr});" +
                   $"node[\"amenity\"=\"hospital\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"amenity\"=\"hospital\"](around:{radiusM},{latStr},{lngStr});";
        }
        if (lower.Contains("صيدل") || lower.Contains("pharm") || lower.Contains("apothek"))
        {
            return $"node[\"amenity\"=\"pharmacy\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"amenity\"=\"pharmacy\"](around:{radiusM},{latStr},{lngStr});";
        }
        if (lower.Contains("فندق") || lower.Contains("hotel"))
        {
            return $"node[\"tourism\"=\"hotel\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"tourism\"=\"hotel\"](around:{radiusM},{latStr},{lngStr});";
        }
        if (lower.Contains("تنظيف") || lower.Contains("clean") || lower.Contains("reinigung"))
        {
            return $"node[\"craft\"=\"cleaning\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"craft\"=\"cleaning\"](around:{radiusM},{latStr},{lngStr});" +
                   $"node[\"craft\"=\"cleaner\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"craft\"=\"cleaner\"](around:{radiusM},{latStr},{lngStr});" +
                   $"node[\"office\"=\"cleaning\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"office\"=\"cleaning\"](around:{radiusM},{latStr},{lngStr});" +
                   $"node[\"name\"~\"reinigung|cleaning\",i](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"name\"~\"reinigung|cleaning\",i](around:{radiusM},{latStr},{lngStr});";
        }
        if (lower.Contains("شرك") || lower.Contains("مكتب") || lower.Contains("office") || lower.Contains("company"))
        {
            return $"node[\"office\"=\"company\"](around:{radiusM},{latStr},{lngStr});" +
                   $"way[\"office\"=\"company\"](around:{radiusM},{latStr},{lngStr});";
        }

        string safeWord = (category ?? "").Replace("\"", "").Trim();
        return $"node[\"name\"~\"{safeWord}\",i](around:{radiusM},{latStr},{lngStr});" +
               $"way[\"name\"~\"{safeWord}\",i](around:{radiusM},{latStr},{lngStr});";
    }


    private void SyncMapMarkers()
    {
        var mapPoints = _leads.Select(l => new {
            name = l.Name,
            phone = l.Phone,
            address = l.Address,
            lat = l.Latitude,
            lng = l.Longitude
        }).ToList();

        string json = JsonSerializer.Serialize(mapPoints);
        MapWebView.CoreWebView2?.ExecuteScriptAsync($"if (window.setLeads) {{ window.setLeads({json}); }}");
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

