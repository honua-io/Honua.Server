// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Print.MapFish;
using Honua.Server.Core.Raster.Print.MapFish;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.GeoservicesREST;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Print;

internal static class MapFishPrintHandlers
{
    public static async Task<IResult> GetApplicationsAsync(
        HttpContext context,
        [FromServices] IMapFishPrintApplicationStore applicationStore,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(context);
        Guard.NotNull(applicationStore);

        var applications = await applicationStore.GetApplicationsAsync(cancellationToken).ConfigureAwait(false);
        if (applications.Count == 0)
        {
            return Results.Json(Array.Empty<object>());
        }

        var request = context.Request;
        var baseUri = new Uri(request.GetDisplayUrl()).GetLeftPart(UriPartial.Authority);
        var payload = applications.Values.Select(app => new
        {
            id = app.Id,
            title = app.Title,
            description = app.Description,
            url = $"{baseUri}/print/apps/{Uri.EscapeDataString(app.Id)}.json",
            reportUrl = $"{baseUri}/print/apps/{Uri.EscapeDataString(app.Id)}/report.pdf"
        });

        return Results.Json(payload, MapFishJsonOptions.Default);
    }

    public static async Task<IResult> GetApplicationCapabilitiesAsync(
        HttpContext context,
        string appId,
        [FromServices] IMapFishPrintApplicationStore applicationStore,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(context);
        Guard.NotNull(applicationStore);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);

        var application = await applicationStore.FindAsync(appId, cancellationToken).ConfigureAwait(false);
        if (application is null)
        {
            return GeoservicesRESTErrorHelper.NotFound("Print application", appId);
        }

        var request = context.Request;
        var baseUri = new Uri(request.GetDisplayUrl()).GetLeftPart(UriPartial.Authority);
        var payload = BuildCapabilitiesPayload(application, baseUri);

        return Results.Json(payload, MapFishJsonOptions.Default);
    }

    public static async Task<IResult> CreateReportAsync(
        string appId,
        string format,
        HttpContext context,
        [FromServices] IMapFishPrintService printService,
        [FromServices] ILogger<MapFishPrintHandlerLogger> logger,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(context);
        Guard.NotNull(printService);
        ArgumentException.ThrowIfNullOrWhiteSpace(appId);
        ArgumentException.ThrowIfNullOrWhiteSpace(format);

        if (!string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
        {
            return Results.BadRequest(new { message = $"Format '{format}' is not supported. Only PDF output is currently available." });
        }

        var spec = await context.Request.ReadFromJsonAsync<MapFishPrintSpec>(cancellationToken: cancellationToken).ConfigureAwait(false);
        if (spec is null)
        {
            return Results.BadRequest(new { message = "Print specification body is required." });
        }

        try
        {
            var result = await printService.CreateReportAsync(appId, spec, cancellationToken).ConfigureAwait(false);
            return Results.File(result.Content, result.ContentType, result.FileName);
        }
        catch (InvalidOperationException ex)
        {
            // These are typically validation errors (e.g., application not found, invalid spec)
            logger.LogWarning(ex, "MapFish print request validation failed for application {ApplicationId}.", appId);
            return Results.BadRequest(new { message = "The print request could not be processed. Please check your print specification and try again." });
        }
        catch (Exception ex)
        {
            // Log the full exception details internally but return a generic error to clients
            // to prevent leaking sensitive information like stack traces or file paths
            logger.LogError(ex, "MapFish print request failed unexpectedly for application {ApplicationId}.", appId);
            return Results.Problem(
                detail: "An error occurred while generating the print report. Please try again later.",
                statusCode: StatusCodes.Status500InternalServerError,
                title: "Print Generation Failed");
        }
    }

    private static object BuildCapabilitiesPayload(MapFishPrintApplicationDefinition application, string baseUri)
    {
        var layouts = application.Layouts.Select(layout =>
        {
            var mapDefinition = layout.Map ?? MapFishPrintLayoutMapDefinition.Default();
            var pageDefinition = layout.Page ?? MapFishPrintLayoutPageDefinition.A4Portrait();

            return new
            {
                name = layout.Name,
                rotation = layout.SupportsRotation,
                @default = layout.Default,
                map = new
                {
                    width = mapDefinition.WidthPixels,
                    height = mapDefinition.HeightPixels,
                    dpi = application.Dpis,
                    supportsRotation = layout.SupportsRotation
                },
                page = new
                {
                    width = pageDefinition.WidthPoints,
                    height = pageDefinition.HeightPoints,
                    margin = pageDefinition.MarginPoints,
                    orientation = pageDefinition.Orientation,
                    size = pageDefinition.Size
                },
                legend = new
                {
                    enabled = layout.Legend.Enabled
                }
            };
        });

        var attributes = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, definition) in application.Attributes)
        {
            attributes[key] = new
            {
                type = definition.Type,
                required = definition.Required,
                description = definition.Description,
                clientInfo = definition.ClientInfo is null
                    ? null
                    : new
                    {
                        dpiSuggestions = definition.ClientInfo.DpiSuggestions,
                        scales = definition.ClientInfo.Scales,
                        projection = definition.ClientInfo.Projection,
                        rotatable = definition.ClientInfo.Rotatable
                    }
            };
        }

        return new
        {
            app = application.Id,
            title = application.Title,
            description = application.Description,
            defaultLayout = application.DefaultLayout,
            defaultOutputFormat = application.DefaultOutputFormat,
            defaultDpi = application.DefaultDpi,
            layouts,
            dpis = application.Dpis.Select(value => new { value }),
            outputFormats = application.OutputFormats.Select(name => new { name }),
            attributes,
            links = new
            {
                report = $"{baseUri}/print/apps/{Uri.EscapeDataString(application.Id)}/report.pdf"
            }
        };
    }

    private static class MapFishJsonOptions
    {
        public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public sealed class MapFishPrintHandlerLogger
    {
    }
}
