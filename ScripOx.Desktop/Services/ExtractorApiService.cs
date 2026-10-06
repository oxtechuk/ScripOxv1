using System.Net.Http;
using System.Net.Http.Json;
using ScripOx.Desktop.Models;
using Serilog;

namespace ScripOx.Desktop.Services;

/// <summary>
/// HTTP client service for communicating with the Python FastAPI extractor.
/// Base URL: http://127.0.0.1:8765/api
/// </summary>
public class ExtractorApiService
{
    private readonly HttpClient _http;

    public ExtractorApiService(string baseUrl = "http://127.0.0.1:8765")
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _http.DefaultRequestHeaders.Add("Accept", "application/json");
    }

    /// <summary>Check if the extractor service is reachable.</summary>
    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            var resp = await _http.GetAsync("/api/health");
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Start a new extraction job.</summary>
    public async Task<ExtractionJob?> StartJobAsync(int sourceId, int triggeredBy, Dictionary<string, object>? jobParams = null)
    {
        try
        {
            var payload = new
            {
                source_id    = sourceId,
                triggered_by = triggeredBy,
                @params      = jobParams ?? new Dictionary<string, object>()
            };

            var resp = await _http.PostAsJsonAsync("/api/jobs", payload);
            resp.EnsureSuccessStatusCode();

            var dto = await resp.Content.ReadFromJsonAsync<JobDto>();
            return dto?.ToModel();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "StartJob failed for source {SourceId}", sourceId);
            return null;
        }
    }

    /// <summary>Poll job status by ID.</summary>
    public async Task<ExtractionJob?> GetJobStatusAsync(int jobId)
    {
        try
        {
            var dto = await _http.GetFromJsonAsync<JobDto>($"/api/jobs/{jobId}");
            return dto?.ToModel();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "GetJobStatus failed for job {JobId}", jobId);
            return null;
        }
    }

    /// <summary>Cancel a running job.</summary>
    public async Task<bool> CancelJobAsync(int jobId)
    {
        try
        {
            var resp = await _http.DeleteAsync($"/api/jobs/{jobId}");
            return resp.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>List all jobs.</summary>
    public async Task<List<ExtractionJob>> GetJobsAsync(int page = 1, int pageSize = 50)
    {
        try
        {
            var dtos = await _http.GetFromJsonAsync<List<JobDto>>($"/api/jobs?page={page}&page_size={pageSize}");
            return dtos?.Select(d => d.ToModel()).ToList() ?? new List<ExtractionJob>();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "GetJobs failed");
            return new List<ExtractionJob>();
        }
    }

    // ── Internal DTO (matches FastAPI response shape) ────────

    private class JobDto
    {
        public int      id           { get; set; }
        public int      source_id    { get; set; }
        public string   status       { get; set; } = "pending";
        public int      total_found  { get; set; }
        public int      total_saved  { get; set; }
        public int      total_dupes  { get; set; }
        public int      total_errors { get; set; }
        public int      progress_pct { get; set; }
        public DateTime? started_at  { get; set; }
        public DateTime? ended_at    { get; set; }
        public string?  error_msg    { get; set; }
        public DateTime created_at  { get; set; }

        public ExtractionJob ToModel() => new()
        {
            Id          = id,
            SourceId    = source_id,
            Status      = status,
            TotalFound  = total_found,
            TotalSaved  = total_saved,
            TotalDupes  = total_dupes,
            TotalErrors = total_errors,
            ProgressPct = progress_pct,
            StartedAt   = started_at,
            EndedAt     = ended_at,
            ErrorMsg    = error_msg,
            CreatedAt   = created_at,
        };
    }
}
