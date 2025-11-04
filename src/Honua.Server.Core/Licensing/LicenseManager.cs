// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Licensing.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Polly;

namespace Honua.Server.Core.Licensing;

/// <summary>
/// Service for managing license lifecycle operations (generate, upgrade, downgrade, revoke, renew).
/// </summary>
public interface ILicenseManager
{
    /// <summary>
    /// Generates a new JWT license for a customer.
    /// </summary>
    Task<LicenseInfo> GenerateLicenseAsync(LicenseGenerationRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upgrades a customer's license to a higher tier.
    /// </summary>
    Task<LicenseInfo> UpgradeLicenseAsync(string customerId, LicenseTier newTier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Downgrades a customer's license to a lower tier.
    /// </summary>
    Task<LicenseInfo> DowngradeLicenseAsync(string customerId, LicenseTier newTier, CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes a license and triggers credential cleanup.
    /// </summary>
    Task RevokeLicenseAsync(string customerId, string reason, string revokedBy, CancellationToken cancellationToken = default);

    /// <summary>
    /// Renews a license by extending its expiration date.
    /// </summary>
    Task<LicenseInfo> RenewLicenseAsync(string customerId, int extensionDays, CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of license lifecycle management service.
/// </summary>
public sealed class LicenseManager : ILicenseManager
{
    private readonly ILicenseStore _licenseStore;
    private readonly ICredentialRevocationService _credentialRevocationService;
    private readonly IOptionsMonitor<LicenseOptions> _options;
    private readonly ILogger<LicenseManager> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;
    private readonly ResiliencePipeline _resiliencePipeline;

    public LicenseManager(
        ILicenseStore licenseStore,
        ICredentialRevocationService credentialRevocationService,
        IOptionsMonitor<LicenseOptions> options,
        ILogger<LicenseManager> logger)
    {
        _licenseStore = licenseStore ?? throw new ArgumentNullException(nameof(licenseStore));
        _credentialRevocationService = credentialRevocationService ?? throw new ArgumentNullException(nameof(credentialRevocationService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenHandler = new JwtSecurityTokenHandler();

        // Create resilience pipeline for database operations
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromMilliseconds(100),
                BackoffType = Polly.DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Retrying license operation (attempt {Attempt})",
                        args.AttemptNumber + 1);
                    return default;
                }
            })
            .Build();
    }

    public async Task<LicenseInfo> GenerateLicenseAsync(
        LicenseGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.CustomerId))
        {
            throw new ArgumentException("CustomerId is required", nameof(request));
        }

        if (string.IsNullOrWhiteSpace(request.Email))
        {
            throw new ArgumentException("Email is required", nameof(request));
        }

        if (request.DurationDays <= 0)
        {
            throw new ArgumentException("DurationDays must be greater than zero", nameof(request));
        }

        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            // Check if customer already has a license
            var existingLicense = await _licenseStore.GetByCustomerIdAsync(request.CustomerId, token);
            if (existingLicense != null)
            {
                throw new InvalidOperationException(
                    $"Customer {request.CustomerId} already has a license. Use upgrade/downgrade instead.");
            }

            var now = DateTimeOffset.UtcNow;
            var expiresAt = now.AddDays(request.DurationDays);

            // Determine features
            var features = request.CustomFeatures ?? LicenseFeatures.GetDefaultForTier(request.Tier);

            // Generate JWT license key
            var licenseKey = GenerateJwtLicenseKey(
                request.CustomerId,
                request.Email,
                request.Tier,
                features,
                now,
                expiresAt);

            // Create license record
            var license = new LicenseInfo
            {
                Id = Guid.NewGuid(),
                CustomerId = request.CustomerId,
                Email = request.Email,
                LicenseKey = licenseKey,
                Tier = request.Tier,
                Status = LicenseStatus.Active,
                IssuedAt = now,
                ExpiresAt = expiresAt,
                Features = features,
                Metadata = request.Metadata
            };

            // Save to database
            var createdLicense = await _licenseStore.CreateAsync(license, token);

            _logger.LogInformation(
                "Generated new license for customer {CustomerId}, tier {Tier}, expires {ExpiresAt}",
                request.CustomerId,
                request.Tier,
                expiresAt);

            return createdLicense;
        }, cancellationToken);
    }

    public async Task<LicenseInfo> UpgradeLicenseAsync(
        string customerId,
        LicenseTier newTier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("CustomerId is required", nameof(customerId));
        }

        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var license = await _licenseStore.GetByCustomerIdAsync(customerId, token);
            if (license == null)
            {
                throw new InvalidOperationException($"License not found for customer {customerId}");
            }

            if (newTier <= license.Tier)
            {
                throw new InvalidOperationException(
                    $"Cannot upgrade to {newTier} from {license.Tier}. Use DowngradeLicenseAsync instead.");
            }

            // Update tier and features
            var oldTier = license.Tier;
            license.Tier = newTier;
            license.Features = LicenseFeatures.GetDefaultForTier(newTier);

            // Generate new license key with updated tier
            var licenseKey = GenerateJwtLicenseKey(
                license.CustomerId,
                license.Email,
                license.Tier,
                license.Features,
                license.IssuedAt,
                license.ExpiresAt);

            license.LicenseKey = licenseKey;

            // Save updated license
            await _licenseStore.UpdateAsync(license, token);

            _logger.LogInformation(
                "Upgraded license for customer {CustomerId} from {OldTier} to {NewTier}",
                customerId,
                oldTier,
                newTier);

            return license;
        }, cancellationToken);
    }

    public async Task<LicenseInfo> DowngradeLicenseAsync(
        string customerId,
        LicenseTier newTier,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("CustomerId is required", nameof(customerId));
        }

        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var license = await _licenseStore.GetByCustomerIdAsync(customerId, token);
            if (license == null)
            {
                throw new InvalidOperationException($"License not found for customer {customerId}");
            }

            if (newTier >= license.Tier)
            {
                throw new InvalidOperationException(
                    $"Cannot downgrade to {newTier} from {license.Tier}. Use UpgradeLicenseAsync instead.");
            }

            // Update tier and features
            var oldTier = license.Tier;
            license.Tier = newTier;
            license.Features = LicenseFeatures.GetDefaultForTier(newTier);

            // Generate new license key with updated tier
            var licenseKey = GenerateJwtLicenseKey(
                license.CustomerId,
                license.Email,
                license.Tier,
                license.Features,
                license.IssuedAt,
                license.ExpiresAt);

            license.LicenseKey = licenseKey;

            // Save updated license
            await _licenseStore.UpdateAsync(license, token);

            _logger.LogInformation(
                "Downgraded license for customer {CustomerId} from {OldTier} to {NewTier}",
                customerId,
                oldTier,
                newTier);

            return license;
        }, cancellationToken);
    }

    public async Task RevokeLicenseAsync(
        string customerId,
        string reason,
        string revokedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("CustomerId is required", nameof(customerId));
        }

        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var license = await _licenseStore.GetByCustomerIdAsync(customerId, token);
            if (license == null)
            {
                throw new InvalidOperationException($"License not found for customer {customerId}");
            }

            // Mark license as revoked
            license.Status = LicenseStatus.Revoked;
            license.RevokedAt = DateTimeOffset.UtcNow;

            await _licenseStore.UpdateAsync(license, token);

            _logger.LogWarning(
                "Revoked license for customer {CustomerId}. Reason: {Reason}, By: {RevokedBy}",
                customerId,
                reason,
                revokedBy);

            // Trigger credential revocation
            if (_options.CurrentValue.EnableAutomaticRevocation)
            {
                try
                {
                    await _credentialRevocationService.RevokeCustomerCredentialsAsync(
                        customerId,
                        reason,
                        revokedBy,
                        token);

                    _logger.LogInformation(
                        "Triggered credential revocation for customer {CustomerId}",
                        customerId);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to revoke credentials for customer {CustomerId}",
                        customerId);
                    // Don't fail the license revocation if credential cleanup fails
                }
            }
        }, cancellationToken);
    }

    public async Task<LicenseInfo> RenewLicenseAsync(
        string customerId,
        int extensionDays,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("CustomerId is required", nameof(customerId));
        }

        if (extensionDays <= 0)
        {
            throw new ArgumentException("ExtensionDays must be greater than zero", nameof(extensionDays));
        }

        return await _resiliencePipeline.ExecuteAsync(async token =>
        {
            var license = await _licenseStore.GetByCustomerIdAsync(customerId, token);
            if (license == null)
            {
                throw new InvalidOperationException($"License not found for customer {customerId}");
            }

            if (license.Status == LicenseStatus.Revoked)
            {
                throw new InvalidOperationException($"Cannot renew revoked license for customer {customerId}");
            }

            // Extend expiration date
            var oldExpiresAt = license.ExpiresAt;
            license.ExpiresAt = license.ExpiresAt.AddDays(extensionDays);

            // If license was expired, reactivate it
            if (license.Status == LicenseStatus.Expired)
            {
                license.Status = LicenseStatus.Active;
            }

            // Generate new license key with updated expiration
            var licenseKey = GenerateJwtLicenseKey(
                license.CustomerId,
                license.Email,
                license.Tier,
                license.Features,
                license.IssuedAt,
                license.ExpiresAt);

            license.LicenseKey = licenseKey;

            // Save updated license
            await _licenseStore.UpdateAsync(license, token);

            _logger.LogInformation(
                "Renewed license for customer {CustomerId}, extended by {Days} days (old expiry: {OldExpiry}, new expiry: {NewExpiry})",
                customerId,
                extensionDays,
                oldExpiresAt,
                license.ExpiresAt);

            return license;
        }, cancellationToken);
    }

    private string GenerateJwtLicenseKey(
        string customerId,
        string email,
        LicenseTier tier,
        LicenseFeatures features,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        var opts = _options.CurrentValue;

        var claims = new[]
        {
            new Claim("customer_id", customerId),
            new Claim("tier", tier.ToString()),
            new Claim("email", email),
            new Claim("features", JsonSerializer.Serialize(features)),
            new Claim(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString()),
            new Claim(JwtRegisteredClaimNames.Exp, expiresAt.ToUnixTimeSeconds().ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(opts.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: opts.Issuer,
            audience: opts.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: creds
        );

        return _tokenHandler.WriteToken(token);
    }
}
