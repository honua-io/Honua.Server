// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.Metadata;

/// <summary>
/// Orchestrates the validation of all metadata definitions.
/// Coordinates specialized validators in the correct dependency order.
/// </summary>
internal static class MetadataValidator
{
    /// <summary>
    /// Validates all metadata definitions by calling specialized validators in dependency order.
    /// </summary>
    /// <param name="catalog">The catalog definition.</param>
    /// <param name="server">The server definition.</param>
    /// <param name="folders">The folder definitions.</param>
    /// <param name="dataSources">The data source definitions.</param>
    /// <param name="services">The service definitions.</param>
    /// <param name="layers">The layer definitions.</param>
    /// <param name="rasterDatasets">The raster dataset definitions.</param>
    /// <param name="styles">The style definitions.</param>
    /// <param name="layerGroups">The layer group definitions.</param>
    /// <param name="logger">Optional logger for warnings.</param>
    /// <exception cref="System.IO.InvalidDataException">Thrown when validation fails.</exception>
    public static void Validate(
        CatalogDefinition catalog,
        ServerDefinition server,
        IReadOnlyList<FolderDefinition> folders,
        IReadOnlyList<DataSourceDefinition> dataSources,
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<LayerDefinition> layers,
        IReadOnlyList<RasterDatasetDefinition> rasterDatasets,
        IReadOnlyList<StyleDefinition> styles,
        IReadOnlyList<LayerGroupDefinition> layerGroups,
        ILogger? logger)
    {
        // Phase 1: Validate catalog and server (no dependencies)
        CatalogValidator.Validate(catalog, server);

        // Phase 2: Validate and collect IDs for basic entities (no dependencies)
        var folderIds = FolderValidator.ValidateAndGetIds(folders);
        var dataSourceIds = DataSourceValidator.ValidateAndGetIds(dataSources);
        var styleIds = StyleValidator.ValidateAndGetIds(styles);

        // Phase 3: Validate services (depends on folders and data sources)
        var serviceIds = ServiceValidator.ValidateAndGetIds(services, folderIds, dataSourceIds);

        // Phase 4: Validate layers (depends on services and styles)
        var layerIds = LayerValidator.ValidateAndGetIds(layers, serviceIds, styleIds, logger);

        // Phase 5: Validate raster datasets (depends on services, layers, and styles)
        var rasterIds = RasterDatasetValidator.ValidateAndGetIds(rasterDatasets, serviceIds, layerIds, styleIds);

        // Phase 6: Validate layer groups (depends on services, layers, and styles)
        var layerGroupIds = LayerGroupValidator.ValidateAndGetIds(layerGroups, serviceIds, layerIds, layers, styleIds);

        // Phase 7: Protocol-specific validation (warnings only)
        ValidateProtocolRequirements(services, layers, rasterDatasets);
    }

    /// <summary>
    /// Validates protocol-specific requirements.
    /// This is kept for backward compatibility but no longer performs validation
    /// as it was moved to HCL schema validation.
    /// </summary>
    /// <param name="services">The service definitions.</param>
    /// <param name="layers">The layer definitions.</param>
    /// <param name="rasterDatasets">The raster dataset definitions.</param>
    private static void ValidateProtocolRequirements(
        IReadOnlyList<ServiceDefinition> services,
        IReadOnlyList<LayerDefinition> layers,
        IReadOnlyList<RasterDatasetDefinition> rasterDatasets)
    {
        // Protocol-specific validation has been removed
        // ProtocolMetadataValidator was deleted as part of Configuration V2 migration
        // Validation is now handled by HCL schema validation
    }
}
