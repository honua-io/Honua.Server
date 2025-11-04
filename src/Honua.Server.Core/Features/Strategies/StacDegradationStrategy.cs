// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Features.Strategies;

/// <summary>
/// Adaptive STAC service that falls back to basic metadata when STAC is unavailable.
/// </summary>
public sealed class AdaptiveStacService
{
    private readonly AdaptiveFeatureService _adaptiveFeature;
    private readonly ILogger<AdaptiveStacService> _logger;

    public AdaptiveStacService(
        AdaptiveFeatureService adaptiveFeature,
        ILogger<AdaptiveStacService> logger)
    {
        _adaptiveFeature = adaptiveFeature ?? throw new ArgumentNullException(nameof(adaptiveFeature));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets metadata for a dataset, using STAC if available or basic metadata as fallback.
    /// </summary>
    public async Task<DatasetMetadata> GetDatasetMetadataAsync(
        string datasetId,
        Func<string, CancellationToken, Task<StacItem?>> getStacFunc,
        Func<string, CancellationToken, Task<BasicMetadata>> getBasicFunc,
        CancellationToken cancellationToken = default)
    {
        var strategy = await _adaptiveFeature.GetMetadataStrategyAsync(cancellationToken);

        switch (strategy)
        {
            case MetadataStrategy.FullStac:
                try
                {
                    var stacItem = await getStacFunc(datasetId, cancellationToken);
                    if (stacItem != null)
                    {
                        return new DatasetMetadata
                        {
                            DatasetId = datasetId,
                            MetadataType = "STAC",
                            StacItem = stacItem,
                            IsCached = false
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to retrieve STAC metadata for dataset {DatasetId}, falling back to basic metadata",
                        datasetId);
                }
                break;

            case MetadataStrategy.CachedStac:
                _logger.LogDebug(
                    "Using cached STAC metadata for dataset {DatasetId} (STAC catalog degraded)",
                    datasetId);

                try
                {
                    var cachedStac = await getStacFunc(datasetId, cancellationToken);
                    if (cachedStac != null)
                    {
                        return new DatasetMetadata
                        {
                            DatasetId = datasetId,
                            MetadataType = "STAC (Cached)",
                            StacItem = cachedStac,
                            IsCached = true,
                            Warning = "STAC catalog is degraded, metadata may be stale"
                        };
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to retrieve cached STAC metadata for dataset {DatasetId}, falling back to basic metadata",
                        datasetId);
                }
                break;
        }

        // Fallback to basic metadata
        _logger.LogDebug(
            "Using basic metadata for dataset {DatasetId} (STAC unavailable)",
            datasetId);

        var basicMetadata = await getBasicFunc(datasetId, cancellationToken);
        return new DatasetMetadata
        {
            DatasetId = datasetId,
            MetadataType = "Basic",
            BasicMetadata = basicMetadata,
            IsCached = false
        };
    }
}

/// <summary>
/// Dataset metadata response with degradation information.
/// </summary>
public sealed class DatasetMetadata
{
    public required string DatasetId { get; init; }
    public required string MetadataType { get; init; }
    public StacItem? StacItem { get; init; }
    public BasicMetadata? BasicMetadata { get; init; }
    public bool IsCached { get; init; }
    public string? Warning { get; init; }
}

/// <summary>
/// STAC item placeholder (actual implementation would be more complex).
/// </summary>
public sealed class StacItem
{
    public required string Id { get; init; }
    public required string Type { get; init; }
    public object? Properties { get; init; }
    public object? Geometry { get; init; }
}

/// <summary>
/// Basic metadata structure.
/// </summary>
public sealed class BasicMetadata
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string[]? Keywords { get; init; }
    public DateTime? Created { get; init; }
    public DateTime? Updated { get; init; }
}
