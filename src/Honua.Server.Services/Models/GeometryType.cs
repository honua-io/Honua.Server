// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

namespace Honua.Server.Services.Models;

/// <summary>
/// Geometry types supported by the system
/// </summary>
public enum GeometryType
{
    Point,
    LineString,
    Polygon,
    MultiPoint,
    MultiLineString,
    MultiPolygon,
    Circle // Special case, stored as Point with radius property
}
