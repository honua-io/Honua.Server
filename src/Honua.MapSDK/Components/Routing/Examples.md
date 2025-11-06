# HonuaRouting Examples

Practical examples demonstrating routing and directions use cases.

## Table of Contents

1. [Basic A to B Routing](#1-basic-a-to-b-routing)
2. [Multi-Stop Delivery Route](#2-multi-stop-delivery-route)
3. [Cycling Route with Elevation](#3-cycling-route-with-elevation)
4. [Walking Tour of City](#4-walking-tour-of-city)
5. [Isochrone Analysis (15-min Drive Time)](#5-isochrone-analysis-15-min-drive-time)
6. [Route Comparison (Fastest vs Shortest)](#6-route-comparison-fastest-vs-shortest)
7. [Avoiding Tolls and Highways](#7-avoiding-tolls-and-highways)
8. [Custom Routing Engine](#8-custom-routing-engine)
9. [Server-Side Routing](#9-server-side-routing)
10. [Route Export and Sharing](#10-route-export-and-sharing)

---

## 1. Basic A to B Routing

Simple point-to-point routing from San Francisco to Oakland.

```razor
@page "/routing/basic"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Components.Routing

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudGrid>
        <MudItem xs="12" md="8">
            <HonuaMap
                Id="basic-map"
                Center="@(new[] { -122.4194, 37.7749 })"
                Zoom="11"
                Style="height: 600px;" />
        </MudItem>
        <MudItem xs="12" md="4">
            <HonuaRouting
                SyncWith="basic-map"
                OnRouteCalculated="@HandleRouteCalculated" />
        </MudItem>
    </MudGrid>
</MudContainer>

@if (_route != null)
{
    <MudAlert Severity="Severity.Success" Class="mt-4">
        Route calculated: @_route.Summary.FormattedDistance in @_route.Summary.FormattedDuration
    </MudAlert>
}

@code {
    private Route? _route;

    private void HandleRouteCalculated(Route route)
    {
        _route = route;
        Console.WriteLine($"Route has {route.Instructions.Count} steps");
    }
}
```

**Output:**
- Map centered on San Francisco Bay Area
- Routing panel with start/end inputs
- Route displayed on map when calculated

---

## 2. Multi-Stop Delivery Route

Delivery route with 5 stops in optimal order.

```razor
@page "/routing/delivery"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Components.Routing
@using Honua.MapSDK.Models.Routing

<MudContainer MaxWidth="MaxWidth.ExtraLarge">
    <MudText Typo="Typo.h4" Class="mb-4">Delivery Route Planner</MudText>

    <MudGrid>
        <MudItem xs="12" md="8">
            <HonuaMap Id="delivery-map" Center="@_center" Zoom="12" Style="height: 700px;" />
        </MudItem>
        <MudItem xs="12" md="4">
            <HonuaRouting
                Id="delivery-routing"
                SyncWith="delivery-map"
                AllowMultipleWaypoints="true"
                MaxWaypoints="10"
                TravelMode="TravelMode.Driving"
                OnRouteCalculated="@OnDeliveryRouteCalculated" />

            @if (_routeSummary != null)
            {
                <MudPaper Class="pa-4 mt-4" Elevation="2">
                    <MudText Typo="Typo.h6">Delivery Summary</MudText>
                    <MudText>Total Stops: @_routeSummary.StopCount</MudText>
                    <MudText>Total Distance: @_routeSummary.TotalDistance</MudText>
                    <MudText>Estimated Time: @_routeSummary.TotalTime</MudText>
                    <MudText>Estimated Fuel: @_routeSummary.FuelCost</MudText>
                </MudPaper>
            }
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private double[] _center = new[] { -122.4194, 37.7749 };
    private DeliveryRouteSummary? _routeSummary;

    private void OnDeliveryRouteCalculated(Route route)
    {
        _routeSummary = new DeliveryRouteSummary
        {
            StopCount = route.Waypoints.Count,
            TotalDistance = route.Summary.FormattedDistance,
            TotalTime = route.Summary.FormattedDuration,
            FuelCost = CalculateFuelCost(route.Distance)
        };
    }

    private string CalculateFuelCost(double distanceMeters)
    {
        var distanceMiles = distanceMeters * 0.000621371;
        var gallons = distanceMiles / 25.0; // 25 mpg average
        var cost = gallons * 4.50; // $4.50 per gallon
        return $"${cost:F2}";
    }

    class DeliveryRouteSummary
    {
        public int StopCount { get; set; }
        public string TotalDistance { get; set; } = "";
        public string TotalTime { get; set; } = "";
        public string FuelCost { get; set; } = "";
    }
}
```

**Features:**
- Multiple delivery stops
- Fuel cost estimation
- Time and distance summary
- Optimal route calculation

---

## 3. Cycling Route with Elevation

Cycling route with elevation profile integration.

```razor
@page "/routing/cycling"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Components.Routing
@using Honua.MapSDK.Components.ElevationProfile

<MudContainer MaxWidth="MaxWidth.ExtraLarge">
    <MudText Typo="Typo.h4" Class="mb-4">Cycling Route Planner</MudText>

    <MudGrid>
        <MudItem xs="12">
            <HonuaMap Id="cycling-map" Center="@_center" Zoom="11" Style="height: 500px;" />
        </MudItem>
        <MudItem xs="12" md="6">
            <HonuaRouting
                SyncWith="cycling-map"
                TravelMode="TravelMode.Cycling"
                ShowInstructions="true"
                OnRouteCalculated="@HandleCyclingRoute" />
        </MudItem>
        <MudItem xs="12" md="6">
            <HonuaElevationProfile
                Id="elevation"
                SyncWith="cycling-map"
                ShowGradient="true"
                ShowSteepSections="true"
                Height="300px" />

            @if (_cyclingStats != null)
            {
                <MudPaper Class="pa-4 mt-2">
                    <MudText Typo="Typo.h6">Cycling Stats</MudText>
                    <MudDivider Class="my-2" />
                    <MudText>Elevation Gain: @_cyclingStats.ElevationGain m</MudText>
                    <MudText>Elevation Loss: @_cyclingStats.ElevationLoss m</MudText>
                    <MudText>Average Grade: @_cyclingStats.AvgGrade%</MudText>
                    <MudText>Max Grade: @_cyclingStats.MaxGrade%</MudText>
                    <MudText>Difficulty: @_cyclingStats.Difficulty</MudText>
                </MudPaper>
            }
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private double[] _center = new[] { -122.4194, 37.7749 };
    private CyclingStats? _cyclingStats;

    [Inject] private ComponentBus Bus { get; set; } = null!;

    protected override void OnInitialized()
    {
        Bus.Subscribe<ElevationProfileGeneratedMessage>(args =>
        {
            var profile = args.Message;
            _cyclingStats = new CyclingStats
            {
                ElevationGain = profile.ElevationGain,
                ElevationLoss = profile.ElevationLoss,
                AvgGrade = profile.AverageGrade,
                MaxGrade = CalculateMaxGrade(profile),
                Difficulty = CalculateDifficulty(profile.ElevationGain, profile.TotalDistance)
            };
            InvokeAsync(StateHasChanged);
        });
    }

    private void HandleCyclingRoute(Route route)
    {
        // Trigger elevation profile generation
        // This will be handled by HonuaElevationProfile component
    }

    private double CalculateMaxGrade(ElevationProfileGeneratedMessage profile)
    {
        // Calculate from steep sections
        return 12.5; // Example
    }

    private string CalculateDifficulty(double gain, double distance)
    {
        var gradient = (gain / distance) * 100;
        return gradient switch
        {
            < 2 => "Easy",
            < 5 => "Moderate",
            < 8 => "Challenging",
            _ => "Difficult"
        };
    }

    class CyclingStats
    {
        public double ElevationGain { get; set; }
        public double ElevationLoss { get; set; }
        public double AvgGrade { get; set; }
        public double MaxGrade { get; set; }
        public string Difficulty { get; set; } = "";
    }
}
```

**Features:**
- Cycling-specific routing
- Elevation profile chart
- Grade and difficulty analysis
- Steep section warnings

---

## 4. Walking Tour of City

Self-guided walking tour with points of interest.

```razor
@page "/routing/walking-tour"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Components.Routing

<MudContainer MaxWidth="MaxWidth.ExtraLarge">
    <MudText Typo="Typo.h4" Class="mb-4">San Francisco Walking Tour</MudText>

    <MudGrid>
        <MudItem xs="12" md="8">
            <HonuaMap Id="tour-map" Center="@_center" Zoom="14" Style="height: 700px;" />
        </MudItem>
        <MudItem xs="12" md="4">
            <MudPaper Class="pa-4 mb-4">
                <MudText Typo="Typo.h6">Tour Information</MudText>
                <MudText>Duration: ~2 hours</MudText>
                <MudText>Distance: ~5 km</MudText>
                <MudText>Difficulty: Easy</MudText>
            </MudPaper>

            <HonuaRouting
                SyncWith="tour-map"
                TravelMode="TravelMode.Walking"
                AllowMultipleWaypoints="true"
                ShowInstructions="true"
                OnRouteCalculated="@OnTourRouteCalculated" />

            <MudPaper Class="pa-4 mt-4">
                <MudText Typo="Typo.h6">Points of Interest</MudText>
                <MudList>
                    @foreach (var poi in _pointsOfInterest)
                    {
                        <MudListItem>
                            <MudText>@poi.Name</MudText>
                            <MudText Typo="Typo.body2" Color="Color.Secondary">@poi.Description</MudText>
                        </MudListItem>
                    }
                </MudList>
            </MudPaper>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private double[] _center = new[] { -122.4194, 37.7749 };

    private List<PointOfInterest> _pointsOfInterest = new()
    {
        new() { Name = "Ferry Building", Description = "Historic marketplace and ferry terminal" },
        new() { Name = "Pier 39", Description = "Waterfront shopping and sea lions" },
        new() { Name = "Fisherman's Wharf", Description = "Famous seafood district" },
        new() { Name = "Ghirardelli Square", Description = "Historic chocolate factory" },
        new() { Name = "Cable Car Museum", Description = "Working museum and barn" }
    };

    private void OnTourRouteCalculated(Route route)
    {
        Console.WriteLine($"Walking tour: {route.Summary.FormattedDuration}");
    }

    class PointOfInterest
    {
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
    }
}
```

---

## 5. Isochrone Analysis (15-min Drive Time)

Service area analysis showing 5, 10, 15-minute drive times.

```razor
@page "/routing/isochrone"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Components.Routing

<MudContainer MaxWidth="MaxWidth.ExtraLarge">
    <MudText Typo="Typo.h4" Class="mb-4">Service Area Analysis</MudText>

    <MudAlert Severity="Severity.Info" Class="mb-4">
        Click on the map to set a location, then click "Calculate Service Area" to see
        areas reachable within 5, 10, and 15 minutes by car.
    </MudAlert>

    <MudGrid>
        <MudItem xs="12" md="8">
            <HonuaMap
                Id="isochrone-map"
                Center="@_center"
                Zoom="11"
                Style="height: 600px;" />
        </MudItem>
        <MudItem xs="12" md="4">
            <HonuaRouting
                SyncWith="isochrone-map"
                RoutingEngine="RoutingEngine.GraphHopper"
                ApiKey="@_graphHopperKey"
                ShowIsochroneOptions="true"
                TravelMode="TravelMode.Driving" />

            @if (_isochroneResult != null)
            {
                <MudPaper Class="pa-4 mt-4">
                    <MudText Typo="Typo.h6">Service Areas</MudText>
                    <MudList>
                        <MudListItem>
                            <div style="display: flex; align-items: center; gap: 8px;">
                                <div style="width: 20px; height: 20px; background: #00FF00; opacity: 0.3;"></div>
                                <MudText>5 minutes: @_isochroneResult.Area5Min km²</MudText>
                            </div>
                        </MudListItem>
                        <MudListItem>
                            <div style="display: flex; align-items: center; gap: 8px;">
                                <div style="width: 20px; height: 20px; background: #FFFF00; opacity: 0.3;"></div>
                                <MudText>10 minutes: @_isochroneResult.Area10Min km²</MudText>
                            </div>
                        </MudListItem>
                        <MudListItem>
                            <div style="display: flex; align-items: center; gap: 8px;">
                                <div style="width: 20px; height: 20px; background: #FF8800; opacity: 0.3;"></div>
                                <MudText>15 minutes: @_isochroneResult.Area15Min km²</MudText>
                            </div>
                        </MudListItem>
                    </MudList>
                </MudPaper>
            }
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private double[] _center = new[] { -122.4194, 37.7749 };
    private string _graphHopperKey = "your_api_key_here";
    private IsochroneResultSummary? _isochroneResult;

    [Inject] private ComponentBus Bus { get; set; } = null!;

    protected override void OnInitialized()
    {
        Bus.Subscribe<IsochroneCalculatedMessage>(args =>
        {
            _isochroneResult = new IsochroneResultSummary
            {
                Area5Min = 25.5,
                Area10Min = 78.2,
                Area15Min = 156.8
            };
            InvokeAsync(StateHasChanged);
        });
    }

    class IsochroneResultSummary
    {
        public double Area5Min { get; set; }
        public double Area10Min { get; set; }
        public double Area15Min { get; set; }
    }
}
```

**Use Cases:**
- Delivery service coverage
- Emergency response planning
- Real estate location analysis
- Restaurant delivery zones

---

## 6. Route Comparison (Fastest vs Shortest)

Side-by-side comparison of fastest vs shortest routes.

```razor
@page "/routing/comparison"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Components.Routing

<MudContainer MaxWidth="MaxWidth.ExtraLarge">
    <MudText Typo="Typo.h4" Class="mb-4">Route Comparison</MudText>

    <MudGrid>
        <MudItem xs="12" md="8">
            <HonuaMap Id="comparison-map" Center="@_center" Zoom="10" Style="height: 600px;" />
        </MudItem>
        <MudItem xs="12" md="4">
            <HonuaRouting
                SyncWith="comparison-map"
                ShowAlternatives="true"
                MaxAlternatives="3"
                OnRouteCalculated="@HandleRouteComparison" />

            @if (_comparison != null)
            {
                <MudPaper Class="pa-4 mt-4">
                    <MudText Typo="Typo.h6" Class="mb-2">Route Comparison</MudText>

                    <MudTable Items="@_comparison" Dense="true" Hover="true">
                        <HeaderContent>
                            <MudTh>Metric</MudTh>
                            <MudTh>Main Route</MudTh>
                            <MudTh>Alternative</MudTh>
                        </HeaderContent>
                        <RowTemplate>
                            <MudTd>@context.Metric</MudTd>
                            <MudTd>@context.MainValue</MudTd>
                            <MudTd>@context.AltValue</MudTd>
                        </RowTemplate>
                    </MudTable>

                    <MudText Typo="Typo.body2" Color="Color.Success" Class="mt-2">
                        <strong>Recommendation:</strong> @_recommendation
                    </MudText>
                </MudPaper>
            }
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private double[] _center = new[] { -122.4194, 37.7749 };
    private List<ComparisonRow>? _comparison;
    private string _recommendation = "";

    private void HandleRouteComparison(Route route)
    {
        // In real implementation, get alternative routes
        _comparison = new List<ComparisonRow>
        {
            new() { Metric = "Distance", MainValue = "45.2 km", AltValue = "42.8 km (-5%)" },
            new() { Metric = "Time", MainValue = "38 min", AltValue = "45 min (+18%)" },
            new() { Metric = "Tolls", MainValue = "$5.50", AltValue = "$0.00" },
            new() { Metric = "Highway", MainValue = "Yes", AltValue = "Minimal" }
        };

        _recommendation = "Main route is faster despite higher tolls. " +
                         "Alternative saves $5.50 but adds 7 minutes.";
    }

    class ComparisonRow
    {
        public string Metric { get; set; } = "";
        public string MainValue { get; set; } = "";
        public string AltValue { get; set; } = "";
    }
}
```

---

## 7. Avoiding Tolls and Highways

Route configuration with avoidance preferences.

```razor
@page "/routing/avoid-tolls"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Components.Routing

<MudContainer MaxWidth="MaxWidth.ExtraLarge">
    <MudText Typo="Typo.h4" Class="mb-4">Toll-Free Route Planning</MudText>

    <MudGrid>
        <MudItem xs="12" md="8">
            <HonuaMap Id="tollfree-map" Center="@_center" Zoom="10" Style="height: 600px;" />
        </MudItem>
        <MudItem xs="12" md="4">
            <HonuaRouting
                SyncWith="tollfree-map"
                ShowAvoidOptions="true"
                ShowAlternatives="true"
                OnRouteCalculated="@HandleTollFreeRoute" />

            @if (_routeInfo != null)
            {
                <MudPaper Class="pa-4 mt-4">
                    <MudText Typo="Typo.h6">Route Details</MudText>
                    <MudDivider Class="my-2" />
                    <MudText>Distance: @_routeInfo.Distance</MudText>
                    <MudText>Time: @_routeInfo.Duration</MudText>
                    <MudText>Tolls: @_routeInfo.Tolls</MudText>
                    <MudText>Highways: @_routeInfo.Highways</MudText>
                    <MudText Color="Color.Success">
                        <strong>Savings: @_routeInfo.TollSavings</strong>
                    </MudText>
                </MudPaper>
            }
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private double[] _center = new[] { -122.4194, 37.7749 };
    private RouteInfo? _routeInfo;

    private void HandleTollFreeRoute(Route route)
    {
        _routeInfo = new RouteInfo
        {
            Distance = route.Summary.FormattedDistance,
            Duration = route.Summary.FormattedDuration,
            Tolls = route.Summary.TollCost.HasValue ? $"${route.Summary.TollCost:F2}" : "None",
            Highways = "Minimal",
            TollSavings = "$12.50"
        };
    }

    class RouteInfo
    {
        public string Distance { get; set; } = "";
        public string Duration { get; set; } = "";
        public string Tolls { get; set; } = "";
        public string Highways { get; set; } = "";
        public string TollSavings { get; set; } = "";
    }
}
```

---

## 8. Custom Routing Engine

Implementing a custom routing provider.

```csharp
// CustomRoutingService.cs
public class VroomRoutingService : IRoutingService
{
    private readonly HttpClient _httpClient;
    private readonly string _baseUrl;

    public string ProviderName => "VROOM";
    public bool RequiresApiKey => false;
    public List<TravelMode> SupportedTravelModes => new() { TravelMode.Driving };

    public VroomRoutingService(HttpClient httpClient, string baseUrl = "http://localhost:3000")
    {
        _httpClient = httpClient;
        _baseUrl = baseUrl;
    }

    public async Task<Route> CalculateRouteAsync(
        List<Waypoint> waypoints,
        RouteOptions options,
        CancellationToken cancellationToken = default)
    {
        // Build VROOM request
        var request = new
        {
            vehicles = new[]
            {
                new
                {
                    id = 1,
                    start = new[] { waypoints[0].Longitude, waypoints[0].Latitude }
                }
            },
            jobs = waypoints.Skip(1).Select((w, i) => new
            {
                id = i + 1,
                location = new[] { w.Longitude, w.Latitude }
            })
        };

        var response = await _httpClient.PostAsJsonAsync($"{_baseUrl}/", request, cancellationToken);
        var result = await response.Content.ReadFromJsonAsync<VroomResponse>(cancellationToken);

        // Convert to Route object
        return ConvertToRoute(result, waypoints);
    }

    private Route ConvertToRoute(VroomResponse? response, List<Waypoint> waypoints)
    {
        // Implementation details...
        return new Route
        {
            Waypoints = waypoints,
            Distance = response?.Routes?.FirstOrDefault()?.Distance ?? 0,
            Duration = response?.Routes?.FirstOrDefault()?.Duration ?? 0
            // ... etc
        };
    }

    // Implement other interface methods...
    public Task<List<Route>> GetAlternativesAsync(...)
    {
        throw new NotImplementedException();
    }

    public Task<IsochroneResult> CalculateIsochroneAsync(...)
    {
        throw new NotSupportedException("VROOM does not support isochrones");
    }
}

// Usage
@code {
    private IRoutingService _customService = new VroomRoutingService(new HttpClient());
}
```

```razor
<HonuaRouting
    SyncWith="map"
    RoutingEngine="RoutingEngine.Custom"
    CustomRoutingService="@_customService" />
```

---

## 9. Server-Side Routing

Proxy routing requests through your server to protect API keys.

```csharp
// Server/Controllers/RoutingController.cs
[ApiController]
[Route("api/[controller]")]
public class RoutingController : ControllerBase
{
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpClientFactory;

    public RoutingController(IConfiguration config, IHttpClientFactory httpClientFactory)
    {
        _config = config;
        _httpClientFactory = httpClientFactory;
    }

    [HttpPost("calculate")]
    public async Task<ActionResult<Route>> CalculateRoute([FromBody] RouteRequest request)
    {
        var apiKey = _config["Routing:MapboxApiKey"];
        var httpClient = _httpClientFactory.CreateClient();
        var service = new MapboxRoutingService(httpClient, apiKey);

        try
        {
            var route = await service.CalculateRouteAsync(request.Waypoints, request.Options);
            return Ok(route);
        }
        catch (RoutingException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}

public class RouteRequest
{
    public List<Waypoint> Waypoints { get; set; } = new();
    public RouteOptions Options { get; set; } = new();
}
```

```csharp
// Client/Services/ServerRoutingService.cs
public class ServerRoutingService : IRoutingService
{
    private readonly HttpClient _httpClient;

    public string ProviderName => "Server";
    public bool RequiresApiKey => false;
    public List<TravelMode> SupportedTravelModes => new() { /* all modes */ };

    public ServerRoutingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<Route> CalculateRouteAsync(
        List<Waypoint> waypoints,
        RouteOptions options,
        CancellationToken cancellationToken = default)
    {
        var request = new RouteRequest
        {
            Waypoints = waypoints,
            Options = options
        };

        var response = await _httpClient.PostAsJsonAsync("/api/routing/calculate", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<Route>(cancellationToken)
            ?? throw new RoutingException("Invalid response from server");
    }
}
```

```razor
<HonuaRouting
    RoutingEngine="RoutingEngine.Custom"
    CustomRoutingService="@_serverService" />

@code {
    [Inject] private HttpClient Http { get; set; } = null!;
    private IRoutingService _serverService => new ServerRoutingService(Http);
}
```

---

## 10. Route Export and Sharing

Export routes and share via URL.

```razor
@page "/routing/export"
@using Honua.MapSDK.Components
@using Honua.MapSDK.Components.Routing

<MudContainer MaxWidth="MaxWidth.ExtraLarge">
    <MudText Typo="Typo.h4" Class="mb-4">Route Export & Sharing</MudText>

    <MudGrid>
        <MudItem xs="12" md="8">
            <HonuaMap Id="export-map" Center="@_center" Zoom="10" Style="height: 600px;" />
        </MudItem>
        <MudItem xs="12" md="4">
            <HonuaRouting
                SyncWith="export-map"
                ShowExportOptions="true"
                OnRouteCalculated="@HandleRouteForExport" />

            @if (_shareUrl != null)
            {
                <MudPaper Class="pa-4 mt-4">
                    <MudText Typo="Typo.h6" Class="mb-2">Share This Route</MudText>

                    <MudTextField
                        T="string"
                        Value="@_shareUrl"
                        ReadOnly="true"
                        Variant="Variant.Outlined"
                        Adornment="Adornment.End"
                        AdornmentIcon="@Icons.Material.Filled.ContentCopy"
                        OnAdornmentClick="@CopyToClipboard" />

                    <div class="d-flex gap-2 mt-2">
                        <MudButton
                            Variant="Variant.Filled"
                            Color="Color.Primary"
                            StartIcon="@Icons.Material.Filled.Email"
                            OnClick="@ShareViaEmail">
                            Email
                        </MudButton>
                        <MudButton
                            Variant="Variant.Filled"
                            Color="Color.Info"
                            StartIcon="@Icons.Material.Filled.Message"
                            OnClick="@ShareViaSMS">
                            SMS
                        </MudButton>
                    </div>

                    <MudDivider Class="my-4" />

                    <MudText Typo="Typo.h6" Class="mb-2">Export Formats</MudText>
                    <MudButton
                        Variant="Variant.Outlined"
                        StartIcon="@Icons.Material.Filled.Download"
                        OnClick="@(() => ExportAs("gpx"))"
                        Class="mb-2"
                        FullWidth="true">
                        Download GPX (GPS devices)
                    </MudButton>
                    <MudButton
                        Variant="Variant.Outlined"
                        StartIcon="@Icons.Material.Filled.Download"
                        OnClick="@(() => ExportAs("kml"))"
                        Class="mb-2"
                        FullWidth="true">
                        Download KML (Google Earth)
                    </MudButton>
                    <MudButton
                        Variant="Variant.Outlined"
                        StartIcon="@Icons.Material.Filled.Download"
                        OnClick="@(() => ExportAs("geojson"))"
                        FullWidth="true">
                        Download GeoJSON (Mapping)
                    </MudButton>
                </MudPaper>
            }
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private double[] _center = new[] { -122.4194, 37.7749 };
    private Route? _currentRoute;
    private string? _shareUrl;

    [Inject] private NavigationManager NavManager { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    private void HandleRouteForExport(Route route)
    {
        _currentRoute = route;
        _shareUrl = GenerateShareUrl(route);
    }

    private string GenerateShareUrl(Route route)
    {
        // Encode route waypoints in URL
        var waypoints = string.Join("|", route.Waypoints.Select(w =>
            $"{w.Latitude},{w.Longitude}"));

        return $"{NavManager.BaseUri}routing/view?w={Uri.EscapeDataString(waypoints)}";
    }

    private async Task CopyToClipboard()
    {
        if (_shareUrl != null)
        {
            await JS.InvokeVoidAsync("navigator.clipboard.writeText", _shareUrl);
        }
    }

    private void ShareViaEmail()
    {
        if (_currentRoute != null && _shareUrl != null)
        {
            var subject = "Check out this route!";
            var body = $"Route: {_currentRoute.Summary.FormattedDistance}, " +
                      $"{_currentRoute.Summary.FormattedDuration}%0D%0A%0D%0A{_shareUrl}";
            NavManager.NavigateTo($"mailto:?subject={subject}&body={body}");
        }
    }

    private void ShareViaSMS()
    {
        if (_shareUrl != null)
        {
            NavManager.NavigateTo($"sms:?body=Check out this route: {_shareUrl}");
        }
    }

    private async Task ExportAs(string format)
    {
        // Export functionality handled by HonuaRouting component
        Console.WriteLine($"Exporting as {format}");
    }
}
```

---

## More Examples

For additional examples, see:
- Integration with other components (Search, ElevationProfile, etc.)
- Real-time traffic routing
- Fleet management and vehicle routing
- Multi-modal transportation planning
- Pedestrian navigation with accessibility options

## Resources

- [Component Documentation](./README.md)
- [API Reference](#)
- [MapSDK Examples](/examples)
