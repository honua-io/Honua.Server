// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Collections.Generic;
using System.Text.Json.Nodes;
using System.Threading;
using NetTopologySuite.Geometries;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Geoservices.GeometryService;

/// <summary>
/// Serializer for converting between geometry objects and JSON representations.
/// Supports ArcGIS REST API geometry format for Geoservices REST compatibility.
/// </summary>
/// <remarks>
/// This interface handles geometry serialization for the ArcGIS Geoservices REST API,
/// which uses a different geometry format than standard GeoJSON. The serializer converts
/// between NetTopologySuite geometry objects and the Esri JSON geometry format.
///
/// Supported geometry types:
/// - Point (esriGeometryPoint)
/// - Multipoint (esriGeometryMultipoint)
/// - Polyline (esriGeometryPolyline)
/// - Polygon (esriGeometryPolygon)
/// - Envelope (esriGeometryEnvelope)
/// </remarks>
public interface IGeometrySerializer
{
    /// <summary>
    /// Deserializes JSON geometries into NetTopologySuite geometry objects.
    /// </summary>
    /// <param name="payload">The JSON node containing geometry data in Esri JSON format.</param>
    /// <param name="geometryType">The Esri geometry type (e.g., "esriGeometryPoint", "esriGeometryPolygon").</param>
    /// <param name="srid">The spatial reference ID (coordinate system) for the geometries.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A read-only list of deserialized <see cref="Geometry"/> objects.</returns>
    /// <exception cref="System.ArgumentException">Thrown when geometryType is invalid or not supported.</exception>
    /// <exception cref="System.Text.Json.JsonException">Thrown when payload cannot be parsed.</exception>
    IReadOnlyList<Geometry> DeserializeGeometries(JsonNode? payload, string geometryType, int srid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes multiple NetTopologySuite geometries into Esri JSON format.
    /// </summary>
    /// <param name="geometries">The geometries to serialize.</param>
    /// <param name="geometryType">The Esri geometry type for the output.</param>
    /// <param name="srid">The spatial reference ID (coordinate system) for the geometries.</param>
    /// <param name="cancellationToken">Cancellation token for the operation.</param>
    /// <returns>A <see cref="JsonObject"/> containing the serialized geometries in Esri JSON format.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when geometries is null.</exception>
    JsonObject SerializeGeometries(IReadOnlyList<Geometry> geometries, string geometryType, int srid, CancellationToken cancellationToken = default);

    /// <summary>
    /// Serializes a single NetTopologySuite geometry into Esri JSON format.
    /// </summary>
    /// <param name="geometry">The geometry to serialize.</param>
    /// <param name="geometryType">The Esri geometry type for the output.</param>
    /// <param name="srid">The spatial reference ID (coordinate system) for the geometry.</param>
    /// <returns>A <see cref="JsonObject"/> containing the serialized geometry in Esri JSON format.</returns>
    /// <exception cref="System.ArgumentNullException">Thrown when geometry is null.</exception>
    JsonObject SerializeGeometry(Geometry geometry, string geometryType, int srid);
}
