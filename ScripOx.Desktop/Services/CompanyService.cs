using Microsoft.Data.Sqlite;
using ScripOx.Desktop.Models;
using Serilog;

namespace ScripOx.Desktop.Services;

/// <summary>
/// Data access service for the `companies` table.
/// Supports filtering, pagination, CRUD, and notes/comments.
/// </summary>
public class CompanyService
{
    private readonly string _connectionString;

    public CompanyService(string connectionString)
    {
        _connectionString = connectionString;
    }

    // ── Query ────────────────────────────────────────────────

    /// <summary>Returns a filtered, paginated list of companies.</summary>
    public async Task<(List<Company> Items, int Total)> GetPagedAsync(
        CompanyFilter filter,
        int page = 1,
        int pageSize = 50)
    {
        var conditions = new List<string> { "1=1" };
        var parameters = new Dictionary<string, object?>();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            conditions.Add("(c.name LIKE @search OR c.phone LIKE @search OR c.email LIKE @search)");
            parameters["@search"] = $"%{filter.Search.Trim()}%";
        }
        if (filter.StatusId.HasValue)
        {
            conditions.Add("c.status_id = @status");
            parameters["@status"] = filter.StatusId;
        }
        if (filter.SourceId.HasValue && filter.SourceId.Value > 0)
        {
            conditions.Add("c.source_id = @source_id");
            parameters["@source_id"] = filter.SourceId.Value;
        }
        if (!string.IsNullOrWhiteSpace(filter.Category))
        {
            conditions.Add("c.category LIKE @category");
            parameters["@category"] = $"%{filter.Category.Trim()}%";
        }
        if (!string.IsNullOrWhiteSpace(filter.City))
        {
            conditions.Add("c.city = @city");
            parameters["@city"] = filter.City;
        }
        if (!string.IsNullOrWhiteSpace(filter.Country))
        {
            conditions.Add("c.country = @country");
            parameters["@country"] = filter.Country;
        }
        if (!string.IsNullOrWhiteSpace(filter.Priority))
        {
            conditions.Add("c.priority = @priority");
            parameters["@priority"] = filter.Priority;
        }
        if (filter.AssignedTo.HasValue)
        {
            conditions.Add("c.assigned_to = @assigned");
            parameters["@assigned"] = filter.AssignedTo;
        }

        var where  = string.Join(" AND ", conditions);
        var offset = (page - 1) * pageSize;

        var sortCol = filter.OrderBy?.ToLower() switch
        {
            "created_at" => "c.created_at",
            "name"       => "c.name",
            "id"         => "c.id",
            _            => "c.updated_at"
        };
        var sortDir = filter.OrderDesc ? "DESC" : "ASC";

        var sql = $"""
            SELECT c.*,
                   s.name  AS status_name,
                   s.color AS status_color,
                   u.full_name AS assigned_name
            FROM companies c
            LEFT JOIN company_statuses s ON s.id = c.status_id
            LEFT JOIN users u ON u.id = c.assigned_to
            WHERE {where}
            ORDER BY {sortCol} {sortDir}
            LIMIT @limit OFFSET @offset
        """;

        var countSql = $"SELECT COUNT(*) FROM companies c WHERE {where}";

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
            cmd.Parameters.AddWithValue("@limit",  pageSize);
            cmd.Parameters.AddWithValue("@offset", offset);

            var items = new List<Company>();
            await using var reader = (SqliteDataReader)await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
                items.Add(MapCompany(reader));

            return (items, total);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetPaged failed");
            throw;
        }
    }

    /// <summary>Loads a single company with notes, comments, and links.</summary>
    public async Task<Company?> GetByIdAsync(long id)
    {
        const string sql = """
            SELECT c.*,
                   s.name  AS status_name,
                   s.color AS status_color,
                   u.full_name AS assigned_name
            FROM companies c
            LEFT JOIN company_statuses s ON s.id = c.status_id
            LEFT JOIN users u ON u.id = c.assigned_to
            WHERE c.id = @id
        """;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();

        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", id);

        await using var reader = (SqliteDataReader)await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        var company = MapCompany(reader);
        await reader.CloseAsync();

        try { company.Notes = await GetNotesAsync(id, conn); }
        catch (Exception ex) { Log.Error(ex, "Failed to load company notes for ID {Id}", id); company.Notes = []; }

        try { company.Comments = await GetCommentsAsync(id, conn); }
        catch (Exception ex) { Log.Error(ex, "Failed to load company comments for ID {Id}", id); company.Comments = []; }

        try { company.Links = await GetLinksAsync(id, conn); }
        catch (Exception ex) { Log.Error(ex, "Failed to load company links for ID {Id}", id); company.Links = []; }

        return company;
    }

    public async Task<long> CreateAsync(Company company, int createdByUserId)
    {
        const string sql = """
            INSERT INTO companies (
                name, category, phone, phone2, email, website, address, city, country,
                postal_code, x, y, status_id, priority, created_by
            ) VALUES (
                @name, @category, @phone, @phone2, @email, @website, @address, @city, @country,
                @postal_code, @x, @y, 1, @priority, @created_by
            )
        """;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@name",        company.Name);
        cmd.Parameters.AddWithValue("@category",    (object?)company.Category ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@phone",       (object?)company.Phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@phone2",      (object?)company.Phone2 ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@email",       (object?)company.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@website",     (object?)company.Website ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@address",     (object?)company.Address ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@city",        (object?)company.City ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@country",     string.IsNullOrWhiteSpace(company.Country) ? "UK" : company.Country);
        cmd.Parameters.AddWithValue("@postal_code", (object?)company.PostalCode ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@x",           (object?)company.X ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@y",           (object?)company.Y ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@priority",    string.IsNullOrWhiteSpace(company.Priority) ? "medium" : company.Priority);
        cmd.Parameters.AddWithValue("@created_by",  createdByUserId);

        await cmd.ExecuteNonQueryAsync();

        using var idCmd = new SqliteCommand("SELECT last_insert_rowid();", conn);
        return Convert.ToInt64(await idCmd.ExecuteScalarAsync());
    }

    public async Task UpdateStatusAsync(long companyId, int statusId, int userId)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqliteCommand(
            "UPDATE companies SET status_id=@s, updated_at=CURRENT_TIMESTAMP WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@s",  statusId);
        cmd.Parameters.AddWithValue("@id", companyId);
        await cmd.ExecuteNonQueryAsync();

        await AuditLogService.LogAsync(conn, userId, "companies", companyId, "update",
            newData: new { status_id = statusId });
    }

    public async Task UpdateCategoryAsync(long companyId, string category)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqliteCommand(
            "UPDATE companies SET category=@c, updated_at=CURRENT_TIMESTAMP WHERE id=@id", conn);
        cmd.Parameters.AddWithValue("@c",  category);
        cmd.Parameters.AddWithValue("@id", companyId);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<CompanyStatus>> GetStatusesAsync()
    {
        var list = new List<CompanyStatus>();
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqliteCommand(
            "SELECT id, name, name_ar, color, sort_order FROM company_statuses ORDER BY sort_order, id", conn);
        await using var reader = (SqliteDataReader)await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new CompanyStatus
            {
                Id        = reader.GetInt32(0),
                Name      = reader.GetString(1),
                NameAr    = reader.IsDBNull(2) ? null : reader.GetString(2),
                Color     = reader.IsDBNull(3) ? "#64748B" : reader.GetString(3),
                SortOrder = reader.IsDBNull(4) ? 0 : reader.GetInt32(4)
            });
        }
        return list;
    }

    public async Task<int> AddNoteAsync(long companyId, int userId, string content, bool isPinned = false)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqliteCommand("""
            INSERT INTO company_notes (company_id, user_id, content, is_pinned)
            VALUES (@cid, @uid, @content, @pinned)
            """, conn);
        cmd.Parameters.AddWithValue("@cid",     companyId);
        cmd.Parameters.AddWithValue("@uid",     userId);
        cmd.Parameters.AddWithValue("@content", content);
        cmd.Parameters.AddWithValue("@pinned",  isPinned);
        await cmd.ExecuteNonQueryAsync();
        using var idCmd = new SqliteCommand("SELECT last_insert_rowid();", conn);
        return Convert.ToInt32(await idCmd.ExecuteScalarAsync());
    }

    public async Task<int> AddCommentAsync(long companyId, int userId, string content, int? parentId = null)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqliteCommand("""
            INSERT INTO company_comments (company_id, user_id, content, parent_id)
            VALUES (@cid, @uid, @content, @parent)
            """, conn);
        cmd.Parameters.AddWithValue("@cid",    companyId);
        cmd.Parameters.AddWithValue("@uid",    userId);
        cmd.Parameters.AddWithValue("@content",content);
        cmd.Parameters.AddWithValue("@parent", (object?)parentId ?? DBNull.Value);
        await cmd.ExecuteNonQueryAsync();
        using var idCmd = new SqliteCommand("SELECT last_insert_rowid();", conn);
        return Convert.ToInt32(await idCmd.ExecuteScalarAsync());
    }

    // ── Geo ──────────────────────────────────────────────────

    /// <summary>Returns all companies with coordinates for the map.</summary>
    public async Task<List<Company>> GetMapPointsAsync(int? statusId = null)
    {
        var where = statusId.HasValue
            ? "WHERE c.status_id = @s AND c.x IS NOT NULL"
            : "WHERE c.x IS NOT NULL";

        var sql = $"""
            SELECT c.id, c.name, c.city, c.phone, c.address,
                   c.x, c.y, c.status_id,
                   s.name AS status_name, s.color AS status_color
            FROM companies c
            LEFT JOIN company_statuses s ON s.id = c.status_id
            {where}
            LIMIT 5000
        """;

        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        await using var cmd = new SqliteCommand(sql, conn);
        if (statusId.HasValue) cmd.Parameters.AddWithValue("@s", statusId.Value);

        var list = new List<Company>();
        await using var reader = (SqliteDataReader)await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var idOrd      = reader.GetOrdinal("id");
            var nameOrd    = reader.GetOrdinal("name");
            var cityOrd    = reader.GetOrdinal("city");
            var phoneOrd   = reader.GetOrdinal("phone");
            var addrOrd    = reader.GetOrdinal("address");
            var xOrd       = reader.GetOrdinal("x");
            var yOrd       = reader.GetOrdinal("y");
            var sidOrd     = reader.GetOrdinal("status_id");
            var snOrd      = reader.GetOrdinal("status_name");
            var scOrd      = reader.GetOrdinal("status_color");

            list.Add(new Company
            {
                Id          = reader.GetInt64(idOrd),
                Name        = reader.GetString(nameOrd),
                City        = reader.IsDBNull(cityOrd)  ? null : reader.GetString(cityOrd),
                Phone       = reader.IsDBNull(phoneOrd) ? null : reader.GetString(phoneOrd),
                Address     = reader.IsDBNull(addrOrd)  ? null : reader.GetString(addrOrd),
                X           = reader.IsDBNull(xOrd)     ? null : reader.GetDouble(xOrd),
                Y           = reader.IsDBNull(yOrd)     ? null : reader.GetDouble(yOrd),
                StatusId    = reader.IsDBNull(sidOrd)   ? null : reader.GetInt32(sidOrd),
                StatusName  = reader.IsDBNull(snOrd)    ? null : reader.GetString(snOrd),
                StatusColor = reader.IsDBNull(scOrd)    ? null : reader.GetString(scOrd),
            });
        }
        return list;
    }

    // ── Private helpers ──────────────────────────────────────

    private static Company MapCompany(SqliteDataReader r)
    {
        int Ord(string col) => r.GetOrdinal(col);
        string? NullStr(string col) { var o = Ord(col); return r.IsDBNull(o) ? null : r.GetString(o); }

        return new Company
        {
            Id          = r.GetInt64(Ord("id")),
            Name        = r.GetString(Ord("name")),
            NameAr      = NullStr("name_ar"),
            Category    = NullStr("category"),
            Phone       = NullStr("phone"),
            Phone2      = NullStr("phone2"),
            Email       = NullStr("email"),
            Website     = NullStr("website"),
            Address     = NullStr("address"),
            City        = NullStr("city"),
            Region      = NullStr("region"),
            Country     = r.GetString(Ord("country")),
            PostalCode  = NullStr("postal_code"),
            X           = r.IsDBNull(Ord("x")) ? null : r.GetDouble(Ord("x")),
            Y           = r.IsDBNull(Ord("y")) ? null : r.GetDouble(Ord("y")),
            StatusId    = r.IsDBNull(Ord("status_id"))    ? null : r.GetInt32(Ord("status_id")),
            StatusName  = NullStr("status_name"),
            StatusColor = NullStr("status_color"),
            AssignedTo  = r.IsDBNull(Ord("assigned_to"))  ? null : r.GetInt32(Ord("assigned_to")),
            AssignedName= NullStr("assigned_name"),
            Priority    = r.GetString(Ord("priority")),
            IsVerified  = r.GetBoolean(Ord("is_verified")),
            CreatedAt   = SafeGetDateTime(r, "created_at"),
            UpdatedAt   = SafeGetDateTime(r, "updated_at"),
        };
    }

    private static DateTime SafeGetDateTime(SqliteDataReader r, string col)
    {
        try
        {
            var o = r.GetOrdinal(col);
            if (r.IsDBNull(o)) return DateTime.UtcNow;
            return r.GetDateTime(o);
        }
        catch
        {
            try
            {
                var o = r.GetOrdinal(col);
                var s = r.GetString(o);
                return DateTime.TryParse(s, out var dt) ? dt : DateTime.UtcNow;
            }
            catch
            {
                return DateTime.UtcNow;
            }
        }
    }

    private static async Task<List<CompanyNote>> GetNotesAsync(long companyId, SqliteConnection conn)
    {
        const string sql = """
            SELECT n.*, u.full_name AS user_name
            FROM company_notes n
            JOIN users u ON u.id = n.user_id
            WHERE n.company_id = @id
            ORDER BY n.is_pinned DESC, n.created_at DESC
        """;
        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", companyId);
        var list = new List<CompanyNote>();
        await using var r = (SqliteDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new CompanyNote
            {
                Id        = r.GetInt32(r.GetOrdinal("id")),
                CompanyId = r.GetInt64(r.GetOrdinal("company_id")),
                UserId    = r.GetInt32(r.GetOrdinal("user_id")),
                UserName  = r.GetString(r.GetOrdinal("user_name")),
                Content   = r.GetString(r.GetOrdinal("content")),
                IsPinned  = r.GetBoolean(r.GetOrdinal("is_pinned")),
                CreatedAt = SafeGetDateTime(r, "created_at"),
                UpdatedAt = SafeGetDateTime(r, "updated_at"),
            });
        return list;
    }

    private static async Task<List<CompanyComment>> GetCommentsAsync(long companyId, SqliteConnection conn)
    {
        const string sql = """
            SELECT c.*, u.full_name AS user_name
            FROM company_comments c
            JOIN users u ON u.id = c.user_id
            WHERE c.company_id = @id AND c.parent_id IS NULL
            ORDER BY c.created_at ASC
        """;
        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", companyId);
        var list = new List<CompanyComment>();
        await using var r = (SqliteDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var pidOrd = r.GetOrdinal("parent_id");
            list.Add(new CompanyComment
            {
                Id        = r.GetInt32(r.GetOrdinal("id")),
                CompanyId = r.GetInt64(r.GetOrdinal("company_id")),
                UserId    = r.GetInt32(r.GetOrdinal("user_id")),
                UserName  = r.GetString(r.GetOrdinal("user_name")),
                Content   = r.GetString(r.GetOrdinal("content")),
                ParentId  = r.IsDBNull(pidOrd) ? null : r.GetInt32(pidOrd),
                IsEdited  = r.GetBoolean(r.GetOrdinal("is_edited")),
                CreatedAt = SafeGetDateTime(r, "created_at"),
            });
        }
        return list;
    }

    private static async Task<List<CompanyLink>> GetLinksAsync(long companyId, SqliteConnection conn)
    {
        const string sql = "SELECT * FROM company_links WHERE company_id = @id";
        await using var cmd = new SqliteCommand(sql, conn);
        cmd.Parameters.AddWithValue("@id", companyId);
        var list = new List<CompanyLink>();
        await using var r = (SqliteDataReader)await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
            list.Add(new CompanyLink
            {
                Id        = r.GetInt32(r.GetOrdinal("id")),
                CompanyId = r.GetInt64(r.GetOrdinal("company_id")),
                Platform  = r.GetString(r.GetOrdinal("platform")),
                Url       = r.GetString(r.GetOrdinal("url")),
            });
        return list;
    }

    public async Task<List<string>> GetCategoriesAsync()
    {
        const string sql = "SELECT DISTINCT category FROM companies WHERE category IS NOT NULL AND category != '' ORDER BY category ASC";
        var list = new List<string>();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqliteCommand(sql, conn);
            await using var r = (SqliteDataReader)await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                if (!r.IsDBNull(0))
                {
                    var val = r.GetString(0).Trim();
                    if (!string.IsNullOrEmpty(val) && !list.Contains(val)) list.Add(val);
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "GetCategoriesAsync failed");
        }
        return list;
    }

    public async Task<int> UpdateCategoryBatchAsync(List<long> companyIds, string newCategory)
    {
        if (companyIds == null || companyIds.Count == 0 || string.IsNullOrWhiteSpace(newCategory)) return 0;
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync();
        var idList = string.Join(",", companyIds);
        await using var cmd = new SqliteCommand(
            $"UPDATE companies SET category = @c, updated_at = CURRENT_TIMESTAMP WHERE id IN ({idList})", conn);
        cmd.Parameters.AddWithValue("@c", newCategory.Trim());
        return await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>
    /// Retrieves companies that have a non-empty website, optionally filtered by category or specific IDs.
    /// </summary>
    public async Task<List<Company>> GetCompaniesWithWebsitesAsync(string? category = null, List<long>? specificIds = null)
    {
        var list = new List<Company>();
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            var conditions = new List<string> { "website IS NOT NULL", "TRIM(website) != ''" };
            var prms = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(category) && category != "الكل" && category != "All")
            {
                conditions.Add("category = @cat");
                prms["@cat"] = category.Trim();
            }

            if (specificIds != null && specificIds.Count > 0)
            {
                conditions.Add($"id IN ({string.Join(",", specificIds)})");
            }

            var sql = $"SELECT id, name, category, phone, phone2, email, website FROM companies WHERE {string.Join(" AND ", conditions)} ORDER BY id ASC";
            await using var cmd = new SqliteCommand(sql, conn);
            foreach (var kvp in prms)
            {
                cmd.Parameters.AddWithValue(kvp.Key, kvp.Value);
            }

            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                list.Add(new Company
                {
                    Id       = r.GetInt64(0),
                    Name     = r.GetString(1),
                    Category = r.IsDBNull(2) ? null : r.GetString(2),
                    Phone    = r.IsDBNull(3) ? null : r.GetString(3),
                    Phone2   = r.IsDBNull(4) ? null : r.GetString(4),
                    Email    = r.IsDBNull(5) ? null : r.GetString(5),
                    Website  = r.IsDBNull(6) ? null : r.GetString(6)
                });
            }
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to load companies with websites");
        }
        return list;
    }

    /// <summary>
    /// Updates email, phone, and/or phone2 for a company when discovered by crawler.
    /// </summary>
    public async Task<bool> UpdateContactInfoAsync(long companyId, string? email, string? phone, string? phone2 = null)
    {
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();

            // First inspect current values so we don't erase existing ones
            string? currentEmail = null;
            string? currentPhone = null;
            string? currentPhone2 = null;

            await using (var checkCmd = new SqliteCommand("SELECT email, phone, phone2 FROM companies WHERE id = @id", conn))
            {
                checkCmd.Parameters.AddWithValue("@id", companyId);
                await using var r = await checkCmd.ExecuteReaderAsync();
                if (await r.ReadAsync())
                {
                    currentEmail  = r.IsDBNull(0) ? null : r.GetString(0);
                    currentPhone  = r.IsDBNull(1) ? null : r.GetString(1);
                    currentPhone2 = r.IsDBNull(2) ? null : r.GetString(2);
                }
            }

            var finalEmail  = !string.IsNullOrWhiteSpace(email) ? email.Trim() : currentEmail;
            var finalPhone  = currentPhone;
            var finalPhone2 = currentPhone2;

            if (!string.IsNullOrWhiteSpace(phone))
            {
                var cleanNewPhone = phone.Trim();
                if (string.IsNullOrWhiteSpace(currentPhone))
                {
                    finalPhone = cleanNewPhone;
                }
                else if (currentPhone != cleanNewPhone && string.IsNullOrWhiteSpace(currentPhone2))
                {
                    finalPhone2 = cleanNewPhone;
                }
            }

            if (!string.IsNullOrWhiteSpace(phone2) && string.IsNullOrWhiteSpace(finalPhone2))
            {
                finalPhone2 = phone2.Trim();
            }

            const string updateSql = """
                UPDATE companies
                SET email = @email,
                    phone = @phone,
                    phone2 = @phone2,
                    updated_at = CURRENT_TIMESTAMP
                WHERE id = @id
            """;

            await using var updCmd = new SqliteCommand(updateSql, conn);
            updCmd.Parameters.AddWithValue("@email",  (object?)finalEmail ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@phone",  (object?)finalPhone ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@phone2", (object?)finalPhone2 ?? DBNull.Value);
            updCmd.Parameters.AddWithValue("@id",     companyId);

            int affected = await updCmd.ExecuteNonQueryAsync();
            return affected > 0;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to update contact info for company {CompanyId}", companyId);
            return false;
        }
    }

    /// <summary>
    /// Bulk deletes companies by their IDs.
    /// </summary>
    public async Task<int> DeleteBatchAsync(List<long> ids)
    {
        if (ids == null || ids.Count == 0) return 0;
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            var idList = string.Join(",", ids);
            await using var cmd = new SqliteCommand($"DELETE FROM companies WHERE id IN ({idList})", conn);
            return await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "DeleteBatchAsync failed for companies");
            return 0;
        }
    }

    /// <summary>
    /// Clears/wipes all companies from the database.
    /// </summary>
    public async Task<int> ClearAllAsync()
    {
        try
        {
            await using var conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync();
            await using var cmd = new SqliteCommand("DELETE FROM companies", conn);
            return await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Log.Error(ex, "ClearAllAsync failed for companies");
            return 0;
        }
    }
}

/// <summary>Filter parameters for company queries.</summary>
public class CompanyFilter
{
    public string? Search     { get; set; }
    public int?    StatusId   { get; set; }
    public int?    SourceId   { get; set; }
    public string? Category   { get; set; }
    public string? City       { get; set; }
    public string? Country    { get; set; }
    public string? Priority   { get; set; }
    public int?    AssignedTo { get; set; }
    public string? OrderBy    { get; set; } = "updated_at";
    public bool    OrderDesc  { get; set; } = true;
}
