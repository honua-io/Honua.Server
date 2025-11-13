// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Honua.Server.Core.Cdn;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Rendering;
using Honua.Server.Core.Security;
using Honua.Server.Core.Raster.Sources;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OSGeo.GDAL;

namespace Honua.Server.Host.Wcs;

/// <summary>
/// WCS 2.0.1 (Web Coverage Service) implementation.
/// Provides access to coverage (raster) data.
/// </summary>
internal static class WcsHandlers
{
    private static readonly XNamespace Wcs = "http://www.opengis.net/wcs/2.0";
    private static readonly XNamespace Ows = "http://www.opengis.net/ows/2.0";
    private static readonly XNamespace Gml = "http://www.opengis.net/gml/3.2";
    private static readonly XNamespace Crs = "http://www.opengis.net/wcs/service-extension/crs/1.0";
    private static readonly XNamespace Xsi = "http://www.w3.org/2001/XMLSchema-instance";
    private static readonly XNamespace XLink = "http://www.w3.org/1999/xlink";
    private static readonly Regex SubsetExpressionRegex = new(@"^(?<axis>[^()]+)\((?<lower>[^,()]+)(,(?<upper>[^()]+))?\)$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public static async Task<IResult> HandleAsync(
        HttpContext context,
        [FromServices] IMetadataRegistry metadataRegistry,
        [FromServices] IRasterDatasetRegistry rasterRegistry,
        [FromServices] IRasterRenderer? renderer,
        [FromServices] IRasterSourceProviderRegistry sourceProviderRegistry,
        CancellationToken cancellationToken)
    {
        Guard.NotNull(context);
        Guard.NotNull(metadataRegistry);
        Guard.NotNull(rasterRegistry);
        Guard.NotNull(sourceProviderRegistry);

        var request = context.Request;
        var query = request.Query;

        var serviceValue = QueryParsingHelpers.GetQueryValue(query, "service");
        if (!serviceValue.EqualsIgnoreCase("WCS"))
        {
            return CreateExceptionReport("InvalidParameterValue", "service", "Parameter 'service' must be set to 'WCS'.");
        }

        var requestValue = QueryParsingHelpers.GetQueryValue(query, "request");
        if (requestValue.IsNullOrWhiteSpace())
        {
            return CreateExceptionReport("MissingParameterValue", "request", "Parameter 'request' is required.");
        }

        var metadataSnapshot = await metadataRegistry.GetSnapshotAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            return requestValue.ToUpperInvariant() switch
            {
                "GETCAPABILITIES" => await HandleGetCapabilitiesAsync(request, metadataSnapshot, rasterRegistry, cancellationToken),
                "DESCRIBECOVERAGE" => await HandleDescribeCoverageAsync(request, query, metadataSnapshot, rasterRegistry, sourceProviderRegistry, cancellationToken),
                "GETCOVERAGE" => await HandleGetCoverageAsync(request, query, metadataSnapshot, rasterRegistry, renderer, sourceProviderRegistry, cancellationToken),
                _ => CreateExceptionReport("InvalidParameterValue", "request", $"Request '{requestValue}' is not supported.")
            };
        }
        catch (Exception ex)
        {
            return CreateExceptionReport("NoApplicableCode", null, $"Error processing request: {ex.Message}");
        }
    }

    private static async Task<IResult> HandleGetCapabilitiesAsync(
        HttpRequest request,
        MetadataSnapshot metadata,
        IRasterDatasetRegistry rasterRegistry,
        CancellationToken cancellationToken)
    {
        using var activity = HonuaTelemetry.OgcProtocols.StartActivity("WCS GetCapabilities");
        activity?.SetTag("wcs.operation", "GetCapabilities");

        var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
        var wcsUrl = $"{baseUrl}/wcs";

        var coverages = await rasterRegistry.GetAllAsync(cancellationToken);

        var capabilities = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Wcs + "Capabilities",
                new XAttribute("version", "2.0.1"),
                new XAttribute(XNamespace.Xmlns + "wcs", Wcs),
                new XAttribute(XNamespace.Xmlns + "ows", Ows),
                new XAttribute(XNamespace.Xmlns + "gml", Gml),
                new XAttribute(XNamespace.Xmlns + "xlink", XLink),
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
                new XAttribute(Xsi + "schemaLocation", "http://www.opengis.net/wcs/2.0 http://schemas.opengis.net/wcs/2.0/wcsAll.xsd"),

                // ServiceIdentification
                new XElement(Ows + "ServiceIdentification",
                    new XElement(Ows + "Title", metadata.Catalog.Title + " - Web Coverage Service"),
                    new XElement(Ows + "Abstract", "Honua Web Coverage Service providing access to raster data"),
                    new XElement(Ows + "ServiceType", "WCS"),
                    new XElement(Ows + "ServiceTypeVersion", "2.0.1"),
                    new XElement(Ows + "Profile", "http://www.opengis.net/spec/WCS/2.0/conf/core"),
                    new XElement(Ows + "Fees", "none"),
                    new XElement(Ows + "AccessConstraints", "none")
                ),

                // ServiceProvider
                new XElement(Ows + "ServiceProvider",
                    new XElement(Ows + "ProviderName", metadata.Catalog.Title),
                    new XElement(Ows + "ProviderSite", new XAttribute(XLink + "href", baseUrl)),
                    new XElement(Ows + "ServiceContact",
                        new XElement(Ows + "IndividualName", "Honua Administrator"),
                        new XElement(Ows + "ContactInfo",
                            new XElement(Ows + "Address",
                                new XElement(Ows + "ElectronicMailAddress", "admin@honua.io")
                            )
                        )
                    )
                ),

                // OperationsMetadata
                new XElement(Ows + "OperationsMetadata",
                    CreateOperation("GetCapabilities", wcsUrl),
                    CreateOperation("DescribeCoverage", wcsUrl),
                    CreateOperation("GetCoverage", wcsUrl)
                ),

                // ServiceMetadata
                new XElement(Wcs + "ServiceMetadata",
                    new XElement(Wcs + "formatSupported", "image/tiff"),
                    new XElement(Wcs + "formatSupported", "image/png"),
                    new XElement(Wcs + "formatSupported", "image/jpeg"),
                    new XElement(Wcs + "Extension",
                        new XElement(Crs + "CrsMetadata",
                            new XElement(Crs + "crsSupported", "http://www.opengis.net/def/crs/EPSG/0/4326"),
                            new XElement(Crs + "crsSupported", "http://www.opengis.net/def/crs/EPSG/0/3857")
                        )
                    )
                ),

                // Contents
                new XElement(Wcs + "Contents",
                    coverages.Select(c => CreateCoverageSummary(c, wcsUrl))
                )
            )
        );

        return Results.Content(capabilities.ToString(), "application/xml; charset=utf-8");
    }

    private static XElement CreateOperation(string name, string url)
    {
        return new XElement(Ows + "Operation",
            new XAttribute("name", name),
            new XElement(Ows + "DCP",
                new XElement(Ows + "HTTP",
                    new XElement(Ows + "Get", new XAttribute(XLink + "href", url)),
                    new XElement(Ows + "Post", new XAttribute(XLink + "href", url))
                )
            )
        );
    }

    private static XElement CreateCoverageSummary(RasterDatasetDefinition dataset, string baseUrl)
    {
        return new XElement(Wcs + "CoverageSummary",
            new XElement(Wcs + "CoverageId", dataset.Id),
            new XElement(Wcs + "CoverageSubtype", "RectifiedGridCoverage"),
            dataset.Extent?.Bbox != null && dataset.Extent.Bbox.Count > 0
                ? new XElement(Ows + "WGS84BoundingBox",
                    new XElement(Ows + "LowerCorner", $"{dataset.Extent.Bbox[0][1]} {dataset.Extent.Bbox[0][0]}"),
                    new XElement(Ows + "UpperCorner", $"{dataset.Extent.Bbox[0][3]} {dataset.Extent.Bbox[0][2]}")
                )
                : null
        );
    }

    private static async Task<IResult> HandleDescribeCoverageAsync(
        HttpRequest request,
        IQueryCollection query,
        MetadataSnapshot metadata,
        IRasterDatasetRegistry rasterRegistry,
        IRasterSourceProviderRegistry sourceProviderRegistry,
        CancellationToken cancellationToken)
    {
        using var activity = HonuaTelemetry.OgcProtocols.StartActivity("WCS DescribeCoverage");
        activity?.SetTag("wcs.operation", "DescribeCoverage");

        var coverageId = QueryParsingHelpers.GetQueryValue(query, "coverageId");
        activity?.SetTag("wcs.coverage_id", coverageId);
        if (coverageId.IsNullOrWhiteSpace())
        {
            return CreateExceptionReport("MissingParameterValue", "coverageId", "Parameter 'coverageId' is required.");
        }

        var dataset = await rasterRegistry.FindAsync(coverageId, cancellationToken).ConfigureAwait(false);
        if (dataset == null)
        {
            return CreateExceptionReport("NoSuchCoverage", "coverageId", $"Coverage '{coverageId}' not found.");
        }

        var location = ValidateRasterPath(dataset.Source.Uri, metadata.Server.Security, out var error);
        if (location is null)
        {
            return CreateExceptionReport("NoApplicableCode", null, error!);
        }

        if (location.IsLocalFile && !File.Exists(location.LocalPath!))
        {
            return CreateExceptionReport("NoApplicableCode", null, "Coverage file not found.");
        }

        Dataset? ds = null;
        string? tempSourcePath = null;

        try
        {
            (ds, tempSourcePath) = await OpenDatasetForProcessingAsync(location, sourceProviderRegistry, cancellationToken).ConfigureAwait(false);
            if (ds == null)
            {
                return CreateExceptionReport("NoApplicableCode", null, $"Unable to open coverage '{coverageId}'.");
            }

            var width = ds.RasterXSize;
            var height = ds.RasterYSize;
            var bandCount = ds.RasterCount;
            var projection = ds.GetProjection();

            var geoTransform = new double[6];
            ds.GetGeoTransform(geoTransform);

            var minX = geoTransform[0];
            var maxY = geoTransform[3];
            var pixelWidth = geoTransform[1];
            var pixelHeight = Math.Abs(geoTransform[5]);
            var maxX = minX + width * pixelWidth;
            var minY = maxY - height * pixelHeight;

            // Determine native CRS for the coverage
            var nativeCrsUri = WcsCrsHelper.GetNativeCrsUri(projection);

            var coverageDescriptionElements = new List<object>
            {
                new XElement(Gml + "boundedBy",
                    new XElement(Gml + "Envelope",
                        new XAttribute("srsName", nativeCrsUri),
                        new XAttribute("axisLabels", "Lat Long"),
                        new XAttribute("uomLabels", "deg deg"),
                        new XAttribute("srsDimension", "2"),
                        new XElement(Gml + "lowerCorner", FormattableString.Invariant($"{minY} {minX}")),
                        new XElement(Gml + "upperCorner", FormattableString.Invariant($"{maxY} {maxX}"))
                    )
                ),
                new XElement(Wcs + "CoverageId", coverageId)
            };

            if (dataset.Temporal.Enabled)
            {
                coverageDescriptionElements.Add(BuildWcsTemporalDomain(dataset.Temporal));
            }

            coverageDescriptionElements.Add(new XElement(Gml + "coverageFunction",
                new XElement(Gml + "GridFunction",
                    new XElement(Gml + "sequenceRule",
                        new XAttribute("axisOrder", "+1 +2"),
                        "Linear"
                    ),
                    new XElement(Gml + "startPoint", "0 0")
                )
            ));

            coverageDescriptionElements.Add(new XElement(Gml + "domainSet",
                new XElement(Gml + "RectifiedGrid",
                    new XAttribute(Gml + "id", $"grid-{coverageId}"),
                    new XAttribute("dimension", "2"),
                    new XElement(Gml + "limits",
                        new XElement(Gml + "GridEnvelope",
                            new XElement(Gml + "low", "0 0"),
                            new XElement(Gml + "high", $"{width - 1} {height - 1}")
                        )
                    ),
                    new XElement(Gml + "axisLabels", "i j"),
                    // Add grid origin point (upper-left corner in CRS coordinates)
                    new XElement(Gml + "origin",
                        new XElement(Gml + "Point",
                            new XAttribute(Gml + "id", $"origin-{coverageId}"),
                            new XAttribute("srsName", nativeCrsUri),
                            new XElement(Gml + "pos", FormattableString.Invariant($"{minX} {maxY}"))
                        )
                    ),
                    // Add offset vectors (pixel size and orientation)
                    // First offset vector: x-axis (columns, i-axis)
                    new XElement(Gml + "offsetVector",
                        new XAttribute("srsName", nativeCrsUri),
                        FormattableString.Invariant($"{pixelWidth} 0")
                    ),
                    // Second offset vector: y-axis (rows, j-axis)
                    new XElement(Gml + "offsetVector",
                        new XAttribute("srsName", nativeCrsUri),
                        FormattableString.Invariant($"0 {-pixelHeight}")
                    )
                )
            ));

            coverageDescriptionElements.Add(new XElement(Gml + "rangeType",
                new XElement(Wcs + "DataRecord",
                    Enumerable.Range(1, bandCount).Select(i =>
                        new XElement(Wcs + "Field",
                            new XAttribute("name", $"Band{i}"),
                            new XElement(Wcs + "Quantity",
                                new XElement(Wcs + "description", $"Band {i}"),
                                new XElement(Wcs + "uom", new XAttribute("code", "W.m-2.Sr-1"))
                            )
                        )
                    )
                )
            ));

            coverageDescriptionElements.Add(new XElement(Wcs + "ServiceParameters",
                new XElement(Wcs + "CoverageSubtype", "RectifiedGridCoverage"),
                new XElement(Wcs + "nativeFormat", "image/tiff")
            ));

            var response = new XDocument(
                new XDeclaration("1.0", "UTF-8", null),
                new XElement(Wcs + "CoverageDescriptions",
                    new XAttribute("version", "2.0.1"),
                    new XAttribute(XNamespace.Xmlns + "wcs", Wcs),
                    new XAttribute(XNamespace.Xmlns + "gml", Gml),
                    new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
                    new XElement(Wcs + "CoverageDescription", coverageDescriptionElements)
                )
            );

            return Results.Content(response.ToString(), "application/xml; charset=utf-8");
        }
        finally
        {
            ds?.Dispose();
            if (!tempSourcePath.IsNullOrEmpty())
            {
                TryDelete(tempSourcePath);
            }
        }
    }

    private static async Task<IResult> HandleGetCoverageAsync(
        HttpRequest request,
        IQueryCollection query,
        MetadataSnapshot metadata,
        IRasterDatasetRegistry rasterRegistry,
        IRasterRenderer? renderer,
        IRasterSourceProviderRegistry sourceProviderRegistry,
        CancellationToken cancellationToken)
    {
        using var activity = HonuaTelemetry.RasterTiles.StartActivity("WCS GetCoverage");
        activity?.SetTag("wcs.operation", "GetCoverage");

        var coverageId = QueryParsingHelpers.GetQueryValue(query, "coverageId");
        activity?.SetTag("wcs.coverage_id", coverageId);
        if (coverageId.IsNullOrWhiteSpace())
        {
            return CreateExceptionReport("MissingParameterValue", "coverageId", "Parameter 'coverageId' is required.");
        }

        var format = QueryParsingHelpers.GetQueryValue(query, "format") ?? "image/tiff";

        string normalizedFormat;
        try
        {
            normalizedFormat = NormalizeCoverageFormat(format);
        }
        catch (InvalidOperationException ex)
        {
            return CreateExceptionReport("InvalidParameterValue", "format", ex.Message);
        }

        var dataset = await rasterRegistry.FindAsync(coverageId, cancellationToken).ConfigureAwait(false);
        if (dataset == null)
        {
            return CreateExceptionReport("NoSuchCoverage", "coverageId", $"Coverage '{coverageId}' not found.");
        }

        // Parse CRS parameters (WCS 2.0 CRS extension)
        var subsettingCrs = QueryParsingHelpers.GetQueryValue(query, "subsettingCrs");
        var outputCrs = QueryParsingHelpers.GetQueryValue(query, "outputCrs");

        // Get native CRS from dataset
        var location = ValidateRasterPath(dataset.Source.Uri, metadata.Server.Security, out var error);
        if (location is null)
        {
            return CreateExceptionReport("NoApplicableCode", null, error!);
        }

        if (location.IsLocalFile && !File.Exists(location.LocalPath!))
        {
            return CreateExceptionReport("NoApplicableCode", null, "Coverage file not found.");
        }

        int nativeEpsg = 4326; // Default
        string? nativeProjection = null;
        Dataset? tempDs = null;
        string? tempPath = null;
        try
        {
            (tempDs, tempPath) = await OpenDatasetForProcessingAsync(location, sourceProviderRegistry, cancellationToken).ConfigureAwait(false);
            if (tempDs != null)
            {
                nativeProjection = tempDs.GetProjection();
                if (WcsCrsHelper.TryExtractEpsgFromProjection(nativeProjection, out var extractedEpsg))
                {
                    nativeEpsg = extractedEpsg;
                }
            }
        }
        finally
        {
            tempDs?.Dispose();
            if (!tempPath.IsNullOrEmpty())
            {
                TryDelete(tempPath);
            }
        }

        var nativeCrsUri = WcsCrsHelper.FormatCrsUri(nativeEpsg);

        // Validate CRS parameters
        if (!WcsCrsHelper.ValidateCrsParameters(subsettingCrs, outputCrs, nativeCrsUri, out var crsError))
        {
            return CreateExceptionReport("InvalidParameterValue", subsettingCrs.HasValue() ? "subsettingCrs" : "outputCrs", crsError!);
        }

        var (spatialSubset, requestedTime, subsetError) = ParseSubsetParameters(query, dataset);
        if (subsetError is not null)
        {
            return CreateExceptionReport("InvalidParameterValue", "subset", subsetError);
        }

        if (!dataset.Temporal.Enabled && requestedTime.HasValue())
        {
            return CreateExceptionReport("InvalidParameterValue", "subset", "Temporal subsets are not supported for this coverage.");
        }

        string? timeValue = null;
        if (dataset.Temporal.Enabled)
        {
            var timeCandidate = requestedTime.IsNullOrWhiteSpace() ? dataset.Temporal.DefaultValue : requestedTime;
            timeValue = ValidateTimeParameter(timeCandidate, dataset.Temporal);
        }

        // Parse scaling parameters (WCS 2.0 Scaling extension)
        var (scaleSize, scaleAxes, scalingError) = ParseScalingParameters(query);
        if (scalingError is not null)
        {
            return CreateExceptionReport("InvalidParameterValue", "scalesize", scalingError);
        }

        // Parse interpolation parameter (WCS 2.0 Interpolation extension)
        var interpolationParam = QueryParsingHelpers.GetQueryValue(query, "interpolation");
        if (!WcsInterpolationHelper.TryParseInterpolation(interpolationParam, out var interpolationMethod, out var interpolationError))
        {
            return CreateExceptionReport("InvalidParameterValue", "interpolation", interpolationError!);
        }

        // Parse rangeSubset parameter (WCS 2.0 Range Subsetting extension)
        var rangeSubsetParam = QueryParsingHelpers.GetQueryValue(query, "rangeSubset");

        var datasetExtent = dataset.Extent?.Bbox is { Count: > 0 } extents ? extents[0] : null;
        var applySpatialSubset = ShouldApplySpatialSubset(spatialSubset, datasetExtent);
        var applyTemporalSubset = dataset.Temporal.Enabled && timeValue.HasValue() &&
                                  !timeValue.EqualsIgnoreCase(dataset.Temporal.DefaultValue);

        var requiresFormatConversion = !normalizedFormat.EqualsIgnoreCase("image/tiff");
        var requiresCrsTransformation = WcsCrsHelper.NeedsTransformation(subsettingCrs, outputCrs, nativeEpsg);
        var requiresScaling = scaleSize != null || scaleAxes != null;

        CoverageData coverage;
        try
        {
            var requiresRangeSubsetting = !rangeSubsetParam.IsNullOrWhiteSpace();

            coverage = applySpatialSubset || applyTemporalSubset || requiresFormatConversion || requiresCrsTransformation || requiresScaling || requiresRangeSubsetting
                ? await CreateSubsetCoverageAsync(location, dataset, spatialSubset, timeValue, normalizedFormat, subsettingCrs, outputCrs, nativeEpsg, scaleSize, scaleAxes, interpolationMethod, rangeSubsetParam, sourceProviderRegistry, cancellationToken).ConfigureAwait(false)
                : CreateFullCoverageData(location, normalizedFormat, sourceProviderRegistry);
        }
        catch (InvalidOperationException ex)
        {
            return CreateExceptionReport("NoApplicableCode", null, ex.Message);
        }

        return CreateCoverageResultWithCdn(coverage, dataset.Cdn);
    }

    private static string NormalizeCoverageFormat(string requestedFormat)
    {
        var normalized = requestedFormat?.Trim();
        if (normalized.IsNullOrWhiteSpace())
        {
            return "image/tiff";
        }

        return normalized.ToLowerInvariant() switch
        {
            "image/tiff" or "image/geotiff" or "image/x-geotiff" => "image/tiff",
            "image/png" => "image/png",
            "image/jpeg" or "image/jpg" => "image/jpeg",
            "image/jp2" or "image/jpeg2000" or "image/jpx" => "image/jp2",
            "application/netcdf" or "application/x-netcdf" => "application/netcdf",
            "application/x-hdf" or "application/x-hdf5" => "application/x-hdf",
            _ => throw new InvalidOperationException($"Format '{requestedFormat}' is not supported. Supported formats: image/tiff, image/png, image/jpeg, image/jp2, application/netcdf.")
        };
    }

    private static (double[]? SpatialSubset, string? TimeValue, string? Error) ParseSubsetParameters(
        IQueryCollection query,
        RasterDatasetDefinition dataset)
    {
        if (!query.TryGetValue("subset", out var subsetValues) || subsetValues.Count == 0)
        {
            return (null, null, null);
        }

        double? minX = null, maxX = null, minY = null, maxY = null;
        string? timeValue = null;

        foreach (var raw in subsetValues)
        {
            if (raw.IsNullOrWhiteSpace())
            {
                continue;
            }

            var match = SubsetExpressionRegex.Match(raw);
            if (!match.Success)
            {
                return (null, null, $"Subset expression '{raw}' is not supported.");
            }

            var axis = match.Groups["axis"].Value.Trim();
            var lowerToken = NormalizeSubsetValue(match.Groups["lower"].Value);
            var upperToken = match.Groups["upper"].Success
                ? NormalizeSubsetValue(match.Groups["upper"].Value)
                : lowerToken;

            if (IsTimeAxis(axis))
            {
                timeValue = lowerToken;
                continue;
            }

            if (!lowerToken.TryParseDouble(out var lower) ||
                !upperToken.TryParseDouble(out var upper))
            {
                return (null, null, $"Subset values '{lowerToken}' and '{upperToken}' could not be parsed as coordinates.");
            }

            if (IsLongitudeAxis(axis))
            {
                minX = lower;
                maxX = upper;
                continue;
            }

            if (IsLatitudeAxis(axis))
            {
                minY = lower;
                maxY = upper;
                continue;
            }

            return (null, null, $"Subset axis '{axis}' is not supported.");
        }

        double[]? bbox = null;
        if (minX.HasValue && maxX.HasValue && minY.HasValue && maxY.HasValue)
        {
            if (minX.Value > maxX.Value || minY.Value > maxY.Value)
            {
                return (null, null, "Subset bounds are inverted.");
            }

            bbox = new[] { minX.Value, minY.Value, maxX.Value, maxY.Value };
        }
        else if (minX.HasValue || maxX.HasValue || minY.HasValue || maxY.HasValue)
        {
            return (null, null, "Both latitude and longitude constraints must be provided.");
        }

        return (bbox, timeValue, null);
    }

    private static string NormalizeSubsetValue(string value)
    {
        var trimmed = value.Trim();
        if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[^1] == '"')
        {
            return trimmed[1..^1];
        }

        return trimmed;
    }

    private static bool IsTimeAxis(string axis) =>
        axis.EqualsIgnoreCase("time") ||
        axis.EqualsIgnoreCase("t");

    private static bool IsLongitudeAxis(string axis) =>
        axis.EqualsIgnoreCase("long") ||
        axis.EqualsIgnoreCase("lon") ||
        axis.EqualsIgnoreCase("longitude") ||
        axis.EqualsIgnoreCase("x") ||
        axis.EqualsIgnoreCase("e") ||
        axis.EqualsIgnoreCase("east") ||
        axis.EqualsIgnoreCase("easting");

    private static bool IsLatitudeAxis(string axis) =>
        axis.EqualsIgnoreCase("lat") ||
        axis.EqualsIgnoreCase("latitude") ||
        axis.EqualsIgnoreCase("y") ||
        axis.EqualsIgnoreCase("n") ||
        axis.EqualsIgnoreCase("north") ||
        axis.EqualsIgnoreCase("northing");

    private static bool ShouldApplySpatialSubset(double[]? subset, double[]? datasetExtent)
    {
        if (subset is null)
        {
            return false;
        }

        if (datasetExtent is null || datasetExtent.Length < 4)
        {
            return true;
        }

        return !BoundingBoxesEqual(subset, datasetExtent);
    }

    private static bool BoundingBoxesEqual(double[] expected, double[] actual, double tolerance = 1e-6)
    {
        if (expected.Length < 4 || actual.Length < 4)
        {
            return false;
        }

        for (var i = 0; i < 4; i++)
        {
            if (Math.Abs(expected[i] - actual[i]) > tolerance)
            {
                return false;
            }
        }

        return true;
    }

    private static ((int Width, int Height)? ScaleSize, Dictionary<string, int>? ScaleAxes, string? Error) ParseScalingParameters(IQueryCollection query)
    {
        // Parse scalesize parameter (WCS 2.0 Scaling extension)
        // Format: scalesize=<axis1>(<size1>),<axis2>(<size2>)
        // Example: scalesize=i(800),j(600)
        var scaleSizeParam = QueryParsingHelpers.GetQueryValue(query, "scalesize");
        (int Width, int Height)? scaleSize = null;

        if (!scaleSizeParam.IsNullOrWhiteSpace())
        {
            var parts = scaleSizeParam.Split(',');
            if (parts.Length != 2)
            {
                return (null, null, "ScaleSize must specify both width and height: scalesize=i(800),j(600)");
            }

            int? width = null;
            int? height = null;

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^([ij])\((\d+)\)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    return (null, null, $"Invalid scalesize format: '{trimmed}'. Expected format: i(800) or j(600)");
                }

                var axis = match.Groups[1].Value.ToLowerInvariant();
                if (!int.TryParse(match.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) || size <= 0)
                {
                    return (null, null, $"Invalid scale size: {match.Groups[2].Value}. Must be a positive integer.");
                }

                if (axis == "i")
                {
                    width = size;
                }
                else if (axis == "j")
                {
                    height = size;
                }
            }

            if (!width.HasValue || !height.HasValue)
            {
                return (null, null, "ScaleSize must specify both i (width) and j (height) axes.");
            }

            scaleSize = (width.Value, height.Value);
        }

        // Parse scaleaxes parameter (alternative to scalesize)
        // Format: scaleaxes=<axis1>(<factor1>),<axis2>(<factor2>)
        // Example: scaleaxes=i(0.5),j(0.5)
        var scaleAxesParam = QueryParsingHelpers.GetQueryValue(query, "scaleaxes");
        Dictionary<string, int>? scaleAxes = null;

        if (!scaleAxesParam.IsNullOrWhiteSpace())
        {
            if (scaleSize.HasValue)
            {
                return (null, null, "Cannot specify both scalesize and scaleaxes parameters.");
            }

            scaleAxes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var parts = scaleAxesParam.Split(',');

            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                var match = System.Text.RegularExpressions.Regex.Match(trimmed, @"^([ij])\(([\d.]+)\)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    return (null, null, $"Invalid scaleaxes format: '{trimmed}'. Expected format: i(0.5) or j(2.0)");
                }

                var axis = match.Groups[1].Value.ToLowerInvariant();
                if (!double.TryParse(match.Groups[2].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var factor) || factor <= 0)
                {
                    return (null, null, $"Invalid scale factor: {match.Groups[2].Value}. Must be a positive number.");
                }

                // Convert factor to percentage for later use
                scaleAxes[axis] = (int)(factor * 100);
            }
        }

        return (scaleSize, scaleAxes, null);
    }

    private static CoverageData CreateFullCoverageData(
        RasterSourceLocation location,
        string contentType,
        IRasterSourceProviderRegistry sourceProviderRegistry)
    {
        if (location.IsLocalFile)
        {
            var info = new FileInfo(location.LocalPath!);
            var length = info.Exists ? info.Length : (long?)null;
            var lastModified = info.Exists ? info.LastWriteTimeUtc : (DateTimeOffset?)null;

            return new CoverageData(
                ct =>
                {
                    if (!File.Exists(location.LocalPath!))
                    {
                        throw new FileNotFoundException($"Coverage file not found: {location.LocalPath}");
                    }

                    var stream = new FileStream(
                        location.LocalPath!,
                        FileMode.Open,
                        FileAccess.Read,
                        FileShare.Read,
                        128 * 1024,
                        FileOptions.Asynchronous | FileOptions.SequentialScan);

                    return Task.FromResult<Stream>(stream);
                },
                contentType,
                length,
                lastModified,
                null);
        }

        return new CoverageData(
            ct => sourceProviderRegistry.OpenReadAsync(location.Uri, ct),
            contentType,
            null,
            null,
            null);
    }

    private static async Task<CoverageData> CreateSubsetCoverageAsync(
        RasterSourceLocation location,
        RasterDatasetDefinition dataset,
        double[]? spatialSubset,
        string? timeValue,
        string format,
        string? subsettingCrs,
        string? outputCrs,
        int nativeEpsg,
        (int Width, int Height)? scaleSize,
        Dictionary<string, int>? scaleAxes,
        string interpolationMethod,
        string? rangeSubset,
        IRasterSourceProviderRegistry sourceProviderRegistry,
        CancellationToken cancellationToken)
    {
        var (driver, extension) = ResolveDriver(format);

        Dataset? source = null;
        string? tempSourcePath = null;
        string? tempOutputPath = null;
        string? tempIntermediatePath = null;

        try
        {
            (source, tempSourcePath) = await OpenDatasetForProcessingAsync(location, sourceProviderRegistry, cancellationToken).ConfigureAwait(false);
            if (source is null)
            {
                throw new InvalidOperationException($"Unable to open raster source '{location.Uri}'.");
            }

            // Step 1: Use gdalwarp for CRS transformation if needed
            Dataset? intermediateDataset = null;
            var needsWarp = !outputCrs.IsNullOrWhiteSpace() || !subsettingCrs.IsNullOrWhiteSpace();

            try
            {
                if (needsWarp)
                {
                    tempIntermediatePath = Path.Combine(Path.GetTempPath(), $"honua-wcs-warp-{Guid.NewGuid():N}.tif");
                    intermediateDataset = await ApplyCrsTransformationAsync(
                        source,
                        tempSourcePath ?? location.PathForGdal,
                        tempIntermediatePath,
                        subsettingCrs,
                        outputCrs,
                        nativeEpsg,
                        spatialSubset,
                        interpolationMethod,
                        cancellationToken).ConfigureAwait(false);

                    // After warping, clear spatial subset since it was already applied
                    spatialSubset = null;
                }

                var datasetToTranslate = intermediateDataset ?? source;

                // Step 2: Apply scaling, band selection, and format conversion with gdal_translate
                var options = new List<string> { "-of", driver };

                if (spatialSubset is { Length: 4 })
                {
                    options.AddRange(new[]
                    {
                        "-projwin",
                        spatialSubset[0].ToString("G17", CultureInfo.InvariantCulture),
                        spatialSubset[3].ToString("G17", CultureInfo.InvariantCulture),
                        spatialSubset[2].ToString("G17", CultureInfo.InvariantCulture),
                        spatialSubset[1].ToString("G17", CultureInfo.InvariantCulture)
                    });
                }

                // Apply range subsetting (band selection)
                List<int> selectedBands;
                if (!rangeSubset.IsNullOrWhiteSpace())
                {
                    // Use range subsetting extension
                    if (!WcsRangeSubsettingHelper.TryParseRangeSubset(rangeSubset, datasetToTranslate.RasterCount, out selectedBands, out var rangeError))
                    {
                        throw new InvalidOperationException($"Invalid rangeSubset parameter: {rangeError}");
                    }
                }
                else
                {
                    // Fall back to temporal dimension for band selection
                    var bandIndex = ResolveTimeBandIndex(dataset, timeValue);
                    if (bandIndex.HasValue)
                    {
                        selectedBands = new List<int> { bandIndex.Value };
                    }
                    else
                    {
                        // No band selection, use all bands
                        selectedBands = Enumerable.Range(0, datasetToTranslate.RasterCount).ToList();
                    }
                }

                // Add band selection options to gdal_translate
                foreach (var bandIdx in selectedBands)
                {
                    options.Add("-b");
                    options.Add((bandIdx + 1).ToString(CultureInfo.InvariantCulture)); // GDAL uses 1-based band indices
                }

                // Apply interpolation method (WCS 2.0 Interpolation extension)
                options.Add("-r");
                options.Add(interpolationMethod);

                // Apply scaling if requested
                if (scaleSize.HasValue)
                {
                    options.Add("-outsize");
                    options.Add(scaleSize.Value.Width.ToString(CultureInfo.InvariantCulture));
                    options.Add(scaleSize.Value.Height.ToString(CultureInfo.InvariantCulture));
                }
                else if (scaleAxes != null)
                {
                    // Convert scale factors to percentages for gdal_translate
                    if (scaleAxes.TryGetValue("i", out var widthPercent) && scaleAxes.TryGetValue("j", out var heightPercent))
                    {
                        options.Add("-outsize");
                        options.Add($"{widthPercent}%");
                        options.Add($"{heightPercent}%");
                    }
                }

                tempOutputPath = Path.Combine(Path.GetTempPath(), $"honua-wcs-{Guid.NewGuid():N}{extension}");

                // Create translate options and perform translation
                using var translateOptions = new GDALTranslateOptions(options.ToArray());
                using (var translated = Gdal.wrapper_GDALTranslate(tempOutputPath, datasetToTranslate, translateOptions, null, null))
                {
                    if (translated is null)
                    {
                        var message = Gdal.GetLastErrorMsg();
                        throw new InvalidOperationException(message.IsNullOrWhiteSpace()
                            ? "Failed to generate coverage subset."
                            : $"Failed to generate coverage subset: {message}");
                    }

                    // Flush and explicitly dispose the translated dataset to ensure file is written and closed
                    translated.FlushCache();

                    // Explicitly dispose before accessing file to ensure GDAL releases all handles
                    // Note: The using statement will also call Dispose, but explicit call ensures
                    // immediate cleanup and file handle release before we validate the output file
                }

                var info = new FileInfo(tempOutputPath);
                if (!info.Exists || info.Length == 0)
                {
                    throw new InvalidOperationException("Generated coverage subset is empty.");
                }

                var cleanupTargets = new[]
                    {
                        tempOutputPath,
                        tempIntermediatePath,
                        tempSourcePath
                    }
                    .Where(path => path.HasValue())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToArray();

                return new CoverageData(
                    ct =>
                    {
                        var stream = new FileStream(
                            tempOutputPath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            128 * 1024,
                            FileOptions.Asynchronous | FileOptions.SequentialScan);

                        return Task.FromResult<Stream>(stream);
                    },
                    format,
                    info.Length,
                    info.LastWriteTimeUtc,
                    () =>
                    {
                        foreach (var path in cleanupTargets)
                        {
                            TryDelete(path);
                        }
                    });
            }
            finally
            {
                intermediateDataset?.Dispose();
            }
        }
        catch
        {
            TryDelete(tempOutputPath);
            TryDelete(tempIntermediatePath);
            TryDelete(tempSourcePath);
            throw;
        }
        finally
        {
            source?.Dispose();
        }
    }

    private static Task<Dataset?> ApplyCrsTransformationAsync(
        Dataset source,
        string sourcePath,
        string outputPath,
        string? subsettingCrs,
        string? outputCrs,
        int nativeEpsg,
        double[]? spatialSubset,
        string interpolationMethod,
        CancellationToken cancellationToken)
    {
        var warpOptions = new List<string>();

        // Determine source CRS for bbox interpretation
        int subsettingEpsg = nativeEpsg;
        if (!subsettingCrs.IsNullOrWhiteSpace() && WcsCrsHelper.TryParseCrsUri(subsettingCrs, out var parsedSubsettingEpsg))
        {
            subsettingEpsg = parsedSubsettingEpsg;
        }

        // Determine target CRS for output
        int targetEpsg = nativeEpsg;
        if (!outputCrs.IsNullOrWhiteSpace() && WcsCrsHelper.TryParseCrsUri(outputCrs, out var parsedOutputEpsg))
        {
            targetEpsg = parsedOutputEpsg;
        }

        // Add target SRS
        warpOptions.Add("-t_srs");
        warpOptions.Add($"EPSG:{targetEpsg}");

        // Add source SRS if different from native
        if (subsettingEpsg != nativeEpsg)
        {
            warpOptions.Add("-s_srs");
            warpOptions.Add($"EPSG:{nativeEpsg}");
        }

        // Apply spatial subset if provided
        if (spatialSubset is { Length: 4 })
        {
            // Transform bbox from subsetting CRS to native CRS if needed
            var bbox = spatialSubset;
            if (subsettingEpsg != nativeEpsg)
            {
                var (minX, minY, maxX, maxY) = Core.Data.CrsTransform.TransformEnvelope(
                    spatialSubset[0], spatialSubset[1], spatialSubset[2], spatialSubset[3],
                    subsettingEpsg, nativeEpsg);
                bbox = new[] { minX, minY, maxX, maxY };
            }

            warpOptions.Add("-te");
            warpOptions.Add(bbox[0].ToString("G17", CultureInfo.InvariantCulture));
            warpOptions.Add(bbox[1].ToString("G17", CultureInfo.InvariantCulture));
            warpOptions.Add(bbox[2].ToString("G17", CultureInfo.InvariantCulture));
            warpOptions.Add(bbox[3].ToString("G17", CultureInfo.InvariantCulture));

            // Specify the CRS for the extent
            if (subsettingEpsg != nativeEpsg)
            {
                warpOptions.Add("-te_srs");
                warpOptions.Add($"EPSG:{subsettingEpsg}");
            }
        }

        // Use specified interpolation method (WCS 2.0 Interpolation extension)
        warpOptions.Add("-r");
        warpOptions.Add(interpolationMethod);

        // Perform warping using GDALWarp
        using var warpOptionsObj = new GDALWarpAppOptions(warpOptions.ToArray());
        using (var warped = Gdal.Warp(outputPath, new[] { source }, warpOptionsObj, null, null))
        {
            if (warped is null)
            {
                var message = Gdal.GetLastErrorMsg();
                throw new InvalidOperationException(message.IsNullOrWhiteSpace()
                    ? "Failed to apply CRS transformation."
                    : $"Failed to apply CRS transformation: {message}");
            }

            warped.FlushCache();
        }

        // Open the warped file and return the dataset
        var result = Gdal.Open(outputPath, Access.GA_ReadOnly);
        if (result is null)
        {
            throw new InvalidOperationException("Failed to open warped coverage.");
        }

        return Task.FromResult<Dataset?>(result);
    }

    private static (string Driver, string Extension) ResolveDriver(string format)
    {
        return format switch
        {
            "image/tiff" => ("GTiff", ".tif"),
            "image/png" => ("PNG", ".png"),
            "image/jpeg" => ("JPEG", ".jpg"),
            "image/jp2" => ("JP2OpenJPEG", ".jp2"),
            "application/netcdf" => ("netCDF", ".nc"),
            "application/x-hdf" => ("HDF5", ".h5"),
            _ => throw new InvalidOperationException($"Format '{format}' is not supported.")
        };
    }

    private static int? ResolveTimeBandIndex(RasterDatasetDefinition dataset, string? timeValue)
    {
        if (!dataset.Temporal.Enabled || timeValue.IsNullOrWhiteSpace())
        {
            return null;
        }

        if (dataset.Temporal.FixedValues is { Count: > 0 })
        {
            for (var i = 0; i < dataset.Temporal.FixedValues.Count; i++)
            {
                if (dataset.Temporal.FixedValues[i].EqualsIgnoreCase(timeValue))
                {
                    return i;
                }
            }

            throw new InvalidOperationException(
                $"TIME value '{timeValue}' is not in the allowed set: {string.Join(", ", dataset.Temporal.FixedValues)}");
        }

        throw new InvalidOperationException("Temporal subsets are only supported for datasets with discrete fixed values.");
    }

    private static async Task<(Dataset? Dataset, string? TempFile)> OpenDatasetForProcessingAsync(
        RasterSourceLocation location,
        IRasterSourceProviderRegistry sourceProviderRegistry,
        CancellationToken cancellationToken)
    {
        var dataset = Gdal.Open(location.PathForGdal, Access.GA_ReadOnly);
        if (dataset is not null)
        {
            return (dataset, null);
        }

        if (!location.IsLocalFile)
        {
            var tempFile = await DownloadRasterToTempFileAsync(sourceProviderRegistry, location.Uri, cancellationToken).ConfigureAwait(false);
            dataset = Gdal.Open(tempFile, Access.GA_ReadOnly);
            if (dataset is not null)
            {
                return (dataset, tempFile);
            }

            TryDelete(tempFile);
        }

        return (null, null);
    }

    private static async Task<string> DownloadRasterToTempFileAsync(
        IRasterSourceProviderRegistry sourceProviderRegistry,
        string uri,
        CancellationToken cancellationToken)
    {
        var extension = ResolveTempExtension(uri);
        var tempPath = Path.Combine(Path.GetTempPath(), $"honua-wcs-src-{Guid.NewGuid():N}{extension}");

        await using var source = await sourceProviderRegistry.OpenReadAsync(uri, cancellationToken).ConfigureAwait(false);
        await using (var destination = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            await source.CopyToAsync(destination, 128 * 1024, cancellationToken).ConfigureAwait(false);
        }

        return tempPath;
    }

    private static string ResolveTempExtension(string uri)
    {
        try
        {
            if (Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            {
                var extension = Path.GetExtension(parsed.AbsolutePath);
                if (extension.HasValue())
                {
                    return extension;
                }
            }
        }
        catch
        {
            // ignored
        }

        var fallback = Path.GetExtension(uri);
        return fallback.IsNullOrWhiteSpace() ? ".tmp" : fallback;
    }

    private static void TryDelete(string? path)
    {
        if (path.IsNullOrWhiteSpace())
        {
            return;
        }

        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignored
        }
    }

    private sealed record CoverageData(
        Func<CancellationToken, Task<Stream>> StreamFactory,
        string ContentType,
        long? ContentLength,
        DateTimeOffset? LastModified,
        Action? OnCompleted);

    private static IResult CreateCoverageResultWithCdn(CoverageData coverage, RasterCdnDefinition cdnDefinition)
    {
        if (!cdnDefinition.Enabled)
        {
            return new CoverageStreamResult(coverage, null);
        }

        var policy = CdnCachePolicy.FromRasterDefinition(cdnDefinition);
        var cacheControl = policy.ToCacheControlHeader();
        return new CoverageStreamResult(coverage, cacheControl);
    }

    private sealed class CoverageStreamResult : IResult
    {
        private readonly CoverageData coverage;
        private readonly string? cacheControl;

        public CoverageStreamResult(CoverageData coverage, string? cacheControl)
        {
            this.coverage = coverage;
            this.cacheControl = cacheControl;
        }

        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.ContentType = this.coverage.ContentType;

            if (!this.cacheControl.IsNullOrEmpty())
            {
                httpContext.Response.Headers.CacheControl = _cacheControl;
            }

            httpContext.Response.Headers.Vary = "Accept-Encoding";

            if (this.coverage.LastModified.HasValue)
            {
                httpContext.Response.Headers.LastModified = this.coverage.LastModified.Value.ToString("R", CultureInfo.InvariantCulture);
            }

            if (this.coverage.ContentLength.HasValue)
            {
                httpContext.Response.ContentLength = this.coverage.ContentLength.Value;
            }

            Stream? stream = null;
            try
            {
                stream = await this.coverage.StreamFactory(httpContext.RequestAborted).ConfigureAwait(false);
                await using (stream)
                {
                    await stream.CopyToAsync(httpContext.Response.Body, 64 * 1024, httpContext.RequestAborted).ConfigureAwait(false);
                }
            }
            finally
            {
                this.coverage.OnCompleted?.Invoke();
            }
        }
    }

    private static XElement BuildWcsTemporalDomain(RasterTemporalDefinition temporal)
    {
        var domainSet = new XElement(Gml + "DomainSet");

        if (temporal.FixedValues is { Count: > 0 })
        {
            foreach (var value in temporal.FixedValues)
            {
                domainSet.Add(new XElement(Gml + "TimeInstant",
                    new XAttribute(Gml + "id", $"time-{value.Replace(":", "-")}"),
                    new XElement(Gml + "timePosition", value)
                ));
            }
        }
        else if (temporal.MinValue.HasValue() && temporal.MaxValue.HasValue())
        {
            domainSet.Add(new XElement(Gml + "TimePeriod",
                new XAttribute(Gml + "id", "time-period"),
                new XElement(Gml + "beginPosition", temporal.MinValue),
                new XElement(Gml + "endPosition", temporal.MaxValue)
            ));
        }

        return domainSet;
    }

    private static string? ValidateTimeParameter(string? timeValue, RasterTemporalDefinition temporal)
    {
        // Use default if no time specified
        if (timeValue.IsNullOrWhiteSpace())
        {
            return temporal.DefaultValue;
        }

        // If fixed values are specified, validate against them
        if (temporal.FixedValues is { Count: > 0 })
        {
            if (!temporal.FixedValues.Contains(timeValue, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"TIME value '{timeValue}' is not in the allowed set: {string.Join(", ", temporal.FixedValues)}");
            }
            return timeValue;
        }

        // If range is specified, validate within bounds
        if (temporal.MinValue.HasValue() && temporal.MaxValue.HasValue())
        {
            if (string.CompareOrdinal(timeValue, temporal.MinValue) < 0 || string.CompareOrdinal(timeValue, temporal.MaxValue) > 0)
            {
                throw new InvalidOperationException($"TIME value '{timeValue}' is outside the valid range: {temporal.MinValue} to {temporal.MaxValue}");
            }
        }

        return timeValue;
    }

    private static IResult CreateExceptionReport(string exceptionCode, string? locator, string exceptionText)
    {
        var exception = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement(Ows + "ExceptionReport",
                new XAttribute("version", "2.0.0"),
                new XAttribute(XNamespace.Xmlns + "ows", Ows),
                new XAttribute(XNamespace.Xmlns + "xsi", Xsi),
                new XAttribute(Xsi + "schemaLocation", "http://www.opengis.net/ows/2.0 http://schemas.opengis.net/ows/2.0/owsExceptionReport.xsd"),
                new XElement(Ows + "Exception",
                    new XAttribute("exceptionCode", exceptionCode),
                    locator != null ? new XAttribute("locator", locator) : null,
                    new XElement(Ows + "ExceptionText", exceptionText)
                )
            )
        );

        return Results.Content(exception.ToString(), "application/xml; charset=utf-8", statusCode: 400);
    }

    private sealed record RasterSourceLocation(string Uri, string? LocalPath, bool IsLocalFile)
    {
        public string PathForGdal => IsLocalFile ? LocalPath! : Uri;
    }

    /// <summary>
    /// Validates a raster source path and enforces the configured allow list.
    /// Returns a normalized local path or the original remote URI.
    /// </summary>
    private static RasterSourceLocation? ValidateRasterPath(
        string requestedPath,
        ServerSecurityDefinition security,
        out string? error)
    {
        error = null;

        if (requestedPath.IsNullOrWhiteSpace())
        {
            error = "Coverage file path is empty.";
            return null;
        }

        try
        {
            if (Uri.TryCreate(requestedPath, UriKind.Absolute, out var uri))
            {
                if (uri.IsFile)
                {
                    return ValidateLocalPath(uri.LocalPath, security);
                }

                if (IsRemoteScheme(uri.Scheme))
                {
                    return new RasterSourceLocation(requestedPath, null, false);
                }
            }

            return ValidateLocalPath(requestedPath, security);
        }
        catch (UnauthorizedAccessException ex)
        {
            error = $"Access to coverage file is forbidden: {ex.Message}";
        }
        catch (ArgumentException ex)
        {
            error = $"Invalid coverage file path: {ex.Message}";
        }
        catch (NotSupportedException ex)
        {
            error = $"Unsupported coverage file path: {ex.Message}";
        }
        catch (Exception ex)
        {
            error = $"Error validating coverage file path: {ex.Message}";
        }

        return null;
    }

    private static RasterSourceLocation ValidateLocalPath(string path, ServerSecurityDefinition security)
    {
        if (security.AllowedRasterDirectories.Count == 0)
        {
            throw new UnauthorizedAccessException(
                "Raster directory allow list is not configured. Refusing to serve local raster sources.");
        }

        var normalized = SecurePathValidator.ValidatePathMultiple(path, security.AllowedRasterDirectories.ToArray());
        return new RasterSourceLocation(normalized, normalized, true);
    }

    private static bool IsRemoteScheme(string? scheme)
    {
        if (scheme.IsNullOrWhiteSpace())
        {
            return false;
        }

        return scheme.EqualsIgnoreCase(Uri.UriSchemeHttp) ||
               scheme.EqualsIgnoreCase(Uri.UriSchemeHttps) ||
               scheme.EqualsIgnoreCase("s3") ||
               scheme.EqualsIgnoreCase("gs") ||
               scheme.EqualsIgnoreCase("gcs") ||
               scheme.EqualsIgnoreCase("azureblob") ||
               scheme.EqualsIgnoreCase("az");
    }
}
