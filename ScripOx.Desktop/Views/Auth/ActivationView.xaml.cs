using System;
using System.Windows;
using System.Windows.Input;
using ScripOx.Desktop.Services;

namespace ScripOx.Desktop.Views.Auth
{
    public partial class ActivationView : Window
    {
        public ActivationView()
        {
            InitializeComponent();
            TxtHardwareId.Text = HardwareInfoService.GetHardwareId();
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
                DragMove();
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private async void BtnActivate_Click(object sender, RoutedEventArgs e)
        {
            string license = TxtLicenseKey.Text.Trim();
            if (string.IsNullOrEmpty(license))
            {
                ShowError("Please enter a license key.");
                return;
            }

            TxtError.Visibility = Visibility.Collapsed;
            BtnActivate.IsEnabled = false;
            BtnActivate.Content = "Activating...";

            var result = await LicenseService.ActivateOnlineAsync(license);

            if (result.Success)
            {
                MessageBox.Show(result.Message, "Activation Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                // On success, go to login view or main view
                var loginView = new LoginView();
                loginView.Show();
                this.Close();
            }
            else
            {
                ShowError(result.Message);
                BtnActivate.IsEnabled = true;
                BtnActivate.Content = "Activate Now";
            }
        }

        private void ShowError(string message)
        {
            TxtError.Text = message;
            TxtError.Visibility = Visibility.Visible;
        }

        private void TxtHardwareId_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!string.IsNullOrEmpty(TxtHardwareId.Text))
            {
                Clipboard.SetText(TxtHardwareId.Text);
                MessageBox.Show("Hardware ID copied to clipboard!", "Copied", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LinkPurchase_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "http://oxtech.uk/p/scripox",
                UseShellExecute = true
            });
        }
    }
}
