using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;
using ScripOx.Desktop.Services;
using ScripOx.Desktop.Models;

namespace ScripOx.Desktop.Views.Companies;

public partial class CompaniesPage : Page
{
    private readonly CompanyService _companyService;
    private int _currentPage = 1;
    private int _pageSize = 50;
    private int _totalItems = 0;

    public CompaniesPage()
    {
        InitializeComponent();
        _companyService = new CompanyService(AppSettings.ConnectionString);
        Loaded += CompaniesPage_Loaded;
        LocalizationService.LanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(object? sender, string lang)
    {
        ApplyLanguage();
        _ = RefreshCategoriesAsync();
        _ = LoadDataAsync();
    }

    public CompaniesPage(string initialSearch) : this()
    {
        TxtSearch.Text = initialSearch;
    }

    private List<Company> _currentCompanies = [];

    private async Task RefreshCategoriesAsync()
    {
        try
        {
            var isAr = LocalizationService.CurrentLanguage == "ar";
            var isDe = LocalizationService.CurrentLanguage == "de";
            var allCat = isAr ? "جميع المجموعات والتصنيفات" : (isDe ? "Alle Gruppen & Kategorien" : "All Groups & Categories");

            var categories = await _companyService.GetCategoriesAsync();
            var catList = new List<string> { allCat };
            catList.AddRange(categories);
            CmbSegment.ItemsSource = catList;
            if (CmbSegment.SelectedIndex < 0) CmbSegment.SelectedIndex = 0;

            CmbTargetGroup.ItemsSource = categories;
        }
        catch { }
    }

    private async void CompaniesPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            ApplyLanguage();

            var isAr = LocalizationService.CurrentLanguage == "ar";
            var isDe = LocalizationService.CurrentLanguage == "de";
            var allStat = isAr ? "جميع الحالات" : (isDe ? "Alle Status" : "All Statuses");

            var statuses = await _companyService.GetStatusesAsync();
            var statusList = new List<CompanyStatus> { new CompanyStatus { Id = 0, Name = allStat, NameAr = allStat } };
            statusList.AddRange(statuses);
            CmbStatus.ItemsSource = statusList;
            CmbStatus.SelectedIndex = 0;

            await RefreshCategoriesAsync();
        }
        catch
        {
            // Ignore
        }

        await LoadDataAsync();
    }

    public void ApplyLanguage()
    {
        var lang = LocalizationService.CurrentLanguage;
        var isAr = lang == "ar";
        var isDe = lang == "de";

        FlowDirection = isAr ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;

        // Top Search & Filter buttons
        BtnFilter.Content = isAr ? "تصفية 🔍" : (isDe ? "Filtern 🔍" : "Filter 🔍");
        BtnResetFilter.Content = isAr ? "مسح الفلتر ✕" : (isDe ? "Zurücksetzen ✕" : "Reset Filter ✕");
        BtnAddCompany.Content = isAr ? "+ إضافة شركة" : (isDe ? "+ Unternehmen" : "+ Add Company");
        BtnImportJson.Content = isAr ? "استيراد JSON 📥" : (isDe ? "JSON Import 📥" : "Import JSON 📥");
        BtnImportExcel.Content = isAr ? "استيراد إكسل" : (isDe ? "Excel Import" : "Import Excel");
        BtnDownloadExample.Content = isAr ? "نموذج الإكسل" : (isDe ? "Excel-Vorlage" : "Excel Template");

        // Priority Dropdown
        if (CmbPriority.Items.Count >= 4)
        {
            ((ComboBoxItem)CmbPriority.Items[0]).Content = isAr ? "جميع الأولويات" : (isDe ? "Alle Prioritäten" : "All Priorities");
            ((ComboBoxItem)CmbPriority.Items[1]).Content = isAr ? "★★★ أولوية عالية" : (isDe ? "★★★ Hohe Priorität" : "★★★ High Priority");
            ((ComboBoxItem)CmbPriority.Items[2]).Content = isAr ? "★★ أولوية متوسطة" : (isDe ? "★★ Mittlere Priorität" : "★★ Medium Priority");
            ((ComboBoxItem)CmbPriority.Items[3]).Content = isAr ? "★ أولوية منخفضة" : (isDe ? "★ Niedrige Priorität" : "★ Low Priority");
        }

        // Sort Dropdown
        if (CmbSort.Items.Count >= 5)
        {
            ((ComboBoxItem)CmbSort.Items[0]).Content = isAr ? "⏱️ تاريخ الإضافة (الأحدث)" : (isDe ? "⏱️ Erstellt (Neueste)" : "⏱️ Date Added (Newest)");
            ((ComboBoxItem)CmbSort.Items[1]).Content = isAr ? "⏱️ تاريخ الإضافة (الأقدم)" : (isDe ? "⏱️ Erstellt (Älteste)" : "⏱️ Date Added (Oldest)");
            ((ComboBoxItem)CmbSort.Items[2]).Content = isAr ? "🔄 آخر تحديث (الأحدث)" : (isDe ? "🔄 Aktualisiert (Neueste)" : "🔄 Last Updated (Newest)");
            ((ComboBoxItem)CmbSort.Items[3]).Content = isAr ? "🔄 آخر تحديث (الأقدم)" : (isDe ? "🔄 Aktualisiert (Älteste)" : "🔄 Last Updated (Oldest)");
            ((ComboBoxItem)CmbSort.Items[4]).Content = isAr ? "🔤 اسم الشركة (أ - ي)" : (isDe ? "🔤 Name (A - Z)" : "🔤 Company Name (A - Z)");
        }

        // Toolbar
        ChkSelectAllTop.Content = isAr ? "تحديد الكل" : (isDe ? "Alles auswählen" : "Select All");
        BtnClearSelection.Content = isAr ? "إلغاء التحديد" : (isDe ? "Auswahl aufheben" : "Clear Selection");
        BtnDeleteSelected.Content = isAr ? "🗑️ حذف المحدد" : (isDe ? "🗑️ Auswahl löschen" : "🗑️ Delete Selected");
        BtnClearAllTable.Content = isAr ? "⚠️ تفريغ جدول الشركات" : (isDe ? "⚠️ Tabelle leeren" : "⚠️ Wipe Table");
        if (LblTargetGroup != null)
            LblTargetGroup.Text = isAr ? "المجموعة المستهدفة:" : (isDe ? "Zielgruppe:" : "Target Group:");
        BtnAssignGroup.Content = isAr ? "📌 حفظ للمجموعة" : (isDe ? "📌 Gruppe zuweisen" : "📌 Assign Group");

        // DataGrid Columns
        if (DgCompanies.Columns.Count >= 11)
        {
            DgCompanies.Columns[1].Header = isAr ? "المعرف" : "ID";
            DgCompanies.Columns[2].Header = isAr ? "اسم الشركة" : (isDe ? "Firmenname" : "Company Name");
            DgCompanies.Columns[3].Header = isAr ? "المجموعة / التصنيف" : (isDe ? "Gruppe / Kategorie" : "Group / Category");
            DgCompanies.Columns[4].Header = isAr ? "الأولوية" : (isDe ? "Priorität" : "Priority");
            DgCompanies.Columns[5].Header = isAr ? "المدينة / المنطقة" : (isDe ? "Stadt / Region" : "City / Region");
            DgCompanies.Columns[6].Header = isAr ? "هاتف الاتصال" : (isDe ? "Telefon" : "Phone");
            DgCompanies.Columns[7].Header = isAr ? "الحالة" : "Status";
            DgCompanies.Columns[8].Header = isAr ? "تاريخ الإضافة" : (isDe ? "Erstellt am" : "Created At");
            DgCompanies.Columns[9].Header = isAr ? "آخر تحديث" : (isDe ? "Aktualisiert" : "Updated At");
            DgCompanies.Columns[10].Header = isAr ? "ملف العميل" : (isDe ? "Kundenprofil" : "Profile");
        }

        // Pagination buttons
        BtnPrev.Content = isAr ? "‹ السابق" : (isDe ? "‹ Zurück" : "‹ Previous");
        BtnNext.Content = isAr ? "التالي ›" : (isDe ? "Weiter ›" : "Next ›");

        UpdateSelectedCount();
    }

    private async Task LoadDataAsync()
    {
        string? priority = null;
        if (CmbPriority.SelectedItem is ComboBoxItem itemPri && itemPri.Tag != null)
        {
            priority = itemPri.Tag.ToString();
        }

        string? category = null;
        if (CmbSegment.SelectedItem is string cat &&
            cat != "All Segments" && cat != "All Groups" && cat != "All Groups & Categories" &&
            cat != "Alle Gruppen & Kategorien" && cat != "جميع المجموعات والتصنيفات" &&
            cat != "جميع المجموعات" && cat != "جميع الشرائح")
        {
            category = cat;
        }

        int? statusId = null;
        if (CmbStatus.SelectedValue is int sid && sid > 0)
        {
            statusId = sid;
        }

        string orderBy = "updated_at";
        bool orderDesc = true;
        if (CmbSort.SelectedItem is ComboBoxItem sortItem && sortItem.Tag is string sortTag)
        {
            switch (sortTag)
            {
                case "created_desc": orderBy = "created_at"; orderDesc = true; break;
                case "created_asc":  orderBy = "created_at"; orderDesc = false; break;
                case "updated_desc": orderBy = "updated_at"; orderDesc = true; break;
                case "updated_asc":  orderBy = "updated_at"; orderDesc = false; break;
                case "name_asc":     orderBy = "name";       orderDesc = false; break;
            }
        }

        var filter = new CompanyFilter
        {
            Search = TxtSearch.Text,
            StatusId = statusId,
            Category = category,
            Priority = priority,
            OrderBy = orderBy,
            OrderDesc = orderDesc
        };

        var (items, total) = await _companyService.GetPagedAsync(filter, _currentPage, _pageSize);
        _totalItems = total;
        _currentCompanies = items;
        
        DgCompanies.ItemsSource = null;
        DgCompanies.ItemsSource = _currentCompanies;
        UpdateSelectedCount();

        int totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalItems / _pageSize));
        var isAr = LocalizationService.CurrentLanguage == "ar";
        var isDe = LocalizationService.CurrentLanguage == "de";
        TxtPageInfo.Text = isAr
            ? $"صفحة {_currentPage} من {totalPages} (الإجمالي: {_totalItems})"
            : (isDe ? $"Seite {_currentPage} von {totalPages} (Gesamt: {_totalItems})"
                    : $"Page {_currentPage} of {totalPages} (Total: {_totalItems})");

        BtnPrev.IsEnabled = _currentPage > 1;
        BtnNext.IsEnabled = _currentPage < totalPages;
    }

    private void RowCheckBox_Click(object sender, RoutedEventArgs e)
    {
        UpdateSelectedCount();
    }

    private void ChkSelectAllTop_Click(object sender, RoutedEventArgs e)
    {
        bool isChecked = ChkSelectAllTop.IsChecked == true;
        foreach (var c in _currentCompanies)
        {
            c.IsSelected = isChecked;
        }
        DgCompanies.Items.Refresh();
        UpdateSelectedCount();
    }

    private void BtnClearSelection_Click(object sender, RoutedEventArgs e)
    {
        ChkSelectAllTop.IsChecked = false;
        foreach (var c in _currentCompanies)
        {
            c.IsSelected = false;
        }
        DgCompanies.Items.Refresh();
        UpdateSelectedCount();
    }

    private void UpdateSelectedCount()
    {
        int count = _currentCompanies.Count(c => c.IsSelected);
        var isAr = LocalizationService.CurrentLanguage == "ar";
        var isDe = LocalizationService.CurrentLanguage == "de";
        TxtSelectedCount.Text = isAr
            ? $"محدد: {count}"
            : (isDe ? $"Ausgewählt: {count}" : $"Selected: {count}");
    }

    private async void BtnAssignGroup_Click(object sender, RoutedEventArgs e)
    {
        var selected = _currentCompanies.Where(c => c.IsSelected).Select(c => c.Id).ToList();
        if (selected.Count == 0)
        {
            MessageBox.Show(LocalizationService.IsRtl ? "برجاء تحديد شركة واحدة على الأقل من الجدول." : "Please select at least one company from the table.", "ScripOx", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var groupName = CmbTargetGroup.Text?.Trim();
        if (string.IsNullOrWhiteSpace(groupName))
        {
            MessageBox.Show(LocalizationService.IsRtl ? "برجاء كتابة أو اختيار اسم المجموعة." : "Please enter or select a group name.", "ScripOx", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            int updated = await _companyService.UpdateCategoryBatchAsync(selected, groupName);
            MessageBox.Show(LocalizationService.IsRtl ? $"تم حفظ وتعيين {updated} شركات للمجموعة '{groupName}' بنجاح!" : $"Successfully assigned {updated} companies to group '{groupName}'!", "ScripOx CRM", MessageBoxButton.OK, MessageBoxImage.Information);
            await RefreshCategoriesAsync();
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to update group: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        _currentPage++;
        await LoadDataAsync();
    }

    private void DgCompanies_MouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DgCompanies.SelectedItem is Company selectedCompany)
        {
            try
            {
                var detailsWindow = new CompanyDetailsWindow(selectedCompany.Id)
                {
                    Owner = Window.GetWindow(this)
                };
                detailsWindow.ShowDialog();
                _ = LoadDataAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to open CompanyDetailsWindow from double click");
                MessageBox.Show($"تعذر فتح نافذة العميل: {ex.Message}", "ScripOx", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void BtnViewLead_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Company comp)
        {
            try
            {
                var detailsWindow = new CompanyDetailsWindow(comp.Id)
                {
                    Owner = Window.GetWindow(this)
                };
                detailsWindow.ShowDialog();
                _ = LoadDataAsync();
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Failed to open CompanyDetailsWindow for company {Id}", comp.Id);
                MessageBox.Show($"تعذر فتح نافذة العميل: {ex.Message}", "ScripOx", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }
    }

    private void BtnAddCompany_Click(object sender, RoutedEventArgs e)
    {
        var addWindow = new AddCompanyWindow();
        addWindow.Owner = Window.GetWindow(this);
        if (addWindow.ShowDialog() == true)
        {
            _currentPage = 1;
            _ = LoadDataAsync();
        }
    }

    private async void BtnImportExcel_Click(object sender, RoutedEventArgs e)
    {
        var ofd = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            Title = "Import Companies from Excel"
        };

        if (ofd.ShowDialog() == true)
        {
            try
            {
                var companies = ExcelImportService.ParseExcel(ofd.FileName);
                if (companies == null || companies.Count == 0)
                {
                    MessageBox.Show("No valid company records found in the Excel file.", "Import Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var result = MessageBox.Show($"Found {companies.Count} company record(s) to import. Do you want to proceed?", "Confirm Import", MessageBoxButton.YesNo, MessageBoxImage.Question);
                if (result != MessageBoxResult.Yes) return;

                var currentUserId = AuthService.CurrentUser?.Id ?? 1;
                int importedCount = 0;

                foreach (var company in companies)
                {
                    try
                    {
                        await _companyService.CreateAsync(company, currentUserId);
                        importedCount++;
                    }
                    catch (Exception ex)
                    {
                        Serilog.Log.Warning(ex, $"Failed to import row for company: {company.Name}");
                    }
                }

                MessageBox.Show($"Successfully imported {importedCount} out of {companies.Count} records!", "Import Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                _currentPage = 1;
                await LoadDataAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Excel import failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnImportJson_Click(object sender, RoutedEventArgs e)
    {
        var isAr = LocalizationService.CurrentLanguage == "ar";
        var isDe = LocalizationService.CurrentLanguage == "de";

        var openFileDialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            Title = isAr ? "اختر ملف JSON لاستيراد الشركات" : (isDe ? "Wählen Sie eine JSON-Datei" : "Select JSON file to import")
        };

        if (openFileDialog.ShowDialog() == true)
        {
            try
            {
                var fileContent = await File.ReadAllTextAsync(openFileDialog.FileName);
                using var document = JsonDocument.Parse(fileContent);
                var root = document.RootElement;
                
                var recordsToImport = new List<JsonElement>();

                if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("records", out var recordsElement) && recordsElement.ValueKind == JsonValueKind.Array)
                {
                    recordsToImport.AddRange(recordsElement.EnumerateArray());
                }
                else if (root.ValueKind == JsonValueKind.Array)
                {
                    recordsToImport.AddRange(root.EnumerateArray());
                }
                else
                {
                    MessageBox.Show(
                        isAr ? "صيغة الملف غير مدعومة." : "Unsupported file format.",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (recordsToImport.Count > 0)
                {
                    var result = MessageBox.Show(
                        isAr ? $"تم العثور على {recordsToImport.Count} شركة. هل ترغب في إضافتهم مباشرة إلى قائمة الشركات المعتمدة (CRM)؟"
                             : $"Found {recordsToImport.Count} companies. Import directly to CRM?", 
                        "Confirm Import", MessageBoxButton.YesNo, MessageBoxImage.Question);
                        
                    if (result != MessageBoxResult.Yes) return;

                    var currentUserId = AuthService.CurrentUser?.Id ?? 1;
                    int importedCount = 0;

                    foreach (var record in recordsToImport)
                    {
                        try
                        {
                            var rawJson = record.GetRawText();
                            // Create temporary RawRecord to reuse its parsing logic
                            var tempRaw = new RawRecord { RawJson = rawJson };

                            var name = tempRaw.Name;
                            if (string.IsNullOrWhiteSpace(name)) name = "Unknown Business";

                            double? latVal = null;
                            double? lngVal = null;
                            if (double.TryParse(tempRaw.Latitude, out var parsedLat)) latVal = parsedLat;
                            if (double.TryParse(tempRaw.Longitude, out var parsedLng)) lngVal = parsedLng;

                            var company = new Company
                            {
                                Name = name,
                                Category = string.IsNullOrWhiteSpace(tempRaw.Category) ? "General" : tempRaw.Category,
                                Phone = tempRaw.Phone,
                                Email = tempRaw.Email,
                                Website = tempRaw.Website,
                                Address = tempRaw.Address,
                                X = lngVal, // longitude
                                Y = latVal, // latitude
                                StatusId = 1, // New
                                Priority = "medium"
                            };

                            await _companyService.CreateAsync(company, currentUserId);
                            importedCount++;
                        }
                        catch (Exception ex)
                        {
                            Serilog.Log.Warning(ex, "Failed to import JSON row directly to companies.");
                        }
                    }

                    MessageBox.Show(
                        isAr ? $"تم استيراد {importedCount} شركة بنجاح." : $"Successfully imported {importedCount} companies.",
                        "ScripOx", MessageBoxButton.OK, MessageBoxImage.Information);
                    
                    _currentPage = 1;
                    await LoadDataAsync();
                }
                else
                {
                    MessageBox.Show(
                        isAr ? "لم يتم العثور على شركات للاستيراد." : "No companies found to import.",
                        "ScripOx", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error importing file: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private void BtnDownloadExample_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new Microsoft.Win32.SaveFileDialog
        {
            Filter = "Excel Files|*.xlsx",
            FileName = "ScripOx_Companies_Template.xlsx",
            Title = "Save Companies Excel Template"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                ExcelImportService.GenerateTemplate(sfd.FileName);
                MessageBox.Show("Template downloaded successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Template download failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnResetFilter_Click(object sender, RoutedEventArgs e)
    {
        TxtSearch.Text = string.Empty;
        if (CmbSegment.Items.Count > 0) CmbSegment.SelectedIndex = 0;
        if (CmbPriority.Items.Count > 0) CmbPriority.SelectedIndex = 0;
        if (CmbStatus.Items.Count > 0) CmbStatus.SelectedIndex = 0;
        if (CmbSort.Items.Count > 0) CmbSort.SelectedIndex = 0;
        _currentPage = 1;
        await LoadDataAsync();
    }

    private async void BtnDeleteSelected_Click(object sender, RoutedEventArgs e)
    {
        var selectedIds = _currentCompanies.Where(c => c.IsSelected).Select(c => c.Id).ToList();
        var isAr = LocalizationService.CurrentLanguage == "ar";
        var isDe = LocalizationService.CurrentLanguage == "de";

        if (selectedIds.Count == 0)
        {
            MessageBox.Show(
                isAr ? "يرجى تحديد شركة واحدة على الأقل لحذفها." : (isDe ? "Bitte wählen Sie mindestens ein Unternehmen zum Löschen aus." : "Please select at least one company to delete."),
                "ScripOx",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var confirmMsg = isAr
            ? $"هل أنت متأكد من حذف {selectedIds.Count} شركة محددة نهائياً من قاعدة البيانات؟"
            : (isDe ? $"Möchten Sie {selectedIds.Count} ausgewählte Unternehmen wirklich dauerhaft löschen?"
                    : $"Are you sure you want to permanently delete {selectedIds.Count} selected company(ies)?");

        var confirmTitle = isAr ? "تأكيد الحذف" : (isDe ? "Löschen bestätigen" : "Confirm Delete");

        var confirm = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Warning);

        if (confirm == MessageBoxResult.Yes)
        {
            var deleted = await _companyService.DeleteBatchAsync(selectedIds);
            MessageBox.Show(
                isAr ? $"تم حذف {deleted} شركة بنجاح." : (isDe ? $"{deleted} Unternehmen erfolgreich gelöscht." : $"Successfully deleted {deleted} companies."),
                "ScripOx",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            ChkSelectAllTop.IsChecked = false;
            UpdateSelectedCount();
            await LoadDataAsync();
        }
    }

    private async void BtnClearAllTable_Click(object sender, RoutedEventArgs e)
    {
        var isAr = LocalizationService.CurrentLanguage == "ar";
        var isDe = LocalizationService.CurrentLanguage == "de";

        var confirmMsg = isAr
            ? "تحذير أمني: هل أنت متأكد تماماً من تفريغ ومسح جميع الشركات المسجلة بالكامل؟ لا يمكن التراجع عن هذا الإجراء."
            : (isDe ? "Sicherheitswarnung: Möchten Sie wirklich alle registrierten Unternehmen vollständig löschen? Diese Aktion kann nicht rückgängig gemacht werden."
                    : "Security Warning: Are you completely sure you want to wipe all registered companies? This action cannot be undone.");

        var confirmTitle = isAr ? "تفريغ الجدول بالكامل" : (isDe ? "Tabelle leeren" : "Wipe Entire Table");

        var confirm = MessageBox.Show(confirmMsg, confirmTitle, MessageBoxButton.YesNo, MessageBoxImage.Stop);

        if (confirm == MessageBoxResult.Yes)
        {
            var count = await _companyService.ClearAllAsync();
            MessageBox.Show(
                isAr ? "تم تفريغ جدول الشركات بنجاح." : (isDe ? "Die Unternehmenstabelle wurde erfolgreich geleert." : "Companies table wiped successfully."),
                "ScripOx",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            ChkSelectAllTop.IsChecked = false;
            UpdateSelectedCount();
            _currentPage = 1;
            await LoadDataAsync();
        }
    }
}
