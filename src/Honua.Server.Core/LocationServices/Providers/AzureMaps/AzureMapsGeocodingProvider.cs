// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Providers.AzureMaps;

/// <summary>
/// Azure Maps implementation of geocoding provider.
/// API Reference: https://learn.microsoft.com/en-us/rest/api/maps/search
/// </summary>
public class AzureMapsGeocodingProvider : IGeocodingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _subscriptionKey;
    private readonly ILogger<AzureMapsGeocodingProvider> _logger;
    private const string BaseUrl = "https://atlas.microsoft.com";
    private const string ApiVersion = "2025-01-01";

    public string ProviderKey => "azure-maps";
    public string ProviderName => "Azure Maps Geocoding";

    public AzureMapsGeocodingProvider(
        HttpClient httpClient,
        string subscriptionKey,
        ILogger<AzureMapsGeocodingProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _subscriptionKey = subscriptionKey ?? throw new ArgumentNullException(nameof(subscriptionKey));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_httpClient.BaseAddress == null)
        {
            _httpClient.BaseAddress = new Uri(BaseUrl);
        }
    }

    public async Task<GeocodingResponse> GeocodeAsync(
        GeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = BuildGeocodeUrl(request);
            _logger.LogDebug("Azure Maps geocoding request: {Url}", url);

            var response = await _httpClient.GetFromJsonAsync<AzureMapsSearchResponse>(
                url,
                cancellationToken);

            if (response == null)
            {
                throw new InvalidOperationException("Azure Maps returned null response");
            }

            return MapToGeocodingResponse(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Azure Maps geocoding request failed for query: {Query}", request.Query);
            throw new InvalidOperationException($"Azure Maps geocoding failed: {ex.Message}", ex);
        }
    }

    public async Task<GeocodingResponse> ReverseGeocodeAsync(
        ReverseGeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = BuildReverseGeocodeUrl(request);
            _logger.LogDebug("Azure Maps reverse geocoding request: {Url}", url);

            var response = await _httpClient.GetFromJsonAsync<AzureMapsSearchResponse>(
                url,
                cancellationToken);

            if (response == null)
            {
                throw new InvalidOperationException("Azure Maps returned null response");
            }

            return MapToGeocodingResponse(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Azure Maps reverse geocoding failed for location: {Lon}, {Lat}",
                request.Longitude, request.Latitude);
            throw new InvalidOperationException($"Azure Maps reverse geocoding failed: {ex.Message}", ex);
        }
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple test with a known location (Microsoft HQ)
            var testRequest = new ReverseGeocodingRequest
            {
                Longitude = -122.13493,
                Latitude = 47.64358
            };

            await ReverseGeocodeAsync(testRequest, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Azure Maps connectivity test failed");
            return false;
        }
    }

    private string BuildGeocodeUrl(GeocodingRequest request)
    {
        var queryParams = new List<string>
        {
            $"api-version={ApiVersion}",
            $"query={Uri.EscapeDataString(request.Query)}",
            $"subscription-key={_subscriptionKey}"
        };

        if (request.MaxResults.HasValue)
        {
            queryParams.Add($"limit={request.MaxResults.Value}");
        }

        if (request.CountryCodes?.Length > 0)
        {
            queryParams.Add($"countrySet={string.Join(",", request.CountryCodes)}");
        }

        if (request.BoundingBox?.Length == 4)
        {
            var bbox = request.BoundingBox;
            queryParams.Add($"bbox={bbox[0]},{bbox[1]},{bbox[2]},{bbox[3]}");
        }

        if (!string.IsNullOrEmpty(request.Language))
        {
            queryParams.Add($"language={request.Language}");
        }

        return $"/search/address/json?{string.Join("&", queryParams)}";
    }

    private string BuildReverseGeocodeUrl(ReverseGeocodingRequest request)
    {
        var queryParams = new List<string>
        {
            $"api-version={ApiVersion}",
            $"query={request.Latitude},{request.Longitude}",
            $"subscription-key={_subscriptionKey}"
        };

        if (!string.IsNullOrEmpty(request.Language))
        {
            queryParams.Add($"language={request.Language}");
        }

        return $"/search/address/reverse/json?{string.Join("&", queryParams)}";
    }

    private GeocodingResponse MapToGeocodingResponse(AzureMapsSearchResponse response)
    {
        var results = response.Results?.Select(r => new GeocodingResult
        {
            FormattedAddress = r.Address?.FreeformAddress ?? string.Empty,
            Longitude = r.Position?.Lon ?? 0,
            Latitude = r.Position?.Lat ?? 0,
            Type = r.Type,
            Confidence = r.Score,
            BoundingBox = r.Viewport != null ? new[]
            {
                r.Viewport.TopLeftPoint?.Lon ?? 0,
                r.Viewport.BtmRightPoint?.Lat ?? 0,
                r.Viewport.BtmRightPoint?.Lon ?? 0,
                r.Viewport.TopLeftPoint?.Lat ?? 0
            } : null,
            Components = r.Address != null ? new AddressComponents
            {
                HouseNumber = r.Address.StreetNumber,
                Street = r.Address.StreetName,
                City = r.Address.Municipality ?? r.Address.MunicipalitySubdivision,
                State = r.Address.CountrySubdivision,
                PostalCode = r.Address.PostalCode,
                Country = r.Address.Country,
                CountryCode = r.Address.CountryCode,
                County = r.Address.CountrySecondarySubdivision,
                District = r.Address.CountryTertiarySubdivision
            } : null,
            Metadata = new Dictionary<string, object>
            {
                ["matchType"] = r.MatchType ?? "unknown",
                ["entityType"] = r.EntityType ?? "unknown"
            }
        }).ToList() ?? new List<GeocodingResult>();

        return new GeocodingResponse
        {
            Results = results,
            Attribution = "© Microsoft Azure Maps"
        };
    }

    #region Azure Maps Response Models

    private class AzureMapsSearchResponse
    {
        public AzureMapsSearchResult[]? Results { get; set; }
    }

    private class AzureMapsSearchResult
    {
        public string? Type { get; set; }
        public double? Score { get; set; }
        public AzureMapsAddress? Address { get; set; }
        public AzureMapsPosition? Position { get; set; }
        public AzureMapsViewport? Viewport { get; set; }
        public string? MatchType { get; set; }
        public string? EntityType { get; set; }
    }

    private class AzureMapsAddress
    {
        public string? StreetNumber { get; set; }
        public string? StreetName { get; set; }
        public string? Municipality { get; set; }
        public string? MunicipalitySubdivision { get; set; }
        public string? CountrySubdivision { get; set; }
        public string? CountrySecondarySubdivision { get; set; }
        public string? CountryTertiarySubdivision { get; set; }
        public string? PostalCode { get; set; }
        public string? Country { get; set; }
        public string? CountryCode { get; set; }
        public string? FreeformAddress { get; set; }
    }

    private class AzureMapsPosition
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }

    private class AzureMapsViewport
    {
        public AzureMapsPosition? TopLeftPoint { get; set; }
        public AzureMapsPosition? BtmRightPoint { get; set; }
    }

    #endregion
}
