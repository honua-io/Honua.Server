# Debugging Guide

This guide covers debugging techniques for Honua Server across different IDEs and scenarios.

## Table of Contents

- [Visual Studio Code](#visual-studio-code)
- [Visual Studio 2022](#visual-studio-2022)
- [JetBrains Rider](#jetbrains-rider)
- [Common Debugging Scenarios](#common-debugging-scenarios)
- [Troubleshooting](#troubleshooting)
- [Performance Profiling](#performance-profiling)

## Visual Studio Code

### Prerequisites

Install the following extensions:
- **C# Dev Kit** (ms-dotnettools.csdevkit)
- **C#** (ms-dotnettools.csharp)

These are automatically recommended when you open the project (see `.vscode/extensions.json`).

### Launch Configurations

The project includes pre-configured launch settings in `.vscode/launch.json`:

#### 1. Debug Honua Server (Default)

Launches the main Honua.Server.Host application with debugging enabled.

**Usage:**
1. Press `F5` or select "Debug Honua Server" from the Run and Debug panel
2. The server will start at `http://localhost:8080`
3. Swagger UI available at `http://localhost:8080/swagger`

**Configuration:**
```json
{
    "name": "Debug Honua Server",
    "type": "coreclr",
    "request": "launch",
    "preLaunchTask": "build",
    "program": "${workspaceFolder}/src/Honua.Server.Host/bin/Debug/net9.0/Honua.Server.Host.dll",
    "cwd": "${workspaceFolder}/src/Honua.Server.Host",
    "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
    },
    "stopAtEntry": false
}
```

#### 2. Debug Tests

Debugs the currently open test file.

**Usage:**
1. Open a test file (e.g., `FeatureServiceTests.cs`)
2. Set breakpoints
3. Select "Debug Tests" from Run and Debug
4. Or use the "Debug Test" CodeLens above test methods

#### 3. Debug with Docker

Debugs the server running in Docker containers.

**Usage:**
1. Ensure Docker containers are running: `docker compose up -d`
2. Select "Attach to Docker" configuration
3. Choose the `honua-server` container

### Setting Breakpoints

#### Simple Breakpoint
Click in the left margin of any line of code, or press `F9` on the current line.

```csharp
public async Task<FeatureCollection> GetFeaturesAsync(string layerId)
{
    // Set breakpoint here by clicking in the margin
    var features = await _dataProvider.GetFeaturesAsync(layerId);
    return features;
}
```

#### Conditional Breakpoint
Right-click on a breakpoint → Edit Breakpoint → Add condition.

```csharp
// Break only when layerId equals "cities"
var features = await _dataProvider.GetFeaturesAsync(layerId);  // Condition: layerId == "cities"
```

#### Logpoint
Right-click in margin → Add Logpoint. Logs a message without stopping execution.

```csharp
// Logpoint: "Processing layer {layerId} with {features.Count} features"
ProcessFeatures(layerId, features);
```

### Debugging Tasks

Pre-configured tasks in `.vscode/tasks.json`:

- **Build** (`Ctrl+Shift+B`): Build the solution
- **Build and Run**: Build and start the server
- **Run Tests**: Execute all tests
- **Run Unit Tests**: Execute unit tests only
- **Format Code**: Run `dotnet format`
- **Clean**: Clean build artifacts

### Watch Window

Monitor variable values while debugging:

1. **Add to Watch**: Right-click variable → Add to Watch
2. **Watch Expressions**: Enter expressions like `features.Count`, `layerId.Length`
3. **Quick Watch** (`Shift+F9`): Evaluate expression in current context

### Debug Console

Execute C# expressions while debugging:

```csharp
// Evaluate expressions
> features.Count
10
> layerId.ToUpper()
"CITIES"
> _dataProvider.GetType().Name
"PostgresDataProvider"
```

### Debugging External Code

By default, VS Code only debugs your code. To debug into .NET libraries:

1. Open `.vscode/launch.json`
2. Add to your configuration:
```json
"justMyCode": false,
"suppressJITOptimizations": true
```

### Tips for VS Code

- **Restart Debugging**: `Ctrl+Shift+F5`
- **Stop Debugging**: `Shift+F5`
- **Step Over**: `F10`
- **Step Into**: `F11`
- **Step Out**: `Shift+F11`
- **Continue**: `F5`

## Visual Studio 2022

### Prerequisites

- Visual Studio 2022 version 17.9 or later
- ASP.NET and web development workload installed

### Launch Configurations

Visual Studio uses `launchSettings.json` in the Host project:

**Built-in profiles:**
1. **Honua.Server.Host** - Default profile (IIS Express)
2. **https** - Kestrel with HTTPS
3. **http** - Kestrel with HTTP only
4. **Docker** - Run in Docker container

**Select profile:**
- Use the dropdown next to the Start button
- Press `F5` to start debugging

### Setting Breakpoints

#### Basic Breakpoints
- Click in the left margin or press `F9`
- Red dot indicates active breakpoint

#### Advanced Breakpoints
Right-click breakpoint → **Conditions...**

**Types:**
1. **Conditional Expression**: `layerId == "cities"`
2. **Hit Count**: Break on 10th hit
3. **Filter**: Break only in specific threads/processes
4. **Actions**: Log messages, run macros

**Example - Conditional Breakpoint:**
```csharp
public async Task ProcessFeatures(string layerId)
{
    // Breakpoint condition: layerId.StartsWith("layer_") && _cache == null
    var features = await GetFeaturesAsync(layerId);
}
```

**Example - Tracepoint (Breakpoint with Action):**
```csharp
// Action: "Processing {layerId} - {DateTime.Now}"
// Check "Continue execution"
ProcessLayer(layerId);
```

### Debugging Tests

**Test Explorer:**
1. Open Test Explorer (`Ctrl+E, T`)
2. Run All Tests or specific test
3. Debug Test: Right-click → Debug

**Debugging a specific test:**
```csharp
[TestMethod]
public async Task GetFeatures_WithFilter_ReturnsFiltered()
{
    // Set breakpoint here
    var result = await _service.GetFeaturesAsync("cities", "population > 10000");
    Assert.IsNotNull(result);
}
```

### Diagnostic Tools

**Performance Profiler** (Alt+F2):
1. CPU Usage
2. Memory Usage
3. Database queries (.NET Object Allocation)
4. .NET Async

**Usage:**
1. Debug → Performance Profiler
2. Select tools to run
3. Click Start
4. Interact with application
5. Stop collection
6. Analyze results

### Data Tips

Hover over variables to see values inline:
- Simple types: Show value directly
- Complex objects: Expand properties
- Collections: See count and items
- Pin data tip: Click pin icon to keep visible

### Immediate Window

Execute C# code during debugging (`Ctrl+Alt+I`):

```csharp
// Evaluate expressions
? features.Count
10
// Call methods
? _dataProvider.GetConnectionString()
"Host=localhost;Database=honua;..."
// Modify variables
layerId = "test_layer"
```

### Call Stack

View call stack (`Ctrl+Alt+C`):
- Shows method call chain leading to current point
- Double-click frame to navigate to that code
- Right-click → Show External Code to see .NET internals

### Exception Settings

Configure exception behavior (`Ctrl+Alt+E`):

**Break when exceptions are:**
- **Thrown**: Break immediately when exception occurs
- **Unhandled**: Break only if not caught

**Common settings:**
- Enable all for `System.NullReferenceException`
- Disable for expected exceptions like `TaskCanceledException`

### Hot Reload

Edit code while debugging without restarting:

1. Make code changes while debugging
2. Click "Hot Reload" button or press `Alt+F10`
3. Changes apply immediately (supported changes only)

**Supported:**
- Method body changes
- Adding methods/properties
- Modifying lambda expressions

**Not supported:**
- Adding/removing types
- Changing method signatures
- Modifying LINQ queries significantly

### Edit and Continue

Similar to Hot Reload but for older code changes:

1. Pause debugging
2. Edit code
3. Continue debugging
4. Changes apply when possible

### Debugging Docker Containers

**Attach to container:**
1. Debug → Attach to Process
2. Connection type: Docker (Linux Container)
3. Find `Honua.Server.Host` process
4. Click Attach

**Or use Docker profile:**
1. Select "Docker" from profile dropdown
2. Press F5
3. Visual Studio builds container and attaches debugger

### Performance Tips for Visual Studio

- Disable "Show Threads in Source" if slow
- Use "Just My Code" to skip .NET framework code
- Disable IntelliTrace for faster debugging
- Use Release build for better performance profiling

## JetBrains Rider

### Prerequisites

- JetBrains Rider 2024.1 or later
- .NET 9 SDK plugin

### Launch Configurations

Rider auto-detects launch configurations from `launchSettings.json`.

**Pre-configured:**
1. **Honua.Server.Host** - Main application
2. **All Tests** - Run all tests
3. **Current Test** - Run test at cursor

**Create custom configuration:**
1. Run → Edit Configurations
2. Add new .NET Project configuration
3. Select project and profile

### Setting Breakpoints

#### Line Breakpoints
Click in gutter or press `Ctrl+F8`

#### Advanced Options
Right-click breakpoint:

1. **Condition**: `layerId.StartsWith("test")`
2. **Log evaluated expression**: Log without stopping
3. **Disable until breakpoint is hit**: Chain breakpoints
4. **Pass count**: Skip first N hits

**Example:**
```csharp
// Condition: features.Count > 100 && layerId != null
// Log: "Processing {layerId} with {features.Count} features"
var result = ProcessFeatures(layerId, features);
```

### Debugging Tests

**Run/Debug test:**
- Click gutter icon next to test method
- Right-click test → Debug
- Use keyboard shortcut on test method

**Test Sessions:**
- View all test runs in Test Explorer
- Re-run failed tests
- Compare test results across runs

```csharp
[TestMethod] // Click gutter icon to run/debug
public async Task GetFeatures_ReturnsData()
{
    var result = await _service.GetFeaturesAsync("cities");
    Assert.IsNotNull(result);
}
```

### Evaluate Expression

**During debugging:**
- **Alt+F8**: Evaluate expression dialog
- **Alt+Click** on variable: Quick evaluate
- Type in Watches window

**Advanced evaluation:**
```csharp
// Evaluate complex expressions
features.Where(f => f.Properties["population"] > 10000).Count()
// Call methods
_dataProvider.GetTableName(layerId)
// Modify state
_cache.Clear()
```

### Step Filters

Configure what to step through:

1. Settings → Build, Execution, Deployment → Debugger → Step Filters
2. Skip framework code
3. Skip properties
4. Skip constructors

**Selective stepping:**
- **F8**: Step Over (skip into method)
- **F7**: Step Into (enter method)
- **Shift+F8**: Step Out (exit method)
- **Alt+F9**: Run to Cursor

### Memory View

View object allocation and memory usage:

1. Run → Attach to Process → Memory View
2. See live memory statistics
3. Take memory snapshots
4. Compare snapshots to find leaks

**Finding memory leaks:**
1. Take snapshot at baseline
2. Perform operation
3. Force GC (`GC.Collect()` in Evaluate)
4. Take second snapshot
5. Compare - objects that persist may be leaks

### Performance Profiling

**Timeline Profiler:**
1. Run → Profile
2. Select Timeline Profiling
3. Start profiling
4. Interact with application
5. Stop and analyze

**Shows:**
- CPU usage over time
- Method call duration
- Async operations
- Database queries

**DotTrace Integration:**
- Sampling mode for production-like performance
- Tracing mode for detailed analysis
- Timeline for concurrent operations

### Database Debugging

**Built-in Database Tools:**
1. Database tool window
2. Connect to PostgreSQL
3. Run queries directly
4. View data while debugging

**Query debugging:**
- Set breakpoints in SQL
- See parameter values
- View query plans

### Rider-Specific Features

**Value Renderers:**
- Custom visualization for complex types
- GeoJSON preview for geometries
- Image preview for byte arrays

**Decompiler:**
- View source of .NET framework methods
- Step into library code
- Understand third-party libraries

**Thread Debugging:**
- Threads window shows all threads
- Switch between threads
- Freeze/Thaw threads
- Detect deadlocks

## Common Debugging Scenarios

### Debugging Async Code

**Await breakpoints:**
```csharp
public async Task<FeatureCollection> GetFeaturesAsync(string layerId)
{
    // Set breakpoint here - before await
    var features = await _dataProvider.GetFeaturesAsync(layerId);
    // Set breakpoint here - after await resumes
    return ProcessFeatures(features);
}
```

**Tips:**
- Use Call Stack to see async chain
- Watch for `Task` status (Running, RanToCompletion, Faulted)
- Check `Result` property to see exception

**Common issues:**
- Deadlocks from `.Result` or `.Wait()`
- Exceptions wrapped in `AggregateException`
- Lost context from missing `ConfigureAwait(false)`

### Debugging Entity Framework / Database Queries

**Log SQL queries:**

Add to `appsettings.Development.json`:
```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

**Or use logging callback:**
```csharp
protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
{
    optionsBuilder.LogTo(Console.WriteLine, LogLevel.Information);
}
```

**Debug queries:**
```csharp
var query = context.Features
    .Where(f => f.LayerId == layerId)
    .Select(f => new { f.Id, f.Geometry });

// Set breakpoint here
var sql = query.ToQueryString();  // See generated SQL
var results = await query.ToListAsync();  // Execute and debug
```

### Debugging API Endpoints

**Test endpoint with breakpoint:**

1. Set breakpoint in endpoint handler:
```csharp
app.MapGet("/api/features/{layerId}", async (string layerId, IFeatureService service) =>
{
    // Breakpoint here
    var features = await service.GetFeaturesAsync(layerId);
    return Results.Ok(features);
});
```

2. Make request:
   - Use Swagger UI at `http://localhost:8080/swagger`
   - Use `curl`: `curl http://localhost:8080/api/features/cities`
   - Use Postman/Insomnia

3. Debugger breaks at endpoint entry

**Inspect HTTP context:**
```csharp
// In debugger, evaluate:
context.Request.Headers
context.Request.Query
context.Response.StatusCode
```

### Debugging Geometry Operations

**Visualize geometries:**
```csharp
var geometry = new Point(10, 20);
var wkt = geometry.ToText();  // View as WKT: "POINT (10 20)"
var geojson = GeoJsonWriter.Write(geometry);  // View as GeoJSON
```

**Debug spatial operations:**
```csharp
var poly1 = (Polygon)reader.Read("POLYGON((0 0, 10 0, 10 10, 0 10, 0 0))");
var poly2 = (Polygon)reader.Read("POLYGON((5 5, 15 5, 15 15, 5 15, 5 5))");

// Set breakpoint and evaluate:
var intersects = poly1.Intersects(poly2);  // true
var intersection = poly1.Intersection(poly2);  // geometry of overlap
var wkt = intersection.ToText();  // visualize result
```

**Common geometry issues:**
- Invalid geometries (use `IsValid`)
- Wrong SRID (use `SRID` property)
- Coordinate precision issues

### Debugging Cache Issues

**Inspect cache state:**
```csharp
// In Redis-based cache
var keys = _redis.GetDatabase().Execute("KEYS", "*");

// In memory cache
var stats = _memoryCache.GetCurrentStatistics();
```

**Cache miss debugging:**
```csharp
public async Task<T> GetOrCreateAsync<T>(string key, Func<Task<T>> factory)
{
    // Breakpoint here
    if (_cache.TryGetValue(key, out T value))
    {
        return value;  // Cache hit
    }

    // Cache miss - why?
    value = await factory();
    _cache.Set(key, value);
    return value;
}
```

### Debugging Authentication Issues

**Inspect claims:**
```csharp
app.MapGet("/api/secure", (ClaimsPrincipal user) =>
{
    // Breakpoint here
    var isAuthenticated = user.Identity.IsAuthenticated;
    var claims = user.Claims.ToList();
    var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
});
```

**JWT debugging:**
```csharp
var token = Request.Headers["Authorization"].ToString().Replace("Bearer ", "");
var handler = new JwtSecurityTokenHandler();
var jsonToken = handler.ReadToken(token) as JwtSecurityToken;

// Inspect in debugger:
// - jsonToken.Claims
// - jsonToken.ValidTo (expiration)
// - jsonToken.Issuer
```

### Debugging Configuration Issues

**Inspect configuration:**
```csharp
public MyService(IConfiguration configuration)
{
    // Breakpoint here
    var connectionString = configuration.GetConnectionString("DefaultConnection");
    var allSettings = configuration.AsEnumerable().ToList();

    // Check if setting exists
    var exists = configuration.GetSection("Honua:Cache:Provider").Exists();
}
```

**Debug binding:**
```csharp
public MyService(IOptions<CacheOptions> options)
{
    // Breakpoint here
    var cacheOptions = options.Value;
    // Inspect properties
}
```

## Troubleshooting

### Breakpoints Not Hitting

**Causes and solutions:**

1. **Code not deployed**
   - Solution: Rebuild (`Ctrl+Shift+B`) and restart

2. **Optimized release build**
   - Solution: Use Debug configuration

3. **Async code optimization**
   - Solution: Add `[MethodImpl(MethodImplOptions.NoOptimization)]`

4. **Symbols not loaded**
   - Solution: Check Output → Debug to see symbol loading
   - Enable "Load All Symbols" in Modules window

5. **Just My Code enabled**
   - Solution: Disable in debug settings

### Cannot See Variable Values

**"Cannot evaluate expression":**
- Variable optimized away in Release build
- Use Debug build
- Add `[MethodImpl(MethodImplOptions.NoOptimization)]` to method

**"Value is not available in current context":**
- Variable out of scope
- Execution point before variable declaration
- Compiler removed unused variable

### Slow Debugging

**Solutions:**
1. Disable IntelliTrace (Visual Studio)
2. Disable diagnostic tools during debugging
3. Enable "Just My Code"
4. Reduce number of watched expressions
5. Disable automatic property evaluation

### Debugging Won't Start

**Common issues:**

1. **Port already in use:**
```bash
# Find process using port 8080
netstat -ano | findstr :8080  # Windows
lsof -i :8080  # Linux/macOS

# Kill process
taskkill /PID <pid> /F  # Windows
kill -9 <pid>  # Linux/macOS
```

2. **Docker containers not running:**
```bash
docker compose up -d postgres redis
```

3. **Build failed:**
- Check Error List / Problems panel
- Run `dotnet build` in terminal to see detailed errors

### Exception Breaks in Framework Code

**Disable specific exceptions:**
- VS Code: Debug → Breakpoints → Uncheck exception type
- Visual Studio: Exception Settings (`Ctrl+Alt+E`)
- Rider: Settings → Debugger → Disable exception breakpoint

**Expected exceptions to ignore:**
- `TaskCanceledException` (request cancellation)
- `OperationCanceledException` (normal cancellation)
- `IOException` (network disconnects)

## Performance Profiling

### CPU Profiling

**Visual Studio:**
1. Debug → Performance Profiler
2. Select "CPU Usage"
3. Start profiling
4. Perform operations
5. Stop profiling
6. Analyze hot paths

**JetBrains dotTrace:**
1. Run → Profile
2. Timeline or Sampling mode
3. Focus on long-running methods
4. Check for N+1 query problems

**Optimization targets:**
- Methods with high "Self Time"
- Methods called many times
- LINQ queries with multiple enumerations

### Memory Profiling

**Find memory leaks:**

1. **Take baseline snapshot**
2. **Perform operation that should be GC'd**
3. **Force garbage collection:**
   ```csharp
   GC.Collect();
   GC.WaitForPendingFinalizers();
   GC.Collect();
   ```
4. **Take second snapshot**
5. **Compare snapshots**

**Look for:**
- Collections that grow unbounded
- Event handlers not unsubscribed
- Static references holding objects
- Cached data not expiring

### Database Query Profiling

**Log slow queries:**

```json
{
  "Logging": {
    "LogLevel": {
      "Microsoft.EntityFrameworkCore.Database.Command": "Information"
    }
  }
}
```

**Use query analyzer:**
```csharp
var query = context.Features.Where(f => f.LayerId == layerId);
var plan = query.ToQueryString();  // View SQL
```

**Use database profiling:**
- **PostgreSQL**: `EXPLAIN ANALYZE`
- **SQL Server**: SQL Server Profiler
- **MySQL**: `EXPLAIN`

**Check for:**
- N+1 query problems (use `.Include()`)
- Missing indexes (check query plans)
- Full table scans
- Unoptimized WHERE clauses

### Async Performance

**Use Timeline profiler** (Rider/dotTrace):
- See concurrent async operations
- Identify bottlenecks
- Find synchronous blocks in async code

**Check for:**
- Synchronous database calls (should be async)
- `Task.Wait()` or `.Result` (causes thread pool starvation)
- Missing `ConfigureAwait(false)` in libraries
- Excessive task creation

### Benchmarking

**BenchmarkDotNet:**

```csharp
[MemoryDiagnoser]
public class FeatureBenchmarks
{
    [Benchmark]
    public async Task GetFeatures_Baseline()
    {
        var features = await _service.GetFeaturesAsync("cities");
    }

    [Benchmark]
    public async Task GetFeatures_Cached()
    {
        var features = await _cachedService.GetFeaturesAsync("cities");
    }
}
```

Run benchmarks:
```bash
dotnet run -c Release --project tests/Honua.Server.Benchmarks
```

---

## Additional Resources

- [Visual Studio Debugging Documentation](https://docs.microsoft.com/en-us/visualstudio/debugger/)
- [VS Code Debugging Guide](https://code.visualstudio.com/docs/editor/debugging)
- [Rider Debugging Features](https://www.jetbrains.com/help/rider/Debugging_Code.html)
- [.NET Debugging Tips](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/)
- [Performance Profiling](https://docs.microsoft.com/en-us/dotnet/core/diagnostics/profiling)

---

**Happy Debugging!** 🐛🔍
