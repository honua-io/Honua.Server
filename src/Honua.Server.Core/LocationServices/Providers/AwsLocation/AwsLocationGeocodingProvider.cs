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

namespace Honua.Server.Core.LocationServices.Providers.AwsLocation;

/// <summary>
/// AWS Location Service implementation of geocoding provider.
/// API Reference: https://docs.aws.amazon.com/location/latest/developerguide/searching-for-places.html
/// </summary>
public class AwsLocationGeocodingProvider : IGeocodingProvider
{
    private readonly HttpClient _httpClient;
    private readonly string _accessKeyId;
    private readonly string _secretAccessKey;
    private readonly string _region;
    private readonly string _placeIndexName;
    private readonly ILogger<AwsLocationGeocodingProvider> _logger;
    private readonly AwsSignatureHelper _signatureHelper;

    public string ProviderKey => "aws-location";
    public string ProviderName => "AWS Location Service Geocoding";

    public AwsLocationGeocodingProvider(
        HttpClient httpClient,
        string accessKeyId,
        string secretAccessKey,
        string region,
        string placeIndexName,
        ILogger<AwsLocationGeocodingProvider> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _accessKeyId = accessKeyId ?? throw new ArgumentNullException(nameof(accessKeyId));
        _secretAccessKey = secretAccessKey ?? throw new ArgumentNullException(nameof(secretAccessKey));
        _region = region ?? throw new ArgumentNullException(nameof(region));
        _placeIndexName = placeIndexName ?? throw new ArgumentNullException(nameof(placeIndexName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _signatureHelper = new AwsSignatureHelper(_accessKeyId, _secretAccessKey, _region, "geo-places");
    }

    public async Task<GeocodingResponse> GeocodeAsync(
        GeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = BuildGeocodeRequest(request);
            var url = $"https://places.geo.{_region}.amazonaws.com/places/v0/indexes/{_placeIndexName}/search/text";

            _logger.LogDebug("AWS Location Service geocoding request for query: {Query}", request.Query);

            var httpRequest = await _signatureHelper.CreateSignedPostRequestAsync(
                url,
                JsonSerializer.Serialize(requestBody),
                cancellationToken);

            var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var response = await httpResponse.Content.ReadFromJsonAsync<AwsPlaceSearchResponse>(
                cancellationToken: cancellationToken);

            if (response == null)
            {
                throw new InvalidOperationException("AWS Location Service returned null response");
            }

            return MapToGeocodingResponse(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AWS Location Service geocoding request failed for query: {Query}", request.Query);
            throw new InvalidOperationException($"AWS Location Service geocoding failed: {ex.Message}", ex);
        }
    }

    public async Task<GeocodingResponse> ReverseGeocodeAsync(
        ReverseGeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var requestBody = BuildReverseGeocodeRequest(request);
            var url = $"https://places.geo.{_region}.amazonaws.com/places/v0/indexes/{_placeIndexName}/search/position";

            _logger.LogDebug("AWS Location Service reverse geocoding request for location: {Lon}, {Lat}",
                request.Longitude, request.Latitude);

            var httpRequest = await _signatureHelper.CreateSignedPostRequestAsync(
                url,
                JsonSerializer.Serialize(requestBody),
                cancellationToken);

            var httpResponse = await _httpClient.SendAsync(httpRequest, cancellationToken);
            httpResponse.EnsureSuccessStatusCode();

            var response = await httpResponse.Content.ReadFromJsonAsync<AwsPlaceSearchResponse>(
                cancellationToken: cancellationToken);

            if (response == null)
            {
                throw new InvalidOperationException("AWS Location Service returned null response");
            }

            return MapToGeocodingResponse(response);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "AWS Location Service reverse geocoding failed for location: {Lon}, {Lat}",
                request.Longitude, request.Latitude);
            throw new InvalidOperationException($"AWS Location Service reverse geocoding failed: {ex.Message}", ex);
        }
    }

    public async Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // Simple test with a known location (AWS headquarters in Seattle)
            var testRequest = new ReverseGeocodingRequest
            {
                Longitude = -122.3321,
                Latitude = 47.6062
            };

            await ReverseGeocodeAsync(testRequest, cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "AWS Location Service connectivity test failed");
            return false;
        }
    }

    private object BuildGeocodeRequest(GeocodingRequest request)
    {
        var awsRequest = new Dictionary<string, object>
        {
            ["Text"] = request.Query
        };

        if (request.MaxResults.HasValue)
        {
            awsRequest["MaxResults"] = request.MaxResults.Value;
        }

        if (request.BiasLocation?.Length == 2)
        {
            awsRequest["BiasPosition"] = new[] { request.BiasLocation[0], request.BiasLocation[1] };
        }

        if (request.BoundingBox?.Length == 4)
        {
            awsRequest["FilterBBox"] = request.BoundingBox;
        }

        if (request.CountryCodes?.Length > 0)
        {
            awsRequest["FilterCountries"] = request.CountryCodes;
        }

        if (!string.IsNullOrEmpty(request.Language))
        {
            awsRequest["Language"] = request.Language;
        }

        return awsRequest;
    }

    private object BuildReverseGeocodeRequest(ReverseGeocodingRequest request)
    {
        var awsRequest = new Dictionary<string, object>
        {
            ["Position"] = new[] { request.Longitude, request.Latitude }
        };

        if (!string.IsNullOrEmpty(request.Language))
        {
            awsRequest["Language"] = request.Language;
        }

        return awsRequest;
    }

    private GeocodingResponse MapToGeocodingResponse(AwsPlaceSearchResponse response)
    {
        var results = response.Results?.Select(r => new GeocodingResult
        {
            FormattedAddress = r.Place?.Label ?? string.Empty,
            Longitude = r.Place?.Geometry?.Point?[0] ?? 0,
            Latitude = r.Place?.Geometry?.Point?[1] ?? 0,
            Type = r.PlaceType,
            Confidence = r.Relevance,
            Components = new AddressComponents
            {
                HouseNumber = r.Place?.AddressNumber,
                Street = r.Place?.Street,
                City = r.Place?.Municipality,
                State = r.Place?.Region,
                PostalCode = r.Place?.PostalCode,
                Country = r.Place?.Country,
                CountryCode = r.Place?.CountryCode,
                District = r.Place?.SubRegion
            },
            Metadata = new Dictionary<string, object>
            {
                ["placeId"] = r.PlaceId ?? "unknown",
                ["distance"] = r.Distance ?? 0
            }
        }).ToList() ?? new List<GeocodingResult>();

        return new GeocodingResponse
        {
            Results = results,
            Attribution = "© AWS Location Service"
        };
    }

    #region AWS Location Service Response Models

    private class AwsPlaceSearchResponse
    {
        public AwsPlaceResult[]? Results { get; set; }
        public AwsSummary? Summary { get; set; }
    }

    private class AwsPlaceResult
    {
        public double? Distance { get; set; }
        public AwsPlace? Place { get; set; }
        public string? PlaceId { get; set; }
        public string? PlaceType { get; set; }
        public double? Relevance { get; set; }
    }

    private class AwsPlace
    {
        public string? AddressNumber { get; set; }
        public string? Country { get; set; }
        public string? CountryCode { get; set; }
        public AwsGeometry? Geometry { get; set; }
        public string? Label { get; set; }
        public string? Municipality { get; set; }
        public string? PostalCode { get; set; }
        public string? Region { get; set; }
        public string? Street { get; set; }
        public string? SubRegion { get; set; }
    }

    private class AwsGeometry
    {
        public double[]? Point { get; set; }
    }

    private class AwsSummary
    {
        public string? DataSource { get; set; }
        public string? Text { get; set; }
    }

    #endregion
}
