// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Migration;

/// <summary>
/// Service for creating detailed migration plans from ArcGIS to Honua.
/// </summary>
public sealed class MigrationPlanner
{
    /// <summary>
    /// Creates a detailed migration plan from ArcGIS to Honua.
    /// </summary>
    /// <param name="serviceMetadata">Service metadata as JSON (layers, fields, geometry types)</param>
    /// <param name="targetConfig">Target Honua configuration as JSON (database type, deployment mode)</param>
    /// <returns>JSON migration plan with phases and tasks</returns>
    public string CreateMigrationPlan(string serviceMetadata, string targetConfig)
    {
        var migrationPhases = new object[]
        {
            new
            {
                phase = 1,
                name = "Pre-Migration Assessment",
                duration = "1-3 days",
                tasks = new[]
                {
                    new
                    {
                        task = "Analyze source ArcGIS service",
                        actions = new[]
                        {
                            "Document all layers and their schemas",
                            "Record current query patterns and usage",
                            "Identify custom functionality (geoprocessing, custom operations)",
                            "Map spatial references to EPSG codes",
                            "Estimate total data volume"
                        },
                        deliverable = "Migration Assessment Report",
                        commands = (string[]?)null,
                        approaches = (object[]?)null,
                        transformations = (string[]?)null,
                        sqlExample = (string?)null,
                        mappings = (object[]?)null,
                        tests = (string[]?)null,
                        items = (string[]?)null,
                        steps = (string[]?)null,
                        deliverables = (string[]?)null
                    },
                    new
                    {
                        task = "Define success criteria",
                        actions = new[]
                        {
                            "Identify critical layers that must migrate",
                            "Define acceptable downtime window",
                            "Establish performance baselines (query time, throughput)",
                            "Document client applications and dependencies"
                        },
                        deliverable = "Success Criteria Document",
                        commands = (string[]?)null,
                        approaches = (object[]?)null,
                        transformations = (string[]?)null,
                        sqlExample = (string?)null,
                        mappings = (object[]?)null,
                        tests = (string[]?)null,
                        items = (string[]?)null,
                        steps = (string[]?)null,
                        deliverables = (string[]?)null
                    }
                }
            },
            new
            {
                phase = 2,
                name = "Infrastructure Setup",
                duration = "1-2 days",
                tasks = new[]
                {
                    new
                    {
                        task = "Provision Honua infrastructure",
                        actions = new[]
                        {
                            "Set up PostGIS or SpatiaLite database",
                            "Install Honua server and CLI tools",
                            "Configure network and security (TLS, firewall)",
                            "Set up monitoring and logging infrastructure"
                        },
                        commands = new object[]
                        {
                            "docker run -d --name honua-postgis -p 5432:5432 postgis/postgis:16-3.4",
                            "dotnet tool install -g honua-cli",
                            "honua config init --host https://new-server.example.com"
                        },
                        deliverable = (string?)null,
                        approaches = (object[]?)null,
                        transformations = (string[]?)null,
                        sqlExample = (string?)null,
                        mappings = (object[]?)null,
                        tests = (string[]?)null,
                        items = (string[]?)null,
                        steps = (string[]?)null,
                        deliverables = (string[]?)null
                    },
                    new
                    {
                        task = "Configure metadata framework",
                        actions = new[]
                        {
                            "Initialize metadata.yaml configuration",
                            "Map ArcGIS layer definitions to OGC collections",
                            "Configure CRS support for each layer"
                        },
                        commands = new object[]
                        {
                            "honua metadata init --format yaml",
                            "# Edit metadata.yaml with collection definitions"
                        },
                        deliverable = (string?)null,
                        approaches = (object[]?)null,
                        transformations = (string[]?)null,
                        sqlExample = (string?)null,
                        mappings = (object[]?)null,
                        tests = (string[]?)null,
                        items = (string[]?)null,
                        steps = (string[]?)null,
                        deliverables = (string[]?)null
                    }
                }
            },
            CreateDataMigrationPhase(),
            CreateServiceConfigurationPhase(),
            CreateTestingPhase(),
            CreateCutoverPhase(),
            CreatePostMigrationPhase()
        };

        return JsonSerializer.Serialize(new
        {
            migrationPlan = migrationPhases,
            totalEstimatedDuration = "7-17 days",
            criticalSuccessFactors = new[]
            {
                "Complete and accurate source service analysis",
                "Proper spatial reference and geometry type mapping",
                "Adequate testing before cutover",
                "Clear rollback plan",
                "Stakeholder communication throughout"
            },
            riskMitigation = new[]
            {
                new { risk = "Data loss during migration", mitigation = "Multiple backups, validation queries, dry-run migrations" },
                new { risk = "Performance degradation", mitigation = "Load testing, index optimization, caching strategy" },
                new { risk = "Client compatibility issues", mitigation = "Early client testing, OGC standards compliance" },
                new { risk = "Extended downtime", mitigation = "Phased migration, parallel run period, quick rollback" }
            },
            recommendedApproach = "Pilot migration with non-critical layer, validate, then scale to all layers"
        }, CliJsonOptions.Indented);
    }

    private static object CreateDataMigrationPhase()
    {
        return new
        {
            phase = 3,
            name = "Data Migration",
            duration = "1-5 days (depending on volume)",
            tasks = new object[]
            {
                new
                {
                    task = "Extract data from ArcGIS",
                    actions = (string[]?)null,
                    commands = (string[]?)null,
                    deliverable = (string?)null,
                    approaches = new object[]
                    {
                        new
                        {
                            method = "GDAL/OGR Direct Read",
                            command = "ogr2ogr -f PostgreSQL PG:\"dbname=honua\" \"AGS:https://server/arcgis/rest/services/MyService/FeatureServer\" -sql \"SELECT * FROM layer_0\"",
                            pros = new[] { "Direct pipeline", "No intermediate files", "Streaming" },
                            cons = new[] { "Network dependent", "May timeout on large datasets" }
                        },
                        new
                        {
                            method = "Export to GeoJSON then Import",
                            command = "ogr2ogr output.geojson 'AGS:url' && ogr2ogr -f PostgreSQL PG:\"...\" output.geojson",
                            pros = new[] { "Inspectable intermediate format", "Resumable" },
                            cons = new[] { "Requires disk space", "Extra step" }
                        },
                        new
                        {
                            method = "Export to GeoPackage then Import",
                            command = "ogr2ogr output.gpkg 'AGS:url' && ogr2ogr -f PostgreSQL PG:\"...\" output.gpkg",
                            pros = new[] { "Efficient intermediate format", "Supports multiple layers" },
                            cons = new[] { "Requires GDAL 2.x+" }
                        }
                    },
                    transformations = (string[]?)null,
                    sqlExample = (string?)null,
                    mappings = (object[]?)null,
                    tests = (string[]?)null,
                    items = (string[]?)null,
                    steps = (string[]?)null,
                    deliverables = (string[]?)null
                },
                new
                {
                    task = "Field mapping and transformation",
                    actions = (string[]?)null,
                    commands = (string[]?)null,
                    deliverable = (string?)null,
                    approaches = (object[]?)null,
                    transformations = new object[]
                    {
                        "Map OBJECTID to id (primary key)",
                        "Convert Shape/SHAPE to geom or geometry",
                        "Transform date fields from epoch to ISO 8601",
                        "Map coded value domains to lookup tables or ENUM types",
                        "Handle NULL geometries (filter or fix)"
                    },
                    sqlExample = @"INSERT INTO honua_layer (id, name, geom)
SELECT OBJECTID AS id, NAME AS name, ST_Transform(Shape, 4326) AS geom
FROM arcgis_import
WHERE Shape IS NOT NULL;",
                    mappings = (object[]?)null,
                    tests = (string[]?)null,
                    items = (string[]?)null,
                    steps = (string[]?)null,
                    deliverables = (string[]?)null
                },
                new
                {
                    task = "Post-migration optimization",
                    actions = new object[]
                    {
                        "CREATE INDEX idx_geom ON layer USING GIST(geom);",
                        "VACUUM ANALYZE layer;",
                        "UPDATE statistics for query optimizer",
                        "Validate geometries: SELECT COUNT(*) WHERE NOT ST_IsValid(geom)"
                    },
                    commands = (string[]?)null,
                    deliverable = (string?)null,
                    approaches = (object[]?)null,
                    transformations = (string[]?)null,
                    sqlExample = (string?)null,
                    mappings = (object[]?)null,
                    tests = (string[]?)null,
                    items = (string[]?)null,
                    steps = (string[]?)null,
                    deliverables = (string[]?)null
                }
            }
        };
    }

    private static object CreateServiceConfigurationPhase()
    {
        return new
        {
            phase = 4,
            name = "Service Configuration",
            duration = "1 day",
            tasks = new object[]
            {
                new
                {
                    task = "Configure OGC collections",
                    actions = new object[]
                    {
                        "Map each ArcGIS layer to OGC collection in metadata.yaml",
                        "Set appropriate extent, CRS, and links",
                        "Configure pagination limits (match ArcGIS maxRecordCount)",
                        "Add descriptive titles and keywords"
                    },
                    commands = (string[]?)null,
                    deliverable = (string?)null,
                    approaches = (object[]?)null,
                    transformations = (string[]?)null,
                    sqlExample = (string?)null,
                    mappings = (object[]?)null,
                    tests = (string[]?)null,
                    items = (string[]?)null,
                    steps = (string[]?)null,
                    deliverables = (string[]?)null
                },
                new
                {
                    task = "Implement query compatibility",
                    actions = (string[]?)null,
                    commands = (string[]?)null,
                    deliverable = (string?)null,
                    approaches = (object[]?)null,
                    transformations = (string[]?)null,
                    sqlExample = (string?)null,
                    mappings = new object[]
                    {
                        new { arcgis = "where=POPULATION>100000", ogc = "filter=POPULATION>100000 (CQL)" },
                        new { arcgis = "geometry=bbox&geometryType=esriGeometryEnvelope", ogc = "bbox=minx,miny,maxx,maxy" },
                        new { arcgis = "outFields=NAME,POP", ogc = "properties=NAME,POP" },
                        new { arcgis = "returnCountOnly=true", ogc = "resulttype=hits" }
                    },
                    tests = (string[]?)null,
                    items = (string[]?)null,
                    steps = (string[]?)null,
                    deliverables = (string[]?)null
                }
            }
        };
    }

    private static object CreateTestingPhase()
    {
        return new
        {
            phase = 5,
            name = "Testing and Validation",
            duration = "2-3 days",
            tasks = new object[]
            {
                new
                {
                    task = "Functional testing",
                    actions = (string[]?)null,
                    commands = (string[]?)null,
                    deliverable = (string?)null,
                    approaches = (object[]?)null,
                    transformations = (string[]?)null,
                    sqlExample = (string?)null,
                    mappings = (object[]?)null,
                    tests = new object[]
                    {
                        "Verify all layers accessible via OGC API",
                        "Test spatial queries (bbox, intersects)",
                        "Validate attribute queries and filtering",
                        "Test pagination with various limits",
                        "Verify CRS transformations",
                        "Compare feature counts: ArcGIS vs Honua"
                    },
                    items = (string[]?)null,
                    steps = (string[]?)null,
                    deliverables = (string[]?)null
                },
                new
                {
                    task = "Performance testing",
                    actions = (string[]?)null,
                    commands = (string[]?)null,
                    deliverable = (string?)null,
                    approaches = (object[]?)null,
                    transformations = (string[]?)null,
                    sqlExample = (string?)null,
                    mappings = (object[]?)null,
                    tests = new object[]
                    {
                        "Benchmark query response times",
                        "Load test with expected concurrency",
                        "Compare performance to ArcGIS baseline",
                        "Test large result set handling",
                        "Verify caching effectiveness"
                    },
                    items = (string[]?)null,
                    steps = (string[]?)null,
                    deliverables = (string[]?)null
                },
                new
                {
                    task = "Client application testing",
                    actions = new object[]
                    {
                        "Update client apps to use OGC API endpoints",
                        "Test with QGIS, ArcGIS Pro, OpenLayers",
                        "Verify web map integrations",
                        "Test mobile applications if applicable"
                    },
                    commands = (string[]?)null,
                    deliverable = (string?)null,
                    approaches = (object[]?)null,
                    transformations = (string[]?)null,
                    sqlExample = (string?)null,
                    mappings = (object[]?)null,
                    tests = (string[]?)null,
                    items = (string[]?)null,
                    steps = (string[]?)null,
                    deliverables = (string[]?)null
                }
            }
        };
    }

    private static object CreateCutoverPhase()
    {
        return new
        {
            phase = 6,
            name = "Cutover and Deployment",
            duration = "1 day",
            tasks = new object[]
            {
                new
                {
                    task = "Pre-cutover checklist",
                    actions = (string[]?)null,
                    commands = (string[]?)null,
                    deliverable = (string?)null,
                    approaches = (object[]?)null,
                    transformations = (string[]?)null,
                    sqlExample = (string?)null,
                    mappings = (object[]?)null,
                    tests = (string[]?)null,
                    items = new object[]
                    {
                        "✓ All layers migrated and validated",
                        "✓ Performance meets or exceeds baseline",
                        "✓ Client applications tested",
                        "✓ Monitoring and alerts configured",
                        "✓ Backup and rollback plan documented",
                        "✓ Stakeholders notified of cutover window"
                    },
                    steps = (string[]?)null,
                    deliverables = (string[]?)null
                },
                new
                {
                    task = "Cutover execution",
                    actions = (string[]?)null,
                    commands = (string[]?)null,
                    deliverable = (string?)null,
                    approaches = (object[]?)null,
                    transformations = (string[]?)null,
                    sqlExample = (string?)null,
                    mappings = (object[]?)null,
                    tests = (string[]?)null,
                    items = (string[]?)null,
                    steps = new object[]
                    {
                        "1. Enable maintenance mode on ArcGIS service",
                        "2. Perform final data sync (if incremental)",
                        "3. Update DNS or load balancer to point to Honua",
                        "4. Monitor for errors in first 15 minutes",
                        "5. Gradually increase traffic (canary deployment)",
                        "6. Validate critical workflows",
                        "7. Declare migration complete or rollback"
                    },
                    deliverables = (string[]?)null
                }
            }
        };
    }

    private static object CreatePostMigrationPhase()
    {
        return new
        {
            phase = 7,
            name = "Post-Migration",
            duration = "Ongoing",
            tasks = new object[]
            {
                new
                {
                    task = "Monitoring and optimization",
                    actions = new object[]
                    {
                        "Monitor error rates and performance metrics",
                        "Optimize slow queries identified in logs",
                        "Tune database and caching configuration",
                        "Gather user feedback and address issues"
                    },
                    commands = (string[]?)null,
                    deliverable = (string?)null,
                    approaches = (object[]?)null,
                    transformations = (string[]?)null,
                    sqlExample = (string?)null,
                    mappings = (object[]?)null,
                    tests = (string[]?)null,
                    items = (string[]?)null,
                    steps = (string[]?)null,
                    deliverables = (string[]?)null
                },
                new
                {
                    task = "Documentation and training",
                    actions = (string[]?)null,
                    commands = (string[]?)null,
                    deliverable = (string?)null,
                    approaches = (object[]?)null,
                    transformations = (string[]?)null,
                    sqlExample = (string?)null,
                    mappings = (object[]?)null,
                    tests = (string[]?)null,
                    items = (string[]?)null,
                    steps = (string[]?)null,
                    deliverables = new object[]
                    {
                        "API documentation with examples",
                        "Migration lessons learned report",
                        "User training materials for OGC API",
                        "Operations runbook for Honua"
                    }
                }
            }
        };
    }
}
