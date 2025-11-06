// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.LocationServices;
using Honua.Server.Core.LocationServices.Models;

namespace Honua.Server.Core.Tests.LocationServices.TestUtilities;

/// <summary>
/// Mock geocoding provider for testing.
/// </summary>
public class MockGeocodingProvider : IGeocodingProvider
{
    private GeocodingResponse? _nextGeocodeResponse;
    private GeocodingResponse? _nextReverseGeocodeResponse;
    private bool _isAvailable = true;
    private Exception? _nextException;

    public string ProviderKey { get; set; } = "mock";
    public string ProviderName { get; set; } = "Mock Geocoding Provider";

    public void SetGeocodeResponse(GeocodingResponse response)
    {
        _nextGeocodeResponse = response;
    }

    public void SetReverseGeocodeResponse(GeocodingResponse response)
    {
        _nextReverseGeocodeResponse = response;
    }

    public void SetAvailability(bool isAvailable)
    {
        _isAvailable = isAvailable;
    }

    public void SetNextException(Exception exception)
    {
        _nextException = exception;
    }

    public void Reset()
    {
        _nextGeocodeResponse = null;
        _nextReverseGeocodeResponse = null;
        _isAvailable = true;
        _nextException = null;
    }

    public Task<GeocodingResponse> GeocodeAsync(
        GeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_nextException != null)
        {
            var ex = _nextException;
            _nextException = null;
            throw ex;
        }

        if (_nextGeocodeResponse != null)
        {
            return Task.FromResult(_nextGeocodeResponse);
        }

        // Default response
        return Task.FromResult(new GeocodingResponse
        {
            Results = new List<GeocodingResult>
            {
                new GeocodingResult
                {
                    FormattedAddress = request.Query,
                    Longitude = -122.4194,
                    Latitude = 37.7749,
                    Confidence = 0.9,
                    Type = "address"
                }
            },
            Attribution = "Mock Provider"
        });
    }

    public Task<GeocodingResponse> ReverseGeocodeAsync(
        ReverseGeocodingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_nextException != null)
        {
            var ex = _nextException;
            _nextException = null;
            throw ex;
        }

        if (_nextReverseGeocodeResponse != null)
        {
            return Task.FromResult(_nextReverseGeocodeResponse);
        }

        // Default response
        return Task.FromResult(new GeocodingResponse
        {
            Results = new List<GeocodingResult>
            {
                new GeocodingResult
                {
                    FormattedAddress = $"{request.Latitude}, {request.Longitude}",
                    Longitude = request.Longitude,
                    Latitude = request.Latitude,
                    Confidence = 0.9,
                    Type = "address"
                }
            },
            Attribution = "Mock Provider"
        });
    }

    public Task<bool> TestConnectivityAsync(CancellationToken cancellationToken = default)
    {
        if (_nextException != null)
        {
            var ex = _nextException;
            _nextException = null;
            throw ex;
        }

        return Task.FromResult(_isAvailable);
    }
}
