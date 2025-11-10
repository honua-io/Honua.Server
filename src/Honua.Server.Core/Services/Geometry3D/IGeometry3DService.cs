// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using Honua.Server.Core.Models.Geometry3D;

namespace Honua.Server.Core.Services.Geometry3D;

/// <summary>
/// Service for managing complex 3D geometries (meshes, solids, parametric surfaces).
/// Supports AEC workflows with OBJ, STL, glTF file formats.
/// </summary>
public interface IGeometry3DService
{
    /// <summary>
    /// Imports a 3D geometry file (OBJ, STL, glTF, etc.)
    /// </summary>
    /// <param name="stream">File stream</param>
    /// <param name="fileName">Original file name (used to detect format)</param>
    /// <param name="request">Import options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Upload response with geometry ID and statistics</returns>
    Task<UploadGeometry3DResponse> ImportGeometryAsync(
        Stream stream,
        string fileName,
        UploadGeometry3DRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Retrieves a geometry by ID
    /// </summary>
    /// <param name="geometryId">Geometry identifier</param>
    /// <param name="includeMeshData">Whether to load the full mesh data (can be large)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Geometry metadata and optionally mesh data</returns>
    Task<ComplexGeometry3D?> GetGeometryAsync(
        Guid geometryId,
        bool includeMeshData = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Exports a geometry to a specific format
    /// </summary>
    /// <param name="geometryId">Geometry identifier</param>
    /// <param name="options">Export options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Stream containing the exported file</returns>
    Task<Stream> ExportGeometryAsync(
        Guid geometryId,
        ExportGeometry3DOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a geometry
    /// </summary>
    /// <param name="geometryId">Geometry identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task DeleteGeometryAsync(Guid geometryId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all geometries associated with a feature
    /// </summary>
    /// <param name="featureId">Feature identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of geometries</returns>
    Task<IEnumerable<ComplexGeometry3D>> GetGeometriesForFeatureAsync(
        Guid featureId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Finds geometries that intersect a bounding box
    /// </summary>
    /// <param name="bbox">Bounding box to search</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of geometries intersecting the bounding box</returns>
    Task<IEnumerable<ComplexGeometry3D>> FindGeometriesByBoundingBoxAsync(
        BoundingBox3D bbox,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates geometry metadata
    /// </summary>
    /// <param name="geometryId">Geometry identifier</param>
    /// <param name="metadata">New metadata</param>
    /// <param name="cancellationToken">Cancellation token</param>
    Task UpdateGeometryMetadataAsync(
        Guid geometryId,
        Dictionary<string, object> metadata,
        CancellationToken cancellationToken = default);
}
