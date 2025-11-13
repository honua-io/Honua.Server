// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Honua.MapSDK.Styling;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Query;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.API;

/// <summary>
/// API for automatic cartographic style generation
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class StyleGenerationController : ControllerBase
{
    private readonly StyleGeneratorService styleGenerator = new();
    private readonly IMetadataProvider metadataProvider;
    // TODO: IQueryService is not defined in the codebase. Re-enable when available.
    // private readonly IQueryService queryService;

    public StyleGenerationController(IMetadataProvider metadataProvider/*, IQueryService queryService*/)
    {
        this.metadataProvider = metadataProvider;
        // this.queryService = queryService;
    }

    /// <summary>
    /// Generate a style automatically based on data characteristics
    /// </summary>
    /// <param name="request">Style generation request</param>
    /// <returns>Generated style with recommendations</returns>
    [HttpPost("generate")]
    [ProducesResponseType(typeof(GeneratedStyle), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<GeneratedStyle> GenerateStyle([FromBody] StyleGenerationApiRequest request)
    {
        try
        {
            var generationRequest = new StyleGenerationRequest
            {
                StyleId = request.StyleId,
                Title = request.Title,
                GeometryType = request.GeometryType,
                LayerId = request.LayerId,
                SourceId = request.SourceId,
                FieldName = request.FieldName,
                FieldValues = request.FieldValues,
                Coordinates = request.Coordinates?.Select(c => (c[0], c[1])),
                ColorPalette = request.ColorPalette,
                BaseColor = request.BaseColor,
                Opacity = request.Opacity,
                ClassCount = request.ClassCount,
                ClassificationMethod = request.ClassificationMethod
            };

            var result = this.styleGenerator.GenerateStyle(generationRequest);
            return this.Ok(result);
        }
        catch (Exception ex)
        {
            return this.BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Generate a style from layer data
    /// </summary>
    /// <param name="request">Layer style generation request</param>
    /// <returns>Generated style based on layer data</returns>
    /// <remarks>
    /// TODO: Re-enable when IQueryService is available.
    /// This endpoint depends on IQueryService for data sampling.
    /// </remarks>
    /*
    [HttpPost("generate-from-layer")]
    [ProducesResponseType(typeof(GeneratedStyle), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<GeneratedStyle>> GenerateFromLayer([FromBody] LayerStyleGenerationRequest request)
    {
        try
        {
            var metadata = await this.metadataProvider.GetSnapshotAsync();
            if (!metadata.TryGetLayer(request.ServiceId, request.LayerId, out var layer))
            {
                return this.NotFound(new { error = $"Layer {request.LayerId} not found" });
            }

            // Sample data from the layer
            var sampleSize = request.SampleSize ?? 1000;
            var queryRequest = new QueryRequest
            {
                ServiceId = request.ServiceId,
                LayerId = request.LayerId,
                Limit = sampleSize,
                OutputCrs = "EPSG:4326"
            };

            var queryResult = await this.queryService.ExecuteQueryAsync(queryRequest, default);

            // Extract field values and coordinates
            List<object?> fieldValues = new();
            List<(double, double)> coordinates = new();

            if (queryResult.Features != null)
            {
                foreach (var feature in queryResult.Features)
                {
                    // Extract field value
                    if (!string.IsNullOrEmpty(request.FieldName) &&
                        feature.Properties.TryGetValue(request.FieldName, out var value))
                    {
                        fieldValues.Add(value);
                    }

                    // Extract coordinates for geometry analysis
                    if (feature.Geometry != null && layer.GeometryType.ToLowerInvariant() == "point")
                    {
                        var coords = ExtractCoordinates(feature.Geometry);
                        if (coords.HasValue)
                        {
                            coordinates.Add(coords.Value);
                        }
                    }
                }
            }

            var generationRequest = new StyleGenerationRequest
            {
                StyleId = request.StyleId,
                Title = request.Title ?? $"Auto Style - {layer.Title}",
                GeometryType = layer.GeometryType,
                LayerId = request.LayerId,
                SourceId = request.ServiceId,
                FieldName = request.FieldName,
                FieldValues = fieldValues.Count > 0 ? fieldValues : null,
                Coordinates = coordinates.Count > 0 ? coordinates : null,
                ColorPalette = request.ColorPalette,
                BaseColor = request.BaseColor,
                Opacity = request.Opacity,
                ClassCount = request.ClassCount,
                ClassificationMethod = request.ClassificationMethod
            };

            var result = this.styleGenerator.GenerateStyle(generationRequest);
            return this.Ok(result);
        }
        catch (Exception ex)
        {
            return this.BadRequest(new { error = ex.Message });
        }
    }
    */

    /// <summary>
    /// Get available color palettes
    /// </summary>
    /// <returns>List of available color palettes</returns>
    [HttpGet("palettes")]
    [ProducesResponseType(typeof(IReadOnlyList<PaletteInfo>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<PaletteInfo>> GetPalettes()
    {
        var palettes = CartographicPalettes.GetPaletteNames()
            .Select(name => new PaletteInfo
            {
                Name = name,
                Colors7 = CartographicPalettes.GetPalette(name, 7),
                Colors5 = CartographicPalettes.GetPalette(name, 5)
            })
            .ToList();

        return this.Ok(palettes);
    }

    /// <summary>
    /// Get a specific palette with custom class count
    /// </summary>
    /// <param name="name">Palette name</param>
    /// <param name="classes">Number of classes (3-12)</param>
    /// <returns>Color array</returns>
    [HttpGet("palettes/{name}")]
    [ProducesResponseType(typeof(string[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<string[]> GetPalette(string name, [FromQuery] int classes = 7)
    {
        if (classes < 3 || classes > 12)
        {
            return this.BadRequest(new { error = "Class count must be between 3 and 12" });
        }

        try
        {
            var colors = CartographicPalettes.GetPalette(name, classes);
            return this.Ok(colors);
        }
        catch (Exception ex)
        {
            return this.BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get available style templates
    /// </summary>
    /// <param name="geometryType">Filter by geometry type (optional)</param>
    /// <param name="useCase">Filter by use case (optional)</param>
    /// <returns>List of available templates</returns>
    [HttpGet("templates")]
    [ProducesResponseType(typeof(IReadOnlyList<StyleTemplateInfo>), StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyList<StyleTemplateInfo>> GetTemplates(
        [FromQuery] string? geometryType = null,
        [FromQuery] string? useCase = null)
    {
        IEnumerable<StyleTemplate> templates;

        if (!string.IsNullOrEmpty(geometryType))
        {
            templates = StyleTemplateLibrary.GetTemplatesByGeometry(geometryType);
        }
        else if (!string.IsNullOrEmpty(useCase))
        {
            templates = StyleTemplateLibrary.GetTemplatesByUseCase(useCase);
        }
        else
        {
            templates = StyleTemplateLibrary.GetTemplateNames()
                .Select(name => StyleTemplateLibrary.GetTemplate(name))
                .Where(t => t != null)
                .Cast<StyleTemplate>();
        }

        var result = templates.Select(t => new StyleTemplateInfo
        {
            Name = t.Name,
            DisplayName = t.DisplayName,
            Description = t.Description,
            UseCase = t.UseCase,
            SupportedGeometries = t.SupportedGeometries,
            ThumbnailUrl = t.ThumbnailUrl
        }).ToList();

        return this.Ok(result);
    }

    /// <summary>
    /// Apply a style template
    /// </summary>
    /// <param name="templateName">Template name</param>
    /// <param name="options">Template options</param>
    /// <returns>Style definition from template</returns>
    [HttpPost("templates/{templateName}/apply")]
    [ProducesResponseType(typeof(StyleDefinition), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<StyleDefinition> ApplyTemplate(
        string templateName,
        [FromBody] StyleTemplateOptions options)
    {
        try
        {
            var template = StyleTemplateLibrary.GetTemplate(templateName);
            if (template == null)
            {
                return this.NotFound(new { error = $"Template '{templateName}' not found" });
            }

            var style = StyleTemplateLibrary.ApplyTemplate(templateName, options);
            return this.Ok(style);
        }
        catch (Exception ex)
        {
            return this.BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Analyze field data without generating a style
    /// </summary>
    /// <param name="request">Field analysis request</param>
    /// <returns>Field analysis results</returns>
    [HttpPost("analyze-field")]
    [ProducesResponseType(typeof(FieldAnalysisResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<FieldAnalysisResult> AnalyzeField([FromBody] FieldAnalysisRequest request)
    {
        try
        {
            var analyzer = new DataAnalyzer();
            var result = analyzer.AnalyzeField(request.Values, request.FieldName);
            return this.Ok(result);
        }
        catch (Exception ex)
        {
            return this.BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>
    /// Get recommended classification method for data
    /// </summary>
    /// <param name="values">Numeric values</param>
    /// <returns>Recommended classification method</returns>
    [HttpPost("recommend-classification")]
    [ProducesResponseType(typeof(ClassificationRecommendation), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public ActionResult<ClassificationRecommendation> RecommendClassification([FromBody] double[] values)
    {
        try
        {
            var sorted = values.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).OrderBy(v => v).ToList();
            var method = ClassificationStrategy.GetRecommendedMethod(sorted);
            var optimalClasses = ClassificationStrategy.FindOptimalClassCount(sorted, method);

            var breaks = ClassificationStrategy.Classify(sorted, optimalClasses, method);
            var gvf = ClassificationStrategy.CalculateGVF(sorted, breaks);

            return this.Ok(new ClassificationRecommendation
            {
                Method = method,
                OptimalClassCount = optimalClasses,
                Breaks = breaks,
                GoodnessOfFit = gvf
            });
        }
        catch (Exception ex)
        {
            return this.BadRequest(new { error = ex.Message });
        }
    }

    private (double x, double y)? ExtractCoordinates(object geometry)
    {
        // Simple coordinate extraction - would need to be more robust in production
        try
        {
            if (geometry is System.Text.Json.JsonElement jsonElement)
            {
                if (jsonElement.TryGetProperty("coordinates", out var coords) &&
                    coords.ValueKind == System.Text.Json.JsonValueKind.Array)
                {
                    var coordArray = coords.EnumerateArray().ToList();
                    if (coordArray.Count >= 2)
                    {
                        return (coordArray[0].GetDouble(), coordArray[1].GetDouble());
                    }
                }
            }
        }
        catch
        {
            // Ignore parsing errors
        }

        return null;
    }
}

#region Request/Response Models

public class StyleGenerationApiRequest
{
    public string? StyleId { get; set; }
    public string? Title { get; set; }

    [Required]
    public required string GeometryType { get; set; }

    public string? LayerId { get; set; }
    public string? SourceId { get; set; }
    public string? FieldName { get; set; }
    public List<object?>? FieldValues { get; set; }
    public List<double[]>? Coordinates { get; set; }
    public string? ColorPalette { get; set; }
    public string? BaseColor { get; set; }
    public double? Opacity { get; set; }
    public int? ClassCount { get; set; }
    public ClassificationMethod? ClassificationMethod { get; set; }
}

public class LayerStyleGenerationRequest
{
    [Required]
    public required string ServiceId { get; set; }

    [Required]
    public required string LayerId { get; set; }

    public string? StyleId { get; set; }
    public string? Title { get; set; }
    public string? FieldName { get; set; }
    public int? SampleSize { get; set; }
    public string? ColorPalette { get; set; }
    public string? BaseColor { get; set; }
    public double? Opacity { get; set; }
    public int? ClassCount { get; set; }
    public ClassificationMethod? ClassificationMethod { get; set; }
}

public class FieldAnalysisRequest
{
    [Required]
    public required string FieldName { get; set; }

    [Required]
    public required IEnumerable<object?> Values { get; set; }
}

public class PaletteInfo
{
    public required string Name { get; set; }
    public required string[] Colors7 { get; set; }
    public required string[] Colors5 { get; set; }
}

public class StyleTemplateInfo
{
    public required string Name { get; set; }
    public required string DisplayName { get; set; }
    public required string Description { get; set; }
    public required string UseCase { get; set; }
    public required string[] SupportedGeometries { get; set; }
    public string? ThumbnailUrl { get; set; }
}

public class ClassificationRecommendation
{
    public ClassificationMethod Method { get; set; }
    public int OptimalClassCount { get; set; }
    public required double[] Breaks { get; set; }
    public double GoodnessOfFit { get; set; }
}

#endregion
