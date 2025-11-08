// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Honua.MapSDK.Models.Routing;
using Honua.MapSDK.Services.Routing;
using Honua.Server.Core.LocationServices;
using Honua.Server.Core.LocationServices.Models;
using CoreRoute = Honua.Server.Core.LocationServices.Models.Route;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Honua.MapSDK.Components.Routing;

/// <summary>
/// Code-behind for the RoutePanel component.
/// </summary>
public partial class RoutePanel : ComponentBase, IAsyncDisposable
{
    [Inject] private IEnumerable<IRoutingProvider> RoutingProviders { get; set; } = null!;
    [Inject] private RouteVisualizationService VisualizationService { get; set; } = null!;
    [Inject] private RouteComparisonService ComparisonService { get; set; } = null!;
    [Inject] private RouteExportService ExportService { get; set; } = null!;
    [Inject] private IJSRuntime JSRuntime { get; set; } = null!;

    /// <summary>
    /// Map instance identifier.
    /// </summary>
    [Parameter]
    public string MapId { get; set; } = "map";

    /// <summary>
    /// Initial provider to use.
    /// </summary>
    [Parameter]
    public string? Provider { get; set; }

    /// <summary>
    /// Initial travel mode.
    /// </summary>
    [Parameter]
    public string InitialTravelMode { get; set; } = "car";

    /// <summary>
    /// Event fired when a route is calculated.
    /// </summary>
    [Parameter]
    public EventCallback<RoutingResponse> OnRouteCalculated { get; set; }

    /// <summary>
    /// Event fired when a route is selected.
    /// </summary>
    [Parameter]
    public EventCallback<CoreRoute> OnRouteSelected { get; set; }

    /// <summary>
    /// Event fired when the panel is closed.
    /// </summary>
    [Parameter]
    public EventCallback OnClose { get; set; }

    // State
    protected Dictionary<string, string> AvailableProviders { get; set; } = new();
    protected string SelectedProvider { get; set; } = "osrm";
    protected string TravelMode { get; set; } = "car";
    protected List<Waypoint> Waypoints { get; set; } = new();
    protected bool AvoidTolls { get; set; }
    protected bool AvoidHighways { get; set; }
    protected bool AvoidFerries { get; set; }
    protected bool UseTraffic { get; set; }
    protected double? VehicleWeight { get; set; }
    protected double? VehicleHeight { get; set; }
    protected bool IsHazmat { get; set; }
    protected bool IsCalculating { get; set; }
    protected string? ErrorMessage { get; set; }
    protected List<CoreRoute> CalculatedRoutes { get; set; } = new();
    protected int SelectedRouteIndex { get; set; }
    protected int? DraggedWaypointIndex { get; set; }

    private IRoutingProvider? _currentProvider;
    private CancellationTokenSource? _calculationCts;

    protected override void OnInitialized()
    {
        // Initialize available providers
        foreach (var provider in RoutingProviders)
        {
            AvailableProviders[provider.ProviderKey] = provider.ProviderName;
        }

        // Set initial provider
        if (!string.IsNullOrEmpty(Provider) && AvailableProviders.ContainsKey(Provider))
        {
            SelectedProvider = Provider;
        }
        else if (AvailableProviders.Count > 0)
        {
            SelectedProvider = AvailableProviders.Keys.First();
        }

        _currentProvider = RoutingProviders.FirstOrDefault(p => p.ProviderKey == SelectedProvider);

        TravelMode = InitialTravelMode;

        // Initialize with 2 waypoints (start and end)
        Waypoints.Add(new Waypoint
        {
            Coordinates = new[] { 0.0, 0.0 },
            Order = 0
        });
        Waypoints.Add(new Waypoint
        {
            Coordinates = new[] { 0.0, 0.0 },
            Order = 1
        });
    }

    protected void OnProviderChanged()
    {
        _currentProvider = RoutingProviders.FirstOrDefault(p => p.ProviderKey == SelectedProvider);
        ClearRouteResults();
    }

    protected void SetTravelMode(string mode)
    {
        TravelMode = mode;
        ClearRouteResults();
    }

    protected void AddWaypoint()
    {
        Waypoints.Insert(Waypoints.Count - 1, new Waypoint
        {
            Coordinates = new[] { 0.0, 0.0 },
            Order = Waypoints.Count - 1
        });
        UpdateWaypointOrders();
    }

    protected void RemoveWaypoint(int index)
    {
        if (Waypoints.Count > 2 && index > 0 && index < Waypoints.Count - 1)
        {
            Waypoints.RemoveAt(index);
            UpdateWaypointOrders();
            ClearRouteResults();
        }
    }

    protected void UpdateWaypointAddress(int index, string? address)
    {
        if (index >= 0 && index < Waypoints.Count)
        {
            Waypoints[index].Address = address;
            // In a real implementation, you would geocode this address
            // For now, we'll just store the address
        }
    }

    protected void ReverseRoute()
    {
        Waypoints.Reverse();
        UpdateWaypointOrders();
        ClearRouteResults();
    }

    protected void ClearRoute()
    {
        Waypoints.Clear();
        Waypoints.Add(new Waypoint { Coordinates = new[] { 0.0, 0.0 }, Order = 0 });
        Waypoints.Add(new Waypoint { Coordinates = new[] { 0.0, 0.0 }, Order = 1 });
        ClearRouteResults();
    }

    protected void HandleDragStart(int index)
    {
        DraggedWaypointIndex = index;
    }

    protected void HandleDragOver()
    {
        // Just prevent default to allow drop
    }

    protected void HandleDragEnd()
    {
        DraggedWaypointIndex = null;
    }

    protected void HandleDrop()
    {
        // In a real implementation, you would handle reordering here
        // This would require JavaScript interop to get the drop target
        DraggedWaypointIndex = null;
    }

    protected async Task CalculateRoute()
    {
        // Validate waypoints
        if (Waypoints.Count < 2)
        {
            ErrorMessage = "Please add at least a start and end location.";
            return;
        }

        // Check if coordinates are set
        if (Waypoints.Any(w => w.Coordinates[0] == 0.0 && w.Coordinates[1] == 0.0))
        {
            ErrorMessage = "Please set valid coordinates for all waypoints.";
            return;
        }

        if (_currentProvider == null)
        {
            ErrorMessage = "No routing provider available.";
            return;
        }

        IsCalculating = true;
        ErrorMessage = null;
        _calculationCts?.Cancel();
        _calculationCts = new CancellationTokenSource();

        try
        {
            var request = new RoutingRequest
            {
                Waypoints = Waypoints.Select(w => w.Coordinates).ToList(),
                TravelMode = TravelMode,
                AvoidTolls = AvoidTolls,
                AvoidHighways = AvoidHighways,
                AvoidFerries = AvoidFerries,
                UseTraffic = UseTraffic,
                Vehicle = TravelMode == "truck" ? new VehicleSpecifications
                {
                    WeightKg = VehicleWeight,
                    HeightMeters = VehicleHeight,
                    IsHazmat = IsHazmat
                } : null
            };

            var response = await _currentProvider.CalculateRouteAsync(request, _calculationCts.Token);

            CalculatedRoutes = response.Routes.ToList();
            SelectedRouteIndex = 0;

            if (CalculatedRoutes.Count > 0)
            {
                await OnRouteCalculated.InvokeAsync(response);
                await DisplayRouteOnMap(CalculatedRoutes[0]);
            }
            else
            {
                ErrorMessage = "No routes found.";
            }
        }
        catch (OperationCanceledException)
        {
            // Calculation was cancelled
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to calculate route: {ex.Message}";
        }
        finally
        {
            IsCalculating = false;
            StateHasChanged();
        }
    }

    protected async Task SelectRoute(int index)
    {
        if (index >= 0 && index < CalculatedRoutes.Count)
        {
            SelectedRouteIndex = index;
            await DisplayRouteOnMap(CalculatedRoutes[index]);
            await OnRouteSelected.InvokeAsync(CalculatedRoutes[index]);
        }
    }

    protected async Task ExportGpx()
    {
        if (CalculatedRoutes.Count > 0)
        {
            var coreRoute = CalculatedRoutes[SelectedRouteIndex];
            var mapRoute = ConvertToMapSDKRoute(coreRoute);
            var coordinates = VisualizationService.ParseRouteGeometry(mapRoute);
            var gpx = ExportService.ExportToGpx(coreRoute, coordinates, "My Route");
            await DownloadFile("route.gpx", gpx, "application/gpx+xml");
        }
    }

    protected async Task ExportKml()
    {
        if (CalculatedRoutes.Count > 0)
        {
            var coreRoute = CalculatedRoutes[SelectedRouteIndex];
            var mapRoute = ConvertToMapSDKRoute(coreRoute);
            var coordinates = VisualizationService.ParseRouteGeometry(mapRoute);
            var kml = ExportService.ExportToKml(coreRoute, coordinates, "My Route");
            await DownloadFile("route.kml", kml, "application/vnd.google-earth.kml+xml");
        }
    }

    protected async Task ShareRoute()
    {
        var shareLink = ExportService.GenerateShareLink(
            Waypoints.Select(w => w.Coordinates).ToList(),
            TravelMode);

        await JSRuntime.InvokeVoidAsync("navigator.clipboard.writeText",
            $"{GetBaseUrl()}{shareLink}");

        // Show a toast notification (would need to implement)
        await JSRuntime.InvokeVoidAsync("alert", $"Share link copied to clipboard!");
    }

    protected async Task PrintRoute()
    {
        if (CalculatedRoutes.Count > 0)
        {
            var coreRoute = CalculatedRoutes[SelectedRouteIndex];
            var html = ExportService.GeneratePrintableHtml(coreRoute, "Route Directions");

            // Open in new window for printing
            await JSRuntime.InvokeVoidAsync("eval",
                $"var w = window.open(); w.document.write({System.Text.Json.JsonSerializer.Serialize(html)}); w.document.close();");
        }
    }

    protected string FormatDistance(double meters)
    {
        if (meters < 1000)
            return $"{meters:F0} m";
        return $"{meters / 1000:F1} km";
    }

    protected string FormatDuration(double seconds)
    {
        var timeSpan = TimeSpan.FromSeconds(seconds);
        if (timeSpan.TotalHours >= 1)
            return $"{(int)timeSpan.TotalHours}h {timeSpan.Minutes}m";
        return $"{(int)timeSpan.TotalMinutes} min";
    }

    private void UpdateWaypointOrders()
    {
        for (int i = 0; i < Waypoints.Count; i++)
        {
            Waypoints[i].Order = i;
        }
    }

    private void ClearRouteResults()
    {
        CalculatedRoutes.Clear();
        SelectedRouteIndex = 0;
        ErrorMessage = null;
    }

    private async Task DisplayRouteOnMap(CoreRoute coreRoute)
    {
        try
        {
            // Convert to MapSDK Route for visualization
            var route = ConvertToMapSDKRoute(coreRoute);

            // Clear previous routes
            await VisualizationService.ClearAllRoutesAsync(MapId);

            // Add main route
            await VisualizationService.AddRouteToMapAsync(
                MapId,
                "main-route",
                route,
                new RouteStyle { Color = "#4285F4" },
                new RouteAnimationOptions { Enabled = true });

            // Add waypoint markers
            await VisualizationService.AddWaypointMarkersAsync(
                MapId,
                "main-route",
                Waypoints);

            // Add turn markers if instructions available
            if (route.Instructions != null && route.Instructions.Count > 0)
            {
                await VisualizationService.AddTurnMarkersAsync(
                    MapId,
                    "main-route",
                    route.Instructions);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Failed to display route: {ex.Message}";
        }
    }

    private async Task DownloadFile(string filename, string content, string mimeType)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(content);
        var base64 = Convert.ToBase64String(bytes);

        await JSRuntime.InvokeVoidAsync("eval", $@"
            var link = document.createElement('a');
            link.download = '{filename}';
            link.href = 'data:{mimeType};base64,{base64}';
            link.click();
        ");
    }

    private string GetBaseUrl()
    {
        // In a real implementation, you would get this from NavigationManager or configuration
        return "https://example.com";
    }

    /// <summary>
    /// Converts a Core Route to a MapSDK Route for visualization.
    /// </summary>
    private Models.Routing.Route ConvertToMapSDKRoute(CoreRoute coreRoute)
    {
        return new Models.Routing.Route
        {
            Id = Guid.NewGuid().ToString("N"),
            Distance = coreRoute.DistanceMeters,
            Duration = (int)coreRoute.DurationSeconds,
            Geometry = coreRoute.Geometry,
            Waypoints = Waypoints.ToList(),
            TravelMode = Enum.TryParse<TravelMode>(TravelMode, true, out var mode) ? mode : Models.Routing.TravelMode.Driving,
            CalculatedAt = DateTime.UtcNow,
            RoutingEngine = SelectedProvider
        };
    }

    public async ValueTask DisposeAsync()
    {
        _calculationCts?.Cancel();
        _calculationCts?.Dispose();

        if (VisualizationService != null)
        {
            await VisualizationService.ClearAllRoutesAsync(MapId);
        }

        GC.SuppressFinalize(this);
    }
}
