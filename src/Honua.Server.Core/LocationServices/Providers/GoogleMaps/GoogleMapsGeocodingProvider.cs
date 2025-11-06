// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Honua.Server.Core.LocationServices.Models;
using Microsoft.Extensions.Logging;

namespace Honua.Server.Core.LocationServices.Providers.GoogleMaps;

/// <summary>
/// Google Maps implementation of geocoding provider.
/// API Reference: https://developers.google.com/maps/documentation/geocoding
/// </summary>
public class GoogleMapsGeocodingProvider : IGeocodingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly string? _clientId;
    private readonly string? _clientSignature;
    private readonly ILogger<GoogleMapsGeocodingProvider> _logger;
    private const string BaseUrl = "https://maps.googleapis.com";

    public string ProviderKey => "google-maps";
    public string ProviderName => "Google Maps Geocoding";

    public GoogleMapsGeocodingProvider(
        HttpClient httpClient,
        string apiKey,
        ILogger<GoogleMapsGeocodingProvider> logger,
        string? clientId = null,
        string? clientSignature = null)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _apiKey = apiKey ?? throw new ArgumentNullException(nameof(apiKey));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _clientId = clientId;
        _clientSignature = clientSignature;

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
            _logger.LogDebug("Google Maps geocoding request: {Url}", url);

            var response = await _httpClient.GetFromJsonAsync<GoogleMapsGeocodingResponse>(
                url,
                cancellationToken);

            if (response == null)
            {
                throw new InvalidOperationException("Google Maps returned null response");
            }

            if (response.Status != "OK" && response.Status != "ZERO_RESULTS")
            {
                throw new InvalidOperationException($"Google Maps geocoding failed with status: {response.Status}. {response.ErrorMessage}");
            }

            return MapToGeocodingResponse(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Google Maps geocoding request failed for query: {Query}", request.Query);
            throw new InvalidOperationException($"Google Maps geocoding failed: {ex.Message}", ex);
        }
    }

    public async Task<GeocodingResponse> ReverseGeocodeAsync(
        ReverseGeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var url = BuildReverseGeocodeUrl(request);
            _logger.LogDebug("Google Maps reverse geocoding request: {Url}", url);

            var response = await _httpClient.GetFromJsonAsync<GoogleMapsGeocodingResponse>(
                url,
                cancellationToken);

            if (response == null)
            {
                throw new InvalidOperationException("Google Maps returned null response");
            }

            if (response.Status != "OK" && response.Status != "ZERO_RESULTS")
            {
                throw new InvalidOperationException($"Google Maps reverse geocoding failed with status: {response.Status}. {response.ErrorMessage}");
            }

            return MapToGeocodingResponse(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "Google Maps reverse geocoding failed for location: {Lon}, {Lat}",
                request.Longitude, request.Latitude);
            throw new InvalidOperationException($"Google Maps reverse geocoding failed: {ex.Message}", ex);
        }
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple test with a known location (Google HQ)
            var testRequest = new ReverseGeocodingRequest
            {
                Longitude = -122.0842499,
                Latitude = 37.4224764
            };

            await ReverseGeocodeAsync(testRequest, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Google Maps connectivity test failed");
            return false;
        }
    }

    private string BuildGeocodeUrl(GeocodingRequest request)
    {
        var queryParams = new List<string>
        {
            $"address={Uri.EscapeDataString(request.Query)}"
        };

        AddAuthenticationParams(queryParams);

        if (request.MaxResults.HasValue)
        {
            // Google doesn't have a direct limit parameter, but we'll filter results
            // after receiving them
        }

        if (request.BoundingBox?.Length == 4)
        {
            var bbox = request.BoundingBox;
            queryParams.Add($"bounds={bbox[1]},{bbox[0]}|{bbox[3]},{bbox[2]}");
        }

        if (request.CountryCodes?.Length > 0)
        {
            var components = string.Join("|", request.CountryCodes.Select(cc => $"country:{cc}"));
            queryParams.Add($"components={components}");
        }

        if (request.BiasLocation?.Length == 2)
        {
            // Google uses 'region' for bias, but we can use bounds with same point
            queryParams.Add($"region={request.BiasLocation[1]},{request.BiasLocation[0]}");
        }

        if (!string.IsNullOrEmpty(request.Language))
        {
            queryParams.Add($"language={request.Language}");
        }

        return $"/maps/api/geocode/json?{string.Join("&", queryParams)}";
    }

    private string BuildReverseGeocodeUrl(ReverseGeocodingRequest request)
    {
        var queryParams = new List<string>
        {
            $"latlng={request.Latitude},{request.Longitude}"
        };

        AddAuthenticationParams(queryParams);

        if (!string.IsNullOrEmpty(request.Language))
        {
            queryParams.Add($"language={request.Language}");
        }

        if (request.ResultTypes?.Length > 0)
        {
            queryParams.Add($"result_type={string.Join("|", request.ResultTypes)}");
        }

        return $"/maps/api/geocode/json?{string.Join("&", queryParams)}";
    }

    private void AddAuthenticationParams(List<string> queryParams)
    {
        // Use API Key or Premium Plan client ID/signature
        if (!string.IsNullOrEmpty(_clientId) && !string.IsNullOrEmpty(_clientSignature))
        {
            queryParams.Add($"client={_clientId}");
            queryParams.Add($"signature={_clientSignature}");
        }
        else
        {
            queryParams.Add($"key={_apiKey}");
        }
    }

    private GeocodingResponse MapToGeocodingResponse(GoogleMapsGeocodingResponse response)
    {
        var results = response.Results?.Select(r => new GeocodingResult
        {
            FormattedAddress = r.FormattedAddress ?? string.Empty,
            Longitude = r.Geometry?.Location?.Lng ?? 0,
            Latitude = r.Geometry?.Location?.Lat ?? 0,
            Type = r.Types?.FirstOrDefault(),
            Confidence = CalculateConfidence(r),
            BoundingBox = r.Geometry?.Viewport != null ? new[]
            {
                r.Geometry.Viewport.Southwest?.Lng ?? 0,
                r.Geometry.Viewport.Southwest?.Lat ?? 0,
                r.Geometry.Viewport.Northeast?.Lng ?? 0,
                r.Geometry.Viewport.Northeast?.Lat ?? 0
            } : null,
            Components = MapAddressComponents(r.AddressComponents),
            Metadata = new Dictionary<string, object>
            {
                ["placeId"] = r.PlaceId ?? string.Empty,
                ["types"] = r.Types ?? Array.Empty<string>(),
                ["locationType"] = r.Geometry?.LocationType ?? "UNKNOWN"
            }
        }).ToList() ?? new List<GeocodingResult>();

        return new GeocodingResponse
        {
            Results = results,
            Attribution = "© Google Maps"
        };
    }

    private AddressComponents? MapAddressComponents(GoogleMapsAddressComponent[]? components)
    {
        if (components == null || components.Length == 0)
        {
            return null;
        }

        var result = new AddressComponents();

        foreach (var component in components)
        {
            if (component.Types == null) continue;

            if (component.Types.Contains("street_number"))
            {
                result = result with { HouseNumber = component.LongName };
            }
            else if (component.Types.Contains("route"))
            {
                result = result with { Street = component.LongName };
            }
            else if (component.Types.Contains("locality"))
            {
                result = result with { City = component.LongName };
            }
            else if (component.Types.Contains("administrative_area_level_1"))
            {
                result = result with { State = component.LongName };
            }
            else if (component.Types.Contains("postal_code"))
            {
                result = result with { PostalCode = component.LongName };
            }
            else if (component.Types.Contains("country"))
            {
                result = result with
                {
                    Country = component.LongName,
                    CountryCode = component.ShortName
                };
            }
            else if (component.Types.Contains("administrative_area_level_2"))
            {
                result = result with { County = component.LongName };
            }
            else if (component.Types.Contains("sublocality") || component.Types.Contains("neighborhood"))
            {
                result = result with { District = component.LongName };
            }
        }

        return result;
    }

    private double CalculateConfidence(GoogleMapsGeocodingResult result)
    {
        // Google doesn't provide explicit confidence scores
        // We estimate based on location_type
        if (result.Geometry?.LocationType == null)
        {
            return 0.5;
        }

        return result.Geometry.LocationType switch
        {
            "ROOFTOP" => 1.0,
            "RANGE_INTERPOLATED" => 0.8,
            "GEOMETRIC_CENTER" => 0.6,
            "APPROXIMATE" => 0.4,
            _ => 0.5
        };
    }

    #region Google Maps Response Models

    private class GoogleMapsGeocodingResponse
    {
        [JsonPropertyName("results")]
        public GoogleMapsGeocodingResult[]? Results { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; } = string.Empty;

        [JsonPropertyName("error_message")]
        public string? ErrorMessage { get; set; }
    }

    private class GoogleMapsGeocodingResult
    {
        [JsonPropertyName("formatted_address")]
        public string? FormattedAddress { get; set; }

        [JsonPropertyName("geometry")]
        public GoogleMapsGeometry? Geometry { get; set; }

        [JsonPropertyName("address_components")]
        public GoogleMapsAddressComponent[]? AddressComponents { get; set; }

        [JsonPropertyName("place_id")]
        public string? PlaceId { get; set; }

        [JsonPropertyName("types")]
        public string[]? Types { get; set; }
    }

    private class GoogleMapsGeometry
    {
        [JsonPropertyName("location")]
        public GoogleMapsLocation? Location { get; set; }

        [JsonPropertyName("location_type")]
        public string? LocationType { get; set; }

        [JsonPropertyName("viewport")]
        public GoogleMapsViewport? Viewport { get; set; }

        [JsonPropertyName("bounds")]
        public GoogleMapsViewport? Bounds { get; set; }
    }

    private class GoogleMapsLocation
    {
        [JsonPropertyName("lat")]
        public double Lat { get; set; }

        [JsonPropertyName("lng")]
        public double Lng { get; set; }
    }

    private class GoogleMapsViewport
    {
        [JsonPropertyName("northeast")]
        public GoogleMapsLocation? Northeast { get; set; }

        [JsonPropertyName("southwest")]
        public GoogleMapsLocation? Southwest { get; set; }
    }

    private class GoogleMapsAddressComponent
    {
        [JsonPropertyName("long_name")]
        public string? LongName { get; set; }

        [JsonPropertyName("short_name")]
        public string? ShortName { get; set; }

        [JsonPropertyName("types")]
        public string[]? Types { get; set; }
    }

    #endregion
}
