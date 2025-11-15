# Traffic Management Endpoints

## Overview

The Traffic Management Endpoints provide a comprehensive API for controlling blue-green deployment traffic distribution in the Honua Gateway. These endpoints allow administrators to:

- Gradually shift traffic between blue and green deployments
- Perform automated canary deployments with health checks
- Execute instant cutovers to new deployments
- Rollback to previous deployments
- Monitor current traffic distribution

## File Location

`/home/mike/projects/Honua.Server/src/Honua.Server.Gateway/Endpoints/TrafficManagementEndpoints.cs`

## Registration

To enable these endpoints in your Gateway application, add the following to your `Program.cs`:

```csharp
using Honua.Server.Gateway.Endpoints;
using Honua.Server.Core.BlueGreen;

// Register BlueGreenTrafficManager
builder.Services.AddSingleton<BlueGreenTrafficManager>();

// Add HttpClientFactory for health checks
builder.Services.AddHttpClient();

// Add authentication if not already configured
builder.Services.AddAuthorization();

// After app.Build()
var app = builder.Build();

// Map the traffic management endpoints
app.MapTrafficManagementEndpoints();
```

## API Endpoints

### 1. POST /admin/traffic/switch

**Purpose**: Gradually switch traffic between blue and green deployments.

**Request Body**:
```json
{
  "serviceName": "my-service",
  "blueEndpoint": "http://blue-deployment:8080",
  "greenEndpoint": "http://green-deployment:8080",
  "greenPercentage": 25
}
```

**Response**:
```json
{
  "success": true,
  "serviceName": "my-service",
  "blueEndpoint": "http://blue-deployment:8080",
  "greenEndpoint": "http://green-deployment:8080",
  "blueTrafficPercentage": 75,
  "greenTrafficPercentage": 25,
  "message": "Traffic switched: 75% blue, 25% green",
  "timestamp": "2025-11-14T12:00:00Z",
  "performedBy": "admin@honua.io"
}
```

**Validation**:
- `serviceName`: Required, non-empty string
- `blueEndpoint`: Required, valid absolute URL
- `greenEndpoint`: Required, valid absolute URL
- `greenPercentage`: Required, integer between 0-100

**Example cURL**:
```bash
curl -X POST http://localhost:5000/admin/traffic/switch \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "serviceName": "api-service",
    "blueEndpoint": "http://api-blue:8080",
    "greenEndpoint": "http://api-green:8080",
    "greenPercentage": 25
  }'
```

---

### 2. POST /admin/traffic/canary

**Purpose**: Perform automated canary deployment with gradual traffic migration and health checks.

**Request Body**:
```json
{
  "serviceName": "my-service",
  "blueEndpoint": "http://blue-deployment:8080",
  "greenEndpoint": "http://green-deployment:8080",
  "strategy": {
    "trafficSteps": [10, 25, 50, 100],
    "soakDurationSeconds": 60,
    "autoRollback": true
  }
}
```

**Response**:
```json
{
  "success": true,
  "rolledBack": false,
  "serviceName": "my-service",
  "blueEndpoint": "http://blue-deployment:8080",
  "greenEndpoint": "http://green-deployment:8080",
  "stages": [
    {
      "greenTrafficPercentage": 10,
      "isHealthy": true,
      "timestamp": "2025-11-14T12:00:00Z"
    },
    {
      "greenTrafficPercentage": 25,
      "isHealthy": true,
      "timestamp": "2025-11-14T12:01:00Z"
    },
    {
      "greenTrafficPercentage": 50,
      "isHealthy": true,
      "timestamp": "2025-11-14T12:02:00Z"
    },
    {
      "greenTrafficPercentage": 100,
      "isHealthy": true,
      "timestamp": "2025-11-14T12:03:00Z"
    }
  ],
  "message": "Canary deployment completed successfully, 100% traffic on green",
  "completedAt": "2025-11-14T12:03:00Z",
  "performedBy": "admin@honua.io"
}
```

**Strategy Options**:
- `trafficSteps`: Array of percentages for gradual traffic migration (default: [10, 25, 50, 100])
- `soakDurationSeconds`: Time to wait at each step before proceeding (default: 60)
- `autoRollback`: Automatically rollback on health check failure (default: true)

**Health Check**:
- Automatically checks `{greenEndpoint}/health` at each stage
- If health check fails, automatically rolls back to 100% blue (if autoRollback is true)

**Example cURL**:
```bash
curl -X POST http://localhost:5000/admin/traffic/canary \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "serviceName": "api-service",
    "blueEndpoint": "http://api-blue:8080",
    "greenEndpoint": "http://api-green:8080",
    "strategy": {
      "trafficSteps": [5, 10, 25, 50, 100],
      "soakDurationSeconds": 120,
      "autoRollback": true
    }
  }'
```

---

### 3. POST /admin/traffic/cutover

**Purpose**: Instantly switch 100% of traffic to green deployment.

**Request Body**:
```json
{
  "serviceName": "my-service",
  "blueEndpoint": "http://blue-deployment:8080",
  "greenEndpoint": "http://green-deployment:8080"
}
```

**Response**:
```json
{
  "success": true,
  "serviceName": "my-service",
  "blueEndpoint": "http://blue-deployment:8080",
  "greenEndpoint": "http://green-deployment:8080",
  "blueTrafficPercentage": 0,
  "greenTrafficPercentage": 100,
  "message": "Traffic switched: 0% blue, 100% green",
  "timestamp": "2025-11-14T12:00:00Z",
  "performedBy": "admin@honua.io"
}
```

**Example cURL**:
```bash
curl -X POST http://localhost:5000/admin/traffic/cutover \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "serviceName": "api-service",
    "blueEndpoint": "http://api-blue:8080",
    "greenEndpoint": "http://api-green:8080"
  }'
```

---

### 4. POST /admin/traffic/rollback

**Purpose**: Instantly rollback 100% of traffic to blue deployment.

**Request Body**:
```json
{
  "serviceName": "my-service",
  "blueEndpoint": "http://blue-deployment:8080",
  "greenEndpoint": "http://green-deployment:8080"
}
```

**Response**:
```json
{
  "success": true,
  "serviceName": "my-service",
  "blueEndpoint": "http://blue-deployment:8080",
  "greenEndpoint": "http://green-deployment:8080",
  "blueTrafficPercentage": 100,
  "greenTrafficPercentage": 0,
  "message": "Traffic switched: 100% blue, 0% green",
  "timestamp": "2025-11-14T12:00:00Z",
  "performedBy": "admin@honua.io"
}
```

**Example cURL**:
```bash
curl -X POST http://localhost:5000/admin/traffic/rollback \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer YOUR_TOKEN" \
  -d '{
    "serviceName": "api-service",
    "blueEndpoint": "http://api-blue:8080",
    "greenEndpoint": "http://api-green:8080"
  }'
```

---

### 5. GET /admin/traffic/status

**Purpose**: Get current proxy configuration and traffic distribution.

**Response**:
```json
{
  "clusters": [
    {
      "clusterId": "my-service",
      "loadBalancingPolicy": "WeightedRoundRobin",
      "destinations": [
        {
          "name": "blue",
          "address": "http://blue-deployment:8080",
          "weight": 75,
          "healthCheckPath": "/health"
        },
        {
          "name": "green",
          "address": "http://green-deployment:8080",
          "weight": 25,
          "healthCheckPath": "/health"
        }
      ],
      "healthCheckEnabled": true
    }
  ],
  "routes": [
    {
      "routeId": "my-service-route",
      "clusterId": "my-service",
      "matchPath": "/{**catch-all}"
    }
  ],
  "timestamp": "2025-11-14T12:00:00Z"
}
```

**Example cURL**:
```bash
curl -X GET http://localhost:5000/admin/traffic/status \
  -H "Authorization: Bearer YOUR_TOKEN"
```

---

## Security

### Authentication & Authorization

All endpoints require authentication via the `RequireAuthorization()` attribute. To configure:

```csharp
// In Program.cs - Configure authentication
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        // Configure JWT validation
        options.Authority = "https://your-auth-server";
        options.Audience = "gateway-api";
    });

builder.Services.AddAuthorization(options =>
{
    // Optional: Add role-based access control
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireRole("admin", "gateway-admin"));
});
```

To use role-based access, change the endpoint registration:

```csharp
var group = endpoints.MapGroup("/admin/traffic")
    .WithTags("Traffic Management")
    .RequireAuthorization("AdminPolicy")  // Use specific policy
    .RequireRateLimiting("per-ip");
```

### Rate Limiting

All endpoints are protected by the "per-ip" rate limiting policy configured in the Gateway. The default configuration (from Program.cs) is:

- Per-IP: 100 requests per 60 seconds
- Global: 1000 requests per 60 seconds

### Audit Logging

All traffic management operations are logged with:
- User identity (username and user ID)
- Service name
- Operation type (switch, canary, cutover, rollback)
- Timestamps
- Success/failure status

Logs include structured logging fields for easy querying:

```log
[2025-11-14 12:00:00] INFO User admin@honua.io (user-123) initiating traffic switch for api-service: 25% to green
[2025-11-14 12:00:01] INFO Traffic switch completed successfully for api-service: Blue=75%, Green=25%
```

---

## Error Handling

All endpoints return RFC 7807 Problem Details for errors:

### Validation Error (400 Bad Request)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "GreenPercentage must be between 0 and 100": ["GreenPercentage must be between 0 and 100"],
    "BlueEndpoint must be a valid absolute URL": ["BlueEndpoint must be a valid absolute URL"]
  }
}
```

### Unauthorized (401)
```json
{
  "type": "https://tools.ietf.org/html/rfc7235#section-3.1",
  "title": "Unauthorized",
  "status": 401
}
```

### Internal Server Error (500)
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.6.1",
  "title": "Traffic switch failed",
  "status": 500,
  "detail": "Failed to update proxy configuration: Connection timeout"
}
```

---

## Integration Example

### Complete Program.cs Setup

```csharp
using Honua.Server.Gateway.Endpoints;
using Honua.Server.Core.BlueGreen;
using Microsoft.AspNetCore.RateLimiting;
using Serilog;
using Yarp.ReverseProxy.Configuration;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, config) =>
    config.ReadFrom.Configuration(context.Configuration));

// Add BlueGreenTrafficManager
builder.Services.AddSingleton<BlueGreenTrafficManager>();

// Add HttpClientFactory
builder.Services.AddHttpClient();

// Add authentication
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "https://auth.honua.io";
        options.Audience = "gateway-api";
    });

// Add authorization with role-based policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminPolicy", policy =>
        policy.RequireRole("admin"));
});

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("per-ip", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromSeconds(60)
            }));
});

// Add YARP reverse proxy
builder.Services.AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

// Add OpenAPI/Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure middleware pipeline
app.UseSwagger();
app.UseSwaggerUI();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

// Map endpoints
app.MapTrafficManagementEndpoints();
app.MapReverseProxy();

await app.RunAsync();
```

---

## Use Cases

### 1. Gradual Rollout

Start with a small percentage and gradually increase:

```bash
# Start with 10% on green
curl -X POST http://localhost:5000/admin/traffic/switch \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"serviceName": "api", "blueEndpoint": "http://blue:8080", "greenEndpoint": "http://green:8080", "greenPercentage": 10}'

# Wait and monitor metrics...

# Increase to 25%
curl -X POST http://localhost:5000/admin/traffic/switch \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"serviceName": "api", "blueEndpoint": "http://blue:8080", "greenEndpoint": "http://green:8080", "greenPercentage": 25}'

# Continue until 100%
```

### 2. Automated Canary Deployment

Let the system handle gradual rollout automatically:

```bash
curl -X POST http://localhost:5000/admin/traffic/canary \
  -H "Authorization: Bearer $TOKEN" \
  -d '{
    "serviceName": "api",
    "blueEndpoint": "http://blue:8080",
    "greenEndpoint": "http://green:8080",
    "strategy": {
      "trafficSteps": [10, 25, 50, 100],
      "soakDurationSeconds": 300,
      "autoRollback": true
    }
  }'
```

### 3. Emergency Rollback

Quickly rollback if issues are detected:

```bash
curl -X POST http://localhost:5000/admin/traffic/rollback \
  -H "Authorization: Bearer $TOKEN" \
  -d '{"serviceName": "api", "blueEndpoint": "http://blue:8080", "greenEndpoint": "http://green:8080"}'
```

### 4. Check Current Status

Monitor current traffic distribution:

```bash
curl -X GET http://localhost:5000/admin/traffic/status \
  -H "Authorization: Bearer $TOKEN" | jq
```

---

## Dependencies

The Traffic Management Endpoints depend on:

- **BlueGreenTrafficManager** (`Honua.Server.Core.BlueGreen`)
  - Handles actual traffic switching logic
  - Manages YARP proxy configuration

- **IProxyConfigProvider** (YARP)
  - Provides access to current proxy configuration

- **IHttpClientFactory**
  - Used for health checks during canary deployments

- **ILogger<BlueGreenTrafficManager>**
  - Structured logging for all operations

---

## Testing

### Unit Testing Example

```csharp
[Fact]
public async Task SwitchTraffic_ValidRequest_ReturnsSuccess()
{
    // Arrange
    var mockTrafficManager = new Mock<BlueGreenTrafficManager>();
    mockTrafficManager
        .Setup(m => m.SwitchTrafficAsync(It.IsAny<string>(), It.IsAny<string>(),
               It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(new TrafficSwitchResult
        {
            Success = true,
            BlueTrafficPercentage = 75,
            GreenTrafficPercentage = 25
        });

    // Act & Assert
    // Test endpoint logic...
}
```

### Integration Testing Example

```csharp
[Fact]
public async Task TrafficManagement_EndToEnd_Success()
{
    // Arrange
    var factory = new WebApplicationFactory<Program>();
    var client = factory.CreateClient();

    var request = new TrafficSwitchRequest
    {
        ServiceName = "test-service",
        BlueEndpoint = "http://blue:8080",
        GreenEndpoint = "http://green:8080",
        GreenPercentage = 25
    };

    // Act
    var response = await client.PostAsJsonAsync("/admin/traffic/switch", request);

    // Assert
    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<TrafficSwitchResponse>();
    Assert.True(result.Success);
    Assert.Equal(25, result.GreenTrafficPercentage);
}
```

---

## OpenAPI/Swagger Documentation

When registered with Swagger, the endpoints will appear in the Swagger UI under the "Traffic Management" tag with full documentation of request/response models.

Access Swagger UI at: `http://localhost:5000/swagger`

---

## Troubleshooting

### Issue: Authentication failures

**Solution**: Ensure JWT bearer authentication is properly configured and tokens are valid.

```csharp
builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.Authority = "https://your-auth-server";
        options.Audience = "gateway-api";
        options.RequireHttpsMetadata = false; // Only for development
    });
```

### Issue: Rate limiting too restrictive

**Solution**: Adjust rate limiting configuration in Program.cs:

```csharp
options.AddPolicy("per-ip", context =>
    RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 1000,  // Increase limit
            Window = TimeSpan.FromSeconds(60)
        }));
```

### Issue: Canary deployment health checks failing

**Solution**: Ensure green deployment has a working `/health` endpoint that returns 200 OK when healthy.

---

## Additional Resources

- **BlueGreenTrafficManager**: `/home/mike/projects/Honua.Server/src/Honua.Server.Core/BlueGreen/BlueGreenTrafficManager.cs`
- **YARP Documentation**: https://microsoft.github.io/reverse-proxy/
- **ASP.NET Core Minimal APIs**: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/minimal-apis
