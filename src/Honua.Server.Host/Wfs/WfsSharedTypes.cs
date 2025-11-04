// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Xml.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Honua.Server.Core.Editing;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Host.Wfs;

/// <summary>
/// Shared constants, namespaces, and types used across WFS handlers.
/// </summary>
internal static class WfsConstants
{
    // XML Namespaces
    public static readonly XNamespace Wfs = "http://www.opengis.net/wfs/2.0";
    public static readonly XNamespace Ows = "http://www.opengis.net/ows/1.1";
    public static readonly XNamespace Xs = "http://www.w3.org/2001/XMLSchema";
    public static readonly XNamespace Gml = "http://www.opengis.net/gml/3.2";
    public static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";
    public static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    public static readonly XNamespace Fes = "http://www.opengis.net/fes/2.0";

    // Format constants
    public const string GeoJsonFormat = "application/geo+json";
    public const string GmlFormat = "application/gml+xml; version=3.2";
    public const string CsvFormat = "text/csv";
    public const string ShapefileFormat = "application/x-shapefile";

    // Default values
    public const int DefaultCount = 100;
    public static readonly TimeSpan DefaultLockDuration = TimeSpan.FromMinutes(5);

    // Shared GeoJSON reader
    public static readonly GeoJsonReader GeoJsonReader = new();
}

/// <summary>
/// Represents the execution plan for a WFS feature query.
/// </summary>
internal sealed record FeatureQueryExecution(
    FeatureContext Context,
    FeatureQuery ResultQuery,
    FeatureQuery CountQuery,
    FeatureResultType ResultType,
    string OutputFormat,
    string ResponseCrsUrn,
    int Srid,
    string RequestedCrs);

/// <summary>
/// Represents the result of executing a WFS feature query.
/// </summary>
internal sealed record FeatureQueryExecutionResult(
    FeatureQueryExecution Execution,
    long NumberMatched,
    IReadOnlyList<WfsFeature> Features);

/// <summary>
/// Represents a transaction entry with its command and result.
/// </summary>
internal sealed record TransactionEntry(
    FeatureEditCommand Command,
    string? FallbackId,
    FeatureEditCommandResult Result);

/// <summary>
/// Represents a WFS feature with its record and geometry.
/// </summary>
internal sealed record WfsFeature(FeatureRecord Record, Geometry? Geometry);
