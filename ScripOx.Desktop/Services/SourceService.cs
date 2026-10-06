using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using ScripOx.Desktop.Models;
using Serilog;

namespace ScripOx.Desktop.Services;

public class SourceService
{
    private readonly string _connectionString;

    public SourceService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<List<Source>> GetSourcesAsync()
    {
        var list = new List<Source>();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var sql = @"
                SELECT id, name, type, base_url, connector, config_json, is_active, created_by, created_at, updated_at
                FROM sources
                ORDER BY id DESC";

            await using var cmd = new SqliteCommand(sql, conn);
            await using var rdr = await cmd.ExecuteReaderAsync();

            while (await rdr.ReadAsync())
            {
                list.Add(new Source
                {
                    Id          = rdr.GetInt64(0),
                    Name        = rdr.GetString(1),
                    Type        = rdr.GetString(2),
                    BaseUrl     = rdr.IsDBNull(3) ? null : rdr.GetString(3),
                    Connector   = rdr.IsDBNull(4) ? null : rdr.GetString(4),
                    ConfigJson  = rdr.IsDBNull(5) ? null : rdr.GetString(5),
                    IsActive    = rdr.GetInt32(6) != 0,
                    CreatedBy   = rdr.IsDBNull(7) ? null : rdr.GetInt32(7),
                    CreatedAt   = rdr.GetDateTime(8),
                    UpdatedAt   = rdr.GetDateTime(9)
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to get sources from database");
        }
        return list;
    }

    public async Task<bool> DeleteSourceAsync(long id)
    {
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            // First delete related jobs and their raw records to maintain referential integrity
            var deleteRecordsSql = @"
                DELETE FROM raw_records 
                WHERE job_id IN (SELECT id FROM extraction_jobs WHERE source_id = @id)";
            await using var cmdRecs = new SqliteCommand(deleteRecordsSql, conn);
            cmdRecs.Parameters.AddWithValue("@id", id);
            await cmdRecs.ExecuteNonQueryAsync();

            var deleteJobsSql = "DELETE FROM extraction_jobs WHERE source_id = @id";
            await using var cmdJobs = new SqliteCommand(deleteJobsSql, conn);
            cmdJobs.Parameters.AddWithValue("@id", id);
            await cmdJobs.ExecuteNonQueryAsync();

            var sql = "DELETE FROM sources WHERE id = @id";
            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@id", id);
            return await cmd.ExecuteNonQueryAsync() > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to delete source script {SourceId}", id);
            return false;
        }
    }
}
