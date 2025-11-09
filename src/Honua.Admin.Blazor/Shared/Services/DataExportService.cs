// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.IO;
using System.Text;
using ClosedXML.Excel;
using Microsoft.JSInterop;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Honua.Admin.Blazor.Shared.Services;

/// <summary>
/// Service for exporting table data to various formats (CSV, Excel, PDF)
/// </summary>
public class DataExportService
{
    private readonly IJSRuntime _jsRuntime;
    private readonly ILogger<DataExportService> _logger;

    public DataExportService(IJSRuntime jsRuntime, ILogger<DataExportService> logger)
    {
        _jsRuntime = jsRuntime;
        _logger = logger;

        // Configure QuestPDF license (Community license is free for open-source projects)
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Export data to CSV format
    /// </summary>
    public async Task ExportToCsvAsync<T>(
        IEnumerable<T> data,
        string filename,
        string[] columnNames,
        Func<T, string[]> rowMapper)
    {
        try
        {
            var csv = GenerateCsv(data, columnNames, rowMapper);
            var bytes = Encoding.UTF8.GetBytes(csv);
            await DownloadFileAsync(filename, bytes, "text/csv");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting data to CSV");
            throw;
        }
    }

    /// <summary>
    /// Export data to Excel format (.xlsx)
    /// </summary>
    public async Task ExportToExcelAsync<T>(
        IEnumerable<T> data,
        string filename,
        string sheetName,
        string[] columnNames,
        Func<T, object[]> rowMapper,
        string? title = null)
    {
        try
        {
            var excel = GenerateExcel(data, sheetName, columnNames, rowMapper, title);
            await DownloadFileAsync(
                filename,
                excel,
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting data to Excel");
            throw;
        }
    }

    /// <summary>
    /// Export data to PDF format
    /// </summary>
    public async Task ExportToPdfAsync<T>(
        IEnumerable<T> data,
        string filename,
        string title,
        string[] columnNames,
        Func<T, string[]> rowMapper)
    {
        try
        {
            var pdf = GeneratePdf(data, title, columnNames, rowMapper);
            await DownloadFileAsync(filename, pdf, "application/pdf");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error exporting data to PDF");
            throw;
        }
    }

    private string GenerateCsv<T>(
        IEnumerable<T> data,
        string[] columnNames,
        Func<T, string[]> rowMapper)
    {
        var sb = new StringBuilder();

        // Add header
        sb.AppendLine(string.Join(",", columnNames.Select(EscapeCsvValue)));

        // Add rows
        foreach (var item in data)
        {
            var values = rowMapper(item);
            sb.AppendLine(string.Join(",", values.Select(EscapeCsvValue)));
        }

        return sb.ToString();
    }

    private byte[] GenerateExcel<T>(
        IEnumerable<T> data,
        string sheetName,
        string[] columnNames,
        Func<T, object[]> rowMapper,
        string? title)
    {
        using var workbook = new XLWorkbook();
        var worksheet = workbook.Worksheets.Add(sheetName);

        int currentRow = 1;

        // Add title if provided
        if (!string.IsNullOrEmpty(title))
        {
            worksheet.Cell(currentRow, 1).Value = title;
            worksheet.Cell(currentRow, 1).Style.Font.Bold = true;
            worksheet.Cell(currentRow, 1).Style.Font.FontSize = 16;
            worksheet.Range(currentRow, 1, currentRow, columnNames.Length).Merge();
            currentRow += 2;
        }

        // Add headers
        for (int i = 0; i < columnNames.Length; i++)
        {
            var cell = worksheet.Cell(currentRow, i + 1);
            cell.Value = columnNames[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.LightGray;
            cell.Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }
        currentRow++;

        // Add data rows
        foreach (var item in data)
        {
            var values = rowMapper(item);
            for (int i = 0; i < values.Length; i++)
            {
                worksheet.Cell(currentRow, i + 1).Value = XLCellValue.FromObject(values[i]);
            }
            currentRow++;
        }

        // Auto-fit columns
        worksheet.Columns().AdjustToContents();

        // Add alternating row colors
        for (int row = (title != null ? 3 : 1) + 1; row < currentRow; row++)
        {
            if (row % 2 == 0)
            {
                worksheet.Row(row).Style.Fill.BackgroundColor = XLColor.AliceBlue;
            }
        }

        // Convert to byte array
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    private byte[] GeneratePdf<T>(
        IEnumerable<T> data,
        string title,
        string[] columnNames,
        Func<T, string[]> rowMapper)
    {
        var dataList = data.ToList();

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(9));

                // Header
                page.Header()
                    .AlignCenter()
                    .Text(title)
                    .SemiBold()
                    .FontSize(16);

                // Content
                page.Content()
                    .PaddingVertical(0.5f, Unit.Centimetre)
                    .Table(table =>
                    {
                        // Define columns
                        table.ColumnsDefinition(columns =>
                        {
                            for (int i = 0; i < columnNames.Length; i++)
                            {
                                columns.RelativeColumn();
                            }
                        });

                        // Header row
                        table.Header(header =>
                        {
                            foreach (var columnName in columnNames)
                            {
                                header.Cell()
                                    .Background(Colors.Grey.Lighten2)
                                    .Padding(5)
                                    .Text(columnName)
                                    .SemiBold();
                            }
                        });

                        // Data rows
                        foreach (var item in dataList)
                        {
                            var values = rowMapper(item);
                            foreach (var value in values)
                            {
                                table.Cell()
                                    .BorderBottom(0.5f)
                                    .BorderColor(Colors.Grey.Lighten2)
                                    .Padding(5)
                                    .Text(value ?? "-");
                            }
                        }
                    });

                // Footer
                page.Footer()
                    .AlignCenter()
                    .Text(text =>
                    {
                        text.Span($"Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss} | ");
                        text.CurrentPageNumber();
                        text.Span(" / ");
                        text.TotalPages();
                    })
                    .FontSize(8);
            });
        });

        return document.GeneratePdf();
    }

    private async Task DownloadFileAsync(string filename, byte[] data, string mimeType)
    {
        var base64 = Convert.ToBase64String(data);
        await _jsRuntime.InvokeVoidAsync("downloadFileFromBase64", filename, mimeType, base64);
    }

    private static string EscapeCsvValue(string value)
    {
        if (string.IsNullOrEmpty(value))
            return "\"\"";

        // Escape quotes and wrap in quotes if necessary
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
        {
            return $"\"{value.Replace("\"", "\"\"")}\"";
        }

        return value;
    }
}
