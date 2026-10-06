using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using ScripOx.Desktop.Models;

namespace ScripOx.Desktop.Services;

public class ExcelImportService
{
    public static List<Company> ParseExcel(string filePath)
    {
        var list = new List<Company>();
        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First();
        var rows = worksheet.RowsUsed().Skip(1); // skip headers

        foreach (var row in rows)
        {
            var name = row.Cell(1).GetString();
            if (string.IsNullOrWhiteSpace(name)) continue;

            var co = new Company
            {
                Name = name,
                Category = row.Cell(2).GetString(),
                Phone = row.Cell(3).GetString(),
                Phone2 = row.Cell(4).GetString(),
                Email = row.Cell(5).GetString(),
                Website = row.Cell(6).GetString(),
                Address = row.Cell(7).GetString(),
                City = row.Cell(8).GetString(),
                Country = string.IsNullOrWhiteSpace(row.Cell(9).GetString()) ? "UK" : row.Cell(9).GetString(),
                PostalCode = row.Cell(10).GetString(),
                Priority = string.IsNullOrWhiteSpace(row.Cell(12).GetString()) ? "medium" : row.Cell(12).GetString(),
            };

            // Parse coordinates
            var latStr = row.Cell(14).GetString();
            var lngStr = row.Cell(15).GetString();
            if (double.TryParse(latStr, out var lat)) co.X = lat;
            if (double.TryParse(lngStr, out var lng)) co.Y = lng;

            list.Add(co);
        }
        return list;
    }

    public static void GenerateTemplate(string filePath)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Companies Template");

        // Headers
        string[] headers = new[]
        {
            "Company Name", "Category", "Phone", "Phone 2", "Email", "Website", "Address", "City", "Country", "Postal Code", "Status", "Priority", "Assigned To", "Latitude", "Longitude"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1E3A5F");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Example row
        ws.Cell(2, 1).Value = "Example Business Name Ltd";
        ws.Cell(2, 2).Value = "Electronics & Electrical";
        ws.Cell(2, 3).Value = "+44 20 7946 0958";
        ws.Cell(2, 4).Value = "";
        ws.Cell(2, 5).Value = "info@example.com";
        ws.Cell(2, 6).Value = "https://www.example.com";
        ws.Cell(2, 7).Value = "123 High Street";
        ws.Cell(2, 8).Value = "London";
        ws.Cell(2, 9).Value = "UK";
        ws.Cell(2, 10).Value = "EC1A 1BB";
        ws.Cell(2, 11).Value = "New";
        ws.Cell(2, 12).Value = "medium";
        ws.Cell(2, 13).Value = "Admin";
        ws.Cell(2, 14).Value = 51.5074;
        ws.Cell(2, 15).Value = -0.1278;

        ws.Columns().AdjustToContents(minWidth: 10, maxWidth: 60);
        wb.SaveAs(filePath);
    }
}
