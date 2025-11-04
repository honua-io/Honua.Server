// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Text.Json;

namespace Honua.Cli.AI.Services.VectorSearch;

public sealed class DeploymentPattern
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = string.Empty;
    public string CloudProvider { get; set; } = string.Empty;
    public int DataVolumeMin { get; set; }
    public int DataVolumeMax { get; set; }
    public int ConcurrentUsersMin { get; set; }
    public int ConcurrentUsersMax { get; set; }
    public double SuccessRate { get; set; }
    public int DeploymentCount { get; set; }
    public object Configuration { get; set; } = new();
    public bool HumanApproved { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime ApprovedDate { get; set; }

    // Pattern Versioning
    public int Version { get; set; } = 1;
    public string? SupersededBy { get; set; }
    public DateTime? DeprecatedDate { get; set; }
    public string? DeprecationReason { get; set; }
    public bool IsTemplate { get; set; }
    public List<string> Tags { get; set; } = new();
}

public sealed class DeploymentRequirements
{
    public int DataVolumeGb { get; set; }
    public int ConcurrentUsers { get; set; }
    public string CloudProvider { get; set; } = "aws";
    public string Region { get; set; } = "us-west-2";
}

public sealed class PatternSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string PatternName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string ConfigurationJson { get; set; } = string.Empty;
    public double SuccessRate { get; set; }
    public int DeploymentCount { get; set; }
    public string CloudProvider { get; set; } = string.Empty;
    public double Score { get; set; }
}

internal static class DeploymentPatternTextGenerator
{
    public static string CreateEmbeddingText(DeploymentPattern pattern)
    {
        return $"""
            Deployment pattern for {pattern.CloudProvider} cloud.
            Data volume: {pattern.DataVolumeMin}-{pattern.DataVolumeMax}GB.
            Concurrent users: {pattern.ConcurrentUsersMin}-{pattern.ConcurrentUsersMax}.
            Success rate: {pattern.SuccessRate * 100:0.##}% over {pattern.DeploymentCount} deployments.
            Configuration: {JsonSerializer.Serialize(pattern.Configuration)}
            """;
    }

    public static string CreateQueryText(DeploymentRequirements requirements)
    {
        return $"""
            Need deployment for {requirements.DataVolumeGb}GB data,
            {requirements.ConcurrentUsers} concurrent users,
            on {requirements.CloudProvider},
            in region {requirements.Region}
            """;
    }
}
