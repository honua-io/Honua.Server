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

        // Get metadata snapshot synchronously (we're in DI registration context)
        var snapshot = metadataRegistry.GetSnapshotAsync(CancellationToken.None)
            .GetAwaiter()
            .GetResult();

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
