// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Honua.Admin.Blazor.Shared.Models;

/// <summary>
/// Request to create a new service.
/// </summary>
public sealed class CreateServiceRequest
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("serviceType")]
    public required string ServiceType { get; set; }

    [JsonPropertyName("folderId")]
    public string? FolderId { get; set; }

    [JsonPropertyName("ogcOptions")]
    public ServiceOgcOptions? OgcOptions { get; set; }
}

/// <summary>
/// Request to update an existing service.
/// </summary>
public sealed class UpdateServiceRequest
{
    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("serviceType")]
    public required string ServiceType { get; set; }

    [JsonPropertyName("folderId")]
    public string? FolderId { get; set; }

    [JsonPropertyName("ogcOptions")]
    public ServiceOgcOptions? OgcOptions { get; set; }
}

/// <summary>
/// Service response model.
/// </summary>
public sealed class ServiceResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("serviceType")]
    public required string ServiceType { get; set; }

    [JsonPropertyName("folderId")]
    public string? FolderId { get; set; }

    [JsonPropertyName("layerCount")]
    public int LayerCount { get; set; }

    [JsonPropertyName("ogcOptions")]
    public ServiceOgcOptions? OgcOptions { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTimeOffset? CreatedAt { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// Service list item (lightweight).
/// </summary>
public sealed class ServiceListItem
{
    [JsonPropertyName("id")]
    public required string Id { get; set; }

    [JsonPropertyName("title")]
    public required string Title { get; set; }

    [JsonPropertyName("serviceType")]
    public required string ServiceType { get; set; }

    [JsonPropertyName("folderId")]
    public string? FolderId { get; set; }

    [JsonPropertyName("layerCount")]
    public int LayerCount { get; set; }

    [JsonPropertyName("updatedAt")]
    public DateTimeOffset? UpdatedAt { get; set; }
}

/// <summary>
/// OGC service options.
/// </summary>
public sealed class ServiceOgcOptions
{
    [JsonPropertyName("wmsEnabled")]
    public bool WmsEnabled { get; set; }

    [JsonPropertyName("wfsEnabled")]
    public bool WfsEnabled { get; set; }

    [JsonPropertyName("wmtsEnabled")]
    public bool WmtsEnabled { get; set; }

    [JsonPropertyName("ogcApiEnabled")]
    public bool OgcApiEnabled { get; set; }

    [JsonPropertyName("maxFeatures")]
    public int? MaxFeatures { get; set; }
}

/// <summary>
/// Dashboard statistics.
/// </summary>
public sealed class DashboardStats
{
    [JsonPropertyName("serviceCount")]
    public int ServiceCount { get; set; }

    [JsonPropertyName("layerCount")]
    public int LayerCount { get; set; }

    [JsonPropertyName("folderCount")]
    public int FolderCount { get; set; }

    [JsonPropertyName("dataSourceCount")]
    public int DataSourceCount { get; set; }

    [JsonPropertyName("supportsVersioning")]
    public bool SupportsVersioning { get; set; }
}
