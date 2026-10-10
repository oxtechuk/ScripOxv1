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

            // Run migrations / ensure missing tables for existing databases
            EnsureMigrations(conn);

            // Seed Admin User
            SeedAdminUser(conn);

            // Ensure CRM Statuses
            EnsureCrmStatuses(conn);

            // Ensure Initial Seed Data (Sources & Sample Leads)
            EnsureSeedData(conn);
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

    private static void EnsureSeedData(SqliteConnection conn)
    {
        try
        {
            // 1. Seed Sources if empty
            using var countSrcCmd = new SqliteCommand("SELECT COUNT(*) FROM sources", conn);
            long srcCount = (long)countSrcCmd.ExecuteScalar()!;
            if (srcCount == 0)
            {
                using var insSrcCmd = new SqliteCommand("""
                    INSERT INTO sources (id, name, type, is_active) VALUES
                    (1, 'خرائط جوجل دبي (Google Maps UAE)', 'maps', 1),
                    (2, 'دليل الرياض التجاري (Riyadh Directory)', 'web', 1),
                    (3, 'جمهور ميتا الإعلاني (Meta Ads Import)', 'meta', 1),
                    (4, 'قائمة لينكد إن للشركات (LinkedIn Leads)', 'social', 1);
                """, conn);
                insSrcCmd.ExecuteNonQuery();
            }

            // 2. Seed Companies if empty
            using var countCompCmd = new SqliteCommand("SELECT COUNT(*) FROM companies", conn);
            long compCount = (long)countCompCmd.ExecuteScalar()!;
            if (compCount == 0)
            {
                using var insCompCmd = new SqliteCommand("""
                    INSERT INTO companies (name, name_ar, category, phone, phone2, email, website, address, city, region, country, postal_code, priority, status_id, source_id)
                    VALUES
                    ('Elizabeth Olsen Luxury Real Estate', 'مؤسسة إليزابيث أولسن للعقارات الفاخرة', 'عقارات واستثمار', '+16505615622', '+16507825622', 'elizabetho@fb.com', 'https://olsen-properties.com', 'Menlo Park, Silicon Valley', 'Menlo Park', 'CA', 'US', '94046', 'high', 4, 3),
                    ('Andrew Jamison Architectural Firm', 'مكتب المهندس أندرو جيميسون للعمارة', 'استشارات هندسية', '+12127363100', '+12125233100', 'andrewj@fb.com', 'https://jamison-arch.com', 'Empire State Bldg, 350 5th Ave', 'New York', 'NY', 'US', '10118', 'high', 4, 3),
                    ('Emaar Business Hub Partners', 'إعمار بارتنرز للخدمات العقارية والتطوير', 'عقارات واستثمار', '+971501234567', '+97143673333', 'invest@emaar-partners.ae', 'https://emaar-partners.ae', 'Downtown Dubai, Boulevard', 'Dubai', 'Dubai', 'AE', '12345', 'high', 4, 1),
                    ('Al Rajhi Tech & Cloud Solutions', 'مجموعة الراجحي للحلول التقنية والسحابية', 'تقنية ومعلومات', '+966559876543', '+966114567890', 'contact@alrajhi-tech.sa', 'https://alrajhi-tech.sa', 'طريق الملك فهد، العليا', 'Riyadh', 'Riyadh', 'SA', '11564', 'high', 2, 2),
                    ('Margaret Johnson Financial Advisory', 'مارغريت جونسون للاستشارات المالية', 'مالية واستثمار', '+13238576000', '+13236176000', 'margaretj@fb.com', 'https://johnson-finance.com', 'Wilshire Blvd', 'Los Angeles', 'CA', 'US', '90001', 'medium', 2, 3),
                    ('John Doe Digital Agency', 'وكالة جون دو للدعاية الرقمية', 'تسويق وإعلان', '+13124433600', '+13125553600', 'johnd@fb.com', 'https://doe-marketing.com', 'Michigan Avenue', 'Chicago', 'IL', 'US', '60603', 'medium', 2, 3),
                    ('Mark Smith Commercial Contracting', 'شركة مارك سميث للمقاولات والتجارة', 'مقاولات عامة', '+443031237300', '+448716631678', 'marks@fb.com', 'https://smith-build.co.uk', 'Westminster, SW1A 1AA', 'London', 'Greater London', 'GB', 'SW1A 1AA', 'high', 4, 3),
                    ('Dubai Gourmet Restaurants Chain', 'سلسلة مطاعم الذواقة دبي', 'مطاعم وضيافة', '+97142234567', '+971529988776', 'info@dubaigourmet.ae', 'https://dubaigourmet.ae', 'JBR The Walk', 'Dubai', 'Dubai', 'AE', '23456', 'medium', 2, 1),
                    ('James McLaughlin Global Logistics', 'مكلولين لخدمات الشحن واللوجستيات', 'شحن ولوجستيات', '+442072194272', '+448444825138', 'jamesm@fb.com', 'https://mclaughlin-cargo.com', 'Heathrow Cargo Park', 'London', 'Greater London', 'GB', 'SW1A 1AA', 'medium', 3, 3),
                    ('Paulo Alessandro Medical Tourism', 'باولو أليساندرو للخدمات الطبية والرعاية', 'رعاية صحية وطبية', '+552139386900', '+551130913116', 'pauloa@fb.com', 'https://alessandro-med.com', 'Av Paulista 1000', 'Sao Paulo', 'SP', 'BR', '01310-200', 'low', 1, 3),
                    ('Marie Laurent Haute Parfumerie', 'ماري لوران للعطور والمنتجات الفاخرة', 'تجارة وتجزئة', '+33892701239', '+33153098282', 'mariel@fb.com', 'https://laurent-paris.fr', 'Rue du Faubourg Saint-Honoré', 'Paris', 'IDF', 'FR', '75007', 'low', 1, 3),
                    ('Thomas Dubois Hospitality Group', 'مجموعة توماس دوبوا الفندقية والضيافة', 'مطاعم وضيافة', '+33892701239', '+33149524263', 'thomasd@fb.com', 'https://dubois-hotels.fr', 'Champs-Élysées', 'Paris', 'IDF', 'FR', '75007', 'medium', 2, 3),
                    ('Al Fanar Marine Services', 'شركة الفنار للملاحة والخدمات البحرية', 'شحن ولوجستيات', '+97143456789', '+971556781234', 'ops@alfanar-marine.ae', 'https://alfanar-marine.ae', 'Port Rashid, Bur Dubai', 'Dubai', 'Dubai', 'AE', '34567', 'high', 4, 1),
                    ('Horizon Contracting & Fitouts', 'هورايزون لأعمال الديكور والمقاولات', 'مقاولات عامة', '+966126543210', '+966504561234', 'info@horizon-fitout.sa', 'https://horizon-fitout.sa', 'طريق المدينة المنورة', 'Jeddah', 'Makkah', 'SA', '21452', 'medium', 2, 2);
                """, conn);
                insCompCmd.ExecuteNonQuery();
            }
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "EnsureSeedData encountered non-fatal error");
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

            CREATE TABLE IF NOT EXISTS company_comments (
              id         INTEGER PRIMARY KEY AUTOINCREMENT,
              company_id INTEGER NOT NULL,
              user_id    INTEGER NOT NULL,
              content    TEXT NOT NULL,
              parent_id  INTEGER,
              is_edited  INTEGER NOT NULL DEFAULT 0,
              created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
              updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
              FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
              FOREIGN KEY (user_id) REFERENCES users(id),
              FOREIGN KEY (parent_id) REFERENCES company_comments(id)
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

    public static void EnsureMigrations(SqliteConnection conn)
    {
        try
        {
            const string createCommentsSql = """
                CREATE TABLE IF NOT EXISTS company_comments (
                  id         INTEGER PRIMARY KEY AUTOINCREMENT,
                  company_id INTEGER NOT NULL,
                  user_id    INTEGER NOT NULL,
                  content    TEXT NOT NULL,
                  parent_id  INTEGER,
                  is_edited  INTEGER NOT NULL DEFAULT 0,
                  created_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                  updated_at DATETIME DEFAULT CURRENT_TIMESTAMP,
                  FOREIGN KEY (company_id) REFERENCES companies(id) ON DELETE CASCADE,
                  FOREIGN KEY (user_id) REFERENCES users(id),
                  FOREIGN KEY (parent_id) REFERENCES company_comments(id)
                );
            """;
            using var cmd = new SqliteCommand(createCommentsSql, conn);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to apply migrations for company_comments");
        }
    }
}
