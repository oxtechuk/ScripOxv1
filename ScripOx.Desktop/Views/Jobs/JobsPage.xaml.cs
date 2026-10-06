using System.Windows;
using System.Windows.Controls;
using ScripOx.Desktop.Services;
using ScripOx.Desktop.Models;

namespace ScripOx.Desktop.Views.Jobs;

public partial class JobsPage : Page
{
    private readonly ExtractorApiService _apiService;

    public JobsPage()
    {
        InitializeComponent();
        _apiService = new ExtractorApiService(AppSettings.ExtractorApiBaseUrl);
        Loaded += JobsPage_Loaded;
    }

    private async void JobsPage_Loaded(object sender, RoutedEventArgs e)
    {
        await LoadJobsAsync();
    }

    private async Task LoadJobsAsync()
    {
        var jobs = await _apiService.GetJobsAsync();
        DgJobs.ItemsSource = jobs;
    }

    private async void BtnStartJob_Click(object sender, RoutedEventArgs e)
    {
        if (AuthService.CurrentUser == null) return;

        var sourceId = CmbSource.SelectedIndex == 0 ? 1 : 2; // 1 = Google Maps, 2 = CSV
        var prms = new Dictionary<string, object>();

        if (sourceId == 1)
        {
            if (string.IsNullOrWhiteSpace(TxtKeyword.Text) || string.IsNullOrWhiteSpace(TxtLocation.Text))
            {
                MessageBox.Show("Keyword and Location are required for Google Maps extraction.");
                return;
            }
            prms.Add("keyword", TxtKeyword.Text);
            prms.Add("location", TxtLocation.Text);
            prms.Add("limit", 60);
        }

        var job = await _apiService.StartJobAsync(sourceId, AuthService.CurrentUser.Id, prms);
        if (job != null)
        {
            MessageBox.Show($"Job {job.Id} started successfully!");
            await LoadJobsAsync();
        }
        else
        {
            MessageBox.Show("Failed to start job. Ensure the Python backend is running.");
        }
    }

    private async void BtnCancelJob_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is int jobId)
        {
            var success = await _apiService.CancelJobAsync(jobId);
            if (success)
            {
                MessageBox.Show("Job cancelled.");
                await LoadJobsAsync();
            }
            else
            {
                MessageBox.Show("Failed to cancel job.");
            }
        }
    }
}
