// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Metadata;
using Microsoft.OData.Edm;
using Microsoft.OData.ModelBuilder;

namespace Honua.Server.Host.OData;

/// <summary>
/// Builds OData EDM (Entity Data Model) from Honua layer definitions.
/// This model is required by Microsoft.AspNetCore.OData middleware.
/// </summary>
public sealed class ODataEdmModelBuilder
{
    /// <summary>
    /// Builds an EDM model from the metadata registry.
    /// Creates entity types and entity sets for all layers configured for OData.
    /// </summary>
    public static IEdmModel BuildModel(IMetadataRegistry metadataRegistry)
    {
        var builder = new ODataConventionModelBuilder();

        // Try to get snapshot without blocking first
        MetadataSnapshot snapshot;
        if (!metadataRegistry.TryGetSnapshot(out snapshot))
        {
            // BLOCKING ASYNC CALL: This is acceptable here because:
            // 1. We're in OData middleware configuration, which is synchronous by design
            // 2. This runs during application startup, BEFORE the app starts serving requests
            // 3. EDM model building is required before OData middleware can be configured
            // 4. This is one-time initialization, not on the request hot path
            // 5. The metadata registry should already be initialized by this point in startup
            // If metadata isn't ready, we must wait for it to complete initialization
            snapshot = metadataRegistry.GetSnapshotAsync(CancellationToken.None)
                .AsTask()
                .GetAwaiter()
                .GetResult();
        }

        // Create entity sets for each layer
        foreach (var service in snapshot.Services)
        {
            foreach (var layer in service.Layers)
            {
                // Entity set name: {serviceId}_{layerId} with hyphens converted to underscores
                var entitySetName = $"{service.Id}_{layer.Id}".Replace("-", "_");

                // Create a dynamic entity type
                // For now, use a generic feature type - we can enhance this later to reflect actual schemas
                var entityType = builder.AddEntityType(typeof(ODataFeature));
                entityType.HasKey(typeof(ODataFeature).GetProperty(nameof(ODataFeature.Id))!);

                // Add the entity set
                builder.AddEntitySet(entitySetName, builder.EntitySets.FirstOrDefault()?.EntityType ?? entityType);
            }
        }

        return builder.GetEdmModel();
    }
}

/// <summary>
/// Generic OData feature entity type.
/// Represents a feature with an ID and optional geometry.
/// </summary>
public class ODataFeature
{
    public string Id { get; set; } = string.Empty;
    public string? Geometry { get; set; }
}
