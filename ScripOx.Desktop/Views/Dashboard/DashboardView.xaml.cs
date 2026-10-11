using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ScripOx.Desktop.Models;
using ScripOx.Desktop.Services;
using ScripOx.Desktop.Views.Auth;
using ScripOx.Desktop.Views.Updates;

namespace ScripOx.Desktop.Views.Dashboard;

public static class NavHelper
{
    public static readonly DependencyProperty IconProperty =
        DependencyProperty.RegisterAttached("Icon", typeof(Geometry), typeof(NavHelper), new PropertyMetadata(null));

    public static Geometry? GetIcon(DependencyObject obj) => (Geometry?)obj.GetValue(IconProperty);
    public static void SetIcon(DependencyObject obj, Geometry? value) => obj.SetValue(IconProperty, value);
}

public partial class DashboardView : Window
{
    private readonly List<Button> _navButtons = [];
    private Button? _activeNavButton;
    private UpdateReleaseInfo? _latestUpdate;
    private readonly CompanyService _companyService = new(AppSettings.ConnectionString);
    private readonly System.Windows.Threading.DispatcherTimer _searchDebounceTimer;

    public DashboardView()
    {
        InitializeComponent();

        _searchDebounceTimer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _searchDebounceTimer.Tick += async (s, e) =>
        {
            _searchDebounceTimer.Stop();
            await PerformSearchAutocompleteAsync();
        };

        _navButtons.AddRange([
            BtnNavDashboard,
            BtnNavCompanies,
            BtnNavSources,
            BtnNavJobs,
            BtnNavRaw,
            BtnNavMap,
            BtnNavDataExtract,
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
        TxtPageTitle.Text = tag switch
        {
            "dashboard" => LocalizationService.Get("title.dashboard"),
            "sources"   => LocalizationService.Get("title.sources"),
            "jobs"      => LocalizationService.Get("title.jobs"),
            "raw"       => LocalizationService.Get("title.raw_records"),
            "companies" => LocalizationService.Get("title.companies"),
            "map"       => LocalizationService.Get("title.map"),
            "extract"   => LocalizationService.Get("title.extract"),
            "exports"   => LocalizationService.Get("title.exports"),
            "extension" => LocalizationService.Get("title.extension"),
            "settings"  => LocalizationService.Get("title.settings"),
            _           => tag
        };
    }

    private void SetActiveNavButton(Button activeBtn)
    {
        _activeNavButton = activeBtn;
        var activePill = new SolidColorBrush(Color.FromRgb(0xEA, 0xEE, 0xF2)); // VS Code soft pill #EAEEF2
        var activeText = (Brush)FindResource("ExDarkObsidian"); // #0F172A
        var secBrush = (Brush)FindResource("ExTextSecondary"); // #64748B

        foreach (var btn in _navButtons)
        {
            if (btn == activeBtn)
            {
                btn.Background = activePill;
                btn.Foreground = activeText;
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

    private void TxtGlobalSearch_GotFocus(object sender, RoutedEventArgs e)
    {
        if (TxtGlobalSearch.Text == LocalizationService.Get("search.placeholder"))
        {
            TxtGlobalSearch.Text = "";
            TxtGlobalSearch.Foreground = (Brush)FindResource("ExTextPrimary");
        }
    }

    private void TxtGlobalSearch_LostFocus(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtGlobalSearch.Text))
        {
            TxtGlobalSearch.Text = LocalizationService.Get("search.placeholder");
            TxtGlobalSearch.Foreground = (Brush)FindResource("ExTextSecondary");
            BtnClearSearch.Visibility = Visibility.Collapsed;
        }
    }

    private void TxtGlobalSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (BtnClearSearch == null || PopupSuggestions == null || TxtGlobalSearch == null)
            return;

        var text = TxtGlobalSearch.Text.Trim();
        bool isPlaceholder = text == LocalizationService.Get("search.placeholder");

        BtnClearSearch.Visibility = !string.IsNullOrEmpty(text) && !isPlaceholder
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (string.IsNullOrEmpty(text) || isPlaceholder || text.Length < 1)
        {
            PopupSuggestions.IsOpen = false;
            return;
        }

        _searchDebounceTimer?.Stop();
        _searchDebounceTimer?.Start();
    }

    private async Task PerformSearchAutocompleteAsync()
    {
        var query = TxtGlobalSearch.Text.Trim();
        if (string.IsNullOrWhiteSpace(query) ||
            query == LocalizationService.Get("search.placeholder"))
        {
            PopupSuggestions.IsOpen = false;
            return;
        }

        try
        {
            var (results, total) = await _companyService.GetPagedAsync(new CompanyFilter { Search = query }, page: 1, pageSize: 8);
            if (results.Count > 0)
            {
                LstSuggestions.ItemsSource = results;
                TxtSuggestionsHeader.Text = LocalizationService.Get("search.suggestions_live");
                TxtSuggestionsCount.Text = $"{total} " + LocalizationService.Get("search.matches");
                PopupSuggestions.IsOpen = true;
            }
            else
            {
                LstSuggestions.ItemsSource = null;
                TxtSuggestionsHeader.Text = LocalizationService.Get("search.no_matches");
                TxtSuggestionsCount.Text = "0";
                PopupSuggestions.IsOpen = true;
            }
        }
        catch
        {
            PopupSuggestions.IsOpen = false;
        }
    }

    private void TxtGlobalSearch_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            PopupSuggestions.IsOpen = false;
            ExecuteSearchNavigation();
        }
        else if (e.Key == Key.Escape)
        {
            PopupSuggestions.IsOpen = false;
        }
        else if (e.Key == Key.Down && PopupSuggestions.IsOpen)
        {
            LstSuggestions.Focus();
        }
    }

    private void LstSuggestions_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (LstSuggestions.SelectedItem is Company comp)
        {
            PopupSuggestions.IsOpen = false;
            LstSuggestions.SelectedItem = null;

            try
            {
                var details = new Views.Companies.CompanyDetailsWindow(comp.Id) { Owner = this };
                details.ShowDialog();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to open details from autocomplete");
            }
        }
    }

    private void BtnClearSearch_Click(object sender, RoutedEventArgs e)
    {
        TxtGlobalSearch.Text = "";
        PopupSuggestions.IsOpen = false;
        BtnClearSearch.Visibility = Visibility.Collapsed;
        TxtGlobalSearch.Focus();
    }

    private void BtnViewAllSearch_Click(object sender, RoutedEventArgs e)
    {
        PopupSuggestions.IsOpen = false;
        ExecuteSearchNavigation();
    }

    private void ExecuteSearchNavigation()
    {
        var text = TxtGlobalSearch.Text.Trim();
        if (text == LocalizationService.Get("search.placeholder"))
        {
            text = "";
        }

        NavigateToCompaniesWithSearch(text);
    }

    private void NavigateToCompaniesWithSearch(string search)
    {
        _currentPageTag = "companies";
        UpdatePageTitle("companies");
        SetActiveNavButton(BtnNavCompanies);
        MainFrame.Navigate(new Views.Companies.CompaniesPage(search));
    }

    private void BtnCreateRecord_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var addWindow = new Views.Companies.AddCompanyWindow { Owner = this };
            if (addWindow.ShowDialog() == true)
            {
                NavigateTo(_currentPageTag);
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to open AddCompanyWindow");
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
            case "extract":
                MainFrame.Navigate(new ScripOx.Desktop.Views.DataExtract.DataExtractPage());
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

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Application.Current.Shutdown();
    private void BtnMinimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void BtnMaximize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    protected override void OnClosed(EventArgs e)
    {
        base.OnClosed(e);
        if (Application.Current.Windows.Count == 0)
        {
            Application.Current.Shutdown();
        }
    }

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

        UpdateButton(BtnNavDashboard,  "dashboard", isExpanded);
        UpdateButton(BtnNavCompanies,  "companies", isExpanded);
        UpdateButton(BtnNavSources,    "sources",   isExpanded);
        UpdateButton(BtnNavJobs,       "jobs",      isExpanded);
        UpdateButton(BtnNavRaw,        "raw",       isExpanded);
        UpdateButton(BtnNavMap,        "map",       isExpanded);
        UpdateButton(BtnNavDataExtract,"extract",   isExpanded);
        UpdateButton(BtnNavExports,    "exports",   isExpanded);
        UpdateButton(BtnNavExtension,  "extension", isExpanded);
        UpdateButton(BtnNavSettings,   "settings",  isExpanded);

        if (_activeNavButton != null)
        {
            SetActiveNavButton(_activeNavButton);
        }
    }

    private void UpdateButton(Button btn, string tag, bool isExpanded)
    {
        var text = GetNavText(tag);
        btn.Content = isExpanded ? text : null;
        btn.ToolTip = isExpanded ? null : text;
        btn.Padding = isExpanded ? new Thickness(14, 10, 14, 10) : new Thickness(0, 10, 0, 10);
        btn.HorizontalContentAlignment = isExpanded ? HorizontalAlignment.Left : HorizontalAlignment.Center;
    }

    private static string GetNavText(string tag)
    {
        return tag switch
        {
            "dashboard" => LocalizationService.Get("nav.dashboard"),
            "companies" => LocalizationService.Get("nav.companies"),
            "sources"   => LocalizationService.Get("nav.sources"),
            "jobs"      => LocalizationService.Get("nav.jobs"),
            "raw"       => LocalizationService.Get("nav.raw_records"),
            "map"       => LocalizationService.Get("nav.map"),
            "extract"   => LocalizationService.Get("nav.extract"),
            "exports"   => LocalizationService.Get("nav.exports"),
            "extension" => LocalizationService.Get("nav.extension"),
            "settings"  => LocalizationService.Get("nav.settings"),
            _ => tag
        };
    }

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }

        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Window_StateChanged(object? sender, EventArgs e)
    {
        if (BtnMaximize != null)
        {
            BtnMaximize.Content = WindowState == WindowState.Maximized ? "❐" : "☐";
            BtnMaximize.ToolTip = WindowState == WindowState.Maximized ? "استعادة / Restore" : "تكبير / Maximize";
        }

        if (RootBorder != null)
        {
            RootBorder.CornerRadius = WindowState == WindowState.Maximized ? new CornerRadius(0) : new CornerRadius(16);
            RootBorder.BorderThickness = WindowState == WindowState.Maximized ? new Thickness(0) : new Thickness(1);
        }
    }

    private void BtnLang_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string lang)
        {
            AppSettings.Language = lang;
            if (AuthService.CurrentUser != null)
                AuthService.CurrentUser.Language = lang;
            LocalizationService.SetLanguage(lang);
            ApplyLanguage();
        }
    }

    private void ApplyLanguage()
    {
        FlowDirection = LocalizationService.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
        UpdateSidebarLayout();

        BtnSignOut.Content      = LocalizationService.Get("auth.logout");
        BtnCreateRecord.Content = LocalizationService.Get("btn.create_record");
        TxtGlobalSearch.Text    = LocalizationService.Get("search.placeholder");
        
        BtnNavDashboard.Content = LocalizationService.Get("nav.dashboard");
        BtnNavCompanies.Content = LocalizationService.Get("nav.companies");
        BtnNavSources.Content   = LocalizationService.Get("nav.sources");
        BtnNavJobs.Content      = LocalizationService.Get("nav.jobs");
        BtnNavRaw.Content       = LocalizationService.Get("nav.raw_records");
        BtnNavMap.Content         = LocalizationService.Get("nav.map");
        BtnNavDataExtract.Content = LocalizationService.Get("nav.extract");
        BtnNavExports.Content     = LocalizationService.Get("nav.exports");
        BtnNavExtension.Content = LocalizationService.Get("nav.extension");
        BtnNavSettings.Content  = LocalizationService.Get("nav.settings");
        
        var user = AuthService.CurrentUser;
        if (user != null)
        {
            if (user.RoleName.Contains("super", StringComparison.OrdinalIgnoreCase))
                TxtUserRole.Text = LocalizationService.Get("role.super_admin");
            else
                TxtUserRole.Text = LocalizationService.Get("role.admin");
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
