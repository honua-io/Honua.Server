// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// AI-powered agent that helps users with data ingestion and migration:
/// - Import files (GeoPackage, Shapefile, GeoTIFF, CSV, GeoJSON)
/// - Create layers from existing database tables/views
/// - Migrate Esri/ArcGIS REST services
/// - Configure styling and symbolization
/// - Setup service metadata
/// - Guide through complete data ingestion workflow
/// </summary>
public sealed class DataIngestionAgent
{
    private readonly Kernel _kernel;
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<DataIngestionAgent> _logger;

    public DataIngestionAgent(
        Kernel kernel,
        ILlmProvider llmProvider,
        ILogger<DataIngestionAgent> logger)
    {
        _kernel = kernel ?? throw new ArgumentNullException(nameof(kernel));
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Main entry point - analyzes user's data ingestion request and guides them through the process.
    /// </summary>
    public async Task<AgentStepResult> ProcessAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Processing data ingestion request: {Request}", request);

            // Analyze what the user wants to do
            var analysis = await AnalyzeIngestionRequestAsync(request, context, cancellationToken);

            if (!analysis.Success)
            {
                return new AgentStepResult
                {
                    AgentName = "DataIngestion",
                    Action = "AnalyzeRequest",
                    Success = false,
                    Message = analysis.Message,
                    Duration = DateTime.UtcNow - startTime
                };
            }

            // Generate recommendations based on analysis
            var recommendations = await GenerateRecommendationsAsync(analysis, context, cancellationToken);

            var responseBuilder = new StringBuilder();
            responseBuilder.AppendLine($"## Data Ingestion Plan: {analysis.DataType}");
            responseBuilder.AppendLine();

            responseBuilder.AppendLine("### Analysis:");
            responseBuilder.AppendLine($"- **Source Type**: {analysis.SourceType}");
            responseBuilder.AppendLine($"- **Data Type**: {analysis.DataType}");
            if (!analysis.TableName.IsNullOrEmpty())
                responseBuilder.AppendLine($"- **Table Name**: {analysis.TableName}");
            if (!analysis.GeometryColumn.IsNullOrEmpty())
                responseBuilder.AppendLine($"- **Geometry Column**: {analysis.GeometryColumn}");
            responseBuilder.AppendLine($"- **Estimated Features/Records**: {analysis.EstimatedFeatureCount}");
            if (!analysis.GeometryType.IsNullOrEmpty())
                responseBuilder.AppendLine($"- **Geometry Type**: {analysis.GeometryType}");
            responseBuilder.AppendLine();

            if (analysis.RequiredSteps.Any())
            {
                responseBuilder.AppendLine("### Required Steps:");
                for (int i = 0; i < analysis.RequiredSteps.Count; i++)
                {
                    responseBuilder.AppendLine($"{i + 1}. {analysis.RequiredSteps[i]}");
                }
                responseBuilder.AppendLine();
            }

            if (recommendations.StylingRecommendations.Any())
            {
                responseBuilder.AppendLine("### Styling Recommendations:");
                foreach (var rec in recommendations.StylingRecommendations)
                {
                    responseBuilder.AppendLine($"- {rec}");
                }
                responseBuilder.AppendLine();
            }

            if (recommendations.MetadataRecommendations.Any())
            {
                responseBuilder.AppendLine("### Metadata Recommendations:");
                foreach (var rec in recommendations.MetadataRecommendations)
                {
                    responseBuilder.AppendLine($"- {rec}");
                }
                responseBuilder.AppendLine();
            }

            if (recommendations.PerformanceConsiderations.Any())
            {
                responseBuilder.AppendLine("### Performance Considerations:");
                foreach (var consideration in recommendations.PerformanceConsiderations)
                {
                    responseBuilder.AppendLine($"- {consideration}");
                }
                responseBuilder.AppendLine();
            }

            if (!recommendations.SampleCommand.IsNullOrEmpty())
            {
                responseBuilder.AppendLine("### Example Command:");
                responseBuilder.AppendLine("```bash");
                responseBuilder.AppendLine(recommendations.SampleCommand);
                responseBuilder.AppendLine("```");
                responseBuilder.AppendLine();
            }

            if (recommendations.Warnings.Any())
            {
                responseBuilder.AppendLine("### ⚠️  Warnings:");
                foreach (var warning in recommendations.Warnings)
                {
                    responseBuilder.AppendLine($"- {warning}");
                }
                responseBuilder.AppendLine();
            }

            // Add metadata configuration template
            responseBuilder.AppendLine("### Honua Metadata Configuration:");
            responseBuilder.AppendLine();
            responseBuilder.AppendLine("Complete metadata.json configuration for your data:");
            responseBuilder.AppendLine();
            responseBuilder.AppendLine(GenerateMetadataTemplate(analysis));
            responseBuilder.AppendLine();
            responseBuilder.AppendLine("**Key Configuration Points:**");
            responseBuilder.AppendLine("- **dataSourceId**: Must match between service and dataSource");
            responseBuilder.AppendLine("- **serviceId**: Must match between layer and service");
            responseBuilder.AppendLine($"- **geometryType**: `{analysis.GeometryType ?? "point"}` (point/line/polygon/multipoint/multiline/multipolygon)");
            responseBuilder.AppendLine($"- **geometryField**: `{analysis.GeometryColumn ?? "geom"}` (your geometry column name)");
            if (!analysis.TableName.IsNullOrEmpty())
            {
                responseBuilder.AppendLine($"- **query.table**: `{analysis.TableName}` (your PostGIS table name)");
            }
            responseBuilder.AppendLine("- **OGC Protocols**: Enable wfs/wms/wmts/ogcapi as needed");
            responseBuilder.AppendLine("- **extent.spatial.bbox**: Update with actual data bounds [minX, minY, maxX, maxY]");
            responseBuilder.AppendLine("- **fields**: Define all table columns with types (integer/string/float/boolean/date)");
            responseBuilder.AppendLine();

            responseBuilder.AppendLine("### Next Steps:");
            responseBuilder.AppendLine("1. Review the analysis and recommendations above");
            responseBuilder.AppendLine("2. Prepare your source data (file path or Geoservices REST a.k.a. Esri REST URL)");
            responseBuilder.AppendLine("3. Run the ingestion command (or I can help generate it)");
            responseBuilder.AppendLine("4. Configure styling and metadata");
            responseBuilder.AppendLine("5. Test the published service");

            return new AgentStepResult
            {
                AgentName = "DataIngestion",
                Action = "GenerateIngestionPlan",
                Success = true,
                Message = responseBuilder.ToString(),
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing data ingestion request");
            return new AgentStepResult
            {
                AgentName = "DataIngestion",
                Action = "ProcessRequest",
                Success = false,
                Message = $"Error processing request: {ex.Message}",
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    /// <summary>
    /// Analyzes the user's request to determine data source, type, and requirements.
    /// </summary>
    private async Task<IngestionAnalysis> AnalyzeIngestionRequestAsync(
        string request,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Analyze this data ingestion request and extract key information:

User Request: {request}

Determine:
1. Source type (file, database_table, esri_rest_service, url)
2. Data type (vector, raster, tabular, non_spatial)
3. File format if applicable (gpkg, shp, geotiff, csv, geojson, etc)
4. Database table name if creating layer from existing table
5. Geometry column name if database table with geometry
6. Geometry type if vector (point, line, polygon, multi)
7. Estimated feature count (small <1000, medium <100k, large >100k)
8. Required steps for ingestion
9. Any potential challenges or considerations

Respond in JSON format:
{{
  ""sourceType"": ""database_table"",
  ""dataType"": ""vector"",
  ""tableName"": ""parcels"",
  ""geometryColumn"": ""geom"",
  ""geometryType"": ""polygon"",
  ""estimatedFeatureCount"": ""medium"",
  ""requiredSteps"": [
    ""Verify table exists in PostGIS database"",
    ""Create service configuration for table"",
    ""Create layer metadata pointing to table"",
    ""Configure styling and symbology"",
    ""Test published layer""
  ],
  ""challenges"": [""Ensure geometry column has spatial index""]
}}";

        try
        {
            var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 1000,
            Temperature = 0.3
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            _logger.LogError("LLM request failed");
            return new IngestionAnalysis
            {
                Success = false,
                Message = "Failed to analyze request",
                SourceType = "Unknown",
                DataType = "Unknown"
            };
        }

            // Extract JSON from response (handle markdown code blocks)
            var jsonStart = response.Content.IndexOf('{');
            var jsonEnd = response.Content.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var data = JsonSerializer.Deserialize<JsonElement>(jsonStr);

                return new IngestionAnalysis
                {
                    Success = true,
                    SourceType = data.GetProperty("sourceType").GetString() ?? "unknown",
                    DataType = data.GetProperty("dataType").GetString() ?? "vector",
                    FileFormat = data.TryGetProperty("fileFormat", out var fmt) ? fmt.GetString() : null,
                    TableName = data.TryGetProperty("tableName", out var tbl) ? tbl.GetString() : null,
                    GeometryColumn = data.TryGetProperty("geometryColumn", out var geomCol) ? geomCol.GetString() : null,
                    GeometryType = data.TryGetProperty("geometryType", out var geom) ? geom.GetString() : null,
                    EstimatedFeatureCount = data.GetProperty("estimatedFeatureCount").GetString() ?? "unknown",
                    RequiredSteps = data.TryGetProperty("requiredSteps", out var steps)
                        ? steps.EnumerateArray().Select(s => s.GetString() ?? "").Where(s => !s.IsNullOrEmpty()).ToList()
                        : new List<string>(),
                    Challenges = data.TryGetProperty("challenges", out var challenges)
                        ? challenges.EnumerateArray().Select(c => c.GetString() ?? "").Where(c => !c.IsNullOrEmpty()).ToList()
                        : new List<string>()
                };
            }

            return new IngestionAnalysis
            {
                Success = false,
                Message = "Could not parse LLM response"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing ingestion request");
            return new IngestionAnalysis
            {
                Success = false,
                Message = $"Analysis error: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Generates a complete metadata configuration based on the ingestion analysis.
    /// </summary>
    private string GenerateMetadataTemplate(IngestionAnalysis analysis)
    {
        var template = new StringBuilder();
        template.AppendLine("```json");
        template.AppendLine("{");
        template.AppendLine("  // =============================================================================");
        template.AppendLine("  // DATA SOURCES: Database connections for your spatial data");
        template.AppendLine("  // =============================================================================");
        template.AppendLine("  \"dataSources\": [");
        template.AppendLine("    {");
        template.AppendLine($"      \"id\": \"{analysis.TableName ?? "my_datasource"}\",  // Unique datasource identifier (referenced by services)");
        template.AppendLine("      \"provider\": \"Npgsql\",  // PostGIS/PostgreSQL provider");
        template.AppendLine("      \"connectionString\": \"Host=localhost;Database=honua;Username=postgres;Password=***\"  // Update with your DB credentials");
        template.AppendLine("    }");
        template.AppendLine("  ],");
        template.AppendLine();
        template.AppendLine("  // =============================================================================");
        template.AppendLine("  // SERVICES: Logical grouping of layers with protocol configuration");
        template.AppendLine("  // =============================================================================");
        template.AppendLine("  \"services\": [");
        template.AppendLine("    {");
        template.AppendLine($"      \"id\": \"{analysis.TableName ?? "my_service"}\",  // Unique service identifier (referenced by layers)");
        template.AppendLine($"      \"title\": \"{analysis.TableName ?? "My GIS Service"}\",  // Human-readable service name");
        template.AppendLine("      \"folderId\": \"root\",  // Parent folder (use \"root\" for top-level)");
        template.AppendLine("      \"serviceType\": \"FeatureServer\",  // FeatureServer (vector) or MapServer (raster/cached)");
        template.AppendLine($"      \"dataSourceId\": \"{analysis.TableName ?? "my_datasource"}\",  // MUST MATCH a dataSource.id above");
        template.AppendLine("      \"enabled\": true,  // Service availability toggle");
        template.AppendLine($"      \"description\": \"{analysis.DataType} data service\",  // Service description");
        template.AppendLine("      \"keywords\": [\"gis\", \"spatial\"],  // Keywords for search/discovery");
        template.AppendLine();
        template.AppendLine("      // OGC Protocol Configuration - Enable standards-based access");
        template.AppendLine("      \"ogc\": {");
        template.AppendLine("        \"wfs\": { \"enabled\": true },      // OGC Web Feature Service (vector query/edit)");
        template.AppendLine("        \"wms\": { \"enabled\": true },      // OGC Web Map Service (rendered images)");
        template.AppendLine("        \"wmts\": { \"enabled\": false },    // OGC Web Map Tile Service (tile cache)");
        template.AppendLine("        \"ogcapi\": { \"enabled\": true }   // Modern OGC API - Features (RESTful JSON)");
        template.AppendLine("      }");
        template.AppendLine("    }");
        template.AppendLine("  ],");
        template.AppendLine();
        template.AppendLine("  // =============================================================================");
        template.AppendLine("  // LAYERS: Individual datasets with geometry, fields, and query configuration");
        template.AppendLine("  // =============================================================================");
        template.AppendLine("  \"layers\": [");
        template.AppendLine("    {");
        template.AppendLine($"      \"id\": \"{analysis.TableName ?? "my_layer"}\",  // Unique layer identifier");
        template.AppendLine($"      \"serviceId\": \"{analysis.TableName ?? "my_service"}\",  // MUST MATCH a service.id above");
        template.AppendLine($"      \"title\": \"{analysis.TableName ?? "My Layer"}\",  // Human-readable layer name");
        template.AppendLine($"      \"description\": \"{analysis.DataType} layer\",  // Layer description");
        template.AppendLine();
        template.AppendLine("      // Geometry Configuration");
        template.AppendLine($"      \"geometryType\": \"{analysis.GeometryType ?? "point"}\",  // point, line, polygon, multipoint, multiline, multipolygon");
        template.AppendLine("      \"idField\": \"id\",  // Primary key field name (must be unique, non-null)");
        template.AppendLine($"      \"geometryField\": \"{analysis.GeometryColumn ?? "geom"}\",  // PostGIS geometry column name");
        template.AppendLine("      \"displayField\": \"name\",  // Field to use for feature labels/display");
        template.AppendLine();
        template.AppendLine("      // Coordinate Reference Systems - Add all supported projections");
        template.AppendLine("      \"crs\": [\"EPSG:4326\", \"EPSG:3857\"],  // WGS84 (lat/lon) and Web Mercator");
        template.AppendLine();
        template.AppendLine("      // Spatial Extent - Update with actual data bounds for better performance");
        template.AppendLine("      \"extent\": {");
        template.AppendLine("        \"spatial\": {");
        template.AppendLine("          \"bbox\": [[-180, -90, 180, 90]],  // [minX, minY, maxX, maxY] - Get from ST_Extent(geom)");
        template.AppendLine("          \"crs\": \"EPSG:4326\"");
        template.AppendLine("        }");
        template.AppendLine("      },");
        template.AppendLine();
        template.AppendLine("      // Field Definitions - Must match your database table schema exactly");
        template.AppendLine("      \"fields\": [");
        template.AppendLine("        { \"name\": \"id\", \"type\": \"integer\", \"nullable\": false },  // Types: integer, string, float, boolean, date");
        template.AppendLine("        { \"name\": \"name\", \"type\": \"string\", \"nullable\": true, \"length\": 255 }  // Add all table columns here");
        template.AppendLine("      ],");
        template.AppendLine();
        template.AppendLine("      // Query Configuration - Controls data access behavior");
        template.AppendLine("      \"query\": {");
        template.AppendLine($"        \"table\": \"{analysis.TableName ?? "my_table"}\",  // PostGIS table or view name");
        template.AppendLine("        \"maxRecordCount\": 5000  // Max features returned per request (prevent overload)");
        template.AppendLine("      }");
        template.AppendLine("    }");
        template.AppendLine("  ]");
        template.AppendLine("}");
        template.AppendLine("```");
        return template.ToString();
    }

    /// <summary>
    /// Generates styling, metadata, and performance recommendations based on data analysis.
    /// </summary>
    private async Task<IngestionRecommendations> GenerateRecommendationsAsync(
        IngestionAnalysis analysis,
        AgentExecutionContext context,
        CancellationToken cancellationToken)
    {
        var prompt = $@"Generate recommendations for ingesting {analysis.DataType} data with Honua metadata schema knowledge:

HONUA METADATA SCHEMA KNOWLEDGE:
- Services require: id, folderId, serviceType (FeatureServer/MapServer), dataSourceId
- Layers require: id, serviceId, geometryType (point/line/polygon/multi*), idField, geometryField
- DataSources require: id, provider (Npgsql for PostGIS), connectionString
- Services can enable OGC protocols: wfs, wms, wmts, ogcapi
- Layers need extent (spatial bbox), crs array, fields array
- Optional: catalog metadata (keywords, links, contact, license)

Data Type: {analysis.DataType}
Source Type: {analysis.SourceType}
Geometry Type: {analysis.GeometryType}
Format: {analysis.FileFormat}
Scale: {analysis.EstimatedFeatureCount} features

Provide Honua-specific recommendations:
1. Styling recommendations (OGC SLD, Mapbox GL styles, simple symbology)
2. Metadata recommendations with Honua schema fields:
   - Service metadata: title, description, keywords, enabled, ogc protocol settings
   - Layer metadata: title, description, displayField, crs array, extent.spatial.bbox
   - Catalog metadata: keywords, links (rel, href, type), contact info, license
3. Performance considerations:
   - PostGIS spatial indexing (CREATE INDEX ON table USING GIST(geom))
   - Layer query settings: maxRecordCount, spatial filtering
   - Caching strategies: storage.cache settings
   - Simplification for large/complex geometries
4. Field mapping considerations:
   - idField: must be unique, non-null integer or string
   - displayField: user-friendly label field
   - fields array: accurate type mapping (integer/string/float/boolean/date)
5. Sample metadata snippet or command
6. Warnings or important notes

Respond in JSON format:
{{
  ""stylingRecommendations"": [
    ""Use simple fill: #3388ff with 0.6 opacity for polygons"",
    ""Add labels using displayField"",
    ""Consider data-driven styling if categorical attributes exist""
  ],
  ""metadataRecommendations"": [
    ""Set service.title to descriptive name"",
    ""Add keywords for discoverability: ['parcels', 'cadastral', 'boundaries']"",
    ""Configure extent.spatial.bbox from ST_Extent(geom)"",
    ""Enable ogcapi for modern OGC API Features access"",
    ""Add contact info with organization and email"",
    ""Set displayField to most user-friendly column (e.g., 'name' or 'parcel_id')""
  ],
  ""performanceConsiderations"": [
    ""Create spatial index: CREATE INDEX idx_parcels_geom ON parcels USING GIST(geom)"",
    ""Set maxRecordCount: 5000 for large datasets"",
    ""Enable storage.cache.enabled for frequently accessed data"",
    ""For 100k+ features, consider ST_Simplify for WMS rendering""
  ],
  ""sampleCommand"": ""UPDATE metadata.json with generated configuration, then restart Honua service"",
  ""warnings"": [
    ""Ensure geometry column has SRID set: SELECT UpdateGeometrySRID('table', 'geom', 4326)"",
    ""Large datasets (>1M features) may require partitioning""
  ]
}}";

        try
        {
            var llmRequest = new LlmRequest
        {
            UserPrompt = prompt,
            MaxTokens = 1500,
            Temperature = 0.4
        };

        var response = await _llmProvider.CompleteAsync(llmRequest, cancellationToken);

        if (!response.Success)
        {
            _logger.LogError("LLM request failed");
            return new IngestionRecommendations
            {
                StylingRecommendations = new List<string>(),
                MetadataRecommendations = new List<string>(),
                PerformanceConsiderations = new List<string>()
            };
        }

            var jsonStart = response.Content.IndexOf('{');
            var jsonEnd = response.Content.LastIndexOf('}');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonStr = response.Content.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var data = JsonSerializer.Deserialize<JsonElement>(jsonStr);

                return new IngestionRecommendations
                {
                    StylingRecommendations = data.TryGetProperty("stylingRecommendations", out var styling)
                        ? styling.EnumerateArray().Select(s => s.GetString() ?? "").Where(s => !s.IsNullOrEmpty()).ToList()
                        : new List<string>(),
                    MetadataRecommendations = data.TryGetProperty("metadataRecommendations", out var metadata)
                        ? metadata.EnumerateArray().Select(m => m.GetString() ?? "").Where(m => !m.IsNullOrEmpty()).ToList()
                        : new List<string>(),
                    PerformanceConsiderations = data.TryGetProperty("performanceConsiderations", out var perf)
                        ? perf.EnumerateArray().Select(p => p.GetString() ?? "").Where(p => !p.IsNullOrEmpty()).ToList()
                        : new List<string>(),
                    SampleCommand = data.TryGetProperty("sampleCommand", out var cmd) ? cmd.GetString() : null,
                    Warnings = data.TryGetProperty("warnings", out var warnings)
                        ? warnings.EnumerateArray().Select(w => w.GetString() ?? "").Where(w => !w.IsNullOrEmpty()).ToList()
                        : new List<string>()
                };
            }

            return new IngestionRecommendations();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating recommendations");
            return new IngestionRecommendations();
        }
    }
}

// Supporting types

public sealed class IngestionAnalysis
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string SourceType { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public string? FileFormat { get; init; }
    public string? TableName { get; init; }
    public string? GeometryColumn { get; init; }
    public string? GeometryType { get; init; }
    public string EstimatedFeatureCount { get; init; } = string.Empty;
    public List<string> RequiredSteps { get; init; } = new();
    public List<string> Challenges { get; init; } = new();
}

public sealed class IngestionRecommendations
{
    public List<string> StylingRecommendations { get; init; } = new();
    public List<string> MetadataRecommendations { get; init; } = new();
    public List<string> PerformanceConsiderations { get; init; } = new();
    public string? SampleCommand { get; init; }
    public List<string> Warnings { get; init; } = new();
}
