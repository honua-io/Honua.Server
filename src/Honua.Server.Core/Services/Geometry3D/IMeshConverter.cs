// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.Models.Geometry3D;

namespace Honua.Server.Core.Services.Geometry3D;

/// <summary>
/// Service for converting 3D meshes to web-friendly formats for preview rendering.
/// Supports SimpleMesh format for Deck.gl and glTF JSON for advanced rendering.
/// </summary>
public interface IMeshConverter
{
    /// <summary>
    /// Converts a TriangleMesh to a simplified mesh preview response
    /// </summary>
    /// <param name="mesh">Source triangle mesh</param>
    /// <param name="levelOfDetail">Level of detail (0-100, 0 is highest quality)</param>
    /// <param name="geometryId">Geometry identifier</param>
    /// <param name="sourceFormat">Original source format</param>
    /// <returns>Mesh preview response with simplified mesh data</returns>
    Task<MeshPreviewResponse> ToSimpleMeshAsync(
        TriangleMesh mesh,
        int levelOfDetail = 0,
        Guid? geometryId = null,
        string? sourceFormat = null);

    /// <summary>
    /// Converts a TriangleMesh to glTF JSON format
    /// </summary>
    /// <param name="mesh">Source triangle mesh</param>
    /// <param name="levelOfDetail">Level of detail (0-100, 0 is highest quality)</param>
    /// <param name="geometryId">Geometry identifier</param>
    /// <param name="sourceFormat">Original source format</param>
    /// <returns>Mesh preview response with glTF data</returns>
    Task<MeshPreviewResponse> ToGltfJsonAsync(
        TriangleMesh mesh,
        int levelOfDetail = 0,
        Guid? geometryId = null,
        string? sourceFormat = null);

    /// <summary>
    /// Applies level-of-detail reduction to a mesh
    /// </summary>
    /// <param name="mesh">Source mesh</param>
    /// <param name="levelOfDetail">LOD level (0-100)</param>
    /// <returns>Simplified mesh</returns>
    Task<TriangleMesh> ApplyLevelOfDetailAsync(TriangleMesh mesh, int levelOfDetail);
}
