// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;

namespace Honua.Cli.AI.Services.Discovery;

/// <summary>
/// Represents the normalized inventory collected from a cloud account.
/// </summary>
public sealed record CloudDiscoverySnapshot(
    string CloudProvider,
    IReadOnlyList<DiscoveredNetwork> Networks,
    IReadOnlyList<DiscoveredDatabase> Databases,
    IReadOnlyList<DiscoveredDnsZone> DnsZones);

/// <summary>
/// Describes an existing VPC / virtual network.
/// </summary>
public sealed record DiscoveredNetwork(
    string Id,
    string Name,
    string Region,
    IReadOnlyList<DiscoveredSubnet> Subnets);

/// <summary>
/// Describes an existing subnet (or equivalent construct).
/// </summary>
public sealed record DiscoveredSubnet(
    string Id,
    string Name,
    string Cidr,
    string AvailabilityZone);

/// <summary>
/// Represents a managed database instance that could be reused.
/// </summary>
public sealed record DiscoveredDatabase(
    string Identifier,
    string Engine,
    string Endpoint,
    string Status);

/// <summary>
/// Represents an existing DNS hosted zone.
/// </summary>
public sealed record DiscoveredDnsZone(
    string Id,
    string Name,
    bool IsPrivate);
