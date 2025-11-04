// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace Honua.Server.Observability.HealthChecks;

/// <summary>
/// Health check for license validation and quota status.
/// </summary>
public class LicenseHealthCheck : IHealthCheck
{
    private readonly string _connectionString;

    public LicenseHealthCheck(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = new NpgsqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            // Check if license table exists
            var licenseTableExists = await connection.QuerySingleAsync<bool>(
                @"SELECT EXISTS (
                    SELECT FROM information_schema.tables
                    WHERE table_schema = 'public'
                    AND table_name = 'licenses'
                )",
                cancellationToken);

            if (!licenseTableExists)
            {
                return HealthCheckResult.Degraded(
                    "License table does not exist",
                    data: new Dictionary<string, object>
                    {
                        { "table_exists", false }
                    });
            }

            // Count active licenses
            var activeLicenses = await connection.QuerySingleAsync<int>(
                @"SELECT COUNT(*) FROM licenses
                  WHERE is_active = true
                  AND (expires_at IS NULL OR expires_at > NOW())",
                cancellationToken);

            // Count expired licenses
            var expiredLicenses = await connection.QuerySingleAsync<int>(
                @"SELECT COUNT(*) FROM licenses
                  WHERE expires_at IS NOT NULL
                  AND expires_at <= NOW()",
                cancellationToken);

            // Count licenses expiring soon (within 7 days)
            var expiringSoon = await connection.QuerySingleAsync<int>(
                @"SELECT COUNT(*) FROM licenses
                  WHERE is_active = true
                  AND expires_at IS NOT NULL
                  AND expires_at BETWEEN NOW() AND NOW() + INTERVAL '7 days'",
                cancellationToken);

            var data = new Dictionary<string, object>
            {
                { "active_licenses", activeLicenses },
                { "expired_licenses", expiredLicenses },
                { "expiring_soon", expiringSoon }
            };

            // Check for licenses expiring soon
            if (expiringSoon > 0)
            {
                return HealthCheckResult.Degraded(
                    $"{expiringSoon} license(s) expiring within 7 days",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                $"{activeLicenses} active license(s)",
                data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "License health check failed",
                ex);
        }
    }
}
