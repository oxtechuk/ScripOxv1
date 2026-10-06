using System.Text.Json;
using Microsoft.Data.Sqlite;
using Serilog;

namespace ScripOx.Desktop.Services;

/// <summary>
/// Writes audit log entries to the `audit_logs` table.
/// Call from any service after important mutations.
/// </summary>
public static class AuditLogService
{
    private static string? _connectionString;

    public static void Initialize(string connectionString)
    {
        _connectionString = connectionString;
    }

    public static async Task LogAsync(
        int?    userId,
        string  entity,
        long?   entityId,
        string  action,
        object? oldData  = null,
        object? newData  = null,
        string? ipAddress = null)
    {
        if (_connectionString is null)
        {
            Log.Warning("AuditLogService not initialized");
            return;
        }
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await LogAsync(conn, userId, entity, entityId, action, oldData, newData, ipAddress);
    }

    public static async Task LogAsync(
        SqliteConnection conn,
        int?    userId,
        string  entity,
        long?   entityId,
        string  action,
        object? oldData  = null,
        object? newData  = null,
        string? ipAddress = null)
    {
        try
        {
            const string sql = """
                INSERT INTO audit_logs (user_id, entity, entity_id, action, old_json, new_json, ip_address)
                VALUES (@uid, @entity, @eid, @action, @old, @new, @ip)
            """;

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@uid",    (object?)userId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@entity", entity);
            cmd.Parameters.AddWithValue("@eid",    (object?)entityId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@action", action);
            cmd.Parameters.AddWithValue("@old",    oldData is null ? DBNull.Value : JsonSerializer.Serialize(oldData));
            cmd.Parameters.AddWithValue("@new",    newData is null ? DBNull.Value : JsonSerializer.Serialize(newData));
            cmd.Parameters.AddWithValue("@ip",     (object?)ipAddress ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "AuditLog write failed for {Entity}/{Action}", entity, action);
        }
    }
}
