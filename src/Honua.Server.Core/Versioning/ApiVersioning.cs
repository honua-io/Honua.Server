// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;

namespace Honua.Server.Core.Versioning;

/// <summary>
/// Provides constants and utilities for API versioning.
/// </summary>
/// <remarks>
/// The Honua API uses URL-based versioning with major version numbers.
/// Version format: /v{major}/ (e.g., /v1/, /v2/)
///
/// This approach provides:
/// - Clear and visible versioning in URLs
/// - Easy testing of different versions
/// - Browser-friendly URLs
/// - Compatibility with all HTTP clients
/// - Compliance with OGC API standards
/// </remarks>
public static class ApiVersioning
{
    /// <summary>
    /// The current production API version.
    /// </summary>
    public const string CurrentVersion = "v1";

    /// <summary>
    /// All versions currently supported by the API.
    /// </summary>
    /// <remarks>
    /// When introducing breaking changes, add new versions to this list.
    /// Deprecated versions should remain in this list until they are removed.
    /// </remarks>
    public static readonly IReadOnlyList<string> SupportedVersions = new[] { "v1" };

    /// <summary>
    /// The default version to use when no version is specified in the request.
    /// </summary>
    public const string DefaultVersion = "v1";

    /// <summary>
    /// The minimum supported API version.
    /// Versions older than this are no longer supported.
    /// </summary>
    public const string MinimumVersion = "v1";

    /// <summary>
    /// The version prefix used in URL paths.
    /// </summary>
    public const string VersionPrefix = "v";

    /// <summary>
    /// Checks if a version string is supported.
    /// </summary>
    /// <param name="version">The version string to check (e.g., "v1").</param>
    /// <returns>True if the version is supported, false otherwise.</returns>
    public static bool IsVersionSupported(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        return SupportedVersions.Contains(version, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Parses a version string to extract the major version number.
    /// </summary>
    /// <param name="version">The version string (e.g., "v1", "v2").</param>
    /// <param name="majorVersion">When this method returns, contains the major version number if the parse succeeded.</param>
    /// <returns>True if the version was successfully parsed, false otherwise.</returns>
    public static bool TryParseVersion(string? version, out int majorVersion)
    {
        majorVersion = 0;

        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        // Remove 'v' prefix if present
        var versionNumber = version.StartsWith(VersionPrefix, StringComparison.OrdinalIgnoreCase)
            ? version[VersionPrefix.Length..]
            : version;

        return int.TryParse(versionNumber, out majorVersion);
    }

    /// <summary>
    /// Formats a major version number as a version string.
    /// </summary>
    /// <param name="majorVersion">The major version number.</param>
    /// <returns>The formatted version string (e.g., "v1").</returns>
    public static string FormatVersion(int majorVersion)
    {
        return $"{VersionPrefix}{majorVersion}";
    }
}

/// <summary>
/// Represents a parsed API version.
/// </summary>
public sealed class ApiVersion : IEquatable<ApiVersion>
{
    /// <summary>
    /// Gets the major version number.
    /// </summary>
    public int Major { get; }

    /// <summary>
    /// Gets the optional minor version number.
    /// </summary>
    public int? Minor { get; }

    /// <summary>
    /// Gets the version string representation.
    /// </summary>
    public string VersionString => Minor.HasValue ? $"v{Major}.{Minor}" : $"v{Major}";

    /// <summary>
    /// Initializes a new instance of the <see cref="ApiVersion"/> class.
    /// </summary>
    /// <param name="major">The major version number.</param>
    /// <param name="minor">The optional minor version number.</param>
    public ApiVersion(int major, int? minor = null)
    {
        if (major < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(major), "Major version must be greater than 0.");
        }

        if (minor.HasValue && minor.Value < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(minor), "Minor version must be greater than or equal to 0.");
        }

        Major = major;
        Minor = minor;
    }

    /// <summary>
    /// Parses a version string to an ApiVersion instance.
    /// </summary>
    /// <param name="version">The version string (e.g., "v1", "v1.0").</param>
    /// <returns>The parsed ApiVersion instance.</returns>
    /// <exception cref="FormatException">Thrown if the version string is invalid.</exception>
    public static ApiVersion Parse(string version)
    {
        if (TryParse(version, out var result))
        {
            return result;
        }

        throw new FormatException($"Invalid version format: {version}");
    }

    /// <summary>
    /// Tries to parse a version string to an ApiVersion instance.
    /// </summary>
    /// <param name="version">The version string (e.g., "v1", "v1.0").</param>
    /// <param name="result">When this method returns, contains the parsed ApiVersion if successful.</param>
    /// <returns>True if the version was successfully parsed, false otherwise.</returns>
    public static bool TryParse(string? version, out ApiVersion result)
    {
        result = null!;

        if (string.IsNullOrWhiteSpace(version))
        {
            return false;
        }

        // Remove 'v' prefix if present
        var versionNumber = version.StartsWith(ApiVersioning.VersionPrefix, StringComparison.OrdinalIgnoreCase)
            ? version[ApiVersioning.VersionPrefix.Length..]
            : version;

        var parts = versionNumber.Split('.');
        if (parts.Length == 0 || parts.Length > 2)
        {
            return false;
        }

        if (!int.TryParse(parts[0], out var major) || major < 1)
        {
            return false;
        }

        int? minor = null;
        if (parts.Length == 2)
        {
            if (!int.TryParse(parts[1], out var minorValue) || minorValue < 0)
            {
                return false;
            }
            minor = minorValue;
        }

        result = new ApiVersion(major, minor);
        return true;
    }

    /// <inheritdoc/>
    public bool Equals(ApiVersion? other)
    {
        if (other is null)
        {
            return false;
        }

        return Major == other.Major && Minor == other.Minor;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj)
    {
        return obj is ApiVersion other && Equals(other);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Major, Minor);
    }

    /// <inheritdoc/>
    public override string ToString()
    {
        return VersionString;
    }

    public static bool operator ==(ApiVersion? left, ApiVersion? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(ApiVersion? left, ApiVersion? right)
    {
        return !Equals(left, right);
    }
}
