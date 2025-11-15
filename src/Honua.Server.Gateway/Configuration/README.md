# InMemoryConfigProvider for YARP

A complete implementation of dynamic configuration updates for YARP reverse proxy, enabling blue-green deployments, canary releases, and runtime traffic management.

## Files Created

1. **InMemoryConfigProvider.cs** - Core provider implementation
2. **ProxyConfigProviderExtensions.cs** - Extension methods for easy registration
3. **YarpConfigurationExtensions.cs** - Legacy helper (updated to use new provider)

## Features

- **Thread-Safe**: Uses locks and volatile fields for safe concurrent access
- **Dynamic Updates**: Runtime configuration changes via `Update()` method
- **Change Notifications**: Uses `IChangeToken` to notify YARP of configuration changes
- **Full Configuration Support**: Parses routes, clusters, health checks, transforms, etc.
- **Comprehensive Logging**: Detailed logging for debugging and monitoring
- **Easy Integration**: Simple extension method for registration

## Usage

### 1. Basic Setup in Program.cs

Replace the existing `.LoadFromConfig()` with `.LoadFromMemory()`:

```csharp
// Before (static configuration):
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// After (dynamic configuration):
builder.Services.AddReverseProxy()
    .LoadFromMemory(builder.Configuration);
```

### 2. Accessing the Provider for Dynamic Updates

Inject `InMemoryConfigProvider` into your services:

```csharp
public class TrafficManagementService
{
    private readonly InMemoryConfigProvider _configProvider;
    private readonly ILogger<TrafficManagementService> _logger;

    public TrafficManagementService(
        InMemoryConfigProvider configProvider,
        ILogger<TrafficManagementService> logger)
    {
        _configProvider = configProvider;
        _logger = logger;
    }

    public async Task SwitchToBlueGreenAsync(
        string serviceName,
        string blueUrl,
        string greenUrl,
        int greenPercentage)
    {
        // Get current configuration
        var currentConfig = _configProvider.GetConfig();
        var routes = currentConfig.Routes.ToList();
        var clusters = currentConfig.Clusters.ToList();

        // Create weighted destinations
        var destinations = new Dictionary<string, DestinationConfig>();

        if (greenPercentage < 100)
        {
            destinations["blue"] = new DestinationConfig
            {
                Address = blueUrl,
                Health = blueUrl + "/health",
                Metadata = new Dictionary<string, string>
                {
                    ["weight"] = (100 - greenPercentage).ToString()
                }
            };
        }

        if (greenPercentage > 0)
        {
            destinations["green"] = new DestinationConfig
            {
                Address = greenUrl,
                Health = greenUrl + "/health",
                Metadata = new Dictionary<string, string>
                {
                    ["weight"] = greenPercentage.ToString()
                }
            };
        }

        // Update or add cluster
        var clusterIndex = clusters.FindIndex(c => c.ClusterId == serviceName);
        var newCluster = new ClusterConfig
        {
            ClusterId = serviceName,
            Destinations = destinations,
            LoadBalancingPolicy = "WeightedRoundRobin",
            HealthCheck = new HealthCheckConfig
            {
                Active = new ActiveHealthCheckConfig
                {
                    Enabled = true,
                    Interval = TimeSpan.FromSeconds(10),
                    Timeout = TimeSpan.FromSeconds(5),
                    Policy = "ConsecutiveFailures",
                    Path = "/health"
                }
            }
        };

        if (clusterIndex >= 0)
        {
            clusters[clusterIndex] = newCluster;
        }
        else
        {
            clusters.Add(newCluster);
        }

        // Apply the update
        _configProvider.Update(routes, clusters);

        _logger.LogInformation(
            "Traffic split updated: {Service} - Blue={Blue}%, Green={Green}%",
            serviceName, 100 - greenPercentage, greenPercentage);
    }
}
```

### 3. Register Your Traffic Management Service

```csharp
// In Program.cs or Startup.cs
builder.Services.AddSingleton<TrafficManagementService>();
```

### 4. Use with BlueGreenTrafficManager

The existing `BlueGreenTrafficManager` from `Honua.Server.Core` is already compatible:

```csharp
// In Program.cs
builder.Services.AddSingleton<BlueGreenTrafficManager>();

// In your controller or service
public class DeploymentController : ControllerBase
{
    private readonly BlueGreenTrafficManager _trafficManager;

    public DeploymentController(BlueGreenTrafficManager trafficManager)
    {
        _trafficManager = trafficManager;
    }

    [HttpPost("deploy/canary")]
    public async Task<IActionResult> DeployCanary(
        [FromBody] CanaryDeploymentRequest request)
    {
        var strategy = new CanaryStrategy
        {
            TrafficSteps = new List<int> { 10, 25, 50, 100 },
            SoakDurationSeconds = 60,
            AutoRollback = true
        };

        var result = await _trafficManager.PerformCanaryDeploymentAsync(
            request.ServiceName,
            request.BlueEndpoint,
            request.GreenEndpoint,
            strategy,
            async (ct) => await CheckHealthAsync(request.GreenEndpoint, ct),
            HttpContext.RequestAborted);

        return Ok(result);
    }

    private async Task<bool> CheckHealthAsync(string endpoint, CancellationToken ct)
    {
        // Implement your health check logic
        using var client = new HttpClient();
        var response = await client.GetAsync($"{endpoint}/health", ct);
        return response.IsSuccessStatusCode;
    }
}
```

## Configuration Format

The provider reads configuration from `appsettings.json` under the `ReverseProxy` section:

```json
{
  "ReverseProxy": {
    "Routes": {
      "api-route": {
        "ClusterId": "api-cluster",
        "Match": {
          "Hosts": ["api.example.com"],
          "Path": "/api/{**catch-all}"
        },
        "Transforms": [
          {
            "RequestHeader": "X-Forwarded-For",
            "Set": "{RemoteIpAddress}"
          }
        ],
        "Order": 1,
        "Metadata": {
          "Description": "API endpoints"
        }
      }
    },
    "Clusters": {
      "api-cluster": {
        "Destinations": {
          "destination1": {
            "Address": "http://localhost:5000",
            "Health": "http://localhost:5000/health"
          }
        },
        "LoadBalancingPolicy": "RoundRobin",
        "HealthCheck": {
          "Active": {
            "Enabled": true,
            "Interval": "00:00:30",
            "Timeout": "00:00:10",
            "Policy": "ConsecutiveFailures",
            "Path": "/health/ready"
          },
          "Passive": {
            "Enabled": true,
            "Policy": "TransportFailureRate",
            "ReactivationPeriod": "00:01:00"
          }
        },
        "HttpRequest": {
          "ActivityTimeout": "00:10:00",
          "Version": "2",
          "VersionPolicy": "RequestVersionOrLower"
        }
      }
    }
  }
}
```

## Complete Example: Update Program.cs

Here's a complete example of how to update your `Program.cs`:

```csharp
using Honua.Server.Gateway.Configuration;
using Honua.Server.Core.BlueGreen;

var builder = WebApplication.CreateBuilder(args);

// ... other services ...

// Configure YARP with InMemoryConfigProvider
builder.Services.AddReverseProxy()
    .LoadFromMemory(builder.Configuration)
    .AddTransforms(builderContext =>
    {
        // Your transforms...
        builderContext.AddXForwarded();
    });

// Register traffic management services
builder.Services.AddSingleton<BlueGreenTrafficManager>();

var app = builder.Build();

// ... middleware pipeline ...

app.MapReverseProxy();

await app.RunAsync();
```

## Dynamic Updates via API

Create an API endpoint for dynamic configuration updates:

```csharp
[ApiController]
[Route("api/[controller]")]
public class GatewayConfigController : ControllerBase
{
    private readonly InMemoryConfigProvider _configProvider;
    private readonly ILogger<GatewayConfigController> _logger;

    public GatewayConfigController(
        InMemoryConfigProvider configProvider,
        ILogger<GatewayConfigController> logger)
    {
        _configProvider = configProvider;
        _logger = logger;
    }

    [HttpPost("routes")]
    public IActionResult AddRoute([FromBody] RouteConfig route)
    {
        var config = _configProvider.GetConfig();
        var routes = config.Routes.ToList();

        // Remove existing route with same ID
        routes.RemoveAll(r => r.RouteId == route.RouteId);
        routes.Add(route);

        _configProvider.Update(routes, config.Clusters.ToList());

        _logger.LogInformation("Route {RouteId} added/updated", route.RouteId);
        return Ok();
    }

    [HttpPost("clusters")]
    public IActionResult AddCluster([FromBody] ClusterConfig cluster)
    {
        var config = _configProvider.GetConfig();
        var clusters = config.Clusters.ToList();

        // Remove existing cluster with same ID
        clusters.RemoveAll(c => c.ClusterId == cluster.ClusterId);
        clusters.Add(cluster);

        _configProvider.Update(config.Routes.ToList(), clusters);

        _logger.LogInformation("Cluster {ClusterId} added/updated", cluster.ClusterId);
        return Ok();
    }
}
```

## How It Works

1. **Initialization**: The provider loads initial configuration from `appsettings.json` via `LoadFromMemory()`
2. **Change Detection**: Each configuration has a `CancellationTokenSource` that signals when it's superseded
3. **Update Flow**:
   - Call `Update(routes, clusters)` with new configuration
   - Provider creates new `InMemoryConfig` instance
   - Old config's change token is signaled
   - YARP detects the change and reloads configuration
   - Traffic immediately uses new routes/clusters
4. **Thread Safety**: Lock ensures only one update at a time, volatile field ensures visibility

## Benefits

- **Zero Downtime Deployments**: Switch traffic without restarting the gateway
- **A/B Testing**: Route percentage of traffic to different versions
- **Blue-Green Deployments**: Instant or gradual traffic switching
- **Canary Releases**: Progressive rollout with automatic rollback
- **Dynamic Scaling**: Add/remove backend destinations on the fly
- **Emergency Rollback**: Quickly revert to previous configuration

## Logging

The provider logs all configuration changes at `Information` level:

```
[INFO] InMemoryConfigProvider initialized with 5 routes and 3 clusters
[INFO] Configuration updated: 5 routes, 4 clusters (previous: 5 routes, 3 clusters)
[DEBUG] Configuration change signaled
```

## Thread Safety

The implementation is fully thread-safe:

- Uses `lock` statement for exclusive access during updates
- `volatile` keyword ensures visibility of config changes across threads
- Immutable configuration objects prevent modification after creation
- `CancellationTokenSource` thread-safe signaling

## Performance

- **Read Performance**: O(1) - simple field access, no locks
- **Update Performance**: O(n) where n is number of routes/clusters
- **Memory**: One `InMemoryConfig` instance per update (old instances are GC'd)
- **Change Notification**: Near-instant via `CancellationToken`

## Compatibility

- YARP 2.3.0+
- .NET 9.0+
- ASP.NET Core 9.0+
- Compatible with existing `BlueGreenTrafficManager` from `Honua.Server.Core`
