// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

// This file contains CRS (Coordinate Reference System) resolution and handling methods.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.Attachments;
using Honua.Server.Core.Data;
using Honua.Server.Core.Editing;
using Honua.Server.Core.Export;
using Honua.Server.Core.Metadata;
using Honua.Server.Core.Observability;
using Honua.Server.Core.Performance;
using Honua.Server.Core.Query;
using Honua.Server.Core.Query.Expressions;
using Honua.Server.Core.Query.Filter;
using Honua.Server.Core.Raster;
using Honua.Server.Core.Raster.Export;
using Honua.Server.Core.Results;
using Honua.Server.Core.Serialization;
using Honua.Server.Core.Styling;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Extensions;
using Honua.Server.Host.Raster;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO;

namespace Honua.Server.Host.Ogc;

internal static partial class OgcSharedHandlers
{
    internal static (string? Value, IResult? Error) ResolveAcceptCrs(HttpRequest request, IReadOnlyCollection<string> supported)
    {
        if (!request.Headers.TryGetValue("Accept-Crs", out var headerValues) || headerValues.Count == 0)
        {
            return (null, null);
        }

        var candidates = new List<(string Crs, double Quality)>();
        foreach (var header in headerValues)
        {
            if (header.IsNullOrWhiteSpace())
            {
                continue;
            }

            foreach (var token in QueryParsingHelpers.ParseCsv(header))
            {
                var semicolonIndex = token.IndexOf(';');
                var crsToken = semicolonIndex >= 0 ? token[..semicolonIndex] : token;
                var quality = 1.0;

                if (semicolonIndex >= 0)
                {
                    var parameters = token[(semicolonIndex + 1)..]
                        .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                    foreach (var parameter in parameters)
                    {
                        var parts = parameter.Split('=', 2, StringSplitOptions.TrimEntries);
                        if (parts.Length == 2 && string.Equals(parts[0], "q", StringComparison.OrdinalIgnoreCase) &&
                            parts[1].TryParseDoubleStrict(out var parsedQ))
                        {
                            quality = parsedQ;
                        }
                    }
                }

                candidates.Add((CrsHelper.NormalizeIdentifier(crsToken), quality));
            }
        }

        if (candidates.Count == 0)
        {
            return (null, null);
        }

        foreach (var candidate in candidates
                     .OrderByDescending(item => item.Quality)
                     .ThenBy(item => item.Crs, StringComparer.OrdinalIgnoreCase))
        {
            if (supported.Any(value => string.Equals(value, candidate.Crs, StringComparison.OrdinalIgnoreCase)))
            {
                return (candidate.Crs, null);
            }
        }

        return (null, Results.StatusCode(StatusCodes.Status406NotAcceptable));
    }
    internal static (string Value, IResult? Error) ResolveContentCrs(string? requestedCrs, ServiceDefinition service, LayerDefinition layer)
    {
        var supported = ResolveSupportedCrs(service, layer);
        var defaultCrs = DetermineDefaultCrs(service, supported);

        if (requestedCrs.IsNullOrWhiteSpace())
        {
            return (defaultCrs, null);
        }

        var normalizedRequested = CrsHelper.NormalizeIdentifier(requestedCrs);
        var match = supported.FirstOrDefault(crs => string.Equals(crs, normalizedRequested, StringComparison.OrdinalIgnoreCase));
        if (match is null)
        {
            var supportedList = string.Join(", ", supported);
            return (string.Empty, CreateValidationProblem($"Requested CRS '{requestedCrs}' is not supported. Supported CRS values: {supportedList}.", "crs"));
        }

        return (match, null);
    }

    internal static IReadOnlyList<string> ResolveSupportedCrs(ServiceDefinition service, LayerDefinition layer)
    {
        var supported = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddValue(string? value)
        {
            if (value.IsNullOrWhiteSpace())
            {
                return;
            }

            var normalized = CrsHelper.NormalizeIdentifier(value);
            if (seen.Add(normalized))
            {
                supported.Add(normalized);
            }
        }

        foreach (var crs in layer.Crs)
        {
            AddValue(crs);
        }

        AddValue(service.Ogc.DefaultCrs);

        foreach (var crs in service.Ogc.AdditionalCrs)
        {
            AddValue(crs);
        }

        if (supported.Count == 0)
        {
            AddValue(CrsHelper.DefaultCrsIdentifier);
        }

        return supported;
    }

    internal static string DetermineDefaultCrs(ServiceDefinition service, IReadOnlyList<string> supported)
    {
        if (supported.Count == 0)
        {
            return CrsHelper.DefaultCrsIdentifier;
        }

        if (service.Ogc.DefaultCrs.HasValue())
        {
            var normalizedDefault = CrsHelper.NormalizeIdentifier(service.Ogc.DefaultCrs);
            var match = supported.FirstOrDefault(crs => string.Equals(crs, normalizedDefault, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
            {
                return match;
            }
        }

        return supported[0];
    }

    internal static string DetermineStorageCrs(LayerDefinition layer)
    {
        if (layer.Storage?.Srid is int srid && srid > 0)
        {
            return CrsHelper.NormalizeIdentifier($"EPSG:{srid}");
        }

        if (layer.Crs.Count > 0)
        {
            return CrsHelper.NormalizeIdentifier(layer.Crs[0]);
        }

        return CrsHelper.DefaultCrsIdentifier;
    }

    private static (BoundingBox? Value, IResult? Error) ParseBoundingBox(string? raw)
    {
        // Note: bbox-crs is parsed separately in ParseItemsQuery and set on the BoundingBox later
        var (bbox, error) = QueryParameterHelper.ParseBoundingBox(raw, crs: null);
        if (error is not null)
        {
            return (null, CreateValidationProblem(error, "bbox"));
        }

        return (bbox, null);
    }

    private static (TemporalInterval? Value, IResult? Error) ParseTemporal(string? raw)
    {
        var (interval, error) = QueryParameterHelper.ParseTemporalRange(raw);
        if (error is not null)
        {
            return (null, CreateValidationProblem(error, "datetime"));
        }

        return (interval, null);
    }

    private static (FeatureResultType Value, IResult? Error) ParseResultType(string? raw)
    {
        var (resultType, error) = QueryParameterHelper.ParseResultType(raw, FeatureResultType.Results);
        if (error is not null)
        {
            return (FeatureResultType.Results, CreateValidationProblem(error, "resultType"));
        }

        return (resultType, null);
    }

    private static IReadOnlyList<string> ParseList(string? raw)
    {
        if (raw.IsNullOrWhiteSpace())
        {
            return Array.Empty<string>();
        }

        var values = QueryParsingHelpers.ParseCsv(raw);
        return values.Count == 0 ? Array.Empty<string>() : values;
    }

    internal static IReadOnlyList<string> BuildDefaultCrs(ServiceDefinition service)
    {
        if (service.Ogc.DefaultCrs.HasValue())
        {
            return new[] { CrsHelper.NormalizeIdentifier(service.Ogc.DefaultCrs!) };
        }

        return new[] { CrsHelper.DefaultCrsIdentifier };
    }
    internal static string FormatContentCrs(string? value)
        => value.IsNullOrWhiteSpace() ? string.Empty : $"<{value}>";

    /// <summary>
    /// Adds a Content-Crs header to the result with proper formatting.
    /// This consolidates the common pattern of calling WithResponseHeader + FormatContentCrs.
    /// </summary>
    internal static IResult WithContentCrsHeader(IResult result, string? contentCrs)
        => WithResponseHeader(result, "Content-Crs", FormatContentCrs(contentCrs));
}
