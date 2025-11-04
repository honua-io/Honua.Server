// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Licensing.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Honua.Server.Core.Licensing;

/// <summary>
/// Background service that periodically checks for expiring and expired licenses.
/// Sends warning emails for licenses expiring soon and revokes credentials for expired licenses.
/// </summary>
public sealed class LicenseExpirationBackgroundService : BackgroundService
{
    private readonly ILicenseStore _licenseStore;
    private readonly ICredentialRevocationService _credentialRevocationService;
    private readonly IOptionsMonitor<LicenseOptions> _options;
    private readonly ILogger<LicenseExpirationBackgroundService> _logger;

    public LicenseExpirationBackgroundService(
        ILicenseStore licenseStore,
        ICredentialRevocationService credentialRevocationService,
        IOptionsMonitor<LicenseOptions> options,
        ILogger<LicenseExpirationBackgroundService> logger)
    {
        _licenseStore = licenseStore ?? throw new ArgumentNullException(nameof(licenseStore));
        _credentialRevocationService = credentialRevocationService ?? throw new ArgumentNullException(nameof(credentialRevocationService));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("License expiration background service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessLicenseExpirationAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing license expiration check");
            }

            // Wait for the configured interval before next check
            var interval = _options.CurrentValue.ExpirationCheckInterval;
            _logger.LogDebug("Next license expiration check in {Interval}", interval);

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                // Service is stopping
                break;
            }
        }

        _logger.LogInformation("License expiration background service stopped");
    }

    private async Task ProcessLicenseExpirationAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting license expiration check");

        var opts = _options.CurrentValue;

        // 1. Check for licenses expiring soon (send warnings)
        await ProcessExpiringLicensesAsync(opts.WarningThresholdDays, cancellationToken);

        // 2. Check for expired licenses (revoke credentials)
        await ProcessExpiredLicensesAsync(cancellationToken);

        _logger.LogInformation("Completed license expiration check");
    }

    private async Task ProcessExpiringLicensesAsync(int warningThresholdDays, CancellationToken cancellationToken)
    {
        try
        {
            var expiringLicenses = await _licenseStore.GetExpiringLicensesAsync(
                warningThresholdDays,
                cancellationToken);

            if (expiringLicenses.Length == 0)
            {
                _logger.LogDebug("No licenses expiring within {Days} days", warningThresholdDays);
                return;
            }

            _logger.LogInformation(
                "Found {Count} licenses expiring within {Days} days",
                expiringLicenses.Length,
                warningThresholdDays);

            foreach (var license in expiringLicenses)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    break;
                }

                try
                {
                    await SendExpirationWarningEmailAsync(license, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to send expiration warning email for customer {CustomerId}",
                        license.CustomerId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing expiring licenses");
        }
    }

    private async Task ProcessExpiredLicensesAsync(CancellationToken cancellationToken)
    {
        try
        {
            var expiredLicenses = await _licenseStore.GetExpiredLicensesAsync(cancellationToken);

            if (expiredLicenses.Length == 0)
            {
                _logger.LogDebug("No expired licenses found");
                return;
            }

            _logger.LogInformation("Found {Count} expired licenses", expiredLicenses.Length);

            // Revoke credentials for expired licenses
            var opts = _options.CurrentValue;
            if (opts.EnableAutomaticRevocation)
            {
                foreach (var license in expiredLicenses)
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        break;
                    }

                    // Skip if already revoked
                    if (license.Status == LicenseStatus.Revoked || license.RevokedAt != null)
                    {
                        continue;
                    }

                    try
                    {
                        _logger.LogWarning(
                            "Revoking credentials for expired license: customer {CustomerId}, expired at {ExpiresAt}",
                            license.CustomerId,
                            license.ExpiresAt);

                        await _credentialRevocationService.RevokeCustomerCredentialsAsync(
                            license.CustomerId,
                            "License expired",
                            "System",
                            cancellationToken);

                        // Update license status to expired
                        if (license.Status != LicenseStatus.Expired)
                        {
                            license.Status = LicenseStatus.Expired;
                            await _licenseStore.UpdateAsync(license, cancellationToken);
                        }

                        _logger.LogInformation(
                            "Successfully revoked credentials for expired license: customer {CustomerId}",
                            license.CustomerId);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Failed to revoke credentials for expired license: customer {CustomerId}",
                            license.CustomerId);
                    }
                }
            }
            else
            {
                _logger.LogWarning(
                    "Automatic credential revocation is disabled. Found {Count} expired licenses that require manual intervention.",
                    expiredLicenses.Length);

                foreach (var license in expiredLicenses)
                {
                    _logger.LogWarning(
                        "Expired license requires manual review: customer {CustomerId}, tier {Tier}, expired at {ExpiresAt}",
                        license.CustomerId,
                        license.Tier,
                        license.ExpiresAt);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing expired licenses");
        }
    }

    private async Task SendExpirationWarningEmailAsync(LicenseInfo license, CancellationToken cancellationToken)
    {
        var opts = _options.CurrentValue;

        if (opts.Smtp == null || string.IsNullOrWhiteSpace(opts.Smtp.Host))
        {
            _logger.LogWarning(
                "SMTP not configured, skipping expiration warning email for customer {CustomerId}",
                license.CustomerId);
            return;
        }

        var daysUntilExpiration = license.DaysUntilExpiration();

        _logger.LogInformation(
            "Sending expiration warning email to {Email} for customer {CustomerId} (expires in {Days} days)",
            license.Email,
            license.CustomerId,
            daysUntilExpiration);

        try
        {
            using var smtpClient = new SmtpClient(opts.Smtp.Host, opts.Smtp.Port)
            {
                EnableSsl = opts.Smtp.EnableSsl,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(opts.Smtp.Username) &&
                !string.IsNullOrWhiteSpace(opts.Smtp.Password))
            {
                smtpClient.Credentials = new NetworkCredential(
                    opts.Smtp.Username,
                    opts.Smtp.Password);
            }

            var mailMessage = new MailMessage
            {
                From = new MailAddress(opts.Smtp.FromEmail, opts.Smtp.FromName),
                Subject = $"Honua License Expiring in {daysUntilExpiration} Days",
                Body = BuildExpirationWarningEmailBody(license, daysUntilExpiration),
                IsBodyHtml = true
            };

            mailMessage.To.Add(license.Email);

            await smtpClient.SendMailAsync(mailMessage, cancellationToken);

            _logger.LogInformation(
                "Successfully sent expiration warning email to {Email}",
                license.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to send expiration warning email to {Email}",
                license.Email);
            throw;
        }
    }

    private static string BuildExpirationWarningEmailBody(LicenseInfo license, int daysUntilExpiration)
    {
        var urgency = daysUntilExpiration <= 1 ? "URGENT" : daysUntilExpiration <= 3 ? "Important" : "";

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"">
    <title>License Expiration Warning</title>
</head>
<body style=""font-family: Arial, sans-serif; line-height: 1.6; color: #333;"">
    <div style=""max-width: 600px; margin: 0 auto; padding: 20px;"">
        <h2 style=""color: #d32f2f;"">{urgency} License Expiration Notice</h2>

        <p>Dear Valued Customer,</p>

        <p>This is a reminder that your Honua license will expire in <strong>{daysUntilExpiration} day{(daysUntilExpiration != 1 ? "s" : "")}</strong>.</p>

        <div style=""background-color: #f5f5f5; border-left: 4px solid #2196f3; padding: 15px; margin: 20px 0;"">
            <h3 style=""margin-top: 0;"">License Details:</h3>
            <ul style=""list-style: none; padding: 0;"">
                <li><strong>Customer ID:</strong> {license.CustomerId}</li>
                <li><strong>Tier:</strong> {license.Tier}</li>
                <li><strong>Expiration Date:</strong> {license.ExpiresAt:yyyy-MM-dd HH:mm:ss} UTC</li>
                <li><strong>Status:</strong> {license.Status}</li>
            </ul>
        </div>

        <p><strong>Important:</strong> When your license expires, the following will occur:</p>
        <ul>
            <li>Access to Honua services will be terminated</li>
            <li>All associated registry credentials (AWS, Azure, GCP, GitHub) will be automatically revoked</li>
            <li>Your data will be retained for 30 days before permanent deletion</li>
        </ul>

        <p>To avoid service interruption, please renew your license before the expiration date.</p>

        <div style=""margin: 30px 0; text-align: center;"">
            <a href=""https://honua.io/renew?customer={license.CustomerId}""
               style=""display: inline-block; padding: 12px 30px; background-color: #2196f3; color: white; text-decoration: none; border-radius: 4px; font-weight: bold;"">
                Renew License Now
            </a>
        </div>

        <p>If you have any questions or need assistance, please contact our support team.</p>

        <p>Best regards,<br>
        The Honua Team</p>

        <hr style=""border: none; border-top: 1px solid #ddd; margin: 30px 0;"">

        <p style=""font-size: 12px; color: #666;"">
            This is an automated message. Please do not reply to this email.
        </p>
    </div>
</body>
</html>";
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("License expiration background service is stopping");
        await base.StopAsync(cancellationToken);
    }
}
