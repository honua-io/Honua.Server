// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OData.Query;
using Microsoft.AspNetCore.OData.Routing.Controllers;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Integration.PowerBI.Models;

namespace Honua.Integration.PowerBI.OData;

/// <summary>
/// OData v4 controller for OGC Features API, optimized for Power BI connectivity.
/// Exposes feature collections as OData entity sets for direct import into Power BI.
/// </summary>
[Route("odata/features")]
[ApiController]
public class FeaturesODataController : ODataController
{
    private readonly IFeatureRepository _repository;
    private readonly IMetadataRegistry _metadataRegistry;
    private readonly ILogger<FeaturesODataController> _logger;

    public FeaturesODataController(
        IFeatureRepository repository,
        IMetadataRegistry metadataRegistry,
        ILogger<FeaturesODataController> logger)
    {
        _repository = repository;
        _metadataRegistry = metadataRegistry;
        _logger = logger;
    }

    /// <summary>
    /// Gets features from a collection with OData query support.
    /// Power BI can query this endpoint using standard OData operators.
    /// </summary>
    /// <param name="collectionId">Collection ID (service::layer format)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Queryable collection of features</returns>
    [HttpGet("{collectionId}")]
    [EnableQuery(MaxTop = 5000, PageSize = 1000)]
    public async Task<IActionResult> GetFeatures(
        string collectionId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Parse collection ID
            var parts = collectionId.Split(new[] { "::" }, StringSplitOptions.None);
            if (parts.Length != 2)
            {
                return BadRequest(new { error = "Invalid collection ID format. Expected format: 'service::layer'" });
            }

            var serviceId = parts[0];
            var layerId = parts[1];

            // Get metadata
            var snapshot = await _metadataRegistry.GetInitializedSnapshotAsync(cancellationToken);
            if (!snapshot.TryGetService(serviceId, out var service))
            {
                return NotFound(new { error = $"Service '{serviceId}' not found" });
            }

            if (!snapshot.TryGetLayer(serviceId, layerId, out var layer))
            {
                return NotFound(new { error = $"Layer '{layerId}' not found in service '{serviceId}'" });
            }

            // Query features
            var query = new FeatureQuery(
                Limit: null, // Let OData handle pagination
                Offset: null,
                Crs: "http://www.opengis.net/def/crs/EPSG/0/4326" // WGS84 for Power BI
            );

            var features = new List<PowerBIFeature>();
            await foreach (var record in _repository.QueryAsync(serviceId, layerId, query, cancellationToken))
            {
                features.Add(PowerBIFeature.FromFeatureRecord(record, layer));
            }

            return Ok(features.AsQueryable());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying features for collection {CollectionId}", collectionId);
            return StatusCode(500, new { error = "Internal server error" });
        }
    }

    /// <summary>
    /// Gets metadata about available feature collections.
    /// </summary>
    [HttpGet("$metadata")]
    public async Task<IActionResult> GetMetadata(CancellationToken cancellationToken)
    {
        try
        {
            var snapshot = await _metadataRegistry.GetInitializedSnapshotAsync(cancellationToken);
            var collections = new List<object>();

            foreach (var service in snapshot.Services.Values)
            {
                foreach (var layer in service.Layers)
                {
                    collections.Add(new
                    {
                        Name = $"{service.Id}::{layer.Id}",
                        Title = layer.Title ?? layer.Id,
                        Description = layer.Description,
                        Type = layer.GeometryType?.ToString() ?? "Unknown"
                    });
                }
            }

            return Ok(new { Collections = collections });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving metadata");
            return StatusCode(500, new { error = "Internal server error" });
        }
    }
}
