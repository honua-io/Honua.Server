# Route Optimization (TSP/VRP) - Honua.MapSDK

Comprehensive multi-stop route optimization system implementing Traveling Salesman Problem (TSP) and Vehicle Routing Problem (VRP) algorithms.

## Features

- **Multiple Algorithms**
  - Nearest Neighbor (fast, O(n²), ~75-85% optimal)
  - 2-opt local search improvement
  - Hybrid approach (NN + 2-opt)
  - Multi-start optimization
  - Simulated Annealing (high quality)

- **Multi-Provider Support**
  - Client-side algorithms (no API required)
  - Mapbox Optimization API
  - OSRM Trip endpoint
  - GraphHopper Route Optimization
  - Valhalla Optimized Route

- **Advanced Constraints**
  - Time window constraints
  - Service duration at each stop
  - Vehicle capacity (VRP)
  - Maximum route duration/distance
  - Priority-based waypoints
  - Optional stops

- **Interactive UI**
  - Drag-and-drop waypoint reordering
  - Visual route comparison
  - Real-time metrics
  - Progress tracking
  - Export/import functionality

## Components

### HonuaRouteOptimizer

Main component for route optimization with visual feedback.

```razor
<HonuaRouteOptimizer
    Waypoints="@waypoints"
    Goal="OptimizationGoal.MinimizeDistance"
    Provider="OptimizationProvider.ClientSide"
    EnableTimeWindows="true"
    VehicleCapacity="1000"
    OnOptimizationComplete="@HandleComplete"
    OnApplyOptimization="@HandleApply" />
```

**Parameters:**
- `Waypoints` - List of waypoints to optimize
- `StartLocation` - Optional depot/origin
- `EndLocation` - Optional return to depot
- `Goal` - Optimization objective (Distance/Time/Cost)
- `Provider` - Algorithm provider
- `EnableTimeWindows` - Enable time constraints
- `VehicleCapacity` - Max vehicle capacity (VRP)
- `MaxRouteDuration` - Max route duration
- `AutoStart` - Auto-start on load
- `ShowComparison` - Show before/after comparison

**Events:**
- `OnOptimizationComplete` - Fires when optimization completes
- `OnApplyOptimization` - Fires when user applies result
- `OnOptimizationProgress` - Progress updates

### MultiStopPlanner

Interactive planning interface for managing waypoints and running optimizations.

```razor
<MultiStopPlanner
    OnWaypointsChanged="@HandleWaypointsChanged"
    OnOptimizationApplied="@HandleApplied"
    EnableMapIntegration="true"
    ShowAdvancedOptions="true" />
```

**Features:**
- Add/remove/reorder waypoints
- Drag-and-drop interface
- Waypoint property editor
- Settings panel
- Import/export
- Real-time distance estimation

### RouteComparison

Detailed comparison view between original and optimized routes.

```razor
<RouteComparison
    Result="@optimizationResult"
    ShowActions="true"
    EnableExport="true"
    OnAcceptRoute="@HandleAccept"
    OnRejectRoute="@HandleReject" />
```

**View Modes:**
- **Split View** - Side-by-side comparison
- **Overlay** - Highlight changes
- **Table** - Detailed metrics table

## Usage Examples

### Basic TSP Optimization

```csharp
// Create waypoints
var waypoints = new List<OptimizationWaypoint>
{
    new() { Location = new Coordinate(21.3099, -157.8581), Name = "Honolulu" },
    new() { Location = new Coordinate(21.4389, -158.0001), Name = "Pearl Harbor" },
    new() { Location = new Coordinate(21.5944, -158.1041), Name = "Haleiwa" },
    new() { Location = new Coordinate(21.6694, -157.9473), Name = "Turtle Bay" },
    new() { Location = new Coordinate(21.5294, -157.8481), Name = "Laie" }
};

// Optimize
var request = new OptimizationRequest
{
    Waypoints = waypoints,
    Goal = OptimizationGoal.MinimizeDistance
};

var result = await optimizationService.OptimizeAsync(
    request,
    OptimizationProvider.ClientSide);

// Result contains:
// - OptimizedSequence: New waypoint order
// - Metrics: Distance/time/cost savings
// - QualityScore: Solution quality estimate
```

### Time Window Constraints

```csharp
var waypoints = new List<OptimizationWaypoint>
{
    new()
    {
        Location = new Coordinate(21.3099, -157.8581),
        Name = "Morning delivery",
        TimeWindow = new TimeWindow
        {
            EarliestArrival = DateTime.Today.AddHours(8),
            LatestArrival = DateTime.Today.AddHours(10)
        },
        ServiceDurationSeconds = 600 // 10 minutes
    },
    new()
    {
        Location = new Coordinate(21.4389, -158.0001),
        Name = "Afternoon delivery",
        TimeWindow = new TimeWindow
        {
            EarliestArrival = DateTime.Today.AddHours(14),
            LatestArrival = DateTime.Today.AddHours(16)
        },
        ServiceDurationSeconds = 900 // 15 minutes
    }
};

var request = new OptimizationRequest
{
    Waypoints = waypoints,
    EnableTimeWindows = true,
    DepartureTime = DateTime.Today.AddHours(7)
};

var result = await optimizationService.OptimizeAsync(request);

// Check for violations
if (result.TimeWindowViolations.Any())
{
    foreach (var violation in result.TimeWindowViolations)
    {
        Console.WriteLine($"{violation.Waypoint.Name}: {violation.Type} by {violation.ViolationSeconds}s");
    }
}
```

### Vehicle Routing Problem (VRP)

```csharp
var waypoints = new List<OptimizationWaypoint>
{
    new() { Location = new Coordinate(21.3099, -157.8581), Demand = 100 },
    new() { Location = new Coordinate(21.4389, -158.0001), Demand = 150 },
    new() { Location = new Coordinate(21.5944, -158.1041), Demand = 200 },
    new() { Location = new Coordinate(21.6694, -157.9473), Demand = 120 },
    new() { Location = new Coordinate(21.5294, -157.8481), Demand = 180 }
};

var request = new OptimizationRequest
{
    Waypoints = waypoints,
    MultipleVehicles = true,
    NumberOfVehicles = 2,
    Vehicle = new VehicleConstraints
    {
        MaxCapacity = 300,
        MaxDurationSeconds = 28800, // 8 hours
        MaxDistanceMeters = 100000, // 100 km
        CostPerMeter = 0.001,
        CostPerSecond = 0.01
    }
};

var result = await optimizationService.OptimizeAsync(request);

// Access vehicle routes
foreach (var route in result.Routes)
{
    Console.WriteLine($"Vehicle {route.VehicleId}:");
    Console.WriteLine($"  Distance: {route.TotalDistanceMeters / 1000:F2} km");
    Console.WriteLine($"  Duration: {route.TotalDurationSeconds / 3600:F2} hours");
    Console.WriteLine($"  Load: {route.TotalLoad}");
    Console.WriteLine($"  Stops: {route.Waypoints.Count}");
}
```

### Client-Side JavaScript

```javascript
// Use client-side TSP solver
const waypoints = [
    { lat: 21.3099, lon: -157.8581, name: "Honolulu" },
    { lat: 21.4389, lon: -158.0001, name: "Pearl Harbor" },
    { lat: 21.5944, lon: -158.1041, name: "Haleiwa" }
];

const result = await HonuaTspSolver.optimizeRoute(waypoints, {
    algorithm: 'hybrid', // 'nearest-neighbor', '2-opt', 'multi-start', 'simulated-annealing'
}, (progress) => {
    console.log(`${progress.stage}: ${progress.percent}%`);
});

console.log(`Original distance: ${result.metrics.originalDistance.toFixed(2)}m`);
console.log(`Optimized distance: ${result.metrics.optimizedDistance.toFixed(2)}m`);
console.log(`Savings: ${result.metrics.savingsPercent}%`);

// Visualize on map
HonuaTspSolver.visualizeOnMap(map, result, {
    originalColor: '#ff0000',
    optimizedColor: '#00ff00'
});
```

## Algorithms

### Nearest Neighbor

**Complexity:** O(n²)
**Quality:** 75-85% optimal
**Best for:** < 100 waypoints, speed priority

Greedy algorithm that always selects the nearest unvisited waypoint.

### 2-opt Improvement

**Complexity:** O(n²) per iteration
**Quality:** 85-95% optimal
**Best for:** < 50 waypoints, quality priority

Local search that swaps edges to reduce total distance.

### Hybrid (NN + 2-opt)

**Complexity:** O(n²)
**Quality:** 88-95% optimal
**Best for:** Most use cases

Combines Nearest Neighbor initialization with 2-opt refinement.

### Multi-Start

**Complexity:** O(k × n²) where k = starts
**Quality:** 90-95% optimal
**Best for:** High quality, small problems

Runs optimization from multiple starting points and selects best.

### Simulated Annealing

**Complexity:** O(iterations × n)
**Quality:** 92-98% optimal
**Best for:** High quality, willing to wait

Probabilistic algorithm that can escape local minima.

## Performance Characteristics

| Waypoints | Algorithm | Time | Quality |
|-----------|-----------|------|---------|
| 5-10 | Multi-Start | < 100ms | 95%+ |
| 10-25 | Hybrid | < 500ms | 90%+ |
| 25-50 | Hybrid | 1-3s | 88%+ |
| 50-100 | Nearest Neighbor | 2-5s | 75%+ |
| 100+ | Nearest Neighbor | 5-15s | 70%+ |

## API Reference

### OptimizationRequest

```csharp
public class OptimizationRequest
{
    public List<OptimizationWaypoint> Waypoints { get; set; }
    public Coordinate? StartLocation { get; set; }
    public Coordinate? EndLocation { get; set; }
    public OptimizationGoal Goal { get; set; }
    public VehicleConstraints? Vehicle { get; set; }
    public bool EnableTimeWindows { get; set; }
    public DateTime? DepartureTime { get; set; }
    public bool MultipleVehicles { get; set; }
    public int NumberOfVehicles { get; set; }
}
```

### OptimizationResult

```csharp
public class OptimizationResult
{
    public List<OptimizationWaypoint> OptimizedSequence { get; set; }
    public List<OptimizationWaypoint> OriginalSequence { get; set; }
    public List<VehicleRoute> Routes { get; set; }
    public OptimizationMetrics Metrics { get; set; }
    public string Algorithm { get; set; }
    public string Provider { get; set; }
    public long ComputationTimeMs { get; set; }
    public bool IsOptimal { get; set; }
    public double QualityScore { get; set; }
    public List<TimeWindowViolation> TimeWindowViolations { get; set; }
}
```

### OptimizationMetrics

```csharp
public class OptimizationMetrics
{
    public double OriginalDistanceMeters { get; set; }
    public double OptimizedDistanceMeters { get; set; }
    public double DistanceSavedMeters { get; }
    public double DistanceSavingsPercent { get; }
    public int OriginalDurationSeconds { get; set; }
    public int OptimizedDurationSeconds { get; set; }
    public int DurationSavedSeconds { get; }
    public double DurationSavingsPercent { get; }
    public double OriginalCost { get; set; }
    public double OptimizedCost { get; set; }
    public double CostSaved { get; }
    public double CostSavingsPercent { get; }
    public int VehiclesUsed { get; set; }
}
```

## Best Practices

1. **Choose the Right Algorithm**
   - Small problems (< 15): Multi-Start
   - Medium problems (15-50): Hybrid
   - Large problems (> 50): Nearest Neighbor

2. **Set Realistic Constraints**
   - Time windows should be reasonable
   - Vehicle capacity should match actual needs
   - Consider service durations

3. **Use Progress Callbacks**
   - Provide user feedback for long optimizations
   - Allow cancellation
   - Show quality metrics

4. **Handle Violations**
   - Check for time window violations
   - Adjust constraints if needed
   - Consider manual adjustments

5. **Test with Real Data**
   - Use actual addresses/coordinates
   - Include realistic service times
   - Validate with domain experts

## Troubleshooting

**Problem:** Optimization takes too long
**Solution:** Use Nearest Neighbor for > 50 waypoints, or reduce MaxIterations

**Problem:** Quality is poor
**Solution:** Switch to Hybrid or Multi-Start algorithm

**Problem:** Time window violations
**Solution:** Relax time windows or adjust departure time

**Problem:** Memory issues with large problems
**Solution:** Use client-side solver with Web Workers

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0
