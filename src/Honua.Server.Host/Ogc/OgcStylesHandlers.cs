// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.GeoservicesREST;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Host.Ogc;

internal static class OgcStylesHandlers
{
    /// <summary>
    /// Shared JsonSerializerOptions for style deserialization to avoid repeated allocations.
    /// </summary>
    private static readonly JsonSerializerOptions StyleJsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        MaxDepth = 64 // Security: Prevent deeply nested JSON DoS attacks
    };

    /// <summary>
    /// Reads and deserializes a style definition from the request body.
    /// Returns the style and validation result, or an error IResult if parsing fails.
    /// </summary>
    private static async Task<(StyleDefinition? Style, ValidationResult? Validation, IResult? Error)> TryReadStyleAsync(
        HttpRequest request,
        [FromServices] ILogger logger,
        CancellationToken cancellationToken)
    {
        using var document = await OgcSharedHandlers.ParseJsonDocumentAsync(request, cancellationToken).ConfigureAwait(false);
        if (document is null)
        {
            return (null, null, OgcSharedHandlers.CreateValidationProblem("Request body must contain a valid style definition.", "body"));
        }

        StyleDefinition? style;
        try
        {
            style = JsonSerializer.Deserialize<StyleDefinition>(document.RootElement.GetRawText(), StyleJsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Failed to deserialize style definition");
            return (null, null, OgcSharedHandlers.CreateValidationProblem($"Invalid style definition: {ex.Message}", "body"));
        }

        if (style is null)
        {
            return (null, null, OgcSharedHandlers.CreateValidationProblem("Style definition is required.", "body"));
        }

        // Validate the style
        var validation = StyleValidator.ValidateStyleDefinition(style);
        if (!validation.IsValid)
        {
            var error = Results.BadRequest(new
            {
                error = "Style validation failed",
                errors = validation.Errors,
                warnings = validation.Warnings
            });
            return (style, validation, error);
        }

        return (style, validation, null);
    }

    /// <summary>
    /// Creates a new style (POST /ogc/styles)
    /// </summary>
    public static async Task<IResult> CreateStyle(
        HttpRequest request,
        [FromServices] IStyleRepository styleRepository,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IApiMetrics apiMetrics,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);
        Guard.NotNull(styleRepository);
        Guard.NotNull(metadataRegistry);

        var (style, validation, error) = await TryReadStyleAsync(request, logger, cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return error;
        }

        // Check if style already exists
        var exists = await styleRepository.ExistsAsync(style.Id, cancellationToken).ConfigureAwait(false);
        if (exists)
        {
            return Results.Conflict(new { error = $"Style '{style.Id}' already exists. Use PUT to update it." });
        }

        // Create the style
        var userName = UserIdentityHelper.GetUserIdentifier(request.HttpContext.User);
        try
        {
            var created = await styleRepository.CreateAsync(style, userName, cancellationToken).ConfigureAwait(false);

            // Reload metadata to pick up the new style
            await metadataRegistry.ReloadAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Created style {StyleId} by {User}", created.Id, userName);

            var location = $"/ogc/styles/{created.Id}";
            return Results.Created(location, new
            {
                id = created.Id,
                title = created.Title,
                format = created.Format,
                geometryType = created.GeometryType,
                renderer = created.Renderer,
                warnings = validation.Warnings.Count > 0 ? validation.Warnings : null,
                links = new[]
                {
                    OgcSharedHandlers.BuildLink(request, $"/ogc/styles/{created.Id}", "self", "application/json", created.Title ?? created.Id)
                }
            });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create style {StyleId}", style.Id);
            return Results.Problem("Failed to create style", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Updates or creates a style (PUT /ogc/styles/{styleId})
    /// </summary>
    public static async Task<IResult> UpdateStyle(
        string styleId,
        HttpRequest request,
        [FromServices] IStyleRepository styleRepository,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IApiMetrics apiMetrics,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(styleId);
        Guard.NotNull(request);
        Guard.NotNull(styleRepository);
        Guard.NotNull(metadataRegistry);

        var (style, validation, error) = await TryReadStyleAsync(request, logger, cancellationToken).ConfigureAwait(false);
        if (error is not null)
        {
            return error;
        }

        // Ensure the ID matches the URL
        if (!string.Equals(style!.Id, styleId, StringComparison.OrdinalIgnoreCase))
        {
            style = style with { Id = styleId };
        }

        // Update or create the style
        var userName = UserIdentityHelper.GetUserIdentifier(request.HttpContext.User);
        var exists = await styleRepository.ExistsAsync(styleId, cancellationToken).ConfigureAwait(false);

        try
        {
            var updated = await styleRepository.UpdateAsync(styleId, style, userName, cancellationToken).ConfigureAwait(false);

            // Reload metadata to pick up the changes
            await metadataRegistry.ReloadAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Updated style {StyleId} by {User}", updated.Id, userName);

            var statusCode = exists ? 200 : 201;
            var result = new
            {
                id = updated.Id,
                title = updated.Title,
                format = updated.Format,
                geometryType = updated.GeometryType,
                renderer = updated.Renderer,
                warnings = validation.Warnings.Count > 0 ? validation.Warnings : null,
                links = new[]
                {
                    OgcSharedHandlers.BuildLink(request, $"/ogc/styles/{updated.Id}", "self", "application/json", updated.Title ?? updated.Id),
                    OgcSharedHandlers.BuildLink(request, $"/ogc/styles/{updated.Id}/history", "version-history", "application/json", "Version History")
                }
            };

            return statusCode == 201 ? Results.Created($"/ogc/styles/{updated.Id}", result) : Results.Ok(result);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update style {StyleId}", styleId);
            return Results.Problem("Failed to update style", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Deletes a style (DELETE /ogc/styles/{styleId})
    /// </summary>
    public static async Task<IResult> DeleteStyle(
        string styleId,
        HttpRequest request,
        [FromServices] IStyleRepository styleRepository,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IApiMetrics apiMetrics,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(styleId);
        Guard.NotNull(styleRepository);
        Guard.NotNull(metadataRegistry);

        var userName = UserIdentityHelper.GetUserIdentifier(request.HttpContext.User);

        try
        {
            var deleted = await styleRepository.DeleteAsync(styleId, userName, cancellationToken).ConfigureAwait(false);

            if (!deleted)
            {
                return GeoservicesRESTErrorHelper.NotFound("Style", styleId);
            }

            // Reload metadata to remove the deleted style
            await metadataRegistry.ReloadAsync(cancellationToken).ConfigureAwait(false);

            logger.LogInformation("Deleted style {StyleId} by {User}", styleId, userName);

            return Results.NoContent();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete style {StyleId}", styleId);
            return Results.Problem("Failed to delete style", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets the version history for a style (GET /ogc/styles/{styleId}/history)
    /// </summary>
    public static async Task<IResult> GetStyleHistory(
        string styleId,
        HttpRequest request,
        [FromServices] IStyleRepository styleRepository,
        [FromServices] IApiMetrics apiMetrics,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(styleId);
        Guard.NotNull(styleRepository);

        try
        {
            var history = await styleRepository.GetVersionHistoryAsync(styleId, cancellationToken).ConfigureAwait(false);

            if (history.Count == 0)
            {
                return GeoservicesRESTErrorHelper.NotFoundWithMessage($"No history found for style '{styleId}'.");
            }


            var versions = history.Select(v => new
            {
                version = v.Version,
                createdAt = v.CreatedAt,
                createdBy = v.CreatedBy,
                changeDescription = v.ChangeDescription,
                links = new[]
                {
                    OgcSharedHandlers.BuildLink(request, $"/ogc/styles/{styleId}/versions/{v.Version}", "version", "application/json", $"Version {v.Version}")
                }
            }).ToArray();

            return Results.Ok(new
            {
                styleId,
                totalVersions = versions.Length,
                versions,
                links = new[]
                {
                    OgcSharedHandlers.BuildLink(request, $"/ogc/styles/{styleId}/history", "self", "application/json", "Version History"),
                    OgcSharedHandlers.BuildLink(request, $"/ogc/styles/{styleId}", "current", "application/json", "Current Version")
                }
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to retrieve history: {ex.Message}", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Gets a specific version of a style (GET /ogc/styles/{styleId}/versions/{version})
    /// </summary>
    public static async Task<IResult> GetStyleVersion(
        string styleId,
        int version,
        HttpRequest request,
        [FromServices] IStyleRepository styleRepository,
        [FromServices] IApiMetrics apiMetrics,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(styleId);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(version);
        Guard.NotNull(styleRepository);

        try
        {
            var style = await styleRepository.GetVersionAsync(styleId, version, cancellationToken).ConfigureAwait(false);

            if (style is null)
            {
                return GeoservicesRESTErrorHelper.NotFoundWithMessage($"Version {version} of style '{styleId}' was not found.");
            }


            var format = request.Query.TryGetValue("f", out var formatValues)
                ? formatValues.ToString()
                : null;

            if (string.Equals(format, "sld", StringComparison.OrdinalIgnoreCase))
            {
                var fileName = $"{FileNameHelper.SanitizeSegment(style.Id)}_v{version}.sld";
                return OgcStyleResponseBuilder.CreateSldFileResponse(style, style.Title ?? style.Id, style.GeometryType, fileName);
            }

            return Results.Ok(new
            {
                styleId,
                version,
                style,
                links = new[]
                {
                    OgcSharedHandlers.BuildLink(request, $"/ogc/styles/{styleId}/versions/{version}", "self", "application/json", $"Version {version}"),
                    OgcSharedHandlers.BuildLink(request, $"/ogc/styles/{styleId}", "current", "application/json", "Current Version"),
                    OgcSharedHandlers.BuildLink(request, $"/ogc/styles/{styleId}/history", "history", "application/json", "Version History")
                }
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Failed to retrieve version: {ex.Message}", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

    /// <summary>
    /// Validates a style without saving it (POST /ogc/styles/validate)
    /// </summary>
    public static async Task<IResult> ValidateStyle(
        HttpRequest request,
        [FromServices] IApiMetrics apiMetrics,
        [FromServices] ILogger<Program> logger,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(request);

        var contentType = request.ContentType?.Split(';')[0].Trim().ToLowerInvariant();
        ValidationResult validation;

        try
        {
            switch (contentType)
            {
                case "application/json":
                    // Reuse existing parsing and validation logic
                    var (style, styleValidation, error) = await TryReadStyleAsync(request, logger, cancellationToken).ConfigureAwait(false);
                    if (error is not null)
                    {
                        return error;
                    }
                    validation = styleValidation!;
                    break;

                case "application/vnd.ogc.sld+xml":
                case "application/xml":
                case "text/xml":
                    using (var reader = new StreamReader(request.Body))
                    {
                        var sldXml = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                        validation = StyleValidator.ValidateSldXml(sldXml);
                    }
                    break;

                case "application/vnd.mapbox-style+json":
                    using (var reader = new StreamReader(request.Body))
                    {
                        var mapboxJson = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                        validation = StyleValidator.ValidateMapboxStyle(mapboxJson);
                    }
                    break;

                case "text/css":
                case "application/x-cartocss":
                    using (var reader = new StreamReader(request.Body))
                    {
                        var cartoCSS = await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
                        validation = StyleValidator.ValidateCartoCSS(cartoCSS);
                    }
                    break;

                default:
                    return Results.Problem($"Unsupported content type: {contentType}", statusCode: StatusCodes.Status415UnsupportedMediaType);
            }


            return Results.Ok(new
            {
                valid = validation.IsValid,
                errors = validation.Errors,
                warnings = validation.Warnings,
                summary = validation.GetSummary()
            });
        }
        catch (Exception ex)
        {
            return Results.Problem($"Validation failed: {ex.Message}", statusCode: StatusCodes.Status500InternalServerError);
        }
    }

}
