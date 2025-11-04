// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Diagnostics;

namespace Honua.Server.Core.Observability;

/// <summary>
/// Central telemetry configuration for Honua Server.
/// Provides ActivitySource instances for distributed tracing across different subsystems.
/// </summary>
public static class HonuaTelemetry
{
    /// <summary>
    /// Service name for OpenTelemetry
    /// </summary>
    public const string ServiceName = "Honua.Server";

    /// <summary>
    /// Service version for OpenTelemetry
    /// </summary>
    public const string ServiceVersion = "1.0.0";

    /// <summary>
    /// ActivitySource for OGC protocol operations (WMS, WFS, WMTS, WCS, CSW)
    /// </summary>
    public static readonly ActivitySource OgcProtocols = new("Honua.Server.OgcProtocols", ServiceVersion);

    /// <summary>
    /// ActivitySource for OData operations
    /// </summary>
    public static readonly ActivitySource OData = new("Honua.Server.OData", ServiceVersion);

    /// <summary>
    /// ActivitySource for STAC catalog operations
    /// </summary>
    public static readonly ActivitySource Stac = new("Honua.Server.Stac", ServiceVersion);

    /// <summary>
    /// ActivitySource for database query operations
    /// </summary>
    public static readonly ActivitySource Database = new("Honua.Server.Database", ServiceVersion);

    /// <summary>
    /// ActivitySource for raster tile operations
    /// </summary>
    public static readonly ActivitySource RasterTiles = new("Honua.Server.RasterTiles", ServiceVersion);

    /// <summary>
    /// ActivitySource for metadata operations
    /// </summary>
    public static readonly ActivitySource Metadata = new("Honua.Server.Metadata", ServiceVersion);

    /// <summary>
    /// ActivitySource for authentication and authorization
    /// </summary>
    public static readonly ActivitySource Authentication = new("Honua.Server.Authentication", ServiceVersion);

    /// <summary>
    /// ActivitySource for data export operations
    /// </summary>
    public static readonly ActivitySource Export = new("Honua.Server.Export", ServiceVersion);

    /// <summary>
    /// ActivitySource for data import and migration operations
    /// </summary>
    public static readonly ActivitySource Import = new("Honua.Server.Import", ServiceVersion);

    /// <summary>
    /// ActivitySource for notification and alerting operations
    /// </summary>
    public static readonly ActivitySource Notifications = new("Honua.Server.Notifications", ServiceVersion);
}
