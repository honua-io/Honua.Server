using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Jobs;
using NetTopologySuite.Geometries;
using System.Text;
using System.Text.Json;
using System.Xml;
using System.Xml.Linq;

namespace Honua.Server.Benchmarks;

/// <summary>
/// Comprehensive benchmarks for OGC API operations including Features, Tiles, Records, and Processes.
/// Measures request parsing, response serialization, and protocol overhead.
/// </summary>
[MemoryDiagnoser]
[SimpleJob(RuntimeMoniker.Net90)]
[RankColumn]
[MarkdownExporter]
[JsonExporter]
public class OgcApiBenchmarks
{
    private JsonSerializerOptions _jsonOptions = null!;
    private GeometryFactory _geometryFactory = null!;
    private List<TestFeature> _features100 = null!;
    private List<TestFeature> _features1000 = null!;
    private const string BaseUrl = "https://api.example.com";

    [GlobalSetup]
    public void Setup()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        _geometryFactory = new GeometryFactory(new PrecisionModel(), 4326);
        _features100 = GenerateFeatures(100);
        _features1000 = GenerateFeatures(1000);
    }

    // =====================================================
    // OGC API Features - Core
    // =====================================================

    [Benchmark(Description = "OGC API Features: Landing page")]
    public string FeaturesLandingPage()
    {
        var landingPage = new
        {
            title = "Honua Geospatial Server",
            description = "OGC API Features compliant server providing access to geospatial datasets",
            links = new[]
            {
                new { href = $"{BaseUrl}/", rel = "self", type = "application/json", title = "This document" },
                new { href = $"{BaseUrl}/api", rel = "service-desc", type = "application/vnd.oai.openapi+json;version=3.0", title = "API definition" },
                new { href = $"{BaseUrl}/conformance", rel = "conformance", type = "application/json", title = "Conformance classes" },
                new { href = $"{BaseUrl}/collections", rel = "data", type = "application/json", title = "Collections" }
            }
        };

        return JsonSerializer.Serialize(landingPage, _jsonOptions);
    }

    [Benchmark(Description = "OGC API Features: Conformance declaration")]
    public string FeaturesConformance()
    {
        var conformance = new
        {
            conformsTo = new[]
            {
                "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core",
                "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/oas30",
                "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson",
                "http://www.opengis.net/spec/ogcapi-features-2/1.0/conf/crs",
                "http://www.opengis.net/spec/ogcapi-features-3/1.0/conf/filter",
                "http://www.opengis.net/spec/ogcapi-common-1/1.0/conf/core",
                "http://www.opengis.net/spec/ogcapi-common-2/1.0/conf/collections"
            }
        };

        return JsonSerializer.Serialize(conformance, _jsonOptions);
    }

    [Benchmark(Description = "OGC API Features: Collections list (100 collections)")]
    public string FeaturesCollectionsList()
    {
        var collections = new
        {
            links = new[]
            {
                new { href = $"{BaseUrl}/collections", rel = "self", type = "application/json" }
            },
            collections = Enumerable.Range(1, 100).Select(i => new
            {
                id = $"collection_{i}",
                title = $"Collection {i}",
                description = $"Test collection {i} for benchmarking",
                extent = new
                {
                    spatial = new
                    {
                        bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } },
                        crs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
                    },
                    temporal = new
                    {
                        interval = new[] { new[] { "2020-01-01T00:00:00Z", "2024-12-31T23:59:59Z" } },
                        trs = "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian"
                    }
                },
                links = new[]
                {
                    new { href = $"{BaseUrl}/collections/collection_{i}", rel = "self", type = "application/json" },
                    new { href = $"{BaseUrl}/collections/collection_{i}/items", rel = "items", type = "application/geo+json" },
                    new { href = $"{BaseUrl}/collections/collection_{i}/queryables", rel = "queryables", type = "application/schema+json" }
                },
                itemType = "feature",
                crs = new[]
                {
                    "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
                    "http://www.opengis.net/def/crs/EPSG/0/4326",
                    "http://www.opengis.net/def/crs/EPSG/0/3857"
                }
            }).ToArray()
        };

        return JsonSerializer.Serialize(collections, _jsonOptions);
    }

    [Benchmark(Description = "OGC API Features: Single collection metadata")]
    public string FeaturesSingleCollection()
    {
        var collection = new
        {
            id = "parcels",
            title = "Property Parcels",
            description = "Property parcel boundaries with ownership information",
            extent = new
            {
                spatial = new
                {
                    bbox = new[] { new[] { -122.5, 45.5, -122.3, 45.7 } },
                    crs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
                },
                temporal = new
                {
                    interval = new[] { new[] { "2020-01-01T00:00:00Z", "2024-12-31T23:59:59Z" } }
                }
            },
            links = new[]
            {
                new { href = $"{BaseUrl}/collections/parcels", rel = "self", type = "application/json" },
                new { href = $"{BaseUrl}/collections/parcels/items", rel = "items", type = "application/geo+json" },
                new { href = $"{BaseUrl}/collections/parcels/schema", rel = "describedby", type = "application/schema+json" }
            },
            itemType = "feature",
            crs = new[]
            {
                "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
                "http://www.opengis.net/def/crs/EPSG/0/4326",
                "http://www.opengis.net/def/crs/EPSG/0/3857"
            },
            storageCrs = "http://www.opengis.net/def/crs/EPSG/0/4326"
        };

        return JsonSerializer.Serialize(collection, _jsonOptions);
    }

    [Benchmark(Description = "OGC API Features: Items response (100 features)")]
    public string FeaturesItems100()
    {
        var response = new
        {
            type = "FeatureCollection",
            numberMatched = 1000,
            numberReturned = 100,
            timeStamp = DateTime.UtcNow.ToString("o"),
            links = new[]
            {
                new { href = $"{BaseUrl}/collections/parcels/items", rel = "self", type = "application/geo+json" },
                new { href = $"{BaseUrl}/collections/parcels/items?offset=100", rel = "next", type = "application/geo+json" }
            },
            features = _features100
        };

        return JsonSerializer.Serialize(response, _jsonOptions);
    }

    [Benchmark(Description = "OGC API Features: Items response (1,000 features)")]
    public string FeaturesItems1000()
    {
        var response = new
        {
            type = "FeatureCollection",
            numberMatched = 1000,
            numberReturned = 1000,
            timeStamp = DateTime.UtcNow.ToString("o"),
            links = new[]
            {
                new { href = $"{BaseUrl}/collections/parcels/items", rel = "self", type = "application/geo+json" }
            },
            features = _features1000
        };

        return JsonSerializer.Serialize(response, _jsonOptions);
    }

    [Benchmark(Description = "OGC API Features: Single item")]
    public string FeaturesSingleItem()
    {
        return JsonSerializer.Serialize(_features100[0], _jsonOptions);
    }

    [Benchmark(Description = "OGC API Features: Parse bbox parameter")]
    public double[] ParseBboxParameter()
    {
        var bboxString = "-122.5,45.5,-122.3,45.7";
        return bboxString.Split(',').Select(double.Parse).ToArray();
    }

    [Benchmark(Description = "OGC API Features: Parse datetime parameter")]
    public (DateTime? start, DateTime? end) ParseDatetimeParameter()
    {
        var datetimeString = "2020-01-01T00:00:00Z/2024-12-31T23:59:59Z";
        var parts = datetimeString.Split('/');
        return (DateTime.Parse(parts[0]), DateTime.Parse(parts[1]));
    }

    // =====================================================
    // OGC API Features - CQL2 Filter
    // =====================================================

    [Benchmark(Description = "OGC API Features: Parse CQL2-TEXT simple")]
    public string ParseCql2TextSimple()
    {
        var cql = "category = 'Residential' AND area_sqm > 10000";
        // Simulate parsing
        var tokens = cql.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return string.Join("|", tokens);
    }

    [Benchmark(Description = "OGC API Features: Parse CQL2-JSON")]
    public object ParseCql2Json()
    {
        var cqlJson = """
        {
            "op": "and",
            "args": [
                {
                    "op": "=",
                    "args": [{"property": "category"}, "Residential"]
                },
                {
                    "op": ">",
                    "args": [{"property": "area_sqm"}, 10000]
                }
            ]
        }
        """;

        return JsonSerializer.Deserialize<object>(cqlJson)!;
    }

    // =====================================================
    // OGC API Tiles
    // =====================================================

    [Benchmark(Description = "OGC API Tiles: Tileset metadata")]
    public string TilesTilesetMetadata()
    {
        var tileset = new
        {
            title = "Property Parcels Vector Tiles",
            tileMatrixSetURI = "http://www.opengis.net/def/tilematrixset/OGC/1.0/WebMercatorQuad",
            crs = "http://www.opengis.net/def/crs/EPSG/0/3857",
            dataType = "vector",
            links = new[]
            {
                new { href = $"{BaseUrl}/collections/parcels/tiles/WebMercatorQuad/{{tileMatrix}}/{{tileRow}}/{{tileCol}}",
                      rel = "item",
                      type = "application/vnd.mapbox-vector-tile",
                      templated = true }
            },
            layers = new[]
            {
                new { id = "parcels", dataType = "vector", minzoom = 0, maxzoom = 14 }
            }
        };

        return JsonSerializer.Serialize(tileset, _jsonOptions);
    }

    [Benchmark(Description = "OGC API Tiles: TileMatrixSet list")]
    public string TilesTileMatrixSetList()
    {
        var tileMatrixSets = new
        {
            tileMatrixSets = new[]
            {
                new { id = "WebMercatorQuad", title = "Google Maps Compatible for the World",
                      uri = "http://www.opengis.net/def/tilematrixset/OGC/1.0/WebMercatorQuad" },
                new { id = "WorldCRS84Quad", title = "CRS84 for the World",
                      uri = "http://www.opengis.net/def/tilematrixset/OGC/1.0/WorldCRS84Quad" }
            }
        };

        return JsonSerializer.Serialize(tileMatrixSets, _jsonOptions);
    }

    [Benchmark(Description = "OGC API Tiles: Parse tile coordinates")]
    public (string tms, int z, int x, int y) ParseTileCoordinates()
    {
        var path = "/collections/parcels/tiles/WebMercatorQuad/10/512/384";
        var parts = path.Split('/');
        return (parts[4], int.Parse(parts[5]), int.Parse(parts[6]), int.Parse(parts[7]));
    }

    // =====================================================
    // WFS 2.0/3.0 XML Operations
    // =====================================================

    [Benchmark(Description = "WFS: GetCapabilities XML generation")]
    public string WfsGetCapabilities()
    {
        var sb = new StringBuilder(8192);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append("<WFS_Capabilities version=\"2.0.0\" ");
        sb.Append("xmlns=\"http://www.opengis.net/wfs/2.0\" ");
        sb.Append("xmlns:xsi=\"http://www.w3.org/2001/XMLSchema-instance\">");
        sb.Append("<ServiceIdentification>");
        sb.Append("<Title>Honua WFS Server</Title>");
        sb.Append("<ServiceType>WFS</ServiceType>");
        sb.Append("<ServiceTypeVersion>2.0.0</ServiceTypeVersion>");
        sb.Append("</ServiceIdentification>");
        sb.Append("<FeatureTypeList>");

        for (int i = 0; i < 50; i++)
        {
            sb.Append($"<FeatureType>");
            sb.Append($"<Name>layer_{i}</Name>");
            sb.Append($"<Title>Layer {i}</Title>");
            sb.Append($"<DefaultCRS>urn:ogc:def:crs:EPSG::4326</DefaultCRS>");
            sb.Append($"<WGS84BoundingBox><LowerCorner>-180 -90</LowerCorner><UpperCorner>180 90</UpperCorner></WGS84BoundingBox>");
            sb.Append($"</FeatureType>");
        }

        sb.Append("</FeatureTypeList>");
        sb.Append("</WFS_Capabilities>");

        return sb.ToString();
    }

    [Benchmark(Description = "WFS: Parse GetFeature request")]
    public Dictionary<string, string> WfsParseGetFeature()
    {
        var queryString = "SERVICE=WFS&VERSION=2.0.0&REQUEST=GetFeature&TYPENAMES=parcels&COUNT=100&BBOX=-122.5,45.5,-122.3,45.7,urn:ogc:def:crs:EPSG::4326";

        return queryString.Split('&')
            .Select(param => param.Split('='))
            .ToDictionary(parts => parts[0], parts => parts[1]);
    }

    [Benchmark(Description = "WFS: GML 3.2 feature serialization (100 features)")]
    public string WfsGml32Serialization()
    {
        var sb = new StringBuilder(16384);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append("<wfs:FeatureCollection xmlns:wfs=\"http://www.opengis.net/wfs/2.0\" ");
        sb.Append("xmlns:gml=\"http://www.opengis.net/gml/3.2\" ");
        sb.Append("numberMatched=\"100\" numberReturned=\"100\">");

        foreach (var feature in _features100)
        {
            sb.Append("<wfs:member>");
            sb.Append($"<Parcel gml:id=\"{feature.Id}\">");
            sb.Append($"<name>{feature.Properties["name"]}</name>");
            sb.Append($"<category>{feature.Properties["category"]}</category>");
            sb.Append("<geometry>");
            sb.Append("<gml:Polygon srsName=\"urn:ogc:def:crs:EPSG::4326\">");
            sb.Append("<gml:exterior><gml:LinearRing>");
            sb.Append("<gml:posList>45.5 -122.5 45.5 -122.4 45.6 -122.4 45.6 -122.5 45.5 -122.5</gml:posList>");
            sb.Append("</gml:LinearRing></gml:exterior>");
            sb.Append("</gml:Polygon>");
            sb.Append("</geometry>");
            sb.Append("</Parcel>");
            sb.Append("</wfs:member>");
        }

        sb.Append("</wfs:FeatureCollection>");
        return sb.ToString();
    }

    // =====================================================
    // WMS Operations
    // =====================================================

    [Benchmark(Description = "WMS 1.3.0: Parse GetMap request")]
    public Dictionary<string, string> WmsParseGetMap()
    {
        var queryString = "SERVICE=WMS&VERSION=1.3.0&REQUEST=GetMap&LAYERS=parcels,boundaries&STYLES=&CRS=EPSG:3857&BBOX=-13692297,5693675,-13688089,5697883&WIDTH=512&HEIGHT=512&FORMAT=image/png&TRANSPARENT=true";

        return queryString.Split('&')
            .Select(param => param.Split('='))
            .ToDictionary(parts => parts[0], parts => parts[1]);
    }

    [Benchmark(Description = "WMS 1.3.0: GetCapabilities XML (100 layers)")]
    public string WmsGetCapabilities()
    {
        var sb = new StringBuilder(32768);
        sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        sb.Append("<WMS_Capabilities version=\"1.3.0\" xmlns=\"http://www.opengis.net/wms\">");
        sb.Append("<Service><Name>WMS</Name><Title>Honua WMS</Title></Service>");
        sb.Append("<Capability>");
        sb.Append("<Layer><Title>Root Layer</Title>");

        for (int i = 0; i < 100; i++)
        {
            sb.Append($"<Layer queryable=\"1\">");
            sb.Append($"<Name>layer_{i}</Name>");
            sb.Append($"<Title>Layer {i}</Title>");
            sb.Append($"<CRS>EPSG:4326</CRS><CRS>EPSG:3857</CRS>");
            sb.Append($"<EX_GeographicBoundingBox><westBoundLongitude>-180</westBoundLongitude>");
            sb.Append($"<eastBoundLongitude>180</eastBoundLongitude>");
            sb.Append($"<southBoundLatitude>-90</southBoundLatitude>");
            sb.Append($"<northBoundLatitude>90</northBoundLatitude></EX_GeographicBoundingBox>");
            sb.Append($"</Layer>");
        }

        sb.Append("</Layer></Capability>");
        sb.Append("</WMS_Capabilities>");

        return sb.ToString();
    }

    // =====================================================
    // Helper Methods
    // =====================================================

    private List<TestFeature> GenerateFeatures(int count)
    {
        var features = new List<TestFeature>(count);
        var random = new Random(42);

        for (int i = 0; i < count; i++)
        {
            var baseLon = -122.4 + (random.NextDouble() * 0.2);
            var baseLat = 45.5 + (random.NextDouble() * 0.2);
            var size = 0.001;

            features.Add(new TestFeature
            {
                Id = $"feature_{i + 1}",
                Type = "Feature",
                Geometry = new
                {
                    type = "Polygon",
                    coordinates = new[]
                    {
                        new[]
                        {
                            new[] { baseLon, baseLat },
                            new[] { baseLon + size, baseLat },
                            new[] { baseLon + size, baseLat + size },
                            new[] { baseLon, baseLat + size },
                            new[] { baseLon, baseLat }
                        }
                    }
                },
                Properties = new Dictionary<string, object>
                {
                    ["name"] = $"Property {i + 1}",
                    ["category"] = i % 3 == 0 ? "Residential" : i % 3 == 1 ? "Commercial" : "Industrial",
                    ["area_sqm"] = 10000.0 + (random.NextDouble() * 5000.0),
                    ["created_at"] = new DateTime(2024, 1, 1).AddDays(i).ToString("o")
                }
            });
        }

        return features;
    }

    private class TestFeature
    {
        public string Id { get; set; } = "";
        public string Type { get; set; } = "Feature";
        public object Geometry { get; set; } = new { };
        public Dictionary<string, object> Properties { get; set; } = new();
    }
}
