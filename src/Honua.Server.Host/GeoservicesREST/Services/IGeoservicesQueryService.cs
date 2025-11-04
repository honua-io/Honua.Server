// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Microsoft.AspNetCore.Mvc;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Service for handling Esri FeatureServer query operations.
/// </summary>
public interface IGeoservicesQueryService
{
    public sealed record GeoservicesIdsQueryResult(
        IReadOnlyList<object> ObjectIds,
        bool ExceededTransferLimit);

    /// <summary>
    /// Executes a feature query against the specified layer.
    /// </summary>
    Task<IActionResult> ExecuteQueryAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Executes a query for related records.
    /// </summary>
    Task<IActionResult> ExecuteRelatedRecordsQueryAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        Microsoft.AspNetCore.Http.HttpRequest request,
        CancellationToken cancellationToken);

    /// <summary>
    /// Fetches feature IDs matching the query.
    /// </summary>
    Task<GeoservicesIdsQueryResult> FetchIdsAsync(
        string serviceId,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Calculates the spatial extent for features matching the query.
    /// </summary>
    Task<GeoservicesRESTExtent?> CalculateExtentAsync(
        string serviceId,
        LayerDefinition layer,
        GeoservicesRESTQueryContext context,
        CancellationToken cancellationToken);
}
