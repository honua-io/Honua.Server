// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Honua.Server.Services.Models.BatchGeocoding;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Services.Services.BatchGeocoding;

/// <summary>
/// Service for parsing and writing CSV files for batch geocoding.
/// </summary>
public class CsvGeocodingService
{
    private readonly ILogger<CsvGeocodingService> _logger;

    public CsvGeocodingService(ILogger<CsvGeocodingService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Parses CSV file and extracts addresses with configuration.
    /// </summary>
    /// <param name="csvContent">CSV file content as byte array.</param>
    /// <param name="config">CSV import configuration.</param>
    /// <param name="options">Batch geocoding options.</param>
    /// <returns>Tuple of addresses and column headers.</returns>
    public async Task<(List<string> addresses, List<string> headers, List<Dictionary<string, string>> rows)> ParseCsvAsync(
        byte[] csvContent,
        CsvImportConfiguration config,
        BatchGeocodingOptions options)
    {
        var addresses = new List<string>();
        var headers = new List<string>();
        var rows = new List<Dictionary<string, string>>();

        try
        {
            var encoding = Encoding.GetEncoding(config.Encoding);
            using var stream = new MemoryStream(csvContent);
            using var reader = new StreamReader(stream, encoding);

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = config.Delimiter.ToString(),
                HasHeaderRecord = config.HasHeader,
                Quote = config.Quote,
                TrimOptions = TrimOptions.Trim,
                BadDataFound = context =>
                {
                    _logger.LogWarning("Bad CSV data at row {Row}: {RawRecord}",
                        context.Context.Parser.Row,
                        context.RawRecord);
                }
            };

            using var csv = new CsvReader(reader, csvConfig);

            // Read header
            await csv.ReadAsync();
            csv.ReadHeader();
            headers = csv.HeaderRecord?.ToList() ?? new List<string>();

            if (!config.HasHeader)
            {
                // Generate column names: Column1, Column2, etc.
                headers = Enumerable.Range(1, csv.Parser.Count).Select(i => $"Column{i}").ToList();
            }

            // Auto-detect address column if not specified
            var addressColumnIndex = DetermineAddressColumn(headers, options);

            // Read rows
            int rowCount = 0;
            while (await csv.ReadAsync())
            {
                if (config.MaxRows > 0 && rowCount >= config.MaxRows)
                    break;

                rowCount++;

                // Read all columns
                var row = new Dictionary<string, string>();
                for (int i = 0; i < headers.Count; i++)
                {
                    var value = csv.GetField(i) ?? string.Empty;
                    row[headers[i]] = value;
                }

                rows.Add(row);

                // Extract address
                var address = ExtractAddress(row, headers, addressColumnIndex, options);

                if (string.IsNullOrWhiteSpace(address) && options.SkipEmptyAddresses)
                {
                    _logger.LogDebug("Skipping row {Row} with empty address", rowCount);
                    continue;
                }

                addresses.Add(address ?? string.Empty);
            }

            _logger.LogInformation(
                "Parsed CSV: {TotalRows} rows, {AddressCount} addresses extracted",
                rowCount,
                addresses.Count);

            return (addresses, headers, rows);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse CSV");
            throw new Exception($"CSV parsing failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Detects available columns in CSV for address mapping.
    /// </summary>
    public List<string> DetectAddressColumns(List<string> headers)
    {
        var commonAddressTerms = new[]
        {
            "address", "addr", "street", "location", "place",
            "city", "town", "state", "province", "zip", "postal",
            "country"
        };

        return headers
            .Where(h => commonAddressTerms.Any(term =>
                h.Contains(term, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    /// <summary>
    /// Auto-detects the primary address column from headers.
    /// </summary>
    public int DetermineAddressColumn(List<string> headers, BatchGeocodingOptions options)
    {
        // Use specified column if provided
        if (!string.IsNullOrEmpty(options.AddressColumn))
        {
            var index = headers.FindIndex(h =>
                h.Equals(options.AddressColumn, StringComparison.OrdinalIgnoreCase));
            if (index >= 0)
                return index;
        }

        // Auto-detect: Look for "address" column
        var addressIndex = headers.FindIndex(h =>
            h.Equals("address", StringComparison.OrdinalIgnoreCase) ||
            h.Equals("full address", StringComparison.OrdinalIgnoreCase) ||
            h.Equals("location", StringComparison.OrdinalIgnoreCase));

        if (addressIndex >= 0)
            return addressIndex;

        // Look for "street" column
        addressIndex = headers.FindIndex(h =>
            h.Contains("street", StringComparison.OrdinalIgnoreCase));

        if (addressIndex >= 0)
            return addressIndex;

        // Default to first column
        return 0;
    }

    /// <summary>
    /// Extracts address from a CSV row.
    /// </summary>
    private string? ExtractAddress(
        Dictionary<string, string> row,
        List<string> headers,
        int addressColumnIndex,
        BatchGeocodingOptions options)
    {
        // Multi-column address (combine multiple columns)
        if (options.AddressColumns != null && options.AddressColumns.Count > 0)
        {
            var parts = new List<string>();
            foreach (var columnName in options.AddressColumns)
            {
                if (row.TryGetValue(columnName, out var value) && !string.IsNullOrWhiteSpace(value))
                {
                    parts.Add(value.Trim());
                }
            }

            return string.Join(options.AddressSeparator, parts);
        }

        // Single column address
        if (addressColumnIndex >= 0 && addressColumnIndex < headers.Count)
        {
            var columnName = headers[addressColumnIndex];
            if (row.TryGetValue(columnName, out var value))
            {
                return value?.Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// Writes batch geocoding results to CSV.
    /// </summary>
    public async Task<byte[]> WriteCsvAsync(
        BatchGeocodingResult result,
        List<string> originalHeaders,
        List<Dictionary<string, string>> originalRows,
        CsvExportConfiguration config)
    {
        try
        {
            using var stream = new MemoryStream();
            var encoding = Encoding.GetEncoding(config.Encoding);
            using var writer = new StreamWriter(stream, encoding);

            var csvConfig = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                Delimiter = config.Delimiter.ToString()
            };

            using var csv = new CsvWriter(writer, csvConfig);

            // Write headers
            if (config.IncludeOriginalColumns && originalHeaders.Count > 0)
            {
                foreach (var header in originalHeaders)
                {
                    csv.WriteField(header);
                }
            }

            csv.WriteField("Latitude");
            csv.WriteField("Longitude");

            if (config.IncludeMatchedAddress)
            {
                csv.WriteField("MatchedAddress");
            }

            if (config.IncludeQuality)
            {
                csv.WriteField("MatchQuality");
            }

            if (config.IncludeStatus)
            {
                csv.WriteField("Status");
            }

            if (config.IncludeConfidence)
            {
                csv.WriteField("Confidence");
            }

            await csv.NextRecordAsync();

            // Write data rows
            for (int i = 0; i < result.Matches.Count; i++)
            {
                var match = result.Matches[i];

                // Write original columns
                if (config.IncludeOriginalColumns && i < originalRows.Count)
                {
                    var originalRow = originalRows[i];
                    foreach (var header in originalHeaders)
                    {
                        var value = originalRow.TryGetValue(header, out var val) ? val : string.Empty;
                        csv.WriteField(value);
                    }
                }

                // Write geocoding results
                csv.WriteField(match.Latitude?.ToString("F6") ?? string.Empty);
                csv.WriteField(match.Longitude?.ToString("F6") ?? string.Empty);

                if (config.IncludeMatchedAddress)
                {
                    csv.WriteField(match.MatchedAddress ?? string.Empty);
                }

                if (config.IncludeQuality)
                {
                    csv.WriteField(match.Quality.ToString());
                }

                if (config.IncludeStatus)
                {
                    csv.WriteField(match.Status.ToString());
                }

                if (config.IncludeConfidence)
                {
                    csv.WriteField(match.Confidence?.ToString("F2") ?? string.Empty);
                }

                await csv.NextRecordAsync();
            }

            await writer.FlushAsync();
            return stream.ToArray();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write CSV");
            throw new Exception($"CSV writing failed: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates a sample CSV with test addresses.
    /// </summary>
    public async Task<byte[]> GenerateSampleCsvAsync()
    {
        var sampleAddresses = new[]
        {
            new { Address = "1600 Amphitheatre Parkway, Mountain View, CA", City = "Mountain View", State = "CA", Zip = "94043" },
            new { Address = "1 Microsoft Way, Redmond, WA", City = "Redmond", State = "WA", Zip = "98052" },
            new { Address = "1 Apple Park Way, Cupertino, CA", City = "Cupertino", State = "CA", Zip = "95014" },
            new { Address = "350 Fifth Avenue, New York, NY", City = "New York", State = "NY", Zip = "10118" },
            new { Address = "1600 Pennsylvania Avenue NW, Washington, DC", City = "Washington", State = "DC", Zip = "20500" },
            new { Address = "221B Baker Street, London, UK", City = "London", State = "", Zip = "NW1 6XE" },
            new { Address = "Eiffel Tower, Paris, France", City = "Paris", State = "", Zip = "75007" },
            new { Address = "Sydney Opera House, Sydney, Australia", City = "Sydney", State = "NSW", Zip = "2000" },
            new { Address = "Machu Picchu, Peru", City = "Aguas Calientes", State = "Cusco", Zip = "" },
            new { Address = "Great Wall of China, Beijing, China", City = "Beijing", State = "", Zip = "" },
            new { Address = "Colosseum, Rome, Italy", City = "Rome", State = "", Zip = "00184" },
            new { Address = "Taj Mahal, Agra, India", City = "Agra", State = "Uttar Pradesh", Zip = "282001" },
            new { Address = "Christ the Redeemer, Rio de Janeiro, Brazil", City = "Rio de Janeiro", State = "RJ", Zip = "22241-330" },
            new { Address = "Petra, Jordan", City = "Petra", State = "Ma'an", Zip = "" },
            new { Address = "Pyramids of Giza, Egypt", City = "Giza", State = "", Zip = "" }
        };

        using var stream = new MemoryStream();
        using var writer = new StreamWriter(stream, Encoding.UTF8);
        using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        // Write header
        csv.WriteField("Address");
        csv.WriteField("City");
        csv.WriteField("State");
        csv.WriteField("Zip");
        await csv.NextRecordAsync();

        // Write data
        foreach (var addr in sampleAddresses)
        {
            csv.WriteField(addr.Address);
            csv.WriteField(addr.City);
            csv.WriteField(addr.State);
            csv.WriteField(addr.Zip);
            await csv.NextRecordAsync();
        }

        await writer.FlushAsync();
        return stream.ToArray();
    }
}
