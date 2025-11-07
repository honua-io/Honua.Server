# HonuaAnalysis Examples

Practical real-world examples of spatial analysis operations using the HonuaAnalysis component.

## Table of Contents

1. [School Buffer Analysis](#1-school-buffer-analysis)
2. [Overlapping Jurisdictions](#2-overlapping-jurisdictions)
3. [Merging Adjacent Parcels](#3-merging-adjacent-parcels)
4. [Service Area Coverage](#4-service-area-coverage)
5. [Census Tract Aggregation](#5-census-tract-aggregation)
6. [Emergency Service Routing](#6-emergency-service-routing)
7. [Real Estate Market Analysis](#7-real-estate-market-analysis)
8. [Environmental Impact Zones](#8-environmental-impact-zones)

---

## 1. School Buffer Analysis

**Scenario**: Find all properties within 500 meters of a school to identify potential enrollment.

### Use Case
A school district wants to identify all residential properties within walking distance (500m) of each school to estimate enrollment and plan transportation services.

### Implementation

```razor
@page "/school-analysis"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Analysis
@using Honua.MapSDK.Models

<div class="analysis-demo">
    <HonuaMap Id="schoolMap"
              Center="@(new[] { -122.4194, 37.7749 })"
              Zoom="13" />

    <HonuaAnalysis
        SyncWith="schoolMap"
        OnAnalysisCompleted="@HandleBufferComplete" />
</div>

@code {
    private List<Feature> schools = new();
    private List<Feature> properties = new();
    private List<AnalysisResult> bufferResults = new();

    protected override async Task OnInitializedAsync()
    {
        // Load school locations
        schools = await LoadSchoolData();

        // Load property parcels
        properties = await LoadPropertyData();
    }

    private async Task HandleBufferComplete(AnalysisResult result)
    {
        if (!result.Success) return;

        bufferResults.Add(result);

        // Find properties within buffer
        var buffer = result.Result;
        var propertiesInBuffer = await FindPropertiesInBuffer(properties, buffer);

        Console.WriteLine($"Found {propertiesInBuffer.Count} properties within 500m of school");
        Console.WriteLine($"Buffer area: {result.Statistics["area"]:N2} sq meters");
        Console.WriteLine($"Estimated students: {propertiesInBuffer.Count * 0.5:N0}");
    }

    private async Task<List<Feature>> FindPropertiesInBuffer(
        List<Feature> properties,
        object buffer)
    {
        // Use point-in-polygon analysis
        var analysisService = new SpatialAnalysisService();
        var result = await analysisService.PointsWithinPolygonAsync(
            properties,
            new Feature { Id = "buffer", Geometry = buffer, Attributes = new() }
        );

        return ParseFeatures(result.Result);
    }
}
```

### Expected Results
- **Buffer Area**: ~785,398 sq meters (78.5 hectares)
- **Properties Found**: Varies by density
- **Analysis Time**: < 100ms for typical dataset

### Visualization
- School location: Blue marker
- 500m buffer: Semi-transparent blue circle
- Properties within: Highlighted in green
- Properties outside: Gray

---

## 2. Overlapping Jurisdictions

**Scenario**: Identify areas where city and county jurisdictions overlap for tax purposes.

### Use Case
A regional planning agency needs to identify areas where city limits overlap with special tax districts to resolve boundary disputes and clarify tax responsibilities.

### Implementation

```razor
@page "/jurisdiction-analysis"
@inject SpatialAnalysisService AnalysisService

<HonuaMap Id="jurisdictionMap" />

<HonuaAnalysis
    SyncWith="jurisdictionMap"
    AvailableOperations="@(new List<AnalysisOperationType> {
        AnalysisOperationType.Intersect
    })"
    OnAnalysisCompleted="@HandleIntersection" />

@code {
    private Feature cityBoundary;
    private Feature taxDistrict;
    private AnalysisResult intersectionResult;

    protected override async Task OnInitializedAsync()
    {
        cityBoundary = await LoadCityBoundary("San Francisco");
        taxDistrict = await LoadTaxDistrict("SFMTA");
    }

    private async Task PerformIntersectionAnalysis()
    {
        intersectionResult = await AnalysisService.IntersectAsync(
            cityBoundary,
            taxDistrict
        );

        if (intersectionResult.Success)
        {
            var overlapArea = intersectionResult.Statistics["area"];
            var cityArea = await GetAreaAsync(cityBoundary);
            var percentOverlap = (overlapArea / cityArea) * 100;

            await ShowResults(new {
                OverlapArea = $"{(overlapArea / 1_000_000):N2} sq km",
                PercentOfCity = $"{percentOverlap:N1}%",
                TaxImplication = CalculateTaxImpact(overlapArea)
            });
        }
    }

    private async Task HandleIntersection(AnalysisResult result)
    {
        if (!result.Success)
        {
            await ShowError("No overlap found between jurisdictions");
            return;
        }

        // Generate report
        var report = GenerateJurisdictionReport(result);
        await ExportReport(report);
    }

    private string GenerateJurisdictionReport(AnalysisResult result)
    {
        var area = result.Statistics["area"];
        return $@"
        Jurisdiction Overlap Analysis Report
        Generated: {DateTime.Now:yyyy-MM-dd HH:mm}

        Overlapping Area: {area / 1_000_000:N2} square kilometers
        Percentage: {(area / 600_000_000 * 100):N1}% of city area

        Affected Parcels: (To be calculated)
        Tax Impact: (To be calculated)

        Recommendations:
        - Review zoning for overlap areas
        - Clarify tax collection procedures
        - Update jurisdiction maps
        ";
    }
}
```

### Expected Results
- **Overlap Area**: Varies by jurisdiction
- **Statistics**: Area in sq km, percentage overlap
- **Output**: Detailed report + map visualization

---

## 3. Merging Adjacent Parcels

**Scenario**: Combine adjacent land parcels owned by the same owner.

### Use Case
A property management company wants to merge all adjacent parcels they own into single unified properties for simplified management and potential development.

### Implementation

```razor
@page "/parcel-union"
@inject SpatialAnalysisService AnalysisService

<div class="parcel-analysis">
    <HonuaMap Id="parcelMap" />

    <div class="controls">
        <MudTextField @bind-Value="ownerName" Label="Owner Name" />
        <MudButton OnClick="@MergeParcelsByOwner" Color="Color.Primary">
            Merge Parcels
        </MudButton>
    </div>

    @if (mergeResult != null)
    {
        <div class="results">
            <h3>Merge Results</h3>
            <p>Original Parcels: @originalParcelCount</p>
            <p>Merged Into: @mergeResult.FeatureCount parcel(s)</p>
            <p>Total Area: @FormatArea(mergeResult.Statistics["area"])</p>
            <p>Perimeter: @FormatDistance(mergeResult.Statistics.GetValueOrDefault("perimeter", 0))</p>
        </div>
    }
</div>

@code {
    private string ownerName = "";
    private int originalParcelCount = 0;
    private AnalysisResult mergeResult;

    private async Task MergeParcelsByOwner()
    {
        // 1. Find all parcels owned by this owner
        var parcels = await LoadParcels();
        var ownerParcels = parcels
            .Where(p => p.Attributes.GetValueOrDefault("owner", "").ToString() == ownerName)
            .ToList();

        originalParcelCount = ownerParcels.Count;

        if (ownerParcels.Count == 0)
        {
            await ShowError($"No parcels found for owner: {ownerName}");
            return;
        }

        // 2. Group by adjacency (simplified - real impl would check topology)
        var adjacentGroups = GroupAdjacentParcels(ownerParcels);

        // 3. Union each group
        var mergedParcels = new List<Feature>();
        foreach (var group in adjacentGroups)
        {
            var unionResult = await AnalysisService.UnionAsync(group);
            if (unionResult.Success)
            {
                mergedParcels.Add(CreateFeatureFromResult(unionResult));
            }
        }

        // 4. Calculate final statistics
        mergeResult = new AnalysisResult
        {
            OperationType = "Merge",
            Result = mergedParcels,
            FeatureCount = mergedParcels.Count,
            Success = true,
            Statistics = new Dictionary<string, double>
            {
                ["area"] = mergedParcels.Sum(p => CalculateArea(p)),
                ["originalCount"] = originalParcelCount,
                ["mergedCount"] = mergedParcels.Count
            }
        };

        await AddMergedParcelsToMap(mergedParcels);
    }

    private List<List<Feature>> GroupAdjacentParcels(List<Feature> parcels)
    {
        // Simplified grouping - in reality, use spatial indexing
        var groups = new List<List<Feature>>();
        var processed = new HashSet<string>();

        foreach (var parcel in parcels)
        {
            if (processed.Contains(parcel.Id)) continue;

            var group = new List<Feature> { parcel };
            processed.Add(parcel.Id);

            // Find adjacent parcels
            var adjacent = FindAdjacentParcels(parcel, parcels.Where(p => !processed.Contains(p.Id)).ToList());
            group.AddRange(adjacent);
            adjacent.ForEach(a => processed.Add(a.Id));

            groups.Add(group);
        }

        return groups;
    }

    private string FormatArea(double sqMeters)
    {
        if (sqMeters > 10000)
            return $"{sqMeters / 10000:N2} hectares";
        else
            return $"{sqMeters:N0} sq meters";
    }
}
```

### Expected Results
- **Before**: 15 separate parcels
- **After**: 3 merged parcels
- **Total Area**: Unchanged (conservation of area)
- **Boundary**: Simplified exterior boundary

---

## 4. Service Area Coverage

**Scenario**: Determine areas NOT covered by emergency services.

### Use Case
A city planning department needs to identify residential areas that fall outside the 5-minute response time for fire stations to plan new station locations.

### Implementation

```razor
@page "/service-coverage"
@inject SpatialAnalysisService AnalysisService

<HonuaMap Id="coverageMap" />

<HonuaAnalysis
    SyncWith="coverageMap"
    OnAnalysisCompleted="@AnalyzeCoverage" />

<div class="coverage-results">
    @if (coverageGaps.Any())
    {
        <MudAlert Severity="Severity.Warning">
            Found @coverageGaps.Count areas with inadequate coverage
        </MudAlert>

        <MudDataGrid Items="@coverageGaps">
            <Columns>
                <Column T="CoverageGap" Field="@nameof(CoverageGap.AreaName)" />
                <Column T="CoverageGap" Field="@nameof(CoverageGap.Population)" Format="N0" />
                <Column T="CoverageGap" Field="@nameof(CoverageGap.DistanceToNearest)" Title="Distance (km)" Format="N2" />
                <Column T="CoverageGap" Field="@nameof(CoverageGap.Priority)" />
            </Columns>
        </MudDataGrid>
    }
</div>

@code {
    private List<Feature> fireStations = new();
    private Feature cityBoundary;
    private List<CoverageGap> coverageGaps = new();

    protected override async Task OnInitializedAsync()
    {
        fireStations = await LoadFireStations();
        cityBoundary = await LoadCityBoundary();
    }

    private async Task AnalyzeCoverage(AnalysisResult result)
    {
        // 1. Create 5km buffers around each fire station
        var serviceAreas = new List<Feature>();
        foreach (var station in fireStations)
        {
            var buffer = await AnalysisService.BufferAsync(
                station,
                5,
                DistanceUnit.Kilometers
            );
            serviceAreas.Add(CreateFeatureFromResult(buffer));
        }

        // 2. Union all service areas
        var totalCoverage = await AnalysisService.UnionAsync(serviceAreas);

        // 3. Subtract from city boundary to find gaps
        var cityFeature = new Feature
        {
            Id = "city",
            Geometry = cityBoundary.Geometry,
            Attributes = new()
        };

        var coverageFeature = CreateFeatureFromResult(totalCoverage);
        var gaps = await AnalysisService.DifferenceAsync(cityFeature, coverageFeature);

        if (gaps.Success && gaps.FeatureCount > 0)
        {
            // 4. Analyze each gap
            coverageGaps = await AnalyzeGaps(gaps.Result);

            // 5. Prioritize based on population density
            coverageGaps = coverageGaps
                .OrderByDescending(g => g.Priority)
                .ToList();

            // 6. Visualize on map
            await VisualizeCoverageGaps(coverageGaps);
        }
    }

    private async Task<List<CoverageGap>> AnalyzeGaps(object gapGeometry)
    {
        var gaps = new List<CoverageGap>();
        var populationData = await LoadPopulationData();

        // Parse gap features
        var gapFeatures = ParseFeatures(gapGeometry);

        foreach (var gap in gapFeatures)
        {
            var area = await AnalysisService.CalculateAreaAsync(gap);
            var centroid = await AnalysisService.CalculateCentroidAsync(gap);

            // Find nearest fire station
            var nearest = await AnalysisService.NearestNeighborAsync(
                CreateFeatureFromResult(centroid),
                fireStations,
                1
            );

            // Estimate population in gap
            var population = await EstimatePopulation(gap, populationData);

            gaps.Add(new CoverageGap
            {
                AreaName = $"Gap {gaps.Count + 1}",
                Area = area.Statistics["area"],
                Population = population,
                DistanceToNearest = nearest.Statistics["nearestDistance"],
                Priority = CalculatePriority(population, area.Statistics["area"])
            });
        }

        return gaps;
    }

    private int CalculatePriority(int population, double area)
    {
        // Higher priority for more people and larger areas
        var density = population / (area / 1_000_000); // per sq km
        if (density > 1000) return 5; // Critical
        if (density > 500) return 4;  // High
        if (density > 100) return 3;  // Medium
        if (density > 10) return 2;   // Low
        return 1;                     // Minimal
    }

    public class CoverageGap
    {
        public string AreaName { get; set; }
        public double Area { get; set; }
        public int Population { get; set; }
        public double DistanceToNearest { get; set; }
        public int Priority { get; set; }
    }
}
```

### Expected Results
- **Total Coverage**: 85% of city area
- **Gap Areas**: 3 zones identified
- **Priority 1 Gap**: 12,000 residents, 8.2km from nearest station
- **Recommendation**: Build new station in Priority 1 area

---

## 5. Census Tract Aggregation

**Scenario**: Combine census tracts by county for regional statistics.

### Use Case
A regional planning organization needs to aggregate census data from tract level to county level for demographic reports.

### Implementation

```razor
@page "/census-aggregation"
@inject SpatialAnalysisService AnalysisService

<HonuaMap Id="censusMap" />

<MudButton OnClick="@AggregateByCounty" Color="Color.Primary">
    Dissolve by County
</MudButton>

@if (aggregationResults != null)
{
    <MudDataGrid Items="@aggregationResults">
        <Columns>
            <Column T="CountyStats" Field="@nameof(CountyStats.CountyName)" />
            <Column T="CountyStats" Field="@nameof(CountyStats.TractCount)" Title="Tracts" />
            <Column T="CountyStats" Field="@nameof(CountyStats.TotalPopulation)" Format="N0" />
            <Column T="CountyStats" Field="@nameof(CountyStats.TotalArea)" Title="Area (sq km)" Format="N2" />
            <Column T="CountyStats" Field="@nameof(CountyStats.Density)" Title="Density (per sq km)" Format="N1" />
        </Columns>
    </MudDataGrid>
}

@code {
    private List<Feature> censusTracts = new();
    private List<CountyStats> aggregationResults;

    protected override async Task OnInitializedAsync()
    {
        censusTracts = await LoadCensusTracts();
    }

    private async Task AggregateByCounty()
    {
        // 1. Dissolve tracts by county field
        var dissolveResult = await AnalysisService.DissolveAsync(
            censusTracts,
            "county_name"
        );

        if (!dissolveResult.Success)
        {
            await ShowError("Dissolve operation failed");
            return;
        }

        // 2. Calculate statistics for each county
        aggregationResults = new List<CountyStats>();
        var countyFeatures = ParseFeatures(dissolveResult.Result);

        foreach (var county in countyFeatures)
        {
            var countyName = county.Attributes["county_name"].ToString();
            var tractsInCounty = censusTracts
                .Where(t => t.Attributes["county_name"].ToString() == countyName)
                .ToList();

            var area = await AnalysisService.CalculateAreaAsync(county);
            var areaSqKm = area.Statistics["area"] / 1_000_000;

            var totalPopulation = tractsInCounty
                .Sum(t => Convert.ToInt32(t.Attributes.GetValueOrDefault("population", 0)));

            aggregationResults.Add(new CountyStats
            {
                CountyName = countyName,
                TractCount = tractsInCounty.Count,
                TotalPopulation = totalPopulation,
                TotalArea = areaSqKm,
                Density = totalPopulation / areaSqKm
            });
        }

        // 3. Add dissolved boundaries to map
        await AddDissolvedCountiesToMap(countyFeatures);
    }

    public class CountyStats
    {
        public string CountyName { get; set; }
        public int TractCount { get; set; }
        public int TotalPopulation { get; set; }
        public double TotalArea { get; set; }
        public double Density { get; set; }
    }
}
```

### Expected Results
| County | Tracts | Population | Area (sq km) | Density |
|--------|--------|------------|--------------|---------|
| San Francisco | 197 | 873,965 | 121.4 | 7,199 |
| Alameda | 361 | 1,671,329 | 1,910.3 | 875 |
| Contra Costa | 226 | 1,153,526 | 1,865.0 | 619 |

---

## 6. Emergency Service Routing

**Scenario**: Find 3 nearest hospitals to accident location.

### Implementation

```razor
@page "/emergency-routing"
@inject SpatialAnalysisService AnalysisService

<HonuaMap Id="emergencyMap" @ref="mapRef" OnMapClick="@HandleMapClick" />

@if (nearestHospitals.Any())
{
    <div class="hospital-list">
        <h3>Nearest Hospitals</h3>
        @foreach (var (hospital, index) in nearestHospitals.Select((h, i) => (h, i)))
        {
            <MudCard Class="hospital-card">
                <MudCardContent>
                    <MudText Typo="Typo.h6">@(index + 1). @hospital.Name</MudText>
                    <MudText>Distance: @hospital.Distance km</MudText>
                    <MudText>Estimated Time: @hospital.EstimatedTime minutes</MudText>
                    <MudText>Trauma Level: @hospital.TraumaLevel</MudText>
                </MudCardContent>
                <MudCardActions>
                    <MudButton Color="Color.Primary" OnClick="@(() => ShowRoute(hospital))">
                        Show Route
                    </MudButton>
                </MudCardActions>
            </MudCard>
        }
    </div>
}

@code {
    private HonuaMap mapRef;
    private List<Feature> hospitals = new();
    private List<HospitalInfo> nearestHospitals = new();
    private Feature accidentLocation;

    protected override async Task OnInitializedAsync()
    {
        hospitals = await LoadHospitals();
    }

    private async Task HandleMapClick(MapClickEventArgs args)
    {
        // Create point feature for accident location
        accidentLocation = new Feature
        {
            Id = "accident",
            Geometry = new
            {
                type = "Point",
                coordinates = new[] { args.Longitude, args.Latitude }
            },
            Attributes = new()
        };

        // Find 3 nearest hospitals
        var result = await AnalysisService.NearestNeighborAsync(
            accidentLocation,
            hospitals,
            3
        );

        if (result.Success)
        {
            nearestHospitals = ParseNearestHospitals(result);
            await VisualizeRoutes(nearestHospitals);
        }
    }

    private List<HospitalInfo> ParseNearestHospitals(AnalysisResult result)
    {
        var features = ParseFeatures(result.Result);
        return features.Select(f => new HospitalInfo
        {
            Name = f.Attributes["name"].ToString(),
            Distance = Convert.ToDouble(f.Attributes["distance"]),
            EstimatedTime = CalculateEmergencyTime(Convert.ToDouble(f.Attributes["distance"])),
            TraumaLevel = f.Attributes.GetValueOrDefault("trauma_level", "Unknown").ToString(),
            Coordinates = ParseCoordinates(f.Geometry)
        }).ToList();
    }

    private int CalculateEmergencyTime(double distanceKm)
    {
        // Assume emergency vehicles average 60 km/h in city
        return (int)Math.Ceiling(distanceKm / 60.0 * 60.0); // minutes
    }

    public class HospitalInfo
    {
        public string Name { get; set; }
        public double Distance { get; set; }
        public int EstimatedTime { get; set; }
        public string TraumaLevel { get; set; }
        public double[] Coordinates { get; set; }
    }
}
```

### Expected Results
1. **SF General Hospital**: 2.3 km, 4 mins, Level I Trauma
2. **Kaiser Permanente**: 3.8 km, 6 mins, Level II Trauma
3. **UCSF Medical Center**: 5.1 km, 8 mins, Level I Trauma

---

## 7. Real Estate Market Analysis

**Scenario**: Multi-ring buffer analysis for property value assessment.

### Implementation

```razor
@page "/market-analysis"
@inject SpatialAnalysisService AnalysisService

<div class="market-analysis">
    <HonuaMap Id="marketMap" />

    <div class="analysis-controls">
        <h3>Market Analysis Parameters</h3>
        <MudSelect @bind-Value="selectedPOI" Label="Point of Interest">
            <MudSelectItem Value="@("Transit Station")">Transit Station</MudSelectItem>
            <MudSelectItem Value="@("School")">School</MudSelectItem>
            <MudSelectItem Value="@("Park")">Park</MudSelectItem>
            <MudSelectItem Value="@("Shopping Center")">Shopping Center</MudSelectItem>
        </MudSelect>

        <MudButton OnClick="@AnalyzeMarket" Color="Color.Primary">
            Analyze Impact
        </MudButton>
    </div>

    @if (marketResults != null)
    {
        <div class="market-results">
            <h3>Property Value Impact by Distance</h3>
            <MudSimpleTable>
                <thead>
                    <tr>
                        <th>Distance Range</th>
                        <th>Properties</th>
                        <th>Avg Value</th>
                        <th>Premium</th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var zone in marketResults.Zones)
                    {
                        <tr>
                            <td>@zone.DistanceRange</td>
                            <td>@zone.PropertyCount</td>
                            <td>$@zone.AverageValue.ToString("N0")</td>
                            <td style="color: @(zone.Premium > 0 ? "green" : "red")">
                                @zone.Premium.ToString("+0.0%;-0.0%")
                            </td>
                        </tr>
                    }
                </tbody>
            </MudSimpleTable>
        </div>
    }
</div>

@code {
    private string selectedPOI = "Transit Station";
    private Feature poiLocation;
    private List<Feature> properties = new();
    private MarketAnalysisResult marketResults;

    protected override async Task OnInitializedAsync()
    {
        properties = await LoadProperties();
        poiLocation = await LoadPOI(selectedPOI);
    }

    private async Task AnalyzeMarket()
    {
        // Create multi-ring buffers: 0-500m, 500-1000m, 1000-2000m, 2000m+
        var distances = new List<double> { 0.5, 1.0, 2.0 }; // kilometers

        var bufferResult = await AnalysisService.MultiRingBufferAsync(
            poiLocation,
            distances,
            DistanceUnit.Kilometers
        );

        if (!bufferResult.Success) return;

        var rings = ParseFeatures(bufferResult.Result);
        var zones = new List<MarketZone>();

        for (int i = 0; i < rings.Count; i++)
        {
            // Find properties in this ring
            var propertiesInRing = await FindPropertiesInRing(
                properties,
                i == 0 ? null : rings[i - 1],
                rings[i]
            );

            var avgValue = propertiesInRing
                .Average(p => Convert.ToDouble(p.Attributes.GetValueOrDefault("assessed_value", 0)));

            // Calculate premium compared to citywide average
            var cityAverage = 750000; // Example baseline
            var premium = (avgValue - cityAverage) / cityAverage;

            zones.Add(new MarketZone
            {
                DistanceRange = GetDistanceRange(i, distances),
                PropertyCount = propertiesInRing.Count,
                AverageValue = avgValue,
                Premium = premium
            });
        }

        marketResults = new MarketAnalysisResult { Zones = zones };

        // Visualize rings on map
        await VisualizeMarketZones(rings, zones);
    }

    private string GetDistanceRange(int index, List<double> distances)
    {
        if (index == 0)
            return $"0 - {distances[0]} km";
        else if (index < distances.Count)
            return $"{distances[index - 1]} - {distances[index]} km";
        else
            return $"> {distances[distances.Count - 1]} km";
    }

    public class MarketAnalysisResult
    {
        public List<MarketZone> Zones { get; set; }
    }

    public class MarketZone
    {
        public string DistanceRange { get; set; }
        public int PropertyCount { get; set; }
        public double AverageValue { get; set; }
        public double Premium { get; set; }
    }
}
```

### Expected Results

| Distance Range | Properties | Avg Value | Premium |
|----------------|------------|-----------|---------|
| 0 - 0.5 km | 1,245 | $925,000 | +23.3% |
| 0.5 - 1.0 km | 2,891 | $815,000 | +8.7% |
| 1.0 - 2.0 km | 5,672 | $765,000 | +2.0% |
| > 2.0 km | 12,338 | $698,000 | -6.9% |

**Insights**:
- Properties within 500m of transit command 23% premium
- Value impact diminishes rapidly beyond 1km
- Transit proximity is a significant value driver

---

## 8. Environmental Impact Zones

**Scenario**: Model noise pollution zones around an airport.

### Implementation

```razor
@page "/environmental-impact"
@inject SpatialAnalysisService AnalysisService

<HonuaMap Id="impactMap" />

<div class="impact-analysis">
    <h3>Airport Noise Impact Analysis</h3>

    <MudButton OnClick="@CalculateNoiseZones" Color="Color.Warning">
        Calculate Impact Zones
    </MudButton>

    @if (impactZones != null)
    {
        <MudExpansionPanels MultiExpansion="true">
            @foreach (var zone in impactZones)
            {
                <MudExpansionPanel>
                    <TitleContent>
                        <div class="zone-header">
                            <MudChip Color="@GetZoneColor(zone.NoiseLevel)">
                                @zone.NoiseLevel dB
                            </MudChip>
                            <span>@zone.AffectedResidents residents affected</span>
                        </div>
                    </TitleContent>
                    <ChildContent>
                        <MudText>Distance: @zone.Distance km from airport</MudText>
                        <MudText>Area: @zone.Area sq km</MudText>
                        <MudText>Residential Units: @zone.ResidentialUnits</MudText>
                        <MudText>Schools: @zone.SchoolCount</MudText>
                        <MudText>Hospitals: @zone.HospitalCount</MudText>
                        <MudText>
                            Compliance:
                            <MudChip Color="@(zone.IsCompliant ? Color.Success : Color.Error)" Size="Size.Small">
                                @(zone.IsCompliant ? "Within limits" : "Exceeds limits")
                            </MudChip>
                        </MudText>
                        <MudText Typo="Typo.caption" Class="mt-2">
                            Mitigation: @zone.MitigationRecommendation
                        </MudText>
                    </ChildContent>
                </MudExpansionPanel>
            }
        </MudExpansionPanels>
    }
</div>

@code {
    private Feature airportLocation;
    private List<NoiseImpactZone> impactZones;

    protected override async Task OnInitializedAsync()
    {
        airportLocation = await LoadAirportLocation();
    }

    private async Task CalculateNoiseZones()
    {
        // Model noise zones: 75dB, 65dB, 55dB
        var noiseDistances = new Dictionary<int, double>
        {
            { 75, 1.5 },  // 75dB at 1.5km
            { 65, 3.5 },  // 65dB at 3.5km
            { 55, 6.0 }   // 55dB at 6.0km
        };

        impactZones = new List<NoiseImpactZone>();

        // Create noise contour rings
        var distances = noiseDistances.Values.OrderBy(d => d).ToList();
        var bufferResult = await AnalysisService.MultiRingBufferAsync(
            airportLocation,
            distances,
            DistanceUnit.Kilometers
        );

        if (!bufferResult.Success) return;

        var rings = ParseFeatures(bufferResult.Result);

        for (int i = 0; i < rings.Count; i++)
        {
            var noiseLevel = noiseDistances.ElementAt(i).Key;
            var zone = rings[i];

            // Find affected structures
            var buildings = await FindBuildingsInZone(zone);
            var schools = buildings.Count(b => b.Attributes["type"].ToString() == "school");
            var hospitals = buildings.Count(b => b.Attributes["type"].ToString() == "hospital");
            var residential = buildings.Count(b => b.Attributes["type"].ToString() == "residential");

            // Calculate population
            var population = await EstimatePopulationInZone(zone);

            // Check compliance
            var isCompliant = CheckNoiseCompliance(noiseLevel, schools > 0, hospitals > 0);

            var area = await AnalysisService.CalculateAreaAsync(zone);

            impactZones.Add(new NoiseImpactZone
            {
                NoiseLevel = noiseLevel,
                Distance = distances[i],
                Area = area.Statistics["area"] / 1_000_000,
                AffectedResidents = population,
                ResidentialUnits = residential,
                SchoolCount = schools,
                HospitalCount = hospitals,
                IsCompliant = isCompliant,
                MitigationRecommendation = GetMitigationRecommendation(noiseLevel, schools, hospitals)
            });
        }

        await VisualizeNoiseZones(rings, impactZones);
    }

    private bool CheckNoiseCompliance(int noiseLevel, bool hasSchools, bool hasHospitals)
    {
        // EPA guidelines: 55dB day-night average
        if (noiseLevel >= 65 && (hasSchools || hasHospitals))
            return false; // Not compliant

        return noiseLevel < 75; // General compliance
    }

    private string GetMitigationRecommendation(int noiseLevel, int schools, int hospitals)
    {
        if (noiseLevel >= 75)
            return "Mandatory sound insulation, restrict flight operations, relocate sensitive facilities";
        else if (noiseLevel >= 65 && (schools > 0 || hospitals > 0))
            return "Install sound barriers, upgrade building insulation, restrict night flights";
        else if (noiseLevel >= 55)
            return "Monitor noise levels, consider sound insulation subsidies";
        else
            return "No immediate mitigation required, continue monitoring";
    }

    private Color GetZoneColor(int noiseLevel)
    {
        return noiseLevel >= 75 ? Color.Error :
               noiseLevel >= 65 ? Color.Warning :
               Color.Info;
    }

    public class NoiseImpactZone
    {
        public int NoiseLevel { get; set; }
        public double Distance { get; set; }
        public double Area { get; set; }
        public int AffectedResidents { get; set; }
        public int ResidentialUnits { get; set; }
        public int SchoolCount { get; set; }
        public int HospitalCount { get; set; }
        public bool IsCompliant { get; set; }
        public string MitigationRecommendation { get; set; }
    }
}
```

### Expected Results

**75 dB Zone (Critical)**
- Distance: 0-1.5 km
- Affected Residents: 3,245
- Schools: 2, Hospitals: 1
- **Compliance**: ❌ Exceeds EPA limits
- **Action Required**: Immediate mitigation

**65 dB Zone (High)**
- Distance: 1.5-3.5 km
- Affected Residents: 12,890
- Schools: 8, Hospitals: 0
- **Compliance**: ⚠️ Marginal
- **Action Recommended**: Sound insulation program

**55 dB Zone (Moderate)**
- Distance: 3.5-6.0 km
- Affected Residents: 35,670
- Schools: 15, Hospitals: 2
- **Compliance**: ✅ Within limits
- **Action**: Continue monitoring

---

## Performance Tips

1. **Large Datasets**: For > 5,000 features, consider server-side processing
2. **Caching**: Cache analysis results to avoid redundant calculations
3. **Simplification**: Simplify complex geometries before analysis
4. **Indexing**: Use spatial indexing for proximity queries
5. **Batching**: Batch multiple operations when possible

## Additional Resources

- [Turf.js Documentation](https://turfjs.org/)
- [GeoJSON Specification](https://geojson.org/)
- [Spatial Analysis Theory](https://en.wikipedia.org/wiki/Spatial_analysis)
- [EPA Noise Guidelines](https://www.epa.gov/noise)

## License

Part of Honua.MapSDK - See main SDK documentation for license information.
