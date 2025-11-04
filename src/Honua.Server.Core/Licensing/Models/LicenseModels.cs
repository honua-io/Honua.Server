// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Licensing.Models;

/// <summary>
/// Represents license tier levels with different feature sets and quotas.
/// </summary>
public enum LicenseTier
{
    /// <summary>Free tier with basic features</summary>
    Free = 0,

    /// <summary>Professional tier with advanced features</summary>
    Professional = 1,

    /// <summary>Enterprise tier with all features and priority support</summary>
    Enterprise = 2
}

/// <summary>
/// Represents the current status of a license.
/// </summary>
public enum LicenseStatus
{
    /// <summary>License is active and valid</summary>
    Active = 0,

    /// <summary>License is expired</summary>
    Expired = 1,

    /// <summary>License is suspended</summary>
    Suspended = 2,

    /// <summary>License has been revoked</summary>
    Revoked = 3,

    /// <summary>License is pending activation</summary>
    Pending = 4
}

/// <summary>
/// Represents features enabled for a license.
/// </summary>
public sealed class LicenseFeatures
{
    /// <summary>Maximum number of users allowed</summary>
    public int MaxUsers { get; set; } = 1;

    /// <summary>Maximum number of collections/layers</summary>
    public int MaxCollections { get; set; } = 10;

    /// <summary>Whether advanced analytics are enabled</summary>
    public bool AdvancedAnalytics { get; set; }

    /// <summary>Whether cloud integrations (AWS, Azure, GCP) are enabled</summary>
    public bool CloudIntegrations { get; set; }

    /// <summary>Whether STAC catalog features are enabled</summary>
    public bool StacCatalog { get; set; }

    /// <summary>Whether raster processing is enabled</summary>
    public bool RasterProcessing { get; set; }

    /// <summary>Whether vector tile generation is enabled</summary>
    public bool VectorTiles { get; set; }

    /// <summary>Whether priority support is included</summary>
    public bool PrioritySupport { get; set; }

    /// <summary>Maximum API requests per day (0 = unlimited)</summary>
    public int MaxApiRequestsPerDay { get; set; }

    /// <summary>Maximum storage in GB (0 = unlimited)</summary>
    public int MaxStorageGb { get; set; }

    /// <summary>
    /// Gets default features for a specific tier.
    /// </summary>
    public static LicenseFeatures GetDefaultForTier(LicenseTier tier)
    {
        return tier switch
        {
            LicenseTier.Free => new LicenseFeatures
            {
                MaxUsers = 1,
                MaxCollections = 10,
                AdvancedAnalytics = false,
                CloudIntegrations = false,
                StacCatalog = false,
                RasterProcessing = false,
                VectorTiles = true,
                PrioritySupport = false,
                MaxApiRequestsPerDay = 10000,
                MaxStorageGb = 5
            },
            LicenseTier.Professional => new LicenseFeatures
            {
                MaxUsers = 10,
                MaxCollections = 100,
                AdvancedAnalytics = true,
                CloudIntegrations = true,
                StacCatalog = true,
                RasterProcessing = true,
                VectorTiles = true,
                PrioritySupport = false,
                MaxApiRequestsPerDay = 100000,
                MaxStorageGb = 100
            },
            LicenseTier.Enterprise => new LicenseFeatures
            {
                MaxUsers = 0, // unlimited
                MaxCollections = 0, // unlimited
                AdvancedAnalytics = true,
                CloudIntegrations = true,
                StacCatalog = true,
                RasterProcessing = true,
                VectorTiles = true,
                PrioritySupport = true,
                MaxApiRequestsPerDay = 0, // unlimited
                MaxStorageGb = 0 // unlimited
            },
            _ => new LicenseFeatures()
        };
    }
}

/// <summary>
/// Represents complete license information.
/// </summary>
public sealed class LicenseInfo
{
    /// <summary>Unique license identifier</summary>
    public Guid Id { get; set; }

    /// <summary>Customer identifier</summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>JWT license key</summary>
    public string LicenseKey { get; set; } = string.Empty;

    /// <summary>License tier</summary>
    public LicenseTier Tier { get; set; }

    /// <summary>Current license status</summary>
    public LicenseStatus Status { get; set; }

    /// <summary>When the license was issued</summary>
    public DateTimeOffset IssuedAt { get; set; }

    /// <summary>When the license expires</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>License features and quotas</summary>
    public LicenseFeatures Features { get; set; } = new();

    /// <summary>When the license was revoked (if applicable)</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    /// <summary>Customer email address</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Additional metadata</summary>
    public Dictionary<string, string>? Metadata { get; set; }

    /// <summary>
    /// Checks if the license is currently valid.
    /// </summary>
    public bool IsValid()
    {
        return Status == LicenseStatus.Active &&
               ExpiresAt > DateTimeOffset.UtcNow &&
               RevokedAt == null;
    }

    /// <summary>
    /// Checks if the license is expired.
    /// </summary>
    public bool IsExpired()
    {
        return ExpiresAt <= DateTimeOffset.UtcNow || Status == LicenseStatus.Expired;
    }

    /// <summary>
    /// Gets the number of days until expiration.
    /// </summary>
    public int DaysUntilExpiration()
    {
        var remaining = ExpiresAt - DateTimeOffset.UtcNow;
        return Math.Max(0, (int)remaining.TotalDays);
    }
}

/// <summary>
/// Result of license validation.
/// </summary>
public sealed class LicenseValidationResult
{
    /// <summary>Whether the license is valid</summary>
    public bool IsValid { get; set; }

    /// <summary>License information if valid</summary>
    public LicenseInfo? License { get; set; }

    /// <summary>Error message if invalid</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Validation error code</summary>
    public LicenseValidationError ErrorCode { get; set; }

    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    public static LicenseValidationResult Success(LicenseInfo license)
    {
        return new LicenseValidationResult
        {
            IsValid = true,
            License = license,
            ErrorCode = LicenseValidationError.None
        };
    }

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    public static LicenseValidationResult Failure(LicenseValidationError errorCode, string errorMessage)
    {
        return new LicenseValidationResult
        {
            IsValid = false,
            ErrorMessage = errorMessage,
            ErrorCode = errorCode
        };
    }
}

/// <summary>
/// License validation error codes.
/// </summary>
public enum LicenseValidationError
{
    None = 0,
    InvalidFormat = 1,
    Expired = 2,
    Revoked = 3,
    InvalidSignature = 4,
    NotFound = 5,
    Suspended = 6,
    InvalidIssuer = 7,
    InvalidAudience = 8
}

/// <summary>
/// Request for generating a new license.
/// </summary>
public sealed class LicenseGenerationRequest
{
    /// <summary>Customer identifier</summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>Customer email address</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>License tier to generate</summary>
    public LicenseTier Tier { get; set; }

    /// <summary>License duration in days</summary>
    public int DurationDays { get; set; } = 365;

    /// <summary>Custom features (optional, uses tier defaults if not specified)</summary>
    public LicenseFeatures? CustomFeatures { get; set; }

    /// <summary>Additional metadata</summary>
    public Dictionary<string, string>? Metadata { get; set; }
}

/// <summary>
/// Represents a credential revocation record.
/// </summary>
public sealed class CredentialRevocation
{
    /// <summary>Unique revocation identifier</summary>
    public int Id { get; set; }

    /// <summary>Customer whose credentials were revoked</summary>
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>Type of registry (AWS, Azure, GCP, GitHub)</summary>
    public string RegistryType { get; set; } = string.Empty;

    /// <summary>When the credentials were revoked</summary>
    public DateTimeOffset RevokedAt { get; set; }

    /// <summary>Reason for revocation</summary>
    public string? Reason { get; set; }

    /// <summary>Who initiated the revocation</summary>
    public string? RevokedBy { get; set; }
}

/// <summary>
/// License configuration options.
/// </summary>
public sealed class LicenseOptions
{
    public const string SectionName = "honua:licensing";

    /// <summary>JWT signing key (base64 encoded, 256+ bits)</summary>
    public string SigningKey { get; set; } = string.Empty;

    /// <summary>JWT issuer (default: https://license.honua.io)</summary>
    public string Issuer { get; set; } = "https://license.honua.io";

    /// <summary>JWT audience (default: honua-server)</summary>
    public string Audience { get; set; } = "honua-server";

    /// <summary>License database connection string</summary>
    public string? ConnectionString { get; set; }

    /// <summary>License database provider (postgres, mysql, sqlite)</summary>
    public string Provider { get; set; } = "postgres";

    /// <summary>How often to check for expiring licenses (default: 1 hour)</summary>
    public TimeSpan ExpirationCheckInterval { get; set; } = TimeSpan.FromHours(1);

    /// <summary>Warning threshold in days (default: 7 days)</summary>
    public int WarningThresholdDays { get; set; } = 7;

    /// <summary>Enable automatic credential revocation on expiration</summary>
    public bool EnableAutomaticRevocation { get; set; } = true;

    /// <summary>SMTP settings for sending expiration warning emails</summary>
    public SmtpOptions? Smtp { get; set; }
}

/// <summary>
/// SMTP configuration for license notifications.
/// </summary>
public sealed class SmtpOptions
{
    /// <summary>SMTP server hostname</summary>
    public string Host { get; set; } = string.Empty;

    /// <summary>SMTP server port (default: 587)</summary>
    public int Port { get; set; } = 587;

    /// <summary>Enable TLS/SSL</summary>
    public bool EnableSsl { get; set; } = true;

    /// <summary>SMTP username</summary>
    public string? Username { get; set; }

    /// <summary>SMTP password</summary>
    public string? Password { get; set; }

    /// <summary>From email address</summary>
    public string FromEmail { get; set; } = "noreply@honua.io";

    /// <summary>From display name</summary>
    public string FromName { get; set; } = "Honua Licensing";
}
