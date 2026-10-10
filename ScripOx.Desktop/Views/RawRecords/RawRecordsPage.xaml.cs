using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ScripOx.Desktop.Services;
using ScripOx.Desktop.Models;

namespace ScripOx.Desktop.Views.RawRecords;

public partial class RawRecordsPage : Page
{
    private readonly RawRecordService _rawRecordService;
    private int _currentPage = 1;
    private int _pageSize = 50;
    private int _totalItems = 0;

    public RawRecordsPage()
    {
        InitializeComponent();
        _rawRecordService = new RawRecordService(AppSettings.ConnectionString);
        Loaded += RawRecordsPage_Loaded;
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, string lang)
    {
        ApplyLanguage();
        _ = LoadDataAsync();
    }

    private async void RawRecordsPage_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyLanguage();
        await LoadDataAsync();
    }

    public void ApplyLanguage()
    {
        var lang = LocalizationService.CurrentLanguage;
        var isAr = lang == "ar";
        var isDe = lang == "de";

        FlowDirection = isAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        BtnFilter.Content = isAr ? "تصفية 🔍" : (isDe ? "Filtern 🔍" : "Filter 🔍");
        BtnResetFilter.Content = isAr ? "مسح الفلتر ✕" : (isDe ? "Zurücksetzen ✕" : "Clear Filter ✕");
        BtnToggleSelectAll.Content = _allSelected
            ? (isAr ? "إلغاء التحديد" : (isDe ? "Auswahl aufheben" : "Deselect All"))
            : (isAr ? "تحديد الكل" : (isDe ? "Alles auswählen" : "Select All"));
        BtnDeleteSelectedRaw.Content = isAr ? "🗑️ حذف المحدد" : (isDe ? "🗑️ Auswahl löschen" : "🗑️ Delete Selected");
        BtnClearAllRaw.Content = isAr ? "⚠️ تفريغ الجدول" : (isDe ? "⚠️ Tabelle leeren" : "⚠️ Wipe Table");

        LblSegment.Text = isAr ? "الشريحة:" : (isDe ? "Segment:" : "Segment:");
        LblRating.Text = isAr ? "التقييم:" : (isDe ? "Bewertung:" : "Rating:");
        LblStatus.Text = isAr ? "الحالة:" : (isDe ? "Status:" : "Status:");

        // CmbTargetSegment items
        if (CmbTargetSegment.Items.Count >= 7)
        {
            ((ComboBoxItem)CmbTargetSegment.Items[0]).Content = isAr ? "عميل مميز VIP" : (isDe ? "VIP-Kunde" : "VIP Client");
            ((ComboBoxItem)CmbTargetSegment.Items[1]).Content = isAr ? "مطاعم وكافيهات" : (isDe ? "Restaurants & Cafés" : "Restaurants & Cafes");
            ((ComboBoxItem)CmbTargetSegment.Items[2]).Content = isAr ? "شركات تسويق ودعاية" : (isDe ? "Marketing & Werbung" : "Marketing & Advertising");
            ((ComboBoxItem)CmbTargetSegment.Items[3]).Content = isAr ? "عقارات وتطوير" : (isDe ? "Immobilien & Bau" : "Real Estate & Dev");
            ((ComboBoxItem)CmbTargetSegment.Items[4]).Content = isAr ? "أطباء ومستشفيات" : (isDe ? "Ärzte & Kliniken" : "Doctors & Clinics");
            ((ComboBoxItem)CmbTargetSegment.Items[5]).Content = isAr ? "تجارة ومستوردين" : (isDe ? "Handel & Import" : "Trade & Importers");
            ((ComboBoxItem)CmbTargetSegment.Items[6]).Content = isAr ? "خدمات وأعمال" : (isDe ? "Dienstleistungen & B2B" : "Services & B2B");
        }

        // CmbTargetPriority items
        if (CmbTargetPriority.Items.Count >= 3)
        {
            ((ComboBoxItem)CmbTargetPriority.Items[0]).Content = isAr ? "★★★ أولوية عالية" : (isDe ? "★★★ Hohe Priorität" : "★★★ High Priority");
            ((ComboBoxItem)CmbTargetPriority.Items[1]).Content = isAr ? "★★ أولوية متوسطة" : (isDe ? "★★ Mittlere Priorität" : "★★ Medium Priority");
            ((ComboBoxItem)CmbTargetPriority.Items[2]).Content = isAr ? "★ أولوية منخفضة" : (isDe ? "★ Niedrige Priorität" : "★ Low Priority");
        }

        // CmbTargetStatus items
        if (CmbTargetStatus.Items.Count >= 4)
        {
            ((ComboBoxItem)CmbTargetStatus.Items[0]).Content = isAr ? "جديد (New)" : (isDe ? "Neu" : "New");
            ((ComboBoxItem)CmbTargetStatus.Items[1]).Content = isAr ? "مهتم (Interested)" : (isDe ? "Interessiert" : "Interested");
            ((ComboBoxItem)CmbTargetStatus.Items[2]).Content = isAr ? "متابعة (Follow-up)" : (isDe ? "Follow-up" : "Follow-up");
            ((ComboBoxItem)CmbTargetStatus.Items[3]).Content = isAr ? "تعاقد (Won)" : (isDe ? "Gewonnen" : "Won / Contract");
        }

        BtnClean.Content = isAr ? "نقل إلى الشركات المعتمدة CRM ➔" : (isDe ? "In verifiziertes CRM übertragen ➔" : "Transfer to Verified CRM ➔");

        // Columns
        if (DgRawRecords.Columns.Count >= 10)
        {
            DgRawRecords.Columns[1].Header = isAr ? "المعرف" : "ID";
            DgRawRecords.Columns[2].Header = isAr ? "اسم الشركة" : (isDe ? "Firmenname" : "Business Name");
            DgRawRecords.Columns[3].Header = isAr ? "التصنيف / الفئة" : (isDe ? "Kategorie / Segment" : "Category / Segment");
            DgRawRecords.Columns[4].Header = isAr ? "التقييم" : (isDe ? "Bewertung" : "Rating");
            DgRawRecords.Columns[5].Header = isAr ? "هاتف الاتصال" : (isDe ? "Telefon" : "Phone");
            DgRawRecords.Columns[6].Header = isAr ? "البريد الإلكتروني" : (isDe ? "E-Mail" : "Email");
            DgRawRecords.Columns[7].Header = isAr ? "الموقع الإلكتروني" : (isDe ? "Webseite" : "Website");
            DgRawRecords.Columns[8].Header = isAr ? "العنوان" : (isDe ? "Adresse" : "Address");
            DgRawRecords.Columns[9].Header = isAr ? "الحالة" : "Status";
        }

        BtnPrev.Content = isAr ? "‹ السابق" : (isDe ? "‹ Zurück" : "‹ Previous");
        BtnNext.Content = isAr ? "التالي ›" : (isDe ? "Weiter ›" : "Next ›");
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var (items, total) = await _rawRecordService.GetPagedAsync(TxtSearch.Text, _currentPage, _pageSize);
            _totalItems = total;

            DgRawRecords.ItemsSource = items;

            int totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalItems / _pageSize));
            var isAr = LocalizationService.CurrentLanguage == "ar";
            var isDe = LocalizationService.CurrentLanguage == "de";
            TxtPageInfo.Text = isAr
                ? $"صفحة {_currentPage} من {totalPages}"
                : (isDe ? $"Seite {_currentPage} von {totalPages}" : $"Page {_currentPage} of {totalPages}");

            BtnPrev.IsEnabled = _currentPage > 1;
            BtnNext.IsEnabled = _currentPage < totalPages;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error loading raw records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnFilter_Click(object sender, RoutedEventArgs e)
    {
        _currentPage = 1;
        await LoadDataAsync();
    }

    private async void BtnPrev_Click(object sender, RoutedEventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            await LoadDataAsync();
        }
    }

    private async void BtnNext_Click(object sender, RoutedEventArgs e)
    {
        int totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalItems / _pageSize));
        if (_currentPage < totalPages)
        {
            _currentPage++;
            await LoadDataAsync();
        }
    }

    private bool _allSelected = false;
    private void BtnToggleSelectAll_Click(object sender, RoutedEventArgs e)
    {
        var records = DgRawRecords.ItemsSource?.Cast<RawRecord>().ToList();
        if (records == null || records.Count == 0) return;

        _allSelected = !_allSelected;
        foreach (var rec in records)
        {
            rec.IsSelected = _allSelected;
        }

        DgRawRecords.Items.Refresh();
        var isAr = LocalizationService.CurrentLanguage == "ar";
        var isDe = LocalizationService.CurrentLanguage == "de";
        BtnToggleSelectAll.Content = _allSelected
            ? (isAr ? "إلغاء التحديد" : (isDe ? "Auswahl aufheben" : "Deselect All"))
            : (isAr ? "تحديد الكل" : (isDe ? "Alles auswählen" : "Select All"));
    }

    private void RowCheckBox_Click(object sender, RoutedEventArgs e)
    {
        // Toggle selected state
    }

    private async void BtnClean_Click(object sender, RoutedEventArgs e)
    {
        var selectedRecords = DgRawRecords.ItemsSource?.Cast<RawRecord>()
            .Where(r => r.IsSelected)
            .ToList();

        var isAr = LocalizationService.CurrentLanguage == "ar";
        var isDe = LocalizationService.CurrentLanguage == "de";

        if (selectedRecords == null || selectedRecords.Count == 0)
        {
            MessageBox.Show(
                isAr ? "يرجى تحديد سجل واحد أو أكثر من الجدول للتنظيف والنقل."
                     : (isDe ? "Bitte wählen Sie einen oder mehrere Rohdatensätze aus." : "Please select one or more raw records to clean and transfer."),
                "ScripOx", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var confirmMsg = isAr
            ? $"هل أنت متأكد من تنظيف ونقل {selectedRecords.Count} سجل محدد إلى قائمة الشركات المعتمدة (CRM)؟"
            : (isDe ? $"Möchten Sie wirklich {selectedRecords.Count} ausgewählte Datensätze ins CRM übertragen?"
                    : $"Are you sure you want to clean and transfer {selectedRecords.Count} selected record(s) to the CRM?");

        var confirmTitle = isAr ? "تأكيد النقل إلى CRM" : (isDe ? "Übertragung bestätigen" : "Confirm Transfer");
        var result = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (result != MessageBoxResult.Yes) return;

        try
        {
            var ids = selectedRecords.Select(r => r.Id).ToList();
            var currentUserId = AuthService.CurrentUser?.Id ?? 1;
            
            // Segment
            var segment = CmbTargetSegment.Text?.Trim();
            if (string.IsNullOrWhiteSpace(segment) && CmbTargetSegment.SelectedItem is ComboBoxItem itemSeg)
            {
                segment = itemSeg.Content?.ToString();
            }

            // Priority
            var priority = "medium";
            if (CmbTargetPriority.SelectedItem is ComboBoxItem itemPri && itemPri.Tag != null)
            {
                priority = itemPri.Tag.ToString() ?? "medium";
            }

            // Status
            int statusId = 1;
            if (CmbTargetStatus.SelectedItem is ComboBoxItem itemStat && itemStat.Tag != null && int.TryParse(itemStat.Tag.ToString(), out var parsedStat))
            {
                statusId = parsedStat;
            }

            await _rawRecordService.CleanAndTransferAsync(ids, currentUserId, segment, priority, statusId);

            var successMsg = isAr
                ? $"تم بنجاح تنظيف وتصنيف ونقل {ids.Count} سجل إلى الـ CRM تحت شريحة '{segment}' وبأولوية '{priority}'!"
                : (isDe ? $"{ids.Count} Datensätze erfolgreich ins CRM unter Segment '{segment}' übertragen!"
                        : $"Successfully cleaned, categorized, and transferred {ids.Count} records to the CRM under segment '{segment}' with priority '{priority}'!");

            MessageBox.Show(successMsg, "ScripOx CRM", MessageBoxButton.OK, MessageBoxImage.Information);
            _allSelected = false;
            BtnToggleSelectAll.Content = isAr ? "تحديد الكل" : (isDe ? "Alles auswählen" : "Select All");
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error transferring records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async void BtnResetFilter_Click(object sender, RoutedEventArgs e)
    {
        TxtSearch.Text = string.Empty;
        _currentPage = 1;
        await LoadDataAsync();
    }

    private async void BtnDeleteSelectedRaw_Click(object sender, RoutedEventArgs e)
    {
        var selectedRecords = DgRawRecords.ItemsSource?.Cast<RawRecord>()
            .Where(r => r.IsSelected)
            .Select(r => r.Id)
            .ToList();

        var isAr = LocalizationService.CurrentLanguage == "ar";
        var isDe = LocalizationService.CurrentLanguage == "de";

        if (selectedRecords == null || selectedRecords.Count == 0)
        {
            MessageBox.Show(
                isAr ? "يرجى تحديد سجل واحد على الأقل لحذفه." : (isDe ? "Bitte wählen Sie mindestens einen Datensatz aus." : "Please select at least one record to delete."),
                "ScripOx", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var confirmMsg = isAr
            ? $"هل أنت متأكد من حذف {selectedRecords.Count} سجل خام محدد نهائياً؟"
            : (isDe ? $"Möchten Sie {selectedRecords.Count} ausgewählte Rohdatensätze dauerhaft löschen?"
                    : $"Are you sure you want to permanently delete {selectedRecords.Count} selected raw record(s)?");

        var confirmTitle = isAr ? "تأكيد الحذف" : (isDe ? "Löschen bestätigen" : "Confirm Delete");
        var confirm = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            var count = await _rawRecordService.DeleteBatchAsync(selectedRecords);
            MessageBox.Show(
                isAr ? $"تم حذف {count} سجل بنجاح." : (isDe ? $"{count} Datensätze erfolgreich gelöscht." : $"Successfully deleted {count} records."),
                "ScripOx", MessageBoxButton.OK, MessageBoxImage.Information);
            _allSelected = false;
            BtnToggleSelectAll.Content = isAr ? "تحديد الكل" : (isDe ? "Alles auswählen" : "Select All");
            await LoadDataAsync();
        }
    }

    private async void BtnClearAllRaw_Click(object sender, RoutedEventArgs e)
    {
        var isAr = LocalizationService.CurrentLanguage == "ar";
        var isDe = LocalizationService.CurrentLanguage == "de";

        var confirmMsg = isAr
            ? "تحذير أمني: هل أنت متأكد تماماً من تفريغ ومسح جميع السجلات الخام بالكامل؟ لا يمكن التراجع عن هذا الإجراء."
            : (isDe ? "Sicherheitswarnung: Möchten Sie wirklich alle Rohdatensätze vollständig löschen? Diese Aktion kann nicht rückgängig gemacht werden."
                    : "Security Warning: Are you completely sure you want to wipe all raw records? This action cannot be undone.");

        var confirmTitle = isAr ? "تفريغ الجدول بالكامل" : (isDe ? "Tabelle leeren" : "Wipe Entire Table");
        var confirm = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Stop);

        if (confirm == MessageBoxResult.Yes)
        {
            var count = await _rawRecordService.ClearAllAsync();
            MessageBox.Show(
                isAr ? "تم تفريغ جدول البيانات الخام بنجاح." : (isDe ? "Die Rohdatentabelle wurde erfolgreich geleert." : "Raw records table wiped successfully."),
                "ScripOx", MessageBoxButton.OK, MessageBoxImage.Information);
            _allSelected = false;
            BtnToggleSelectAll.Content = isAr ? "تحديد الكل" : (isDe ? "Alles auswählen" : "Select All");
            _currentPage = 1;
            await LoadDataAsync();
        }
    }

    private async void BtnImportJson_Click(object sender, RoutedEventArgs e)
    {
        var isAr = LocalizationService.CurrentLanguage == "ar";
        var isDe = LocalizationService.CurrentLanguage == "de";

        var openFileDialog = new OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = isAr ? "اختر ملف JSON للاستيراد" : (isDe ? "Wählen Sie eine JSON-Datei zum Importieren aus" : "Select JSON file to import")
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var fileContent = await File.ReadAllTextAsync(openFileDialog.FileName);
                using var document = JsonDocument.Parse(fileContent);
                var root = document.RootElement;
                
                var jsonRecords = new List<string>();

                // Check if it's our exported format with a "records" array
                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("records", out var recordsElement) && recordsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var record in recordsElement.EnumerateArray())
                    {
                        jsonRecords.Add(record.GetRawText());
                    }
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    // Fallback to simple array of objects
                    foreach (var record in root.EnumerateArray())
                    {
                        jsonRecords.Add(record.GetRawText());
                    }
                }
                else
                {
                    MessageBox.Show(
                        isAr ? "صيغة الملف غير مدعومة. يجب أن يحتوي الملف على مصفوفة 'records'." : "Unsupported file format.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (jsonRecords.Count > 0)
                {
                    var currentUserId = AuthService.CurrentUser?.Id ?? 1;
                    var importedCount = await _rawRecordService.ImportFromJsonAsync(jsonRecords, currentUserId);
                    MessageBox.Show(
                        isAr ? $"تم استيراد {importedCount} سجل بنجاح." : $"Successfully imported {importedCount} records.",
                        "ScripOx", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    _currentPage = 1;
                    await LoadDataAsync();
                }
                else
                {
                    MessageBox.Show(
                        isAr ? "لم يتم العثور على سجلات للاستيراد." : "No records found to import.",
                        "ScripOx", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
