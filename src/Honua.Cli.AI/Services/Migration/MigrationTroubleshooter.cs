// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Migration;

/// <summary>
/// Service for providing troubleshooting guidance for migration-specific errors.
/// </summary>
public sealed class MigrationTroubleshooter
{
    /// <summary>
    /// Provides troubleshooting guidance for migration-specific errors.
    /// </summary>
    /// <param name="errorType">Error type (connection, data, geometry, performance)</param>
    /// <param name="context">Error context and messages</param>
    /// <returns>JSON troubleshooting guidance</returns>
    public string TroubleshootIssue(string errorType, string context)
    {
        var troubleshooting = (object)(errorType.ToLowerInvariant() switch
        {
            "connection" => CreateConnectionTroubleshooting(),
            "data" => CreateDataTroubleshooting(),
            "geometry" => CreateGeometryTroubleshooting(),
            "performance" => CreatePerformanceTroubleshooting(),
            _ => CreateGeneralTroubleshooting()
        });

        return JsonSerializer.Serialize(new
        {
            errorType,
            context,
            troubleshooting,
            generalTips = new[]
            {
                "Always test migration with small subset first",
                "Keep intermediate exports for debugging",
                "Document workarounds for future migrations",
                "Monitor logs in real-time during large migrations",
                "Have rollback plan ready before production migration"
            },
            support = new[]
            {
                "GDAL mailing list: gdal-dev@lists.osgeo.org",
                "ArcGIS REST API docs: https://developers.arcgis.com/rest/",
                "PostGIS documentation: https://postgis.net/docs/",
                "Honua GitHub issues: [repository]/issues"
            }
        }, CliJsonOptions.Indented);
    }

    private static object CreateConnectionTroubleshooting()
    {
        return new
        {
            issue = "Connection to ArcGIS Service Failed",
            commonCauses = new[]
            {
                "Network connectivity issues",
                "Invalid service URL or service offline",
                "Authentication required but not provided",
                "GDAL driver not supporting HTTPS/SSL"
            },
            diagnostics = new[]
            {
                "Test URL in browser: https://service/rest/services/MyService/MapServer?f=json",
                "Check GDAL drivers: ogrinfo --formats | grep -i esri",
                "Test with curl: curl -v 'https://service/MapServer?f=json'",
                "Verify network: ping service-host"
            },
            solutions = new[]
            {
                "Use correct protocol: AGS:https://... or AGS:http://...",
                "Add authentication: AGS:https://user:pass@server/...",
                "Trust SSL certificate: export GDAL_HTTP_UNSAFESSL=YES (testing only)",
                "Use intermediate export: Export to GeoJSON first, then import"
            }
        };
    }

    private static object CreateDataTroubleshooting()
    {
        return new
        {
            issue = "Data Transfer or Corruption Issues",
            commonCauses = new[]
            {
                "Large dataset timeout",
                "Field type incompatibility",
                "Special characters in field names",
                "Null geometry handling"
            },
            diagnostics = new[]
            {
                "Check layer size: curl 'service/layer/query?where=1=1&returnCountOnly=true&f=json'",
                "Inspect field types: ogrinfo -al -so 'AGS:url'",
                "Test small subset: Add -sql 'SELECT * FROM layer WHERE OBJECTID < 100'",
                "Check for NULLs: Query for features with null geometries"
            },
            solutions = new[]
            {
                "Use batch loading: Split large datasets with spatial filters -spat",
                "Skip failures: Add -skipfailures flag",
                "Map field names: Use -sql to rename problematic fields",
                "Filter null geometries: -sql 'SELECT * FROM layer WHERE Shape IS NOT NULL'",
                "Increase timeout: GDAL_HTTP_TIMEOUT=300"
            }
        };
    }

    private static object CreateGeometryTroubleshooting()
    {
        return new
        {
            issue = "Geometry Processing Errors",
            commonCauses = new[]
            {
                "Invalid geometries in source",
                "Unsupported geometry type",
                "CRS transformation failure",
                "Multi-part to single-part conversion issues"
            },
            diagnostics = new[]
            {
                "Check geometry types: ogrinfo -al -so 'AGS:url' | grep Type",
                "Validate in ArcGIS: Use Check Geometry tool",
                "Test transformation: ogr2ogr -t_srs EPSG:4326 test.geojson 'AGS:url' -limit 10",
                "Inspect invalid geometries in QGIS"
            },
            solutions = new[]
            {
                "Skip invalid: Add -skipfailures",
                "Fix geometries: Use -dialect SQLite with ST_MakeValid",
                "Simplify geometries: Add -simplify 0.0001 for Web Mercator",
                "Force geometry type: Use -nlt MULTIPOLYGON to force multi-type",
                "Transform incrementally: Test CRS transformation on small sample first"
            }
        };
    }

    private static object CreatePerformanceTroubleshooting()
    {
        return new
        {
            issue = "Slow Migration Performance",
            commonCauses = new[]
            {
                "No use of COPY protocol for PostgreSQL",
                "Spatial index creation during import",
                "Network latency",
                "Complex geometry processing"
            },
            diagnostics = new[]
            {
                "Monitor network: iftop or nethogs during migration",
                "Check database load: pg_stat_activity",
                "Profile with EXPLAIN ANALYZE for queries",
                "Monitor ogr2ogr progress with -progress flag"
            },
            solutions = new[]
            {
                "Enable COPY: Use -lco PG_USE_COPY=YES",
                "Defer spatial index: Use -lco SPATIAL_INDEX=NO, create after",
                "Batch processing: Split into chunks with -spat or -where filters",
                "Parallel import: Run multiple ogr2ogr processes for different layers",
                "Local export first: Export to GeoPackage, then import locally",
                "Optimize PostGIS: Increase work_mem, disable fsync during load (unsafe)"
            }
        };
    }

    private static object CreateGeneralTroubleshooting()
    {
        return new
        {
            issue = "General Migration Issues",
            guidance = new[]
            {
                "Review ogr2ogr error messages for specific hints",
                "Enable verbose logging: ogr2ogr --debug ON",
                "Test with minimal dataset first (-limit 10)",
                "Check GDAL version: ogrinfo --version (3.x recommended)",
                "Review ArcGIS REST API documentation for service specifics"
            },
            commonFlags = new[]
            {
                "-skipfailures: Continue on errors",
                "-progress: Show progress indicators",
                "--debug ON: Verbose logging",
                "-spat xmin ymin xmax ymax: Spatial filter",
                "-where 'OBJECTID < 1000': Attribute filter for testing"
            }
        };
    }
}
