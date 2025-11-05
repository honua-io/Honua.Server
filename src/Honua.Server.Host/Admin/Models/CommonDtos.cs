// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Host.Admin.Models;

/// <summary>
/// Dashboard statistics summary.
/// </summary>
public sealed record DashboardStatsResponse
{
    public int ServiceCount { get; init; }
    public int LayerCount { get; init; }
    public int FolderCount { get; init; }
    public int DataSourceCount { get; init; }
    public bool SupportsVersioning { get; init; }
    public bool SupportsRealTimeUpdates { get; init; }
}

/// <summary>
/// Standard error response following RFC 7807 Problem Details.
/// </summary>
public sealed record ProblemDetailsResponse
{
    public required string Type { get; init; }
    public required string Title { get; init; }
    public int Status { get; init; }
    public string? Detail { get; init; }
    public Dictionary<string, object>? Extensions { get; init; }
}
