// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using Honua.Server.Core.Data;
using Honua.Server.Core.Extensions;
using Honua.Server.Core.Metadata;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Honua.Server.Host.Ogc.Services;

/// <summary>
/// Service for handling CRS (Coordinate Reference System) resolution, validation, and negotiation
/// for OGC API requests.
/// </summary>
internal sealed class OgcCrsService
{
    /// <summary>
    /// Resolves the Accept-Crs header from a request, performing content negotiation with quality values.
    /// </summary>
    /// <param name="request">The HTTP request containing Accept-Crs header.</param>
    /// <param name="supported">Collection of supported CRS identifiers.</param>
    /// <returns>The resolved CRS identifier and optional error result if negotiation fails.</returns>
    internal (string? Value, IResult? Error) ResolveAcceptCrs(HttpRequest request, IReadOnlyCollection<string> supported)
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

    /// <summary>
    /// Resolves the content CRS from a requested CRS parameter, validating against supported CRS list.
    /// </summary>
    /// <param name="requestedCrs">The requested CRS identifier.</param>
    /// <param name="service">The service definition.</param>
    /// <param name="layer">The layer definition.</param>
    /// <returns>The resolved CRS identifier and optional error result if validation fails.</returns>
    internal (string Value, IResult? Error) ResolveContentCrs(string? requestedCrs, ServiceDefinition service, LayerDefinition layer)
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
            var error = OgcProblemDetails.Create(
                "Invalid parameter value",
                $"Requested CRS '{requestedCrs}' is not supported. Supported CRS values: {supportedList}.",
                StatusCodes.Status400BadRequest,
                "crs");
            return (string.Empty, Results.Json(error, statusCode: StatusCodes.Status400BadRequest));
        }

        return (match, null);
    }

    /// <summary>
    /// Resolves the complete list of supported CRS identifiers for a service and layer combination.
    /// </summary>
    /// <param name="service">The service definition.</param>
    /// <param name="layer">The layer definition.</param>
    /// <returns>A read-only list of supported CRS identifiers.</returns>
    internal IReadOnlyList<string> ResolveSupportedCrs(ServiceDefinition service, LayerDefinition layer)
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

        // Priority: Layer CRS, Service default CRS, Service additional CRS, fallback to WGS84
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

    /// <summary>
    /// Determines the default CRS for a service based on configuration and supported CRS list.
    /// </summary>
    /// <param name="service">The service definition.</param>
    /// <param name="supported">List of supported CRS identifiers.</param>
    /// <returns>The default CRS identifier.</returns>
    internal string DetermineDefaultCrs(ServiceDefinition service, IReadOnlyList<string> supported)
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

    /// <summary>
    /// Determines the storage CRS for a layer based on its SRID or configured CRS list.
    /// </summary>
    /// <param name="layer">The layer definition.</param>
    /// <returns>The storage CRS identifier.</returns>
    internal string DetermineStorageCrs(LayerDefinition layer)
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

    /// <summary>
    /// Builds the default CRS list for a service based on its configuration.
    /// </summary>
    /// <param name="service">The service definition.</param>
    /// <returns>A read-only list containing the default CRS identifier.</returns>
    internal IReadOnlyList<string> BuildDefaultCrs(ServiceDefinition service)
    {
        if (service.Ogc.DefaultCrs.HasValue())
        {
            return new[] { CrsHelper.NormalizeIdentifier(service.Ogc.DefaultCrs!) };
        }

        return new[] { CrsHelper.DefaultCrsIdentifier };
    }

    /// <summary>
    /// Formats a CRS identifier for use in Content-Crs header.
    /// </summary>
    /// <param name="value">The CRS identifier to format.</param>
    /// <returns>The formatted CRS identifier, or empty string if value is null/whitespace.</returns>
    internal string FormatContentCrs(string? value)
    {
        if (value.IsNullOrWhiteSpace())
        {
            return string.Empty;
        }

        return $"<{value}>";
    }
}
