// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Providers.OpenStreetMap;

/// <summary>
/// Nominatim (OpenStreetMap) implementation of geocoding provider.
/// API Reference: https://nominatim.org/release-docs/latest/api/Overview/
/// </summary>
public class NominatimGeocodingProvider : IGeocodingProvider
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<NominatimGeocodingProvider> _logger;
    private readonly string _baseUrl;
    private readonly string _userAgent;

    public string ProviderKey => "nominatim";
    public string ProviderName => "Nominatim (OpenStreetMap)";

    public NominatimGeocodingProvider(
        HttpClient httpClient,
        ILogger<NominatimGeocodingProvider> logger,
        string? baseUrl = null,
        string? userAgent = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _baseUrl = baseUrl ?? "https://nominatim.openstreetmap.org";
        _userAgent = userAgent ?? "HonuaServer/1.0";

        // Nominatim requires a User-Agent header
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent", _userAgent);
        }

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(_baseUrl);
        }
    }

    public async Task<GeocodingResponse> GeocodeAsync(
        GeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = BuildSearchUrl(request);
            _logger.LogDebug("Nominatim geocoding request: {Url}", url);

            // Nominatim rate limiting: max 1 request per second
            await Task.Delay(1000, cancellationToken);

            var results = await _httpClient.GetFromJsonAsync<NominatimSearchResult[]>(
                url,
                cancellationToken);

            if (results == null)
            {
                results = Array.Empty<NominatimSearchResult>();
            }

            return MapToGeocodingResponse(results);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Nominatim geocoding request failed for query: {Query}", request.Query);
            throw new InvalidOperationException($"Nominatim geocoding failed: {ex.Message}", ex);
        }
    }

    public async Task<GeocodingResponse> ReverseGeocodeAsync(
        ReverseGeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = BuildReverseUrl(request);
            _logger.LogDebug("Nominatim reverse geocoding request: {Url}", url);

            // Nominatim rate limiting: max 1 request per second
            await Task.Delay(1000, cancellationToken);

            var result = await _httpClient.GetFromJsonAsync<NominatimSearchResult>(
                url,
                cancellationToken);

            var results = result != null ? new[] { result } : Array.Empty<NominatimSearchResult>();
            return MapToGeocodingResponse(results);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Nominatim reverse geocoding failed for location: {Lon}, {Lat}",
                request.Longitude, request.Latitude);
            throw new InvalidOperationException($"Nominatim reverse geocoding failed: {ex.Message}", ex);
        }
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var testRequest = new GeocodingRequest
            {
                Query = "OpenStreetMap Foundation"
            };

            await GeocodeAsync(testRequest, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Nominatim connectivity test failed");
            return false;
        }
    }

    private string BuildSearchUrl(GeocodingRequest request)
    {
        var queryParams = new List<string>
        {
            $"q={Uri.EscapeDataString(request.Query)}",
            "format=json",
            "addressdetails=1"
        };

        if (request.MaxResults.HasValue)
        {
            queryParams.Add($"limit={request.MaxResults.Value}");
        }

        if (request.CountryCodes?.Length > 0)
        {
            queryParams.Add($"countrycodes={string.Join(",", request.CountryCodes)}");
        }

        if (request.BoundingBox?.Length == 4)
        {
            var bbox = request.BoundingBox;
            // Nominatim expects: left,top,right,bottom (min_lon,max_lat,max_lon,min_lat)
            queryParams.Add($"viewbox={bbox[0]},{bbox[3]},{bbox[2]},{bbox[1]}");
            queryParams.Add("bounded=1");
        }

        if (!string.IsNullOrEmpty(request.Language))
        {
            queryParams.Add($"accept-language={request.Language}");
        }

        return $"/search?{string.Join("&", queryParams)}";
    }

    private string BuildReverseUrl(ReverseGeocodingRequest request)
    {
        var queryParams = new List<string>
        {
            $"lat={request.Latitude.ToString(CultureInfo.InvariantCulture)}",
            $"lon={request.Longitude.ToString(CultureInfo.InvariantCulture)}",
            "format=json",
            "addressdetails=1"
        };

        if (!string.IsNullOrEmpty(request.Language))
        {
            queryParams.Add($"accept-language={request.Language}");
        }

        return $"/reverse?{string.Join("&", queryParams)}";
    }

    private GeocodingResponse MapToGeocodingResponse(NominatimSearchResult[] results)
    {
        var mappedResults = results.Select(r => new GeocodingResult
        {
            FormattedAddress = r.DisplayName ?? string.Empty,
            Longitude = double.Parse(r.Lon ?? "0", CultureInfo.InvariantCulture),
            Latitude = double.Parse(r.Lat ?? "0", CultureInfo.InvariantCulture),
            Type = r.Type,
            Confidence = r.Importance,
            BoundingBox = r.Boundingbox?.Length == 4 ? new[]
            {
                double.Parse(r.Boundingbox[2], CultureInfo.InvariantCulture), // west
                double.Parse(r.Boundingbox[0], CultureInfo.InvariantCulture), // south
                double.Parse(r.Boundingbox[3], CultureInfo.InvariantCulture), // east
                double.Parse(r.Boundingbox[1], CultureInfo.InvariantCulture)  // north
            } : null,
            Components = r.Address != null ? new AddressComponents
            {
                HouseNumber = r.Address.HouseNumber,
                Street = r.Address.Road,
                City = r.Address.City ?? r.Address.Town ?? r.Address.Village,
                State = r.Address.State,
                PostalCode = r.Address.Postcode,
                Country = r.Address.Country,
                CountryCode = r.Address.CountryCode,
                County = r.Address.County,
                District = r.Address.Suburb ?? r.Address.District
            } : null,
            Metadata = new Dictionary<string, object>
            {
                ["placeId"] = r.PlaceId ?? 0,
                ["osmType"] = r.OsmType ?? "unknown",
                ["osmId"] = r.OsmId ?? 0,
                ["class"] = r.Class ?? "unknown"
            }
        }).ToList();

        return new GeocodingResponse
        {
            Results = mappedResults,
            Attribution = "© OpenStreetMap contributors"
        };
    }

    #region Nominatim Response Models

    private class NominatimSearchResult
    {
        public long? PlaceId { get; set; }
        public string? OsmType { get; set; }
        public long? OsmId { get; set; }
        public string? Lat { get; set; }
        public string? Lon { get; set; }
        public string? Class { get; set; }
        public string? Type { get; set; }
        public string? DisplayName { get; set; }
        public double? Importance { get; set; }
        public string[]? Boundingbox { get; set; }
        public NominatimAddress? Address { get; set; }
    }

    private class NominatimAddress
    {
        public string? HouseNumber { get; set; }
        public string? Road { get; set; }
        public string? Suburb { get; set; }
        public string? District { get; set; }
        public string? City { get; set; }
        public string? Town { get; set; }
        public string? Village { get; set; }
        public string? County { get; set; }
        public string? State { get; set; }
        public string? Postcode { get; set; }
        public string? Country { get; set; }
        public string? CountryCode { get; set; }
    }

    #endregion
}
