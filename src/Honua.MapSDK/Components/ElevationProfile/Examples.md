# HonuaElevationProfile - Examples

Comprehensive examples demonstrating various use cases for the elevation profile component.

## Example 1: Hiking Trail Elevation Profile

Display elevation profile for a popular hiking trail with waypoints and time estimates.

```razor
@page "/hiking-trail"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.ElevationProfile
@using Honua.MapSDK.Models
@inject ComponentBus Bus

<div class="hiking-example">
    <h2>Half Dome Trail - Yosemite</h2>

    <HonuaMap Id="hiking-map"
              Center="new[] { -119.5333, 37.7459 }"
              Zoom="12"
              Style="outdoors"
              Height="600px" />

    <HonuaElevationProfile
        SyncWith="hiking-map"
        Position="bottom-right"
        ElevationSource="ElevationSource.OpenElevation"
        SamplePoints="150"
        Unit="MeasurementUnit.Imperial"
        ShowStatistics="true"
        ShowGradeColors="true"
        AllowDraw="true"
        OnProfileGenerated="OnProfileGenerated" />
</div>

@code {
    private ElevationProfile? currentProfile;

    protected override async Task OnInitializedAsync()
    {
        // Load trail coordinates
        var trailCoordinates = new List<double[]>
        {
            new[] { -119.5333, 37.7459 },  // Trailhead
            new[] { -119.5383, 37.7509 },
            new[] { -119.5433, 37.7559 },
            new[] { -119.5483, 37.7609 },  // Vernal Fall
            new[] { -119.5533, 37.7659 },
            new[] { -119.5583, 37.7709 },  // Nevada Fall
            new[] { -119.5633, 37.7759 },
            new[] { -119.5683, 37.7809 },  // Sub Dome
            new[] { -119.5333, 37.7459 }   // Half Dome Summit
        };

        // Publish feature to trigger elevation profile
        await Task.Delay(1000); // Wait for map to initialize

        await Bus.PublishAsync(new FeatureDrawnMessage
        {
            FeatureId = "half-dome-trail",
            GeometryType = "LineString",
            Geometry = new
            {
                type = "LineString",
                coordinates = trailCoordinates
            },
            ComponentId = "elevation-1"
        });
    }

    private async Task OnProfileGenerated(ElevationProfile profile)
    {
        currentProfile = profile;

        // Add custom waypoints
        profile.Waypoints.Add(new Waypoint
        {
            Name = "Vernal Fall",
            Coordinates = new[] { -119.5483, 37.7609 },
            Type = WaypointType.Viewpoint,
            Description = "317 ft waterfall viewpoint"
        });

        profile.Waypoints.Add(new Waypoint
        {
            Name = "Nevada Fall",
            Coordinates = new[] { -119.5583, 37.7709 },
            Type = WaypointType.Viewpoint,
            Description = "594 ft waterfall viewpoint"
        });

        profile.Waypoints.Add(new Waypoint
        {
            Name = "Half Dome Summit",
            Coordinates = new[] { -119.5333, 37.7459 },
            Type = WaypointType.Summit,
            Description = "8,842 ft elevation - breathtaking 360° views"
        });

        // Log statistics
        Console.WriteLine($"Trail Distance: {profile.TotalDistance / 1609.34:F2} miles");
        Console.WriteLine($"Elevation Gain: {profile.ElevationGain * 3.28084:F0} feet");
        Console.WriteLine($"Estimated Time: {profile.TimeEstimate?.TotalMinutes / 60:F1} hours");
    }
}
```

**Key Features:**
- Imperial units for US hiking
- Custom waypoints for points of interest
- Time estimation for hiking activity
- Steep section highlighting
- Integration with outdoor map style

---

## Example 2: Cycling Route with Grade Analysis

Plan a cycling route with detailed grade analysis for training purposes.

```razor
@page "/cycling-route"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.ElevationProfile
@using Honua.MapSDK.Components.Draw
@using Honua.MapSDK.Models

<div class="cycling-example">
    <MudPaper Class="pa-4 mb-4">
        <MudText Typo="Typo.h5">Cycling Route Planner</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary">
            Draw your route and analyze elevation profile
        </MudText>
    </MudPaper>

    <HonuaMap Id="cycling-map"
              Center="new[] { -122.4194, 37.7749 }"
              Zoom="11"
              Style="streets"
              Height="500px" />

    <HonuaDraw
        SyncWith="cycling-map"
        Position="top-left"
        ShowMeasurements="true" />

    <HonuaElevationProfile
        SyncWith="cycling-map"
        Position="bottom-left"
        Width="600px"
        ElevationSource="ElevationSource.MapboxAPI"
        ApiKey="@_mapboxApiKey"
        SamplePoints="200"
        Unit="MeasurementUnit.Metric"
        ShowStatistics="true"
        ShowGradeColors="true"
        ChartHeight="350"
        OnProfileGenerated="AnalyzeRoute" />

    @if (_routeAnalysis != null)
    {
        <MudPaper Class="pa-4 mt-4">
            <MudText Typo="Typo.h6">Route Analysis</MudText>
            <MudGrid>
                <MudItem xs="12" md="4">
                    <MudCard>
                        <MudCardContent>
                            <MudText Typo="Typo.h4" Color="Color.Primary">
                                @_routeAnalysis.Distance
                            </MudText>
                            <MudText Typo="Typo.body2">Total Distance</MudText>
                        </MudCardContent>
                    </MudCard>
                </MudItem>
                <MudItem xs="12" md="4">
                    <MudCard>
                        <MudCardContent>
                            <MudText Typo="Typo.h4" Color="Color.Success">
                                @_routeAnalysis.Climbing
                            </MudText>
                            <MudText Typo="Typo.body2">Total Climbing</MudText>
                        </MudCardContent>
                    </MudCard>
                </MudItem>
                <MudItem xs="12" md="4">
                    <MudCard>
                        <MudCardContent>
                            <MudText Typo="Typo.h4" Color="Color.Warning">
                                @_routeAnalysis.Difficulty
                            </MudText>
                            <MudText Typo="Typo.body2">Difficulty Rating</MudText>
                        </MudCardContent>
                    </MudCard>
                </MudItem>
            </MudGrid>

            @if (_routeAnalysis.SteepClimbs.Any())
            {
                <MudText Typo="Typo.h6" Class="mt-4">Steep Climbs</MudText>
                <MudTable Items="_routeAnalysis.SteepClimbs" Dense="true">
                    <HeaderContent>
                        <MudTh>Location</MudTh>
                        <MudTh>Length</MudTh>
                        <MudTh>Avg Grade</MudTh>
                        <MudTh>Category</MudTh>
                    </HeaderContent>
                    <RowTemplate>
                        <MudTd>@context.Location</MudTd>
                        <MudTd>@context.Length</MudTd>
                        <MudTd>@context.Grade</MudTd>
                        <MudTd>
                            <MudChip Size="Size.Small" Color="@GetCategoryColor(context.Category)">
                                @context.Category
                            </MudChip>
                        </MudTd>
                    </RowTemplate>
                </MudTable>
            }
        </MudPaper>
    }
</div>

@code {
    private string _mapboxApiKey = "pk.your_mapbox_token";
    private RouteAnalysis? _routeAnalysis;

    private class RouteAnalysis
    {
        public string Distance { get; set; } = "";
        public string Climbing { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public List<ClimbInfo> SteepClimbs { get; set; } = new();
    }

    private class ClimbInfo
    {
        public string Location { get; set; } = "";
        public string Length { get; set; } = "";
        public string Grade { get; set; } = "";
        public string Category { get; set; } = "";
    }

    private void AnalyzeRoute(ElevationProfile profile)
    {
        _routeAnalysis = new RouteAnalysis
        {
            Distance = $"{profile.TotalDistance / 1000:F1} km",
            Climbing = $"{profile.ElevationGain:F0} m",
            Difficulty = GetDifficultyRating(profile)
        };

        // Categorize steep climbs (cycling categories)
        foreach (var section in profile.SteepSections.Where(s => s.AverageGrade > 3))
        {
            var category = GetClimbCategory(section.Length, section.AverageGrade);

            _routeAnalysis.SteepClimbs.Add(new ClimbInfo
            {
                Location = $"km {section.StartDistance / 1000:F1}",
                Length = $"{section.Length / 1000:F2} km",
                Grade = $"{section.AverageGrade:F1}%",
                Category = category
            });
        }

        StateHasChanged();
    }

    private string GetDifficultyRating(ElevationProfile profile)
    {
        var score = (profile.TotalDistance / 1000) + (profile.ElevationGain / 10);

        if (score < 50) return "Easy";
        if (score < 100) return "Moderate";
        if (score < 150) return "Challenging";
        return "Very Difficult";
    }

    private string GetClimbCategory(double length, double grade)
    {
        // Based on cycling climb categorization
        var score = (length / 1000) * grade;

        if (score < 8) return "Category 4";
        if (score < 16) return "Category 3";
        if (score < 32) return "Category 2";
        if (score < 64) return "Category 1";
        return "HC (Hors Catégorie)";
    }

    private Color GetCategoryColor(string category)
    {
        return category switch
        {
            "Category 4" => Color.Success,
            "Category 3" => Color.Info,
            "Category 2" => Color.Warning,
            "Category 1" => Color.Error,
            "HC (Hors Catégorie)" => Color.Dark,
            _ => Color.Default
        };
    }
}
```

**Key Features:**
- Draw integration for route planning
- Cycling-specific analysis
- Climb categorization
- Performance metrics
- Detailed statistics table

---

## Example 3: Infrastructure Pipeline Planning

Analyze terrain for pipeline or utility corridor planning.

```razor
@page "/pipeline-planning"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.ElevationProfile
@using Honua.MapSDK.Models

<div class="pipeline-example">
    <MudGrid>
        <MudItem xs="12" lg="8">
            <HonuaMap Id="pipeline-map"
                      Center="new[] { -106.3468, 35.6870 }"
                      Zoom="10"
                      Style="satellite"
                      Height="700px" />
        </MudItem>

        <MudItem xs="12" lg="4">
            <MudPaper Class="pa-4">
                <MudText Typo="Typo.h6">Pipeline Corridor Analysis</MudText>

                @if (_corridorAnalysis != null)
                {
                    <MudList Dense="true">
                        <MudListItem Icon="@Icons.Material.Filled.Straighten">
                            Length: @_corridorAnalysis.Length
                        </MudListItem>
                        <MudListItem Icon="@Icons.Material.Filled.TrendingUp">
                            Max Grade: @_corridorAnalysis.MaxGrade
                        </MudListItem>
                        <MudListItem Icon="@Icons.Material.Filled.Landscape">
                            Elevation Range: @_corridorAnalysis.ElevationRange
                        </MudListItem>
                        <MudListItem Icon="@Icons.Material.Filled.Warning">
                            Challenging Sections: @_corridorAnalysis.ChallengingSections
                        </MudListItem>
                    </MudList>

                    <MudDivider Class="my-4" />

                    <MudText Typo="Typo.subtitle2" Class="mb-2">Engineering Considerations</MudText>

                    @if (_corridorAnalysis.RequiresPumpStations)
                    {
                        <MudAlert Severity="Severity.Warning" Dense="true" Class="mb-2">
                            Pump stations required due to elevation changes
                        </MudAlert>
                    }

                    @if (_corridorAnalysis.SteepSections > 0)
                    {
                        <MudAlert Severity="Severity.Info" Dense="true" Class="mb-2">
                            @_corridorAnalysis.SteepSections section(s) require special anchoring
                        </MudAlert>
                    }

                    <MudButton Variant="Variant.Filled"
                               Color="Color.Primary"
                               FullWidth="true"
                               OnClick="ExportAnalysis">
                        Export Analysis Report
                    </MudButton>
                }
            </MudPaper>
        </MudItem>
    </MudGrid>

    <HonuaElevationProfile
        SyncWith="pipeline-map"
        Position="bottom-left"
        Width="800px"
        ElevationSource="ElevationSource.USGSAPI"
        SamplePoints="300"
        ShowStatistics="true"
        ShowGradeColors="false"
        ChartHeight="250"
        OnProfileGenerated="AnalyzeCorridor" />
</div>

@code {
    private CorridorAnalysis? _corridorAnalysis;

    private class CorridorAnalysis
    {
        public string Length { get; set; } = "";
        public string MaxGrade { get; set; } = "";
        public string ElevationRange { get; set; } = "";
        public int ChallengingSections { get; set; }
        public bool RequiresPumpStations { get; set; }
        public int SteepSections { get; set; }
    }

    private void AnalyzeCorridor(ElevationProfile profile)
    {
        var maxGrade = Math.Abs(profile.MaxGrade);
        var elevRange = profile.MaxElevation - profile.MinElevation;

        _corridorAnalysis = new CorridorAnalysis
        {
            Length = $"{profile.TotalDistance / 1000:F2} km",
            MaxGrade = $"{maxGrade:F1}%",
            ElevationRange = $"{elevRange:F0} m",
            ChallengingSections = profile.SteepSections.Count(s => Math.Abs(s.AverageGrade) > 15),
            RequiresPumpStations = elevRange > 100,
            SteepSections = profile.SteepSections.Count(s => Math.Abs(s.AverageGrade) > 20)
        };

        StateHasChanged();
    }

    private async Task ExportAnalysis()
    {
        // Export comprehensive analysis report
        await Task.CompletedTask;
    }
}
```

**Key Features:**
- High sample rate for accuracy
- USGS elevation data for US locations
- Engineering-focused analysis
- Special consideration for steep sections
- Export functionality

---

## Example 4: Flight Path Elevation Clearance

Visualize flight path with terrain clearance analysis.

```razor
@page "/flight-path"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.ElevationProfile
@using Honua.MapSDK.Models

<div class="flight-example">
    <HonuaMap Id="flight-map"
              Center="new[] { -118.2437, 34.0522 }"
              Zoom="9"
              Style="satellite"
              Height="600px" />

    <HonuaElevationProfile
        @ref="_elevationProfile"
        SyncWith="flight-map"
        Position="bottom-right"
        Width="700px"
        ElevationSource="ElevationSource.OpenElevation"
        SamplePoints="100"
        ShowStatistics="true"
        ChartHeight="300"
        OnProfileGenerated="AnalyzeFlightPath" />

    @if (_clearanceAnalysis != null)
    {
        <MudPaper Class="pa-4 mt-4">
            <MudGrid>
                <MudItem xs="12" md="6">
                    <MudText Typo="Typo.h6">Flight Path Analysis</MudText>
                    <MudText>Route: @_clearanceAnalysis.Route</MudText>
                    <MudText>Distance: @_clearanceAnalysis.Distance</MudText>
                    <MudText>Flight Level: @_clearanceAnalysis.FlightLevel ft</MudText>
                </MudItem>
                <MudItem xs="12" md="6">
                    <MudText Typo="Typo.h6">Terrain Clearance</MudText>
                    <MudText>Max Terrain Elevation: @_clearanceAnalysis.MaxTerrain ft</MudText>
                    <MudText>Minimum Clearance: @_clearanceAnalysis.MinClearance ft</MudText>

                    @if (_clearanceAnalysis.ClearanceAdequate)
                    {
                        <MudChip Color="Color.Success" Icon="@Icons.Material.Filled.CheckCircle">
                            Clearance Adequate
                        </MudChip>
                    }
                    else
                    {
                        <MudChip Color="Color.Error" Icon="@Icons.Material.Filled.Warning">
                            Insufficient Clearance
                        </MudChip>
                    }
                </MudItem>
            </MudGrid>
        </MudPaper>
    }
</div>

@code {
    private HonuaElevationProfile? _elevationProfile;
    private FlightClearanceAnalysis? _clearanceAnalysis;

    private class FlightClearanceAnalysis
    {
        public string Route { get; set; } = "";
        public string Distance { get; set; } = "";
        public double FlightLevel { get; set; }
        public double MaxTerrain { get; set; }
        public double MinClearance { get; set; }
        public bool ClearanceAdequate { get; set; }
    }

    protected override async Task OnInitializedAsync()
    {
        // Example: LAX to Las Vegas flight path
        var flightPath = new List<double[]>
        {
            new[] { -118.4085, 33.9416 },  // LAX
            new[] { -117.8, 34.5 },
            new[] { -116.5, 35.2 },
            new[] { -115.5, 36.0 },
            new[] { -115.1523, 36.0840 }   // LAS
        };

        await Task.Delay(1500);

        await Bus.PublishAsync(new FeatureDrawnMessage
        {
            FeatureId = "flight-path",
            GeometryType = "LineString",
            Geometry = new
            {
                type = "LineString",
                coordinates = flightPath
            },
            ComponentId = "elevation-1"
        });
    }

    private void AnalyzeFlightPath(ElevationProfile profile)
    {
        const double flightLevelFeet = 15000; // FL150
        const double requiredClearanceFeet = 1000;

        var maxTerrainMeters = profile.MaxElevation;
        var maxTerrainFeet = maxTerrainMeters * 3.28084;
        var minClearanceFeet = flightLevelFeet - maxTerrainFeet;

        _clearanceAnalysis = new FlightClearanceAnalysis
        {
            Route = "LAX to LAS",
            Distance = $"{profile.TotalDistance / 1609.34:F1} nm",
            FlightLevel = flightLevelFeet,
            MaxTerrain = maxTerrainFeet,
            MinClearance = minClearanceFeet,
            ClearanceAdequate = minClearanceFeet >= requiredClearanceFeet
        };

        StateHasChanged();
    }
}
```

**Key Features:**
- Flight path visualization
- Terrain clearance calculation
- Safety analysis
- Aviation-specific metrics
- Visual clearance indicators

---

## Example 5: Cross-Section Analysis for Surveying

Generate terrain cross-sections for surveying and civil engineering.

```razor
@page "/cross-section"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.ElevationProfile
@using Honua.MapSDK.Models

<div class="survey-example">
    <MudPaper Class="pa-4 mb-4">
        <MudText Typo="Typo.h5">Terrain Cross-Section Tool</MudText>
        <MudGrid>
            <MudItem xs="12" md="4">
                <MudTextField @bind-Value="_startLat" Label="Start Latitude" Variant="Variant.Outlined" />
            </MudItem>
            <MudItem xs="12" md="4">
                <MudTextField @bind-Value="_startLon" Label="Start Longitude" Variant="Variant.Outlined" />
            </MudItem>
            <MudItem xs="12" md="4">
                <MudButton Variant="Variant.Filled"
                           Color="Color.Primary"
                           OnClick="GenerateCrossSection"
                           FullWidth="true">
                    Generate Cross-Section
                </MudButton>
            </MudItem>
        </MudGrid>
    </MudPaper>

    <HonuaMap Id="survey-map"
              Center="new[] { -122.4194, 37.7749 }"
              Zoom="12"
              Style="satellite"
              Height="500px" />

    <HonuaElevationProfile
        SyncWith="survey-map"
        Position="bottom-left"
        Width="900px"
        ElevationSource="ElevationSource.USGSAPI"
        SamplePoints="500"
        ShowStatistics="true"
        ShowGradeColors="false"
        ChartHeight="400"
        OnProfileGenerated="ProcessCrossSection" />

    @if (_crossSectionData != null)
    {
        <MudPaper Class="pa-4 mt-4">
            <MudText Typo="Typo.h6">Cross-Section Data</MudText>
            <MudTable Items="_crossSectionData" Dense="true" Height="300px">
                <HeaderContent>
                    <MudTh>Station</MudTh>
                    <MudTh>Distance (m)</MudTh>
                    <MudTh>Elevation (m)</MudTh>
                    <MudTh>Grade (%)</MudTh>
                    <MudTh>Cut/Fill</MudTh>
                </HeaderContent>
                <RowTemplate>
                    <MudTd>@context.Station</MudTd>
                    <MudTd>@context.Distance.ToString("F2")</MudTd>
                    <MudTd>@context.Elevation.ToString("F2")</MudTd>
                    <MudTd>@context.Grade.ToString("F2")</MudTd>
                    <MudTd>@context.CutFill</MudTd>
                </RowTemplate>
            </MudTable>

            <MudButton Variant="Variant.Outlined"
                       Color="Color.Primary"
                       StartIcon="@Icons.Material.Filled.Download"
                       OnClick="ExportSurveyData"
                       Class="mt-4">
                Export Survey Data (CSV)
            </MudButton>
        </MudPaper>
    }
</div>

@code {
    private double _startLat = 37.7749;
    private double _startLon = -122.4194;
    private List<CrossSectionPoint>? _crossSectionData;

    private class CrossSectionPoint
    {
        public string Station { get; set; } = "";
        public double Distance { get; set; }
        public double Elevation { get; set; }
        public double Grade { get; set; }
        public string CutFill { get; set; } = "";
    }

    private async Task GenerateCrossSection()
    {
        // Generate 5km cross-section
        var bearing = 45.0; // Northeast
        var distance = 5000.0; // 5km

        var coordinates = GenerateLineFromBearing(_startLon, _startLat, bearing, distance);

        await Bus.PublishAsync(new FeatureDrawnMessage
        {
            FeatureId = "cross-section",
            GeometryType = "LineString",
            Geometry = new
            {
                type = "LineString",
                coordinates = coordinates
            },
            ComponentId = "elevation-1"
        });
    }

    private List<double[]> GenerateLineFromBearing(double lon, double lat, double bearing, double distance)
    {
        // Simplified - would use proper geodesic calculations
        var coords = new List<double[]>();
        var steps = 100;
        var stepDistance = distance / steps;

        for (int i = 0; i <= steps; i++)
        {
            var dist = i * stepDistance;
            var radBearing = bearing * Math.PI / 180;

            // Approximate offset (not accurate for large distances)
            var latOffset = (dist / 111320) * Math.Cos(radBearing);
            var lonOffset = (dist / (111320 * Math.Cos(lat * Math.PI / 180))) * Math.Sin(radBearing);

            coords.Add(new[] { lon + lonOffset, lat + latOffset });
        }

        return coords;
    }

    private void ProcessCrossSection(ElevationProfile profile)
    {
        const double designGrade = 2.0; // 2% target grade
        const double designElevation = 100.0; // Starting design elevation

        _crossSectionData = new List<CrossSectionPoint>();

        for (int i = 0; i < profile.Points.Count; i++)
        {
            var point = profile.Points[i];
            var station = $"{(i * 50):0}+{((i * 50) % 100):00}"; // Format as 0+00, 0+50, etc.

            var designElev = designElevation + (point.Distance * designGrade / 100);
            var cutFill = point.Elevation - designElev;
            var cutFillStr = cutFill > 0 ? $"Cut {Math.Abs(cutFill):F2}m" : $"Fill {Math.Abs(cutFill):F2}m";

            _crossSectionData.Add(new CrossSectionPoint
            {
                Station = station,
                Distance = point.Distance,
                Elevation = point.Elevation,
                Grade = point.Grade,
                CutFill = cutFillStr
            });
        }

        StateHasChanged();
    }

    private async Task ExportSurveyData()
    {
        // Export cross-section data as CSV
        await Task.CompletedTask;
    }
}
```

**Key Features:**
- High-resolution sampling (500 points)
- USGS data for accuracy
- Survey station formatting
- Cut/fill analysis
- Export for CAD software

---

## Example 6: Multi-Segment Route with Alternative Paths

Compare multiple route options with elevation profiles.

```razor
@page "/route-comparison"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.ElevationProfile
@using Honua.MapSDK.Models

<div class="comparison-example">
    <MudPaper Class="pa-4 mb-4">
        <MudText Typo="Typo.h5">Route Comparison Tool</MudText>
        <MudText Typo="Typo.body2">
            Compare elevation profiles for different route options
        </MudText>

        <MudButtonGroup Class="mt-4">
            <MudButton Variant="Variant.Filled"
                       Color="@(_selectedRoute == "coastal" ? Color.Primary : Color.Default)"
                       OnClick="@(() => LoadRoute("coastal"))">
                Coastal Route
            </MudButton>
            <MudButton Variant="Variant.Filled"
                       Color="@(_selectedRoute == "mountain" ? Color.Primary : Color.Default)"
                       OnClick="@(() => LoadRoute("mountain"))">
                Mountain Pass
            </MudButton>
            <MudButton Variant="Variant.Filled"
                       Color="@(_selectedRoute == "valley" ? Color.Primary : Color.Default)"
                       OnClick="@(() => LoadRoute("valley"))">
                Valley Route
            </MudButton>
        </MudButtonGroup>
    </MudPaper>

    <HonuaMap Id="compare-map"
              Center="new[] { -122.4194, 37.7749 }"
              Zoom="9"
              Style="outdoors"
              Height="500px" />

    <HonuaElevationProfile
        SyncWith="compare-map"
        Position="bottom-right"
        Width="700px"
        ElevationSource="ElevationSource.MapboxAPI"
        ApiKey="@_mapboxApiKey"
        SamplePoints="150"
        ShowStatistics="true"
        ShowGradeColors="true"
        ChartHeight="300"
        OnProfileGenerated="CompareRoutes" />

    @if (_routeComparison.Count > 0)
    {
        <MudPaper Class="pa-4 mt-4">
            <MudText Typo="Typo.h6" Class="mb-4">Route Comparison</MudText>
            <MudTable Items="_routeComparison" Dense="true">
                <HeaderContent>
                    <MudTh>Route</MudTh>
                    <MudTh>Distance</MudTh>
                    <MudTh>Climbing</MudTh>
                    <MudTh>Max Grade</MudTh>
                    <MudTh>Est. Time</MudTh>
                    <MudTh>Difficulty</MudTh>
                    <MudTh>Recommended</MudTh>
                </HeaderContent>
                <RowTemplate>
                    <MudTd>@context.Name</MudTd>
                    <MudTd>@context.Distance</MudTd>
                    <MudTd>@context.Climbing</MudTd>
                    <MudTd>@context.MaxGrade</MudTd>
                    <MudTd>@context.EstTime</MudTd>
                    <MudTd>
                        <MudChip Size="Size.Small" Color="@GetDifficultyColor(context.Difficulty)">
                            @context.Difficulty
                        </MudChip>
                    </MudTd>
                    <MudTd>
                        @if (context.IsRecommended)
                        {
                            <MudIcon Icon="@Icons.Material.Filled.Star" Color="Color.Warning" />
                        }
                    </MudTd>
                </RowTemplate>
            </MudTable>
        </MudPaper>
    }
</div>

@code {
    private string _mapboxApiKey = "pk.your_mapbox_token";
    private string _selectedRoute = "";
    private List<RouteComparison> _routeComparison = new();

    private class RouteComparison
    {
        public string Name { get; set; } = "";
        public string Distance { get; set; } = "";
        public string Climbing { get; set; } = "";
        public string MaxGrade { get; set; } = "";
        public string EstTime { get; set; } = "";
        public string Difficulty { get; set; } = "";
        public bool IsRecommended { get; set; }
        public double Score { get; set; }
    }

    private Dictionary<string, List<double[]>> _routes = new()
    {
        ["coastal"] = new List<double[]>
        {
            new[] { -122.5, 37.7 },
            new[] { -122.4, 37.75 },
            new[] { -122.3, 37.8 }
        },
        ["mountain"] = new List<double[]>
        {
            new[] { -122.5, 37.7 },
            new[] { -122.45, 37.8 },
            new[] { -122.3, 37.8 }
        },
        ["valley"] = new List<double[]>
        {
            new[] { -122.5, 37.7 },
            new[] { -122.4, 37.7 },
            new[] { -122.3, 37.8 }
        }
    };

    private async Task LoadRoute(string routeName)
    {
        _selectedRoute = routeName;
        var coordinates = _routes[routeName];

        await Bus.PublishAsync(new FeatureDrawnMessage
        {
            FeatureId = $"route-{routeName}",
            GeometryType = "LineString",
            Geometry = new
            {
                type = "LineString",
                coordinates = coordinates
            },
            ComponentId = "elevation-1"
        });
    }

    private void CompareRoutes(ElevationProfile profile)
    {
        var difficulty = CalculateDifficulty(profile);
        var score = CalculateRouteScore(profile);

        var comparison = new RouteComparison
        {
            Name = _selectedRoute,
            Distance = $"{profile.TotalDistance / 1000:F1} km",
            Climbing = $"{profile.ElevationGain:F0} m",
            MaxGrade = $"{profile.MaxGrade:F1}%",
            EstTime = FormatTime(profile.TimeEstimate?.TotalMinutes ?? 0),
            Difficulty = difficulty,
            Score = score
        };

        // Add or update in comparison
        _routeComparison.RemoveAll(r => r.Name == _selectedRoute);
        _routeComparison.Add(comparison);

        // Mark recommended route (lowest score = best)
        var bestRoute = _routeComparison.MinBy(r => r.Score);
        foreach (var route in _routeComparison)
        {
            route.IsRecommended = route == bestRoute;
        }

        StateHasChanged();
    }

    private string CalculateDifficulty(ElevationProfile profile)
    {
        var score = (profile.TotalDistance / 1000) * 0.5 + profile.ElevationGain / 50;

        if (score < 20) return "Easy";
        if (score < 40) return "Moderate";
        if (score < 60) return "Hard";
        return "Very Hard";
    }

    private double CalculateRouteScore(ElevationProfile profile)
    {
        // Lower score is better
        return (profile.TotalDistance / 1000) * 1.0 +
               profile.ElevationGain * 0.5 +
               Math.Abs(profile.MaxGrade) * 2.0;
    }

    private string FormatTime(double minutes)
    {
        var hours = (int)(minutes / 60);
        var mins = (int)(minutes % 60);
        return hours > 0 ? $"{hours}h {mins}m" : $"{mins}m";
    }

    private Color GetDifficultyColor(string difficulty)
    {
        return difficulty switch
        {
            "Easy" => Color.Success,
            "Moderate" => Color.Info,
            "Hard" => Color.Warning,
            "Very Hard" => Color.Error,
            _ => Color.Default
        };
    }
}
```

**Key Features:**
- Multiple route comparison
- Route scoring system
- Recommended route selection
- Side-by-side statistics
- Interactive route switching

---

## Common Patterns

### Listen to Profile Generation

```csharp
Bus.Subscribe<ElevationProfileGeneratedMessage>(args =>
{
    var message = args.Message;
    Console.WriteLine($"Profile generated: {message.TotalDistance}m, Gain: {message.ElevationGain}m");
});
```

### Handle Errors

```csharp
private Task OnProfileError(string error)
{
    _snackbar.Add($"Error generating profile: {error}", Severity.Error);
    return Task.CompletedTask;
}
```

### Custom Export

```csharp
private async Task ExportCustom(ElevationProfile profile)
{
    var json = JsonSerializer.Serialize(profile, new JsonSerializerOptions
    {
        WriteIndented = true
    });

    await JS.InvokeVoidAsync("downloadFile", json, "profile.json", "application/json");
}
```

## Tips and Best Practices

1. **Choose appropriate sample count** - Balance accuracy vs performance
2. **Use correct elevation source** - Match to your region and accuracy needs
3. **Cache results** - Don't regenerate profiles unnecessarily
4. **Handle errors gracefully** - APIs can fail, have fallbacks
5. **Optimize for mobile** - Reduce chart height and stats on small screens
6. **Use ComponentBus** - Enable component communication
7. **Export options** - Let users save their analysis

## Next Steps

- See [README.md](./README.md) for complete API documentation
- Explore other MapSDK components for enhanced functionality
- Check out the Demo application for live examples
