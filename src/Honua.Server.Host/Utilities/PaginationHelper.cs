// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Host.Utilities;

/// <summary>
/// Provides unified pagination utilities for cursor token management, pagination calculations,
/// and context metadata across STAC, OGC API, and Records protocols.
/// </summary>
/// <remarks>
/// <para><strong>Design Philosophy:</strong></para>
/// <para>
/// This helper consolidates ~120 lines of duplicate pagination logic across protocols:
/// </para>
/// <list type="bullet">
///   <item>
///     <description>
///       <strong>STAC:</strong> Uses cursor-based pagination with Base64-encoded tokens containing
///       "collectionId:itemId" pairs for stable pagination across large result sets.
///     </description>
///   </item>
///   <item>
///     <description>
///       <strong>OGC API / Records:</strong> Uses offset-based pagination with simple numeric offsets
///       for page navigation and standardized count metadata.
///     </description>
///   </item>
///   <item>
///     <description>
///       <strong>Link Generation:</strong> Delegates to RequestLinkHelper for URL building to avoid
///       duplication and ensure consistent proxy header handling.
///     </description>
///   </item>
/// </list>
/// <para><strong>Security:</strong></para>
/// <para>
/// Cursor tokens are validated for length (max 256 chars) and format (alphanumeric + allowed punctuation)
/// to prevent injection attacks. Invalid tokens are rejected gracefully without exposing internal details.
/// </para>
/// </remarks>
public static class PaginationHelper
{
    private const int MaxTokenLength = 256;
    private const int MaxCursorPartLength = 128;

    /// <summary>
    /// Generates a Base64-encoded cursor token for STAC-style cursor pagination.
    /// </summary>
    /// <param name="collectionId">The collection identifier of the last item in the current page.</param>
    /// <param name="itemId">The item identifier of the last item in the current page.</param>
    /// <returns>A Base64-encoded cursor token in format "collectionId:itemId".</returns>
    /// <exception cref="ArgumentException">Thrown when collection or item ID is invalid or too long.</exception>
    /// <remarks>
    /// <para>
    /// STAC uses cursor-based pagination where tokens encode the position of the last returned item.
    /// The next page starts after this item based on lexicographic ordering (collectionId, then itemId).
    /// </para>
    /// <para>
    /// Base64 encoding provides URL-safe tokens and prevents issues with special characters in IDs.
    /// Maximum part length of 128 characters prevents excessively large tokens.
    /// </para>
    /// </remarks>
    public static string GenerateCursorToken(string collectionId, string itemId)
    {
        Guard.NotNullOrWhiteSpace(collectionId);
        Guard.NotNullOrWhiteSpace(itemId);

        if (collectionId.Length > MaxCursorPartLength)
        {
            throw new ArgumentException($"Collection ID exceeds maximum length of {MaxCursorPartLength} characters.", nameof(collectionId));
        }

        if (itemId.Length > MaxCursorPartLength)
        {
            throw new ArgumentException($"Item ID exceeds maximum length of {MaxCursorPartLength} characters.", nameof(itemId));
        }

        var tokenValue = $"{collectionId}:{itemId}";
        var bytes = Encoding.UTF8.GetBytes(tokenValue);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Parses a Base64-encoded cursor token into collection and item IDs for STAC pagination.
    /// </summary>
    /// <param name="token">The Base64-encoded cursor token.</param>
    /// <returns>
    /// A tuple containing (collectionId, itemId) if valid, or (null, null) if the token is invalid.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Validates token format, length, and encoding. Invalid tokens are rejected gracefully
    /// by returning null values rather than throwing exceptions to prevent information disclosure.
    /// </para>
    /// <para>
    /// Security: Validates token length before decoding to prevent buffer exhaustion attacks.
    /// Validates decoded content format and character safety.
    /// </para>
    /// </remarks>
    public static (string? CollectionId, string? ItemId) ParseCursorToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return (null, null);
        }

        if (token.Length > MaxTokenLength)
        {
            return (null, null);
        }

        try
        {
            var bytes = Convert.FromBase64String(token);
            var decoded = Encoding.UTF8.GetString(bytes);

            if (decoded.Length > MaxTokenLength)
            {
                return (null, null);
            }

            var parts = decoded.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return (null, null);
            }

            var collectionId = parts[0];
            var itemId = parts[1];

            // Validate part lengths
            if (collectionId.Length > MaxCursorPartLength || itemId.Length > MaxCursorPartLength)
            {
                return (null, null);
            }

            // Validate that parts contain safe characters (alphanumeric, hyphen, underscore, period)
            if (!IsSafeToken(collectionId) || !IsSafeToken(itemId))
            {
                return (null, null);
            }

            return (collectionId, itemId);
        }
        catch
        {
            // Invalid Base64 or UTF-8 encoding
            return (null, null);
        }
    }

    /// <summary>
    /// Generates a simple offset-based cursor token for offset pagination.
    /// </summary>
    /// <param name="offset">The offset value for the next page.</param>
    /// <param name="limit">The page size limit.</param>
    /// <returns>A Base64-encoded token containing "offset:limit".</returns>
    /// <remarks>
    /// This is useful for protocols that want cursor-style tokens but use offset-based pagination internally.
    /// For OGC API and Records, prefer using offset parameters directly via RequestLinkHelper.
    /// </remarks>
    public static string GenerateOffsetToken(int offset, int limit)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be non-negative.");
        }

        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be at least 1.");
        }

        var tokenValue = $"{offset}:{limit}";
        var bytes = Encoding.UTF8.GetBytes(tokenValue);
        return Convert.ToBase64String(bytes);
    }

    /// <summary>
    /// Parses an offset-based cursor token into offset and limit values.
    /// </summary>
    /// <param name="token">The Base64-encoded offset token.</param>
    /// <returns>
    /// A tuple containing (offset, limit) if valid, or null if the token is invalid.
    /// </returns>
    public static (int Offset, int Limit)? ParseOffsetToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        if (token.Length > MaxTokenLength)
        {
            return null;
        }

        try
        {
            var bytes = Convert.FromBase64String(token);
            var decoded = Encoding.UTF8.GetString(bytes);

            var parts = decoded.Split(':', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2)
            {
                return null;
            }

            if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var offset) ||
                !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var limit))
            {
                return null;
            }

            if (offset < 0 || limit <= 0)
            {
                return null;
            }

            return (offset, limit);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Builds standardized pagination context metadata for OGC API and Records responses.
    /// </summary>
    /// <param name="returned">The number of items returned in the current page.</param>
    /// <param name="matched">The total number of items matching the query (may be null if unknown).</param>
    /// <param name="limit">The requested page size limit.</param>
    /// <returns>An object containing numberMatched, numberReturned, and limit metadata.</returns>
    /// <remarks>
    /// Used by OGC API Features and Records API to provide standardized pagination metadata.
    /// Matched count may be null for performance reasons (e.g., expensive COUNT queries).
    /// </remarks>
    public static object BuildPaginationContext(int returned, long? matched, int limit)
    {
        if (returned < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(returned), returned, "Returned count must be non-negative.");
        }

        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be non-negative.");
        }

        if (matched.HasValue)
        {
            return new
            {
                numberMatched = matched.Value,
                numberReturned = returned,
                limit
            };
        }

        return new
        {
            numberReturned = returned,
            limit
        };
    }

    /// <summary>
    /// Builds STAC-specific context metadata with matched/returned counts.
    /// </summary>
    /// <param name="returned">The number of items returned in the current page.</param>
    /// <param name="matched">The total number of items matching the query (may be -1 if unknown).</param>
    /// <param name="limit">The requested page size limit.</param>
    /// <returns>A JsonObject containing STAC context metadata.</returns>
    /// <remarks>
    /// <para>
    /// STAC uses a "context" object in search responses with matched/returned/limit fields.
    /// When matched is -1, it indicates the count is unknown (for performance optimization).
    /// </para>
    /// <para>
    /// This differs from OGC API which uses numberMatched/numberReturned at the root level.
    /// </para>
    /// </remarks>
    public static JsonObject BuildStacContext(int returned, int matched, int limit)
    {
        if (returned < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(returned), returned, "Returned count must be non-negative.");
        }

        if (limit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be non-negative.");
        }

        var context = new JsonObject
        {
            ["returned"] = returned
        };

        if (matched >= 0)
        {
            context["matched"] = matched;
        }

        if (limit > 0)
        {
            context["limit"] = limit;
        }

        return context;
    }

    /// <summary>
    /// Builds OGC API-specific context metadata (simple returned/matched counts).
    /// </summary>
    /// <param name="returned">The number of items returned in the current page.</param>
    /// <param name="matched">The total number of items matching the query (may be null if unknown).</param>
    /// <returns>An object containing numberReturned and optional numberMatched.</returns>
    public static object BuildOgcContext(int returned, long? matched)
    {
        if (returned < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(returned), returned, "Returned count must be non-negative.");
        }

        if (matched.HasValue)
        {
            return new
            {
                numberReturned = returned,
                numberMatched = matched.Value
            };
        }

        return new
        {
            numberReturned = returned
        };
    }

    /// <summary>
    /// Calculates the offset for the next page in offset-based pagination.
    /// </summary>
    /// <param name="currentOffset">The current page offset.</param>
    /// <param name="limit">The page size limit.</param>
    /// <param name="returned">The number of items actually returned (used to determine if more pages exist).</param>
    /// <returns>The offset for the next page, or null if no next page exists.</returns>
    public static int? CalculateNextOffset(int currentOffset, int limit, int returned)
    {
        if (currentOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentOffset), currentOffset, "Current offset must be non-negative.");
        }

        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be at least 1.");
        }

        if (returned < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(returned), returned, "Returned count must be non-negative.");
        }

        // Only return next offset if we got a full page (more results may exist)
        if (returned < limit)
        {
            return null;
        }

        return currentOffset + limit;
    }

    /// <summary>
    /// Calculates the offset for the previous page in offset-based pagination.
    /// </summary>
    /// <param name="currentOffset">The current page offset.</param>
    /// <param name="limit">The page size limit.</param>
    /// <returns>The offset for the previous page, or null if on the first page.</returns>
    public static int? CalculatePrevOffset(int currentOffset, int limit)
    {
        if (currentOffset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(currentOffset), currentOffset, "Current offset must be non-negative.");
        }

        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be at least 1.");
        }

        if (currentOffset == 0)
        {
            return null;
        }

        return Math.Max(currentOffset - limit, 0);
    }

    /// <summary>
    /// Determines if a next page exists based on the number of items returned.
    /// </summary>
    /// <param name="returned">The number of items returned in the current page.</param>
    /// <param name="limit">The page size limit.</param>
    /// <returns>True if more pages exist (full page returned), false otherwise.</returns>
    public static bool HasNextPage(int returned, int limit)
    {
        if (returned < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(returned), returned, "Returned count must be non-negative.");
        }

        if (limit < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), limit, "Limit must be at least 1.");
        }

        return returned >= limit;
    }

    /// <summary>
    /// Determines if a previous page exists based on the current offset.
    /// </summary>
    /// <param name="offset">The current page offset.</param>
    /// <returns>True if previous pages exist (offset > 0), false otherwise.</returns>
    public static bool HasPrevPage(int offset)
    {
        if (offset < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Offset must be non-negative.");
        }

        return offset > 0;
    }

    /// <summary>
    /// Validates that a string contains only safe characters for use in tokens.
    /// Allows alphanumeric characters, hyphen, underscore, period, and space.
    /// </summary>
    private static bool IsSafeToken(string value)
    {
        foreach (var ch in value)
        {
            if (!char.IsLetterOrDigit(ch) && ch != '-' && ch != '_' && ch != '.' && ch != ' ')
            {
                return false;
            }
        }

        return true;
    }
}
