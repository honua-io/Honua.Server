// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Stac;

/// <summary>
/// Provides sanitization for STAC metadata text fields to prevent XSS and injection attacks.
/// STAC responses are JSON, but they may be consumed by web UIs that render text as HTML.
/// </summary>
internal static partial class StacTextSanitizer
{
    // Detect HTML tags and common XSS patterns
    [GeneratedRegex(@"<[^>]+>|javascript:|on\w+\s*=|data:text/html", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex DangerousPatternRegex();

    // Reserved STAC property keys that should not be overwritten via AdditionalProperties
    private static readonly HashSet<string> ReservedStacKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        // Core STAC Collection fields
        "type", "stac_version", "id", "title", "description", "keywords",
        "license", "providers", "extent", "summaries", "links", "assets",
        "stac_extensions", "item_assets",

        // Core STAC Item fields
        "bbox", "geometry", "properties", "collection",

        // Common extension fields
        "datetime", "start_datetime", "end_datetime"
    };

    /// <summary>
    /// Sanitizes text for use in STAC JSON responses.
    /// Encodes HTML entities and removes/neutralizes dangerous patterns.
    /// </summary>
    /// <param name="text">The text to sanitize.</param>
    /// <param name="allowEmpty">Whether to allow empty/null strings (default true).</param>
    /// <returns>Sanitized text safe for JSON serialization and HTML rendering.</returns>
    public static string? Sanitize(string? text, bool allowEmpty = true)
    {
        if (text.IsNullOrWhiteSpace())
        {
            return allowEmpty ? text : throw new ArgumentException("Text cannot be empty.", nameof(text));
        }

        // HTML encode to neutralize any HTML/script tags
        var encoded = WebUtility.HtmlEncode(text);

        // Additional check: if dangerous patterns are still present after encoding,
        // this indicates a potential bypass attempt - reject it
        if (DangerousPatternRegex().IsMatch(encoded))
        {
            throw new InvalidOperationException(
                $"Text contains potentially dangerous patterns that cannot be safely sanitized: {TruncateForLog(text)}");
        }

        return encoded;
    }

    /// <summary>
    /// Sanitizes a URL for use in STAC href fields.
    /// Validates URL scheme, prevents javascript: and data: URIs, and blocks path traversal attempts.
    /// </summary>
    /// <param name="url">The URL to sanitize.</param>
    /// <returns>Sanitized URL.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the URL contains dangerous protocols or path traversal patterns.</exception>
    public static string SanitizeUrl(string? url)
    {
        if (url.IsNullOrWhiteSpace())
        {
            return string.Empty;
        }

        // Trim whitespace
        url = url.Trim();

        // Check for dangerous protocols
        if (CultureInvariantHelpers.StartsWithIgnoreCase(url, "javascript:") ||
            CultureInvariantHelpers.StartsWithIgnoreCase(url, "data:text/html") ||
            CultureInvariantHelpers.StartsWithIgnoreCase(url, "vbscript:"))
        {
            throw new InvalidOperationException(
                $"URL contains dangerous protocol: {TruncateForLog(url)}");
        }

        // Check for path traversal attempts (including URL-encoded variants)
        // This prevents attempts to access files outside intended directories
        if (CultureInvariantHelpers.ContainsIgnoreCase(url, "../") ||
            CultureInvariantHelpers.ContainsIgnoreCase(url, "..\\") ||
            CultureInvariantHelpers.ContainsIgnoreCase(url, "%2e%2e/") ||
            CultureInvariantHelpers.ContainsIgnoreCase(url, "%2e%2e\\") ||
            CultureInvariantHelpers.ContainsIgnoreCase(url, "%2e%2e%2f") ||
            CultureInvariantHelpers.ContainsIgnoreCase(url, "%2e%2e%5c"))
        {
            throw new InvalidOperationException(
                $"URL contains path traversal pattern and is not allowed: {TruncateForLog(url)}");
        }

        // Validate URI format
        if (!Uri.TryCreate(url, UriKind.RelativeOrAbsolute, out var uri))
        {
            throw new InvalidOperationException($"Invalid URL format: {TruncateForLog(url)}");
        }

        // If absolute URI, ensure it's using safe schemes
        if (uri.IsAbsoluteUri)
        {
            var scheme = uri.Scheme.ToLowerInvariant();
            if (scheme != "http" && scheme != "https" && scheme != "ftp" && scheme != "s3" && scheme != "gs")
            {
                throw new InvalidOperationException(
                    $"URL scheme not allowed: {scheme}");
            }
        }

        return url;
    }

    /// <summary>
    /// Validates additional properties dictionary to prevent malicious content.
    /// Ensures no reserved STAC keys are being overwritten and sanitizes values.
    /// </summary>
    /// <param name="additionalProperties">The additional properties to validate.</param>
    /// <returns>Validated and sanitized dictionary.</returns>
    public static Dictionary<string, object> ValidateAdditionalProperties(
        IReadOnlyDictionary<string, object> additionalProperties)
    {
        Guard.NotNull(additionalProperties);

        var sanitized = new Dictionary<string, object>();

        foreach (var (key, value) in additionalProperties)
        {
            // Prevent overwriting core STAC fields
            if (ReservedStacKeys.Contains(key))
            {
                throw new InvalidOperationException(
                    $"Cannot override reserved STAC property '{key}' via AdditionalProperties");
            }

            // Validate key format (must be valid JSON property name)
            if (key.IsNullOrWhiteSpace() || key.Length > 256)
            {
                throw new InvalidOperationException(
                    $"Invalid additional property key: {TruncateForLog(key)}");
            }

            // Sanitize string values
            var sanitizedValue = value switch
            {
                string str => Sanitize(str),
                IEnumerable<string> strings => strings.Select(s => Sanitize(s)).ToList(),
                // Other types (numbers, booleans, objects) pass through
                // JSON serialization will handle them safely
                _ => value
            };

            sanitized[key] = sanitizedValue!;
        }

        return sanitized;
    }

    /// <summary>
    /// Truncates text for safe logging without exposing full malicious payloads.
    /// </summary>
    private static string TruncateForLog(string? text, int maxLength = 50)
    {
        if (text.IsNullOrEmpty())
        {
            return string.Empty;
        }

        if (text.Length <= maxLength)
        {
            return text;
        }

        return string.Concat(text.AsSpan(0, maxLength), "...");
    }
}
