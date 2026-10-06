using System.Windows;
using System.Windows.Input;
using ScripOx.Desktop.Services;
using ScripOx.Desktop.Views.Dashboard;
using Serilog;

namespace ScripOx.Desktop.Views.Auth;

public partial class LoginView : Window
{
    private readonly AuthService _auth;

    public LoginView()
    {
        InitializeComponent();
        _auth = new AuthService(AppSettings.ConnectionString);
    }

    private async void BtnLogin_Click(object sender, RoutedEventArgs e)
    {
        TxtError.Visibility = Visibility.Collapsed;
        BtnLogin.IsEnabled  = false;
        BtnLoginText.Text   = "Signing in…";

        var username = TxtUsername.Text.Trim();
        var password = TxtPassword.Password;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError("Please enter username and password.");
            return;
        }

        var user = await _auth.LoginAsync(username, password);

        if (user is null)
        {
            ShowError("Invalid username or password.");
            return;
        }

        // Apply language preference
        LocalizationService.SetLanguage(user.Language);

        // Open Dashboard
        var dashboard = new DashboardView();
        dashboard.Show();
        Close();
    }

    private void ShowError(string msg)
    {
        TxtError.Text       = msg;
        TxtError.Visibility = Visibility.Visible;
        BtnLogin.IsEnabled  = true;
        BtnLoginText.Text   = "Sign In";
    }

    private void BtnClose_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void Language_Click(object sender, RoutedEventArgs e)
    {
        var lang = RbAr.IsChecked == true ? "ar" : "en";
        LocalizationService.SetLanguage(lang);
        FlowDirection = lang == "ar"
            ? FlowDirection.RightToLeft
            : FlowDirection.LeftToRight;
    }
}
