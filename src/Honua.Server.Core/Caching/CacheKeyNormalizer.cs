// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Utilities;

namespace Honua.Server.Core.Caching;

/// <summary>
/// Provides utilities for normalizing and generating cache keys across different caching backends.
/// Ensures consistent, safe, and collision-resistant cache key generation.
/// </summary>
/// <remarks>
/// This utility consolidates cache key generation patterns to:
/// - Provide deterministic key generation (same input = same output)
/// - Enforce length limits (default 250 characters for Redis/S3 compatibility)
/// - Apply URL-safe encoding and sanitization
/// - Generate hash-based keys for long inputs
/// - Support platform-specific sanitization (Redis, S3, GCS, Filesystem)
/// </remarks>
public static class CacheKeyNormalizer
{
    private const int DefaultMaxLength = 250;
    private const char DefaultSeparator = ':';
    private const int HashPrefixLength = 16; // 64 bits of SHA256 hash
    private static readonly char[] InvalidFileNameChars = Path.GetInvalidFileNameChars();

    /// <summary>
    /// Normalizes a cache key to be URL-safe and within length limits.
    /// </summary>
    /// <param name="key">The cache key to normalize.</param>
    /// <param name="maxLength">Maximum key length (default: 250 characters).</param>
    /// <returns>A normalized cache key.</returns>
    /// <example>
    /// <code>
    /// var key = CacheKeyNormalizer.Normalize("my/cache/key with spaces!", 100);
    /// // Result: "my-cache-key-with-spaces"
    /// </code>
    /// </example>
    public static string Normalize(string key, int maxLength = DefaultMaxLength)
    {
        Guard.NotNullOrWhiteSpace(key);
        Guard.Require(maxLength > 0, "Max length must be greater than 0");

        if (key.Length <= maxLength && IsAlreadyNormalized(key))
        {
            return key;
        }

        // Sanitize characters
        var sanitized = SanitizeForGeneric(key);

        // If still too long after sanitization, hash it
        if (sanitized.Length > maxLength)
        {
            // Keep a readable prefix + hash suffix for debugging
            var hashSuffix = GenerateCompactHash(key);
            var prefixLength = Math.Max(0, maxLength - hashSuffix.Length - 1);

            if (prefixLength > 0)
            {
                var prefix = sanitized.Substring(0, Math.Min(prefixLength, sanitized.Length));
                return $"{prefix}-{hashSuffix}";
            }

            return hashSuffix.Substring(0, Math.Min(maxLength, hashSuffix.Length));
        }

        return sanitized;
    }

    /// <summary>
    /// Combines multiple parts into a single cache key with a separator.
    /// </summary>
    /// <param name="parts">The parts to combine.</param>
    /// <returns>A combined cache key.</returns>
    /// <example>
    /// <code>
    /// var key = CacheKeyNormalizer.Combine("user", "123", "profile");
    /// // Result: "user:123:profile"
    /// </code>
    /// </example>
    public static string Combine(params string[] parts)
    {
        return Combine(DefaultSeparator, parts);
    }

    /// <summary>
    /// Combines multiple parts into a single cache key with a custom separator.
    /// </summary>
    /// <param name="separator">The separator character to use.</param>
    /// <param name="parts">The parts to combine.</param>
    /// <returns>A combined cache key.</returns>
    /// <example>
    /// <code>
    /// var key = CacheKeyNormalizer.Combine('/', "bucket", "prefix", "file.txt");
    /// // Result: "bucket/prefix/file.txt"
    /// </code>
    /// </example>
    public static string Combine(char separator, params string[] parts)
    {
        Guard.NotNull(parts);
        Guard.Require(parts.Length > 0, "At least one part is required");

        // Filter out null/empty parts
        var validParts = parts.Where(p => !p.IsNullOrWhiteSpace()).ToArray();

        if (validParts.Length == 0)
        {
            throw new ArgumentException("At least one non-empty part is required", nameof(parts));
        }

        if (validParts.Length == 1)
        {
            return validParts[0];
        }

        // Use string.Join for efficiency
        return string.Join(separator, validParts);
    }

    /// <summary>
    /// Generates a SHA256 hash-based cache key for long inputs.
    /// Returns a 64-character lowercase hex string.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>A 64-character SHA256 hash (lowercase hex).</returns>
    /// <example>
    /// <code>
    /// var hash = CacheKeyNormalizer.Hash("very-long-input-string-that-needs-hashing");
    /// // Result: "a1b2c3d4..." (64 hex characters)
    /// </code>
    /// </example>
    public static string Hash(string input)
    {
        Guard.NotNullOrWhiteSpace(input);

        var bytes = Encoding.UTF8.GetBytes(input);
        var hashBytes = SHA256.HashData(bytes);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    /// <summary>
    /// Combines multiple parts and generates a SHA256 hash of the result.
    /// Useful for creating deterministic cache keys from multiple components.
    /// </summary>
    /// <param name="parts">The parts to combine and hash.</param>
    /// <returns>A 64-character SHA256 hash (lowercase hex).</returns>
    /// <example>
    /// <code>
    /// var hash = CacheKeyNormalizer.HashCombine("dataset", "layer1", "2024-01-15");
    /// // Result: SHA256 hash of "dataset:layer1:2024-01-15"
    /// </code>
    /// </example>
    public static string HashCombine(params string[] parts)
    {
        Guard.NotNull(parts);
        Guard.Require(parts.Length > 0, "At least one part is required");

        var combined = Combine(DefaultSeparator, parts);
        return Hash(combined);
    }

    /// <summary>
    /// Generates a compact hash (first 16 hex characters of SHA256).
    /// Provides 64 bits of collision resistance while keeping keys readable.
    /// </summary>
    /// <param name="input">The input string to hash.</param>
    /// <returns>A 16-character hash prefix (lowercase hex).</returns>
    /// <example>
    /// <code>
    /// var compactHash = CacheKeyNormalizer.GenerateCompactHash("input");
    /// // Result: "a1b2c3d4e5f6g7h8" (16 hex characters)
    /// </code>
    /// </example>
    public static string GenerateCompactHash(string input)
    {
        Guard.NotNullOrWhiteSpace(input);

        var fullHash = Hash(input);
        return fullHash.Substring(0, HashPrefixLength);
    }

    /// <summary>
    /// Adds a prefix to a cache key.
    /// </summary>
    /// <param name="prefix">The prefix to add.</param>
    /// <param name="key">The cache key.</param>
    /// <returns>The prefixed cache key.</returns>
    /// <example>
    /// <code>
    /// var key = CacheKeyNormalizer.WithPrefix("honua", "metadata");
    /// // Result: "honua:metadata"
    /// </code>
    /// </example>
    public static string WithPrefix(string prefix, string key)
    {
        Guard.NotNullOrWhiteSpace(prefix);
        Guard.NotNullOrWhiteSpace(key);

        return $"{prefix}{DefaultSeparator}{key}";
    }

    /// <summary>
    /// Adds a suffix to a cache key.
    /// </summary>
    /// <param name="key">The cache key.</param>
    /// <param name="suffix">The suffix to add.</param>
    /// <returns>The suffixed cache key.</returns>
    /// <example>
    /// <code>
    /// var key = CacheKeyNormalizer.WithSuffix("snapshot", "v2");
    /// // Result: "snapshot:v2"
    /// </code>
    /// </example>
    public static string WithSuffix(string key, string suffix)
    {
        Guard.NotNullOrWhiteSpace(key);
        Guard.NotNullOrWhiteSpace(suffix);

        return $"{key}{DefaultSeparator}{suffix}";
    }

    /// <summary>
    /// Sanitizes a cache key for Redis.
    /// Redis keys support most characters but have length limits.
    /// </summary>
    /// <param name="key">The cache key to sanitize.</param>
    /// <returns>A Redis-safe cache key.</returns>
    /// <example>
    /// <code>
    /// var key = CacheKeyNormalizer.SanitizeForRedis("my cache key!");
    /// // Result: "my-cache-key"
    /// </code>
    /// </example>
    public static string SanitizeForRedis(string key)
    {
        Guard.NotNullOrWhiteSpace(key);

        // Redis supports most characters, but we normalize for consistency
        // Avoid spaces and special characters that could cause issues
        var builder = new StringBuilder(key.Length);

        foreach (var ch in key)
        {
            if (char.IsWhiteSpace(ch))
            {
                builder.Append('-');
            }
            else if (char.IsLetterOrDigit(ch) || ch == ':' || ch == '-' || ch == '_' || ch == '.' || ch == '/')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('-');
            }
        }

        var result = builder.ToString().Trim('-');
        return result.IsNullOrEmpty() ? "default" : result;
    }

    /// <summary>
    /// Sanitizes a cache key for filesystem usage.
    /// Removes characters that are invalid in file/directory names.
    /// </summary>
    /// <param name="key">The cache key to sanitize.</param>
    /// <returns>A filesystem-safe cache key.</returns>
    /// <example>
    /// <code>
    /// var key = CacheKeyNormalizer.SanitizeForFilesystem("my/cache\\key");
    /// // Result: "my-cache-key"
    /// </code>
    /// </example>
    public static string SanitizeForFilesystem(string key)
    {
        Guard.NotNullOrWhiteSpace(key);

        var builder = new StringBuilder(key.Length);

        foreach (var ch in key)
        {
            if (ch == '/' || ch == '\\' || char.IsWhiteSpace(ch))
            {
                builder.Append('-');
            }
            else if (char.IsControl(ch))
            {
                builder.Append('-');
            }
            else if (InvalidFileNameChars.Contains(ch))
            {
                builder.Append('-');
            }
            else if (char.IsLetterOrDigit(ch) || ch == '-' || ch == '_' || ch == '.')
            {
                builder.Append(char.ToLowerInvariant(ch));
            }
            else
            {
                builder.Append('-');
            }
        }

        var result = builder.ToString().Trim('-');

        // Limit length to prevent filesystem issues
        if (result.Length > 64)
        {
            result = result.Substring(0, 64).TrimEnd('-');
        }

        return result.IsNullOrEmpty() ? "default" : result;
    }

    /// <summary>
    /// Sanitizes a cache key for S3 object keys.
    /// S3 keys support most characters but have specific requirements.
    /// </summary>
    /// <param name="key">The cache key to sanitize.</param>
    /// <returns>An S3-safe cache key.</returns>
    /// <example>
    /// <code>
    /// var key = CacheKeyNormalizer.SanitizeForS3("my bucket/cache key");
    /// // Result: "my-bucket/cache-key"
    /// </code>
    /// </example>
    public static string SanitizeForS3(string key)
    {
        Guard.NotNullOrWhiteSpace(key);

        // S3 keys support: letters, numbers, /, !, -, _, ., *, ', (, )
        // We normalize spaces and most special characters for consistency
        var builder = new StringBuilder(key.Length);

        foreach (var ch in key)
        {
            if (char.IsWhiteSpace(ch) && ch != '/')
            {
                builder.Append('-');
            }
            else if (char.IsLetterOrDigit(ch) || ch == '/' || ch == '-' || ch == '_' || ch == '.')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('-');
            }
        }

        var result = builder.ToString().Trim('-');

        // Ensure no leading/trailing slashes (S3 best practice)
        result = result.Trim('/');

        return result.IsNullOrEmpty() ? "default" : result;
    }

    /// <summary>
    /// Normalizes a path for consistent hashing across platforms.
    /// Converts all backslashes to forward slashes and lowercases the result.
    /// </summary>
    /// <param name="path">The path to normalize.</param>
    /// <returns>A normalized path.</returns>
    /// <example>
    /// <code>
    /// var normalized = CacheKeyNormalizer.NormalizePath(@"C:\Data\File.txt");
    /// // Result: "c:/data/file.txt"
    /// </code>
    /// </example>
    public static string NormalizePath(string path)
    {
        Guard.NotNullOrWhiteSpace(path);

        return path.Replace('\\', '/').ToLowerInvariant();
    }

    /// <summary>
    /// Generates a versioned cache key with a version suffix.
    /// Useful for cache invalidation when data schemas change.
    /// </summary>
    /// <param name="key">The base cache key.</param>
    /// <param name="version">The version number.</param>
    /// <returns>A versioned cache key.</returns>
    /// <example>
    /// <code>
    /// var key = CacheKeyNormalizer.WithVersion("metadata:snapshot", 2);
    /// // Result: "metadata:snapshot:v2"
    /// </code>
    /// </example>
    public static string WithVersion(string key, int version)
    {
        Guard.NotNullOrWhiteSpace(key);
        Guard.Require(version >= 1, "Version must be >= 1");

        return $"{key}{DefaultSeparator}v{version.ToString(CultureInfo.InvariantCulture)}";
    }

    /// <summary>
    /// Creates a time-based cache key component from a DateTimeOffset.
    /// Formats as ISO 8601 compatible string (URL-safe).
    /// </summary>
    /// <param name="timestamp">The timestamp to format.</param>
    /// <returns>A time-based cache key component.</returns>
    /// <example>
    /// <code>
    /// var time = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
    /// var key = CacheKeyNormalizer.FormatTimestamp(time);
    /// // Result: "2024-01-15t10-30-00z"
    /// </code>
    /// </example>
    public static string FormatTimestamp(DateTimeOffset timestamp)
    {
        // ISO 8601 format but with dashes instead of colons for filesystem safety
        return timestamp.UtcDateTime
            .ToString("yyyy-MM-ddTHH-mm-ssZ", CultureInfo.InvariantCulture)
            .ToLowerInvariant();
    }

    /// <summary>
    /// Validates that a cache key contains only safe characters.
    /// </summary>
    /// <param name="key">The cache key to validate.</param>
    /// <returns>True if the key is safe, false otherwise.</returns>
    public static bool IsSafe(string key)
    {
        if (key.IsNullOrWhiteSpace())
        {
            return false;
        }

        // Check for control characters or excessive length
        if (key.Length > DefaultMaxLength)
        {
            return false;
        }

        foreach (var ch in key)
        {
            if (char.IsControl(ch))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Escapes special characters for use in Redis pattern matching (SCAN/KEYS).
    /// </summary>
    /// <param name="pattern">The pattern to escape.</param>
    /// <returns>An escaped pattern safe for Redis glob matching.</returns>
    /// <example>
    /// <code>
    /// var pattern = CacheKeyNormalizer.EscapeRedisPattern("user:*:profile");
    /// // Result: "user:\\*:profile" (literal asterisk)
    /// </code>
    /// </example>
    public static string EscapeRedisPattern(string pattern)
    {
        Guard.NotNullOrWhiteSpace(pattern);

        // Escape special glob characters: * ? [ ] \ ^
        return pattern
            .Replace("\\", "\\\\")
            .Replace("*", "\\*")
            .Replace("?", "\\?")
            .Replace("[", "\\[")
            .Replace("]", "\\]")
            .Replace("^", "\\^");
    }

    /// <summary>
    /// Generic sanitization for cache keys (used internally).
    /// </summary>
    private static string SanitizeForGeneric(string value)
    {
        var builder = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            if (char.IsWhiteSpace(ch))
            {
                builder.Append('-');
            }
            else if (char.IsControl(ch))
            {
                builder.Append('-');
            }
            else if (char.IsLetterOrDigit(ch) || ch == ':' || ch == '-' || ch == '_' || ch == '.' || ch == '/')
            {
                builder.Append(ch);
            }
            else
            {
                builder.Append('-');
            }
        }

        return builder.ToString().Trim('-');
    }

    /// <summary>
    /// Checks if a key is already normalized (fast path optimization).
    /// </summary>
    private static bool IsAlreadyNormalized(string key)
    {
        foreach (var ch in key)
        {
            // Check for any character that would need normalization
            if (char.IsWhiteSpace(ch) || char.IsControl(ch))
            {
                return false;
            }

            // Check for characters outside our safe set
            if (!(char.IsLetterOrDigit(ch) || ch == ':' || ch == '-' || ch == '_' || ch == '.' || ch == '/'))
            {
                return false;
            }
        }

        return true;
    }
}
