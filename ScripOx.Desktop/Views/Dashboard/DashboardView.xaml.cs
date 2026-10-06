using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScripOx.Desktop.Services;
using ScripOx.Desktop.Views.Auth;
using ScripOx.Desktop.Views.Updates;

namespace ScripOx.Desktop.Views.Dashboard;

public partial class DashboardView : Window
{
    private readonly List<Button> _navButtons = [];
    private Button? _activeNavButton;
    private UpdateReleaseInfo? _latestUpdate;

    public DashboardView()
    {
        InitializeComponent();

        _navButtons.AddRange([
            BtnNavDashboard,
            BtnNavCompanies,
            BtnNavSources,
            BtnNavJobs,
            BtnNavRaw,
            BtnNavMap,
            BtnNavExports,
            BtnNavExtension,
            BtnNavSettings
        ]);

        var user = AuthService.CurrentUser;
        if (user is not null)
        {
            TxtUserName.Text = user.FullName.Length > 0 ? user.FullName : user.Username;
            TxtUserRole.Text = user.RoleName.Replace("_", " ").ToTitleCase();
        }

        // Set Dashboard as initial active button
        SetActiveNavButton(BtnNavDashboard);
        _currentPageTag = "dashboard";
        ApplyLanguage();

        // Check for updates in background
        if (AppSettings.AutoCheckUpdatesOnStartup)
        {
            CheckForUpdatesAsync();
        }
    }

    private string _currentPageTag = "dashboard";

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string tag)
        {
            SetActiveNavButton(btn);
            _currentPageTag = tag;
            UpdatePageTitle(tag);
            NavigateTo(tag);
        }
    }

    private void UpdatePageTitle(string tag)
    {
        var lang = LocalizationService.CurrentLanguage;
        TxtPageTitle.Text = tag switch
        {
            "dashboard" => lang == "ar" ? "حركة العملاء والشركات" : (lang == "de" ? "Kundenbewegungen" : "Customer Movements"),
            "sources"   => lang == "ar" ? "مصادر الاستخراج والربط" : (lang == "de" ? "Datenquellen & Gateways" : "Data Sources & Gateways"),
            "jobs"      => lang == "ar" ? "مهام وعمليات الاستخراج" : (lang == "de" ? "Extraktions-Pipelines" : "Extraction Pipelines & Jobs"),
            "raw"       => lang == "ar" ? "السجلات والبيانات الخام" : (lang == "de" ? "Rohdatensätze" : "Raw Data Records"),
            "companies" => lang == "ar" ? "إدارة الشركات والعملاء" : (lang == "de" ? "Unternehmensverwaltung" : "Companies Management"),
            "map"       => lang == "ar" ? "مستكشف الخرائط الجغرافية" : (lang == "de" ? "Geografischer Explorer" : "Geographical Explorer"),
            "exports"   => lang == "ar" ? "مركز تصدير التقارير" : (lang == "de" ? "Datenexport-Zentrum" : "Data Exports Center"),
            "extension" => lang == "ar" ? "إضافة المتصفح الذكية" : (lang == "de" ? "Erweiterungs-Hub" : "Visual Extension Hub"),
            "settings"  => lang == "ar" ? "إعدادات النظام" : (lang == "de" ? "Systemeinstellungen" : "System Settings"),
            _           => tag
        };
    }

    private void SetActiveNavButton(Button activeBtn)
    {
        _activeNavButton = activeBtn;
        var darkBrush = (Brush)FindResource("ExDarkObsidian");
        var secBrush = (Brush)FindResource("ExTextSecondary");

        foreach (var btn in _navButtons)
        {
            if (btn == activeBtn)
            {
                btn.Background = darkBrush;
                btn.Foreground = Brushes.White;
                btn.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                btn.Background = Brushes.Transparent;
                btn.Foreground = secBrush;
                btn.FontWeight = FontWeights.Medium;
            }
        }
    }

    private void NavigateTo(string page)
    {
        switch (page)
        {
            case "dashboard":
                MainFrame.Navigate(new ScripOx.Desktop.Views.Dashboard.DashboardPage());
                break;
            case "companies":
                MainFrame.Navigate(new ScripOx.Desktop.Views.Companies.CompaniesPage());
                break;
            case "jobs":
                MainFrame.Navigate(new ScripOx.Desktop.Views.Jobs.JobsPage());
                break;
            case "map":
                MainFrame.Navigate(new ScripOx.Desktop.Views.Map.MapPage());
                break;
            case "raw":
                MainFrame.Navigate(new ScripOx.Desktop.Views.RawRecords.RawRecordsPage());
                break;
            case "sources":
                MainFrame.Navigate(new ScripOx.Desktop.Views.Sources.SourcesPage());
                break;
            case "exports":
                MainFrame.Navigate(new ScripOx.Desktop.Views.Exports.ExportsPage());
                break;
            case "extension":
                MainFrame.Navigate(new ScripOx.Desktop.Views.Extension.ExtensionPage());
                break;
            case "settings":
                MainFrame.Navigate(new ScripOx.Desktop.Views.Settings.SettingsPage());
                break;
            default:
                MainFrame.Navigate(new ScripOx.Desktop.Views.Dashboard.DashboardPage());
                break;
        }
    }

    private void BtnSignOut_Click(object sender, RoutedEventArgs e)
    {
        AuthService.Logout();
        var login = new LoginView();
        login.Show();
        Close();
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e)    => Close();
    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private bool _sidebarCollapsed = false;
    private bool _isHovered = false;

    private void BtnToggleSidebar_Click(object sender, RoutedEventArgs e)
    {
        _sidebarCollapsed = !_sidebarCollapsed;
        UpdateSidebarLayout();
    }

    private void Sidebar_MouseEnter(object sender, MouseEventArgs e)
    {
        if (_sidebarCollapsed)
        {
            _isHovered = true;
            UpdateSidebarLayout();
        }
    }

    private void Sidebar_MouseLeave(object sender, MouseEventArgs e)
    {
        if (_sidebarCollapsed)
        {
            _isHovered = false;
            UpdateSidebarLayout();
        }
    }

    private void UpdateSidebarLayout()
    {
        bool isExpanded = !_sidebarCollapsed || _isHovered;

        SidebarColumn.Width = isExpanded ? new GridLength(240) : new GridLength(64);

        PanelLogo.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
        PanelMiniLogo.Visibility = isExpanded ? Visibility.Collapsed : Visibility.Visible;

        PanelUserInfo.Visibility = isExpanded ? Visibility.Visible : Visibility.Collapsed;
        BtnMiniSignOut.Visibility = isExpanded ? Visibility.Collapsed : Visibility.Visible;

        SepNav.Margin = isExpanded ? new Thickness(12, 10, 12, 10) : new Thickness(4, 8, 4, 8);

        UpdateButton(BtnNavDashboard,  "Dashboard",     "DB", isExpanded);
        UpdateButton(BtnNavCompanies,  "Companies",     "CP", isExpanded);
        UpdateButton(BtnNavSources,    "Sources",       "SC", isExpanded);
        UpdateButton(BtnNavJobs,       "Jobs",          "JB", isExpanded);
        UpdateButton(BtnNavRaw,        "Raw Records",   "RR", isExpanded);
        UpdateButton(BtnNavMap,        "Map Explorer",  "MP", isExpanded);
        UpdateButton(BtnNavExports,    "Exports",       "EX", isExpanded);
        UpdateButton(BtnNavExtension,  "Extension",     "ET", isExpanded);
        UpdateButton(BtnNavSettings,   "Settings",      "ST", isExpanded);

        if (_activeNavButton != null)
        {
            SetActiveNavButton(_activeNavButton);
        }
    }

    private static void UpdateButton(Button btn, string expandedText, string collapsedText, bool isExpanded)
    {
        btn.Content = isExpanded ? expandedText : collapsedText;
        btn.Padding = isExpanded ? new Thickness(16, 11, 16, 11) : new Thickness(0, 11, 0, 11);
        btn.HorizontalContentAlignment = isExpanded ? HorizontalAlignment.Left : HorizontalAlignment.Center;
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void BtnLang_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string lang)
        {
            LocalizationService.SetLanguage(lang);
            ApplyLanguage();
        }
    }

    private void ApplyLanguage()
    {
        var lang = LocalizationService.CurrentLanguage;
        FlowDirection = lang == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        if (lang == "ar")
        {
            BtnNavDashboard.Content = "لوحة التحكم";
            BtnNavCompanies.Content = "الشركات والعملاء";
            BtnNavSources.Content   = "مصادر الاستخراج";
            BtnNavJobs.Content      = "مهام الاستخراج";
            BtnNavRaw.Content       = "السجلات الخام";
            BtnNavMap.Content       = "مستكشف الخرائط";
            BtnNavExports.Content   = "مركز التصدير";
            BtnNavExtension.Content = "إضافة المتصفح";
            BtnNavSettings.Content  = "الإعدادات";
            BtnSignOut.Content      = "تسجيل الخروج";
            BtnCreateRecord.Content = "+ سجل جديد";
            TxtGlobalSearch.Text    = "ابحث عن الشركات، السجلات، أو المهام...";
        }
        else if (lang == "de")
        {
            BtnNavDashboard.Content = "Übersicht";
            BtnNavCompanies.Content = "Unternehmen";
            BtnNavSources.Content   = "Datenquellen";
            BtnNavJobs.Content      = "Extraktions-Jobs";
            BtnNavRaw.Content       = "Rohdaten";
            BtnNavMap.Content       = "Karten-Explorer";
            BtnNavExports.Content   = "Exporte";
            BtnNavExtension.Content = "Erweiterung";
            BtnNavSettings.Content  = "Einstellungen";
            BtnSignOut.Content      = "Abmelden";
            BtnCreateRecord.Content = "+ Neuer Datensatz";
            TxtGlobalSearch.Text    = "Unternehmen, Datensätze suchen...";
        }
        else
        {
            BtnNavDashboard.Content = "Dashboard";
            BtnNavCompanies.Content = "Companies";
            BtnNavSources.Content   = "Sources";
            BtnNavJobs.Content      = "Jobs";
            BtnNavRaw.Content       = "Raw Records";
            BtnNavMap.Content       = "Map Explorer";
            BtnNavExports.Content   = "Exports";
            BtnNavExtension.Content = "Extension";
            BtnNavSettings.Content  = "Settings";
            BtnSignOut.Content      = "Sign Out";
            BtnCreateRecord.Content = "+ Create Record";
            TxtGlobalSearch.Text    = "Search companies, records, or pipelines...";
        }

        UpdatePageTitle(_currentPageTag);
        UpdateLanguageButtons();
        NavigateTo(_currentPageTag);
    }

    private void UpdateLanguageButtons()
    {
        var current = LocalizationService.CurrentLanguage;
        HighlightLangButton(BtnLangEN, current == "en");
        HighlightLangButton(BtnLangDE, current == "de");
        HighlightLangButton(BtnLangAR, current == "ar");
    }

    private void HighlightLangButton(Button btn, bool isActive)
    {
        if (isActive)
        {
            btn.Background = (Brush)FindResource("ExDarkObsidian");
            btn.Foreground = Brushes.White;
        }
        else
        {
            btn.Background = Brushes.Transparent;
            btn.Foreground = (Brush)FindResource("ExTextSecondary");
        }
    }

    // ── Software Updates Handlers ─────────────────────────────
    private async void CheckForUpdatesAsync()
    {
        try
        {
            var update = await UpdateService.CheckForUpdatesAsync();
            if (update is { IsUpdateAvailable: true })
            {
                _latestUpdate = update;
                var isAr = LocalizationService.CurrentLanguage == "ar";
                TxtBannerUpdateMessage.Text = isAr
                    ? $"يتوفر إصدار جديد لـ ScripOx ({update.TagName}) يتضمن تحسينات ومزايا جديدة."
                    : $"A new version of ScripOx ({update.TagName}) is available.";
                BtnBannerViewDetails.Content = isAr ? "عرض التحديث" : "View Update";
                BannerUpdateNotification.Visibility = Visibility.Visible;
            }
        }
        catch
        {
            // Silently ignore background check failures
        }
    }

    private void BtnBannerViewDetails_Click(object sender, RoutedEventArgs e)
    {
        if (_latestUpdate != null)
        {
            var dialog = new UpdateDialog(_latestUpdate) { Owner = this };
            dialog.ShowDialog();
        }
    }

    private void BtnBannerDismiss_Click(object sender, RoutedEventArgs e)
    {
        BannerUpdateNotification.Visibility = Visibility.Collapsed;
    }
}

file static class StringExt
{
    public static string ToTitleCase(this string s) =>
        System.Globalization.CultureInfo.CurrentCulture.TextInfo.ToTitleCase(s.ToLower());
}
