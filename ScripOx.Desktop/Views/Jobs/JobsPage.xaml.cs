using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using ScripOx.Desktop.Models;
using ScripOx.Desktop.Services;
using Serilog;

namespace ScripOx.Desktop.Views.Jobs;

public class DiscoveredItemViewModel
{
    public string Name          { get; set; } = string.Empty;
    public string EmailsDisplay { get; set; } = string.Empty;
    public string PhonesDisplay { get; set; } = string.Empty;
    public string Url           { get; set; } = string.Empty;
}

public partial class JobsPage : Page
{
    private readonly CompanyService _companyService;
    private readonly ExtractionJobService _jobService;
    private readonly ContactCrawlerService _crawlerService;

    private readonly ObservableCollection<string> _logItems = [];
    private readonly ObservableCollection<DiscoveredItemViewModel> _discoveredItems = [];

    private CancellationTokenSource? _crawlerCts;
    private List<Company> _cachedTargets = [];

    public JobsPage()
    {
        InitializeComponent();

        _companyService = new CompanyService(AppSettings.ConnectionString);
        _jobService     = new ExtractionJobService(AppSettings.ConnectionString);
        _crawlerService = new ContactCrawlerService(_companyService, _jobService);

        LstLogs.ItemsSource      = _logItems;
        DgDiscovered.ItemsSource = _discoveredItems;

        _crawlerService.OnLog += CrawlerService_OnLog;
        _crawlerService.OnProgress += CrawlerService_OnProgress;
        _crawlerService.OnItemExtracted += CrawlerService_OnItemExtracted;

        Loaded += JobsPage_Loaded;
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, string lang)
    {
        ApplyLanguage();
        _ = LoadCategoriesAsync();
        _ = RefreshTargetCountAsync();
    }

    private async void JobsPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyLanguage();
        await LoadCategoriesAsync();
        await RefreshTargetCountAsync();
        await LoadJobsAsync();
    }

    public void ApplyLanguage()
    {
        var lang = LocalizationService.CurrentLanguage;
        var isAr = lang == "ar";
        var isDe = lang == "de";

        FlowDirection = isAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        TxtHeaderTitle.Text = isAr
            ? "مركز الاستخراج والزحف الذكي للمواقع (Web & Contact Crawler)"
            : (isDe ? "Web & Kontakt-Crawler (Extraktions-Pipelines)" : "Web & Contact Crawler (Extraction Pipelines & Jobs)");

        TxtHeaderSubtitle.Text = isAr
            ? "محرك متقدم لفحص مواقع الشركات، زيارة صفحات /contact والتواصل، واستخراج الإيميلات (@) وأرقام الهواتف وتحديث قاعدة البيانات فورياً"
            : (isDe ? "Erweiterte Crawler-Engine zum Durchsuchen von Websites und Extrahieren von Kontaktdaten in Echtzeit."
                    : "Advanced crawler engine to scan websites, probe /contact pages, extract emails (@) & phones, and update database in real-time.");

        BtnRefreshJobs.Content = isAr ? "تحديث السجل 🔄" : (isDe ? "Aktualisieren 🔄" : "Refresh Jobs 🔄");

        TxtConfigTitle.Text = isAr ? "إعدادات مصدر البيانات والزحف الذكي" : (isDe ? "Datenquellen & Crawler-Einstellungen" : "Data Source & Smart Crawler Settings");
        TxtAntiBotBadge.Text = isAr ? "🛡️ نظام التخفي وتفادي حظر الروبوتات مفعّل" : (isDe ? "🛡️ Stealth- & Anti-Bot-Schutz Aktiv" : "🛡️ Anti-Bot & Human Stealth Engine Active");

        LblSourceType.Text = isAr ? "مصدر الروابط والشركات" : (isDe ? "Quelle für Links & Firmen" : "Source of Links & Companies");
        if (CmbSourceType.Items.Count >= 3)
        {
            ((ComboBoxItem)CmbSourceType.Items[0]).Content = isAr ? "🌐 كل الشركات التي لها موقع" : (isDe ? "🌐 Alle Unternehmen mit Website" : "🌐 All Companies with Websites");
            ((ComboBoxItem)CmbSourceType.Items[1]).Content = isAr ? "📁 تصفية بمجموعة / تصنيف" : (isDe ? "📁 Nach Kategorie filtern" : "📁 Filter by Group / Category");
            ((ComboBoxItem)CmbSourceType.Items[2]).Content = isAr ? "🔗 روابط مخصصة" : (isDe ? "🔗 Benutzerdefinierte URLs" : "🔗 Custom URLs");
        }

        LblTargetCategory.Text = isAr ? "المجموعة / التصنيف المستهدف" : (isDe ? "Zielgruppe / Kategorie" : "Target Group / Category");
        LblCrawlMode.Text = isAr ? "نمط الزحف والحماية" : (isDe ? "Crawl- & Schutzmodus" : "Crawl & Protection Mode");

        if (CmbCrawlMode.Items.Count >= 2)
        {
            ((ComboBoxItem)CmbCrawlMode.Items[0]).Content = isAr ? "🛡️ وضع التخفي البشري (موصى به)" : (isDe ? "🛡️ Menschlicher Stealth-Modus" : "🛡️ Human Stealth Mode (Recommended)");
            ((ComboBoxItem)CmbCrawlMode.Items[1]).Content = isAr ? "⚡ وضع السرعة الفائقة" : (isDe ? "⚡ Turbo-Modus" : "⚡ High-Speed Turbo Mode");
        }

        ChkProbeContact.Content = isAr ? "فحص مسار /contact وصفحات التواصل تلقائياً" : (isDe ? "Kontaktseiten automatisch prüfen" : "Auto-probe /contact and contact pages");
        ChkExtractEmails.Content = isAr ? "استخراج الإيميلات (@)" : (isDe ? "E-Mails extrahieren (@)" : "Extract Emails (@)");
        ChkExtractPhones.Content = isAr ? "استخراج أرقام الهواتف" : (isDe ? "Telefonnummern extrahieren" : "Extract Phone Numbers");

        BtnStartCrawler.Content = isAr ? "▶ بدء الاستخراج والزحف الذكي" : (isDe ? "▶ Crawler Starten" : "▶ Start Smart Crawler");
        BtnStopCrawler.Content = isAr ? "⏹ إيقاف الزحف" : (isDe ? "⏹ Crawler Stoppen" : "⏹ Stop Crawler");

        TxtProgressTitle.Text = isAr ? "حالة الزحف المباشر ونسبة الإنجاز" : (isDe ? "Live-Crawl Status & Fortschritt" : "Live Crawl Status & Progress");
        if (_crawlerCts == null)
        {
            TxtStatusHint.Text = isAr ? "جاهز للبدء" : (isDe ? "Bereit" : "Ready to start");
        }

        LblMetricTotal.Text = isAr ? "🌐 إجمالي المواقع المستهدفة" : (isDe ? "🌐 Ziel-Websites Gesamt" : "🌐 Target Websites");
        LblMetricChecked.Text = isAr ? "⚡ تم فحصها" : (isDe ? "⚡ Geprüft" : "⚡ Processed");
        LblMetricEmails.Text = isAr ? "✉️ إيميلات مستخرجة (@)" : (isDe ? "✉️ Extrahierte E-Mails (@)" : "✉️ Emails Extracted (@)");
        LblMetricPhones.Text = isAr ? "📞 أرقام هواتف مستخرجة" : (isDe ? "📞 Extrahierte Telefone" : "📞 Phones Extracted");

        BtnClearLog.Content = isAr ? "مسح الشاشة 🧹" : (isDe ? "Konsole leeren 🧹" : "Clear Console 🧹");
        TxtDiscoveredTitle.Text = isAr ? "البيانات المكتشفة والمحفوظة فورياً" : (isDe ? "Entdeckte Kontakte" : "Discovered & Live-Saved Contacts");
        TxtDiscoveredBadge.Text = isAr ? "حفظ مباشر SQLite" : (isDe ? "Direkt SQLite" : "Direct SQLite Save");

        if (DgDiscovered.Columns.Count >= 4)
        {
            DgDiscovered.Columns[0].Header = isAr ? "الشركة" : (isDe ? "Unternehmen" : "Company");
            DgDiscovered.Columns[1].Header = isAr ? "البريد المستخرج" : (isDe ? "Extrahierte E-Mail" : "Extracted Email");
            DgDiscovered.Columns[2].Header = isAr ? "الهاتف المستخرج" : (isDe ? "Extrahiertes Telefon" : "Extracted Phone");
            DgDiscovered.Columns[3].Header = isAr ? "الموقع" : (isDe ? "Webseite" : "Website");
        }

        TxtJobsHistoryTitle.Text = isAr ? "سجل عمليات ومهام الاستخراج في النظام" : (isDe ? "Crawler-Job-Historie" : "Crawler Jobs & Extraction History");
        TxtJobsDbBadge.Text = isAr ? "قاعدة البيانات المحلية SQLite" : (isDe ? "Lokale SQLite DB" : "Local SQLite DB");
        TxtJobsAuditSubtitle.Text = isAr ? "سجل المراقبة والمتابعة المباشر" : (isDe ? "Live-Audit" : "Live Audit & History Log");

        if (DgJobs.Columns.Count >= 9)
        {
            DgJobs.Columns[0].Header = isAr ? "رقم المهمة" : "Job ID";
            DgJobs.Columns[1].Header = isAr ? "نوع المهمة / المصدر" : (isDe ? "Job-Typ / Quelle" : "Job Type / Source");
            DgJobs.Columns[2].Header = isAr ? "الحالة" : "Status";
            DgJobs.Columns[3].Header = isAr ? "نسبة الإنجاز" : (isDe ? "Fortschritt" : "Progress");
            DgJobs.Columns[4].Header = isAr ? "مستخرجة" : (isDe ? "Gefunden" : "Found");
            DgJobs.Columns[5].Header = isAr ? "محفوظة" : (isDe ? "Gespeichert" : "Saved");
            DgJobs.Columns[6].Header = isAr ? "أخطاء" : (isDe ? "Fehler" : "Errors");
            DgJobs.Columns[7].Header = isAr ? "وقت البدء" : (isDe ? "Startzeit" : "Started At");
            DgJobs.Columns[8].Header = isAr ? "إجراء" : (isDe ? "Aktion" : "Action");
        }
    }

    private async Task LoadCategoriesAsync()
    {
        try
        {
            var isAr = LocalizationService.CurrentLanguage == "ar";
            var isDe = LocalizationService.CurrentLanguage == "de";
            var allCat = isAr ? "الكل (جميع التصنيفات)" : (isDe ? "Alle Kategorien" : "All (All Categories)");

            var categories = await _companyService.GetCategoriesAsync();
            CmbTargetCategory.Items.Clear();
            CmbTargetCategory.Items.Add(allCat);
            foreach (var cat in categories)
            {
                CmbTargetCategory.Items.Add(cat);
            }
            CmbTargetCategory.SelectedIndex = 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load categories for crawler");
        }
    }

    private async Task RefreshTargetCountAsync()
    {
        try
        {
            if (CmbSourceType.SelectedIndex == 2)
            {
                var urls = (TxtCustomUrls?.Text ?? "")
                    .Split(new[] { '\r', '\n', ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(u => u.Trim())
                    .Where(u => !string.IsNullOrWhiteSpace(u))
                    .Distinct()
                    .ToList();

                _cachedTargets = urls.Select((u, idx) => new Company
                {
                    Id = 0,
                    Name = Uri.TryCreate(u, UriKind.Absolute, out var p) ? p.Host : $"Target #{idx + 1}",
                    Website = u
                }).ToList();
            }
            else
            {
                string? categoryFilter = null;
                if (CmbSourceType.SelectedIndex == 1 && CmbTargetCategory.SelectedItem is string selectedCat &&
                    selectedCat != "الكل (جميع التصنيفات)" && selectedCat != "All (All Categories)" && selectedCat != "Alle Kategorien")
                {
                    categoryFilter = selectedCat;
                }

                _cachedTargets = await _companyService.GetCompaniesWithWebsitesAsync(categoryFilter);
            }

            var isAr = LocalizationService.CurrentLanguage == "ar";
            var isDe = LocalizationService.CurrentLanguage == "de";

            TxtAvailableTargets.Text = isAr
                ? $"متاح {_cachedTargets.Count} شركة لها رابط موقع جاهز للفحص"
                : (isDe ? $"{_cachedTargets.Count} Ziel-Unternehmen mit Webseiten bereit"
                        : $"{_cachedTargets.Count} companies with websites ready to crawl");
            TxtMetricTotal.Text = _cachedTargets.Count.ToString();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to refresh target count");
            var isAr = LocalizationService.CurrentLanguage == "ar";
            TxtAvailableTargets.Text = isAr ? "تعذر حصر المواقع" : "Unable to count targets";
        }
    }

    private async Task LoadJobsAsync()
    {
        try
        {
            var jobs = await _jobService.GetJobsAsync();
            DgJobs.ItemsSource = jobs;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load jobs");
        }
    }

    private async void CmbSourceType_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (PnlCategory == null || PnlCustomUrls == null) return;
        PnlCategory.Visibility = CmbSourceType.SelectedIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
        PnlCustomUrls.Visibility = CmbSourceType.SelectedIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        await RefreshTargetCountAsync();
    }

    private async void CmbTargetCategory_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await RefreshTargetCountAsync();
    }

    private async void TxtCustomUrls_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (!IsLoaded) return;
        await RefreshTargetCountAsync();
    }

    private async void BtnStartCrawler_Click(object sender, RoutedEventArgs e)
    {
        var isAr = LocalizationService.CurrentLanguage == "ar";
        var isDe = LocalizationService.CurrentLanguage == "de";

        if (_crawlerService.IsRunning)
        {
            MessageBox.Show(
                isAr ? "عملية الزحف قيد التشغيل بالفعل." : (isDe ? "Crawler läuft bereits." : "Crawler is already running."),
                "ScripOx", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_cachedTargets.Count == 0)
        {
            await RefreshTargetCountAsync();
            if (_cachedTargets.Count == 0)
            {
                MessageBox.Show(
                    isAr ? "لا توجد شركات مسجلة بروابط مواقع في هذا التصنيف."
                         : (isDe ? "Keine Unternehmen mit Websites in dieser Kategorie gefunden."
                                 : "No companies found with website URLs in this category."),
                    "ScripOx", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        // Toggle UI
        BtnStartCrawler.IsEnabled = false;
        BtnStopCrawler.IsEnabled  = true;
        CmbSourceType.IsEnabled   = false;
        CmbTargetCategory.IsEnabled = false;

        _crawlerCts = new CancellationTokenSource();

        // Reset metrics
        TxtMetricChecked.Text = "0";
        TxtMetricEmails.Text  = "0";
        TxtMetricPhones.Text  = "0";
        TxtProgressPct.Text   = "0%";
        PbCrawlProgress.Value = 0;
        _discoveredItems.Clear();

        // Create job in database
        var userId = AuthService.CurrentUser?.Id ?? 1;
        var jobId = await _jobService.CreateJobAsync(
            sourceId: 3, // 3 = Web & Contact Crawler
            userId: userId,
            paramsJson: $"{{\"targets_count\": {_cachedTargets.Count}, \"probe_contact\": {ChkProbeContact.IsChecked == true}}}"
        );

        LogToConsole("🚀", isAr
            ? $"تم إنشاء مهمة استخراج برقم #{jobId} في قاعدة البيانات..."
            : $"Extraction job #{jobId} created in database...");
        LogToConsole("🌐", isAr
            ? $"بدء فحص {_cachedTargets.Count} موقع مع مسار /contact لاستخراج الإيميلات والأرقام..."
            : $"Probing {_cachedTargets.Count} targets with /contact subpath for emails & phones...");

        try
        {
            var probeContact = ChkProbeContact.IsChecked == true;
            var stealthMode  = CmbCrawlMode?.SelectedIndex != 1; // Default to true (stealth mode)
            TxtStatusHint.Text = isAr
                ? (stealthMode ? "جاري الزحف بنمط التخفي الذكي..." : "جاري الزحف بالوضع السريع...")
                : (stealthMode ? "Crawling in human stealth mode..." : "Crawling in high-speed turbo mode...");

            await _crawlerService.CrawlCompaniesAsync(
                companies: _cachedTargets,
                jobId: jobId,
                probeContactSubpath: probeContact,
                stealthMode: stealthMode,
                maxConcurrency: stealthMode ? 2 : 4,
                cancellationToken: _crawlerCts.Token
            );
        }
        catch (OperationCanceledException)
        {
            LogToConsole("⏹", isAr ? "تم إيقاف عملية الاستخراج." : "Crawler stopped by user.");
            TxtStatusHint.Text = isAr ? "تم إيقاف العملية" : "Stopped";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Crawler encountered an unhandled error");
            LogToConsole("❌", isAr ? $"حدث خطأ أثناء الاستخراج: {ex.Message}" : $"Crawler error: {ex.Message}");
            TxtStatusHint.Text = isAr ? "حدث خطأ" : "Error occurred";
        }
        finally
        {
            BtnStartCrawler.IsEnabled = true;
            BtnStopCrawler.IsEnabled  = false;
            CmbSourceType.IsEnabled   = true;
            CmbTargetCategory.IsEnabled = true;
            if (TxtStatusHint.Text.StartsWith("جاري") || TxtStatusHint.Text.StartsWith("Crawl"))
            {
                TxtStatusHint.Text = isAr ? "اكتمل الفحص" : "Finished";
            }

            await LoadJobsAsync();
        }
    }

    private void BtnStopCrawler_Click(object sender, RoutedEventArgs e)
    {
        if (_crawlerCts != null && !_crawlerCts.IsCancellationRequested)
        {
            _crawlerCts.Cancel();
            BtnStopCrawler.IsEnabled = false;
            var isAr = LocalizationService.CurrentLanguage == "ar";
            LogToConsole("⏳", isAr ? "جارٍ إيقاف العملية بطلب من المستخدم..." : "Stopping crawler requested by user...");
        }
    }

    private void CrawlerService_OnLog(CrawlerLogEntry entry)
    {
        Dispatcher.Invoke(() =>
        {
            _logItems.Add(entry.ToString());
            if (_logItems.Count > 400)
            {
                _logItems.RemoveAt(0);
            }
            if (LstLogs.Items.Count > 0)
            {
                LstLogs.ScrollIntoView(LstLogs.Items[^1]);
            }
        });
    }

    private void CrawlerService_OnProgress(CrawlerStats stats)
    {
        Dispatcher.Invoke(() =>
        {
            TxtMetricTotal.Text   = stats.TotalTargets.ToString();
            TxtMetricChecked.Text = stats.ProcessedCount.ToString();
            TxtMetricEmails.Text  = stats.EmailsFound.ToString();
            TxtMetricPhones.Text  = stats.PhonesFound.ToString();

            TxtProgressPct.Text   = $"{stats.ProgressPct}%";
            PbCrawlProgress.Value = stats.ProgressPct;
            var isAr = LocalizationService.CurrentLanguage == "ar";
            var isDe = LocalizationService.CurrentLanguage == "de";
            TxtStatusHint.Text = isAr
                ? $"تم فحص {stats.ProcessedCount} من أصل {stats.TotalTargets} موقع"
                : (isDe ? $"{stats.ProcessedCount} von {stats.TotalTargets} Websites geprüft"
                        : $"Processed {stats.ProcessedCount} of {stats.TotalTargets} sites");
        });
    }

    private void CrawlerService_OnItemExtracted(CrawlResult item)
    {
        if (!item.IsSuccess) return;

        Dispatcher.Invoke(() =>
        {
            _discoveredItems.Insert(0, new DiscoveredItemViewModel
            {
                Name          = item.Name,
                EmailsDisplay = item.Emails.Count > 0 ? string.Join(", ", item.Emails) : "-",
                PhonesDisplay = item.Phones.Count > 0 ? string.Join(", ", item.Phones) : "-",
                Url           = item.Url
            });

            if (_discoveredItems.Count > 150)
            {
                _discoveredItems.RemoveAt(_discoveredItems.Count - 1);
            }
        });
    }

    private void LogToConsole(string icon, string text)
    {
        CrawlerService_OnLog(new CrawlerLogEntry
        {
            Message = $"{icon} {text}"
        });
    }

    private void BtnClearLog_Click(object sender, RoutedEventArgs e)
    {
        _logItems.Clear();
    }

    private async void BtnRefreshJobs_Click(object sender, RoutedEventArgs e)
    {
        await LoadJobsAsync();
        await RefreshTargetCountAsync();
    }

    private async void BtnCancelJob_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int jobId)
        {
            var isAr = LocalizationService.CurrentLanguage == "ar";
            var isDe = LocalizationService.CurrentLanguage == "de";

            var msg = isAr
                ? $"هل أنت متأكد من إلغاء المهمة رقم #{jobId}؟"
                : (isDe ? $"Möchten Sie den Job #{jobId} wirklich abbrechen?" : $"Are you sure you want to cancel job #{jobId}?");
            var title = isAr ? "تأكيد الإلغاء" : (isDe ? "Abbrechen bestätigen" : "Confirm Cancel");

            var res = MessageBox.Show(msg, title, MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                await _jobService.CancelJobAsync(jobId);
                await LoadJobsAsync();
            }
        }
    }
}
