using Microsoft.Data.Sqlite;
using ScripOx.Desktop.Models;
using BC = BCrypt.Net.BCrypt;
using Serilog;

namespace ScripOx.Desktop.Services;

/// <summary>
/// Handles user authentication, session management, and password operations.
/// </summary>
public class AuthService
{
    private readonly string _connectionString;

    // Currently logged-in user (null if not authenticated)
    public static User? CurrentUser { get; internal set; }

    public AuthService(string connectionString)
    {
        _connectionString = connectionString;
    }

    /// <summary>
    /// Validates credentials against the database.
    /// Returns the User on success, null on failure.
    /// </summary>
    public async Task<User?> LoginAsync(string username, string password)
    {
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            const string sql = """
                SELECT u.id, u.username, u.email, u.full_name, u.password_hash,
                       u.role_id, u.is_active, u.language, u.last_login,
                       r.name AS role_name
                FROM users u
                JOIN roles r ON r.id = u.role_id
                WHERE u.username = @username AND u.is_active = 1
                LIMIT 1
            """;

            await using var cmd = new SqliteCommand(sql, conn);
            cmd.Parameters.AddWithValue("@username", username);

            await using var reader = await cmd.ExecuteReaderAsync() as SqliteDataReader
                ?? throw new InvalidOperationException("Reader cast failed");

            if (!await reader.ReadAsync())
                return null;

            var hash = reader.GetString(reader.GetOrdinal("password_hash"));
            if (!BC.Verify(password, hash))
                return null;

            var lastLoginOrdinal = reader.GetOrdinal("last_login");
            var user = new User
            {
                Id        = reader.GetInt32(reader.GetOrdinal("id")),
                Username  = reader.GetString(reader.GetOrdinal("username")),
                Email     = reader.GetString(reader.GetOrdinal("email")),
                FullName  = reader.GetString(reader.GetOrdinal("full_name")),
                RoleId    = reader.GetInt32(reader.GetOrdinal("role_id")),
                RoleName  = reader.GetString(reader.GetOrdinal("role_name")),
                IsActive  = reader.GetBoolean(reader.GetOrdinal("is_active")),
                Language  = reader.GetString(reader.GetOrdinal("language")),
                LastLogin = reader.IsDBNull(lastLoginOrdinal)
                            ? null
                            : reader.GetDateTime(lastLoginOrdinal),
            };

            CurrentUser = user;
            await reader.CloseAsync();
            await _UpdateLastLoginAsync(user.Id, conn);

            Log.Information("User '{Username}' logged in (role: {Role})", user.Username, user.RoleName);
            return user;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Login error for user '{Username}'", username);
            return null;
        }
    }

    public static void Logout()
    {
        Log.Information("User '{Username}' logged out", CurrentUser?.Username);
        CurrentUser = null;
    }

    /// <summary>
    /// Bypasses login by setting a dummy admin user for development purposes.
    /// </summary>
    public static void SetDevelopmentUser()
    {
        CurrentUser = new User
        {
            Id = 1,
            Username = "superadmin",
            Email = "admin@scripox.com",
            FullName = "Dev Admin",
            RoleId = 1,
            RoleName = "super_admin",
            IsActive = true,
            Language = "en"
        };
        Log.Warning("Development mode active: Bypassed login with mock user.");
    }

    public static bool IsLoggedIn => CurrentUser is not null;

    // ── Private helpers ──────────────────────────────────────

    private static async Task _UpdateLastLoginAsync(int userId, SqliteConnection conn)
    {
        try
        {
            await using var cmd = new SqliteCommand(
                "UPDATE users SET last_login = CURRENT_TIMESTAMP WHERE id = @id", conn);
            cmd.Parameters.AddWithValue("@id", userId);
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Failed to update last_login for user {UserId}", userId);
        }
    }

    /// <summary>Hashes a plain-text password using BCrypt (work factor 12).</summary>
    public static string HashPassword(string plain) =>
        BC.HashPassword(plain, workFactor: 12);
}
