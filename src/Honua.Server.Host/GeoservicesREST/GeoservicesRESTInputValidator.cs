// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Honua.Server.Host.GeoservicesREST;

/// <summary>
/// Provides comprehensive input validation limits to prevent abuse of the GeoservicesREST API.
/// All limits are designed to protect against resource exhaustion while allowing legitimate use cases.
/// </summary>
internal static class GeoservicesRESTInputValidator
{
    /// <summary>
    /// Maximum length for WHERE clause in characters (4KB).
    /// </summary>
    public const int MaxWhereClauseLength = 4096;

    /// <summary>
    /// Maximum number of objectIds allowed per request.
    /// </summary>
    public const int MaxObjectIds = 1000;

    /// <summary>
    /// Maximum number of features in applyEdits operation (adds + updates + deletes combined).
    /// </summary>
    public const int MaxEditOperations = 1000;

    /// <summary>
    /// Maximum number of vertices in a geometry.
    /// </summary>
    public const int MaxGeometryVertices = 100000;

    /// <summary>
    /// Maximum number of fields in outFields parameter.
    /// </summary>
    public const int MaxOutFields = 100;

    /// <summary>
    /// Maximum number of statistics definitions in outStatistics parameter.
    /// </summary>
    public const int MaxStatisticsDefinitions = 10;

    /// <summary>
    /// Validates that a WHERE clause does not exceed the maximum allowed length.
    /// </summary>
    /// <param name="whereClause">The WHERE clause to validate.</param>
    /// <param name="httpContext">Optional HTTP context for security logging.</param>
    /// <param name="logger">Optional logger for security events.</param>
    /// <exception cref="GeoservicesRESTQueryException">Thrown when the WHERE clause exceeds the maximum length.</exception>
    public static void ValidateWhereClauseLength(string? whereClause, HttpContext? httpContext = null, ILogger? logger = null)
    {
        if (string.IsNullOrWhiteSpace(whereClause))
        {
            return;
        }

        if (whereClause.Length > MaxWhereClauseLength)
        {
            if (httpContext is not null && logger is not null)
            {
                GeoservicesRESTSecurityLogger.LogValidationFailure(
                    logger,
                    httpContext,
                    "WhereClauseLength",
                    $"Length: {whereClause.Length}, Max: {MaxWhereClauseLength}");
            }

            ThrowValidationError(
                $"WHERE clause exceeds maximum allowed length ({MaxWhereClauseLength} characters). " +
                $"Please simplify your query or use a more targeted filter.");
        }
    }

    /// <summary>
    /// Validates that the number of objectIds does not exceed the maximum allowed count.
    /// </summary>
    /// <param name="objectIdCount">The number of objectIds in the request.</param>
    /// <param name="httpContext">Optional HTTP context for security logging.</param>
    /// <param name="logger">Optional logger for security events.</param>
    /// <exception cref="GeoservicesRESTQueryException">Thrown when the objectId count exceeds the maximum.</exception>
    public static void ValidateObjectIdCount(int objectIdCount, HttpContext? httpContext = null, ILogger? logger = null)
    {
        if (objectIdCount > MaxObjectIds)
        {
            if (httpContext is not null && logger is not null)
            {
                GeoservicesRESTSecurityLogger.LogLargeRequest(
                    logger,
                    httpContext,
                    "ObjectIds",
                    objectIdCount,
                    MaxObjectIds);
            }

            ThrowValidationError(
                $"Request exceeds maximum allowed objectIds ({MaxObjectIds}). " +
                $"Please use pagination or reduce your request size.");
        }
    }

    /// <summary>
    /// Validates that the total number of edit operations does not exceed the maximum allowed count.
    /// </summary>
    /// <param name="addCount">The number of features to add.</param>
    /// <param name="updateCount">The number of features to update.</param>
    /// <param name="deleteCount">The number of features to delete.</param>
    /// <param name="httpContext">Optional HTTP context for security logging.</param>
    /// <param name="logger">Optional logger for security events.</param>
    /// <exception cref="GeoservicesRESTQueryException">Thrown when the total operation count exceeds the maximum.</exception>
    public static void ValidateEditOperationCount(int addCount, int updateCount, int deleteCount, HttpContext? httpContext = null, ILogger? logger = null)
    {
        var totalOperations = addCount + updateCount + deleteCount;

        if (totalOperations > MaxEditOperations)
        {
            if (httpContext is not null && logger is not null)
            {
                GeoservicesRESTSecurityLogger.LogLargeRequest(
                    logger,
                    httpContext,
                    "EditOperations",
                    totalOperations,
                    MaxEditOperations);
            }

            ThrowValidationError(
                $"Request exceeds maximum allowed edit operations ({MaxEditOperations}). " +
                $"Total operations: {totalOperations} (adds: {addCount}, updates: {updateCount}, deletes: {deleteCount}). " +
                $"Please split your request into smaller batches.");
        }
    }

    /// <summary>
    /// Validates that a geometry does not exceed the maximum allowed vertex count.
    /// </summary>
    /// <param name="geometry">The geometry to validate.</param>
    /// <param name="httpContext">Optional HTTP context for security logging.</param>
    /// <param name="logger">Optional logger for security events.</param>
    /// <exception cref="GeoservicesRESTQueryException">Thrown when the geometry exceeds the maximum vertex count.</exception>
    public static void ValidateGeometryComplexity(Geometry? geometry, HttpContext? httpContext = null, ILogger? logger = null)
    {
        if (geometry is null)
        {
            return;
        }

        var vertexCount = CountVertices(geometry);

        if (vertexCount > MaxGeometryVertices)
        {
            if (httpContext is not null && logger is not null)
            {
                GeoservicesRESTSecurityLogger.LogValidationFailure(
                    logger,
                    httpContext,
                    "GeometryComplexity",
                    $"VertexCount: {vertexCount}, Max: {MaxGeometryVertices}");
            }

            ThrowValidationError(
                $"Geometry exceeds maximum allowed complexity ({MaxGeometryVertices} vertices). " +
                $"Provided geometry has {vertexCount} vertices. " +
                $"Please simplify the geometry before submitting.");
        }
    }

    /// <summary>
    /// Validates that the number of output fields does not exceed the maximum allowed count.
    /// </summary>
    /// <param name="fieldCount">The number of fields requested in outFields.</param>
    /// <exception cref="GeoservicesRESTQueryException">Thrown when the field count exceeds the maximum.</exception>
    public static void ValidateOutFieldsCount(int fieldCount)
    {
        if (fieldCount > MaxOutFields)
        {
            ThrowValidationError(
                $"Request exceeds maximum allowed outFields count ({MaxOutFields}). " +
                $"Please reduce the number of requested fields or use '*' to select all fields.");
        }
    }

    /// <summary>
    /// Validates that the number of statistics definitions does not exceed the maximum allowed count.
    /// </summary>
    /// <param name="statisticsCount">The number of statistics definitions.</param>
    /// <exception cref="GeoservicesRESTQueryException">Thrown when the statistics count exceeds the maximum.</exception>
    public static void ValidateStatisticsCount(int statisticsCount)
    {
        if (statisticsCount > MaxStatisticsDefinitions)
        {
            ThrowValidationError(
                $"Request exceeds maximum allowed statistics definitions ({MaxStatisticsDefinitions}). " +
                $"Please reduce the number of statistics in your request.");
        }
    }

    /// <summary>
    /// Counts the total number of vertices in a geometry, including all sub-geometries.
    /// </summary>
    private static int CountVertices(Geometry geometry)
    {
        return geometry switch
        {
            Point => 1,
            LineString lineString => lineString.NumPoints,
            Polygon polygon => CountPolygonVertices(polygon),
            MultiPoint multiPoint => multiPoint.NumPoints,
            MultiLineString multiLineString => CountMultiLineStringVertices(multiLineString),
            MultiPolygon multiPolygon => CountMultiPolygonVertices(multiPolygon),
            GeometryCollection collection => CountGeometryCollectionVertices(collection),
            _ => 0
        };
    }

    private static int CountPolygonVertices(Polygon polygon)
    {
        var count = polygon.ExteriorRing.NumPoints;

        for (var i = 0; i < polygon.NumInteriorRings; i++)
        {
            count += polygon.GetInteriorRingN(i).NumPoints;
        }

        return count;
    }

    private static int CountMultiLineStringVertices(MultiLineString multiLineString)
    {
        var count = 0;

        for (var i = 0; i < multiLineString.NumGeometries; i++)
        {
            if (multiLineString.GetGeometryN(i) is LineString lineString)
            {
                count += lineString.NumPoints;
            }
        }

        return count;
    }

    private static int CountMultiPolygonVertices(MultiPolygon multiPolygon)
    {
        var count = 0;

        for (var i = 0; i < multiPolygon.NumGeometries; i++)
        {
            if (multiPolygon.GetGeometryN(i) is Polygon polygon)
            {
                count += CountPolygonVertices(polygon);
            }
        }

        return count;
    }

    private static int CountGeometryCollectionVertices(GeometryCollection collection)
    {
        var count = 0;

        for (var i = 0; i < collection.NumGeometries; i++)
        {
            count += CountVertices(collection.GetGeometryN(i));
        }

        return count;
    }

    private static void ThrowValidationError(string message)
    {
        throw new GeoservicesRESTQueryException(message);
    }
}
