// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Honua.Server.Core.Extensions;

namespace Honua.Server.Host.Wcs;

/// <summary>
/// Helper for WCS 2.0 Range Subsetting Extension support.
/// Allows selection of specific bands/fields from multi-band coverages.
/// Specification: OGC 12-040 - WCS 2.0 Range Subsetting Extension
/// </summary>
internal static class WcsRangeSubsettingHelper
{
    private static readonly Regex RangeComponentRegex = new(
        @"^(?<name>[A-Za-z_][A-Za-z0-9_]*)$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex RangeIntervalRegex = new(
        @"^(?<start>\d+):(?<end>\d+)$",
        RegexOptions.Compiled);

    /// <summary>
    /// Parses a rangeSubset parameter to extract band indices.
    /// Supports formats:
    /// - Band names: "Band1,Band3,Band5"
    /// - Band indices: "0,2,4" or "1,3,5" (1-based or 0-based)
    /// - Range intervals: "0:2" (bands 0, 1, 2)
    /// - Mixed: "Band1,3:5,Band7"
    /// </summary>
    /// <param name="rangeSubset">The rangeSubset parameter value.</param>
    /// <param name="totalBands">The total number of bands in the coverage.</param>
    /// <param name="bandIndices">The extracted 0-based band indices.</param>
    /// <param name="error">Error message if parsing fails.</param>
    /// <returns>True if successfully parsed, false otherwise.</returns>
    public static bool TryParseRangeSubset(
        string? rangeSubset,
        int totalBands,
        out List<int> bandIndices,
        out string? error)
    {
        bandIndices = new List<int>();
        error = null;

        if (rangeSubset.IsNullOrWhiteSpace())
        {
            // No range subset specified, return all bands
            bandIndices = Enumerable.Range(0, totalBands).ToList();
            return true;
        }

        if (totalBands <= 0)
        {
            error = "Coverage has no bands.";
            return false;
        }

        var components = rangeSubset.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var selectedIndices = new HashSet<int>();

        foreach (var component in components)
        {
            // Try to parse as interval (e.g., "0:2")
            var intervalMatch = RangeIntervalRegex.Match(component);
            if (intervalMatch.Success)
            {
                if (!int.TryParse(intervalMatch.Groups["start"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var start) ||
                    !int.TryParse(intervalMatch.Groups["end"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var end))
                {
                    error = $"Invalid range interval: '{component}'.";
                    return false;
                }

                if (start > end)
                {
                    error = $"Invalid range interval: start ({start}) > end ({end}) in '{component}'.";
                    return false;
                }

                if (start < 0 || end >= totalBands)
                {
                    error = $"Range interval '{component}' is out of bounds. Coverage has {totalBands} bands (indices 0-{totalBands - 1}).";
                    return false;
                }

                for (var i = start; i <= end; i++)
                {
                    selectedIndices.Add(i);
                }
                continue;
            }

            // Try to parse as integer index
            if (int.TryParse(component, NumberStyles.Integer, CultureInfo.InvariantCulture, out var index))
            {
                // Support both 0-based and 1-based indexing
                // If the index is exactly totalBands, assume 1-based (e.g., "3" for band 2 in a 3-band image)
                var zeroBasedIndex = index;
                if (index >= 1 && index <= totalBands)
                {
                    // Could be 1-based, convert to 0-based
                    zeroBasedIndex = index - 1;
                }

                if (zeroBasedIndex < 0 || zeroBasedIndex >= totalBands)
                {
                    error = $"Band index {index} is out of bounds. Coverage has {totalBands} bands (indices 0-{totalBands - 1} or 1-{totalBands}).";
                    return false;
                }

                selectedIndices.Add(zeroBasedIndex);
                continue;
            }

            // Try to parse as band name (e.g., "Band1", "Red", "NIR")
            if (RangeComponentRegex.IsMatch(component))
            {
                // Try to extract number from band name
                if (TryParseBandName(component, out var bandIndex))
                {
                    var zeroBasedIndex = bandIndex - 1; // Band names are typically 1-based
                    if (zeroBasedIndex < 0 || zeroBasedIndex >= totalBands)
                    {
                        error = $"Band name '{component}' resolves to index {bandIndex}, which is out of bounds. Coverage has {totalBands} bands.";
                        return false;
                    }

                    selectedIndices.Add(zeroBasedIndex);
                    continue;
                }

                // If we can't parse the band name, treat it as an error
                error = $"Cannot resolve band name '{component}'. Use numeric indices (0-{totalBands - 1}) or band names like 'Band1', 'Band2', etc.";
                return false;
            }

            error = $"Invalid range subset component: '{component}'. Expected band name, index, or interval.";
            return false;
        }

        if (selectedIndices.Count == 0)
        {
            error = "Range subset resulted in no bands selected.";
            return false;
        }

        bandIndices = selectedIndices.OrderBy(i => i).ToList();
        return true;
    }

    /// <summary>
    /// Tries to parse a band name to extract the band number.
    /// Examples: "Band1" -> 1, "band_3" -> 3, "B5" -> 5
    /// </summary>
    private static bool TryParseBandName(string bandName, out int bandNumber)
    {
        bandNumber = 0;

        // Match patterns like "Band1", "band_3", "B5", etc.
        var match = Regex.Match(bandName, @"[Bb]and[_\s]*(\d+)|[Bb](\d+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            var numStr = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
            return int.TryParse(numStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out bandNumber);
        }

        return false;
    }

    /// <summary>
    /// Validates that a range subset parameter is well-formed.
    /// </summary>
    public static bool IsValidRangeSubset(string? rangeSubset, int totalBands, out string? error)
    {
        return TryParseRangeSubset(rangeSubset, totalBands, out _, out error);
    }

    /// <summary>
    /// Converts band indices to a human-readable string.
    /// </summary>
    public static string FormatBandList(IEnumerable<int> bandIndices)
    {
        return string.Join(", ", bandIndices.Select(i => $"Band{i + 1}(index:{i})"));
    }
}
