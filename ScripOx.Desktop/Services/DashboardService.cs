using Microsoft.Data.Sqlite;
using ScripOx.Desktop.Models;
using Serilog;

namespace ScripOx.Desktop.Services;

public class DashboardStats
{
    public int TotalCompanies { get; set; }
    public int TotalSources { get; set; }
    public int CompletedJobs { get; set; }
    public int RawRecordsCount { get; set; }
    public List<CompanyStatusCount> StatusCounts { get; set; } = new();
}

public class CompanyStatusCount
{
    public string StatusName { get; set; } = "";
    public string StatusColor { get; set; } = "";
    public int Count { get; set; }
}

public class DashboardService
{
    private readonly string _connectionString;

    public DashboardService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<DashboardStats> GetStatsAsync()
    {
        var stats = new DashboardStats();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            // Total Companies
            await using var cmdCompanies = new SqliteCommand("SELECT COUNT(*) FROM companies", conn);
            stats.TotalCompanies = Convert.ToInt32(await cmdCompanies.ExecuteScalarAsync());

            // Total Sources
            await using var cmdSources = new SqliteCommand("SELECT COUNT(*) FROM sources", conn);
            stats.TotalSources = Convert.ToInt32(await cmdSources.ExecuteScalarAsync());

            // Completed Jobs
            await using var cmdJobs = new SqliteCommand("SELECT COUNT(*) FROM extraction_jobs WHERE status = 'completed'", conn);
            stats.CompletedJobs = Convert.ToInt32(await cmdJobs.ExecuteScalarAsync());

            // Raw Records
            await using var cmdRaw = new SqliteCommand("SELECT COUNT(*) FROM raw_records", conn);
            stats.RawRecordsCount = Convert.ToInt32(await cmdRaw.ExecuteScalarAsync());

            // Status Counts
            await using var cmdStatus = new SqliteCommand(@"
                SELECT s.name, s.color, COUNT(c.id) as total
                FROM company_statuses s
                LEFT JOIN companies c ON c.status_id = s.id
                GROUP BY s.id, s.name, s.color
            ", conn);
            await using var reader = await cmdStatus.ExecuteReaderAsync() as SqliteDataReader;
            if (reader != null)
            {
                while (await reader.ReadAsync())
                {
                    stats.StatusCounts.Add(new CompanyStatusCount
                    {
                        StatusName = reader.GetString(reader.GetOrdinal("name")),
                        StatusColor = reader.IsDBNull(reader.GetOrdinal("color")) ? "#808080" : reader.GetString(reader.GetOrdinal("color")),
                        Count = reader.GetInt32(reader.GetOrdinal("total"))
                    });
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get dashboard stats");
        }

        return stats;
    }
}
