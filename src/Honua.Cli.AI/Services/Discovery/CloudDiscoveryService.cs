// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;
using Microsoft.Extensions.Logging;

namespace Honua.Cli.AI.Services.Discovery;

/// <summary>
/// Orchestrates discovery of existing cloud infrastructure (networks, databases, DNS zones).
/// </summary>
public interface ICloudDiscoveryService
{
    Task<CloudDiscoverySnapshot> DiscoverAsync(CloudDiscoveryRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// Represents a user or workflow request to inventory existing cloud resources.
/// </summary>
public sealed record CloudDiscoveryRequest(
    string CloudProvider,
    string? Region = null);

/// <summary>
/// Default discovery implementation that shells out to existing cloud CLIs.
/// </summary>
public sealed class CloudDiscoveryService : ICloudDiscoveryService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly IAwsCli _awsCli;
    private readonly IAzureCli _azureCli;
    private readonly IGcloudCli _gcloudCli;
    private readonly ILogger<CloudDiscoveryService> _logger;

    public CloudDiscoveryService(
        ILogger<CloudDiscoveryService> logger,
        IAwsCli? awsCli = null,
        IAzureCli? azureCli = null,
        IGcloudCli? gcloudCli = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _awsCli = awsCli ?? DefaultAwsCli.Shared;
        _azureCli = azureCli ?? DefaultAzureCli.Shared;
        _gcloudCli = gcloudCli ?? DefaultGcloudCli.Shared;
    }

    public async Task<CloudDiscoverySnapshot> DiscoverAsync(CloudDiscoveryRequest request, CancellationToken cancellationToken)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var provider = request.CloudProvider.ToLowerInvariant();
        return provider switch
        {
            "aws" => await DiscoverAwsAsync(request, cancellationToken).ConfigureAwait(false),
            "azure" => await DiscoverAzureAsync(request, cancellationToken).ConfigureAwait(false),
            "gcp" or "google" => await DiscoverGcpAsync(request, cancellationToken).ConfigureAwait(false),
            _ => throw new NotSupportedException($"Discovery for provider '{request.CloudProvider}' is not yet supported.")
        };
    }

    private async Task<CloudDiscoverySnapshot> DiscoverAwsAsync(CloudDiscoveryRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting AWS discovery for region {Region}", request.Region ?? "(all regions)");

        var vpcsJson = await _awsCli.ExecuteAsync(cancellationToken, "ec2", "describe-vpcs", "--output", "json");
        var subnetsJson = await _awsCli.ExecuteAsync(cancellationToken, "ec2", "describe-subnets", "--output", "json");
        var rdsJson = await _awsCli.ExecuteAsync(cancellationToken, "rds", "describe-db-instances", "--output", "json");
        var zonesJson = await _awsCli.ExecuteAsync(cancellationToken, "route53", "list-hosted-zones", "--output", "json");

        var vpcs = AwsEc2DescribeVpcsResponse.FromJson(vpcsJson);
        var subnets = AwsEc2DescribeSubnetsResponse.FromJson(subnetsJson);
        var dbInstances = AwsDescribeDbInstancesResponse.FromJson(rdsJson);
        var hostedZones = AwsListHostedZonesResponse.FromJson(zonesJson);

        var subnetsByVpc = new Dictionary<string, List<DiscoveredSubnet>>(StringComparer.OrdinalIgnoreCase);
        foreach (var subnet in subnets.Subnets)
        {
            if (!subnetsByVpc.TryGetValue(subnet.VpcId ?? string.Empty, out var list))
            {
                list = new List<DiscoveredSubnet>();
                subnetsByVpc[subnet.VpcId ?? string.Empty] = list;
            }

            var subnetName = subnet.Tags.TryGetValue("Name", out var subnetTagName)
                ? subnetTagName
                : subnet.SubnetId ?? string.Empty;

            var subnetModel = new DiscoveredSubnet(
                Id: subnet.SubnetId ?? string.Empty,
                Name: subnetName,
                Cidr: subnet.CidrBlock ?? string.Empty,
                AvailabilityZone: subnet.AvailabilityZone ?? string.Empty);

            list.Add(subnetModel);
        }

        var networks = new List<DiscoveredNetwork>();
        foreach (var vpc in vpcs.Vpcs)
        {
            subnetsByVpc.TryGetValue(vpc.VpcId ?? string.Empty, out var vpcSubnets);
            var networkName = vpc.Tags.TryGetValue("Name", out var tagName)
                ? tagName
                : vpc.VpcId ?? string.Empty;
            IReadOnlyList<DiscoveredSubnet> subnetsForNetwork = vpcSubnets is null
                ? Array.Empty<DiscoveredSubnet>()
                : vpcSubnets;

            networks.Add(new DiscoveredNetwork(
                Id: vpc.VpcId ?? string.Empty,
                Name: networkName,
                Region: vpc.Region ?? request.Region ?? string.Empty,
                Subnets: subnetsForNetwork));
        }

        var databases = new List<DiscoveredDatabase>();
        foreach (var db in dbInstances.DBInstances)
        {
            databases.Add(new DiscoveredDatabase(
                Identifier: db.DBInstanceIdentifier ?? string.Empty,
                Engine: db.Engine ?? string.Empty,
                Endpoint: db.Endpoint?.Address ?? string.Empty,
                Status: db.DBInstanceStatus ?? string.Empty));
        }

        var zones = new List<DiscoveredDnsZone>();
        foreach (var zone in hostedZones.HostedZones)
        {
            zones.Add(new DiscoveredDnsZone(
                Id: zone.Id ?? string.Empty,
                Name: zone.Name?.TrimEnd('.') ?? string.Empty,
                IsPrivate: zone.Config?.PrivateZone ?? false));
        }

        return new CloudDiscoverySnapshot(
            CloudProvider: "AWS",
            Networks: networks,
            Databases: databases,
            DnsZones: zones);
    }

    private async Task<CloudDiscoverySnapshot> DiscoverAzureAsync(CloudDiscoveryRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Azure discovery for subscription (region hint: {Region})", request.Region ?? "(unspecified)");

        var vnetsJson = await _azureCli.ExecuteAsync(cancellationToken, "network", "vnet", "list", "--output", "json");
        var sqlJson = await _azureCli.ExecuteAsync(cancellationToken, "sql", "server", "list", "--output", "json");
        var dnsJson = await _azureCli.ExecuteAsync(cancellationToken, "network", "dns", "zone", "list", "--output", "json");

        var vnets = AzureVnet.FromJson(vnetsJson);
        var sqlServers = AzureSqlServer.FromJson(sqlJson);
        var dnsZones = AzureDnsZone.FromJson(dnsJson);

        var networks = new List<DiscoveredNetwork>();
        foreach (var vnet in vnets)
        {
            var subnets = new List<DiscoveredSubnet>();
            if (vnet.Subnets is not null)
            {
                foreach (var subnet in vnet.Subnets)
                {
                    subnets.Add(new DiscoveredSubnet(
                        Id: subnet.Id ?? string.Empty,
                        Name: subnet.Name ?? subnet.Id ?? string.Empty,
                        Cidr: subnet.AddressPrefix ?? string.Empty,
                        AvailabilityZone: request.Region ?? string.Empty));
                }
            }

            networks.Add(new DiscoveredNetwork(
                Id: vnet.Id ?? string.Empty,
                Name: vnet.Name ?? string.Empty,
                Region: vnet.Location ?? request.Region ?? string.Empty,
                Subnets: subnets));
        }

        var databases = new List<DiscoveredDatabase>();
        foreach (var sql in sqlServers)
        {
            databases.Add(new DiscoveredDatabase(
                Identifier: sql.Name ?? string.Empty,
                Engine: "Azure SQL",
                Endpoint: sql.FullyQualifiedDomainName ?? string.Empty,
                Status: sql.State ?? string.Empty));
        }

        var zones = new List<DiscoveredDnsZone>();
        foreach (var zone in dnsZones)
        {
            zones.Add(new DiscoveredDnsZone(
                Id: zone.Id ?? string.Empty,
                Name: zone.Name ?? string.Empty,
                IsPrivate: (zone.ZoneType ?? string.Empty).Equals("Private", StringComparison.OrdinalIgnoreCase)));
        }

        return new CloudDiscoverySnapshot(
            CloudProvider: "Azure",
            Networks: networks,
            Databases: databases,
            DnsZones: zones);
    }

    private async Task<CloudDiscoverySnapshot> DiscoverGcpAsync(CloudDiscoveryRequest request, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting GCP discovery for project (region hint: {Region})", request.Region ?? "(unspecified)");

        var networksJson = await _gcloudCli.ExecuteAsync(cancellationToken, "compute", "networks", "list", "--format=json");
        var subnetsJson = await _gcloudCli.ExecuteAsync(cancellationToken, "compute", "networks", "subnets", "list", "--format=json");
        var sqlJson = await _gcloudCli.ExecuteAsync(cancellationToken, "sql", "instances", "list", "--format=json");
        var dnsJson = await _gcloudCli.ExecuteAsync(cancellationToken, "dns", "managed-zones", "list", "--format=json");

        var networksResponse = GcpNetwork.FromJson(networksJson);
        var subnetsResponse = GcpSubnet.FromJson(subnetsJson);
        var sqlInstances = GcpSqlInstance.FromJson(sqlJson);
        var dnsZones = GcpManagedZone.FromJson(dnsJson);

        var subnetsByNetwork = new Dictionary<string, List<DiscoveredSubnet>>(StringComparer.OrdinalIgnoreCase);
        foreach (var subnet in subnetsResponse)
        {
            if (!subnetsByNetwork.TryGetValue(subnet.Network ?? string.Empty, out var list))
            {
                list = new List<DiscoveredSubnet>();
                subnetsByNetwork[subnet.Network ?? string.Empty] = list;
            }

            list.Add(new DiscoveredSubnet(
                Id: subnet.SelfLink ?? string.Empty,
                Name: subnet.Name ?? string.Empty,
                Cidr: subnet.IpCidrRange ?? string.Empty,
                AvailabilityZone: subnet.Region ?? request.Region ?? string.Empty));
        }

        var networks = new List<DiscoveredNetwork>();
        foreach (var net in networksResponse)
        {
            subnetsByNetwork.TryGetValue(net.SelfLink ?? string.Empty, out var subnets);
            networks.Add(new DiscoveredNetwork(
                Id: net.Id ?? string.Empty,
                Name: net.Name ?? string.Empty,
                Region: request.Region ?? string.Empty,
                Subnets: subnets is null ? Array.Empty<DiscoveredSubnet>() : subnets));
        }

        var databases = new List<DiscoveredDatabase>();
        foreach (var instance in sqlInstances)
        {
            databases.Add(new DiscoveredDatabase(
                Identifier: instance.Name ?? string.Empty,
                Engine: instance.DatabaseVersion ?? string.Empty,
                Endpoint: instance.IpAddresses is { Count: > 0 } ? instance.IpAddresses[0].IpAddress ?? string.Empty : string.Empty,
                Status: instance.State ?? string.Empty));
        }

        var zones = new List<DiscoveredDnsZone>();
        foreach (var zone in dnsZones)
        {
            zones.Add(new DiscoveredDnsZone(
                Id: zone.Id ?? string.Empty,
                Name: zone.DnsName?.TrimEnd('.') ?? string.Empty,
                IsPrivate: (zone.Visibility ?? string.Empty).Equals("private", StringComparison.OrdinalIgnoreCase)));
        }

        return new CloudDiscoverySnapshot(
            CloudProvider: "GCP",
            Networks: networks,
            Databases: databases,
            DnsZones: zones);
    }

    private sealed record AwsEc2DescribeVpcsResponse(IReadOnlyList<AwsVpc> Vpcs)
    {
        public static AwsEc2DescribeVpcsResponse FromJson(string json) =>
            JsonSerializer.Deserialize<AwsEc2DescribeVpcsResponse>(json, JsonOptions)
            ?? new AwsEc2DescribeVpcsResponse(Array.Empty<AwsVpc>());
    }

    private sealed record AwsEc2DescribeSubnetsResponse(IReadOnlyList<AwsSubnet> Subnets)
    {
        public static AwsEc2DescribeSubnetsResponse FromJson(string json) =>
            JsonSerializer.Deserialize<AwsEc2DescribeSubnetsResponse>(json, JsonOptions)
            ?? new AwsEc2DescribeSubnetsResponse(Array.Empty<AwsSubnet>());
    }

    private sealed record AwsDescribeDbInstancesResponse(IReadOnlyList<AwsDbInstance> DBInstances)
    {
        public static AwsDescribeDbInstancesResponse FromJson(string json) =>
            JsonSerializer.Deserialize<AwsDescribeDbInstancesResponse>(json, JsonOptions)
            ?? new AwsDescribeDbInstancesResponse(Array.Empty<AwsDbInstance>());
    }

    private sealed record AwsListHostedZonesResponse(IReadOnlyList<AwsHostedZone> HostedZones)
    {
        public static AwsListHostedZonesResponse FromJson(string json) =>
            JsonSerializer.Deserialize<AwsListHostedZonesResponse>(json, JsonOptions)
            ?? new AwsListHostedZonesResponse(Array.Empty<AwsHostedZone>());
    }

    private sealed record AwsVpc(
        [property: JsonPropertyName("VpcId")] string? VpcId,
        [property: JsonPropertyName("CidrBlock")] string? CidrBlock,
        [property: JsonPropertyName("Region")] string? Region,
        [property: JsonIgnore] IReadOnlyList<AwsTag>? TagsRaw)
    {
        [JsonPropertyName("Tags")]
        public IReadOnlyDictionary<string, string> Tags => AwsTag.ToDictionary(TagsRaw);
    }

    private sealed record AwsSubnet(
        [property: JsonPropertyName("SubnetId")] string? SubnetId,
        [property: JsonPropertyName("VpcId")] string? VpcId,
        [property: JsonPropertyName("CidrBlock")] string? CidrBlock,
        [property: JsonPropertyName("AvailabilityZone")] string? AvailabilityZone,
        [property: JsonIgnore] IReadOnlyList<AwsTag>? TagsRaw)
    {
        [JsonPropertyName("Tags")]
        public IReadOnlyDictionary<string, string> Tags => AwsTag.ToDictionary(TagsRaw);
    }

    private sealed record AwsDbInstance(
        [property: JsonPropertyName("DBInstanceIdentifier")] string? DBInstanceIdentifier,
        [property: JsonPropertyName("Engine")] string? Engine,
        [property: JsonPropertyName("DBInstanceStatus")] string? DBInstanceStatus,
        [property: JsonPropertyName("Endpoint")] AwsDbEndpoint? Endpoint);

    private sealed record AwsDbEndpoint(
        [property: JsonPropertyName("Address")] string? Address,
        [property: JsonPropertyName("Port")] int Port);

    private sealed record AwsHostedZone(
        [property: JsonPropertyName("Id")] string? Id,
        [property: JsonPropertyName("Name")] string? Name,
        [property: JsonPropertyName("Config")] AwsHostedZoneConfig? Config);

    private sealed record AwsHostedZoneConfig(
        [property: JsonPropertyName("PrivateZone")] bool? PrivateZone);

    private sealed record AwsTag(
        [property: JsonPropertyName("Key")] string? Key,
        [property: JsonPropertyName("Value")] string? Value)
    {
        public static IReadOnlyDictionary<string, string> ToDictionary(IReadOnlyList<AwsTag>? tags)
        {
            if (tags is null || tags.Count == 0)
            {
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }

            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tag in tags)
            {
                if (!string.IsNullOrEmpty(tag.Key))
                {
                    dict[tag.Key] = tag.Value ?? string.Empty;
                }
            }

            return dict;
        }
    }

    private sealed record AzureVnet(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("subnets")] IReadOnlyList<AzureSubnet>? Subnets)
    {
        public static IReadOnlyList<AzureVnet> FromJson(string json) =>
            JsonSerializer.Deserialize<IReadOnlyList<AzureVnet>>(json, JsonOptions) ?? Array.Empty<AzureVnet>();
    }

    private sealed record AzureSubnet(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("addressPrefix")] string? AddressPrefix);

    private sealed record AzureSqlServer(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("fullyQualifiedDomainName")] string? FullyQualifiedDomainName,
        [property: JsonPropertyName("state")] string? State)
    {
        public static IReadOnlyList<AzureSqlServer> FromJson(string json) =>
            JsonSerializer.Deserialize<IReadOnlyList<AzureSqlServer>>(json, JsonOptions) ?? Array.Empty<AzureSqlServer>();
    }

    private sealed record AzureDnsZone(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("zoneType")] string? ZoneType)
    {
        public static IReadOnlyList<AzureDnsZone> FromJson(string json) =>
            JsonSerializer.Deserialize<IReadOnlyList<AzureDnsZone>>(json, JsonOptions) ?? Array.Empty<AzureDnsZone>();
    }

    private sealed record GcpNetwork(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("selfLink")] string? SelfLink)
    {
        public static IReadOnlyList<GcpNetwork> FromJson(string json) =>
            JsonSerializer.Deserialize<IReadOnlyList<GcpNetwork>>(json, JsonOptions) ?? Array.Empty<GcpNetwork>();
    }

    private sealed record GcpSubnet(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("network")] string? Network,
        [property: JsonPropertyName("ipCidrRange")] string? IpCidrRange,
        [property: JsonPropertyName("region")] string? Region,
        [property: JsonPropertyName("selfLink")] string? SelfLink)
    {
        public static IReadOnlyList<GcpSubnet> FromJson(string json) =>
            JsonSerializer.Deserialize<IReadOnlyList<GcpSubnet>>(json, JsonOptions) ?? Array.Empty<GcpSubnet>();
    }

    private sealed record GcpSqlInstance(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("state")] string? State,
        [property: JsonPropertyName("databaseVersion")] string? DatabaseVersion,
        [property: JsonPropertyName("ipAddresses")] IReadOnlyList<GcpIpAddress>? IpAddresses)
    {
        public static IReadOnlyList<GcpSqlInstance> FromJson(string json) =>
            JsonSerializer.Deserialize<IReadOnlyList<GcpSqlInstance>>(json, JsonOptions) ?? Array.Empty<GcpSqlInstance>();
    }

    private sealed record GcpIpAddress(
        [property: JsonPropertyName("ipAddress")] string? IpAddress);

    private sealed record GcpManagedZone(
        [property: JsonPropertyName("id")] string? Id,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("dnsName")] string? DnsName,
        [property: JsonPropertyName("visibility")] string? Visibility)
    {
        public static IReadOnlyList<GcpManagedZone> FromJson(string json) =>
            JsonSerializer.Deserialize<IReadOnlyList<GcpManagedZone>>(json, JsonOptions) ?? Array.Empty<GcpManagedZone>();
    }
}
