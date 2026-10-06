using Microsoft.Data.Sqlite;
using Serilog;

namespace ScripOx.Desktop.Services;

public static class DatabaseInitializer
{
    public static void Initialize(string connectionString)
    {
        try
        {
            using var conn = new SqliteConnection(connectionString);
            conn.Open();

            // Enable foreign keys
            using var pragmaCmd = new SqliteCommand("PRAGMA foreign_keys = ON;", conn);
            pragmaCmd.ExecuteNonQuery();

            // Create Tables
            using var cmd = new SqliteCommand(GetSchemaSql(), conn);
            cmd.ExecuteNonQuery();

            // Seed Admin User
            SeedAdminUser(conn);

            // Ensure CRM Statuses
            EnsureCrmStatuses(conn);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to initialize SQLite database.");
        }
    }

    private static void SeedAdminUser(SqliteConnection conn)
    {
        using var checkCmd = new SqliteCommand("SELECT COUNT(*) FROM users", conn);
        long count = (long)checkCmd.ExecuteScalar()!;
        if (count > 0) return;

        using var trans = conn.BeginTransaction();
        try
        {
            // Seed Role
            using var roleCmd = new SqliteCommand("INSERT INTO roles (id, name, name_ar) VALUES (1, 'Super Admin', 'مدير النظام')", conn, trans);
            roleCmd.ExecuteNonQuery();

            // Seed Admin
            string hash = BCrypt.Net.BCrypt.HashPassword("Admin@ScripOx2025", 12);
            using var userCmd = new SqliteCommand(
                "INSERT INTO users (username, email, password_hash, full_name, role_id, is_active, language) " +
                "VALUES ('superadmin', 'admin@oxtech.uk', @hash, 'Dev Admin', 1, 1, 'en')", conn, trans);
            userCmd.Parameters.AddWithValue("@hash", hash);
            userCmd.ExecuteNonQuery();

            // Seed Source
            using var sourceCmd = new SqliteCommand("INSERT INTO sources (id, name, type) VALUES (1, 'Google Maps Default', 'web')", conn, trans);
            sourceCmd.ExecuteNonQuery();

            // Seed Statuses
            string[] statuses = { 
                "INSERT INTO company_statuses (id, name, color, sort_order) VALUES (1, 'New', '#0D6EFD', 1)",
                "INSERT INTO company_statuses (id, name, color, sort_order) VALUES (2, 'Verified', '#198754', 2)",
                "INSERT INTO company_statuses (id, name, color, sort_order) VALUES (3, 'Invalid', '#DC3545', 3)"
            };
            foreach (var s in statuses)
            {
                using var sCmd = new SqliteCommand(s, conn, trans);
                sCmd.ExecuteNonQuery();
            }

            trans.Commit();
        }
        catch
        {
            trans.Rollback();
            throw;
        }

        EnsureCrmStatuses(conn);
    }

    public static void EnsureCrmStatuses(SqliteConnection conn)
    {
        var statuses = new (int id, string name, string nameAr, string color, int sort)[]
        {
            (1, "New", "جديد", "#64748B", 1),
            (2, "Interested", "مهتم", "#10B981", 2),
            (3, "Needs Follow-up", "يحتاج متابعة", "#F59E0B", 3),
            (4, "Deal Won", "تم التعاقد", "#059669", 4),
            (5, "Not Interested", "غير مهتم", "#94A3B8", 5)
        };

        foreach (var (id, name, nameAr, color, sort) in statuses)
        {
            using var cmd = new SqliteCommand("""
                INSERT INTO company_statuses (id, name, name_ar, color, sort_order)
                VALUES (@id, @name, @nameAr, @color, @sort)
                ON CONFLICT(id) DO UPDATE SET
                    name=excluded.name,
                    name_ar=excluded.name_ar,
                    color=excluded.color,
                    sort_order=excluded.sort_order;
            """, conn);
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@nameAr", nameAr);
            cmd.Parameters.AddWithValue("@color", color);
            cmd.Parameters.AddWithValue("@sort", sort);
            cmd.ExecuteNonQuery();
        }
    }

    private static string GetSchemaSql()
    {
        return """
            CREATE TABLE IF NOT EXISTS roles (
              id         INTEGER PRIMARY KEY AUTOINCREMENT,
              name       TEXT NOT NULL UNIQUE,
              name_ar    TEXT,
              created_at DATETIME DEFAULT CURRENT_TIMESTAMP
            );

            CREATE TABLE IF NOT EXISTS users (
              id            INTEGER PRIMARY KEY AUTOINCREMENT,
              username      TEXT NOT NULL UNIQUE,
              email         TEXT NOT NULL UNIQUE,
              password_hash TEXT NOT NULL,
              full_name     TEXT,
              role_id       INTEGER NOT NULL,
              is_active     INTEGER NOT NULL DEFAULT 1,
              language      TEXT NOT NULL DEFAULT 'en',
              avatar_url    TEXT,
              created_at    DATETIME DEFAULT CURRENT_TIMESTAMP,
              updated_at    DATETIME DEFAULT CURRENT_TIMESTAMP,
              last_login    DATETIME,
              FOREIGN KEY (role_id) REFERENCES roles(id)
            );

            CREATE TABLE IF NOT EXISTS sources (
              id          INTEGER PRIMARY KEY AUTOINCREMENT,
              name        TEXT NOT NULL,
              type        TEXT NOT NULL DEFAULT 'web',
              base_url    TEXT,
              connector   TEXT,
              config_json TEXT,
              is_active   INTEGER NOT NULL DEFAULT 1,
              created_by  INTEGER,
              created_at  DATETIME DEFAULT CURRENT_TIMESTAMP,
              updated_at  DATETIME DEFAULT CURRENT_TIMESTAMP,
              FOREIGN KEY (created_by) REFERENCES users(id)
            );

            CREATE TABLE IF NOT EXISTS extraction_jobs (
              id           INTEGER PRIMARY KEY AUTOINCREMENT,
              source_id    INTEGER NOT NULL,
              triggered_by INTEGER NOT NULL,
              status       TEXT NOT NULL DEFAULT 'pending',
              params_json  TEXT,
              total_found  INTEGER NOT NULL DEFAULT 0,
              total_saved  INTEGER NOT NULL DEFAULT 0,
              total_dupes  INTEGER NOT NULL DEFAULT 0,
              total_errors INTEGER NOT NULL DEFAULT 0,
              progress_pct INTEGER NOT NULL DEFAULT 0,
              started_at   DATETIME,
              ended_at     DATETIME,
              error_msg    TEXT,
              created_at   DATETIME DEFAULT CURRENT_TIMESTAMP,
              FOREIGN KEY (source_id) REFERENCES sources(id),
              FOREIGN KEY (triggered_by) REFERENCES users(id)
            );

            CREATE TABLE IF NOT EXISTS raw_records (
              id          INTEGER PRIMARY KEY AUTOINCREMENT,
              job_id      INTEGER NOT NULL,
              raw_json    TEXT NOT NULL,
              status      TEXT NOT NULL DEFAULT 'pending',
              reviewed_by INTEGER,
              reviewed_at DATETIME,
              reject_reason TEXT,
              created_at  DATETIME DEFAULT CURRENT_TIMESTAMP,
              FOREIGN KEY (job_id) REFERENCES extraction_jobs(id),
              FOREIGN KEY (reviewed_by) REFERENCES users(id)
            );

            CREATE TABLE IF NOT EXISTS company_statuses (
              id         INTEGER PRIMARY KEY AUTOINCREMENT,
              name       TEXT NOT NULL,
              name_ar    TEXT,
              color      TEXT NOT NULL DEFAULT '#6C757D',
              is_final   INTEGER NOT NULL DEFAULT 0,
              sort_order INTEGER NOT NULL DEFAULT 0
            );

            CREATE TABLE IF NOT EXISTS companies (
              id             INTEGER PRIMARY KEY AUTOINCREMENT,
              name           TEXT NOT NULL,
              name_ar        TEXT,
              category       TEXT,
              description    TEXT,
              phone          TEXT,
              phone2         TEXT,
              email          TEXT,
              website        TEXT,
              address        TEXT,
              city           TEXT,
              region         TEXT,
              country        TEXT NOT NULL DEFAULT 'UK',
              postal_code    TEXT,
              x              REAL,
              y              REAL,
              source_id      INTEGER,
              source_name    TEXT,
              source_url     TEXT,
              raw_record_id  INTEGER,
              status_id      INTEGER,
              assigned_to    INTEGER,
              priority       TEXT NOT NULL DEFAULT 'medium',
              is_verified    INTEGER NOT NULL DEFAULT 0,
              reviewed_at    DATETIME,
              created_by     INTEGER,
              created_at     DATETIME DEFAULT CURRENT_TIMESTAMP,
              updated_at     DATETIME DEFAULT CURRENT_TIMESTAMP,
              FOREIGN KEY (source_id) REFERENCES sources(id),
              FOREIGN KEY (status_id) REFERENCES company_statuses(id),
              FOREIGN KEY (assigned_to) REFERENCES users(id),
              FOREIGN KEY (created_by) REFERENCES users(id),
              FOREIGN KEY (raw_record_id) REFERENCES raw_records(id)
            );

            CREATE TABLE IF NOT EXISTS company_links (
              id         INTEGER PRIMARY KEY AUTOINCREMENT,
              company_id INTEGER NOT NULL,
              platform   TEXT NOT NULL,
              url        TEXT NOT NULL,
              FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE
            );

            CREATE TABLE IF NOT EXISTS company_notes (
              id         INTEGER PRIMARY KEY AUTOINCREMENT,
              company_id INTEGER NOT NULL,
              user_id    INTEGER NOT NULL,
              content    TEXT NOT NULL,
              is_pinned  INTEGER NOT NULL DEFAULT 0,
              created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
              updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
              FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
              FOREIGN KEY (user_id) REFERENCES users(id)
            );

            CREATE TABLE IF NOT EXISTS audit_logs (
              id         INTEGER PRIMARY KEY AUTOINCREMENT,
              user_id    INTEGER,
              entity     TEXT NOT NULL,
              entity_id  INTEGER,
              action     TEXT NOT NULL,
              old_json   TEXT,
              new_json   TEXT,
              ip_address TEXT,
              user_agent TEXT,
              created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
              FOREIGN KEY (user_id) REFERENCES users(id)
            );
        """;
    }
}
