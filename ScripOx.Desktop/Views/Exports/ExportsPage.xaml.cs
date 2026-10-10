using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;
using ScripOx.Desktop.Models;
using ScripOx.Desktop.Services;

namespace ScripOx.Desktop.Views.Exports;

public partial class ExportsPage : Page
{
    private readonly CompanyService _companyService;
    private readonly SourceService _sourceService;
    private readonly ExcelExportService _exportService;
    private readonly AudienceExportService _audienceExportService;
    private bool _isInitialized = false;

    // Cache of sources
    private List<Source> _sources = new();

    public ExportsPage()
    {
        InitializeComponent();
        _companyService = new CompanyService(AppSettings.ConnectionString);
        _sourceService = new SourceService(AppSettings.ConnectionString);
        _exportService = new ExcelExportService();
        _audienceExportService = new AudienceExportService();
        
        ApplyLanguage();
        LocalizationService.LanguageChanged += OnLanguageChanged;
        
        Loaded += ExportsPage_Loaded;
        Unloaded += (s, e) => LocalizationService.LanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, string e)
    {
        ApplyLanguage();
        if (_isInitialized)
        {
            _ = RefreshPreviewAsync();
        }
    }

    private void ApplyLanguage()
    {
        TxtPageHeaderTitle.Text = LocalizationService.Get("exports.page_title");
        TxtPageHeaderSub.Text = LocalizationService.Get("exports.page_sub");
        TxtWizardTitle.Text = LocalizationService.Get("exports.wizard_title");
        TxtWizardSub.Text = LocalizationService.Get("exports.wizard_sub");

        TxtStep1.Text = LocalizationService.Get("exports.step1");
        TxtStep2.Text = LocalizationService.Get("exports.step2");
        TxtStep3.Text = LocalizationService.Get("exports.step3");
        TxtStep4.Text = LocalizationService.Get("exports.step4");

        TxtLblStatus.Text = LocalizationService.Get("exports.lbl_status");
        ChkOnlyWithContact.Content = LocalizationService.Get("exports.chk_contact");
        TxtBtnCopyCsv.Text = LocalizationService.Get("exports.btn_copy_csv");

        TxtBackupTitle.Text = LocalizationService.Get("exports.backup_title");
        TxtBackupDesc.Text = LocalizationService.Get("exports.backup_desc");
        BtnBackup.Content = LocalizationService.Get("exports.btn_backup");

        TxtRefresh.Text = LocalizationService.Get("exports.refresh");
        TxtCopyCsv.Text = LocalizationService.Get("exports.copy_csv");
        TxtExportNow.Text = LocalizationService.Get("exports.export_now");

        TxtLblRecordsCount.Text = LocalizationService.Get("exports.records_count");
        TxtLblLtv.Text = LocalizationService.Get("exports.ltv");

        if (CmbPriority.Items.Count >= 4)
        {
            ((ComboBoxItem)CmbPriority.Items[0]).Content = LocalizationService.Get("exports.filter_all_priorities");
            ((ComboBoxItem)CmbPriority.Items[1]).Content = LocalizationService.Get("exports.priority_high");
            ((ComboBoxItem)CmbPriority.Items[2]).Content = LocalizationService.Get("exports.priority_medium");
            ((ComboBoxItem)CmbPriority.Items[3]).Content = LocalizationService.Get("exports.priority_low");
        }
        
        if (CmbExportFormat.Items.Count >= 4)
        {
            ((ComboBoxItem)CmbExportFormat.Items[0]).Content = LocalizationService.Get("exports.meta_combo");
            ((ComboBoxItem)CmbExportFormat.Items[1]).Content = LocalizationService.Get("exports.google_combo");
            ((ComboBoxItem)CmbExportFormat.Items[2]).Content = LocalizationService.Get("exports.mail_combo");
            ((ComboBoxItem)CmbExportFormat.Items[3]).Content = LocalizationService.Get("exports.excel_combo");
        }

        if (_isInitialized)
        {
            // Re-apply "All" items for dynamic dropdowns if they exist
            if (CmbGroup.Items.Count > 0 && CmbGroup.Items[0] is string)
            {
                var groupOptions = new List<string> { LocalizationService.Get("exports.filter_all_groups") };
                groupOptions.AddRange(_sources.Select(s => s.Name));
                var oldIdx = CmbGroup.SelectedIndex;
                CmbGroup.ItemsSource = groupOptions;
                CmbGroup.SelectedIndex = oldIdx >= 0 ? oldIdx : 0;
            }
        }
    }

    private async void ExportsPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            // 1. Load Groups / Sources
            _sources = await _sourceService.GetSourcesAsync();
            var groupOptions = new List<string> { LocalizationService.Get("exports.filter_all_groups") };
            groupOptions.AddRange(_sources.Select(s => s.Name));
            CmbGroup.ItemsSource = groupOptions;
            CmbGroup.SelectedIndex = 0;

            // 2. Load Categories
            var categories = await _companyService.GetCategoriesAsync();
            var categoryOptions = new List<string> { LocalizationService.Get("exports.filter_all_categories") };
            categoryOptions.AddRange(categories);
            CmbCategory.ItemsSource = categoryOptions;
            CmbCategory.SelectedIndex = 0;

            // 3. Load Statuses
            var statuses = await _companyService.GetStatusesAsync();
            var statusList = new List<CompanyStatus>
            {
                new() { Id = 0, Name = LocalizationService.Get("exports.filter_all_statuses"), NameAr = LocalizationService.Get("exports.filter_all_statuses") }
            };
            statusList.AddRange(statuses);
            CmbStatus.ItemsSource = statusList;
            CmbStatus.SelectedIndex = 0;

            _isInitialized = true;
            await RefreshPreviewAsync();
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "ExportsPage_Loaded failed");
        }
    }

    private async Task<List<Company>> GetFilteredCompaniesAsync()
    {
        var filter = new CompanyFilter();

        // 1. Group / Source filter
        if (CmbGroup.SelectedIndex > 0 && CmbGroup.SelectedItem is string selectedGroupName)
        {
            var matchedSource = _sources.FirstOrDefault(s => s.Name == selectedGroupName);
            if (matchedSource != null)
            {
                filter.SourceId = (int)matchedSource.Id;
            }
        }

        // 2. Category filter
        if (CmbCategory.SelectedIndex > 0 && CmbCategory.SelectedItem is string selectedCat)
        {
            filter.Category = selectedCat;
        }

        // 3. Priority filter
        if (CmbPriority.SelectedIndex > 0)
        {
            filter.Priority = CmbPriority.SelectedIndex switch
            {
                1 => "high",
                2 => "medium",
                3 => "low",
                _ => null
            };
        }

        // 4. Status filter
        if (CmbStatus.SelectedValue is int sid && sid > 0)
        {
            filter.StatusId = sid;
        }

        var (items, _) = await _companyService.GetPagedAsync(filter, 1, 10000);

        // Optional filter: only leads with contact info (email or phone)
        if (ChkOnlyWithContact.IsChecked == true)
        {
            items = items.Where(c => !string.IsNullOrWhiteSpace(c.Email) || !string.IsNullOrWhiteSpace(c.Phone)).ToList();
        }

        return items;
    }

    private async Task RefreshPreviewAsync()
    {
        if (!_isInitialized) return;

        try
        {
            var companies = await GetFilteredCompaniesAsync();
            int formatIndex = CmbExportFormat.SelectedIndex;

            switch (formatIndex)
            {
                case 0: // Meta Ads
                    SetupMetaColumns();
                    var metaRecords = companies.Select(AudienceExportService.MapToMetaRecord).ToList();
                    DgPreview.ItemsSource = metaRecords;
                    TxtRecordCount.Text = $"{metaRecords.Count} {LocalizationService.Get("exports.contact")}";
                    TxtTotalValue.Text = $"${metaRecords.Sum(r => r.Value):N0}";
                    TxtPreviewTitle.Text = LocalizationService.Get("exports.meta_title");
                    TxtBadgeTemplate.Text = "Meta Template (19 Cols)";
                    BadgeTemplateBorder.Background = new SolidColorBrush(Color.FromRgb(24, 119, 242));
                    TxtPreviewSubtitle.Text = LocalizationService.Get("exports.meta_sub");
                    TxtTemplateSpec.Text = LocalizationService.Get("exports.meta_spec");
                    TxtBtnExportLabel.Text = LocalizationService.Get("exports.meta_btn");
                    break;

                case 1: // Google Ads
                    SetupGoogleColumns();
                    var googleRecords = companies.Select(AudienceExportService.MapToGoogleRecord).ToList();
                    DgPreview.ItemsSource = googleRecords;
                    TxtRecordCount.Text = $"{googleRecords.Count} {LocalizationService.Get("exports.contact")}";
                    TxtTotalValue.Text = $"${googleRecords.Sum(r => r.Value):N0}";
                    TxtPreviewTitle.Text = LocalizationService.Get("exports.google_title");
                    TxtBadgeTemplate.Text = "Google Ads (4 Cols)";
                    BadgeTemplateBorder.Background = new SolidColorBrush(Color.FromRgb(234, 67, 53));
                    TxtPreviewSubtitle.Text = LocalizationService.Get("exports.google_sub");
                    TxtTemplateSpec.Text = LocalizationService.Get("exports.google_spec");
                    TxtBtnExportLabel.Text = LocalizationService.Get("exports.google_btn");
                    break;

                case 2: // Email Marketing
                    SetupMarketingColumns();
                    var mktRecords = companies.Select(AudienceExportService.MapToMarketingRecord).ToList();
                    DgPreview.ItemsSource = mktRecords;
                    TxtRecordCount.Text = $"{mktRecords.Count} {LocalizationService.Get("exports.contact")}";
                    TxtTotalValue.Text = $"{mktRecords.Count} {LocalizationService.Get("exports.subscriber")}";
                    TxtPreviewTitle.Text = LocalizationService.Get("exports.mail_title");
                    TxtBadgeTemplate.Text = "Email Marketing (8 Cols)";
                    BadgeTemplateBorder.Background = new SolidColorBrush(Color.FromRgb(124, 58, 237));
                    TxtPreviewSubtitle.Text = LocalizationService.Get("exports.mail_sub");
                    TxtTemplateSpec.Text = LocalizationService.Get("exports.mail_spec");
                    TxtBtnExportLabel.Text = LocalizationService.Get("exports.mail_btn");
                    break;

                case 3: // Excel Dossier
                    SetupExcelColumns();
                    var excelRecords = companies.Select(AudienceExportService.MapToExcelRecord).ToList();
                    DgPreview.ItemsSource = excelRecords;
                    TxtRecordCount.Text = $"{excelRecords.Count} {LocalizationService.Get("exports.company_count")}";
                    TxtTotalValue.Text = $"${excelRecords.Sum(r => r.Value):N0}";
                    TxtPreviewTitle.Text = LocalizationService.Get("exports.excel_title");
                    TxtBadgeTemplate.Text = "Excel Dossier (.xlsx)";
                    BadgeTemplateBorder.Background = new SolidColorBrush(Color.FromRgb(16, 149, 193));
                    TxtPreviewSubtitle.Text = LocalizationService.Get("exports.excel_sub");
                    TxtTemplateSpec.Text = LocalizationService.Get("exports.excel_spec");
                    TxtBtnExportLabel.Text = LocalizationService.Get("exports.excel_btn");
                    break;
            }
        }
        catch (Exception ex)
        {
            Serilog.Log.Error(ex, "Failed to refresh preview in ExportsPage");
        }
    }

    // ── Column Builders for Dynamic Matrix ────────────────────

    private void SetupMetaColumns()
    {
        DgPreview.Columns.Clear();
        DgPreview.Columns.Add(CreateTextColumn("uid", "Uid", 65));
        DgPreview.Columns.Add(CreateValueColumn("value ($)", "ValueDisplay", 95));
        DgPreview.Columns.Add(CreateTextColumn("fn", "Fn", 100));
        DgPreview.Columns.Add(CreateTextColumn("ln", "Ln", 100));
        DgPreview.Columns.Add(CreateTextColumn("email", "Email", 180));
        DgPreview.Columns.Add(CreateTextColumn("phone", "Phone", 130));
        DgPreview.Columns.Add(CreateTextColumn("phone (2)", "Phone2", 110));
        DgPreview.Columns.Add(CreateTextColumn("ct", "Ct", 100));
        DgPreview.Columns.Add(CreateTextColumn("st", "St", 80));
        DgPreview.Columns.Add(CreateTextColumn("country", "Country", 65));
        DgPreview.Columns.Add(CreateTextColumn("zip", "Zip", 75));
        DgPreview.Columns.Add(CreateTextColumn("madid", "Madid", 60));
        DgPreview.Columns.Add(CreateTextColumn("dob", "Dob", 55));
        DgPreview.Columns.Add(CreateTextColumn("doby", "Doby", 55));
        DgPreview.Columns.Add(CreateTextColumn("gen", "Gen", 50));
        DgPreview.Columns.Add(CreateTextColumn("age", "Age", 50));
    }

    private void SetupGoogleColumns()
    {
        DgPreview.Columns.Clear();
        DgPreview.Columns.Add(CreateTextColumn("Email", "Email", 220));
        DgPreview.Columns.Add(CreateTextColumn("Phone", "Phone", 160));
        DgPreview.Columns.Add(CreateTextColumn("First Name", "FirstName", 140));
        DgPreview.Columns.Add(CreateTextColumn("Country", "Country", 90));
        DgPreview.Columns.Add(CreateValueColumn("Value ($)", "ValueDisplay", 100));
    }

    private void SetupMarketingColumns()
    {
        DgPreview.Columns.Clear();
        DgPreview.Columns.Add(CreateTextColumn("Email Address", "Email", 220));
        DgPreview.Columns.Add(CreateTextColumn("First Name", "FirstName", 130));
        DgPreview.Columns.Add(CreateTextColumn("Company", "Company", 180));
        DgPreview.Columns.Add(CreateTextColumn("Phone Number", "Phone", 140));
        DgPreview.Columns.Add(CreateTextColumn("City", "City", 110));
        DgPreview.Columns.Add(CreateTextColumn("Country", "Country", 80));
        DgPreview.Columns.Add(CreateTextColumn("Segment", "Segment", 120));
        DgPreview.Columns.Add(CreateTextColumn("Status", "Status", 100));
    }

    private void SetupExcelColumns()
    {
        DgPreview.Columns.Clear();
        DgPreview.Columns.Add(CreateTextColumn("ID", "Id", 65));
        DgPreview.Columns.Add(CreateTextColumn("الشركة (Company)", "CompanyName", 180));
        DgPreview.Columns.Add(CreateTextColumn("الفئة (Category)", "Category", 130));
        DgPreview.Columns.Add(CreateTextColumn("الهاتف (Phone)", "Phone", 130));
        DgPreview.Columns.Add(CreateTextColumn("البريد الإلكتروني (Email)", "Email", 180));
        DgPreview.Columns.Add(CreateTextColumn("المدينة (City)", "City", 100));
        DgPreview.Columns.Add(CreateTextColumn("الدولة (Country)", "Country", 70));
        DgPreview.Columns.Add(CreateTextColumn("الأولوية (Priority)", "Priority", 95));
        DgPreview.Columns.Add(CreateTextColumn("الحالة (Status)", "Status", 95));
        DgPreview.Columns.Add(CreateValueColumn("القيمة (Value $)", "ValueDisplay", 100));
    }

    private static DataGridTextColumn CreateTextColumn(string header, string bindingPath, double width)
    {
        return new DataGridTextColumn
        {
            Header = header,
            Binding = new System.Windows.Data.Binding(bindingPath),
            Width = new DataGridLength(width)
        };
    }

    private static DataGridTemplateColumn CreateValueColumn(string header, string bindingPath, double width)
    {
        var col = new DataGridTemplateColumn
        {
            Header = header,
            Width = new DataGridLength(width)
        };

        var factory = new FrameworkElementFactory(typeof(Border));
        factory.SetValue(Border.BackgroundProperty, new SolidColorBrush(Color.FromArgb(25, 16, 185, 129)));
        factory.SetValue(Border.CornerRadiusProperty, new CornerRadius(6));
        factory.SetValue(Border.PaddingProperty, new Thickness(6, 2, 6, 2));
        factory.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));
        textFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding(bindingPath));
        textFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
        textFactory.SetValue(TextBlock.FontSizeProperty, 11.0);
        textFactory.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(16, 185, 129)));

        factory.AppendChild(textFactory);
        col.CellTemplate = new DataTemplate { VisualTree = factory };
        return col;
    }

    // ── Events ────────────────────────────────────────────────

    private async void FilterCriteria_Changed(object sender, RoutedEventArgs e)
    {
        if (_isInitialized)
        {
            await RefreshPreviewAsync();
        }
    }

    private async void CmbExportFormat_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isInitialized)
        {
            await RefreshPreviewAsync();
        }
    }

    private async void BtnRefresh_Click(object sender, RoutedEventArgs e)
    {
        await RefreshPreviewAsync();
    }

    private async void BtnExportActive_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var companies = await GetFilteredCompaniesAsync();
            if (companies.Count == 0)
            {
                MessageBox.Show("لا توجد بيانات مطابقة للفلاتر المحددة لتصديرها.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int formatIndex = CmbExportFormat.SelectedIndex;
            switch (formatIndex)
            {
                case 0: // Meta Ads
                {
                    var sfd = new SaveFileDialog
                    {
                        Filter = "Meta Ads CSV (*.csv)|*.csv",
                        FileName = $"Meta_Value_Audience_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                    };
                    if (sfd.ShowDialog() == true)
                    {
                        await _audienceExportService.ExportMetaAdsCsvAsync(companies, sfd.FileName);
                        MessageBox.Show($"تم تصدير {companies.Count} جهة اتصال متوافقة 100% مع Meta Ads بنجاح!", "اكتمل التصدير", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;
                }

                case 1: // Google Ads
                {
                    var sfd = new SaveFileDialog
                    {
                        Filter = "Google Ads CSV (*.csv)|*.csv",
                        FileName = $"Google_CustomerMatch_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                    };
                    if (sfd.ShowDialog() == true)
                    {
                        await _audienceExportService.ExportGoogleAdsCsvAsync(companies, sfd.FileName);
                        MessageBox.Show($"تم تصدير {companies.Count} جهة اتصال جاهزة لمنصة Google Ads!", "اكتمل التصدير", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;
                }

                case 2: // Email Marketing
                {
                    var sfd = new SaveFileDialog
                    {
                        Filter = "Marketing CSV (*.csv)|*.csv",
                        FileName = $"Email_Marketing_List_{DateTime.Now:yyyyMMdd_HHmm}.csv"
                    };
                    if (sfd.ShowDialog() == true)
                    {
                        await _audienceExportService.ExportMarketingListCsvAsync(companies, sfd.FileName);
                        MessageBox.Show($"تم تصدير {companies.Count} جهة اتصال جاهزة لـ Mailchimp / Brevo!", "اكتمل التصدير", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;
                }

                case 3: // Excel Dossier
                {
                    var sfd = new SaveFileDialog
                    {
                        Filter = "Excel Files (*.xlsx)|*.xlsx",
                        FileName = $"ScripOx_Export_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
                    };
                    if (sfd.ShowDialog() == true)
                    {
                        var options = new ExcelExportService.ExportOptions(
                            FilePath: sfd.FileName,
                            Language: LocalizationService.IsRtl ? "ar" : "en",
                            IncludeSummary: true
                        );
                        await _exportService.ExportAsync(companies, options);
                        MessageBox.Show($"تم تصدير ملف الإكسل التنفيذي لـ {companies.Count} شركة بنجاح!", "اكتمل التصدير", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                    break;
                }
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"فشل التصدير: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnCopyActive_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var companies = await GetFilteredCompaniesAsync();
            if (companies.Count == 0)
            {
                MessageBox.Show("لا توجد سجلات مطابقة لنسخها في الحافظة.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            int formatIndex = CmbExportFormat.SelectedIndex;
            string csv;

            switch (formatIndex)
            {
                case 1:
                    csv = AudienceExportService.GenerateGoogleAdsCsv(companies);
                    break;
                case 2:
                    csv = AudienceExportService.GenerateMarketingListCsv(companies);
                    break;
                case 3:
                case 0:
                default:
                    csv = AudienceExportService.GenerateMetaAdsCsv(companies);
                    break;
            }

            Clipboard.SetText(csv);
            MessageBox.Show($"تم نسخ {companies.Count} سطر بصيغة القالب المختار بنجاح إلى الحافظة!", "تم النسخ", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"فشل النسخ إلى الحافظة: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void BtnBackup_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "SQLite Database (*.db)|*.db",
            FileName = $"ScripOx_Backup_{DateTime.Now:yyyyMMdd_HHmm}.db"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var dbPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripox.db");
                if (File.Exists(dbPath))
                {
                    File.Copy(dbPath, sfd.FileName, true);
                    MessageBox.Show("تم إنشاء وتصدير النسخة الاحتياطية لقاعدة البيانات بنجاح!", "نجاح", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("لم يتم العثور على ملف قاعدة البيانات في المجلد الافتراضي.", "تنبيه", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"فشل إنشاء النسخة الاحتياطية: {ex.Message}", "خطأ", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
