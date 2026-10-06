using System.IO;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ScripOx.Desktop.Models;
using ScripOx.Desktop.Services;

namespace ScripOx.Desktop.Views.Exports;

public partial class ExportsPage : Page
{
    private readonly CompanyService _companyService;
    private readonly ExcelExportService _exportService;
    private readonly AudienceExportService _audienceExportService;

    public ExportsPage()
    {
        InitializeComponent();
        _companyService = new CompanyService(AppSettings.ConnectionString);
        _exportService = new ExcelExportService();
        _audienceExportService = new AudienceExportService();
        Loaded += ExportsPage_Loaded;
    }

    private async void ExportsPage_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            var statuses = await _companyService.GetStatusesAsync();
            CmbStatus.ItemsSource = statuses;
        }
        catch
        {
            // Ignore
        }
    }

    private async Task<List<Company>> GetTargetCompaniesAsync(bool audienceMode)
    {
        var filter = new CompanyFilter();
        if (audienceMode && ChkQualifiedOnly.IsChecked == true)
        {
            // Fetch records, then filter in-memory for Interested (2) or Deal Won (4)
            var (allItems, _) = await _companyService.GetPagedAsync(filter, 1, 10000);
            return allItems.Where(c => c.StatusId == 2 || c.StatusId == 4).ToList();
        }
        else if (CmbStatus.SelectedValue is int sid)
        {
            filter.StatusId = sid;
        }

        var (items, _) = await _companyService.GetPagedAsync(filter, 1, 10000);
        return items;
    }

    private async void BtnExportMeta_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Meta Ads CSV (*.csv)|*.csv",
            FileName = $"Meta_Audience_ScripOx_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var companies = await GetTargetCompaniesAsync(audienceMode: true);
                if (companies.Count == 0)
                {
                    MessageBox.Show("No matching leads found for Meta Ads audience export.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                await _audienceExportService.ExportMetaAdsCsvAsync(companies, sfd.FileName);
                MessageBox.Show($"Exported {companies.Count} leads formatted for Meta Ads Manager upload!", "Meta Audience Export Ready", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnExportGoogle_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Google Ads CSV (*.csv)|*.csv",
            FileName = $"Google_CustomerMatch_ScripOx_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var companies = await GetTargetCompaniesAsync(audienceMode: true);
                if (companies.Count == 0)
                {
                    MessageBox.Show("No matching leads found for Google Ads Customer Match.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                await _audienceExportService.ExportGoogleAdsCsvAsync(companies, sfd.FileName);
                MessageBox.Show($"Exported {companies.Count} leads formatted for Google Ads Customer Match!", "Google Audience Export Ready", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnExportMarketing_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Marketing CSV (*.csv)|*.csv",
            FileName = $"Marketing_Leads_ScripOx_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var companies = await GetTargetCompaniesAsync(audienceMode: true);
                if (companies.Count == 0)
                {
                    MessageBox.Show("No matching leads found for Email Marketing export.", "No Data", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                await _audienceExportService.ExportMarketingListCsvAsync(companies, sfd.FileName);
                MessageBox.Show($"Exported {companies.Count} segmented contacts for Mailchimp / Brevo / CRM!", "Marketing List Export Ready", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }

    private async void BtnExport_Click(object sender, RoutedEventArgs e)
    {
        var sfd = new SaveFileDialog
        {
            Filter = "Excel Files (*.xlsx)|*.xlsx",
            FileName = $"ScripOx_Export_{DateTime.Now:yyyyMMdd_HHmm}.xlsx"
        };

        if (sfd.ShowDialog() == true)
        {
            try
            {
                var companies = await GetTargetCompaniesAsync(audienceMode: false);
                var lang = CmbLang.SelectedIndex == 1 ? "ar" : "en";
                var options = new ExcelExportService.ExportOptions(
                    FilePath: sfd.FileName,
                    Language: lang,
                    IncludeSummary: ChkSummary.IsChecked ?? true
                );

                await _exportService.ExportAsync(companies, options);
                MessageBox.Show($"Excel dossier with {companies.Count} companies exported successfully!", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
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
                    MessageBox.Show("Database archive backup completed successfully!", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Database file not found in application directory.", "Notice", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Backup failed: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
