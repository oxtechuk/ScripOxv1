using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    }

    private async void CompaniesPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (LocalizationService.IsRtl)
        {
            BtnFilter.Content = "تصفية";
            BtnAddCompany.Content = "+ إضافة شركة";
            BtnImportExcel.Content = "استيراد إكسيل";
            BtnDownloadExample.Content = "نموذج جاهز";

            if (DgCompanies.Columns.Count >= 8)
            {
                DgCompanies.Columns[0].Header = "المعرف";
                DgCompanies.Columns[1].Header = "اسم الشركة";
                DgCompanies.Columns[2].Header = "الشريحة والتصنيف";
                DgCompanies.Columns[3].Header = "التقييم والأولوية";
                DgCompanies.Columns[4].Header = "المدينة / المنطقة";
                DgCompanies.Columns[5].Header = "رقم الهاتف";
                DgCompanies.Columns[6].Header = "الحالة";
                DgCompanies.Columns[7].Header = "إجراء CRM";
            }
        }

        try
        {
            var statuses = await _companyService.GetStatusesAsync();
            var statusList = new List<CompanyStatus> { new CompanyStatus { Id = 0, Name = LocalizationService.IsRtl ? "جميع الحالات" : "All Statuses", NameAr = "جميع الحالات" } };
            statusList.AddRange(statuses);
            CmbStatus.ItemsSource = statusList;
            CmbStatus.SelectedIndex = 0;

            var categories = await _companyService.GetCategoriesAsync();
            var catList = new List<string> { LocalizationService.IsRtl ? "جميع الشرائح" : "All Segments" };
            catList.AddRange(categories);
            CmbSegment.ItemsSource = catList;
            CmbSegment.SelectedIndex = 0;
        }
        catch
        {
            // Ignore
        }

        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        string? priority = null;
        if (CmbPriority.SelectedItem is ComboBoxItem itemPri && itemPri.Tag != null)
        {
            priority = itemPri.Tag.ToString();
        }

        string? category = null;
        if (CmbSegment.SelectedItem is string cat && cat != "All Segments")
        {
            category = cat;
        }

        int? statusId = null;
        if (CmbStatus.SelectedValue is int sid && sid > 0)
        {
            statusId = sid;
        }

        var filter = new CompanyFilter
        {
            Search = TxtSearch.Text,
            StatusId = statusId,
            Category = category,
            Priority = priority
        };

        var (items, total) = await _companyService.GetPagedAsync(filter, _currentPage, _pageSize);
        _totalItems = total;
        
        DgCompanies.ItemsSource = items;

        int totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalItems / _pageSize));
        TxtPageInfo.Text = $"Page {_currentPage} of {totalPages}";

        BtnPrev.IsEnabled = _currentPage > 1;
        BtnNext.IsEnabled = _currentPage < totalPages;
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
            var detailsWindow = new CompanyDetailsWindow(selectedCompany.Id);
            detailsWindow.ShowDialog();
            _ = LoadDataAsync();
        }
    }

    private void BtnViewLead_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is Company comp)
        {
            var detailsWindow = new CompanyDetailsWindow(comp.Id);
            detailsWindow.ShowDialog();
            _ = LoadDataAsync();
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
}
