// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for data ingestion analysis and recommendations.
/// Helps users load geospatial data into Honua with optimal strategies.
/// </summary>
public sealed class DataIngestionPlugin
{
    [KernelFunction, Description("Analyzes a geospatial data file and reports format, CRS, schema, and characteristics")]
    public string AnalyzeDataFile(
        [Description("Path to the geospatial data file")] string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                return JsonSerializer.Serialize(new
                {
                    success = false,
                    error = "File not found",
                    filePath
                });
            }

            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            var fileSize = new FileInfo(filePath).Length;
            var fileSizeMB = fileSize / (1024.0 * 1024.0);

            var analysis = extension switch
            {
                ".gpkg" or ".geopackage" => new
                {
                    format = "GeoPackage",
                    description = "SQLite-based vector/raster format, OGC standard",
                    characteristics = new
                    {
                        multiLayerSupport = true,
                        vectorSupport = true,
                        rasterSupport = true,
                        crsFlexibility = "High - supports any CRS",
                        performance = "Excellent for read-heavy workloads"
                    },
                    recommendedFor = new[] { "Vector layers", "Raster datasets", "Multi-layer data" },
                    limitations = "Not ideal for concurrent writes from multiple processes"
                },
                ".shp" => new
                {
                    format = "Shapefile",
                    description = "Legacy Esri format, widely supported",
                    characteristics = new
                    {
                        multiLayerSupport = false,
                        vectorSupport = true,
                        rasterSupport = false,
                        crsFlexibility = "Medium - .prj file required",
                        performance = "Good for small-medium datasets"
                    },
                    recommendedFor = new[] { "Simple vector layers", "Maximum compatibility" },
                    limitations = "2GB file size limit, no support for complex types, multiple sidecar files (.shx, .dbf, .prj)"
                },
                ".geojson" or ".json" => new
                {
                    format = "GeoJSON",
                    description = "JSON-based vector format, web-friendly",
                    characteristics = new
                    {
                        multiLayerSupport = false,
                        vectorSupport = true,
                        rasterSupport = false,
                        crsFlexibility = "Low - typically WGS84 (EPSG:4326)",
                        performance = "Slower for large datasets due to text format"
                    },
                    recommendedFor = new[] { "Web applications", "Small-medium datasets", "Human-readable data" },
                    limitations = "Text format is verbose, assumes WGS84, no native indexing"
                },
                ".tif" or ".tiff" or ".geotiff" => new
                {
                    format = "GeoTIFF",
                    description = "Tagged Image File Format with geospatial metadata",
                    characteristics = new
                    {
                        multiLayerSupport = false,
                        vectorSupport = false,
                        rasterSupport = true,
                        crsFlexibility = "High - embedded in TIFF tags",
                        performance = "Excellent with tiling and compression"
                    },
                    recommendedFor = new[] { "Satellite imagery", "DEMs", "Raster analysis" },
                    limitations = "Not suitable for vector data, can be very large"
                },
                ".kml" or ".kmz" => new
                {
                    format = extension == ".kmz" ? "KMZ (Compressed KML)" : "KML",
                    description = "Google Earth format, XML-based",
                    characteristics = new
                    {
                        multiLayerSupport = true,
                        vectorSupport = true,
                        rasterSupport = false,
                        crsFlexibility = "Low - always WGS84",
                        performance = "Slower due to XML parsing"
                    },
                    recommendedFor = new[] { "Google Earth visualization", "Simple point/line/polygon data" },
                    limitations = "Always WGS84, limited attribute support, not optimized for databases"
                },
                ".csv" => new
                {
                    format = "CSV",
                    description = "Comma-separated values with coordinate columns",
                    characteristics = new
                    {
                        multiLayerSupport = false,
                        vectorSupport = true,
                        rasterSupport = false,
                        crsFlexibility = "None - must be specified manually",
                        performance = "Slow, requires geometry construction"
                    },
                    recommendedFor = new[] { "Simple point data", "Tabular data with lat/lon" },
                    limitations = "No native spatial support, must specify CRS, only points unless using WKT"
                },
                ".gdb" => new
                {
                    format = "File Geodatabase",
                    description = "Esri File Geodatabase",
                    characteristics = new
                    {
                        multiLayerSupport = true,
                        vectorSupport = true,
                        rasterSupport = true,
                        crsFlexibility = "High",
                        performance = "Excellent (proprietary format)"
                    },
                    recommendedFor = new[] { "proprietary GIS platforms", "Complex spatial data" },
                    limitations = "Proprietary format, requires GDAL with FileGDB driver"
                },
                _ => new
                {
                    format = "Unknown",
                    description = $"Unrecognized format: {extension}",
                    characteristics = new
                    {
                        multiLayerSupport = false,
                        vectorSupport = false,
                        rasterSupport = false,
                        crsFlexibility = "Unknown",
                        performance = "Unknown"
                    },
                    recommendedFor = Array.Empty<string>(),
                    limitations = "Format not recognized by Honua"
                }
            };

            return JsonSerializer.Serialize(new
            {
                success = true,
                filePath,
                fileName = Path.GetFileName(filePath),
                extension,
                fileSizeMB = Math.Round(fileSizeMB, 2),
                analysis,
                nextSteps = new[]
                {
                    "Use 'ogrinfo' command to inspect layers and schema",
                    "Check CRS with 'gdalsrsinfo' if needed",
                    "Run 'honua data ingest' to load into Honua"
                }
            }, CliJsonOptions.Indented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                filePath
            });
        }
    }

    [KernelFunction, Description("Suggests optimal data ingestion strategy based on file type, size, and target database")]
    public string SuggestIngestionStrategy(
        [Description("File format (gpkg, shp, geojson, tif, etc.)")] string fileFormat,
        [Description("File size in MB")] double fileSizeMB,
        [Description("Target database (postgis or spatialite)")] string targetDatabase)
    {
        var isLarge = fileSizeMB > 500;
        var isVeryLarge = fileSizeMB > 5000;
        var isPostGIS = targetDatabase.Equals("postgis", StringComparison.OrdinalIgnoreCase);

        var strategies = new System.Collections.Generic.List<object>();

        // Strategy 1: Direct loading
        if (!isVeryLarge)
        {
            strategies.Add(new
            {
                approach = "Direct Loading",
                priority = isLarge ? "medium" : "high",
                description = "Use ogr2ogr to load data directly into target database",
                command = isPostGIS
                    ? $"ogr2ogr -f PostgreSQL PG:\"dbname=honua host=localhost\" \"{fileFormat}\" -nln layer_name"
                    : $"ogr2ogr -f SQLite -dsco SPATIALITE=YES honua.db \"{fileFormat}\" -nln layer_name",
                pros = new[] { "Simple", "Fast for small-medium files", "Preserves all attributes" },
                cons = isLarge ? new[] { "May be slow for large files", "No progress feedback" } : new[] { "None for this size" },
                estimatedTime = fileSizeMB < 100 ? "< 1 minute" : fileSizeMB < 500 ? "1-5 minutes" : "5-30 minutes"
            });
        }

        // Strategy 2: Batch loading with progress
        if (isLarge)
        {
            strategies.Add(new
            {
                approach = "Batch Loading",
                priority = "high",
                description = "Split data into chunks for better progress tracking and memory management",
                command = isPostGIS
                    ? "Split large file into chunks, load each with ogr2ogr -append"
                    : "Use PRAGMA statements and batch inserts",
                pros = new[] { "Progress tracking", "Lower memory usage", "Can resume if interrupted" },
                cons = new[] { "More complex", "Requires scripting" },
                estimatedTime = "10-60 minutes depending on size"
            });
        }

        // Strategy 3: Pre-processing
        if (fileFormat.Equals("shp", StringComparison.OrdinalIgnoreCase) && isLarge)
        {
            strategies.Add(new
            {
                approach = "Convert to GeoPackage First",
                priority = "high",
                description = "Convert Shapefile to GeoPackage, then load into database",
                command = "ogr2ogr -f GPKG intermediate.gpkg input.shp && ogr2ogr -f PostgreSQL PG:\"dbname=honua\" intermediate.gpkg",
                pros = new[] { "Faster loading", "Better format", "Single file" },
                cons = new[] { "Extra step", "Requires disk space" },
                estimatedTime = "Add 2-5 minutes for conversion"
            });
        }

        // Strategy 4: Parallel loading (very large files)
        if (isVeryLarge && isPostGIS)
        {
            strategies.Add(new
            {
                approach = "Parallel Loading",
                priority = "critical",
                description = "Split data spatially and load in parallel using multiple ogr2ogr processes",
                command = "Use spatial filters (-spat) to split data, run multiple ogr2ogr processes",
                pros = new[] { "Much faster for very large files", "Utilizes multi-core CPUs" },
                cons = new[] { "Complex setup", "Requires spatial extent knowledge" },
                estimatedTime = "Can reduce time by 50-75% vs sequential"
            });
        }

        // Performance optimizations
        var optimizations = new[]
        {
            new
            {
                optimization = "Disable Spatial Index During Load",
                applicability = isPostGIS && isLarge,
                command = "Add -lco SPATIAL_INDEX=NO to ogr2ogr, create index after loading",
                benefit = "20-40% faster loading"
            },
            new
            {
                optimization = "Use COPY Instead of INSERT",
                applicability = isPostGIS && isLarge,
                command = "Add -lco PG_USE_COPY=YES to ogr2ogr",
                benefit = "2-5x faster for bulk inserts"
            },
            new
            {
                optimization = "Increase Work Memory",
                applicability = isPostGIS && isLarge,
                command = "SET work_mem = '256MB' before loading",
                benefit = "Faster index creation and sorting"
            },
            new
            {
                optimization = "Use WAL Mode",
                applicability = !isPostGIS && isLarge,
                command = "PRAGMA journal_mode=WAL before loading",
                benefit = "Better write performance for SQLite"
            }
        };

        return JsonSerializer.Serialize(new
        {
            fileFormat,
            fileSizeMB,
            targetDatabase,
            isLarge,
            isVeryLarge,
            recommendedApproach = strategies.First(),
            strategies,
            optimizations = optimizations.Where(o => o.applicability).ToArray(),
            postLoadSteps = new[]
            {
                "Run VACUUM ANALYZE (PostgreSQL) or VACUUM (SQLite)",
                "Create spatial index if not created during load",
                "Validate data with SELECT ST_IsValid(geom) FROM layer",
                "Update table statistics for query optimization"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Validates data quality and identifies common issues")]
    public string ValidateDataQuality(
        [Description("Description of data quality issues or concerns")] string concerns = "general validation")
    {
        var validationChecks = new[]
        {
            new
            {
                check = "Null Geometries",
                query = "SELECT COUNT(*) FROM layer WHERE geom IS NULL",
                issue = "Records without geometries cannot be displayed on maps",
                solution = "Delete or fix records with NULL geometries",
                severity = "High"
            },
            new
            {
                check = "Invalid Geometries",
                query = "SELECT COUNT(*) FROM layer WHERE NOT ST_IsValid(geom)",
                issue = "Invalid geometries cause spatial operations to fail",
                solution = "Use ST_MakeValid(geom) or fix source data",
                severity = "Critical"
            },
            new
            {
                check = "Empty Geometries",
                query = "SELECT COUNT(*) FROM layer WHERE ST_IsEmpty(geom)",
                issue = "Empty geometries have no spatial data",
                solution = "Delete or investigate source data issues",
                severity = "Medium"
            },
            new
            {
                check = "CRS Consistency",
                query = "SELECT DISTINCT ST_SRID(geom) FROM layer",
                issue = "Mixed CRS values cause spatial query failures",
                solution = "Transform all geometries to consistent CRS",
                severity = "Critical"
            },
            new
            {
                check = "Extent Check",
                query = "SELECT ST_Extent(geom) FROM layer",
                issue = "Verify data extent matches expected geographic area",
                solution = "Check for coordinate system errors or data corruption",
                severity = "Medium"
            },
            new
            {
                check = "Duplicate Features",
                query = "SELECT geom, COUNT(*) FROM layer GROUP BY geom HAVING COUNT(*) > 1",
                issue = "Duplicate geometries inflate dataset size and slow queries",
                solution = "Remove duplicates or verify if intentional",
                severity = "Low"
            },
            new
            {
                check = "Topology Errors",
                query = "SELECT * FROM layer WHERE ST_NumInteriorRings(geom) > 0 AND ST_IsValid(geom) = false",
                issue = "Self-intersections and invalid topology",
                solution = "Use ST_MakeValid or topology cleaning tools",
                severity = "High"
            }
        };

        return JsonSerializer.Serialize(new
        {
            validationChecks,
            automatedTools = new[]
            {
                new { tool = "PostGIS ST_IsValid", usage = "Built-in validation function" },
                new { tool = "QGIS Topology Checker", usage = "Visual validation and fixing" },
                new { tool = "ogr2ogr -skipfailures", usage = "Skip invalid features during import" }
            },
            bestPractices = new[]
            {
                "Always validate geometries before publishing to production",
                "Use ST_MakeValid to automatically fix simple issues",
                "Keep a log of validation results for audit trail",
                "Test spatial queries on cleaned data before deployment"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Recommends schema mapping between source and target")]
    public string RecommendSchemaMapping(
        [Description("Source schema description or column list")] string sourceSchema,
        [Description("Target schema requirements")] string targetRequirements)
    {
        var mappingStrategies = new[]
        {
            new
            {
                strategy = "Attribute Name Normalization",
                description = "Standardize column names to OGC conventions",
                examples = new[]
                {
                    new { from = "OBJECTID", to = "id", reason = "OGC API Features uses 'id' as primary key" },
                    new { from = "SHAPE", to = "geometry", reason = "Standard geometry column name" },
                    new { from = "NAME_1, NAME_2", to = "name", reason = "Simplify attribute schema" }
                },
                command = "Use -sql with AS to rename: ogr2ogr -sql \"SELECT OBJECTID AS id, SHAPE AS geometry FROM layer\""
            },
            new
            {
                strategy = "Type Conversion",
                description = "Convert data types for optimal database storage",
                examples = new[]
                {
                    new { from = "String dates", to = "DATE/TIMESTAMP", reason = "Enable temporal queries" },
                    new { from = "Numeric strings", to = "INTEGER/DOUBLE", reason = "Better performance and storage" },
                    new { from = "Boolean text", to = "BOOLEAN", reason = "Proper type for yes/no values" }
                },
                command = "Use CAST in SQL: CAST(date_string AS DATE)"
            },
            new
            {
                strategy = "Attribute Filtering",
                description = "Include only necessary attributes to reduce storage",
                examples = new[]
                {
                    new { from = "50 columns", to = "10 essential columns", reason = "Reduce storage and improve query performance" }
                },
                command = "Use -select to pick columns: ogr2ogr -select \"id,name,category,geometry\""
            },
            new
            {
                strategy = "Value Transformation",
                description = "Transform attribute values during import",
                examples = new[]
                {
                    new { from = "Coded values (1,2,3)", to = "Descriptive strings", reason = "Improve API readability" },
                    new { from = "Various units", to = "Standard units", reason = "Consistency" }
                },
                command = "Use CASE statements in -sql"
            }
        };

        return JsonSerializer.Serialize(new
        {
            mappingStrategies,
            bestPractices = new[]
            {
                "Always include a unique identifier column (id)",
                "Use lowercase names for cross-database compatibility",
                "Avoid special characters in column names",
                "Document mapping decisions for future reference",
                "Test mapping with small sample before full import"
            },
            tools = new[]
            {
                new { tool = "ogrinfo", purpose = "Inspect source schema" },
                new { tool = "ogr2ogr -sql", purpose = "Transform data during import" },
                new { tool = "psql \\d table", purpose = "Verify target schema" }
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Generates complete data ingestion command for ogr2ogr or Honua CLI")]
    public string GenerateIngestionCommand(
        [Description("Source file path")] string sourceFile,
        [Description("Target database (postgis or spatialite)")] string targetDatabase,
        [Description("Layer/table name")] string layerName,
        [Description("Optional CRS to reproject to (e.g., EPSG:3857)")] string? targetCRS = null)
    {
        var isPostGIS = targetDatabase.Equals("postgis", StringComparison.OrdinalIgnoreCase);
        var hasReprojection = !targetCRS.IsNullOrEmpty();

        var baseCommand = isPostGIS
            ? $"ogr2ogr -f PostgreSQL PG:\"host=localhost dbname=honua user=honua_user password=${{DB_PASSWORD}}\" \"{sourceFile}\" -nln {layerName}"
            : $"ogr2ogr -f SQLite -dsco SPATIALITE=YES honua.db \"{sourceFile}\" -nln {layerName}";

        var options = new System.Collections.Generic.List<string>();

        if (hasReprojection)
        {
            options.Add($"-t_srs {targetCRS}");
        }

        // Performance options
        if (isPostGIS)
        {
            options.Add("-lco PG_USE_COPY=YES");
            options.Add("-lco GEOMETRY_NAME=geom");
            options.Add("-lco FID=id");
        }

        // Progress reporting
        options.Add("--config PG_USE_COPY YES");
        options.Add("-progress");

        var fullCommand = baseCommand + " " + string.Join(" ", options);

        var honuaCommand = $"honua data ingest --service-id {layerName} --layer-id {layerName} {sourceFile}";

        return JsonSerializer.Serialize(new
        {
            recommended = "Use Honua CLI for managed ingestion with progress tracking",
            commands = new
            {
                honuaCli = new
                {
                    command = honuaCommand,
                    description = "Managed ingestion through Honua control plane with job tracking",
                    advantages = new[] { "Progress monitoring", "Job management", "Automatic retry", "Validation" }
                },
                ogr2ogr = new
                {
                    command = fullCommand,
                    description = "Direct database ingestion using GDAL/OGR",
                    advantages = new[] { "No Honua server required", "Maximum control", "Custom SQL transforms" }
                }
            },
            preIngestionChecklist = new[]
            {
                "✓ Verify source file exists and is readable",
                "✓ Check target database connection",
                "✓ Ensure layer name is unique or use -overwrite/-append",
                "✓ Validate source data CRS matches expectations",
                "✓ Have sufficient disk space for target database"
            },
            postIngestionSteps = new[]
            {
                "Create spatial index: CREATE INDEX idx_geom ON {layerName} USING GIST(geom)",
                "Run VACUUM ANALYZE to update statistics",
                "Test query performance with sample spatial query",
                "Register layer in Honua metadata.yaml"
            }
        }, CliJsonOptions.Indented);
    }
}
