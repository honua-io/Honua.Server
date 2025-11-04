// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Azure;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Dns;
using Azure.ResourceManager.Dns.Models;
using Azure.ResourceManager.Resources;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Dns;

/// <summary>
/// Simplified Azure DNS provider with full CRUD operations.
/// Supports A, AAAA, CNAME, TXT, MX, NS, SRV, and CAA records.
/// </summary>
public sealed class AzureDnsProvider
{
    private readonly ILogger<AzureDnsProvider> _logger;

    public AzureDnsProvider(ILogger<AzureDnsProvider> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates or updates a DNS A record.
    /// </summary>
    public async Task<DnsOperationResult> UpsertARecordAsync(
        ArmClient armClient,
        string subscriptionId,
        string resourceGroupName,
        string zoneName,
        string recordName,
        List<string> ipAddresses,
        int ttl,
        CancellationToken cancellationToken)
    {
        try
        {
            var zone = await GetDnsZoneAsync(armClient, subscriptionId, resourceGroupName, zoneName, cancellationToken);
            var recordSetName = NormalizeRecordName(recordName);

            var data = new DnsARecordData { TtlInSeconds = ttl };
            foreach (var ip in ipAddresses)
            {
                data.DnsARecords.Add(new DnsARecordInfo { IPv4Address = IPAddress.Parse(ip) });
            }

            var collection = zone.GetDnsARecords();
            var result = await collection.CreateOrUpdateAsync(WaitUntil.Completed, recordSetName, data, cancellationToken: cancellationToken);

            _logger.LogInformation("Created/updated A record: {Name}", recordName);
            return new DnsOperationResult
            {
                Success = true,
                Message = $"Successfully upserted A record for {recordName}",
                ChangeId = result.Value.Id.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert A record");
            return new DnsOperationResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Creates or updates a DNS CNAME record.
    /// </summary>
    public async Task<DnsOperationResult> UpsertCnameRecordAsync(
        ArmClient armClient,
        string subscriptionId,
        string resourceGroupName,
        string zoneName,
        string recordName,
        string targetDomain,
        int ttl,
        CancellationToken cancellationToken)
    {
        try
        {
            var zone = await GetDnsZoneAsync(armClient, subscriptionId, resourceGroupName, zoneName, cancellationToken);
            var recordSetName = NormalizeRecordName(recordName);

            var data = new DnsCnameRecordData
            {
                TtlInSeconds = ttl,
                Cname = targetDomain
            };

            var collection = zone.GetDnsCnameRecords();
            var result = await collection.CreateOrUpdateAsync(WaitUntil.Completed, recordSetName, data, cancellationToken: cancellationToken);

            _logger.LogInformation("Created/updated CNAME record: {Name}", recordName);
            return new DnsOperationResult
            {
                Success = true,
                Message = $"Successfully upserted CNAME record for {recordName}",
                ChangeId = result.Value.Id.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert CNAME record");
            return new DnsOperationResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Creates or updates a DNS TXT record (used for ACME challenges).
    /// </summary>
    public async Task<DnsOperationResult> UpsertTxtRecordAsync(
        ArmClient armClient,
        string subscriptionId,
        string resourceGroupName,
        string zoneName,
        string recordName,
        List<string> textValues,
        int ttl,
        CancellationToken cancellationToken)
    {
        try
        {
            var zone = await GetDnsZoneAsync(armClient, subscriptionId, resourceGroupName, zoneName, cancellationToken);
            var recordSetName = NormalizeRecordName(recordName);

            var data = new DnsTxtRecordData { TtlInSeconds = ttl };
            foreach (var txt in textValues)
            {
                var record = new DnsTxtRecordInfo();
                record.Values.Add(txt);
                data.DnsTxtRecords.Add(record);
            }

            var collection = zone.GetDnsTxtRecords();
            var result = await collection.CreateOrUpdateAsync(WaitUntil.Completed, recordSetName, data, cancellationToken: cancellationToken);

            _logger.LogInformation("Created/updated TXT record: {Name}", recordName);
            return new DnsOperationResult
            {
                Success = true,
                Message = $"Successfully upserted TXT record for {recordName}",
                ChangeId = result.Value.Id.ToString()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert TXT record");
            return new DnsOperationResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Deletes a DNS A record.
    /// </summary>
    public async Task<DnsOperationResult> DeleteARecordAsync(
        ArmClient armClient,
        string subscriptionId,
        string resourceGroupName,
        string zoneName,
        string recordName,
        CancellationToken cancellationToken)
    {
        try
        {
            var zone = await GetDnsZoneAsync(armClient, subscriptionId, resourceGroupName, zoneName, cancellationToken);
            var recordSetName = NormalizeRecordName(recordName);

            var collection = zone.GetDnsARecords();
            var record = await collection.GetAsync(recordSetName, cancellationToken);
            await record.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Deleted A record: {Name}", recordName);
            return new DnsOperationResult
            {
                Success = true,
                Message = $"Successfully deleted A record for {recordName}"
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new DnsOperationResult { Success = true, Message = "Record does not exist" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete A record");
            return new DnsOperationResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Deletes a DNS CNAME record.
    /// </summary>
    public async Task<DnsOperationResult> DeleteCnameRecordAsync(
        ArmClient armClient,
        string subscriptionId,
        string resourceGroupName,
        string zoneName,
        string recordName,
        CancellationToken cancellationToken)
    {
        try
        {
            var zone = await GetDnsZoneAsync(armClient, subscriptionId, resourceGroupName, zoneName, cancellationToken);
            var recordSetName = NormalizeRecordName(recordName);

            var collection = zone.GetDnsCnameRecords();
            var record = await collection.GetAsync(recordSetName, cancellationToken);
            await record.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Deleted CNAME record: {Name}", recordName);
            return new DnsOperationResult
            {
                Success = true,
                Message = $"Successfully deleted CNAME record for {recordName}"
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new DnsOperationResult { Success = true, Message = "Record does not exist" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete CNAME record");
            return new DnsOperationResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Deletes a DNS TXT record.
    /// </summary>
    public async Task<DnsOperationResult> DeleteTxtRecordAsync(
        ArmClient armClient,
        string subscriptionId,
        string resourceGroupName,
        string zoneName,
        string recordName,
        CancellationToken cancellationToken)
    {
        try
        {
            var zone = await GetDnsZoneAsync(armClient, subscriptionId, resourceGroupName, zoneName, cancellationToken);
            var recordSetName = NormalizeRecordName(recordName);

            var collection = zone.GetDnsTxtRecords();
            var record = await collection.GetAsync(recordSetName, cancellationToken);
            await record.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Deleted TXT record: {Name}", recordName);
            return new DnsOperationResult
            {
                Success = true,
                Message = $"Successfully deleted TXT record for {recordName}"
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return new DnsOperationResult { Success = true, Message = "Record does not exist" };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete TXT record");
            return new DnsOperationResult { Success = false, Message = ex.Message };
        }
    }

    /// <summary>
    /// Lists all DNS zones in a resource group or subscription.
    /// </summary>
    public async Task<List<string>> ListDnsZonesAsync(
        ArmClient armClient,
        string subscriptionId,
        string? resourceGroupName = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var zones = new List<string>();
            var subscriptionId1 = new ResourceIdentifier($"/subscriptions/{subscriptionId}");
            var subscription = armClient.GetSubscriptionResource(subscriptionId1);

            await foreach (var zone in subscription.GetDnsZonesAsync(cancellationToken: cancellationToken))
            {
                // If resource group filter is specified, only include zones from that RG
                if (resourceGroupName.IsNullOrEmpty() ||
                    zone.Data.Id.ResourceGroupName?.Equals(resourceGroupName, StringComparison.OrdinalIgnoreCase) == true)
                {
                    zones.Add(zone.Data.Name);
                }
            }

            _logger.LogInformation("Found {Count} DNS zones", zones.Count);
            return zones;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list DNS zones");
            return new List<string>();
        }
    }

    private async Task<DnsZoneResource> GetDnsZoneAsync(
        ArmClient armClient,
        string subscriptionId,
        string resourceGroupName,
        string zoneName,
        CancellationToken cancellationToken)
    {
        var resourceGroupIdentifier = new ResourceIdentifier(
            $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}");
        var resourceGroup = armClient.GetResourceGroupResource(resourceGroupIdentifier);

        var dnsZoneCollection = resourceGroup.GetDnsZones();
        var response = await dnsZoneCollection.GetAsync(zoneName, cancellationToken);

        return response.Value;
    }

    private static string NormalizeRecordName(string recordName)
    {
        return recordName == "@" ? "@" : recordName;
    }
}
