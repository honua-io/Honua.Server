// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Catalog;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Analytics;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using RasterPoint = Honua.Server.Core.Raster.Analytics.Point;

namespace Honua.Server.Host.GeoservicesREST;

[ApiController]
[Authorize(Policy = "RequireViewer")]
[Route("rest/services/{folderId}/{serviceId}/ImageServer")]
public sealed class GeoservicesRESTImageServerController : ControllerBase
{
    private const double GeoServicesVersion = 10.81;

    private readonly ICatalogProjectionService catalog;
    private readonly IRasterDatasetRegistry rasterRegistry;
    private readonly IRasterRenderer rasterRenderer;
    private readonly IMetadataRegistry metadataRegistry;
    private readonly IRasterAnalyticsService analytics;
    private readonly ILogger<GeoservicesRESTImageServerController> logger;

    public GeoservicesRESTImageServerController(
        ICatalogProjectionService catalog,
        IRasterDatasetRegistry rasterRegistry,
        IRasterRenderer rasterRenderer,
        IMetadataRegistry metadataRegistry,
        IRasterAnalyticsService analytics,
        ILogger<GeoservicesRESTImageServerController> logger)
    {
        this.catalog = Guard.NotNull(catalog);
        this.rasterRegistry = Guard.NotNull(rasterRegistry);
        this.rasterRenderer = Guard.NotNull(rasterRenderer);
        this.metadataRegistry = Guard.NotNull(metadataRegistry);
        this.analytics = Guard.NotNull(analytics);
        this.logger = Guard.NotNull(logger);
    }

    [HttpGet]
    public async Task<ActionResult<GeoservicesRESTImageServiceSummary>> GetService(
        string folderId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return this.NotFound();
        }

        var datasets = await this.rasterRegistry.GetByServiceAsync(serviceView.Service.Id, cancellationToken).ConfigureAwait(false);
        var rasterDatasets = datasets.Where(IsRenderableDataset).ToArray();
        if (rasterDatasets.Length == 0)
        {
            return this.NotFound();
        }

        var summary = GeoservicesRESTMetadataMapper.CreateImageServiceSummary(serviceView, rasterDatasets, GeoServicesVersion);
        return this.Ok(summary);
    }

    [HttpGet("exportImage")]
    public async Task<IActionResult> ExportImageAsync(
        string folderId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        return await ActivityScope.ExecuteAsync(
            HonuaTelemetry.RasterTiles,
            "ArcGIS ImageServer ExportImage",
            [
                ("arcgis.operation", "ExportImage"),
                ("arcgis.service", serviceId)
            ],
            async activity =>
            {
                var serviceView = ResolveService(folderId, serviceId);
                if (serviceView is null)
                {
                    return this.NotFound();
                }

                // Parse export parameters using shared helper
                // Note: For ImageServer, we filter for renderable datasets only (COG/GeoTIFF)
                var (parameters, error) = await GeoservicesRESTRasterExportHelper.TryParseExportRequestAsync(
                    Request,
                    serviceView,
                    _rasterRegistry,
                    cancellationToken,
                    datasetFilter: IsRenderableDataset).ConfigureAwait(false);

                if (error is not null)
                {
                    return error;
                }

                var style = await GeoservicesRESTRasterExportHelper.ResolveStyleDefinitionAsync(
                    _metadataRegistry,
                    parameters!.Dataset,
                    parameters.StyleId,
                    cancellationToken).ConfigureAwait(false);

                var renderRequest = new RasterRenderRequest(
                    parameters.Dataset,
                    parameters.Bbox,
                    parameters.Width,
                    parameters.Height,
                    parameters.SourceCrs,
                    parameters.TargetCrs,
                    parameters.Format,
                    parameters.Transparent,
                    parameters.StyleId,
                    style,
                    null);

                var result = await this.rasterRenderer.RenderAsync(renderRequest, cancellationToken).ConfigureAwait(false);
                if (result.Content.CanSeek)
                {
                    result.Content.Seek(0, SeekOrigin.Begin);
                }

                this.Response.Headers["X-Rendered-Dataset"] = parameters.Dataset.Id;
                this.Response.Headers["X-Target-CRS"] = parameters.TargetCrs;

                return this.File(result.Content, result.ContentType);
            }).ConfigureAwait(false);
    }

    [HttpGet("legend")]
    public async Task<IActionResult> GetLegendAsync(
        string folderId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return this.NotFound();
        }

        var datasets = await this.rasterRegistry.GetByServiceAsync(serviceView.Service.Id, cancellationToken).ConfigureAwait(false);
        var rasterDatasets = datasets.Where(IsRenderableDataset).ToArray();
        if (rasterDatasets.Length == 0)
        {
            return this.NotFound();
        }

        var layers = rasterDatasets.Select(dataset => new
        {
            layerId = dataset.Id,
            layerName = dataset.Title ?? dataset.Id,
            legend = BuildLegendEntries(dataset)
        }).ToArray();

        return this.Ok(new
        {
            currentVersion = GeoServicesVersion,
            layers
        });
    }

    [HttpGet("getSamples")]
    public async Task<IActionResult> GetSamplesAsync(
        string folderId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        var (_, dataset, problem) = await ResolveDatasetAsync(folderId, serviceId, this.Request.Query["rasterId"], cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var styleIdRaw = this.Request.Query.TryGetValue("styleId", out var styleValues) ? styleValues.ToString() : null;
        if (!GeoservicesRESTRasterRequestParser.TryResolveStyle(dataset!, styleIdRaw, out var resolvedStyle, out var unresolvedStyle))
        {
            return this.BadRequest(new { error = $"Style '{unresolvedStyle}' is not defined for raster dataset '{dataset!.Id}'." });
        }

        var geometryText = this.Request.Query.TryGetValue("geometry", out var geometryValues) ? geometryValues.ToString() : null;
        if (!TryParsePoint(geometryText, out var x, out var y, out var geometryWkid))
        {
            return this.BadRequest(new { error = "Parameter 'geometry' must be a JSON point with x and y coordinates." });
        }

        var sr = ResolveSpatialReference(this.Request.Query.TryGetValue("sr", out var srValues) ? srValues.ToString() : null, geometryWkid, dataset!);

        try
        {
            var extraction = await this.analytics.ExtractValuesAsync(
                new RasterValueExtractionRequest(
                    dataset!,
                    new[] { new RasterPoint(x, y) }),
                cancellationToken).ConfigureAwait(false);

            var sample = extraction.Values.FirstOrDefault();
            if (sample is null)
            {
                return this.NotFound(new { error = "No sample is available at the requested location." });
            }

            var values = sample.Value.HasValue
                ? new double?[] { sample.Value.Value }
                : Array.Empty<double?>();

            var attributes = new Dictionary<string, object?>();
            if (resolvedStyle.HasValue())
            {
                attributes["styleId"] = resolvedStyle;
            }

            var payload = new
            {
                rasterId = dataset!.Id,
                location = new
                {
                    x = sample.X,
                    y = sample.Y,
                    spatialReference = new { wkid = sr }
                },
                attributes,
                value = values
            };

            return this.Ok(new { samples = new[] { payload } });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to extract raster sample for dataset {DatasetId}", dataset!.Id);
            return this.StatusCode(StatusCodes.Status502BadGateway, new { error = "Raster analytics service is unavailable." });
        }
    }

    [HttpGet("identify")]
    public async Task<IActionResult> IdentifyAsync(
        string folderId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        var (_, dataset, problem) = await ResolveDatasetAsync(folderId, serviceId, this.Request.Query["rasterId"], cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        var geometryText = this.Request.Query.TryGetValue("geometry", out var geometryValues) ? geometryValues.ToString() : null;
        if (!TryParsePoint(geometryText, out var x, out var y, out _))
        {
            return this.BadRequest(new { error = "Parameter 'geometry' must be a JSON point with x and y coordinates." });
        }

        var sr = ResolveSpatialReference(this.Request.Query.TryGetValue("sr", out var srValues) ? srValues.ToString() : null, null, dataset!);

        try
        {
            var extraction = await this.analytics.ExtractValuesAsync(
                new RasterValueExtractionRequest(
                    dataset!,
                    new[] { new RasterPoint(x, y) }),
                cancellationToken).ConfigureAwait(false);

            var sample = extraction.Values.FirstOrDefault();
            if (sample is null)
            {
                return this.NotFound(new { error = "No pixel values are available at the requested location." });
            }

            var value = sample.Value.HasValue
                ? new double?[] { sample.Value.Value }
                : Array.Empty<double?>();

            var catalogItems = new[]
            {
                new
                {
                    name = dataset!.Title ?? dataset!.Id,
                    sample = new
                    {
                        rasterId = dataset!.Id,
                        location = new
                        {
                            x = sample.X,
                            y = sample.Y,
                            spatialReference = new { wkid = sr }
                        },
                        value
                    }
                }
            };

            return this.Ok(new { catalogItems });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to identify raster sample for dataset {DatasetId}", dataset!.Id);
            return this.StatusCode(StatusCodes.Status502BadGateway, new { error = "Raster analytics service is unavailable." });
        }
    }

    [HttpGet("computeHistograms")]
    public async Task<IActionResult> ComputeHistogramsAsync(
        string folderId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        if (!ResolveBins(this.Request.Query.TryGetValue("bins", out var binValues) ? binValues.ToString() : null, out var bins, out var validationError))
        {
            return this.BadRequest(new { error = validationError });
        }

        var (_, dataset, problem) = await ResolveDatasetAsync(folderId, serviceId, this.Request.Query["rasterId"], cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        try
        {
            var histogram = await this.analytics.CalculateHistogramAsync(
                new RasterHistogramRequest(dataset!, bins),
                cancellationToken).ConfigureAwait(false);

            var statistics = await this.analytics.CalculateStatisticsAsync(
                new RasterStatisticsRequest(dataset!),
                cancellationToken).ConfigureAwait(false);

            var bandStats = statistics.Bands.FirstOrDefault(b => b.BandIndex == histogram.BandIndex)
                ?? statistics.Bands.FirstOrDefault();

            var counts = histogram.Bins.Select(bin => bin.Count).ToArray();

            var histograms = new[]
            {
                new
                {
                    rasterId = dataset!.Id,
                    bands = new[]
                    {
                        new
                        {
                            band = histogram.BandIndex,
                            minimum = bandStats?.Min ?? histogram.Min,
                            maximum = bandStats?.Max ?? histogram.Max,
                            mean = bandStats?.Mean ?? 0d,
                            counts,
                            description = dataset!.Title ?? dataset!.Id
                        }
                    }
                }
            };

            return this.Ok(new { histograms });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to compute histogram for dataset {DatasetId}", dataset!.Id);
            return this.StatusCode(StatusCodes.Status502BadGateway, new { error = "Raster analytics service is unavailable." });
        }
    }

    [HttpGet("getRasterAttributes")]
    public async Task<IActionResult> GetRasterAttributesAsync(
        string folderId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        var (_, dataset, problem) = await ResolveDatasetAsync(folderId, serviceId, this.Request.Query["rasterId"], cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        try
        {
            var statistics = await this.analytics.CalculateStatisticsAsync(
                new RasterStatisticsRequest(dataset!),
                cancellationToken).ConfigureAwait(false);

            var fields = new[]
            {
                new { name = "OBJECTID", type = "esriFieldTypeOID", alias = "OBJECTID" },
                new { name = "Band", type = "esriFieldTypeInteger", alias = "Band" },
                new { name = "Min", type = "esriFieldTypeDouble", alias = "Minimum" },
                new { name = "Max", type = "esriFieldTypeDouble", alias = "Maximum" },
                new { name = "Mean", type = "esriFieldTypeDouble", alias = "Mean" },
                new { name = "StdDev", type = "esriFieldTypeDouble", alias = "Standard Deviation" },
                new { name = "ValidPixels", type = "esriFieldTypeInteger", alias = "Valid Pixels" },
                new { name = "NoDataPixels", type = "esriFieldTypeInteger", alias = "NoData Pixels" },
                new { name = "NoDataValue", type = "esriFieldTypeDouble", alias = "NoData Value" },
                new { name = "Value", type = "esriFieldTypeDouble", alias = "Value" }
            };

            var features = statistics.Bands
                .Select((band, index) => new
                {
                    attributes = new Dictionary<string, object?>
                    {
                        ["OBJECTID"] = index + 1,
                        ["Band"] = band.BandIndex,
                        ["Min"] = band.Min,
                        ["Max"] = band.Max,
                        ["Mean"] = band.Mean,
                        ["StdDev"] = band.StdDev,
                        ["ValidPixels"] = band.ValidPixelCount,
                        ["NoDataPixels"] = band.NoDataPixelCount,
                        ["NoDataValue"] = band.NoDataValue,
                        ["Value"] = band.Mean
                    }
                })
                .ToArray();

            return this.Ok(new { fields, features });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to retrieve raster attributes for dataset {DatasetId}", dataset!.Id);
            return this.StatusCode(StatusCodes.Status502BadGateway, new { error = "Raster analytics service is unavailable." });
        }
    }

    [HttpGet("getRasterInfo")]
    public async Task<IActionResult> GetRasterInfoAsync(
        string folderId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        var (_, dataset, problem) = await ResolveDatasetAsync(folderId, serviceId, this.Request.Query["rasterId"], cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        try
        {
            var statistics = await this.analytics.CalculateStatisticsAsync(
                new RasterStatisticsRequest(dataset!),
                cancellationToken).ConfigureAwait(false);

            var wkid = ResolveSpatialReference(null, null, dataset!);
            var extent = ResolveExtent(dataset!);

            var bands = statistics.Bands.Select(band => new
            {
                bandId = band.BandIndex,
                name = $"Band {band.BandIndex + 1}",
                minimum = band.Min,
                maximum = band.Max,
                mean = band.Mean,
                standardDeviation = band.StdDev,
                description = dataset!.Title ?? dataset!.Id,
                noDataValue = band.NoDataValue
            }).ToArray();

            var info = new
            {
                datasetId = dataset!.Id,
                name = dataset!.Title ?? dataset!.Id,
                bandCount = bands.Length,
                pixelSizeX = 1.0,
                pixelSizeY = -1.0,
                pixelType = "F32",
                spatialReference = new { wkid },
                extent = new
                {
                    xmin = extent.xmin,
                    ymin = extent.ymin,
                    xmax = extent.xmax,
                    ymax = extent.ymax,
                    spatialReference = new { wkid }
                },
                bands
            };

            return this.Ok(info);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to retrieve raster info for dataset {DatasetId}", dataset!.Id);
            return this.StatusCode(StatusCodes.Status502BadGateway, new { error = "Raster analytics service is unavailable." });
        }
    }

    [HttpGet("getPixelHistograms")]
    public async Task<IActionResult> GetPixelHistogramsAsync(
        string folderId,
        string serviceId,
        CancellationToken cancellationToken)
    {
        if (!ResolveBins(this.Request.Query.TryGetValue("bins", out var binValues) ? binValues.ToString() : null, out var bins, out var validationError))
        {
            return this.BadRequest(new { error = validationError });
        }

        var (_, dataset, problem) = await ResolveDatasetAsync(folderId, serviceId, this.Request.Query["rasterId"], cancellationToken).ConfigureAwait(false);
        if (problem is not null)
        {
            return problem;
        }

        try
        {
            var histogram = await this.analytics.CalculateHistogramAsync(
                new RasterHistogramRequest(dataset!, bins),
                cancellationToken).ConfigureAwait(false);

            var counts = histogram.Bins.Select(bin => bin.Count).ToArray();

            var histograms = new[]
            {
                new
                {
                    rasterId = dataset!.Id,
                    bands = new[]
                    {
                        new
                        {
                            band = histogram.BandIndex,
                            counts,
                            description = dataset!.Title ?? dataset!.Id
                        }
                    }
                }
            };

            return this.Ok(new { histograms });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            this.logger.LogError(ex, "Failed to compute pixel histogram for dataset {DatasetId}", dataset!.Id);
            return this.StatusCode(StatusCodes.Status502BadGateway, new { error = "Raster analytics service is unavailable." });
        }
    }

    private async Task<(CatalogServiceView? ServiceView, RasterDatasetDefinition? Dataset, IActionResult? Problem)> ResolveDatasetAsync(
        string folderId,
        string serviceId,
        string? rasterId,
        CancellationToken cancellationToken)
    {
        var serviceView = ResolveService(folderId, serviceId);
        if (serviceView is null)
        {
            return (null, null, NotFound());
        }

        RasterDatasetDefinition? dataset = null;
        if (rasterId.HasValue())
        {
            dataset = await this.rasterRegistry.FindAsync(rasterId, cancellationToken).ConfigureAwait(false);
        }

        if (dataset is not null && !IsRenderableDataset(dataset))
        {
            dataset = null;
        }

        if (dataset is null)
        {
            var datasets = await this.rasterRegistry.GetByServiceAsync(serviceView.Service.Id, cancellationToken).ConfigureAwait(false);
            dataset = datasets.FirstOrDefault(IsRenderableDataset);
        }

        if (dataset is null)
        {
            return (serviceView, null, NotFound(new { error = "No raster datasets registered for this service." }));
        }

        return (serviceView, dataset, null);
    }

    private CatalogServiceView? ResolveService(string folderId, string serviceId)
    {
        if (serviceId.IsNullOrWhiteSpace())
        {
            return null;
        }

        var service = this.catalog.GetService(serviceId);
        if (service is null)
        {
            return null;
        }

        if (!service.Service.FolderId.EqualsIgnoreCase(folderId))
        {
            return null;
        }

        return SupportsImageServer(service.Service) ? service : null;
    }

    private static bool SupportsImageServer(ServiceDefinition service)
    {
        return service.ServiceType.EqualsIgnoreCase("ImageServer")
            || service.ServiceType.EqualsIgnoreCase("image")
            || service.ServiceType.EqualsIgnoreCase("MapServer")
            || service.ServiceType.EqualsIgnoreCase("map")
            || service.ServiceType.EqualsIgnoreCase("FeatureServer")
            || service.ServiceType.EqualsIgnoreCase("feature");
    }

    private static bool IsRenderableDataset(RasterDatasetDefinition dataset)
    {
        var type = dataset?.Source?.Type;
        if (type.IsNullOrWhiteSpace())
        {
            return false;
        }

        return type.EqualsIgnoreCase("cog")
            || type.EqualsIgnoreCase("geotiff");
    }


    private static IReadOnlyList<object> BuildLegendEntries(RasterDatasetDefinition dataset)
    {
        var label = dataset.Styles.DefaultStyleId ?? dataset.Title ?? dataset.Id;
        var entry = new
        {
            label,
            url = string.Empty,
            imageData = Convert.ToBase64String(PlaceholderLegendPng),
            contentType = "image/png"
        };

        return new[] { (object)entry };
    }

    private static bool TryParsePoint(string? geometry, out double x, out double y, out int? wkid)
    {
        x = 0;
        y = 0;
        wkid = null;

        if (geometry.IsNullOrWhiteSpace())
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(geometry);
            var root = document.RootElement;
            if (root.TryGetProperty("x", out var xElement))
            {
                x = xElement.GetDouble();
            }

            if (root.TryGetProperty("y", out var yElement))
            {
                y = yElement.GetDouble();
            }

            if (root.TryGetProperty("spatialReference", out var srElement) && srElement.ValueKind == JsonValueKind.Object && srElement.TryGetProperty("wkid", out var wkidElement))
            {
                wkid = wkidElement.GetInt32();
            }

            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static int ResolveSpatialReference(string? srParameter, int? geometryWkid, RasterDatasetDefinition dataset)
    {
        if (srParameter.HasValue() && TryParseWkid(srParameter, out var parsed))
        {
            return parsed;
        }

        if (geometryWkid.HasValue)
        {
            return geometryWkid.Value;
        }

        var datasetCrs = dataset.Crs.FirstOrDefault();
        if (datasetCrs.HasValue() && TryParseWkid(datasetCrs, out var datasetWkid))
        {
            return datasetWkid;
        }

        return 4326;
    }

    private static bool TryParseWkid(string? value, out int wkid)
    {
        wkid = 0;
        if (value.IsNullOrWhiteSpace())
        {
            return false;
        }

        if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out wkid))
        {
            return true;
        }

        if (value.Contains(':', StringComparison.Ordinal))
        {
            var parts = value.Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var candidate = parts.Last();
            return int.TryParse(candidate, NumberStyles.Integer, CultureInfo.InvariantCulture, out wkid);
        }

        return false;
    }

    private static bool ResolveBins(string? value, out int bins, out string? error)
    {
        var (parsed, parseError) = QueryParsingHelpers.ParsePositiveInt(
            value,
            "bins",
            required: false,
            defaultValue: 256,
            allowZero: false,
            errorDetail: "Parameter 'bins' must be a positive integer.");

        if (parseError is not null)
        {
            error = QueryParsingHelpers.ExtractProblemMessage(parseError, "Parameter 'bins' must be a positive integer.");
            bins = 256;
            return false;
        }

        bins = parsed ?? 256;
        error = null;
        return true;
    }

    private static (double xmin, double ymin, double xmax, double ymax) ResolveExtent(RasterDatasetDefinition dataset)
    {
        if (dataset.Extent?.Bbox is { Count: > 0 })
        {
            var bbox = dataset.Extent.Bbox[0];
            if (bbox.Length == 4)
            {
                return (bbox[0], bbox[1], bbox[2], bbox[3]);
            }
        }

        return (-180d, -90d, 180d, 90d);
    }

    private static readonly byte[] PlaceholderLegendPng =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
    };
}
