// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.ComponentModel;
using System.Linq;
using System.Text.Json;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Plugins;

/// <summary>
/// Semantic Kernel plugin for OGC metadata generation and validation.
/// Provides AI with capabilities to generate, validate, and optimize metadata for OGC services.
/// </summary>
public sealed class MetadataPlugin
{
    [KernelFunction, Description("Auto-generates OGC Collection metadata from database schema information")]
    public string GenerateCollectionMetadata(
        [Description("Database schema information as JSON (tableName, columns, geometryType, extent, srid)")] string dataSourceInfo = "{\"tableName\":\"example\",\"geometryType\":\"Point\"}")
    {
        try
        {
            var schema = JsonSerializer.Deserialize<JsonElement>(dataSourceInfo);

            var tableName = schema.TryGetProperty("tableName", out var tn) ? tn.GetString() : "unknown";
            var geometryType = schema.TryGetProperty("geometryType", out var gt) ? gt.GetString() : "Unknown";
            var srid = schema.TryGetProperty("srid", out var sr) ? sr.GetInt32() : 4326;

            var metadata = new
            {
                id = tableName?.ToLowerInvariant().Replace("_", "-"),
                title = FormatTitle(tableName),
                description = $"Geospatial {geometryType} data collection for {FormatTitle(tableName)}",
                itemType = "feature",
                crs = new[]
                {
                    $"http://www.opengis.net/def/crs/EPSG/0/{srid}",
                    "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
                },
                storageCrs = $"http://www.opengis.net/def/crs/EPSG/0/{srid}",
                extent = schema.TryGetProperty("extent", out var ext) ? ext : JsonSerializer.SerializeToElement(new
                {
                    spatial = new
                    {
                        bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } },
                        crs = "http://www.opengis.net/def/crs/OGC/1.3/CRS84"
                    },
                    temporal = new
                    {
                        interval = new[] { new string?[] { null, null } },
                        trs = "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian"
                    }
                }),
                links = new[]
                {
                    new { rel = "self", type = "application/json", title = "This collection", href = $"/collections/{tableName}" },
                    new { rel = "items", type = "application/geo+json", title = "Items", href = $"/collections/{tableName}/items" },
                    new { rel = "describedBy", type = "text/html", title = "Schema", href = $"/collections/{tableName}/schema" }
                }
            };

            return JsonSerializer.Serialize(new
            {
                success = true,
                generatedMetadata = metadata,
                nextSteps = new[]
                {
                    "Add metadata to metadata.yaml or metadata.json",
                    "Customize title and description for better discoverability",
                    "Update extent with actual data bounds using ST_Extent(geom)",
                    "Add temporal extent if data has time attributes",
                    "Validate with: honua metadata validate"
                },
                example = new
                {
                    yaml = $@"collections:
  - id: {metadata.id}
    title: ""{metadata.title}""
    description: ""{metadata.description}""
    itemType: {metadata.itemType}
    crs: {JsonSerializer.Serialize(metadata.crs)}
    storageCrs: {metadata.storageCrs}"
                }
            }, CliJsonOptions.Indented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message,
                hint = "Ensure dataSourceInfo contains valid JSON with tableName, geometryType, srid fields"
            });
        }
    }

    [KernelFunction, Description("Validates metadata against OGC API standards and reports compliance issues")]
    public string ValidateOgcCompliance(
        [Description("Metadata configuration as JSON string")] string metadataJson = "{}")
    {
        try
        {
            var metadata = JsonSerializer.Deserialize<JsonElement>(metadataJson);
            var issues = new System.Collections.Generic.List<object>();
            var warnings = new System.Collections.Generic.List<object>();

            // Required fields validation
            if (!metadata.TryGetProperty("id", out var id) || id.GetString().IsNullOrWhiteSpace() == true)
            {
                issues.Add(new { field = "id", severity = "error", message = "Collection ID is required and must be unique" });
            }

            if (!metadata.TryGetProperty("title", out var title) || title.GetString().IsNullOrWhiteSpace() == true)
            {
                issues.Add(new { field = "title", severity = "error", message = "Collection title is required for discoverability" });
            }

            if (!metadata.TryGetProperty("extent", out var extent))
            {
                issues.Add(new { field = "extent", severity = "error", message = "Spatial extent is required for OGC API Features" });
            }
            else
            {
                if (!extent.TryGetProperty("spatial", out var spatial) || !spatial.TryGetProperty("bbox", out _))
                {
                    issues.Add(new { field = "extent.spatial.bbox", severity = "error", message = "Bounding box is required in extent" });
                }
            }

            // Best practices validation
            if (!metadata.TryGetProperty("description", out var desc) || desc.GetString()?.Length < 20)
            {
                warnings.Add(new { field = "description", severity = "warning", message = "Add a descriptive summary (20+ characters) for better discoverability" });
            }

            if (!metadata.TryGetProperty("links", out var links) || !links.EnumerateArray().Any())
            {
                warnings.Add(new { field = "links", severity = "warning", message = "Add links (self, items, describedBy) for proper hypermedia navigation" });
            }

            if (!metadata.TryGetProperty("crs", out var crs))
            {
                warnings.Add(new { field = "crs", severity = "warning", message = "Specify supported CRS list for projection flexibility" });
            }

            if (metadata.TryGetProperty("keywords", out var keywords))
            {
                if (!keywords.EnumerateArray().Any())
                {
                    warnings.Add(new { field = "keywords", severity = "info", message = "Add keywords for catalog search optimization" });
                }
            }
            else
            {
                warnings.Add(new { field = "keywords", severity = "info", message = "Add keywords array for better discoverability" });
            }

            var isCompliant = issues.Count == 0;

            return JsonSerializer.Serialize(new
            {
                compliant = isCompliant,
                summary = isCompliant
                    ? "Metadata meets OGC API Features requirements"
                    : $"Found {issues.Count} compliance issues that must be fixed",
                errorCount = issues.Count,
                warningCount = warnings.Count,
                errors = issues,
                warnings = warnings,
                recommendations = new[]
                {
                    "Review OGC API Features Part 1: Core specification",
                    "Use honua metadata validate for automated checks",
                    "Test with OGC API validator: https://cite.opengeospatial.org/teamengine/",
                    "Ensure all required fields have meaningful values, not placeholders"
                }
            }, CliJsonOptions.Indented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                compliant = false,
                error = "Invalid JSON format",
                message = ex.Message,
                hint = "Ensure metadataJson is valid JSON"
            });
        }
    }

    [KernelFunction, Description("Analyzes current metadata and suggests enhancements for completeness and discoverability")]
    public string SuggestMetadataEnhancements(
        [Description("Current metadata configuration as JSON")] string currentMetadata = "{}")
    {
        try
        {
            var metadata = JsonSerializer.Deserialize<JsonElement>(currentMetadata);
            var enhancements = new System.Collections.Generic.List<object>();

            // Temporal extent enhancement
            if (!metadata.TryGetProperty("extent", out var extent) ||
                !extent.TryGetProperty("temporal", out _))
            {
                enhancements.Add(new
                {
                    category = "Temporal Information",
                    priority = "Medium",
                    enhancement = "Add temporal extent if data has time attributes",
                    benefit = "Enables time-based filtering and queries",
                    example = new
                    {
                        temporal = new
                        {
                            interval = new[] { new[] { "2020-01-01T00:00:00Z", "2024-12-31T23:59:59Z" } },
                            trs = "http://www.opengis.net/def/uom/ISO-8601/0/Gregorian"
                        }
                    }
                });
            }

            // Keywords enhancement
            if (!metadata.TryGetProperty("keywords", out var keywords) || !keywords.EnumerateArray().Any())
            {
                enhancements.Add(new
                {
                    category = "Discoverability",
                    priority = "High",
                    enhancement = "Add relevant keywords for catalog search",
                    benefit = "Improves discovery in data catalogs and search engines",
                    example = new
                    {
                        keywords = new[] { "geospatial", "vector", "administrative boundaries", "OGC API" }
                    }
                });
            }

            // License information
            if (!metadata.TryGetProperty("license", out _))
            {
                enhancements.Add(new
                {
                    category = "Legal Information",
                    priority = "High",
                    enhancement = "Add license information for data usage clarity",
                    benefit = "Clarifies usage rights and restrictions",
                    example = new
                    {
                        license = "CC-BY-4.0",
                        licenseUrl = "https://creativecommons.org/licenses/by/4.0/"
                    }
                });
            }

            // Provider information
            if (!metadata.TryGetProperty("providers", out _))
            {
                enhancements.Add(new
                {
                    category = "Data Provenance",
                    priority = "Medium",
                    enhancement = "Add provider/publisher information",
                    benefit = "Establishes data source credibility and contact points",
                    example = new
                    {
                        providers = new[]
                        {
                            new
                            {
                                name = "Organization Name",
                                description = "Data provider and maintainer",
                                roles = new[] { "producer", "licensor" },
                                url = "https://example.org"
                            }
                        }
                    }
                });
            }

            // CRS support
            if (!metadata.TryGetProperty("crs", out var crsInfo))
            {
                enhancements.Add(new
                {
                    category = "Projection Support",
                    priority = "High",
                    enhancement = "Specify supported coordinate reference systems",
                    benefit = "Enables clients to request data in preferred projections",
                    example = new
                    {
                        crs = new[]
                        {
                            "http://www.opengis.net/def/crs/OGC/1.3/CRS84",
                            "http://www.opengis.net/def/crs/EPSG/0/4326",
                            "http://www.opengis.net/def/crs/EPSG/0/3857"
                        }
                    }
                });
            }

            // Data quality information
            if (!metadata.TryGetProperty("dataQuality", out _))
            {
                enhancements.Add(new
                {
                    category = "Data Quality",
                    priority = "Low",
                    enhancement = "Add data quality and accuracy information",
                    benefit = "Helps users assess fitness for purpose",
                    example = new
                    {
                        dataQuality = new
                        {
                            scope = "dataset",
                            lineage = "Derived from authoritative government sources, updated annually",
                            accuracy = "Positional accuracy: ±5 meters"
                        }
                    }
                });
            }

            return JsonSerializer.Serialize(new
            {
                currentFields = metadata.EnumerateObject().Select(p => p.Name).ToArray(),
                enhancementCount = enhancements.Count,
                enhancements,
                priority = new
                {
                    critical = enhancements.Where(e => ((dynamic)e).priority == "High").Count(),
                    recommended = enhancements.Where(e => ((dynamic)e).priority == "Medium").Count(),
                    optional = enhancements.Where(e => ((dynamic)e).priority == "Low").Count()
                },
                seoTips = new[]
                {
                    "Use descriptive, keyword-rich titles (not generic names)",
                    "Write descriptions in clear, accessible language",
                    "Include location names in keywords for geographic search",
                    "Add update frequency information for data freshness",
                    "Link to schema documentation and data dictionaries"
                }
            }, CliJsonOptions.Indented);
        }
        catch (Exception ex)
        {
            return JsonSerializer.Serialize(new
            {
                success = false,
                error = ex.Message
            });
        }
    }

    [KernelFunction, Description("Generates STAC (SpatioTemporal Asset Catalog) catalog structure from raster data")]
    public string GenerateSTACCatalog(
        [Description("Workspace path containing raster files and metadata")] string workspacePath = "/tmp/workspace")
    {
        var stacCatalog = new
        {
            stac_version = "1.0.0",
            type = "Catalog",
            id = "honua-raster-catalog",
            title = "Honua Raster Data Catalog",
            description = "STAC catalog for raster datasets served by Honua",
            links = new[]
            {
                new { rel = "root", href = "./catalog.json", type = "application/json" },
                new { rel = "self", href = "./catalog.json", type = "application/json" }
            },
            collections = new[]
            {
                new
                {
                    stac_version = "1.0.0",
                    type = "Collection",
                    id = "elevation-dem",
                    title = "Digital Elevation Model",
                    description = "High-resolution elevation data",
                    license = "CC-BY-4.0",
                    extent = new
                    {
                        spatial = new
                        {
                            bbox = new[] { new[] { -180.0, -90.0, 180.0, 90.0 } }
                        },
                        temporal = new
                        {
                            interval = new[] { new string?[] { "2020-01-01T00:00:00Z", null } }
                        }
                    },
                    links = new[]
                    {
                        new { rel = "self", href = "./collections/elevation-dem.json", type = "application/json" },
                        new { rel = "parent", href = "./catalog.json", type = "application/json" },
                        new { rel = "items", href = "./collections/elevation-dem/items", type = "application/geo+json" }
                    }
                }
            }
        };

        var stacItem = new
        {
            stac_version = "1.0.0",
            stac_extensions = new[] { "https://stac-extensions.github.io/projection/v1.0.0/schema.json" },
            type = "Feature",
            id = "dem-tile-001",
            geometry = new
            {
                type = "Polygon",
                coordinates = new[] { new[] { new[] { -180.0, -90.0 }, new[] { 180.0, -90.0 }, new[] { 180.0, 90.0 }, new[] { -180.0, 90.0 }, new[] { -180.0, -90.0 } } }
            },
            bbox = new[] { -180.0, -90.0, 180.0, 90.0 },
            properties = new
            {
                datetime = "2020-01-01T00:00:00Z",
                projection_epsg = 4326
            },
            links = new[]
            {
                new { rel = "self", href = "./collections/elevation-dem/items/dem-tile-001.json", type = "application/json" },
                new { rel = "parent", href = "./collections/elevation-dem.json", type = "application/json" },
                new { rel = "collection", href = "./collections/elevation-dem.json", type = "application/json" }
            },
            assets = new
            {
                data = new
                {
                    href = "/tiles/elevation-dem/{tileMatrixSetId}/{tileMatrix}/{tileRow}/{tileCol}",
                    type = "image/tiff; application=geotiff; profile=cloud-optimized",
                    title = "COG Elevation Data",
                    roles = new[] { "data" }
                },
                preview = new
                {
                    href = "/tiles/elevation-dem/WebMercatorQuad/{z}/{x}/{y}.png",
                    type = "image/png",
                    title = "Visual Preview",
                    roles = new[] { "visual" }
                }
            }
        };

        return JsonSerializer.Serialize(new
        {
            catalogGenerated = true,
            stacVersion = "1.0.0",
            catalog = stacCatalog,
            exampleItem = stacItem,
            implementation = new
            {
                catalogFile = "Save catalog object to: catalog.json",
                collectionFile = "Save each collection to: collections/{collection-id}.json",
                itemsEndpoint = "Serve items at: /collections/{collection-id}/items",
                staticAPI = "Can serve as static JSON files or implement STAC API"
            },
            nextSteps = new[]
            {
                "Scan workspace for .tif files to create actual STAC items",
                "Extract raster metadata using gdalinfo",
                "Generate bounding boxes from GeoTIFF georeferencing",
                "Create collection for each raster dataset type",
                "Implement STAC API endpoints or serve static catalog",
                "Validate with stac-validator: pip install stac-validator"
            },
            resources = new[]
            {
                "STAC Spec: https://stacspec.org",
                "STAC Browser: https://github.com/radiantearth/stac-browser",
                "PySTAC: https://pystac.readthedocs.io for Python implementations"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Provides SEO and discoverability optimization tips for metadata")]
    public string OptimizeMetadataForDiscovery(
        [Description("Current metadata as JSON")] string metadata = "{}")
    {
        var optimizations = new object[]
        {
            new
            {
                aspect = "Title Optimization",
                currentPractice = "Generic names like 'layer1' or 'data'",
                bestPractice = "Descriptive, keyword-rich titles",
                examples = new object[]
                {
                    new { poor = "buildings", better = "Municipal Building Footprints 2024 - City of Portland" },
                    new { poor = "roads", better = "Transportation Network - Primary and Secondary Roads" },
                    new { poor = "layer3", better = "Agricultural Land Use Classification - USDA 2023" }
                },
                impact = "High - Titles are primary search ranking factor"
            },
            new
            {
                aspect = "Description Enhancement",
                currentPractice = "Short, technical descriptions",
                bestPractice = "Comprehensive, accessible summaries",
                examples = new[]
                {
                    new {
                        poor = "Polygon layer of buildings",
                        better = "Comprehensive building footprint dataset covering all structures in the metropolitan area. Includes building height, construction year, and use classification. Updated quarterly from municipal records and aerial imagery analysis."
                    }
                },
                impact = "High - Descriptions improve relevance scoring"
            },
            new
            {
                aspect = "Keyword Strategy",
                currentPractice = "No keywords or minimal technical terms",
                bestPractice = "Mix of technical, geographic, and domain keywords",
                examples = new[]
                {
                    new {
                        poor = new[] { "vector", "polygon" },
                        better = new[] {
                            "buildings", "footprints", "architecture", "urban planning",
                            "Portland", "Oregon", "municipal", "cadastral",
                            "OGC API", "geospatial", "vector", "GIS"
                        }
                    }
                },
                impact = "Critical - Keywords enable catalog and web search discovery"
            },
            new
            {
                aspect = "Temporal Information",
                currentPractice = "No temporal metadata",
                bestPractice = "Explicit date ranges and update frequency",
                examples = new[]
                {
                    new {
                        field = "updateFrequency",
                        value = "quarterly",
                        benefit = "Users know data freshness"
                    },
                    new {
                        field = "temporal.interval",
                        value = "[\"2020-01-01\", \"2024-12-31\"]",
                        benefit = "Enables time-based search filters"
                    }
                },
                impact = "Medium - Critical for time-sensitive applications"
            },
            new
            {
                aspect = "Link Relationships",
                currentPractice = "Minimal or missing links",
                bestPractice = "Comprehensive link network",
                examples = new[]
                {
                    new { rel = "describedBy", purpose = "Link to schema/data dictionary", benefit = "Improves data understanding" },
                    new { rel = "license", purpose = "Link to license terms", benefit = "Clarifies usage rights" },
                    new { rel = "via", purpose = "Link to source data", benefit = "Establishes provenance" },
                    new { rel = "preview", purpose = "Link to map preview", benefit = "Visual discovery" }
                },
                impact = "Medium - Enhances navigation and SEO link graph"
            },
            new
            {
                aspect = "Structured Data Markup",
                currentPractice = "Plain JSON metadata",
                bestPractice = "Schema.org markup for web pages",
                examples = new[]
                {
                    new {
                        type = "Dataset",
                        markup = "Add schema.org/Dataset to HTML landing pages",
                        benefit = "Google Dataset Search indexing"
                    }
                },
                impact = "High - Direct Google Dataset Search visibility"
            }
        };

        var checklist = new[]
        {
            new {
                category = "Content Quality",
                items = new[]
                {
                    "✓ Title includes dataset name, location, and year",
                    "✓ Description is 100+ words with context and use cases",
                    "✓ Keywords include geographic, thematic, and technical terms",
                    "✓ All acronyms are defined in description"
                }
            },
            new {
                category = "Discoverability",
                items = new[]
                {
                    "✓ Metadata includes license and usage rights",
                    "✓ Provider/publisher information is complete",
                    "✓ Update frequency is specified",
                    "✓ Links to documentation and previews are included"
                }
            },
            new {
                category = "Technical SEO",
                items = new[]
                {
                    "✓ Landing page has schema.org/Dataset markup",
                    "✓ Canonical URLs are set for all endpoints",
                    "✓ Sitemap includes all collection pages",
                    "✓ robots.txt allows crawler access"
                }
            }
        };

        return JsonSerializer.Serialize(new
        {
            optimizationStrategies = optimizations,
            implementationChecklist = checklist,
            quickWins = new[]
            {
                "Add geographic location names to titles (city, state, country)",
                "Expand descriptions to 150-300 words with use cases",
                "Include 8-15 diverse keywords (technical + domain + geographic)",
                "Add 'license' link to clarify usage rights",
                "Set 'updated' timestamp to show data freshness"
            },
            searchEngineIntegration = new
            {
                googleDatasetSearch = new
                {
                    requirement = "schema.org/Dataset markup on HTML pages",
                    validation = "https://search.google.com/test/rich-results",
                    implementation = "Add JSON-LD script to collection landing pages"
                },
                dataCatalogs = new[]
                {
                    "Data.gov - Submit catalog URL",
                    "CKAN - Implement CKAN harvesting endpoint",
                    "GeoNetwork - OGC CSW support for catalog harvesting"
                }
            },
            resources = new[]
            {
                "Schema.org Dataset: https://schema.org/Dataset",
                "Google Dataset Search: https://datasetsearch.research.google.com",
                "OGC API Records: https://ogcapi.ogc.org/records/"
            }
        }, CliJsonOptions.Indented);
    }

    [KernelFunction, Description("Analyzes workspace for metadata configuration")]
    public string AnalyzeWorkspace(
        [Description("Path to the workspace directory")] string workspacePath = "/tmp/workspace")
    {
        return JsonSerializer.Serialize(new
        {
            success = true,
            workspacePath,
            note = "For detailed workspace analysis, use Workspace.AnalyzeWorkspace",
            summary = new
            {
                metadataFound = false,
                recommendation = "Generate metadata using Metadata.GenerateCollectionMetadata"
            }
        }, CliJsonOptions.Indented);
    }

    private static string FormatTitle(string? tableName)
    {
        if (tableName.IsNullOrWhiteSpace())
            return "Untitled Collection";

        // Convert snake_case or kebab-case to Title Case
        return string.Join(" ", tableName.Split(new[] { '_', '-' }, StringSplitOptions.RemoveEmptyEntries)
            .Where(word => !word.IsNullOrEmpty()) // Ensure word is not empty before indexing
            .Select(word => char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant()));
    }
}
