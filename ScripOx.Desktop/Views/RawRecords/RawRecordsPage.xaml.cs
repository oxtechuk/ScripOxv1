using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
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
    }

    private async void RawRecordsPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (LocalizationService.IsRtl)
        {
            BtnFilter.Content = "تصفية";
            BtnToggleSelectAll.Content = _allSelected ? "إلغاء التحديد" : "تحديد الكل";
            LblSegment.Text = "الشريحة:";
            LblRating.Text = "التقييم:";
            LblStatus.Text = "التصنيف:";
            BtnClean.Content = "تنظيف ونقل إلى الـ CRM";

            if (DgRawRecords.Columns.Count >= 10)
            {
                DgRawRecords.Columns[0].Header = "تحديد";
                DgRawRecords.Columns[1].Header = "المعرف";
                DgRawRecords.Columns[2].Header = "اسم النشاط";
                DgRawRecords.Columns[3].Header = "الشريحة والتصنيف";
                DgRawRecords.Columns[4].Header = "التقييم";
                DgRawRecords.Columns[5].Header = "الهاتف";
                DgRawRecords.Columns[6].Header = "البريد الإلكتروني";
                DgRawRecords.Columns[7].Header = "الموقع الإلكتروني";
                DgRawRecords.Columns[8].Header = "العنوان";
                DgRawRecords.Columns[9].Header = "الحالة";
            }
        }
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            var (items, total) = await _rawRecordService.GetPagedAsync(TxtSearch.Text, _currentPage, _pageSize);
            _totalItems = total;

            DgRawRecords.ItemsSource = items;

            int totalPages = Math.Max(1, (int)Math.Ceiling((double)_totalItems / _pageSize));
            TxtPageInfo.Text = $"Page {_currentPage} of {totalPages}";

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
        BtnToggleSelectAll.Content = _allSelected ? "Deselect All" : "Select All";
    }

    private async void BtnClean_Click(object sender, RoutedEventArgs e)
    {
        var selectedRecords = DgRawRecords.ItemsSource?.Cast<RawRecord>()
            .Where(r => r.IsSelected)
            .ToList();

        if (selectedRecords == null || selectedRecords.Count == 0)
        {
            MessageBox.Show("Please select one or more raw records to clean and transfer.", "No Records Selected", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var result = MessageBox.Show($"Are you sure you want to clean and transfer {selectedRecords.Count} selected record(s) to the Companies list?", "Confirm Transfer", MessageBoxButton.YesNo, MessageBoxImage.Question);
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

            MessageBox.Show($"Successfully cleaned, categorized, and transferred {ids.Count} records to the CRM under segment '{segment}' with priority '{priority}'!", "CRM Ingestion Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            _allSelected = false;
            BtnToggleSelectAll.Content = "Select All";
            await LoadDataAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Error transferring records: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
