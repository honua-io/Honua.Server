// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for testing and quality assurance.
/// Provides AI with capabilities to generate test data, suggest test scenarios, and analyze test results.
/// </summary>
public sealed class TestingPlugin
{
    [KernelFunction, Description("Generates synthetic spatial test data based on schema")]
    public string GenerateTestData(
        [Description("Schema definition as JSON (table name, columns, geometry type)")] string schema = "{\"tableName\":\"test_features\",\"geometryType\":\"Point\"}",
        [Description("Number of records to generate")] int recordCount = 100)
    {
        var testDataStrategies = new
        {
            postgisGeneration = new
            {
                description = "Generate test data directly in PostGIS",
                pointData = $@"
-- Generate {recordCount} random points within bounding box
INSERT INTO test_points (id, name, category, geom)
SELECT
    generate_series(1, {recordCount}) AS id,
    'Point_' || generate_series(1, {recordCount}) AS name,
    (ARRAY['residential', 'commercial', 'industrial', 'park'])[1 + floor(random() * 4)::int] AS category,
    ST_SetSRID(
        ST_MakePoint(
            -122.5 + (random() * 0.3),  -- Longitude range
            37.7 + (random() * 0.3)      -- Latitude range
        ),
        4326
    ) AS geom;",

                lineData = $@"
-- Generate {recordCount} random line segments
INSERT INTO test_lines (id, name, length_m, geom)
SELECT
    id,
    'Line_' || id AS name,
    ST_Length(geom::geography) AS length_m,
    geom
FROM (
    SELECT
        generate_series(1, {recordCount}) AS id,
        ST_SetSRID(
            ST_MakeLine(
                ST_MakePoint(-122.5 + random() * 0.3, 37.7 + random() * 0.3),
                ST_MakePoint(-122.5 + random() * 0.3, 37.7 + random() * 0.3)
            ),
            4326
        ) AS geom
) lines;",

                polygonData = $@"
-- Generate {recordCount} random polygons (rectangles)
INSERT INTO test_polygons (id, name, area_sqm, geom)
SELECT
    id,
    'Polygon_' || id AS name,
    ST_Area(geom::geography) AS area_sqm,
    geom
FROM (
    SELECT
        generate_series(1, {recordCount}) AS id,
        ST_SetSRID(
            ST_MakeEnvelope(
                -122.5 + random() * 0.3,
                37.7 + random() * 0.3,
                -122.5 + random() * 0.3 + random() * 0.02,
                37.7 + random() * 0.3 + random() * 0.02
            ),
            4326
        ) AS geom
) polys;",

                complexGeometries = @"
-- Generate spatially clustered points (more realistic)
WITH clusters AS (
    SELECT
        generate_series(1, 5) AS cluster_id,
        ST_MakePoint(-122.5 + random() * 0.3, 37.7 + random() * 0.3) AS center
)
INSERT INTO test_clustered_points (id, cluster_id, geom)
SELECT
    row_number() OVER () AS id,
    cluster_id,
    ST_SetSRID(
        ST_Translate(
            center,
            (random() - 0.5) * 0.01,  -- Small offset from cluster center
            (random() - 0.5) * 0.01
        ),
        4326
    ) AS geom
FROM clusters, generate_series(1, 20); -- 20 points per cluster"
            },

            pythonGeneration = new
            {
                description = "Generate test data with Python and Shapely",
                script = @"
import random
from shapely.geometry import Point, LineString, Polygon
from shapely import wkt
import json

def generate_test_points(count=100):
    features = []
    for i in range(count):
        point = Point(
            -122.5 + random.random() * 0.3,
            37.7 + random.random() * 0.3
        )
        features.append({
            'type': 'Feature',
            'id': i + 1,
            'properties': {
                'name': f'Point_{i+1}',
                'category': random.choice(['residential', 'commercial', 'industrial', 'park'])
            },
            'geometry': json.loads(json.dumps(point.__geo_interface__))
        })

    geojson = {
        'type': 'FeatureCollection',
        'features': features
    }

    with open('test_points.geojson', 'w') as f:
        json.dump(geojson, f, indent=2)

def generate_test_polygons(count=50):
    features = []
    for i in range(count):
        x = -122.5 + random.random() * 0.3
        y = 37.7 + random.random() * 0.3
        width = random.random() * 0.02
        height = random.random() * 0.02

        polygon = Polygon([
            (x, y),
            (x + width, y),
            (x + width, y + height),
            (x, y + height),
            (x, y)
        ])

        features.append({
            'type': 'Feature',
            'id': i + 1,
            'properties': {
                'name': f'Polygon_{i+1}',
                'area_sqm': polygon.area * 111320 ** 2  # Rough conversion to sqm
            },
            'geometry': json.loads(json.dumps(polygon.__geo_interface__))
        })

    geojson = {
        'type': 'FeatureCollection',
        'features': features
    }

    with open('test_polygons.geojson', 'w') as f:
        json.dump(geojson, f, indent=2)

# Generate test data
generate_test_points(100)
generate_test_polygons(50)"
            },

            gdalGeneration = new
            {
                description = "Generate test data using GDAL/OGR",
                vrtTemplate = @"
<!-- test_points.vrt - Virtual format for test data generation -->
<OGRVRTDataSource>
    <OGRVRTLayer name=""test_points"">
        <SrcDataSource>CSV:test_points.csv</SrcDataSource>
        <GeometryType>wkbPoint</GeometryType>
        <LayerSRS>EPSG:4326</LayerSRS>
        <GeometryField encoding=""PointFromColumns"" x=""longitude"" y=""latitude""/>
    </OGRVRTLayer>
</OGRVRTDataSource>",

                csvGeneration = @"
# Generate CSV with bash
echo ""id,name,category,latitude,longitude"" > test_points.csv
for i in {1..100}; do
    lat=$(echo ""37.7 + $RANDOM * 0.3 / 32767"" | bc -l)
    lon=$(echo ""-122.5 + $RANDOM * 0.3 / 32767"" | bc -l)
    cat=$(shuf -n 1 -e residential commercial industrial park)
    echo ""$i,Point_$i,$cat,$lat,$lon"" >> test_points.csv
done

# Convert to spatial format
ogr2ogr -f GPKG test_points.gpkg test_points.vrt",

                command = "ogr2ogr -f PostgreSQL PG:\"dbname=honua\" test_points.gpkg -nln test_points"
            },

            dataPatterns = new[]
            {
                new
                {
                    pattern = "Uniform Distribution",
                    use = "General testing, no spatial bias",
                    implementation = "random() across extent"
                },
                new
                {
                    pattern = "Clustered Distribution",
                    use = "Realistic urban data, hotspot testing",
                    implementation = "Generate cluster centers, then points around each center"
                },
                new
                {
                    pattern = "Grid Pattern",
                    use = "Regular sampling, coverage testing",
                    implementation = "ST_SquareGrid or calculated grid points"
                },
                new
                {
                    pattern = "Road Network",
                    use = "Linear feature testing",
                    implementation = "Generate connected line segments forming network"
                },
                new
                {
                    pattern = "Hierarchical",
                    use = "Parent-child relationships (countries > states > cities)",
                    implementation = "Generate larger polygons, subdivide for child features"
                }
            }
        };

        return JsonSerializer.Serialize(new
        {
            recordCount,
            testDataStrategies,
            loadingCommands = new
            {
                fromGeoJSON = "ogr2ogr -f PostgreSQL PG:\"dbname=honua\" test_points.geojson -nln test_points",
                fromSQL = "psql -h localhost -U honua_user -d honua -f generate_test_data.sql",
                validate = "SELECT COUNT(*), ST_GeometryType(geom), ST_SRID(geom) FROM test_points GROUP BY 2, 3;"
            },
            bestPractices = new[]
            {
                "Use realistic data distributions (clustered for cities, not uniform)",
                "Include edge cases: null values, empty geometries, complex polygons",
                "Generate data at multiple scales for zoom level testing",
                "Include attributes that support filtering and styling tests",
                "Validate generated geometries: ST_IsValid(geom) should return true",
                "Document test data generation for reproducibility"
            },
            useCases = new[]
            {
                new { use = "Performance testing", recommendation = "Generate 10K-1M features to test query speed" },
                new { use = "Rendering testing", recommendation = "Generate clustered points at multiple zoom levels" },
                new { use = "API testing", recommendation = "Small dataset (100-1000) with diverse attributes" },
                new { use = "Spatial operations", recommendation = "Overlapping geometries to test intersection" }
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Suggests test scenarios for different service types")]
    public string SuggestTestScenarios(
        [Description("Service type: ogc-features, ogc-tiles, or raster")] string serviceType = "ogc-features")
    {
        var scenarios = (object)(serviceType.ToLowerInvariant() switch
        {
            "ogc-features" or "features" => new
            {
                serviceType = "OGC API Features",
                testScenarios = new object[]
                {
                    new
                    {
                        scenario = "Basic Retrieval",
                        tests = new[]
                        {
                            new { test = "Landing page", endpoint = (string?)"/", expectedStatus = 200, validates = "Service availability, links structure" },
                            new { test = "Conformance classes", endpoint = (string?)"/conformance", expectedStatus = 200, validates = "OGC API Features compliance" },
                            new { test = "Collections list", endpoint = (string?)"/collections", expectedStatus = 200, validates = "Metadata loading, collection configuration" },
                            new { test = "Single collection", endpoint = (string?)"/collections/{collectionId}", expectedStatus = 200, validates = "Collection metadata completeness" },
                            new { test = "Items", endpoint = (string?)"/collections/{collectionId}/items", expectedStatus = 200, validates = "Data access, GeoJSON output" }
                        }
                    },
                    new
                    {
                        scenario = "Pagination",
                        tests = new[]
                        {
                            new { test = "Default limit", endpoint = (string?)"/collections/{id}/items", check = "Should return default limit (e.g., 10 items)" },
                            new { test = "Custom limit", endpoint = (string?)"/collections/{id}/items?limit=100", check = "Should return requested number of items" },
                            new { test = "Offset navigation", endpoint = (string?)"/collections/{id}/items?offset=50", check = "Should skip first 50 items" },
                            new { test = "Max limit enforcement", endpoint = (string?)"/collections/{id}/items?limit=10000", check = "Should cap at server max (e.g., 1000)" },
                            new { test = "Rel links", endpoint = (string?)"/collections/{id}/items", check = "Should include next/prev links in response" }
                        }
                    },
                    new
                    {
                        scenario = "Spatial Filtering",
                        tests = new object[]
                        {
                            new { test = "Bounding box", endpoint = (string?)"/collections/{id}/items?bbox=-122.5,37.7,-122.3,37.9", validates = "Spatial index usage, correct filtering", expectedStatus = (int?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Bbox with CRS", endpoint = (string?)"/collections/{id}/items?bbox-crs=http://www.opengis.net/def/crs/EPSG/0/3857", validates = "CRS parameter handling", expectedStatus = (int?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Invalid bbox", endpoint = (string?)"/collections/{id}/items?bbox=invalid", expectedStatus = 400, validates = "Parameter validation", check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "World extent", endpoint = (string?)"/collections/{id}/items?bbox=-180,-90,180,90", validates = "Global query performance", expectedStatus = (int?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null }
                        }
                    },
                    new
                    {
                        scenario = "Attribute Filtering (CQL)",
                        tests = new object[]
                        {
                            new { test = "Simple equality", endpoint = (string?)"/collections/{id}/items?filter=category='residential'", validates = "CQL parsing, filter execution", expectedStatus = (int?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Numeric comparison", endpoint = (string?)"/collections/{id}/items?filter=population>100000", validates = "Numeric operators", expectedStatus = (int?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Combined filters", endpoint = (string?)"/collections/{id}/items?filter=population>100000 AND category='city'", validates = "Logical operators", expectedStatus = (int?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Spatial + attribute", endpoint = (string?)"/collections/{id}/items?bbox=...&filter=population>50000", validates = "Combined filtering", expectedStatus = (int?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null }
                        }
                    },
                    new
                    {
                        scenario = "CRS/Projection",
                        tests = new object[]
                        {
                            new { test = "Default CRS (CRS84)", endpoint = (string?)"/collections/{id}/items", check = "Coordinates in lon/lat order", validates = (string?)null, expectedStatus = (int?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Request specific CRS", endpoint = (string?)"/collections/{id}/items?crs=http://www.opengis.net/def/crs/EPSG/0/3857", validates = "CRS transformation", expectedStatus = (int?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Unsupported CRS", endpoint = (string?)"/collections/{id}/items?crs=unsupported", expectedStatus = 400, validates = "CRS validation", check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Content-Crs header", endpoint = (string?)"/collections/{id}/items", check = "Response includes Content-Crs header", validates = (string?)null, expectedStatus = (int?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null }
                        }
                    },
                    new
                    {
                        scenario = "Error Handling",
                        tests = new object[]
                        {
                            new { test = "Collection not found", endpoint = (string?)"/collections/nonexistent/items", expectedStatus = 404, validates = "Error response format", check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Invalid parameter", endpoint = (string?)"/collections/{id}/items?limit=abc", expectedStatus = 400, validates = "Parameter validation", check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Malformed bbox", endpoint = (string?)"/collections/{id}/items?bbox=1,2,3", expectedStatus = 400, validates = "Bbox validation (needs 4 values)", check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Invalid filter", endpoint = (string?)"/collections/{id}/items?filter=invalid syntax", expectedStatus = 400, validates = "CQL syntax validation", check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null }
                        }
                    },
                    new
                    {
                        scenario = "Performance",
                        tests = new object[]
                        {
                            new { test = "Query time", endpoint = (string?)"/collections/{id}/items?limit=1000", metric = "Response time < 1 second", validates = (string?)null, expectedStatus = (int?)null, check = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Spatial query", endpoint = (string?)"/collections/{id}/items?bbox=...", metric = "Spatial index usage, response < 500ms", validates = (string?)null, expectedStatus = (int?)null, check = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Large result set", endpoint = (string?)"/collections/{id}/items?limit=1000", metric = "Memory usage stable", validates = (string?)null, expectedStatus = (int?)null, check = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Concurrent requests", load = "100 req/sec", metric = "No errors, avg response < 2sec", endpoint = (string?)null, validates = (string?)null, expectedStatus = (int?)null, check = (string?)null, headers = (string?)null, contentType = (string?)null }
                        }
                    }
                }
            },
            "ogc-tiles" or "tiles" => new
            {
                serviceType = "OGC API Tiles",
                testScenarios = new[]
                {
                    new
                    {
                        scenario = "Tile Retrieval",
                        tests = new object[]
                        {
                            new { test = "Tile matrix sets", endpoint = (string?)"/tileMatrixSets", expectedStatus = 200, validates = (string?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "WebMercatorQuad", endpoint = (string?)"/tileMatrixSets/WebMercatorQuad", expectedStatus = 200, validates = (string?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Vector tile", endpoint = (string?)"/collections/{id}/tiles/WebMercatorQuad/{z}/{x}/{y}.mvt", contentType = "application/vnd.mapbox-vector-tile", expectedStatus = (int?)null, validates = (string?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null },
                            new { test = "PNG tile", endpoint = (string?)"/collections/{id}/tiles/WebMercatorQuad/{z}/{x}/{y}.png", contentType = "image/png", expectedStatus = (int?)null, validates = (string?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null },
                            new { test = "Tile outside extent", endpoint = (string?)"/collections/{id}/tiles/WebMercatorQuad/10/10000/10000.mvt", expectedStatus = 404, validates = (string?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null }
                        }
                    },
                    new
                    {
                        scenario = "Tile Caching",
                        tests = new object[]
                        {
                            new { test = "Cache headers", endpoint = (string?)"/collections/{id}/tiles/{z}/{x}/{y}.mvt", check = "Cache-Control header present", validates = (string?)null, expectedStatus = (int?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "ETag support", endpoint = (string?)"/collections/{id}/tiles/{z}/{x}/{y}.mvt", check = "ETag header for cache validation", validates = (string?)null, expectedStatus = (int?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Conditional request", headers = "If-None-Match: <etag>", expectedStatus = 304, endpoint = (string?)null, validates = (string?)null, check = (string?)null, metric = (string?)null, load = (string?)null, contentType = (string?)null },
                            new { test = "Cache hit", endpoint = "Same tile twice", metric = "Second request from cache (faster)", validates = (string?)null, expectedStatus = (int?)null, check = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null }
                        }
                    },
                    new
                    {
                        scenario = "Tile Content",
                        tests = new object[]
                        {
                            new { test = "MVT structure", endpoint = (string?)"/collections/{id}/tiles/{z}/{x}/{y}.mvt", validates = "Valid Mapbox Vector Tile format", expectedStatus = (int?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Layer names", check = "MVT layer name matches collection ID", endpoint = (string?)null, validates = (string?)null, expectedStatus = (int?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Feature properties", check = "Tile includes expected attributes", endpoint = (string?)null, validates = (string?)null, expectedStatus = (int?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Geometry clipping", check = "Features clipped to tile boundary", endpoint = (string?)null, validates = (string?)null, expectedStatus = (int?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Empty tiles", endpoint = "Tile with no features", expectedStatus = 204, check = "Or empty MVT", validates = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null }
                        }
                    }
                }
            },
            "raster" => new
            {
                serviceType = "Raster Services",
                testScenarios = new[]
                {
                    new
                    {
                        scenario = "Coverage Access",
                        tests = new object[]
                        {
                            new { test = "Coverage list", endpoint = (string?)"/collections?type=coverage", expectedStatus = 200, validates = (string?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Coverage metadata", endpoint = (string?)"/collections/{coverageId}", check = "Include bands, resolution, CRS", validates = (string?)null, expectedStatus = (int?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Tile retrieval", endpoint = (string?)"/collections/{coverageId}/coverage/tiles/{z}/{x}/{y}.png", contentType = "image/png", expectedStatus = (int?)null, validates = (string?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null },
                            new { test = "GeoTIFF export", endpoint = (string?)"/collections/{coverageId}/coverage?bbox=...&format=geotiff", contentType = "image/tiff", expectedStatus = (int?)null, validates = (string?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null }
                        }
                    },
                    new
                    {
                        scenario = "Band Selection",
                        tests = new object[]
                        {
                            new { test = "Single band", endpoint = (string?)"/coverage?subset=band(0)", check = "Returns grayscale", validates = (string?)null, expectedStatus = (int?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "RGB composite", endpoint = (string?)"/coverage?subset=band(0,1,2)", check = "Returns RGB image", validates = (string?)null, expectedStatus = (int?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Invalid band", endpoint = (string?)"/coverage?subset=band(999)", expectedStatus = 400, validates = (string?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null }
                        }
                    }
                }
            },
            _ => new
            {
                serviceType = "General API Testing",
                testScenarios = new[]
                {
                    new
                    {
                        scenario = "Health Checks",
                        tests = new object[]
                        {
                            new { test = "Landing page availability", endpoint = (string?)"/", expectedStatus = 200, validates = (string?)null, check = (string?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Database connectivity", check = "Service can query database", endpoint = (string?)null, validates = (string?)null, expectedStatus = (int?)null, metric = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null },
                            new { test = "Response time", metric = "All endpoints respond within SLA", endpoint = (string?)null, validates = (string?)null, expectedStatus = (int?)null, check = (string?)null, load = (string?)null, headers = (string?)null, contentType = (string?)null }
                        }
                    }
                }
            }
        });

        return JsonSerializer.Serialize(new
        {
            scenarios,
            automatedTestingFrameworks = new
            {
                dotnet = new
                {
                    framework = "xUnit + FluentAssertions",
                    example = @"
[Fact]
public async Task Collections_ReturnsSuccess()
{
    var response = await _client.GetAsync(""/collections"");
    response.StatusCode.Should().Be(HttpStatusCode.OK);

    var content = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
    var collections = JsonSerializer.Deserialize<CollectionsResponse>(content);
    collections.Collections.Should().NotBeEmpty();
}

[Theory]
[InlineData(""?limit=10"", 10)]
[InlineData(""?limit=100"", 100)]
public async Task Items_RespectsLimitParameter(string query, int expectedCount)
{
    var response = await _client.GetAsync($""/collections/test/items{query}"");
    var items = JsonSerializer.Deserialize<FeatureCollection>(await response.Content.ReadAsStringAsync().ConfigureAwait(false));
    items.Features.Should().HaveCount(expectedCount);
}"
                },

                python = new
                {
                    framework = "pytest + requests",
                    example = @"
import pytest
import requests

BASE_URL = ""http://localhost:5000""

def test_landing_page():
    response = requests.get(f""{BASE_URL}/"")
    assert response.status_code == 200
    data = response.json()
    assert ""links"" in data

@pytest.mark.parametrize(""bbox,expected_min"", [
    (""-122.5,37.7,-122.3,37.9"", 1),
    (""-180,-90,180,90"", 100),
])
def test_bbox_filtering(bbox, expected_min):
    response = requests.get(f""{BASE_URL}/collections/test/items?bbox={bbox}"")
    assert response.status_code == 200
    data = response.json()
    assert len(data[""features""]) >= expected_min"
                },

                postman = new
                {
                    tool = "Postman Collection",
                    features = new[]
                    {
                        "Create collection of API requests",
                        "Add tests with JavaScript assertions",
                        "Run via Newman for CI/CD",
                        "Generate test reports"
                    },
                    example = @"
pm.test(""Status code is 200"", function () {
    pm.response.to.have.status(200);
});

pm.test(""Response has collections array"", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.collections).to.be.an('array');
    pm.expect(jsonData.collections).to.have.lengthOf.at.least(1);
});"
                }
            },

            loadTesting = new
            {
                nbomber = new
                {
                    framework = ".NET load testing framework",
                    example = @"
var scenario = ScenarioBuilder
    .CreateScenario(""ogc_api_load_test"", async context =>
    {
        var response = await httpClient.GetAsync(""/collections/test/items?limit=100"");

        return response.IsSuccessStatusCode
            ? Response.Ok()
            : Response.Fail();
    })
    .WithLoadSimulations(
        Simulation.InjectPerSec(rate: 100, during: TimeSpan.FromSeconds(30))
    );

NBomberRunner
    .RegisterScenarios(scenario)
    .Run();"
                },

                k6 = new
                {
                    framework = "Grafana k6 load testing",
                    example = @"
import http from 'k6/http';
import { check } from 'k6';

export let options = {
    stages: [
        { duration: '1m', target: 50 },
        { duration: '2m', target: 100 },
        { duration: '1m', target: 0 },
    ],
};

export default function () {
    let response = http.get('http://localhost:5000/collections/test/items?limit=100');
    check(response, {
        'status is 200': (r) => r.status === 200,
        'response time < 500ms': (r) => r.timings.duration < 500,
    });
}"
                }
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Validates OGC API conformance against official test suites")]
    public string ValidateOgcConformance(
        [Description("Server URL to test (e.g., http://localhost:5000)")] string serverUrl = "http://localhost:5000")
    {
        var validation = new
        {
            serverUrl,
            ogcCiteTests = new
            {
                description = "Official OGC Compliance & Interoperability Testing",
                url = "https://cite.opengeospatial.org/teamengine/",
                testSuites = new[]
                {
                    new
                    {
                        suite = "OGC API - Features - Part 1: Core 1.0",
                        citation = "http://www.opengis.net/spec/ogcapi-features-1/1.0",
                        tests = new[] { "Conformance classes", "Collections", "Items", "OpenAPI", "GeoJSON encoding" },
                        notes = "Primary conformance requirement for OGC API Features"
                    },
                    new
                    {
                        suite = "OGC API - Features - Part 2: CRS",
                        citation = "http://www.opengis.net/spec/ogcapi-features-2/1.0",
                        tests = new[] { "CRS negotiation", "CRS transformation", "Storage CRS" },
                        notes = "Required for multi-CRS support"
                    }
                },
                usage = new
                {
                    web = "Visit cite.opengeospatial.org and run tests via web interface",
                    command = "docker run -p 8080:8080 ogccite/ets-ogcapi-features10 # Run locally"
                }
            },

            manualValidation = new
            {
                description = "Manual conformance checks",
                checks = new[]
                {
                    new
                    {
                        check = "Landing Page Structure",
                        endpoint = (string?)"/",
                        validates = new[]
                        {
                            "Must include 'links' array",
                            "Must have rel='service-desc' link to OpenAPI",
                            "Must have rel='conformance' link",
                            "Must have rel='data' or rel='collections' link"
                        },
                        test = (string?)$"curl -s {serverUrl}/ | jq '.links[] | select(.rel==\"conformance\")'"
                    },
                    new
                    {
                        check = "Conformance Classes",
                        endpoint = (string?)"/conformance",
                        validates = new[]
                        {
                            "Must declare: http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core",
                            "Must declare: http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson",
                            "Should declare: http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/oas30"
                        },
                        test = (string?)$"curl -s {serverUrl}/conformance | jq '.conformsTo[]'"
                    },
                    new
                    {
                        check = "Collections Response",
                        endpoint = (string?)"/collections",
                        validates = new[]
                        {
                            "Must include 'collections' array",
                            "Each collection must have 'id' and 'links'",
                            "Each collection should have 'extent' (spatial and optionally temporal)",
                            "Must include rel='self' and rel='items' links"
                        },
                        test = (string?)$"curl -s {serverUrl}/collections | jq '.collections[0] | keys'"
                    },
                    new
                    {
                        check = "Items Response (GeoJSON)",
                        endpoint = (string?)"/collections/{{collectionId}}/items",
                        validates = new[]
                        {
                            "Must be valid GeoJSON FeatureCollection",
                            "Must include 'type', 'features' properties",
                            "Should include 'links' for pagination (self, next, prev)",
                            "Features must have 'type', 'geometry', 'properties'"
                        },
                        test = (string?)$@"curl -s {serverUrl}/collections/COLLECTION_ID/items?limit=1 | jq '{{type, features: (.features | length), links: (.links | length)}}'"
                    },
                    new
                    {
                        check = "Pagination Support",
                        endpoint = (string?)"/collections/{{collectionId}}/items?limit=10",
                        validates = new[]
                        {
                            "Must respect 'limit' parameter",
                            "Must include 'next' link if more results available",
                            "Must support 'offset' or cursor-based pagination"
                        },
                        test = (string?)$"curl -s '{serverUrl}/collections/COLLECTION_ID/items?limit=10' | jq '.features | length'"
                    },
                    new
                    {
                        check = "Bounding Box Filter",
                        endpoint = (string?)"/collections/{{collectionId}}/items?bbox=...",
                        validates = new[]
                        {
                            "Must filter features to bbox",
                            "Must support bbox in CRS84 (lon,lat order)",
                            "Must validate bbox (4 or 6 numbers)"
                        },
                        test = (string?)$"curl -s '{serverUrl}/collections/COLLECTION_ID/items?bbox=-180,-90,180,90' | jq '.features | length'"
                    },
                    new
                    {
                        check = "Error Handling",
                        endpoint = (string?)null,
                        validates = new[]
                        {
                            "404 for non-existent collections",
                            "400 for invalid parameters",
                            "500 for server errors (with proper error response)",
                            "Error responses should include 'code' and 'description'"
                        },
                        test = (string?)$"curl -s -w '%{{http_code}}' {serverUrl}/collections/nonexistent/items"
                    }
                }
            },

            automatedValidation = new
            {
                bashScript = $@"#!/bin/bash
# OGC API Features Conformance Validation Script
# Run against: {serverUrl}

BASE_URL=""{serverUrl}""
ERRORS=0

echo ""Testing OGC API Features Conformance...""

# Test 1: Landing Page
echo -n ""Testing landing page... ""
STATUS=$(curl -s -w '%{{http_code}}' -o /dev/null $BASE_URL/)
if [ $STATUS -eq 200 ]; then
    echo ""✓""
else
    echo ""✗ (Status: $STATUS)""
    ERRORS=$((ERRORS + 1))
fi

# Test 2: Conformance
echo -n ""Testing conformance declaration... ""
CORE_CONF=$(curl -s $BASE_URL/conformance | jq -r '.conformsTo[] | select(contains(""ogcapi-features-1/1.0/conf/core""))')
if [ ! -z ""$CORE_CONF"" ]; then
    echo ""✓""
else
    echo ""✗ (Core conformance class not declared)""
    ERRORS=$((ERRORS + 1))
fi

# Test 3: Collections
echo -n ""Testing collections... ""
COLLECTIONS=$(curl -s $BASE_URL/collections | jq -r '.collections | length')
if [ $COLLECTIONS -gt 0 ]; then
    echo ""✓ ($COLLECTIONS collections)""
else
    echo ""✗ (No collections found)""
    ERRORS=$((ERRORS + 1))
fi

# Test 4: Items
COLLECTION_ID=$(curl -s $BASE_URL/collections | jq -r '.collections[0].id')
echo -n ""Testing items for collection '$COLLECTION_ID'... ""
ITEMS_STATUS=$(curl -s -w '%{{http_code}}' -o /dev/null $BASE_URL/collections/$COLLECTION_ID/items)
if [ $ITEMS_STATUS -eq 200 ]; then
    echo ""✓""
else
    echo ""✗ (Status: $ITEMS_STATUS)""
    ERRORS=$((ERRORS + 1))
fi

# Test 5: GeoJSON Format
echo -n ""Testing GeoJSON format... ""
GEOJSON_TYPE=$(curl -s $BASE_URL/collections/$COLLECTION_ID/items?limit=1 | jq -r '.type')
if [ ""$GEOJSON_TYPE"" = ""FeatureCollection"" ]; then
    echo ""✓""
else
    echo ""✗ (Not a FeatureCollection)""
    ERRORS=$((ERRORS + 1))
fi

# Summary
echo """"
echo ""========================================""
if [ $ERRORS -eq 0 ]; then
    echo ""✓ All conformance tests passed!""
    exit 0
else
    echo ""✗ $ERRORS test(s) failed""
    exit 1
fi",

                pythonScript = @"
import requests
import sys

def validate_ogc_conformance(base_url):
    errors = []

    # Test landing page
    try:
        response = requests.get(base_url)
        if response.status_code != 200:
            errors.append(f""Landing page returned {response.status_code}"")
        if 'links' not in response.json():
            errors.append(""Landing page missing 'links' property"")
    except Exception as e:
        errors.append(f""Landing page error: {str(e)}"")

    # Test conformance
    try:
        response = requests.get(f""{base_url}/conformance"")
        conformance = response.json()
        required_class = ""http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core""
        if required_class not in conformance.get('conformsTo', []):
            errors.append(f""Missing required conformance class: {required_class}"")
    except Exception as e:
        errors.append(f""Conformance error: {str(e)}"")

    # Test collections
    try:
        response = requests.get(f""{base_url}/collections"")
        collections = response.json()
        if not collections.get('collections'):
            errors.append(""No collections found"")
        else:
            # Test first collection items
            collection_id = collections['collections'][0]['id']
            response = requests.get(f""{base_url}/collections/{collection_id}/items?limit=1"")
            items = response.json()
            if items.get('type') != 'FeatureCollection':
                errors.append(f""Items response is not a FeatureCollection"")
    except Exception as e:
        errors.append(f""Collections error: {str(e)}"")

    return errors

if __name__ == '__main__':
    base_url = sys.argv[1] if len(sys.argv) > 1 else 'http://localhost:5000'
    errors = validate_ogc_conformance(base_url)

    if errors:
        print(f""❌ {len(errors)} conformance error(s):"")
        for error in errors:
            print(f""  - {error}"")
        sys.exit(1)
    else:
        print(""✅ OGC API Features conformance validated successfully!"")
        sys.exit(0)"
            }
        };

        return JsonSerializer.Serialize(validation, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Generates load test scripts for performance testing")]
    public string GenerateLoadTestScript(
        [Description("Endpoint to test (e.g., /collections/test/items)")] string endpoint,
        [Description("Target requests per second")] int targetRps = 100)
    {
        var loadTests = new
        {
            endpoint,
            targetRps,
            nbomberExample = $@"
using NBomber.CSharp;
using NBomber.Plugins.Http.CSharp;

// Use IHttpClientFactory to avoid socket exhaustion
var httpClient = httpClientFactory.CreateClient(""LoadTest"");

var scenario = Scenario.Create(""ogc_api_load_test"", async context =>
{{
    var request = Http.CreateRequest(""GET"", ""http://localhost:5000{endpoint}"")
        .WithHeader(""Accept"", ""application/geo+json"");

    var response = await Http.Send(httpClient, request);

    return response;
}})
.WithLoadSimulations(
    Simulation.RampingInject(rate: {targetRps},
                            interval: TimeSpan.FromSeconds(1),
                            during: TimeSpan.FromMinutes(5))
);

NBomberRunner
    .RegisterScenarios(scenario)
    .WithReportFormats(ReportFormat.Html, ReportFormat.Csv)
    .Run();",

            k6Example = $@"
import http from 'k6/http';
import {{ check, sleep }} from 'k6';

export let options = {{
    stages: [
        {{ duration: '2m', target: {targetRps / 2} }}, // Ramp up to half target
        {{ duration: '5m', target: {targetRps} }},     // Stay at target RPS
        {{ duration: '2m', target: {targetRps * 2} }}, // Spike test
        {{ duration: '3m', target: {targetRps} }},     // Return to target
        {{ duration: '2m', target: 0 }},               // Ramp down
    ],
    thresholds: {{
        http_req_duration: ['p(95)<500'], // 95% of requests under 500ms
        http_req_failed: ['rate<0.01'],   // Error rate under 1%
    }},
}};

export default function () {{
    let response = http.get('http://localhost:5000{endpoint}?limit=100', {{
        headers: {{ 'Accept': 'application/geo+json' }},
    }});

    check(response, {{
        'status is 200': (r) => r.status === 200,
        'has features': (r) => JSON.parse(r.body).features.length > 0,
        'response time OK': (r) => r.timings.duration < 500,
    }});

    sleep(1);
}}",

            apacheBenchExample = $@"
# Apache Bench (ab) - Simple load testing
# Test {targetRps} req/sec for 30 seconds

ab -n {targetRps * 30} -c 10 -t 30 \
   -H ""Accept: application/geo+json"" \
   -g results.tsv \
   http://localhost:5000{endpoint}?limit=100

# Results analysis
cat results.tsv | awk '{{sum+=$9}} END {{print ""Average response time: "" sum/NR ""ms""}}'",

            wrk2Example = $@"
# wrk2 - Constant throughput load testing
# Maintain {targetRps} RPS for 2 minutes

wrk -t4 -c100 -d2m -R{targetRps} \
    --latency \
    -H ""Accept: application/geo+json"" \
    http://localhost:5000{endpoint}?limit=100

# With Lua script for variable queries
wrk -t4 -c100 -d2m -R{targetRps} \
    --latency \
    -s variable_bbox.lua \
    http://localhost:5000{endpoint}

# variable_bbox.lua
-- request = function()
--     local lon = math.random(-180, 180)
--     local lat = math.random(-90, 90)
--     local bbox = string.format(""?bbox=%d,%d,%d,%d"", lon-1, lat-1, lon+1, lat+1)
--     return wrk.format(""GET"", wrk.path .. bbox)
-- end",

            performanceMetrics = new
            {
                keyMetrics = new[]
                {
                    new { metric = "Response Time", target = "p95 < 500ms, p99 < 1000ms", importance = "User experience" },
                    new { metric = "Throughput", target = $"{targetRps} req/sec sustained", importance = "Capacity planning" },
                    new { metric = "Error Rate", target = "< 0.1% (1 per 1000)", importance = "Reliability" },
                    new { metric = "CPU Usage", target = "< 70% average", importance = "Resource efficiency" },
                    new { metric = "Memory Usage", target = "Stable (no leaks)", importance = "Stability" },
                    new { metric = "Database Connections", target = "< 80% of pool", importance = "Resource management" }
                },
                analysisQueries = new
                {
                    slowQueries = "SELECT query, calls, mean_exec_time FROM pg_stat_statements ORDER BY mean_exec_time DESC LIMIT 10;",
                    connectionPool = "SELECT count(*) FROM pg_stat_activity WHERE datname = 'honua';",
                    cacheHitRate = "SELECT sum(blks_hit) / (sum(blks_hit) + sum(blks_read)) AS cache_hit_ratio FROM pg_stat_database WHERE datname = 'honua';"
                }
            },

            monitoringDuringTest = new[]
            {
                "Monitor server CPU and memory: top -p $(pgrep dotnet)",
                "Watch database connections: watch -n 1 'psql -c \"SELECT count(*) FROM pg_stat_activity\"'",
                "Track response times: Use application metrics (Prometheus, App Insights)",
                "Monitor error logs: tail -f logs/honua.log | grep ERROR",
                "Check cache hit rates: redis-cli info stats"
            },

            testScenarios = new[]
            {
                new { name = "Sustained Load", description = "Constant RPS for extended duration", validates = "Stability, resource leaks" },
                new { name = "Spike Test", description = "Sudden increase to 2-5x normal load", validates = "Elasticity, error handling" },
                new { name = "Soak Test", description = "Normal load for hours/days", validates = "Memory leaks, connection pool exhaustion" },
                new { name = "Stress Test", description = "Gradually increase until failure", validates = "Breaking point, degradation patterns" },
                new { name = "Scalability Test", description = "Test with varying data sizes", validates = "Performance at different scales" }
            }
        };

        return JsonSerializer.Serialize(loadTests, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Analyzes test results and identifies failures")]
    public string AnalyzeTestResults(
        [Description("Test output or results summary")] string testOutput)
    {
        var analysis = new
        {
            analysisApproaches = new object[]
            {
                new
                {
                    aspect = "Test Failure Patterns",
                    patterns = new[]
                    {
                        new
                        {
                            pattern = "Intermittent Failures",
                            causes = new[] { "Race conditions", "Timeout issues", "Network instability", "Resource contention" },
                            investigation = new[]
                            {
                                "Re-run failed tests multiple times",
                                "Check for timing-dependent assertions",
                                "Review logs for transient errors",
                                "Test with different load levels"
                            }
                        },
                        new
                        {
                            pattern = "Consistent Failures",
                            causes = new[] { "Logic bugs", "Configuration errors", "Data issues", "API contract violations" },
                            investigation = new[]
                            {
                                "Review error messages and stack traces",
                                "Validate test data and setup",
                                "Check recent code changes",
                                "Verify environment configuration"
                            }
                        },
                        new
                        {
                            pattern = "Cascading Failures",
                            causes = new[] { "Shared state between tests", "Database not reset", "Cache pollution", "Dependency failures" },
                            investigation = new[]
                            {
                                "Run tests in isolation",
                                "Check test cleanup/teardown",
                                "Verify test independence",
                                "Reset database between test runs"
                            }
                        }
                    },
                    analysis = (object?)null,
                    checks = (object?)null
                },
                new
                {
                    aspect = "Performance Degradation",
                    analysis = new[]
                    {
                        new
                        {
                            symptom = "Increasing Response Times",
                            diagnosis = new[]
                            {
                                "Check for missing spatial indexes: SELECT * FROM pg_indexes WHERE tablename = 'layer'",
                                "Review query execution plans: EXPLAIN ANALYZE query",
                                "Monitor database connection pool: pg_stat_activity",
                                "Check for memory leaks: Monitor process memory over time"
                            },
                            resolution = new[]
                            {
                                "Add missing indexes",
                                "Optimize slow queries",
                                "Increase connection pool size",
                                "Implement query result caching"
                            }
                        },
                        new
                        {
                            symptom = "High Error Rates Under Load",
                            diagnosis = new[]
                            {
                                "Check timeout configuration",
                                "Review connection pool exhaustion",
                                "Look for database deadlocks",
                                "Check resource limits (file descriptors, memory)"
                            },
                            resolution = new[]
                            {
                                "Increase timeouts appropriately",
                                "Scale connection pool",
                                "Implement circuit breakers",
                                "Add retry logic with backoff"
                            }
                        }
                    },
                    patterns = (object?)null,
                    checks = (object?)null
                },
                new
                {
                    aspect = "Data Quality Issues",
                    checks = new[]
                    {
                        new
                        {
                            issue = "Incorrect Feature Count",
                            validation = "SELECT COUNT(*) FROM layer; -- Compare with API response",
                            fixes = new[] { "Verify filters are working correctly", "Check pagination logic", "Ensure spatial index is used" }
                        },
                        new
                        {
                            issue = "Geometry Errors",
                            validation = "SELECT COUNT(*) FROM layer WHERE NOT ST_IsValid(geom)",
                            fixes = new[] { "Fix invalid geometries: UPDATE layer SET geom = ST_MakeValid(geom)", "Add validation to import pipeline" }
                        },
                        new
                        {
                            issue = "CRS Transformation Errors",
                            validation = "SELECT ST_SRID(geom) FROM layer LIMIT 1; -- Verify expected SRID",
                            fixes = new[] { "Ensure correct source SRID", "Verify transformation to target CRS", "Check CRS configuration in metadata" }
                        }
                    },
                    patterns = (object?)null,
                    analysis = (object?)null
                }
            },

            reportingTools = new
            {
                xunitReport = new
                {
                    format = "XML/HTML test results",
                    command = "dotnet test --logger \"html;LogFileName=testresults.html\"",
                    analysis = "Review failed test details, stack traces, and error messages"
                },
                allureReport = new
                {
                    tool = "Allure Test Report Framework",
                    features = new[] { "Trends over time", "Categorized failures", "Historical comparison", "Screenshots/attachments" },
                    usage = @"
# Generate Allure report from test results
allure generate ./allure-results --clean -o ./allure-report
allure open ./allure-report"
                },
                customDashboard = new
                {
                    metrics = new[] { "Pass rate", "Failure trends", "Performance metrics", "Coverage percentage" },
                    tools = new[] { "Grafana + Prometheus", "Azure DevOps", "GitHub Actions Summary", "Custom HTML/JavaScript" }
                }
            },

            trendAnalysis = new
            {
                detectingRegressions = new[]
                {
                    "Compare current results with baseline",
                    "Track performance metrics over time (response time, throughput)",
                    "Monitor test flakiness rate (intermittent failures)",
                    "Analyze failure patterns by category"
                },
                baselineComparison = @"
-- Performance baseline comparison
WITH current_run AS (
    SELECT 'current' AS run, AVG(response_time_ms) AS avg_response
    FROM test_results WHERE test_run_id = (SELECT MAX(test_run_id) FROM test_results)
),
baseline AS (
    SELECT 'baseline' AS run, AVG(response_time_ms) AS avg_response
    FROM test_results WHERE test_run_id = (SELECT test_run_id FROM test_baselines WHERE is_baseline = true)
)
SELECT
    current_run.avg_response AS current_avg,
    baseline.avg_response AS baseline_avg,
    ((current_run.avg_response - baseline.avg_response) / baseline.avg_response * 100) AS percent_change
FROM current_run, baseline;",

                alertingThresholds = new[]
                {
                    new { metric = "Pass Rate", threshold = "< 95%", action = "Investigate failing tests immediately" },
                    new { metric = "Performance Regression", threshold = "> 20% slower", action = "Profile and optimize" },
                    new { metric = "Flaky Tests", threshold = "> 5% flakiness", action = "Fix or quarantine flaky tests" },
                    new { metric = "Coverage", threshold = "< 80%", action = "Add tests for uncovered code" }
                }
            },

            actionableInsights = new[]
            {
                "Fix flaky tests immediately - they hide real failures",
                "Group failures by root cause, not by test name",
                "Track MTTR (Mean Time To Repair) for test failures",
                "Maintain a green build - broken tests lose value quickly",
                "Trend performance metrics to catch degradation early",
                "Document known issues and workarounds in test reports"
            }
        };

        return JsonSerializer.Serialize(analysis, CliJsonOptions.Indented);
    }
}
