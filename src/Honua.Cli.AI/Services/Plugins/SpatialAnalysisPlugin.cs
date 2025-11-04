// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for spatial analysis and CRS guidance.
/// Provides AI with spatial operation recommendations and coordinate system expertise.
/// </summary>
public sealed class SpatialAnalysisPlugin
{
    [KernelFunction, Description("Validates geometries and provides fixing recommendations")]
    public string ValidateGeometries(
        [Description("Layer information as JSON (table name, geometry type, sample issues)")] string layerInfo = "{\"tableName\":\"test_layer\",\"geometryType\":\"Polygon\"}",
        [Description("Data file path (optional - for file-based validation)")] string dataFile = "")
    {
        var validationChecks = new object[]
        {
            new
            {
                check = "Null Geometries",
                severity = "High",
                query = "SELECT id FROM {table} WHERE geom IS NULL",
                impact = "Features will be invisible on maps and fail spatial queries",
                fixes = new[]
                {
                    "DELETE FROM {table} WHERE geom IS NULL; -- Remove features without geometry",
                    "UPDATE {table} SET geom = ST_Point(0, 0) WHERE geom IS NULL; -- Placeholder for required geometry",
                    "Investigate data source for missing geometries"
                }
            },
            new
            {
                check = "Invalid Geometries",
                severity = "Critical",
                query = "SELECT id, ST_IsValidReason(geom) FROM {table} WHERE NOT ST_IsValid(geom)",
                impact = "Spatial operations will fail or return incorrect results",
                fixes = new[]
                {
                    "UPDATE {table} SET geom = ST_MakeValid(geom) WHERE NOT ST_IsValid(geom);",
                    "UPDATE {table} SET geom = ST_Buffer(geom, 0) WHERE NOT ST_IsValid(geom); -- Alternative fix",
                    "For complex issues: Use ST_IsValidDetail to get specific error locations"
                },
                validation = "SELECT COUNT(*) FROM {table} WHERE NOT ST_IsValid(geom) -- Should return 0"
            },
            new
            {
                check = "Empty Geometries",
                severity = "Medium",
                query = "SELECT id FROM {table} WHERE ST_IsEmpty(geom)",
                impact = "Geometries exist but contain no coordinates",
                fixes = new[]
                {
                    "DELETE FROM {table} WHERE ST_IsEmpty(geom);",
                    "UPDATE {table} SET geom = NULL WHERE ST_IsEmpty(geom); -- Then handle as null"
                }
            },
            new
            {
                check = "Self-Intersecting Polygons",
                severity = "High",
                query = "SELECT id FROM {table} WHERE GeometryType(geom) = 'POLYGON' AND NOT ST_IsSimple(geom)",
                impact = "Area calculations incorrect, topology operations fail",
                fixes = new[]
                {
                    "UPDATE {table} SET geom = ST_MakeValid(geom) WHERE NOT ST_IsSimple(geom);",
                    "Use ST_UnaryUnion for complex multi-part overlaps",
                    "Manual editing may be required for critical features"
                }
            },
            new
            {
                check = "Duplicate Vertices",
                severity = "Low",
                query = "SELECT id, ST_NPoints(geom) FROM {table} WHERE ST_NPoints(geom) > 1000",
                impact = "Increased storage, slower rendering and processing",
                fixes = new[]
                {
                    "UPDATE {table} SET geom = ST_SimplifyPreserveTopology(geom, 0.0001) WHERE ST_NPoints(geom) > 1000;",
                    "Use ST_RemoveRepeatedPoints(geom) for exact duplicates",
                    "Adjust tolerance based on data precision requirements"
                }
            },
            new
            {
                check = "Mixed Dimensions (2D/3D)",
                severity = "Medium",
                query = "SELECT id, ST_CoordDim(geom) FROM {table} GROUP BY ST_CoordDim(geom)",
                impact = "Inconsistent geometry dimensions cause processing issues",
                fixes = new[]
                {
                    "UPDATE {table} SET geom = ST_Force2D(geom); -- Force all to 2D",
                    "UPDATE {table} SET geom = ST_Force3D(geom, 0); -- Force all to 3D with Z=0",
                    "Standardize on either 2D or 3D for entire dataset"
                }
            },
            new
            {
                check = "SRID Consistency",
                severity = "Critical",
                query = "SELECT DISTINCT ST_SRID(geom) FROM {table}",
                impact = "Mixed coordinate systems break spatial relationships",
                fixes = new[]
                {
                    "UPDATE {table} SET geom = ST_SetSRID(geom, 4326) WHERE ST_SRID(geom) = 0; -- Set SRID",
                    "UPDATE {table} SET geom = ST_Transform(geom, 4326) WHERE ST_SRID(geom) != 4326; -- Transform to target CRS",
                    "Verify source data CRS before transformation"
                }
            },
            new
            {
                check = "Out of Bounds Coordinates",
                severity = "High",
                query = "SELECT id FROM {table} WHERE ST_XMax(geom) > 180 OR ST_XMin(geom) < -180 OR ST_YMax(geom) > 90 OR ST_YMin(geom) < -90",
                impact = "Invalid coordinates for geographic CRS (WGS84)",
                fixes = new[]
                {
                    "Verify correct SRID - coordinates may be in projected CRS",
                    "UPDATE {table} SET geom = ST_SetSRID(geom, 3857) WHERE ST_XMax(geom) > 180; -- Likely Web Mercator",
                    "Clip to valid extent: ST_Intersection(geom, ST_MakeEnvelope(-180, -90, 180, 90, 4326))"
                }
            }
        };

        var automatedValidation = new
        {
            comprehensiveCheck = @"
-- Comprehensive geometry validation report
SELECT
    'Null Geometries' AS check_type,
    COUNT(*) AS issue_count,
    'High' AS severity
FROM {table}
WHERE geom IS NULL

UNION ALL

SELECT
    'Invalid Geometries',
    COUNT(*),
    'Critical'
FROM {table}
WHERE NOT ST_IsValid(geom)

UNION ALL

SELECT
    'Empty Geometries',
    COUNT(*),
    'Medium'
FROM {table}
WHERE ST_IsEmpty(geom)

UNION ALL

SELECT
    'Self-Intersecting',
    COUNT(*),
    'High'
FROM {table}
WHERE GeometryType(geom) LIKE '%POLYGON%' AND NOT ST_IsSimple(geom)

UNION ALL

SELECT
    'High Vertex Count',
    COUNT(*),
    'Low'
FROM {table}
WHERE ST_NPoints(geom) > 1000

ORDER BY
    CASE severity
        WHEN 'Critical' THEN 1
        WHEN 'High' THEN 2
        WHEN 'Medium' THEN 3
        ELSE 4
    END;",

            batchFix = @"
-- Automated batch geometry fix
BEGIN;

-- Fix invalid geometries
UPDATE {table}
SET geom = ST_MakeValid(geom)
WHERE NOT ST_IsValid(geom);

-- Remove exact duplicate vertices
UPDATE {table}
SET geom = ST_RemoveRepeatedPoints(geom, 0.00000001);

-- Simplify high-complexity geometries
UPDATE {table}
SET geom = ST_SimplifyPreserveTopology(geom, 0.0001)
WHERE ST_NPoints(geom) > 5000;

-- Ensure consistent SRID
UPDATE {table}
SET geom = ST_SetSRID(geom, 4326)
WHERE ST_SRID(geom) = 0;

-- Update statistics
VACUUM ANALYZE {table};

COMMIT;

-- Validate fixes
SELECT COUNT(*) AS remaining_issues
FROM {table}
WHERE NOT ST_IsValid(geom);",

            qgisValidation = new
            {
                tool = "QGIS Geometry Checker",
                usage = "Vector > Geometry Tools > Check Validity",
                features = new[]
                {
                    "Visual identification of geometry issues",
                    "Interactive fixing with preview",
                    "Topology rule validation",
                    "Export validation report"
                }
            }
        };

        return JsonSerializer.Serialize(new
        {
            validationChecks,
            automatedValidation,
            bestPractices = new[]
            {
                "Always validate geometries before publishing to production",
                "Test fixes on a copy of data first",
                "Document validation results and fixes applied",
                "Re-validate after fixes to ensure resolution",
                "Set up automated validation in CI/CD pipeline",
                "Monitor geometry quality metrics over time"
            },
            performanceImpact = new
            {
                invalidGeometries = "Can cause query failures and incorrect spatial results",
                highVertexCount = "Slows rendering and spatial operations by 10-100x",
                mixedSRID = "Prevents spatial indexes from being used effectively"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Suggests appropriate spatial operations based on user intent")]
    public string SuggestSpatialOperations(
        [Description("User intent description (e.g., 'find overlapping areas', 'points within polygon')")] string userIntent)
    {
        var intentLower = userIntent.ToLowerInvariant();
        var suggestions = new System.Collections.Generic.List<object>();

        if (intentLower.Contains("overlap") || intentLower.Contains("intersect"))
        {
            suggestions.Add(new
            {
                operation = "Intersection",
                function = "ST_Intersects(geom_a, geom_b)",
                useCase = "Find features that spatially overlap",
                example = "SELECT * FROM layer_a WHERE ST_Intersects(geom, (SELECT geom FROM layer_b WHERE id = 1))",
                returnType = "Boolean (true if intersect)",
                performance = "Use spatial index, very fast with GIST index"
            });

            suggestions.Add(new
            {
                operation = "Intersection Geometry",
                function = "ST_Intersection(geom_a, geom_b)",
                useCase = "Get the overlapping geometry portion",
                example = "SELECT id, ST_Intersection(a.geom, b.geom) AS overlap_geom FROM layer_a a, layer_b b WHERE ST_Intersects(a.geom, b.geom)",
                returnType = "Geometry (the overlapping part)",
                performance = "Computationally expensive, filter with ST_Intersects first"
            });
        }

        if (intentLower.Contains("within") || intentLower.Contains("inside") || intentLower.Contains("contain"))
        {
            suggestions.Add(new
            {
                operation = "Within/Contains",
                function = "ST_Within(geom_a, geom_b) or ST_Contains(geom_b, geom_a)",
                useCase = "Find points/features completely inside a polygon",
                example = @"
-- Points within polygon
SELECT p.* FROM points p, polygons poly
WHERE ST_Within(p.geom, poly.geom) AND poly.id = 1;

-- Which polygon contains each point
SELECT p.id AS point_id, poly.id AS polygon_id
FROM points p
JOIN polygons poly ON ST_Contains(poly.geom, p.geom);",
                returnType = "Boolean",
                performance = "Excellent with spatial index"
            });
        }

        if (intentLower.Contains("buffer") || intentLower.Contains("expand") || intentLower.Contains("radius"))
        {
            suggestions.Add(new
            {
                operation = "Buffer",
                function = "ST_Buffer(geom, distance)",
                useCase = "Create area around features (e.g., 500m radius around points)",
                example = @"
-- 500 meter buffer around points (geometry in meters, e.g., EPSG:3857)
SELECT id, ST_Buffer(geom, 500) AS buffer_geom FROM points;

-- Buffer in degrees (for EPSG:4326, approximate)
SELECT id, ST_Buffer(geom::geography, 500)::geometry AS buffer_geom FROM points;

-- Find features within buffer
SELECT p.* FROM points p
WHERE ST_DWithin(p.geom, ST_Point(-122.4, 37.8), 1000); -- 1km",
                returnType = "Geometry (polygon)",
                performance = "Medium - can be optimized with geography type"
            });
        }

        if (intentLower.Contains("distance") || intentLower.Contains("nearest") || intentLower.Contains("closest"))
        {
            suggestions.Add(new
            {
                operation = "Distance Calculation",
                function = "ST_Distance(geom_a, geom_b)",
                useCase = "Calculate distance between features",
                example = @"
-- Distance between two points (cartesian)
SELECT ST_Distance(
    ST_Point(-122.4, 37.8),
    ST_Point(-122.5, 37.9)
);

-- Distance in meters (use geography)
SELECT ST_Distance(
    ST_Point(-122.4, 37.8)::geography,
    ST_Point(-122.5, 37.9)::geography
) AS distance_meters;

-- Find 5 nearest features
SELECT id, ST_Distance(geom, ST_Point(-122.4, 37.8)) AS dist
FROM points
ORDER BY geom <-> ST_Point(-122.4, 37.8)
LIMIT 5;",
                returnType = "Double (distance in CRS units or meters for geography)",
                performance = "Use <-> operator with GIST index for nearest neighbor"
            });
        }

        if (intentLower.Contains("union") || intentLower.Contains("merge") || intentLower.Contains("dissolve"))
        {
            suggestions.Add(new
            {
                operation = "Union/Dissolve",
                function = "ST_Union(geom) or ST_UnaryUnion(geom)",
                useCase = "Merge multiple features into single geometry",
                example = @"
-- Dissolve all features into one
SELECT ST_Union(geom) AS merged_geom FROM polygons;

-- Dissolve by attribute
SELECT category, ST_Union(geom) AS merged_geom
FROM polygons
GROUP BY category;

-- Remove internal boundaries
SELECT ST_UnaryUnion(ST_Collect(geom)) FROM polygons;",
                returnType = "Geometry (merged)",
                performance = "Expensive for large datasets, consider ST_Subdivide first"
            });
        }

        if (intentLower.Contains("clip") || intentLower.Contains("cut") || intentLower.Contains("extract"))
        {
            suggestions.Add(new
            {
                operation = "Clip/Intersection",
                function = "ST_Intersection(geom, clip_boundary)",
                useCase = "Extract portion of features within boundary",
                example = @"
-- Clip to bounding box
SELECT id, ST_Intersection(
    geom,
    ST_MakeEnvelope(-122.5, 37.7, -122.3, 37.9, 4326)
) AS clipped_geom
FROM layer
WHERE ST_Intersects(geom, ST_MakeEnvelope(-122.5, 37.7, -122.3, 37.9, 4326));",
                returnType = "Geometry (clipped)",
                performance = "Use ST_Intersects filter first"
            });
        }

        if (intentLower.Contains("area") || intentLower.Contains("perimeter") || intentLower.Contains("length"))
        {
            suggestions.Add(new
            {
                operation = "Measurement",
                functions = new[]
                {
                    "ST_Area(geom) - Calculate area",
                    "ST_Perimeter(geom) - Calculate perimeter",
                    "ST_Length(geom) - Calculate line length"
                },
                useCase = "Calculate geometric measurements",
                example = @"
-- Area in square meters (use geography)
SELECT id, ST_Area(geom::geography) AS area_sqm FROM polygons;

-- Area in acres (square meters / 4046.86)
SELECT id, ST_Area(geom::geography) / 4046.86 AS area_acres FROM polygons;

-- Length of roads in kilometers
SELECT id, ST_Length(geom::geography) / 1000 AS length_km FROM roads;",
                returnType = "Double (in CRS units or meters for geography)",
                performance = "Fast - no spatial index needed"
            });
        }

        if (suggestions.Count == 0)
        {
            suggestions.Add(new
            {
                recommendation = "Common spatial operations",
                operations = new[]
                {
                    new { op = "ST_Intersects", use = "Boolean check if geometries overlap" },
                    new { op = "ST_Within", use = "Check if geometry A is inside geometry B" },
                    new { op = "ST_Buffer", use = "Create distance radius around geometry" },
                    new { op = "ST_Distance", use = "Calculate distance between geometries" },
                    new { op = "ST_Union", use = "Merge geometries into one" },
                    new { op = "ST_Intersection", use = "Extract overlapping portion" },
                    new { op = "ST_Difference", use = "Remove overlapping portion" },
                    new { op = "ST_Area", use = "Calculate polygon area" },
                    new { op = "ST_Length", use = "Calculate line length" }
                }
            });
        }

        return JsonSerializer.Serialize(new
        {
            userIntent,
            suggestions,
            performanceTips = new[]
            {
                "Always use spatial indexes (GIST) for geometry columns",
                "Use geography type for accurate distance calculations on Earth",
                "Filter with ST_Intersects before expensive operations like ST_Intersection",
                "Use <-> operator for efficient nearest neighbor queries",
                "Consider ST_Subdivide for very large or complex geometries",
                "Use ST_Simplify to reduce geometry complexity before processing"
            },
            resources = new[]
            {
                "PostGIS Reference: https://postgis.net/docs/reference.html",
                "PostGIS Workshop: https://postgis.net/workshops/postgis-intro/",
                "Spatial SQL cookbook: https://postgis.net/docs/using_postgis_dbmanagement.html"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Recommends optimal CRS/projection based on data extent and use case")]
    public string RecommendCRS(
        [Description("Data extent as JSON (bbox: [minx, miny, maxx, maxy], center location)")] string dataExtent = "{\"bbox\":[-180,-90,180,90],\"center\":[0,0]}",
        [Description("Use case: web-mapping, analysis, display, or storage")] string useCase = "web-mapping")
    {
        var useCaseLower = useCase.ToLowerInvariant();
        var recommendations = new System.Collections.Generic.List<object>();

        // Web mapping recommendations
        if (useCaseLower.Contains("web") || useCaseLower.Contains("display"))
        {
            recommendations.Add(new
            {
                crs = "EPSG:3857 - Web Mercator",
                suitability = "Excellent for web maps",
                pros = new[]
                {
                    "Standard for web mapping (Google Maps, OpenStreetMap, Mapbox)",
                    "Fast tile rendering and caching",
                    "Wide client library support",
                    "Seamless integration with web tile services"
                },
                cons = new[]
                {
                    "Significant distortion at high latitudes (>70°)",
                    "Not suitable for area calculations",
                    "Poles cannot be represented"
                },
                usage = "SELECT ST_Transform(geom, 3857) FROM layer;",
                notes = "Best for display, NOT for accurate measurement"
            });

            recommendations.Add(new
            {
                crs = "EPSG:4326 - WGS84 Geographic",
                suitability = "Good for web APIs and data exchange",
                pros = new[]
                {
                    "Standard for GeoJSON and OGC APIs",
                    "No projection distortion",
                    "Universal compatibility",
                    "Human-readable coordinates (lat/lon)"
                },
                cons = new[]
                {
                    "Distance calculations require geography type or transformation",
                    "Not equal-area or conformal",
                    "Degrees not intuitive for measurements"
                },
                usage = "SELECT ST_Transform(geom, 4326) FROM layer; -- Or use ::geography for measurements",
                notes = "Best for data interchange and OGC APIs"
            });
        }

        // Analysis recommendations
        if (useCaseLower.Contains("analysis") || useCaseLower.Contains("measurement"))
        {
            recommendations.Add(new
            {
                crs = "UTM Zone (appropriate for region)",
                suitability = "Excellent for accurate local analysis",
                pros = new[]
                {
                    "Accurate distance and area measurements",
                    "Minimal distortion within zone (6° width)",
                    "Meters as unit, intuitive for analysis",
                    "Preserves shape locally (conformal)"
                },
                cons = new[]
                {
                    "Only accurate within specific zone",
                    "Distortion increases outside zone",
                    "Need to determine correct zone for data"
                },
                zoneSelection = @"
-- Find UTM zone for data center
SELECT
    CASE
        WHEN lon >= -180 AND lon < -174 THEN 'EPSG:' || (32601 + CASE WHEN lat >= 0 THEN 0 ELSE 100 END)
        -- ... (full UTM zone calculation)
        ELSE 'Calculate based on longitude'
    END AS utm_epsg
FROM (SELECT ST_X(ST_Centroid(ST_Extent(geom))) AS lon, ST_Y(ST_Centroid(ST_Extent(geom))) AS lat FROM layer) center;",
                examples = new[]
                {
                    "EPSG:32610 - UTM Zone 10N (Western USA, -126° to -120°)",
                    "EPSG:32633 - UTM Zone 33N (Central Europe, 12° to 18°)",
                    "EPSG:32756 - UTM Zone 56S (Eastern Australia)"
                }
            });

            recommendations.Add(new
            {
                crs = "Local Projected CRS",
                suitability = "Best for regional/national analysis",
                examples = new[]
                {
                    new { region = "USA (Contiguous)", epsg = "EPSG:5070 - NAD83 Albers Equal Area", use = "National-scale area analysis" },
                    new { region = "USA (State Plane)", epsg = "EPSG:2227 - California Zone 3", use = "High-accuracy local surveys" },
                    new { region = "Europe", epsg = "EPSG:3035 - ETRS89 LAEA", use = "Pan-European statistical mapping" },
                    new { region = "UK", epsg = "EPSG:27700 - British National Grid", use = "UK-specific mapping" },
                    new { region = "Australia", epsg = "EPSG:3577 - GDA94 Australian Albers", use = "National mapping and analysis" }
                },
                notes = "Choose based on data's geographic region and analysis requirements"
            });
        }

        // Storage recommendations
        if (useCaseLower.Contains("storage") || useCaseLower.Contains("database"))
        {
            recommendations.Add(new
            {
                strategy = "Store in Native/Source CRS",
                rationale = "Preserve original data accuracy",
                approach = new
                {
                    storageCRS = "Use source data's original CRS (check with gdalinfo or ST_SRID)",
                    apiCRS = "Transform to EPSG:4326 or EPSG:3857 on query",
                    benefits = new[]
                    {
                        "No transformation errors in stored data",
                        "Original precision preserved",
                        "Can serve in multiple CRS via on-the-fly transformation"
                    }
                },
                example = @"
-- Store in native CRS, serve in multiple CRS
CREATE TABLE layer (
    id SERIAL PRIMARY KEY,
    geom GEOMETRY(POINT, 32610),  -- Store in UTM Zone 10N
    name VARCHAR
);

-- Query in WGS84 for OGC API
SELECT id, name, ST_Transform(geom, 4326) AS geom FROM layer;

-- Query in Web Mercator for web maps
SELECT id, name, ST_Transform(geom, 3857) AS geom FROM layer;"
            });
        }

        // Add geographic vs projected guidance
        recommendations.Add(new
        {
            concept = "Geographic vs Projected CRS",
            geographic = new
            {
                definition = "Coordinates in degrees (latitude/longitude)",
                examples = new[] { "EPSG:4326 (WGS84)", "EPSG:4269 (NAD83)" },
                useWhen = "Data exchange, global datasets, web APIs",
                measurements = "Use ::geography or ST_Transform for accurate measurements"
            },
            projected = new
            {
                definition = "Coordinates in linear units (meters, feet)",
                examples = new[] { "EPSG:3857 (Web Mercator)", "EPSG:32610 (UTM 10N)" },
                useWhen = "Analysis, measurement, local mapping",
                measurements = "Direct distance/area calculations accurate within projection limits"
            }
        });

        return JsonSerializer.Serialize(new
        {
            dataExtent,
            useCase,
            recommendations,
            selectionGuidelines = new[]
            {
                "For global web maps: EPSG:3857 (Web Mercator)",
                "For data interchange and APIs: EPSG:4326 (WGS84)",
                "For accurate local measurements: UTM or local projected CRS",
                "For area calculations: Equal-area projections (Albers, Lambert)",
                "For navigation/direction: Conformal projections (Mercator, Lambert Conformal Conic)"
            },
            tools = new object[]
            {
                new { tool = "EPSG.io", url = "https://epsg.io", use = "Search and explore CRS definitions" },
                new { tool = "Projection Wizard", url = "https://projectionwizard.org", use = "Interactive CRS selection" },
                new { tool = "PostGIS ST_Transform", use = "Transform between CRS" },
                new { tool = "GDAL ogr2ogr", use = "Reproject data files" }
            },
            transformationExample = @"
-- Transform from WGS84 to Web Mercator
UPDATE layer SET geom = ST_Transform(ST_SetSRID(geom, 4326), 3857);

-- Transform from UTM to WGS84
SELECT ST_Transform(geom, 4326) FROM layer WHERE ST_SRID(geom) = 32610;

-- Use geography for accurate distance on WGS84
SELECT ST_Distance(geom::geography, ST_Point(-122, 37)::geography) FROM layer;"
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Analyzes spatial distribution and provides clustering/density insights")]
    public string AnalyzeSpatialDistribution(
        [Description("Layer statistics as JSON (feature count, extent, geometry type)")] string layerStats)
    {
        var analyses = new object[]
        {
            new
            {
                analysis = "Point Clustering Detection",
                description = "Identify clusters and hotspots in point data",
                methods = new[]
                {
                    new
                    {
                        method = "DBSCAN Clustering",
                        query = @"
-- Simple clustering using ST_ClusterDBSCAN
SELECT
    cluster_id,
    COUNT(*) AS cluster_size,
    ST_Centroid(ST_Collect(geom)) AS cluster_center
FROM (
    SELECT
        ST_ClusterDBSCAN(geom, eps := 0.01, minpoints := 5) OVER () AS cluster_id,
        geom
    FROM points
) clusters
GROUP BY cluster_id;",
                        parameters = "eps = distance threshold, minpoints = minimum cluster size"
                    },
                    new
                    {
                        method = "K-Means Clustering",
                        query = @"
-- K-Means clustering (group points into K clusters)
SELECT
    cluster_id,
    COUNT(*) AS size,
    ST_Centroid(ST_Collect(geom)) AS center
FROM (
    SELECT
        ST_ClusterKMeans(geom, 5) OVER () AS cluster_id,
        geom
    FROM points
) clusters
GROUP BY cluster_id;",
                        parameters = "5 = number of clusters (adjust based on data)"
                    }
                }
            },
            new
            {
                analysis = "Density Heatmap",
                description = "Calculate density of features across space",
                approaches = new object[]
                {
                    new
                    {
                        approach = "Grid-based Density",
                        query = @"
-- Create hexagonal grid and count points per cell
WITH hex_grid AS (
    SELECT ST_HexagonGrid(0.01, ST_Envelope(ST_Extent(geom))) AS geom
    FROM points
)
SELECT
    h.geom,
    COUNT(p.id) AS point_count,
    COUNT(p.id)::float / ST_Area(h.geom::geography) AS density
FROM hex_grid h
LEFT JOIN points p ON ST_Within(p.geom, h.geom)
GROUP BY h.geom;",
                        visualization = "Use graduated colors based on density values"
                    },
                    new
                    {
                        approach = "Kernel Density",
                        implementation = "Use QGIS Heatmap plugin or PostGIS raster functions",
                        output = "Raster surface showing point density with smoothing"
                    }
                }
            },
            new
            {
                analysis = "Spatial Autocorrelation",
                description = "Measure if features are clustered, dispersed, or random",
                metrics = new[]
                {
                    new
                    {
                        metric = "Nearest Neighbor Index",
                        interpretation = new
                        {
                            ratio_less_than_1 = "Clustered pattern",
                            ratio_equals_1 = "Random pattern",
                            ratio_greater_than_1 = "Dispersed pattern"
                        },
                        calculation = @"
-- Calculate average nearest neighbor distance
WITH nn_distances AS (
    SELECT
        a.id,
        MIN(ST_Distance(a.geom, b.geom)) AS nn_distance
    FROM points a
    JOIN points b ON a.id != b.id
    GROUP BY a.id
)
SELECT
    AVG(nn_distance) AS observed_mean_distance,
    0.5 / SQRT((SELECT COUNT(*)::float FROM points) / ST_Area(ST_Envelope(ST_Extent(geom))::geography)) AS expected_mean_distance
FROM nn_distances;",
                        notes = "Ratio = observed_mean_distance / expected_mean_distance"
                    }
                }
            },
            new
            {
                analysis = "Spatial Statistics",
                description = "Summary statistics for spatial distribution",
                metrics = new[]
                {
                    new
                    {
                        metric = "Center of Mass (Centroid)",
                        query = "SELECT ST_Centroid(ST_Collect(geom)) AS center FROM layer;",
                        use = "Geographic center of all features"
                    },
                    new
                    {
                        metric = "Standard Distance",
                        query = @"
SELECT SQRT(AVG(ST_Distance(geom, center)^2)) AS std_distance
FROM layer,
    (SELECT ST_Centroid(ST_Collect(geom)) AS center FROM layer) c;",
                        use = "Measure of spatial dispersion"
                    },
                    new
                    {
                        metric = "Bounding Box / Extent",
                        query = "SELECT ST_Extent(geom) AS bbox FROM layer;",
                        use = "Minimum bounding rectangle"
                    },
                    new
                    {
                        metric = "Convex Hull",
                        query = "SELECT ST_ConvexHull(ST_Collect(geom)) AS hull FROM layer;",
                        use = "Minimum convex polygon containing all features"
                    }
                }
            },
            new
            {
                analysis = "Directional Distribution",
                description = "Identify directional trends and orientation",
                methods = new object[]
                {
                    new
                    {
                        method = "Standard Deviational Ellipse",
                        description = "Ellipse representing directional distribution",
                        tools = "Use ArcGIS Directional Distribution or implement with PostGIS geometry operations"
                    },
                    new
                    {
                        method = "Linear Directional Mean",
                        description = "Mean direction and length for linear features",
                        application = "Road networks, migration paths, flow analysis"
                    }
                }
            }
        };

        return JsonSerializer.Serialize(new
        {
            analyses,
            visualizationTips = new[]
            {
                "Use graduated symbols for cluster sizes",
                "Apply heat color ramp (blue to red) for density",
                "Show standard distance as circle around centroid",
                "Overlay convex hull to show spatial extent",
                "Use transparency for overlapping density surfaces"
            },
            tools = new object[]
            {
                new { tool = "PostGIS Clustering", functions = "ST_ClusterDBSCAN, ST_ClusterKMeans, ST_ClusterWithin" },
                new { tool = "QGIS Heatmap", usage = "Raster > Heatmap (Kernel Density)" },
                new { tool = "Python: scikit-learn", usage = "DBSCAN, KMeans for advanced clustering" },
                new { tool = "R: spatstat", usage = "Comprehensive spatial statistics" }
            },
            interpretationGuide = new
            {
                clustered = "Features concentrated in specific areas - investigate attractors or causes",
                dispersed = "Features evenly distributed - may indicate competition or coverage optimization",
                random = "No spatial pattern - features independent of location",
                hotspots = "High-density areas requiring special attention or resources"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Generates map styling recommendations based on geometry and attributes")]
    public string GenerateStyleForData(
        [Description("Geometry type (Point, LineString, Polygon)")] string geometryType,
        [Description("Attribute information as JSON (field names, types, value ranges)")] string attributes)
    {
        var geometryLower = geometryType.ToLowerInvariant();
        var stylingRecommendations = new System.Collections.Generic.List<object>();

        // Point styling
        if (geometryLower.Contains("point"))
        {
            stylingRecommendations.Add(new
            {
                geometryType = "Point",
                baseStyle = new
                {
                    symbolType = "Circle or Icon",
                    size = "4-12 pixels (scale-dependent)",
                    color = "#3388ff (blue)",
                    stroke = "1px white outline for visibility",
                    opacity = 0.8
                },
                categoricalStyling = new
                {
                    method = "Unique values by attribute",
                    example = new
                    {
                        attribute = "category",
                        rules = new[]
                        {
                            new { value = "hospital", symbol = "hospital icon", color = "#e74c3c" },
                            new { value = "school", symbol = "school icon", color = "#3498db" },
                            new { value = "park", symbol = "tree icon", color = "#27ae60" }
                        }
                    },
                    mapboxStyle = @"{
  'type': 'circle',
  'paint': {
    'circle-radius': 8,
    'circle-color': [
      'match',
      ['get', 'category'],
      'hospital', '#e74c3c',
      'school', '#3498db',
      'park', '#27ae60',
      '#3388ff'
    ]
  }
}"
                },
                graduatedStyling = new
                {
                    method = "Size or color by numeric attribute",
                    example = new
                    {
                        attribute = "population",
                        visualization = "Proportional symbols",
                        formula = "radius = sqrt(population) * scaleFactor"
                    },
                    mapboxStyle = @"{
  'type': 'circle',
  'paint': {
    'circle-radius': [
      'interpolate', ['linear'], ['get', 'population'],
      0, 4,
      100000, 12,
      1000000, 24
    ],
    'circle-color': [
      'interpolate', ['linear'], ['get', 'population'],
      0, '#ffffcc',
      500000, '#fd8d3c',
      1000000, '#e31a1c'
    ]
  }
}"
                }
            });
        }

        // LineString styling
        if (geometryLower.Contains("line") || geometryLower.Contains("string"))
        {
            stylingRecommendations.Add(new
            {
                geometryType = "LineString",
                baseStyle = new
                {
                    strokeWidth = "2-5 pixels (scale-dependent)",
                    strokeColor = "#3388ff",
                    strokeStyle = "solid",
                    opacity = 0.8
                },
                roadNetworkExample = new
                {
                    classification = "Style by road type",
                    rules = new[]
                    {
                        new { type = "highway", width = 6, color = "#e74c3c", pattern = "solid" },
                        new { type = "arterial", width = 4, color = "#f39c12", pattern = "solid" },
                        new { type = "local", width = 2, color = "#95a5a6", pattern = "solid" },
                        new { type = "trail", width = 2, color = "#27ae60", pattern = "dashed" }
                    },
                    cartoCSS = @"#roads {
  line-width: 2;
  line-color: #ccc;

  [type='highway'] {
    line-width: 6;
    line-color: #e74c3c;
    ::outline {
      line-width: 8;
      line-color: #000;
      line-opacity: 0.3;
    }
  }
  [type='arterial'] { line-width: 4; line-color: #f39c12; }
  [type='local'] { line-width: 2; line-color: #95a5a6; }
}"
                },
                flowVisualization = new
                {
                    attribute = "traffic_volume or direction",
                    technique = "Graduated width or animated flow",
                    mapboxStyle = @"{
  'type': 'line',
  'paint': {
    'line-width': [
      'interpolate', ['linear'], ['get', 'traffic'],
      0, 1,
      1000, 3,
      5000, 8
    ],
    'line-color': [
      'interpolate', ['linear'], ['get', 'traffic'],
      0, '#1a9850',
      2500, '#fee08b',
      5000, '#d73027'
    ]
  }
}"
                }
            });
        }

        // Polygon styling
        if (geometryLower.Contains("polygon"))
        {
            stylingRecommendations.Add(new
            {
                geometryType = "Polygon",
                baseStyle = new
                {
                    fillColor = "#3388ff",
                    fillOpacity = 0.4,
                    strokeColor = "#0066cc",
                    strokeWidth = 1,
                    strokeOpacity = 0.8
                },
                choroplethExample = new
                {
                    method = "Color by numeric attribute (density, rate, etc.)",
                    colorScheme = "Use ColorBrewer for scientifically-designed palettes",
                    classification = new[]
                    {
                        "Equal Interval - same range width",
                        "Quantile - same number of features per class",
                        "Natural Breaks (Jenks) - minimize within-class variance",
                        "Standard Deviation - based on mean and std dev"
                    },
                    mapboxStyle = @"{
  'type': 'fill',
  'paint': {
    'fill-color': [
      'step',
      ['get', 'density'],
      '#ffffb2', 10,
      '#fed976', 25,
      '#feb24c', 50,
      '#fd8d3c', 100,
      '#fc4e2a', 250,
      '#e31a1c', 500,
      '#b10026'
    ],
    'fill-opacity': 0.7
  }
}"
                },
                patternFill = new
                {
                    use = "Categorical differentiation with patterns",
                    patterns = new[] { "diagonal lines", "dots", "crosshatch", "solid" },
                    accessibility = "Patterns help colorblind users differentiate categories"
                }
            });
        }

        // Common styling best practices
        var bestPractices = new
        {
            colorSelection = new[]
            {
                "Use ColorBrewer (colorbrewer2.org) for scientific color schemes",
                "Sequential: single-hue for ordered data (light to dark)",
                "Diverging: two-hue for data with meaningful midpoint (blue-red)",
                "Qualitative: distinct colors for categories (max 8-12 categories)",
                "Consider colorblind-safe palettes (especially avoid red-green)"
            },
            scaleDependent = new
            {
                principle = "Adjust styling based on zoom level",
                implementation = @"
// Mapbox GL JS zoom-based styling
'circle-radius': [
  'interpolate', ['exponential', 2], ['zoom'],
  5, 2,    // At zoom 5, radius 2px
  10, 6,   // At zoom 10, radius 6px
  15, 12   // At zoom 15, radius 12px
]",
                clustering = "Use point clustering at low zoom, individual points at high zoom"
            },
            dataClassification = new
            {
                naturalBreaks = "Best for non-uniform distributions, highlights natural groupings",
                quantile = "Good for uniform comparison, each class has equal features",
                equalInterval = "Simple interpretation, may have empty classes",
                standardDeviation = "Highlights outliers and deviation from mean"
            },
            accessibility = new[]
            {
                "Ensure sufficient color contrast (WCAG AA: 4.5:1 minimum)",
                "Provide alternative pattern fills for colorblind users",
                "Use text labels for critical information, not just color",
                "Test with colorblind simulation tools",
                "Provide legend with clear category descriptions"
            }
        };

        return JsonSerializer.Serialize(new
        {
            geometryType,
            stylingRecommendations,
            bestPractices,
            tools = new object[]
            {
                new { tool = "ColorBrewer", url = "https://colorbrewer2.org", use = "Scientific color scheme selection" },
                new { tool = "Mapbox Studio", url = "https://studio.mapbox.com", use = "Visual style editor" },
                new { tool = "QGIS Symbology", use = "Desktop GIS styling with export to web formats" },
                new { tool = "CartoCSS", use = "Declarative map styling language" },
                new { tool = "Maputnik", url = "https://maputnik.github.io", use = "Open source visual style editor for Mapbox GL" }
            },
            exportFormats = new
            {
                mapboxGL = "JSON style specification for Mapbox GL JS and compatible libraries",
                sld = "Styled Layer Descriptor (OGC standard for WMS)",
                qml = "QGIS style format",
                cartoCSS = "CartoCSS for Carto and Mapbox Studio Classic"
            }
        }, CliJsonOptions.Indented);
    }
}
