using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using ScripOx.Desktop.Models;
using Serilog;

namespace ScripOx.Desktop.Services;

/// <summary>
/// Service for managing extraction jobs directly in the SQLite database.
/// Provides offline/native job execution, monitoring, and history tracking.
/// </summary>
public class ExtractionJobService
{
    private readonly string _connectionString;

    public ExtractionJobService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<ExtractionJob>> GetJobsAsync()
    {
        var list = new List<ExtractionJob>();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = """
                SELECT j.id, j.source_id, IFNULL(s.name, 'Contact & Web Crawler') AS source_name,
                       j.triggered_by, j.status, j.total_found, j.total_saved, j.total_dupes,
                       j.total_errors, j.progress_pct, j.started_at, j.ended_at, j.error_msg, j.created_at
                FROM extraction_jobs j
                LEFT JOIN sources s ON s.id = j.source_id
                ORDER BY j.id DESC
                LIMIT 200
            """;

            await using var cmd = new SqliteCommand(sql, conn);
            await using var r = await cmd.ExecuteReaderAsync();

            while (await r.ReadAsync())
            {
                list.Add(new ExtractionJob
                {
                    Id          = r.GetInt32(0),
                    SourceId    = r.GetInt32(1),
                    SourceName  = r.GetString(2),
                    TriggeredBy = r.GetInt32(3),
                    Status      = r.GetString(4),
                    TotalFound  = r.GetInt32(5),
                    TotalSaved  = r.GetInt32(6),
                    TotalDupes  = r.GetInt32(7),
                    TotalErrors = r.GetInt32(8),
                    ProgressPct = r.GetInt32(9),
                    StartedAt   = r.IsDBNull(10) ? null : SafeGetDateTime(r, 10),
                    EndedAt     = r.IsDBNull(11) ? null : SafeGetDateTime(r, 11),
                    ErrorMsg    = r.IsDBNull(12) ? null : r.GetString(12),
                    CreatedAt   = SafeGetDateTime(r, 13)
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load extraction jobs from database");
        }
        return list;
    }

    public async Task<int> CreateJobAsync(int sourceId, int userId, string paramsJson = "{}")
    {
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = """
                INSERT INTO extraction_jobs (source_id, triggered_by, status, params_json, started_at, progress_pct)
                VALUES (@src, @user, 'running', @params, CURRENT_TIMESTAMP, 0);
                SELECT last_insert_rowid();
            """;

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@src", sourceId);
            cmd.Parameters.AddWithValue("@user", userId);
            cmd.Parameters.AddWithValue("@params", paramsJson);

            var idObj = await cmd.ExecuteScalarAsync();
            return Convert.ToInt32(idObj);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to create extraction job in database");
            return 0;
        }
    }

    public async Task UpdateJobProgressAsync(int jobId, int progressPct, int totalFound, int totalSaved, int totalErrors = 0, string status = "running")
    {
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = """
                UPDATE extraction_jobs
                SET progress_pct = @pct,
                    total_found  = @found,
                    total_saved  = @saved,
                    total_errors = @err,
                    status       = @status
                WHERE id = @id
            """;

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@pct", progressPct);
            cmd.Parameters.AddWithValue("@found", totalFound);
            cmd.Parameters.AddWithValue("@saved", totalSaved);
            cmd.Parameters.AddWithValue("@err", totalErrors);
            cmd.Parameters.AddWithValue("@status", status);
            cmd.Parameters.AddWithValue("@id", jobId);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update job {JobId} progress", jobId);
        }
    }

    public async Task CompleteJobAsync(int jobId, int totalFound, int totalSaved, int totalErrors = 0, string? errorMsg = null)
    {
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = """
                UPDATE extraction_jobs
                SET progress_pct = 100,
                    total_found  = @found,
                    total_saved  = @saved,
                    total_errors = @err,
                    status       = @status,
                    error_msg    = @msg,
                    ended_at     = CURRENT_TIMESTAMP
                WHERE id = @id
            """;

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@found", totalFound);
            cmd.Parameters.AddWithValue("@saved", totalSaved);
            cmd.Parameters.AddWithValue("@err", totalErrors);
            cmd.Parameters.AddWithValue("@status", string.IsNullOrWhiteSpace(errorMsg) ? "completed" : "failed");
            cmd.Parameters.AddWithValue("@msg", (object?)errorMsg ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@id", jobId);

            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to complete extraction job {JobId}", jobId);
        }
    }

    public async Task<bool> CancelJobAsync(int jobId)
    {
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = """
                UPDATE extraction_jobs
                SET status   = 'cancelled',
                    ended_at = CURRENT_TIMESTAMP
                WHERE id = @id
            """;

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", jobId);
            int rows = await cmd.ExecuteNonQueryAsync();
            return rows > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to cancel extraction job {JobId}", jobId);
            return false;
        }
    }

    private static DateTime SafeGetDateTime(SqliteDataReader r, int ordinal)
    {
        try
        {
            var str = r.GetString(ordinal);
            return DateTime.TryParse(str, out var dt) ? dt : DateTime.UtcNow;
        }
        catch
        {
            return DateTime.UtcNow;
        }
    }
}
