// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Certificates;

/// <summary>
/// Service for acquiring and managing SSL/TLS certificates via ACME/Let's Encrypt using Certes library.
/// </summary>
public sealed class AcmeCertificateService
{
    private readonly ILogger<AcmeCertificateService> _logger;
    private static readonly Uri LetsEncryptStagingV2 = WellKnownServers.LetsEncryptStagingV2;
    private static readonly Uri LetsEncryptV2 = WellKnownServers.LetsEncryptV2;

    public AcmeCertificateService(ILogger<AcmeCertificateService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Acquires a new certificate from Let's Encrypt.
    /// </summary>
    /// <param name="domains">List of domains for the certificate</param>
    /// <param name="email">Contact email for ACME account</param>
    /// <param name="challengeType">Challenge type (Http01 or Dns01)</param>
    /// <param name="isProduction">Use production Let's Encrypt environment</param>
    /// <param name="accountKeyPath">Path to ACME account key (created if not exists)</param>
    /// <param name="challengeProvider">Provider for DNS-01 or HTTP-01 challenge validation</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Certificate acquisition result</returns>
    public async Task<CertificateAcquisitionResult> AcquireCertificateAsync(
        List<string> domains,
        string email,
        string challengeType,
        bool isProduction,
        string accountKeyPath,
        IChallengeProvider challengeProvider,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting certificate acquisition for domains: {Domains}", string.Join(", ", domains));

            // Select ACME server
            var acmeServer = isProduction ? LetsEncryptV2 : LetsEncryptStagingV2;
            _logger.LogInformation("Using ACME server: {Server}", isProduction ? "Production" : "Staging");

            // Load or create account key
            var accountKey = await LoadOrCreateAccountKeyAsync(accountKeyPath, cancellationToken);
            var acme = new AcmeContext(acmeServer, accountKey);

            // Get or create account
            var account = await acme.NewAccount(email, termsOfServiceAgreed: true);
            _logger.LogInformation("ACME account ready: {AccountId}", account.Location);

            // Create order
            var order = await acme.NewOrder(domains);
            _logger.LogInformation("Created ACME order: {OrderUrl}", order.Location);

            // Get authorizations and complete challenges
            var authorizations = await order.Authorizations();

            foreach (var auth in authorizations)
            {
                var authz = await auth.Resource();
                _logger.LogInformation("Processing authorization for: {Domain}", authz.Identifier.Value);

                IChallengeContext? challenge = challengeType switch
                {
                    "Http01" => await auth.Http(),
                    "Dns01" => await auth.Dns(),
                    _ => throw new ArgumentException($"Unsupported challenge type: {challengeType}")
                };

                // Get challenge details
                var challengeDetails = await challenge.Resource();
                var token = challengeDetails.Token;
                var keyAuthz = challenge.KeyAuthz;

                _logger.LogInformation("Challenge type: {Type}, Token: {Token}", challengeType, token);

                // Deploy challenge using provider
                await challengeProvider.DeployChallengeAsync(
                    authz.Identifier.Value,
                    token,
                    keyAuthz,
                    challengeType,
                    cancellationToken);

                // Validate challenge
                _logger.LogInformation("Validating challenge for {Domain}", authz.Identifier.Value);
                var challengeResult = await challenge.Validate();

                // Wait for validation
                int attempts = 0;
                const int maxAttempts = 30;
                while (attempts < maxAttempts)
                {
                    var challengeResource = await challenge.Resource();
                    if (challengeResource.Status == ChallengeStatus.Valid)
                    {
                        _logger.LogInformation("Challenge validated successfully for {Domain}", authz.Identifier.Value);
                        break;
                    }

                    if (challengeResource.Status == ChallengeStatus.Invalid)
                    {
                        var error = challengeResource.Error;
                        throw new InvalidOperationException($"Challenge validation failed for {authz.Identifier.Value}: {error?.Detail}");
                    }

                    attempts++;
                    await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
                }

                if (attempts >= maxAttempts)
                {
                    throw new TimeoutException($"Challenge validation timed out for {authz.Identifier.Value}");
                }

                // Cleanup challenge
                await challengeProvider.CleanupChallengeAsync(
                    authz.Identifier.Value,
                    token,
                    keyAuthz,
                    challengeType,
                    cancellationToken);
            }

            // Generate certificate private key
            var privateKey = KeyFactory.NewKey(KeyAlgorithm.ES256);

            // Finalize order and download certificate
            _logger.LogInformation("Finalizing order and downloading certificate");
            var finalizedOrder = await order.Finalize(new CsrInfo
            {
                CommonName = domains.First()
            }, privateKey);

            // Wait for certificate to be ready
            int finalAttempts = 0;
            const int maxFinalAttempts = 30;
            while (finalAttempts < maxFinalAttempts)
            {
                var orderResource = await order.Resource();
                if (orderResource.Status == OrderStatus.Valid)
                {
                    break;
                }

                if (orderResource.Status == OrderStatus.Invalid)
                {
                    throw new InvalidOperationException("Order became invalid during finalization");
                }

                finalAttempts++;
                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
            }

            if (finalAttempts >= maxFinalAttempts)
            {
                throw new TimeoutException("Certificate finalization timed out");
            }

            // Download certificate
            var certificate = await order.Download();
            var certificateChain = certificate.ToPem();
            var privateKeyPem = privateKey.ToPem();

            // Parse certificate to get actual expiry date
            DateTime expiresAt;
            try
            {
                var certBytes = System.Text.Encoding.UTF8.GetBytes(certificateChain);
                using var x509 = System.Security.Cryptography.X509Certificates.X509CertificateLoader.LoadCertificate(certBytes);
                expiresAt = x509.NotAfter.ToUniversalTime();
                _logger.LogInformation("Certificate expires at: {ExpiryDate}", expiresAt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to parse certificate expiry date, defaulting to 90 days");
                expiresAt = DateTime.UtcNow.AddDays(90); // Fallback to typical Let's Encrypt duration
            }

            _logger.LogInformation("Certificate acquired successfully for domains: {Domains}", string.Join(", ", domains));

            return new CertificateAcquisitionResult
            {
                Success = true,
                CertificatePem = certificateChain,
                PrivateKeyPem = privateKeyPem,
                Domains = domains,
                ExpiresAt = expiresAt,
                Message = $"Successfully acquired certificate for {string.Join(", ", domains)}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire certificate");
            return new CertificateAcquisitionResult
            {
                Success = false,
                Message = $"Failed to acquire certificate: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Loads existing ACME account key or creates a new one.
    /// </summary>
    private async Task<IKey> LoadOrCreateAccountKeyAsync(string accountKeyPath, CancellationToken cancellationToken)
    {
        if (File.Exists(accountKeyPath))
        {
            _logger.LogInformation("Loading existing ACME account key from {Path}", accountKeyPath);
            var existingPemKey = await File.ReadAllTextAsync(accountKeyPath, cancellationToken);
            return KeyFactory.FromPem(existingPemKey);
        }

        _logger.LogInformation("Creating new ACME account key at {Path}", accountKeyPath);
        var key = KeyFactory.NewKey(KeyAlgorithm.ES256);
        var pemKey = key.ToPem();

        var directory = Path.GetDirectoryName(accountKeyPath);
        if (!directory.IsNullOrEmpty() && !System.IO.Directory.Exists(directory))
        {
            System.IO.Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(accountKeyPath, pemKey, cancellationToken);

        // Set restrictive permissions on Unix systems
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(accountKeyPath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }

        return key;
    }

    /// <summary>
    /// Renews an existing certificate if it's close to expiration.
    /// </summary>
    /// <param name="certificatePath">Path to existing certificate</param>
    /// <param name="domains">Domains for the certificate</param>
    /// <param name="email">Contact email</param>
    /// <param name="challengeType">Challenge type</param>
    /// <param name="isProduction">Use production environment</param>
    /// <param name="accountKeyPath">ACME account key path</param>
    /// <param name="challengeProvider">Challenge provider</param>
    /// <param name="daysBeforeExpiry">Renew if certificate expires within this many days</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public async Task<CertificateRenewalResult> RenewCertificateIfNeededAsync(
        string certificatePath,
        List<string> domains,
        string email,
        string challengeType,
        bool isProduction,
        string accountKeyPath,
        IChallengeProvider challengeProvider,
        int daysBeforeExpiry,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(certificatePath))
            {
                _logger.LogWarning("Certificate not found at {Path}, acquiring new certificate", certificatePath);
                var acquisitionResult = await AcquireCertificateAsync(
                    domains, email, challengeType, isProduction, accountKeyPath, challengeProvider, cancellationToken);

                return new CertificateRenewalResult
                {
                    Success = acquisitionResult.Success,
                    Renewed = true,
                    Message = acquisitionResult.Message,
                    CertificatePem = acquisitionResult.CertificatePem,
                    PrivateKeyPem = acquisitionResult.PrivateKeyPem
                };
            }

            // Load and check existing certificate
            var certBytes = await File.ReadAllBytesAsync(certificatePath, cancellationToken);
            using var cert = X509CertificateLoader.LoadCertificate(certBytes);
            var daysUntilExpiry = (cert.NotAfter - DateTime.UtcNow).TotalDays;

            _logger.LogInformation("Certificate expires in {Days} days (threshold: {Threshold})", daysUntilExpiry, daysBeforeExpiry);

            if (daysUntilExpiry > daysBeforeExpiry)
            {
                return new CertificateRenewalResult
                {
                    Success = true,
                    Renewed = false,
                    Message = $"Certificate is valid for {daysUntilExpiry:F0} more days, no renewal needed"
                };
            }

            // Renew certificate
            _logger.LogInformation("Certificate expires soon, renewing...");
            var renewalResult = await AcquireCertificateAsync(
                domains, email, challengeType, isProduction, accountKeyPath, challengeProvider, cancellationToken);

            return new CertificateRenewalResult
            {
                Success = renewalResult.Success,
                Renewed = true,
                Message = renewalResult.Message,
                CertificatePem = renewalResult.CertificatePem,
                PrivateKeyPem = renewalResult.PrivateKeyPem
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to renew certificate");
            return new CertificateRenewalResult
            {
                Success = false,
                Renewed = false,
                Message = $"Failed to renew certificate: {ex.Message}"
            };
        }
    }
}

/// <summary>
/// Provider interface for ACME challenge validation.
/// </summary>
public interface IChallengeProvider
{
    Task DeployChallengeAsync(string domain, string token, string keyAuthz, string challengeType, CancellationToken cancellationToken);
    Task CleanupChallengeAsync(string domain, string token, string keyAuthz, string challengeType, CancellationToken cancellationToken);
}

/// <summary>
/// Result of certificate acquisition.
/// </summary>
public sealed class CertificateAcquisitionResult
{
    public bool Success { get; set; }
    public string CertificatePem { get; set; } = string.Empty;
    public string PrivateKeyPem { get; set; } = string.Empty;
    public List<string> Domains { get; set; } = new();
    public DateTime ExpiresAt { get; set; }
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Result of certificate renewal check.
/// </summary>
public sealed class CertificateRenewalResult
{
    public bool Success { get; set; }
    public bool Renewed { get; set; }
    public string CertificatePem { get; set; } = string.Empty;
    public string PrivateKeyPem { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
