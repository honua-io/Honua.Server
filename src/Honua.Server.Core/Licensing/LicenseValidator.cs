// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Licensing.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Honua.Server.Core.Licensing;

/// <summary>
/// Service for validating JWT-based license keys and checking license permissions.
/// </summary>
public interface ILicenseValidator
{
    /// <summary>
    /// Validates a JWT license key and extracts license information.
    /// </summary>
    Task<LicenseValidationResult> ValidateAsync(string licenseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets detailed license information from a valid license key.
    /// </summary>
    Task<LicenseInfo?> GetLicenseInfoAsync(string licenseKey, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a license is expired.
    /// </summary>
    Task<bool> CheckExpirationAsync(string customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates if a license permits access to a specific tier/feature.
    /// </summary>
    Task<bool> ValidateTierAccessAsync(string customerId, LicenseTier requiredTier, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of license validation service using JWT tokens.
/// </summary>
public sealed class LicenseValidator : ILicenseValidator
{
    private readonly ILicenseStore _licenseStore;
    private readonly IOptionsMonitor<LicenseOptions> _options;
    private readonly ILogger<LicenseValidator> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    public LicenseValidator(
        ILicenseStore licenseStore,
        IOptionsMonitor<LicenseOptions> options,
        ILogger<LicenseValidator> logger)
    {
        _licenseStore = licenseStore ?? throw new ArgumentNullException(nameof(licenseStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    public async Task<LicenseValidationResult> ValidateAsync(
        string licenseKey,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            return LicenseValidationResult.Failure(
                LicenseValidationError.InvalidFormat,
                "License key is required");
        }

        try
        {
            // Parse JWT token
            if (!_tokenHandler.CanReadToken(licenseKey))
            {
                _logger.LogWarning("Invalid JWT format for license key");
                return LicenseValidationResult.Failure(
                    LicenseValidationError.InvalidFormat,
                    "Invalid license key format");
            }

            var opts = _options.CurrentValue;
            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey));

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = opts.Issuer,
                ValidateAudience = true,
                ValidAudience = opts.Audience,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = signingKey,
                ClockSkew = TimeSpan.FromMinutes(5)
            };

            // Validate JWT signature and claims
            ClaimsPrincipal principal;
            SecurityToken validatedToken;
            try
            {
                principal = _tokenHandler.ValidateToken(licenseKey, validationParameters, out validatedToken);
            }
            catch (SecurityTokenExpiredException)
            {
                _logger.LogWarning("License key has expired");
                return LicenseValidationResult.Failure(
                    LicenseValidationError.Expired,
                    "License has expired");
            }
            catch (SecurityTokenInvalidSignatureException)
            {
                _logger.LogWarning("License key has invalid signature");
                return LicenseValidationResult.Failure(
                    LicenseValidationError.InvalidSignature,
                    "License signature is invalid");
            }
            catch (SecurityTokenInvalidIssuerException)
            {
                _logger.LogWarning("License key has invalid issuer");
                return LicenseValidationResult.Failure(
                    LicenseValidationError.InvalidIssuer,
                    "License issuer is invalid");
            }
            catch (SecurityTokenInvalidAudienceException)
            {
                _logger.LogWarning("License key has invalid audience");
                return LicenseValidationResult.Failure(
                    LicenseValidationError.InvalidAudience,
                    "License audience is invalid");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating license key");
                return LicenseValidationResult.Failure(
                    LicenseValidationError.InvalidFormat,
                    "License validation failed");
            }

            // Extract license information from claims
            var customerId = principal.FindFirst("customer_id")?.Value;
            if (string.IsNullOrWhiteSpace(customerId))
            {
                return LicenseValidationResult.Failure(
                    LicenseValidationError.InvalidFormat,
                    "License is missing customer_id claim");
            }

            // Check if license exists in database and verify status
            var storedLicense = await _licenseStore.GetByCustomerIdAsync(customerId, cancellationToken);
            if (storedLicense == null)
            {
                _logger.LogWarning("License not found in database for customer {CustomerId}", customerId);
                return LicenseValidationResult.Failure(
                    LicenseValidationError.NotFound,
                    "License not found");
            }

            // Check if license has been revoked
            if (storedLicense.Status == LicenseStatus.Revoked || storedLicense.RevokedAt != null)
            {
                _logger.LogWarning("License has been revoked for customer {CustomerId}", customerId);
                return LicenseValidationResult.Failure(
                    LicenseValidationError.Revoked,
                    "License has been revoked");
            }

            // Check if license is suspended
            if (storedLicense.Status == LicenseStatus.Suspended)
            {
                _logger.LogWarning("License is suspended for customer {CustomerId}", customerId);
                return LicenseValidationResult.Failure(
                    LicenseValidationError.Suspended,
                    "License is suspended");
            }

            // Check if license is expired
            if (storedLicense.IsExpired())
            {
                _logger.LogWarning("License is expired for customer {CustomerId}", customerId);

                // Update status to expired if not already
                if (storedLicense.Status != LicenseStatus.Expired)
                {
                    storedLicense.Status = LicenseStatus.Expired;
                    await _licenseStore.UpdateAsync(storedLicense, cancellationToken);
                }

                return LicenseValidationResult.Failure(
                    LicenseValidationError.Expired,
                    "License has expired");
            }

            _logger.LogInformation(
                "License validated successfully for customer {CustomerId}, tier {Tier}",
                customerId,
                storedLicense.Tier);

            return LicenseValidationResult.Success(storedLicense);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during license validation");
            return LicenseValidationResult.Failure(
                LicenseValidationError.InvalidFormat,
                "License validation failed");
        }
    }

    public async Task<LicenseInfo?> GetLicenseInfoAsync(
        string licenseKey,
        CancellationToken cancellationToken = default)
    {
        var validationResult = await ValidateAsync(licenseKey, cancellationToken);
        return validationResult.IsValid ? validationResult.License : null;
    }

    public async Task<bool> CheckExpirationAsync(
        string customerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return true; // Treat missing customer as expired
        }

        var license = await _licenseStore.GetByCustomerIdAsync(customerId, cancellationToken);
        return license?.IsExpired() ?? true;
    }

    public async Task<bool> ValidateTierAccessAsync(
        string customerId,
        LicenseTier requiredTier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return false;
        }

        var license = await _licenseStore.GetByCustomerIdAsync(customerId, cancellationToken);
        if (license == null || !license.IsValid())
        {
            return false;
        }

        // Check if license tier is equal to or higher than required tier
        return license.Tier >= requiredTier;
    }
}

/// <summary>
/// Interface for license data storage operations.
/// </summary>
public interface ILicenseStore
{
    /// <summary>
    /// Gets a license by customer ID.
    /// </summary>
    Task<LicenseInfo?> GetByCustomerIdAsync(string customerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a license by license ID.
    /// </summary>
    Task<LicenseInfo?> GetByIdAsync(Guid licenseId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new license.
    /// </summary>
    Task<LicenseInfo> CreateAsync(LicenseInfo license, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing license.
    /// </summary>
    Task UpdateAsync(LicenseInfo license, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all licenses expiring within the specified number of days.
    /// </summary>
    Task<LicenseInfo[]> GetExpiringLicensesAsync(int daysFromNow, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all expired licenses.
    /// </summary>
    Task<LicenseInfo[]> GetExpiredLicensesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first active license (for single-tenant deployments).
    /// </summary>
    Task<LicenseInfo?> GetFirstActiveLicenseAsync(CancellationToken cancellationToken = default);
}
