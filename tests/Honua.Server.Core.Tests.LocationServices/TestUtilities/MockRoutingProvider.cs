// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using Honua.Server.Core.LocationServices;
using Honua.Server.Core.LocationServices.Models;

namespace Honua.Server.Core.Tests.LocationServices.TestUtilities;

/// <summary>
/// Mock routing provider for testing.
/// </summary>
public class MockRoutingProvider : IRoutingProvider
{
    private RoutingResponse? _nextResponse;
    private bool _isAvailable = true;
    private Exception? _nextException;

    public string ProviderKey { get; set; } = "mock";
    public string ProviderName { get; set; } = "Mock Routing Provider";

    public void SetResponse(RoutingResponse response)
    {
        _nextResponse = response;
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
        _nextResponse = null;
        _isAvailable = true;
        _nextException = null;
    }

    public Task<RoutingResponse> CalculateRouteAsync(
        RoutingRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_nextException != null)
        {
            var ex = _nextException;
            _nextException = null;
            throw ex;
        }

        if (_nextResponse != null)
        {
            return Task.FromResult(_nextResponse);
        }

        // Default response - simple route between first and last waypoint
        var start = request.Waypoints.First();
        var end = request.Waypoints.Last();

        // Calculate approximate distance (simple Euclidean for testing)
        var distance = Math.Sqrt(
            Math.Pow((end[0] - start[0]) * 111000, 2) +
            Math.Pow((end[1] - start[1]) * 111000, 2));

        return Task.FromResult(new RoutingResponse
        {
            Routes = new List<Route>
            {
                new Route
                {
                    DistanceMeters = distance,
                    DurationSeconds = distance / 13.89, // ~50 km/h average
                    Geometry = "mock_polyline_data",
                    GeometryFormat = "polyline",
                    Summary = $"Route from start to end via {request.Waypoints.Count} waypoints"
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
