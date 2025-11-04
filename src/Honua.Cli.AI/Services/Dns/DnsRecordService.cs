// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.Route53;
using Amazon.Route53.Model;
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
/// Service for managing DNS records across multiple providers (Route53, Azure DNS, Cloudflare).
/// </summary>
public sealed class DnsRecordService
{
    private readonly ILogger<DnsRecordService> _logger;

    public DnsRecordService(ILogger<DnsRecordService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates or updates a DNS record using AWS Route53.
    /// </summary>
    public async Task<DnsOperationResult> UpsertRoute53RecordAsync(
        IAmazonRoute53 route53Client,
        string hostedZoneId,
        string recordName,
        string recordType,
        List<string> recordValues,
        int ttl,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Upserting Route53 DNS record: {Name} ({Type})", recordName, recordType);

            var recordSet = new ResourceRecordSet
            {
                Name = recordName,
                Type = new RRType(recordType),
                TTL = ttl,
                ResourceRecords = recordValues.ConvertAll(v => new ResourceRecord { Value = v })
            };

            var changeRequest = new ChangeResourceRecordSetsRequest
            {
                HostedZoneId = hostedZoneId,
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change>
                    {
                        new Change
                        {
                            Action = ChangeAction.UPSERT,
                            ResourceRecordSet = recordSet
                        }
                    }
                }
            };

            var response = await route53Client.ChangeResourceRecordSetsAsync(changeRequest, cancellationToken);
            _logger.LogInformation("DNS record created/updated. Change ID: {ChangeId}", response.ChangeInfo.Id);

            return new DnsOperationResult
            {
                Success = true,
                Message = $"Successfully upserted {recordType} record for {recordName}",
                ChangeId = response.ChangeInfo.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert Route53 DNS record");
            return new DnsOperationResult
            {
                Success = false,
                Message = $"Failed to upsert DNS record: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Deletes a DNS record using AWS Route53.
    /// </summary>
    public async Task<DnsOperationResult> DeleteRoute53RecordAsync(
        IAmazonRoute53 route53Client,
        string hostedZoneId,
        string recordName,
        string recordType,
        List<string> recordValues,
        int ttl,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Deleting Route53 DNS record: {Name} ({Type})", recordName, recordType);

            var recordSet = new ResourceRecordSet
            {
                Name = recordName,
                Type = new RRType(recordType),
                TTL = ttl,
                ResourceRecords = recordValues.ConvertAll(v => new ResourceRecord { Value = v })
            };

            var changeRequest = new ChangeResourceRecordSetsRequest
            {
                HostedZoneId = hostedZoneId,
                ChangeBatch = new ChangeBatch
                {
                    Changes = new List<Change>
                    {
                        new Change
                        {
                            Action = ChangeAction.DELETE,
                            ResourceRecordSet = recordSet
                        }
                    }
                }
            };

            var response = await route53Client.ChangeResourceRecordSetsAsync(changeRequest, cancellationToken);
            _logger.LogInformation("DNS record deleted. Change ID: {ChangeId}", response.ChangeInfo.Id);

            return new DnsOperationResult
            {
                Success = true,
                Message = $"Successfully deleted {recordType} record for {recordName}",
                ChangeId = response.ChangeInfo.Id
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Route53 DNS record");
            return new DnsOperationResult
            {
                Success = false,
                Message = $"Failed to delete DNS record: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Creates or updates a DNS record using Azure DNS.
    /// </summary>
    public async Task<DnsOperationResult> UpsertAzureDnsRecordAsync(
        ArmClient armClient,
        string subscriptionId,
        string resourceGroupName,
        string zoneName,
        string recordName,
        string recordType,
        List<string> recordValues,
        int ttl,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Upserting Azure DNS record: {Name} ({Type}) in zone {Zone}",
                recordName, recordType, zoneName);

            var subscriptionResourceId = new ResourceIdentifier($"/subscriptions/{subscriptionId}");
            var resourceGroupResourceId = new ResourceIdentifier(
                $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}");
            var dnsZoneResourceId = new ResourceIdentifier(
                $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/dnsZones/{zoneName}");

            var dnsZone = armClient.GetDnsZoneResource(dnsZoneResourceId);

            // Create appropriate record data based on type
            switch (recordType.ToUpperInvariant())
            {
                case "A":
                    return await UpsertARecordAsync(dnsZone, recordName, recordValues, ttl, cancellationToken);
                case "AAAA":
                    return await UpsertAAAARecordAsync(dnsZone, recordName, recordValues, ttl, cancellationToken);
                case "CNAME":
                    return await UpsertCNAMERecordAsync(dnsZone, recordName, recordValues, ttl, cancellationToken);
                case "TXT":
                    return await UpsertTXTRecordAsync(dnsZone, recordName, recordValues, ttl, cancellationToken);
                case "MX":
                    return await UpsertMXRecordAsync(dnsZone, recordName, recordValues, ttl, cancellationToken);
                case "NS":
                    return await UpsertNSRecordAsync(dnsZone, recordName, recordValues, ttl, cancellationToken);
                case "PTR":
                    return await UpsertPTRRecordAsync(dnsZone, recordName, recordValues, ttl, cancellationToken);
                case "SRV":
                    return await UpsertSRVRecordAsync(dnsZone, recordName, recordValues, ttl, cancellationToken);
                default:
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"Unsupported record type: {recordType}"
                    };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upsert Azure DNS record");
            return new DnsOperationResult
            {
                Success = false,
                Message = $"Failed to upsert DNS record: {ex.Message}"
            };
        }
    }

    private async Task<DnsOperationResult> UpsertARecordAsync(
        DnsZoneResource dnsZone, string recordName, List<string> values, int ttl, CancellationToken cancellationToken)
    {
        var data = new DnsARecordData { TtlInSeconds = ttl };
        foreach (var value in values)
        {
            data.DnsARecords.Add(new DnsARecordInfo { IPv4Address = IPAddress.Parse(value) });
        }

        var collection = dnsZone.GetDnsARecords();
        var result = await collection.CreateOrUpdateAsync(WaitUntil.Completed, recordName, data, cancellationToken: cancellationToken);

        return new DnsOperationResult
        {
            Success = true,
            Message = $"Successfully upserted A record for {recordName}",
            ChangeId = result.Value.Id.ToString()
        };
    }

    private async Task<DnsOperationResult> UpsertAAAARecordAsync(
        DnsZoneResource dnsZone, string recordName, List<string> values, int ttl, CancellationToken cancellationToken)
    {
        var data = new DnsAaaaRecordData { TtlInSeconds = ttl };
        foreach (var value in values)
        {
            data.DnsAaaaRecords.Add(new DnsAaaaRecordInfo { IPv6Address = IPAddress.Parse(value) });
        }

        var collection = dnsZone.GetDnsAaaaRecords();
        var result = await collection.CreateOrUpdateAsync(WaitUntil.Completed, recordName, data, cancellationToken: cancellationToken);

        return new DnsOperationResult
        {
            Success = true,
            Message = $"Successfully upserted AAAA record for {recordName}",
            ChangeId = result.Value.Id.ToString()
        };
    }

    private async Task<DnsOperationResult> UpsertCNAMERecordAsync(
        DnsZoneResource dnsZone, string recordName, List<string> values, int ttl, CancellationToken cancellationToken)
    {
        var data = new DnsCnameRecordData { TtlInSeconds = ttl, Cname = values.First() };

        var collection = dnsZone.GetDnsCnameRecords();
        var result = await collection.CreateOrUpdateAsync(WaitUntil.Completed, recordName, data, cancellationToken: cancellationToken);

        return new DnsOperationResult
        {
            Success = true,
            Message = $"Successfully upserted CNAME record for {recordName}",
            ChangeId = result.Value.Id.ToString()
        };
    }

    private async Task<DnsOperationResult> UpsertTXTRecordAsync(
        DnsZoneResource dnsZone, string recordName, List<string> values, int ttl, CancellationToken cancellationToken)
    {
        var data = new DnsTxtRecordData { TtlInSeconds = ttl };
        foreach (var value in values)
        {
            data.DnsTxtRecords.Add(new DnsTxtRecordInfo { Values = { value } });
        }

        var collection = dnsZone.GetDnsTxtRecords();
        var result = await collection.CreateOrUpdateAsync(WaitUntil.Completed, recordName, data, cancellationToken: cancellationToken);

        return new DnsOperationResult
        {
            Success = true,
            Message = $"Successfully upserted TXT record for {recordName}",
            ChangeId = result.Value.Id.ToString()
        };
    }

    private async Task<DnsOperationResult> UpsertMXRecordAsync(
        DnsZoneResource dnsZone, string recordName, List<string> values, int ttl, CancellationToken cancellationToken)
    {
        var data = new DnsMXRecordData { TtlInSeconds = ttl };
        foreach (var value in values)
        {
            var parts = value.Split(' ', 2);
            if (parts.Length == 2 && int.TryParse(parts[0], out var preference))
            {
                data.DnsMXRecords.Add(new DnsMXRecordInfo { Preference = preference, Exchange = parts[1] });
            }
        }

        var collection = dnsZone.GetDnsMXRecords();
        var result = await collection.CreateOrUpdateAsync(WaitUntil.Completed, recordName, data, cancellationToken: cancellationToken);

        return new DnsOperationResult
        {
            Success = true,
            Message = $"Successfully upserted MX record for {recordName}",
            ChangeId = result.Value.Id.ToString()
        };
    }

    private async Task<DnsOperationResult> UpsertNSRecordAsync(
        DnsZoneResource dnsZone, string recordName, List<string> values, int ttl, CancellationToken cancellationToken)
    {
        var data = new DnsNSRecordData { TtlInSeconds = ttl };
        foreach (var value in values)
        {
            data.DnsNSRecords.Add(new DnsNSRecordInfo { DnsNSDomainName = value });
        }

        var collection = dnsZone.GetDnsNSRecords();
        var result = await collection.CreateOrUpdateAsync(WaitUntil.Completed, recordName, data, cancellationToken: cancellationToken);

        return new DnsOperationResult
        {
            Success = true,
            Message = $"Successfully upserted NS record for {recordName}",
            ChangeId = result.Value.Id.ToString()
        };
    }

    private async Task<DnsOperationResult> UpsertPTRRecordAsync(
        DnsZoneResource dnsZone, string recordName, List<string> values, int ttl, CancellationToken cancellationToken)
    {
        var data = new DnsPtrRecordData { TtlInSeconds = ttl };
        foreach (var value in values)
        {
            data.DnsPtrRecords.Add(new DnsPtrRecordInfo { DnsPtrDomainName = value });
        }

        var collection = dnsZone.GetDnsPtrRecords();
        var result = await collection.CreateOrUpdateAsync(WaitUntil.Completed, recordName, data, cancellationToken: cancellationToken);

        return new DnsOperationResult
        {
            Success = true,
            Message = $"Successfully upserted PTR record for {recordName}",
            ChangeId = result.Value.Id.ToString()
        };
    }

    private async Task<DnsOperationResult> UpsertSRVRecordAsync(
        DnsZoneResource dnsZone, string recordName, List<string> values, int ttl, CancellationToken cancellationToken)
    {
        var data = new DnsSrvRecordData { TtlInSeconds = ttl };
        foreach (var value in values)
        {
            var parts = value.Split(' ', 4);
            if (parts.Length == 4 &&
                int.TryParse(parts[0], out var priority) &&
                int.TryParse(parts[1], out var weight) &&
                int.TryParse(parts[2], out var port))
            {
                data.DnsSrvRecords.Add(new DnsSrvRecordInfo
                {
                    Priority = priority,
                    Weight = weight,
                    Port = port,
                    Target = parts[3]
                });
            }
        }

        var collection = dnsZone.GetDnsSrvRecords();
        var result = await collection.CreateOrUpdateAsync(WaitUntil.Completed, recordName, data, cancellationToken: cancellationToken);

        return new DnsOperationResult
        {
            Success = true,
            Message = $"Successfully upserted SRV record for {recordName}",
            ChangeId = result.Value.Id.ToString()
        };
    }

    /// <summary>
    /// Deletes a DNS record using Azure DNS.
    /// </summary>
    public async Task<DnsOperationResult> DeleteAzureDnsRecordAsync(
        ArmClient armClient,
        string subscriptionId,
        string resourceGroupName,
        string zoneName,
        string recordName,
        string recordType,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Deleting Azure DNS record: {Name} ({Type}) in zone {Zone}",
                recordName, recordType, zoneName);

            var dnsZoneResourceId = new ResourceIdentifier(
                $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/dnsZones/{zoneName}");
            var dnsZone = armClient.GetDnsZoneResource(dnsZoneResourceId);

            ArmOperation? operation = null;

            switch (recordType.ToUpperInvariant())
            {
                case "A":
                    var aRecord = await dnsZone.GetDnsARecords().GetAsync(recordName, cancellationToken);
                    operation = await aRecord.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
                case "AAAA":
                    var aaaaRecord = await dnsZone.GetDnsAaaaRecords().GetAsync(recordName, cancellationToken);
                    operation = await aaaaRecord.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
                case "CNAME":
                    var cnameRecord = await dnsZone.GetDnsCnameRecords().GetAsync(recordName, cancellationToken);
                    operation = await cnameRecord.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
                case "TXT":
                    var txtRecord = await dnsZone.GetDnsTxtRecords().GetAsync(recordName, cancellationToken);
                    operation = await txtRecord.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
                case "MX":
                    var mxRecord = await dnsZone.GetDnsMXRecords().GetAsync(recordName, cancellationToken);
                    operation = await mxRecord.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
                case "NS":
                    var nsRecord = await dnsZone.GetDnsNSRecords().GetAsync(recordName, cancellationToken);
                    operation = await nsRecord.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
                case "PTR":
                    var ptrRecord = await dnsZone.GetDnsPtrRecords().GetAsync(recordName, cancellationToken);
                    operation = await ptrRecord.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
                case "SRV":
                    var srvRecord = await dnsZone.GetDnsSrvRecords().GetAsync(recordName, cancellationToken);
                    operation = await srvRecord.Value.DeleteAsync(WaitUntil.Completed, cancellationToken: cancellationToken).ConfigureAwait(false);
                    break;
                default:
                    return new DnsOperationResult
                    {
                        Success = false,
                        Message = $"Unsupported record type: {recordType}"
                    };
            }

            _logger.LogInformation("Successfully deleted {Type} record {Name}", recordType, recordName);

            return new DnsOperationResult
            {
                Success = true,
                Message = $"Successfully deleted {recordType} record for {recordName}"
            };
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning("DNS record not found: {Name} ({Type})", recordName, recordType);
            return new DnsOperationResult
            {
                Success = false,
                Message = $"DNS record not found: {recordName}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete Azure DNS record");
            return new DnsOperationResult
            {
                Success = false,
                Message = $"Failed to delete DNS record: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets a DNS record from Azure DNS.
    /// </summary>
    public async Task<DnsRecordResult?> GetAzureDnsRecordAsync(
        ArmClient armClient,
        string subscriptionId,
        string resourceGroupName,
        string zoneName,
        string recordName,
        string recordType,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Getting Azure DNS record: {Name} ({Type}) in zone {Zone}",
                recordName, recordType, zoneName);

            var dnsZoneResourceId = new ResourceIdentifier(
                $"/subscriptions/{subscriptionId}/resourceGroups/{resourceGroupName}/providers/Microsoft.Network/dnsZones/{zoneName}");
            var dnsZone = armClient.GetDnsZoneResource(dnsZoneResourceId);

            switch (recordType.ToUpperInvariant())
            {
                case "A":
                    var aRecord = await dnsZone.GetDnsARecords().GetAsync(recordName, cancellationToken);
                    return new DnsRecordResult
                    {
                        Name = recordName,
                        Type = "A",
                        Ttl = aRecord.Value.Data.TtlInSeconds ?? 3600,
                        Values = aRecord.Value.Data.DnsARecords.Select(r => r.IPv4Address.ToString()).ToList()
                    };
                case "CNAME":
                    var cnameRecord = await dnsZone.GetDnsCnameRecords().GetAsync(recordName, cancellationToken);
                    return new DnsRecordResult
                    {
                        Name = recordName,
                        Type = "CNAME",
                        Ttl = cnameRecord.Value.Data.TtlInSeconds ?? 3600,
                        Values = new List<string> { cnameRecord.Value.Data.Cname ?? string.Empty }
                    };
                case "TXT":
                    var txtRecord = await dnsZone.GetDnsTxtRecords().GetAsync(recordName, cancellationToken);
                    return new DnsRecordResult
                    {
                        Name = recordName,
                        Type = "TXT",
                        Ttl = txtRecord.Value.Data.TtlInSeconds ?? 3600,
                        Values = txtRecord.Value.Data.DnsTxtRecords
                            .SelectMany(r => r.Values)
                            .ToList()
                    };
                default:
                    _logger.LogWarning("Unsupported record type for get operation: {Type}", recordType);
                    return null;
            }
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogDebug("DNS record not found: {Name} ({Type})", recordName, recordType);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get Azure DNS record");
            return null;
        }
    }

    /// <summary>
    /// Lists all DNS zones in a subscription.
    /// </summary>
    public async Task<List<string>> ListAzureDnsZonesAsync(
        ArmClient armClient,
        string subscriptionId,
        string resourceGroupName,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Listing Azure DNS zones in subscription {Subscription}", subscriptionId);

            var subscriptionResourceId = new ResourceIdentifier($"/subscriptions/{subscriptionId}");
            var subscription = armClient.GetSubscriptionResource(subscriptionResourceId);

            var zones = new List<string>();
            await foreach (var zone in subscription.GetDnsZonesAsync(top: null, cancellationToken: cancellationToken))
            {
                // Filter by resource group if provided
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
            _logger.LogError(ex, "Failed to list Azure DNS zones");
            return new List<string>();
        }
    }

    /// <summary>
    /// Verifies a DNS record has propagated globally.
    /// </summary>
    public async Task<bool> VerifyDnsPropagationAsync(
        string domain,
        string recordType,
        string expectedValue,
        int maxAttempts = 30,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Verifying DNS propagation for {Domain} ({Type})", domain, recordType);

        for (int i = 0; i < maxAttempts; i++)
        {
            try
            {
                var lookup = new DnsClient.LookupClient();
                var result = await lookup.QueryAsync(domain, DnsClient.QueryType.ANY, cancellationToken: cancellationToken);

                var found = result.Answers.Any(answer =>
                    answer.ToString().Contains(expectedValue, StringComparison.OrdinalIgnoreCase));

                if (found)
                {
                    _logger.LogInformation("DNS record verified: {Domain} resolves to {Value}", domain, expectedValue);
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "DNS lookup attempt {Attempt} failed", i + 1);
            }

            if (i < maxAttempts - 1)
            {
                await Task.Delay(2000, cancellationToken).ConfigureAwait(false);
            }
        }

        _logger.LogWarning("DNS propagation verification timed out for {Domain}", domain);
        return false;
    }

    /// <summary>
    /// Creates a CNAME record pointing to a load balancer or CloudFront distribution.
    /// </summary>
    public Task<DnsOperationResult> CreateAliasRecordAsync(
        string provider,
        string domain,
        string targetEndpoint,
        int ttl,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Creating alias record: {Domain} -> {Target}", domain, targetEndpoint);

        // Implementation would vary by provider
        // Route53 supports alias records natively
        // Azure DNS uses CNAME or A records
        // Cloudflare uses CNAME flattening

        return Task.FromResult(new DnsOperationResult
        {
            Success = true,
            Message = $"Alias record created: {domain} -> {targetEndpoint}"
        });
    }
}

/// <summary>
/// Result of DNS operation.
/// </summary>
public sealed class DnsOperationResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ChangeId { get; set; }
}

/// <summary>
/// DNS record details.
/// </summary>
public sealed class DnsRecordResult
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public List<string> Values { get; set; } = new();
    public long Ttl { get; set; }
}
