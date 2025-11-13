// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;
using Honua.MapSDK.Models.Import;
using Microsoft.Extensions.Logging;

namespace Honua.MapSDK.Services.Import;

/// <summary>
/// Service for detecting address columns in uploaded data files with confidence scoring.
/// Supports various address formats (US, international, lat/lon pairs).
/// </summary>
public class AddressDetectionService
{
    private readonly ILogger<AddressDetectionService> _logger;

    // Common address-related column name patterns
    private static readonly string[] AddressKeywords = new[]
    {
        "address", "addr", "street", "location", "place", "venue"
    };

    private static readonly string[] StreetKeywords = new[]
    {
        "street", "st", "road", "rd", "avenue", "ave", "boulevard", "blvd", "lane", "ln"
    };

    private static readonly string[] CityKeywords = new[]
    {
        "city", "town", "municipality"
    };

    private static readonly string[] StateKeywords = new[]
    {
        "state", "province", "region", "county"
    };

    private static readonly string[] PostalKeywords = new[]
    {
        "zip", "postal", "postcode", "zipcode"
    };

    private static readonly string[] CountryKeywords = new[]
    {
        "country", "nation"
    };

    // Regex patterns for address detection
    private static readonly Regex UsAddressPattern = new(
        @"\d+\s+[\w\s]+(?:street|st|road|rd|avenue|ave|boulevard|blvd|lane|ln|drive|dr|way|court|ct|place|pl)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ZipCodePattern = new(
        @"\b\d{5}(?:-\d{4})?\b",
        RegexOptions.Compiled);

    private static readonly Regex PostalCodePattern = new(
        @"\b[A-Z]\d[A-Z]\s?\d[A-Z]\d\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LatLonPattern = new(
        @"^-?\d{1,3}\.\d+,\s*-?\d{1,3}\.\d+$",
        RegexOptions.Compiled);

    public AddressDetectionService(ILogger<AddressDetectionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Detects address columns in parsed data with confidence scoring.
    /// </summary>
    /// <param name="parsedData">Parsed data from file upload</param>
    /// <returns>List of detected address column candidates with confidence scores</returns>
    public List<AddressColumnCandidate> DetectAddressColumns(ParsedData parsedData)
    {
        var candidates = new List<AddressColumnCandidate>();

        if (parsedData.Fields.Count == 0)
        {
            _logger.LogWarning("No fields found in parsed data for address detection");
            return candidates;
        }

        // Analyze each field
        foreach (var field in parsedData.Fields)
        {
            var candidate = AnalyzeField(field, parsedData.Features);
            if (candidate.Confidence > 0)
            {
                candidates.Add(candidate);
            }
        }

        // Sort by confidence descending
        candidates = candidates.OrderByDescending(c => c.Confidence).ToList();

        _logger.LogInformation(
            "Detected {Count} potential address columns from {TotalFields} fields",
            candidates.Count,
            parsedData.Fields.Count);

        return candidates;
    }

    /// <summary>
    /// Automatically selects the best address configuration from detected candidates.
    /// </summary>
    /// <param name="parsedData">Parsed data from file upload</param>
    /// <returns>Suggested address configuration or null if no suitable addresses found</returns>
    public AddressConfiguration? SuggestAddressConfiguration(ParsedData parsedData)
    {
        var candidates = DetectAddressColumns(parsedData);

        if (candidates.Count == 0)
        {
            return null;
        }

        // Strategy 1: Look for single full address column (highest confidence)
        var fullAddressCandidate = candidates
            .FirstOrDefault(c => c.Type == AddressColumnType.FullAddress && c.Confidence >= 0.7);

        if (fullAddressCandidate != null)
        {
            return new AddressConfiguration
            {
                Type = AddressConfigurationType.SingleColumn,
                SingleColumnName = fullAddressCandidate.FieldName,
                Confidence = fullAddressCandidate.Confidence
            };
        }

        // Strategy 2: Look for structured address (multiple columns)
        var structuredConfig = DetectStructuredAddress(candidates, parsedData);
        if (structuredConfig != null && structuredConfig.Confidence >= 0.6)
        {
            return structuredConfig;
        }

        // Strategy 3: Fall back to highest confidence single column
        var bestCandidate = candidates.First();
        if (bestCandidate.Confidence >= 0.5)
        {
            return new AddressConfiguration
            {
                Type = AddressConfigurationType.SingleColumn,
                SingleColumnName = bestCandidate.FieldName,
                Confidence = bestCandidate.Confidence
            };
        }

        return null;
    }

    /// <summary>
    /// Analyzes a single field to determine if it contains address data.
    /// </summary>
    private AddressColumnCandidate AnalyzeField(FieldDefinition field, List<ParsedFeature> features)
    {
        var candidate = new AddressColumnCandidate
        {
            FieldName = field.Name,
            Type = AddressColumnType.Unknown,
            Confidence = 0.0
        };

        // Skip non-string fields (except for pre-marked address fields)
        if (field.Type != FieldType.String && !field.IsLikelyAddress)
        {
            return candidate;
        }

        double confidenceScore = 0.0;
        var type = AddressColumnType.Unknown;

        // Check field name against known patterns
        var normalizedName = field.Name.ToLowerInvariant().Replace("_", "").Replace(" ", "");

        // Full address detection
        if (AddressKeywords.Any(k => normalizedName.Contains(k)))
        {
            confidenceScore += 0.4;
            type = AddressColumnType.FullAddress;
        }

        // Specific component detection
        if (StreetKeywords.Any(k => normalizedName.Contains(k)))
        {
            confidenceScore += 0.3;
            type = AddressColumnType.Street;
        }
        else if (CityKeywords.Any(k => normalizedName.Contains(k)))
        {
            confidenceScore += 0.3;
            type = AddressColumnType.City;
        }
        else if (StateKeywords.Any(k => normalizedName.Contains(k)))
        {
            confidenceScore += 0.3;
            type = AddressColumnType.State;
        }
        else if (PostalKeywords.Any(k => normalizedName.Contains(k)))
        {
            confidenceScore += 0.3;
            type = AddressColumnType.PostalCode;
        }
        else if (CountryKeywords.Any(k => normalizedName.Contains(k)))
        {
            confidenceScore += 0.3;
            type = AddressColumnType.Country;
        }

        // Analyze sample values for content patterns
        var sampleValues = field.SampleValues
            .Where(v => v != null && !string.IsNullOrWhiteSpace(v.ToString()))
            .Select(v => v!.ToString()!)
            .ToList();

        if (sampleValues.Any())
        {
            var contentScore = AnalyzeFieldContent(sampleValues, ref type);
            confidenceScore += contentScore;
        }

        // Penalize fields with high null count
        if (field.NullCount > 0)
        {
            var nullRatio = (double)field.NullCount / (field.NullCount + sampleValues.Count);
            confidenceScore *= (1 - nullRatio * 0.5); // Up to 50% penalty
        }

        // Cap confidence at 1.0
        candidate.Confidence = Math.Min(confidenceScore, 1.0);
        candidate.Type = type;
        candidate.SampleValues = sampleValues.Take(3).ToList();

        return candidate;
    }

    /// <summary>
    /// Analyzes field content to detect address patterns.
    /// </summary>
    private double AnalyzeFieldContent(List<string> values, ref AddressColumnType type)
    {
        double score = 0.0;
        double matchCount = 0;

        foreach (var value in values.Take(10)) // Sample first 10 values
        {
            // Check for US street address pattern
            if (UsAddressPattern.IsMatch(value))
            {
                matchCount++;
                if (type == AddressColumnType.Unknown)
                {
                    type = AddressColumnType.FullAddress;
                }
            }
            // Check for ZIP code pattern
            else if (ZipCodePattern.IsMatch(value))
            {
                matchCount++;
                if (type == AddressColumnType.Unknown)
                {
                    type = AddressColumnType.PostalCode;
                }
            }
            // Check for postal code pattern (e.g., Canadian)
            else if (PostalCodePattern.IsMatch(value))
            {
                matchCount++;
                if (type == AddressColumnType.Unknown)
                {
                    type = AddressColumnType.PostalCode;
                }
            }
            // Check for lat/lon pair
            else if (LatLonPattern.IsMatch(value))
            {
                matchCount++;
                if (type == AddressColumnType.Unknown)
                {
                    type = AddressColumnType.LatLonPair;
                }
            }
            // Check for comma-separated address components
            else if (value.Contains(',') && value.Split(',').Length >= 2)
            {
                matchCount += 0.5; // Lower weight for generic comma detection
                if (type == AddressColumnType.Unknown)
                {
                    type = AddressColumnType.FullAddress;
                }
            }
        }

        // Calculate confidence based on match ratio
        if (values.Count > 0)
        {
            var matchRatio = matchCount / (double)Math.Min(values.Count, 10);
            score = matchRatio * 0.5; // Content analysis contributes up to 50% confidence
        }

        return score;
    }

    /// <summary>
    /// Detects structured address format (multiple columns combined).
    /// </summary>
    private AddressConfiguration? DetectStructuredAddress(
        List<AddressColumnCandidate> candidates,
        ParsedData parsedData)
    {
        var street = candidates.FirstOrDefault(c => c.Type == AddressColumnType.Street);
        var city = candidates.FirstOrDefault(c => c.Type == AddressColumnType.City);
        var state = candidates.FirstOrDefault(c => c.Type == AddressColumnType.State);
        var postal = candidates.FirstOrDefault(c => c.Type == AddressColumnType.PostalCode);

        // Require at least street and city for structured address
        if (street == null || city == null)
        {
            return null;
        }

        var columns = new List<string> { street.FieldName, city.FieldName };
        var avgConfidence = (street.Confidence + city.Confidence) / 2.0;

        if (state != null)
        {
            columns.Add(state.FieldName);
            avgConfidence = (avgConfidence * 2 + state.Confidence) / 3.0;
        }

        if (postal != null)
        {
            columns.Add(postal.FieldName);
            avgConfidence = (avgConfidence * columns.Count + postal.Confidence) / (columns.Count + 1);
        }

        return new AddressConfiguration
        {
            Type = AddressConfigurationType.MultiColumn,
            MultiColumnNames = columns,
            Separator = ", ",
            Confidence = avgConfidence
        };
    }
}

/// <summary>
/// Represents a detected address column candidate with confidence score.
/// </summary>
public class AddressColumnCandidate
{
    /// <summary>
    /// Field name in the dataset
    /// </summary>
    public required string FieldName { get; set; }

    /// <summary>
    /// Type of address data detected
    /// </summary>
    public AddressColumnType Type { get; set; }

    /// <summary>
    /// Confidence score (0-1) indicating likelihood this is an address column
    /// </summary>
    public double Confidence { get; set; }

    /// <summary>
    /// Sample values from this column
    /// </summary>
    public List<string> SampleValues { get; set; } = new();
}

/// <summary>
/// Type of address column detected
/// </summary>
public enum AddressColumnType
{
    Unknown,
    FullAddress,
    Street,
    City,
    State,
    PostalCode,
    Country,
    LatLonPair
}

/// <summary>
/// Configuration for address extraction from dataset
/// </summary>
public class AddressConfiguration
{
    /// <summary>
    /// Type of address configuration
    /// </summary>
    public AddressConfigurationType Type { get; set; }

    /// <summary>
    /// Single column name (for SingleColumn type)
    /// </summary>
    public string? SingleColumnName { get; set; }

    /// <summary>
    /// Multiple column names (for MultiColumn type)
    /// </summary>
    public List<string>? MultiColumnNames { get; set; }

    /// <summary>
    /// Separator for combining multiple columns
    /// </summary>
    public string Separator { get; set; } = ", ";

    /// <summary>
    /// Overall confidence score
    /// </summary>
    public double Confidence { get; set; }
}

/// <summary>
/// Type of address configuration
/// </summary>
public enum AddressConfigurationType
{
    SingleColumn,
    MultiColumn
}
