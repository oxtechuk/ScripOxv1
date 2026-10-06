using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using ScripOx.Desktop.Models;
using Serilog;

namespace ScripOx.Desktop.Services;

public class RawRecordService
{
    private readonly string _connectionString;

    public RawRecordService(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<(List<RawRecord> Items, int Total)> GetPagedAsync(
        string search = "",
        int page = 1,
        int pageSize = 50)
    {
        var conditions = new List<string> { "1=1" };
        var parameters = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(search))
        {
            conditions.Add("raw_json LIKE @search");
            parameters["@search"] = $"%{search.Trim()}%";
        }

        var where = string.Join(" AND ", conditions);
        var offset = (page - 1) * pageSize;

        var sql = $"""
            SELECT * FROM raw_records
            WHERE {where}
            ORDER BY id DESC
            LIMIT @limit OFFSET @offset
        """;

        var countSql = $"SELECT COUNT(*) FROM raw_records WHERE {where}";

        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            // Count
            await using var countCmd = new SqliteCommand(countSql, conn);
            foreach (var (k, v) in parameters) countCmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
            var total = Convert.ToInt32(await countCmd.ExecuteScalarAsync());

            // Data
            await using var cmd = new SqliteCommand(sql, conn);
            foreach (var (k, v) in parameters) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@limit", pageSize);
            cmd.Parameters.AddWithValue("@offset", offset);

            var items = new List<RawRecord>();
            await using var reader = await cmd.ExecuteReaderAsync() as SqliteDataReader;
            if (reader != null)
            {
                while (await reader.ReadAsync())
                {
                    items.Add(new RawRecord
                    {
                        Id = reader.GetInt64(reader.GetOrdinal("id")),
                        JobId = reader.GetInt64(reader.GetOrdinal("job_id")),
                        RawJson = reader.GetString(reader.GetOrdinal("raw_json")),
                        Status = reader.GetString(reader.GetOrdinal("status")),
                        ReviewedBy = reader.IsDBNull(reader.GetOrdinal("reviewed_by")) ? null : reader.GetInt32(reader.GetOrdinal("reviewed_by")),
                        ReviewedAt = reader.IsDBNull(reader.GetOrdinal("reviewed_at")) ? null : reader.GetDateTime(reader.GetOrdinal("reviewed_at")),
                        RejectReason = reader.IsDBNull(reader.GetOrdinal("reject_reason")) ? null : reader.GetString(reader.GetOrdinal("reject_reason")),
                        CreatedAt = reader.GetDateTime(reader.GetOrdinal("created_at")),
                    });
                }
            }

            return (items, total);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetPagedAsync failed for RawRecords");
            throw;
        }
    }

    public async Task CleanAndTransferAsync(
        List<long> recordIds, 
        int userId, 
        string? customSegment = null,
        string priority = "medium",
        int statusId = 1)
    {
        if (recordIds == null || recordIds.Count == 0) return;

        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            await using var transaction = conn.BeginTransaction();

            foreach (var recordId in recordIds)
            {
                // 1. Fetch raw record
                var rawRecord = await GetRawRecordByIdAsync(recordId, conn, transaction);
                if (rawRecord == null) continue;

                // 2. Parse values from model properties
                var name = rawRecord.Name;
                if (string.IsNullOrWhiteSpace(name)) name = "Unknown Business"; // Avoid empty name crash

                var defaultCat = string.IsNullOrWhiteSpace(rawRecord.Category) ? "General" : rawRecord.Category;
                var category = !string.IsNullOrWhiteSpace(customSegment) ? customSegment.Trim() : defaultCat;
                var phone = rawRecord.Phone;
                var email = rawRecord.Email;
                var website = rawRecord.Website;
                var address = rawRecord.Address;

                double? latVal = null;
                double? lngVal = null;
                if (double.TryParse(rawRecord.Latitude, out var parsedLat)) latVal = parsedLat;
                if (double.TryParse(rawRecord.Longitude, out var parsedLng)) lngVal = parsedLng;

                // 3. Insert into companies
                const string insertSql = """
                    INSERT INTO companies (
                        name, category, phone, email, website, address,
                        x, y, raw_record_id, status_id, priority, created_by
                    ) VALUES (
                        @name, @category, @phone, @email, @website, @address,
                        @x, @y, @raw_record_id, @status_id, @priority, @created_by
                    )
                """;

                await using var cmd = new SqliteCommand(insertSql, conn, transaction);
                cmd.Parameters.AddWithValue("@name", name);
                cmd.Parameters.AddWithValue("@category", category);
                cmd.Parameters.AddWithValue("@phone", string.IsNullOrWhiteSpace(phone) ? DBNull.Value : phone);
                cmd.Parameters.AddWithValue("@email", string.IsNullOrWhiteSpace(email) ? DBNull.Value : email);
                cmd.Parameters.AddWithValue("@website", string.IsNullOrWhiteSpace(website) ? DBNull.Value : website);
                cmd.Parameters.AddWithValue("@address", string.IsNullOrWhiteSpace(address) ? DBNull.Value : address);
                cmd.Parameters.AddWithValue("@x", (object?)latVal ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@y", (object?)lngVal ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@raw_record_id", recordId);
                cmd.Parameters.AddWithValue("@status_id", statusId > 0 ? statusId : 1);
                cmd.Parameters.AddWithValue("@priority", string.IsNullOrWhiteSpace(priority) ? "medium" : priority.ToLower());
                cmd.Parameters.AddWithValue("@created_by", userId);

                await cmd.ExecuteNonQueryAsync();

                // 4. Update raw record status
                const string updateSql = "UPDATE raw_records SET status = 'imported', reviewed_by = @uid, reviewed_at = CURRENT_TIMESTAMP WHERE id = @id";
                await using var updateCmd = new SqliteCommand(updateSql, conn, transaction);
                updateCmd.Parameters.AddWithValue("@uid", userId);
                updateCmd.Parameters.AddWithValue("@id", recordId);
                await updateCmd.ExecuteNonQueryAsync();
            }

            await transaction.CommitAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "CleanAndTransferAsync failed");
            throw;
        }
    }

    private async Task<RawRecord?> GetRawRecordByIdAsync(long id, SqliteConnection conn, SqliteTransaction transaction)
    {
        const string sql = "SELECT * FROM raw_records WHERE id = @id";
        await using var cmd = new SqliteCommand(sql, conn, transaction);
        cmd.Parameters.AddWithValue("@id", id);
        await using var reader = await cmd.ExecuteReaderAsync() as SqliteDataReader;
        if (reader != null && await reader.ReadAsync())
        {
            return new RawRecord
            {
                Id = reader.GetInt64(reader.GetOrdinal("id")),
                JobId = reader.GetInt64(reader.GetOrdinal("job_id")),
                RawJson = reader.GetString(reader.GetOrdinal("raw_json")),
                Status = reader.GetString(reader.GetOrdinal("status")),
            };
        }
        return null;
    }
}
