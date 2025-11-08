// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data;

/// <summary>
/// Resolves feature context (service and layer metadata) for feature data operations.
/// Provides the necessary context for querying, editing, and managing features across different data sources.
/// </summary>
/// <remarks>
/// The feature context includes critical information such as:
/// - Service configuration and connection details
/// - Layer schema (fields, geometry type, coordinate system)
/// - Storage provider and connection settings
/// - Access permissions and capabilities
///
/// This interface is used throughout the data access pipeline to ensure consistent
/// feature operations across different OGC services (WFS, OGC API - Features, etc.).
/// </remarks>
public interface IFeatureContextResolver
{
    /// <summary>
    /// Resolves the feature context for a specific service and layer combination.
    /// </summary>
    /// <param name="serviceId">The unique identifier of the service (e.g., "buildings", "parcels").</param>
    /// <param name="layerId">The unique identifier of the layer within the service.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A <see cref="FeatureContext"/> containing service and layer metadata.</returns>
    /// <exception cref="System.ArgumentException">Thrown when serviceId or layerId is null or empty.</exception>
    /// <exception cref="Honua.Server.Core.Exceptions.LayerNotFoundException">Thrown when the specified layer cannot be found.</exception>
    Task<FeatureContext> ResolveAsync(string serviceId, string layerId, CancellationToken cancellationToken = default);
}
