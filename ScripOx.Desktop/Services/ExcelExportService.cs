using ClosedXML.Excel;
using ScripOx.Desktop.Models;
using Serilog;

namespace ScripOx.Desktop.Services;

/// <summary>
/// Exports company data to Excel (.xlsx) using ClosedXML.
/// Supports bilingual headers, status colour coding, and a summary sheet.
/// </summary>
public class ExcelExportService
{
    public record ExportOptions(
        string     FilePath,
        string     Language      = "en",
        bool       IncludeSummary = true,
        List<string>? Columns    = null
    );

    private static readonly Dictionary<string, (string En, string Ar)> _defaultColumns = new()
    {
        ["name"]        = ("Company Name",  "اسم الشركة"),
        ["category"]    = ("Category",      "التصنيف"),
        ["phone"]       = ("Phone",         "الهاتف"),
        ["phone2"]      = ("Phone 2",       "هاتف 2"),
        ["email"]       = ("Email",         "البريد"),
        ["website"]     = ("Website",       "الموقع"),
        ["address"]     = ("Address",       "العنوان"),
        ["city"]        = ("City",          "المدينة"),
        ["country"]     = ("Country",       "الدولة"),
        ["postal_code"] = ("Postal Code",   "الرمز البريدي"),
        ["status"]      = ("Status",        "الحالة"),
        ["priority"]    = ("Priority",      "الأولوية"),
        ["assigned"]    = ("Assigned To",   "مسند إلى"),
        ["x"]           = ("Longitude",     "خط الطول"),
        ["y"]           = ("Latitude",      "خط العرض"),
        ["source"]      = ("Source",        "المصدر"),
        ["created_at"]  = ("Created At",    "تاريخ الإضافة"),
    };

    public async Task ExportAsync(IEnumerable<Company> companies, ExportOptions options)
    {
        var list = companies.ToList();
        var lang  = options.Language;
        var cols  = options.Columns ?? _defaultColumns.Keys.ToList();

        await Task.Run(() =>
        {
            using var wb = new XLWorkbook();

            // ── Sheet 1: Data ────────────────────────────────
            var ws = wb.Worksheets.Add(lang == "ar" ? "الشركات" : "Companies");

            if (lang == "ar")
                ws.RightToLeft = true;

            // Header row
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var header = _defaultColumns.TryGetValue(col, out var h)
                    ? (lang == "ar" ? h.Ar : h.En)
                    : col;

                var cell = ws.Cell(1, i + 1);
                cell.Value = header;
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }

            // Data rows
            int row = 2;
            foreach (var co in list)
            {
                for (int i = 0; i < cols.Count; i++)
                {
                    var cell = ws.Cell(row, i + 1);
                    cell.Value = GetValue(co, cols[i]);

                    // Status colour coding
                    if (cols[i] == "status" && !string.IsNullOrEmpty(co.StatusColor))
                    {
                        try { cell.Style.Fill.BackgroundColor = XLColor.FromHtml(co.StatusColor); }
                        catch { /* ignore invalid hex */ }
                    }

                    // Alternate row colour
                    if (row % 2 == 0)
                        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#F8F9FA");
                }
                row++;
            }

            // Auto-fit columns
            ws.Columns().AdjustToContents(minWidth: 10, maxWidth: 60);
            ws.SheetView.FreezeRows(1);

            // ── Sheet 2: Summary ─────────────────────────────
            if (options.IncludeSummary)
            {
                var summary = wb.Worksheets.Add(lang == "ar" ? "ملخص" : "Summary");
                summary.Cell(1, 1).Value = lang == "ar" ? "الإجمالي" : "Total Records";
                summary.Cell(1, 2).Value = list.Count;

                var byStatus = list
                    .GroupBy(c => c.StatusName ?? "Unknown")
                    .OrderByDescending(g => g.Count());

                int sRow = 3;
                summary.Cell(sRow, 1).Value = lang == "ar" ? "الحالة" : "Status";
                summary.Cell(sRow, 2).Value = lang == "ar" ? "العدد" : "Count";
                summary.Row(sRow).Style.Font.Bold = true;
                sRow++;

                foreach (var g in byStatus)
                {
                    summary.Cell(sRow, 1).Value = g.Key;
                    summary.Cell(sRow, 2).Value = g.Count();
                    sRow++;
                }

                summary.Columns().AdjustToContents();
            }

            wb.SaveAs(options.FilePath);
            Log.Information("Excel exported: {Path} ({Count} rows)", options.FilePath, list.Count);
        });
    }

    // ── Value resolver ───────────────────────────────────────

    private static XLCellValue GetValue(Company co, string col) => col switch
    {
        "name"        => co.Name        ?? string.Empty,
        "category"    => co.Category    ?? string.Empty,
        "phone"       => co.Phone       ?? string.Empty,
        "phone2"      => co.Phone2      ?? string.Empty,
        "email"       => co.Email       ?? string.Empty,
        "website"     => co.Website     ?? string.Empty,
        "address"     => co.Address     ?? string.Empty,
        "city"        => co.City        ?? string.Empty,
        "country"     => co.Country,
        "postal_code" => co.PostalCode  ?? string.Empty,
        "status"      => co.StatusName  ?? string.Empty,
        "priority"    => co.Priority,
        "assigned"    => co.AssignedName ?? string.Empty,
        "x"           => co.X.HasValue ? co.X.Value : Blank.Value,
        "y"           => co.Y.HasValue ? co.Y.Value : Blank.Value,
        "source"      => co.SourceName  ?? string.Empty,
        "created_at"  => co.CreatedAt.ToString("yyyy-MM-dd"),
        _             => string.Empty
    };
}
