// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
namespace Honua.Server.Core.Models.Geometry3D;

/// <summary>
/// Types of complex 3D geometries supported by Honua.Server for AEC workflows.
/// </summary>
public enum GeometryType3D
{
    /// <summary>
    /// Simple 3D point (existing NetTopologySuite support)
    /// </summary>
    Point3D,

    /// <summary>
    /// 3D line string with Z coordinates (existing NetTopologySuite support)
    /// </summary>
    LineString3D,

    /// <summary>
    /// 3D polygon with Z coordinates (existing NetTopologySuite support)
    /// </summary>
    Polygon3D,

    /// <summary>
    /// Triangle mesh from OBJ, STL, glTF files
    /// </summary>
    TriangleMesh,

    /// <summary>
    /// Point cloud data (future support)
    /// </summary>
    PointCloud,

    /// <summary>
    /// Boundary representation solid (OpenCascade - future support)
    /// </summary>
    BRepSolid,

    /// <summary>
    /// Parametric surface using NURBS (OpenCascade - future support)
    /// </summary>
    ParametricSurface,

    /// <summary>
    /// Constructive solid geometry (OpenCascade - future support)
    /// </summary>
    CsgSolid
}
