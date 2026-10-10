using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using ScripOx.Desktop.Models;
using ScripOx.Desktop.Services;

namespace ScripOx.Desktop.Views.Companies;

public partial class CompanyDetailsWindow : Window
{
    private readonly long _companyId;
    private readonly CompanyService _companyService;
    private Company? _company;
    private bool _hasNotePlaceholder = true;

    public CompanyDetailsWindow(long companyId)
    {
        InitializeComponent();
        _companyId = companyId;
        _companyService = new CompanyService(AppSettings.ConnectionString);
        Loaded += CompanyDetailsWindow_Loaded;
    }

    private async void CompanyDetailsWindow_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplyLanguage();
            await LoadStatusesAsync();
            await LoadDetailsAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Error in CompanyDetailsWindow_Loaded for ID {CompanyId}", _companyId);
        }
    }

    private void ApplyLanguage()
    {
        var lang = LocalizationService.CurrentLanguage;
        var isAr = lang == "ar";
        var isDe = lang == "de";

        FlowDirection = isAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        Title = isAr ? "ملف العميل / كرت الشركة" : (isDe ? "Unternehmensakte / Lead-Profil" : "Lead CRM Profile");

        TxtLblCoordinates.Text = isAr ? "الإحداثيات الجغرافية (X , Y)" : (isDe ? "GPS-Koordinaten (X, Y)" : "GPS Coordinates (X, Y)");
        TxtLblCoordY.Text = isAr ? "خط العرض (Y / Latitude)" : (isDe ? "Breitengrad (Y / Latitude)" : "Latitude (Y)");
        TxtLblCoordX.Text = isAr ? "خط الطول (X / Longitude)" : (isDe ? "Längengrad (X / Longitude)" : "Longitude (X)");
        BtnSaveCoordinates.Content = isAr ? "حفظ الإحداثيات" : (isDe ? "Koordinaten speichern" : "Save Coordinates");
        BtnOpenInMap.Content = isAr ? "🗺️ الخريطة" : (isDe ? "🗺️ Karte" : "🗺️ Open Map");
    }

    private async Task LoadStatusesAsync()
    {
        try
        {
            var statuses = await _companyService.GetStatusesAsync();
            CmbUpdateStatus.ItemsSource = statuses;
        }
        catch
        {
            // Fallback handled gracefully
        }
    }

    private async Task LoadDetailsAsync()
    {
        try
        {
            _company = await _companyService.GetByIdAsync(_companyId);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to load company ID {CompanyId}", _companyId);
            return;
        }

        if (_company != null)
        {
            TxtName.Text = _company.DisplayName;
            TxtCategory.Text = _company.Category ?? "General";
            TxtSegmentBadge.Text = $"Segment: {(!string.IsNullOrWhiteSpace(_company.Category) ? _company.Category : "General")}";
            
            var loc = string.Join(", ", new[] { _company.City, _company.Country }.Where(s => !string.IsNullOrWhiteSpace(s)));
            TxtLocationBadge.Text = $"Location: {(!string.IsNullOrWhiteSpace(loc) ? loc : "Global")}";

            TxtPhone.Text = !string.IsNullOrWhiteSpace(_company.Phone) ? _company.Phone : "N/A";
            TxtEmail.Text = !string.IsNullOrWhiteSpace(_company.Email) ? _company.Email : "N/A";
            TxtWebsiteText.Text = !string.IsNullOrWhiteSpace(_company.Website) ? _company.Website : "N/A";
            TxtAddress.Text = !string.IsNullOrWhiteSpace(_company.Address) ? _company.Address : "N/A";

            // GPS Coordinates (X = Longitude, Y = Latitude)
            TxtCoordX.Text = _company.X.HasValue ? _company.X.Value.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) : "";
            TxtCoordY.Text = _company.Y.HasValue ? _company.Y.Value.ToString("F6", System.Globalization.CultureInfo.InvariantCulture) : "";

            // Status Badge
            TxtCurrentStatus.Text = _company.StatusName ?? "New Lead";
            if (!string.IsNullOrWhiteSpace(_company.StatusColor))
            {
                try
                {
                    var color = (Color)ColorConverter.ConvertFromString(_company.StatusColor);
                    PillStatus.Background = new SolidColorBrush(color);
                }
                catch
                {
                    PillStatus.Background = (Brush)FindResource("ExDarkObsidian");
                }
            }

            if (_company.StatusId.HasValue)
            {
                CmbUpdateStatus.SelectedValue = _company.StatusId.Value;
            }

            // Communication Buttons States
            BtnWhatsApp.IsEnabled = !string.IsNullOrWhiteSpace(_company.Phone);
            BtnEmail.IsEnabled = !string.IsNullOrWhiteSpace(_company.Email);
            BtnWebsite.IsEnabled = !string.IsNullOrWhiteSpace(_company.Website);

            LstNotes.ItemsSource = _company.Notes;
        }
    }

    private async void BtnSaveCoordinates_Click(object sender, RoutedEventArgs e)
    {
        double? x = null;
        double? y = null;

        if (double.TryParse(TxtCoordX.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedX))
        {
            x = parsedX;
        }

        if (double.TryParse(TxtCoordY.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double parsedY))
        {
            y = parsedY;
        }

        try
        {
            await _companyService.UpdateCoordinatesAsync(_companyId, x, y);
            var uid = AuthService.CurrentUser?.Id ?? 1;
            await _companyService.AddNoteAsync(_companyId, uid, $"Coordinates updated: Lat (Y)={y?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "N/A"}, Lon (X)={x?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "N/A"}");
            await LoadDetailsAsync();

            var isDe = LocalizationService.CurrentLanguage == "de";
            var isAr = LocalizationService.CurrentLanguage == "ar";
            string msg = isAr 
                ? "تم حفظ الإحداثيات (X , Y) في كرت العميل بنجاح." 
                : (isDe ? "Koordinaten (X, Y) wurden erfolgreich gespeichert." : "Coordinates (X, Y) saved successfully.");
            MessageBox.Show(msg, "ScripOx CRM", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to update coordinates for company ID {CompanyId}", _companyId);
            MessageBox.Show($"Failed to save coordinates: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnOpenInMap_Click(object sender, RoutedEventArgs e)
    {
        if (double.TryParse(TxtCoordY.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lat) &&
            double.TryParse(TxtCoordX.Text.Trim(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double lng))
        {
            OpenBrowser($"https://www.google.com/maps?q={lat.ToString(System.Globalization.CultureInfo.InvariantCulture)},{lng.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
        }
        else
        {
            var isDe = LocalizationService.CurrentLanguage == "de";
            var isAr = LocalizationService.CurrentLanguage == "ar";
            string msg = isAr ? "لا توجد إحداثيات صالحة لعرضها على الخريطة." : (isDe ? "Keine gültigen Koordinaten vorhanden." : "No valid coordinates available.");
            MessageBox.Show(msg, "Map", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    private async void BtnQualInterested_Click(object sender, RoutedEventArgs e)
    {
        await SetStatusAsync(2, "Interested / مهتم");
    }


    private async void BtnQualFollowUp_Click(object sender, RoutedEventArgs e)
    {
        await SetStatusAsync(3, "Needs Follow-up / متابعة");
    }

    private async void BtnQualWon_Click(object sender, RoutedEventArgs e)
    {
        await SetStatusAsync(4, "Deal Won / تم التعاقد");
    }

    private async void BtnQualNotInterested_Click(object sender, RoutedEventArgs e)
    {
        await SetStatusAsync(5, "Not Interested / غير مهتم");
    }

    private async Task SetStatusAsync(int statusId, string label)
    {
        var uid = AuthService.CurrentUser?.Id ?? 1;
        await _companyService.UpdateStatusAsync(_companyId, statusId, uid);
        await _companyService.AddNoteAsync(_companyId, uid, $"Qualification updated to: {label}");
        await LoadDetailsAsync();
    }

    private async void BtnSaveStatus_Click(object sender, RoutedEventArgs e)
    {
        if (CmbUpdateStatus.SelectedValue is int newStatusId)
        {
            var uid = AuthService.CurrentUser?.Id ?? 1;
            await _companyService.UpdateStatusAsync(_companyId, newStatusId, uid);
            await LoadDetailsAsync();
        }
    }

    private async void BtnUpdateSegment_Click(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(TxtCategory.Text))
        {
            await _companyService.UpdateCategoryAsync(_companyId, TxtCategory.Text.Trim());
            var uid = AuthService.CurrentUser?.Id ?? 1;
            await _companyService.AddNoteAsync(_companyId, uid, $"Segment categorized as: {TxtCategory.Text.Trim()}");
            await LoadDetailsAsync();
            MessageBox.Show("Segment updated successfully.", "ScripOx CRM", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }

    private void BtnWhatsApp_Click(object sender, RoutedEventArgs e)
    {
        if (_company != null && !string.IsNullOrWhiteSpace(_company.Phone))
        {
            var cleanPhone = Regex.Replace(_company.Phone, @"[^\d]", "");
            if (cleanPhone.Length > 0)
            {
                OpenBrowser($"https://wa.me/{cleanPhone}");
                return;
            }
        }
        MessageBox.Show("No valid phone number available for WhatsApp.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
    }

    private void BtnEmail_Click(object sender, RoutedEventArgs e)
    {
        if (_company != null && !string.IsNullOrWhiteSpace(_company.Email))
        {
            OpenBrowser($"mailto:{_company.Email}");
        }
    }

    private void BtnWebsite_Click(object sender, RoutedEventArgs e)
    {
        if (_company != null && !string.IsNullOrWhiteSpace(_company.Website))
        {
            var url = _company.Website;
            if (!url.StartsWith("http://") && !url.StartsWith("https://"))
                url = "https://" + url;
            OpenBrowser(url);
        }
    }

    private static void OpenBrowser(string url)
    {
        try
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignore failure on system protocol handler
        }
    }

    private void TxtNewNote_GotFocus(object sender, RoutedEventArgs e)
    {
        if (_hasNotePlaceholder)
        {
            TxtNewNote.Text = "";
            _hasNotePlaceholder = false;
        }
    }

    private async void BtnAddNote_Click(object sender, RoutedEventArgs e)
    {
        var content = TxtNewNote.Text.Trim();
        if (!string.IsNullOrWhiteSpace(content) && !_hasNotePlaceholder)
        {
            var uid = AuthService.CurrentUser?.Id ?? 1;
            await _companyService.AddNoteAsync(_companyId, uid, content);
            TxtNewNote.Text = "";
            _hasNotePlaceholder = true;
            await LoadDetailsAsync();
        }
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();
}
