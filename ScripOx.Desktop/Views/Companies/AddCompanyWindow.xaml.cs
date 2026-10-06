using System;
using System.Windows;
using ScripOx.Desktop.Models;
using ScripOx.Desktop.Services;

namespace ScripOx.Desktop.Views.Companies;

public partial class AddCompanyWindow : Window
{
    private readonly CompanyService _companyService;

    public AddCompanyWindow()
    {
        InitializeComponent();
        _companyService = new CompanyService(AppSettings.ConnectionString);
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void BtnSave_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(TxtName.Text))
        {
            MessageBox.Show("Company Name is required.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        double? latVal = null;
        double? lngVal = null;

        if (!string.IsNullOrWhiteSpace(TxtLatitude.Text))
        {
            if (double.TryParse(TxtLatitude.Text, out var lat)) latVal = lat;
            else
            {
                MessageBox.Show("Latitude must be a valid number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(TxtLongitude.Text))
        {
            if (double.TryParse(TxtLongitude.Text, out var lng)) lngVal = lng;
            else
            {
                MessageBox.Show("Longitude must be a valid number.", "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
        }

        try
        {
            var company = new Company
            {
                Name = TxtName.Text.Trim(),
                Category = string.IsNullOrWhiteSpace(TxtCategory.Text) ? null : TxtCategory.Text.Trim(),
                Phone = string.IsNullOrWhiteSpace(TxtPhone.Text) ? null : TxtPhone.Text.Trim(),
                Email = string.IsNullOrWhiteSpace(TxtEmail.Text) ? null : TxtEmail.Text.Trim(),
                Website = string.IsNullOrWhiteSpace(TxtWebsite.Text) ? null : TxtWebsite.Text.Trim(),
                Address = string.IsNullOrWhiteSpace(TxtAddress.Text) ? null : TxtAddress.Text.Trim(),
                City = string.IsNullOrWhiteSpace(TxtCity.Text) ? null : TxtCity.Text.Trim(),
                Country = string.IsNullOrWhiteSpace(TxtCountry.Text) ? "UK" : TxtCountry.Text.Trim(),
                X = latVal,
                Y = lngVal,
                Priority = (CmbPriority.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "medium"
            };

            var currentUserId = AuthService.CurrentUser?.Id ?? 1;
            await _companyService.CreateAsync(company, currentUserId);

            DialogResult = true;
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Failed to save company: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
