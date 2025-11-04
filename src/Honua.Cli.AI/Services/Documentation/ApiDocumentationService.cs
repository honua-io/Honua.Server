// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Text.Json;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Documentation;

/// <summary>
/// Service for generating OpenAPI/Swagger documentation.
/// </summary>
public sealed class ApiDocumentationService
{
    /// <summary>
    /// Generates OpenAPI/Swagger documentation from metadata configuration.
    /// </summary>
    /// <param name="metadataConfig">Metadata configuration as JSON</param>
    /// <returns>JSON containing OpenAPI specification and Swagger UI setup</returns>
    public string GenerateApiDocs(string metadataConfig)
    {
        var openApiSpec = new
        {
            openapi = "3.0.3",
            info = new
            {
                title = "Honua Geospatial API",
                description = "OGC API Features implementation for geospatial data",
                version = "1.0.0",
                contact = new
                {
                    name = "API Support",
                    email = "support@example.com"
                },
                license = new
                {
                    name = "MIT",
                    url = "https://opensource.org/licenses/MIT"
                }
            },
            servers = new[]
            {
                new { url = "https://api.example.com", description = "Production" },
                new { url = "https://staging.example.com", description = "Staging" },
                new { url = "http://localhost:5000", description = "Local Development" }
            },
            paths = new
            {
                root = new
                {
                    get = new
                    {
                        summary = "Landing page",
                        description = "The landing page provides links to API definition, conformance, and data",
                        operationId = "getLandingPage",
                        tags = new[] { "Capabilities" },
                        responses = new
                        {
                            _200 = new
                            {
                                description = "Links to API resources",
                                content = new
                                {
                                    applicationJson = new
                                    {
                                        schema = new
                                        {
                                            type = "object",
                                            properties = new
                                            {
                                                links = new
                                                {
                                                    type = "array",
                                                    items = new { @ref = "#/components/schemas/Link" }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                conformance = new
                {
                    get = new
                    {
                        summary = "Conformance classes",
                        description = "List of OGC API conformance classes implemented by this server",
                        operationId = "getConformance",
                        tags = new[] { "Capabilities" },
                        responses = new
                        {
                            _200 = new
                            {
                                description = "Conformance declaration",
                                content = new
                                {
                                    applicationJson = new
                                    {
                                        schema = new
                                        {
                                            type = "object",
                                            required = new[] { "conformsTo" },
                                            properties = new
                                            {
                                                conformsTo = new
                                                {
                                                    type = "array",
                                                    items = new { type = "string" },
                                                    example = new[]
                                                    {
                                                        "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/core",
                                                        "http://www.opengis.net/spec/ogcapi-features-1/1.0/conf/geojson"
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                },
                collections = new
                {
                    get = new
                    {
                        summary = "List collections",
                        description = "List all available geospatial data collections",
                        operationId = "getCollections",
                        tags = new[] { "Data" },
                        responses = new
                        {
                            _200 = new
                            {
                                description = "Collection list",
                                content = new
                                {
                                    applicationJson = new
                                    {
                                        schema = new { @ref = "#/components/schemas/Collections" }
                                    }
                                }
                            }
                        }
                    }
                },
                items = new
                {
                    get = new
                    {
                        summary = "Get features",
                        description = "Retrieve features from a collection",
                        operationId = "getFeatures",
                        tags = new[] { "Data" },
                        parameters = new object[]
                        {
                            new
                            {
                                name = "collectionId",
                                @in = "path",
                                required = true,
                                schema = new { type = "string" },
                                description = "Collection identifier",
                                example = (string?)null
                            },
                            new
                            {
                                name = "limit",
                                @in = "query",
                                required = false,
                                schema = new { type = "integer", @default = 10, minimum = 1, maximum = 10000 },
                                description = "Maximum number of features to return",
                                example = (string?)null
                            },
                            new
                            {
                                name = "bbox",
                                @in = "query",
                                required = false,
                                schema = new { type = "string" },
                                description = "Bounding box: minx,miny,maxx,maxy",
                                example = "-122.5,37.7,-122.3,37.9"
                            },
                            new
                            {
                                name = "offset",
                                @in = "query",
                                required = false,
                                schema = new { type = "integer", minimum = 0 },
                                description = "Number of features to skip",
                                example = (string?)null
                            }
                        },
                        responses = new
                        {
                            _200 = new
                            {
                                description = "Feature collection in GeoJSON format",
                                content = new
                                {
                                    applicationGeoJson = new
                                    {
                                        schema = new { @ref = "#/components/schemas/FeatureCollection" }
                                    }
                                }
                            },
                            _400 = new { description = "Invalid parameters" },
                            _404 = new { description = "Collection not found" }
                        }
                    }
                }
            },
            components = new
            {
                schemas = new
                {
                    Link = new
                    {
                        type = "object",
                        required = new[] { "href", "rel" },
                        properties = new
                        {
                            href = new { type = "string", format = "uri" },
                            rel = new { type = "string", example = "self" },
                            type = new { type = "string", example = "application/json" },
                            title = new { type = "string" }
                        }
                    },
                    FeatureCollection = new
                    {
                        type = "object",
                        required = new[] { "type", "features" },
                        properties = new
                        {
                            type = new { type = "string", @enum = new[] { "FeatureCollection" } },
                            features = new
                            {
                                type = "array",
                                items = new { @ref = "#/components/schemas/Feature" }
                            },
                            links = new
                            {
                                type = "array",
                                items = new { @ref = "#/components/schemas/Link" }
                            },
                            numberMatched = new { type = "integer" },
                            numberReturned = new { type = "integer" }
                        }
                    }
                }
            }
        };

        var swaggerUISetup = @"
<!-- Swagger UI Integration -->
<!DOCTYPE html>
<html>
<head>
    <title>Honua API Documentation</title>
    <link rel=""stylesheet"" href=""https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui.css"">
</head>
<body>
    <div id=""swagger-ui""></div>
    <script src=""https://cdn.jsdelivr.net/npm/swagger-ui-dist@5/swagger-ui-bundle.js""></script>
    <script>
        SwaggerUIBundle({
            url: '/openapi.json',
            dom_id: '#swagger-ui',
            presets: [
                SwaggerUIBundle.presets.apis,
                SwaggerUIBundle.SwaggerUIStandalonePreset
            ]
        });
    </script>
</body>
</html>";

        return JsonSerializer.Serialize(new
        {
            openApiSpecification = openApiSpec,
            swaggerUISetup,
            generation = new
            {
                automatic = "Use Swashbuckle.AspNetCore for ASP.NET Core auto-generation",
                manual = "Customize OpenAPI spec based on metadata configuration",
                validation = "Use openapi-generator validate to check spec",
                hosting = "Serve at /openapi.json and /api-docs for Swagger UI"
            },
            codeGeneration = new[]
            {
                "Client libraries: openapi-generator generate -i openapi.json -g csharp",
                "TypeScript client: openapi-generator generate -i openapi.json -g typescript-fetch",
                "Python client: openapi-generator generate -i openapi.json -g python",
                "Go client: openapi-generator generate -i openapi.json -g go"
            }
        }, CliJsonOptions.Indented);
    }
}
