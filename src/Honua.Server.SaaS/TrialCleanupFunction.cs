// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Dapper;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.SaaS;

/// <summary>
/// Background function that runs daily to clean up expired trial tenants
/// </summary>
public class TrialCleanupFunction
{
    private readonly ILogger<TrialCleanupFunction> _logger;
    private readonly string _connectionString;
    private readonly string _subscriptionId;
    private readonly string _resourceGroupName;
    private readonly string _dnsZoneName;
    private readonly int _gracePeriodDays;

    public TrialCleanupFunction(ILogger<TrialCleanupFunction> logger)
    {
        _logger = logger;
        _connectionString = Environment.GetEnvironmentVariable("PostgresConnectionString")
            ?? throw new InvalidOperationException("PostgresConnectionString not configured");
        _subscriptionId = Environment.GetEnvironmentVariable("AzureSubscriptionId")
            ?? throw new InvalidOperationException("AzureSubscriptionId not configured");
        _resourceGroupName = Environment.GetEnvironmentVariable("DnsResourceGroupName")
            ?? throw new InvalidOperationException("DnsResourceGroupName not configured");
        _dnsZoneName = Environment.GetEnvironmentVariable("DnsZoneName") ?? "honua.io";
        _gracePeriodDays = int.TryParse(Environment.GetEnvironmentVariable("GracePeriodDays"), out var days) ? days : 7;
    }

    /// <summary>
    /// Runs daily at 2 AM UTC to clean up expired trials
    /// </summary>
    [Function("TrialCleanup")]
    public async Task Run([TimerTrigger("0 0 2 * * *")] TimerInfo timerInfo)
    {
        _logger.LogInformation("Trial cleanup job started at {Time}", DateTime.UtcNow);

        try
        {
            // Find expired trials
            var expiredTenants = await GetExpiredTrialsAsync();
            _logger.LogInformation("Found {Count} expired trial tenants to clean up", expiredTenants.Count);

            foreach (var tenant in expiredTenants)
            {
                try
                {
                    await CleanupTenantAsync(tenant);
                    _logger.LogInformation("Cleaned up expired trial tenant {TenantId}", tenant);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error cleaning up tenant {TenantId}", tenant);
                }
            }

            _logger.LogInformation("Trial cleanup job completed. Cleaned up {Count} tenants", expiredTenants.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in trial cleanup job");
            throw;
        }
    }

    private async Task<List<string>> GetExpiredTrialsAsync()
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        // Find trials expired more than grace period days ago
        var sql = @"
            SELECT c.customer_id
            FROM customers c
            INNER JOIN licenses l ON c.customer_id = l.customer_id
            WHERE c.subscription_status = 'trial'
              AND l.status = 'trial'
              AND l.trial_expires_at < NOW() - (@GracePeriodDays || ' days')::INTERVAL
              AND c.deleted_at IS NULL
            ORDER BY l.trial_expires_at";

        var tenants = await connection.QueryAsync<string>(sql, new { GracePeriodDays = _gracePeriodDays });
        return tenants.ToList();
    }

    private async Task CleanupTenantAsync(string tenantId)
    {
        _logger.LogInformation("Cleaning up tenant {TenantId}", tenantId);

        // Soft delete customer and related records
        await SoftDeleteCustomerAsync(tenantId);

        // Remove DNS record
        await DeleteDnsRecordAsync(tenantId);

        _logger.LogInformation("Successfully cleaned up tenant {TenantId}", tenantId);
    }

    private async Task SoftDeleteCustomerAsync(string tenantId)
    {
        await using var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();

        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // Soft delete customer (cascades to licenses via FK)
            await connection.ExecuteAsync(@"
                UPDATE customers
                SET deleted_at = NOW(),
                    subscription_status = 'cancelled'
                WHERE customer_id = @TenantId
                  AND deleted_at IS NULL",
                new { TenantId = tenantId },
                transaction);

            // Update license status
            await connection.ExecuteAsync(@"
                UPDATE licenses
                SET status = 'expired',
                    revoked_at = NOW(),
                    revoked_by = 'system',
                    revoked_reason = 'Trial expired and grace period ended'
                WHERE customer_id = @TenantId",
                new { TenantId = tenantId },
                transaction);

            await transaction.CommitAsync();
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    private async Task DeleteDnsRecordAsync(string tenantId)
    {
        try
        {
            var cancellationToken = CancellationToken.None;
            var credential = new DefaultAzureCredential();
            var armClient = new ArmClient(credential);

            // Get DNS zone
            var subscriptionResource = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{_subscriptionId}"));
            var resourceGroup = await subscriptionResource.GetResourceGroupAsync(_resourceGroupName);
            var dnsZone = await resourceGroup.Value.GetDnsZoneAsync(_dnsZoneName);

            // Delete A record
            var aRecords = dnsZone.Value.GetDnsARecords();
            if (await aRecords.ExistsAsync(tenantId, cancellationToken).ConfigureAwait(false))
            {
                var record = await aRecords.GetAsync(tenantId, cancellationToken).ConfigureAwait(false);
                await record.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("Deleted DNS record for {TenantId}.{DnsZone}", tenantId, _dnsZoneName);
            }
            else
            {
                _logger.LogWarning("DNS record not found for {TenantId}.{DnsZone}", tenantId, _dnsZoneName);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting DNS record for {TenantId}", tenantId);
            // Don't throw - allow cleanup to continue
        }
    }
}
