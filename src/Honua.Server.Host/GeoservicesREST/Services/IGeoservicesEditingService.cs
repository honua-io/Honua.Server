// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Microsoft.AspNetCore.Http;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Service for handling Esri FeatureServer editing operations.
/// </summary>
public interface IGeoservicesEditingService
{
    /// <summary>
    /// Applies a batch of edits (adds, updates, deletes) to features.
    /// </summary>
    Task<GeoservicesEditExecutionResult> ExecuteEditsAsync(
        CatalogServiceView serviceView,
        CatalogLayerView layerView,
        JsonElement payload,
        HttpRequest request,
        string[] addPropertyNames,
        string[] updatePropertyNames,
        string[] deletePropertyNames,
        bool includeAdds,
        bool includeUpdates,
        bool includeDeletes,
        CancellationToken cancellationToken);
}
