// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Honua.Server.Core.Data;

namespace Honua.Server.Core.Http;

/// <summary>
/// Provides utilities for generating and parsing ETags for optimistic concurrency control.
/// </summary>
public static class ETagHelper
{
    /// <summary>
    /// Generates an ETag value from a feature record's version.
    /// </summary>
    /// <param name="record">The feature record containing version information.</param>
    /// <returns>An ETag string value, or null if no version is available.</returns>
    public static string? GenerateETag(FeatureRecord? record)
    {
        if (record?.Version == null)
        {
            return null;
        }

        return GenerateETag(record.Version);
    }

    /// <summary>
    /// Generates an ETag value from a version object.
    /// Supports various version types: long, int, byte[], string, DateTimeOffset.
    /// </summary>
    /// <param name="version">The version object to convert to an ETag.</param>
    /// <returns>An ETag string value in the format "W/"{value}"" for weak ETags.</returns>
    public static string? GenerateETag(object? version)
    {
        if (version == null)
        {
            return null;
        }

        // Convert version to string representation
        string versionString = version switch
        {
            // PostgreSQL BIGINT, SQLite INTEGER, MySQL BIGINT
            long l => l.ToString(CultureInfo.InvariantCulture),
            int i => i.ToString(CultureInfo.InvariantCulture),

            // SQL Server ROWVERSION (byte array)
            byte[] bytes => Convert.ToBase64String(bytes),

            // String version (e.g., GUID)
            string s => s,

            // DateTimeOffset (timestamp-based version)
            DateTimeOffset dto => dto.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),

            // DateTime (convert to UTC first)
            DateTime dt => new DateTimeOffset(dt.ToUniversalTime()).ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),

            // Fallback: use ToString() and hash it for consistency
            _ => ComputeHash(version.ToString() ?? string.Empty)
        };

        // Return as weak ETag - weak ETags indicate semantic equivalence
        // rather than byte-for-byte equality, which is appropriate for resource versions
        return $"W/\"{versionString}\"";
    }

    /// <summary>
    /// Parses an ETag header value to extract the version string.
    /// Handles both strong ("value") and weak (W/"value") ETags.
    /// </summary>
    /// <param name="etag">The ETag header value.</param>
    /// <returns>The extracted version string, or null if the ETag is invalid.</returns>
    public static string? ParseETag(string? etag)
    {
        if (string.IsNullOrWhiteSpace(etag))
        {
            return null;
        }

        // Remove W/ prefix for weak ETags
        var value = etag.Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            value = value.Substring(2).Trim();
        }

        // Remove surrounding quotes
        if (value.StartsWith("\"") && value.EndsWith("\"") && value.Length >= 2)
        {
            value = value.Substring(1, value.Length - 2);
        }

        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    /// <summary>
    /// Converts a parsed ETag string back to the appropriate version object type.
    /// </summary>
    /// <param name="etagValue">The parsed ETag value string.</param>
    /// <param name="versionType">The expected version type (e.g., "bigint", "rowversion", "timestamp").</param>
    /// <returns>The version object, or null if conversion fails.</returns>
    public static object? ConvertETagToVersion(string? etagValue, string? versionType = null)
    {
        if (string.IsNullOrWhiteSpace(etagValue))
        {
            return null;
        }

        // Try to infer type based on format or use explicit version type
        if (versionType?.Equals("rowversion", StringComparison.OrdinalIgnoreCase) == true ||
            (etagValue.Length > 0 && etagValue.Contains("=") || etagValue.Contains("+")))
        {
            // Base64-encoded byte array (SQL Server ROWVERSION)
            try
            {
                return Convert.FromBase64String(etagValue);
            }
            catch
            {
                return null;
            }
        }

        // Try parsing as long (most common for BIGINT versions)
        if (long.TryParse(etagValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var longValue))
        {
            return longValue;
        }

        // Try parsing as int
        if (int.TryParse(etagValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
        {
            return intValue;
        }

        // Try parsing as DateTimeOffset (Unix timestamp in milliseconds)
        if (long.TryParse(etagValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp))
        {
            try
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(timestamp);
            }
            catch
            {
                // Not a valid timestamp, continue
            }
        }

        // Return as string if no other type matches
        return etagValue;
    }

    /// <summary>
    /// Checks if an ETag matches the current resource version.
    /// </summary>
    /// <param name="requestETag">The ETag from the request (If-Match or If-None-Match header).</param>
    /// <param name="currentVersion">The current version of the resource.</param>
    /// <returns>True if the ETag matches the current version, false otherwise.</returns>
    public static bool ETagMatches(string? requestETag, object? currentVersion)
    {
        if (string.IsNullOrWhiteSpace(requestETag) || currentVersion == null)
        {
            return false;
        }

        var currentETag = GenerateETag(currentVersion);
        if (currentETag == null)
        {
            return false;
        }

        // Compare ETags (case-insensitive, handles both strong and weak)
        var parsedRequestETag = ParseETag(requestETag);
        var parsedCurrentETag = ParseETag(currentETag);

        return string.Equals(parsedRequestETag, parsedCurrentETag, StringComparison.Ordinal);
    }

    /// <summary>
    /// Computes a SHA256 hash of the input string.
    /// Used as a fallback for version types that don't have a standard string representation.
    /// </summary>
    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToBase64String(bytes).Substring(0, 32); // Truncate for brevity
    }
}
