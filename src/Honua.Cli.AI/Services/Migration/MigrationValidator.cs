// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Migration;

/// <summary>
/// Service for validating migration readiness and identifying compatibility issues.
/// </summary>
public sealed class MigrationValidator
{
    /// <summary>
    /// Validates migration readiness and identifies compatibility issues.
    /// </summary>
    /// <param name="sourceInfo">Source ArcGIS service info as JSON</param>
    /// <param name="targetInfo">Target Honua environment info as JSON</param>
    /// <returns>JSON readiness assessment</returns>
    public string ValidateReadiness(string sourceInfo, string targetInfo)
    {
        var readinessChecks = new object[]
        {
            CreateDataCompatibilityChecks(),
            CreateFeatureParityChecks(),
            CreateInfrastructureChecks(),
            CreateClientReadinessChecks()
        };

        var readinessScore = new
        {
            dataCompatibility = "Calculate based on checks",
            featureParity = "Calculate based on required features",
            infrastructure = "Calculate based on requirements",
            clientReadiness = "Calculate based on client needs",
            overall = "Average of categories",
            recommendation = "Proceed if overall > 80%, address gaps if 60-80%, reconsider if < 60%"
        };

        return JsonSerializer.Serialize(new
        {
            readinessChecks,
            readinessScore,
            blockers = new[]
            {
                "Identify any features in ArcGIS not available in OGC API",
                "Document unsupported geometry types or field types",
                "List client applications requiring significant updates"
            },
            actionItems = new[]
            {
                "Address all critical blockers before migration",
                "Prepare workarounds for feature gaps",
                "Schedule client application updates",
                "Plan training for new OGC API patterns",
                "Document migration requirements and timeline"
            }
        }, CliJsonOptions.Indented);
    }

    private static object CreateDataCompatibilityChecks()
    {
        return new
        {
            category = "Data Compatibility",
            checks = new object[]
            {
                new
                {
                    check = "Geometry Types",
                    status = "Verify",
                    action = "Confirm all ArcGIS geometry types (Point, Polyline, Polygon) are supported",
                    validation = "SELECT DISTINCT GeometryType(Shape) FROM arcgis_layers"
                },
                new
                {
                    check = "Spatial References",
                    status = "Map",
                    action = "Map all WKID values to EPSG codes",
                    validation = "Common: 102100->3857, 4326->4326, 102113->3785 (deprecated)"
                },
                new
                {
                    check = "Field Types",
                    status = "Verify",
                    action = "Ensure all ArcGIS field types can map to PostGIS types",
                    mapping = new
                    {
                        esriFieldTypeString = "VARCHAR or TEXT",
                        esriFieldTypeInteger = "INTEGER",
                        esriFieldTypeSmallInteger = "SMALLINT",
                        esriFieldTypeDouble = "DOUBLE PRECISION",
                        esriFieldTypeSingle = "REAL",
                        esriFieldTypeDate = "TIMESTAMP",
                        esriFieldTypeOID = "SERIAL or BIGSERIAL",
                        esriFieldTypeGlobalID = "UUID",
                        esriFieldTypeGeometry = "GEOMETRY"
                    }
                },
                new
                {
                    check = "Data Volume",
                    status = "Estimate",
                    action = "Calculate storage requirements and migration time",
                    estimation = "1M features ≈ 15-30 min migration, 100-500MB storage (geometry dependent)"
                }
            }
        };
    }

    private static object CreateFeatureParityChecks()
    {
        return new
        {
            category = "Feature Parity",
            checks = new object[]
            {
                new
                {
                    feature = "Spatial Queries",
                    arcgis = "geometry filter with spatial relationships (intersects, contains, within)",
                    ogcApi = "bbox parameter, advanced filtering with CQL2",
                    compatible = true,
                    notes = "OGC API supports bbox natively; complex spatial filters via CQL2-JSON"
                },
                new
                {
                    feature = "Attribute Queries",
                    arcgis = "WHERE clause with SQL-like syntax",
                    ogcApi = "filter parameter with CQL",
                    compatible = true,
                    notes = "Syntax differs but functionality equivalent"
                },
                new
                {
                    feature = "Pagination",
                    arcgis = "resultOffset and resultRecordCount",
                    ogcApi = "offset and limit parameters",
                    compatible = true,
                    notes = "Direct mapping: resultOffset->offset, resultRecordCount->limit"
                },
                new
                {
                    feature = "Output Formats",
                    arcgis = "JSON, GeoJSON, KML, Shapefile",
                    ogcApi = "GeoJSON, JSON-FG, HTML",
                    compatible = "Partial",
                    notes = "OGC focuses on web formats; use ogr2ogr for shapefile export if needed"
                },
                new
                {
                    feature = "Projections/CRS",
                    arcgis = "outSR parameter to reproject",
                    ogcApi = "crs parameter or Accept-Crs header",
                    compatible = true,
                    notes = "OGC uses EPSG codes; ArcGIS uses WKID (mostly same values)"
                }
            }
        };
    }

    private static object CreateInfrastructureChecks()
    {
        return new
        {
            category = "Infrastructure Readiness",
            checks = new[]
            {
                new
                {
                    requirement = "Database Capacity",
                    check = "Verify sufficient storage, memory, and CPU",
                    recommendation = "PostGIS: 2x data size for storage, 4GB+ RAM, 4+ cores"
                },
                new
                {
                    requirement = "Network Bandwidth",
                    check = "Ensure adequate bandwidth for data transfer during migration",
                    recommendation = "1 Gbps minimum for large datasets (>10GB)"
                },
                new
                {
                    requirement = "Backup Infrastructure",
                    check = "Verify backup and disaster recovery capabilities",
                    recommendation = "Automated backups, tested restore procedures, PITR"
                },
                new
                {
                    requirement = "Monitoring Tools",
                    check = "Confirm monitoring and alerting is configured",
                    recommendation = "Prometheus + Grafana, or cloud-native monitoring"
                }
            }
        };
    }

    private static object CreateClientReadinessChecks()
    {
        return new
        {
            category = "Client Application Readiness",
            checks = new object[]
            {
                new
                {
                    client = "Web Applications",
                    compatibility = "High",
                    requirement = "Update JavaScript libraries to support OGC API Features",
                    libraries = new[] { "OpenLayers 6+", "Leaflet with ogcapi plugin", "Mapbox GL JS" }
                },
                new
                {
                    client = "Desktop GIS (QGIS, ArcGIS Pro)",
                    compatibility = "High",
                    requirement = "Use WFS 3.0/OGC API Features connection",
                    notes = "QGIS 3.10+, ArcGIS Pro 2.6+ have native support"
                },
                new
                {
                    client = "Mobile Applications",
                    compatibility = "Medium",
                    requirement = "Update API endpoints and implement OGC request patterns",
                    notes = "May require mobile app updates"
                },
                new
                {
                    client = "ETL/Integration Tools",
                    compatibility = "High",
                    requirement = "Update data connectors to OGC API",
                    notes = "FME, GDAL/OGR have built-in OGC API support"
                }
            }
        };
    }
}
