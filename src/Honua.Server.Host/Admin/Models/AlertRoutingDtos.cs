// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Admin.Models;

/// <summary>
/// Alert routing configuration.
/// </summary>
public sealed record AlertRoutingConfigurationResponse
{
    public List<AlertRoutingRuleDto> Routes { get; init; } = new();
    public AlertRoutingRuleDto? DefaultRoute { get; init; }
}

/// <summary>
/// Request to update alert routing configuration.
/// </summary>
public sealed record UpdateAlertRoutingConfigurationRequest
{
    public required List<AlertRoutingRuleDto> Routes { get; init; } = new();
    public AlertRoutingRuleDto? DefaultRoute { get; init; }
}

/// <summary>
/// Alert routing rule.
/// </summary>
public sealed record AlertRoutingRuleDto
{
    public string? Name { get; init; }
    public Dictionary<string, string> Matchers { get; init; } = new();
    public List<long> NotificationChannelIds { get; init; } = new();
    public bool Continue { get; init; } = false; // If true, continue to next matching route
}
