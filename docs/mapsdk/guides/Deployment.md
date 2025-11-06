# Deployment Guide

This comprehensive guide covers deploying Honua.MapSDK applications to various platforms including Azure, AWS, Docker, and more.

---

## Table of Contents

1. [Azure Deployment](#azure-deployment)
2. [AWS Deployment](#aws-deployment)
3. [Docker Containerization](#docker-containerization)
4. [Environment Configuration](#environment-configuration)
5. [SSL/Security](#sslsecurity)
6. [Performance Monitoring](#performance-monitoring)
7. [Scaling Strategies](#scaling-strategies)

---

## Azure Deployment

### Azure App Service (Blazor Server)

```bash
# Login to Azure
az login

# Create resource group
az group create --name MapSDK-RG --location eastus

# Create App Service plan
az appservice plan create \
    --name MapSDK-Plan \
    --resource-group MapSDK-RG \
    --sku B1 \
    --is-linux

# Create web app
az webapp create \
    --name my-mapsdk-app \
    --resource-group MapSDK-RG \
    --plan MapSDK-Plan \
    --runtime "DOTNETCORE:8.0"

# Configure app settings
az webapp config appsettings set \
    --name my-mapsdk-app \
    --resource-group MapSDK-RG \
    --settings \
        ASPNETCORE_ENVIRONMENT=Production \
        MapTileServer__Url=https://tiles.example.com

# Deploy
dotnet publish -c Release
cd bin/Release/net8.0/publish
az webapp deployment source config-zip \
    --resource-group MapSDK-RG \
    --name my-mapsdk-app \
    --src publish.zip
```

### Azure Static Web Apps (Blazor WebAssembly)

```yaml
# .github/workflows/azure-static-web-apps.yml
name: Azure Static Web Apps CI/CD

on:
  push:
    branches:
      - main

jobs:
  build_and_deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
        with:
          submodules: true

      - name: Build And Deploy
        uses: Azure/static-web-apps-deploy@v1
        with:
          azure_static_web_apps_api_token: ${{ secrets.AZURE_STATIC_WEB_APPS_API_TOKEN }}
          repo_token: ${{ secrets.GITHUB_TOKEN }}
          action: "upload"
          app_location: "src/MyMapApp.Client"
          api_location: "src/MyMapApp.Api"
          output_location: "wwwroot"
```

### Azure Container Apps

```bash
# Create container registry
az acr create \
    --resource-group MapSDK-RG \
    --name mapsdk \
    --sku Basic

# Build and push image
az acr build \
    --registry mapsdk \
    --image mapsdk-app:latest \
    .

# Create container app environment
az containerapp env create \
    --name mapsdk-env \
    --resource-group MapSDK-RG \
    --location eastus

# Deploy container app
az containerapp create \
    --name mapsdk-app \
    --resource-group MapSDK-RG \
    --environment mapsdk-env \
    --image mapsdk.azurecr.io/mapsdk-app:latest \
    --target-port 80 \
    --ingress external \
    --registry-server mapsdk.azurecr.io
```

---

## AWS Deployment

### AWS Elastic Beanstalk

```bash
# Install EB CLI
pip install awsebcli

# Initialize Elastic Beanstalk
eb init -p "64bit Amazon Linux 2 v2.5.0 running .NET Core" mapsdk-app

# Create environment
eb create mapsdk-env \
    --instance-type t3.medium \
    --envvars \
        ASPNETCORE_ENVIRONMENT=Production,\
        MapTileServer__Url=https://tiles.example.com

# Deploy
dotnet publish -c Release
eb deploy
```

### AWS S3 + CloudFront (Blazor WASM)

```bash
# Build for production
dotnet publish -c Release

# Create S3 bucket
aws s3 mb s3://mapsdk-app

# Enable static website hosting
aws s3 website s3://mapsdk-app/ \
    --index-document index.html \
    --error-document index.html

# Upload files
aws s3 sync bin/Release/net8.0/publish/wwwroot/ s3://mapsdk-app/ \
    --acl public-read \
    --cache-control "max-age=31536000"

# Create CloudFront distribution
aws cloudfront create-distribution \
    --origin-domain-name mapsdk-app.s3.amazonaws.com \
    --default-root-object index.html
```

### AWS ECS (Docker)

```bash
# Create ECR repository
aws ecr create-repository --repository-name mapsdk-app

# Build and push image
$(aws ecr get-login --no-include-email)
docker build -t mapsdk-app .
docker tag mapsdk-app:latest $ECR_REGISTRY/mapsdk-app:latest
docker push $ECR_REGISTRY/mapsdk-app:latest

# Create ECS cluster
aws ecs create-cluster --cluster-name mapsdk-cluster

# Register task definition
aws ecs register-task-definition --cli-input-json file://task-definition.json

# Create service
aws ecs create-service \
    --cluster mapsdk-cluster \
    --service-name mapsdk-service \
    --task-definition mapsdk-app \
    --desired-count 2 \
    --launch-type FARGATE
```

---

## Docker Containerization

### Dockerfile

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy project files
COPY ["MyMapApp/MyMapApp.csproj", "MyMapApp/"]
RUN dotnet restore "MyMapApp/MyMapApp.csproj"

# Copy source code and build
COPY . .
WORKDIR "/src/MyMapApp"
RUN dotnet build "MyMapApp.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "MyMapApp.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
EXPOSE 80
EXPOSE 443

# Install dependencies for MapLibre (if needed)
RUN apt-get update && apt-get install -y \
    libgdiplus \
    libc6-dev \
    && rm -rf /var/lib/apt/lists/*

# Copy published app
COPY --from=publish /app/publish .

# Set environment variables
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production

# Health check
HEALTHCHECK --interval=30s --timeout=3s --start-period=5s --retries=3 \
    CMD curl -f http://localhost/health || exit 1

ENTRYPOINT ["dotnet", "MyMapApp.dll"]
```

### Docker Compose

```yaml
# docker-compose.yml
version: '3.8'

services:
  app:
    build:
      context: .
      dockerfile: Dockerfile
    ports:
      - "8080:80"
      - "8443:443"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__DefaultConnection=Server=db;Database=MapSDK;User=sa;Password=YourPassword123!
      - MapTileServer__Url=http://tileserver:8080
    depends_on:
      - db
      - tileserver
    restart: unless-stopped

  db:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=YourPassword123!
    ports:
      - "1433:1433"
    volumes:
      - mapsdk-data:/var/opt/mssql
    restart: unless-stopped

  tileserver:
    image: maptiler/tileserver-gl:latest
    ports:
      - "8081:8080"
    volumes:
      - ./tiles:/data
    restart: unless-stopped

  redis:
    image: redis:alpine
    ports:
      - "6379:6379"
    restart: unless-stopped

volumes:
  mapsdk-data:
```

### Build and Run

```bash
# Build image
docker build -t mapsdk-app:latest .

# Run container
docker run -d \
    --name mapsdk-app \
    -p 8080:80 \
    -e ASPNETCORE_ENVIRONMENT=Production \
    mapsdk-app:latest

# With Docker Compose
docker-compose up -d

# View logs
docker-compose logs -f app

# Scale service
docker-compose up -d --scale app=3
```

---

## Environment Configuration

### appsettings.json Structure

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "MapSDK": {
    "TileServer": {
      "Url": "https://tiles.example.com",
      "ApiKey": "${TILE_SERVER_API_KEY}"
    },
    "Features": {
      "EnableClustering": true,
      "MaxFeaturesPerRequest": 10000,
      "CacheDurationMinutes": 10
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "${DATABASE_CONNECTION_STRING}"
  },
  "Authentication": {
    "AzureAd": {
      "Instance": "https://login.microsoftonline.com/",
      "Domain": "${AZURE_AD_DOMAIN}",
      "TenantId": "${AZURE_AD_TENANT_ID}",
      "ClientId": "${AZURE_AD_CLIENT_ID}",
      "ClientSecret": "${AZURE_AD_CLIENT_SECRET}"
    }
  }
}
```

### Environment-Specific Configuration

```json
// appsettings.Development.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  },
  "MapSDK": {
    "TileServer": {
      "Url": "http://localhost:8080"
    }
  }
}

// appsettings.Production.json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning"
    }
  },
  "MapSDK": {
    "TileServer": {
      "Url": "https://tiles.production.example.com",
      "ApiKey": "${TILE_SERVER_API_KEY}"
    },
    "Features": {
      "EnableCaching": true,
      "CacheDurationMinutes": 30
    }
  }
}
```

### Loading Configuration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Add configuration sources
builder.Configuration
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
    .AddEnvironmentVariables()
    .AddUserSecrets<Program>(optional: true);

// Azure Key Vault (optional)
if (builder.Environment.IsProduction())
{
    var keyVaultUrl = builder.Configuration["KeyVault:Url"];
    if (!string.IsNullOrEmpty(keyVaultUrl))
    {
        builder.Configuration.AddAzureKeyVault(
            new Uri(keyVaultUrl),
            new DefaultAzureCredential());
    }
}

// Bind configuration
builder.Services.Configure<MapSDKOptions>(
    builder.Configuration.GetSection("MapSDK"));
```

---

## SSL/Security

### HTTPS Configuration

```csharp
// Program.cs
var builder = WebApplication.CreateBuilder(args);

// Configure Kestrel for HTTPS
builder.WebHost.ConfigureKestrel(options =>
{
    options.ListenAnyIP(80);
    options.ListenAnyIP(443, listenOptions =>
    {
        listenOptions.UseHttps(
            builder.Configuration["Certificates:Path"],
            builder.Configuration["Certificates:Password"]);
    });
});

var app = builder.Build();

// Redirect HTTP to HTTPS
app.UseHttpsRedirection();
app.UseHsts();
```

### Security Headers

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Add("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://cdn.jsdelivr.net; " +
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com; " +
        "img-src 'self' data: https:; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "connect-src 'self' https://tiles.example.com;");

    await next();
});
```

### Let's Encrypt SSL

```bash
# Install certbot
sudo apt-get install certbot

# Generate certificate
sudo certbot certonly --standalone -d yourdomain.com

# Configure renewal
sudo certbot renew --dry-run

# Add to crontab for auto-renewal
0 12 * * * /usr/bin/certbot renew --quiet
```

---

## Performance Monitoring

### Application Insights (Azure)

```csharp
// Program.cs
builder.Services.AddApplicationInsightsTelemetry(options =>
{
    options.ConnectionString = builder.Configuration["ApplicationInsights:ConnectionString"];
});

// Custom telemetry
public class MapPerformanceMiddleware
{
    private readonly RequestDelegate _next;
    private readonly TelemetryClient _telemetry;

    public MapPerformanceMiddleware(RequestDelegate next, TelemetryClient telemetry)
    {
        _next = next;
        _telemetry = telemetry;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var sw = Stopwatch.StartNew();

        await _next(context);

        sw.Stop();

        _telemetry.TrackMetric("RequestDuration", sw.ElapsedMilliseconds);
        _telemetry.TrackMetric("MapRenderTime", GetMapRenderTime(context));
    }
}
```

### Health Checks

```csharp
// Program.cs
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>()
    .AddUrlGroup(new Uri(builder.Configuration["MapTileServer:Url"]), "Tile Server")
    .AddRedis(builder.Configuration["Redis:ConnectionString"])
    .AddCheck<MapServiceHealthCheck>("MapService");

app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = UIResponseWriter.WriteHealthCheckUIResponse
});

// Custom health check
public class MapServiceHealthCheck : IHealthCheck
{
    private readonly IMapService _mapService;

    public MapServiceHealthCheck(IMapService mapService)
    {
        _mapService = mapService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _mapService.PingAsync();
            return HealthCheckResult.Healthy("Map service is healthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Map service is unhealthy", ex);
        }
    }
}
```

---

## Scaling Strategies

### Horizontal Scaling

```yaml
# Kubernetes deployment
apiVersion: apps/v1
kind: Deployment
metadata:
  name: mapsdk-app
spec:
  replicas: 3
  selector:
    matchLabels:
      app: mapsdk
  template:
    metadata:
      labels:
        app: mapsdk
    spec:
      containers:
      - name: mapsdk
        image: mapsdk-app:latest
        ports:
        - containerPort: 80
        resources:
          requests:
            memory: "512Mi"
            cpu: "500m"
          limits:
            memory: "1Gi"
            cpu: "1000m"
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: mapsdk-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: mapsdk-app
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
```

### Load Balancing

```nginx
# nginx.conf
upstream mapsdk_backend {
    least_conn;
    server app1:80 weight=3;
    server app2:80 weight=2;
    server app3:80 weight=1;
}

server {
    listen 80;
    server_name yourdomain.com;

    location / {
        proxy_pass http://mapsdk_backend;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;

        # WebSocket support
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
    }

    # Cache static assets
    location ~* \.(jpg|jpeg|png|gif|ico|css|js|woff2)$ {
        expires 1y;
        add_header Cache-Control "public, immutable";
        proxy_pass http://mapsdk_backend;
    }
}
```

### Caching with Redis

```csharp
// Program.cs
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = builder.Configuration["Redis:ConnectionString"];
    options.InstanceName = "MapSDK_";
});

// Usage
public class CachedFeatureService : IFeatureService
{
    private readonly IFeatureService _inner;
    private readonly IDistributedCache _cache;

    public async Task<List<Feature>> GetFeaturesAsync(string region)
    {
        var cacheKey = $"features_{region}";

        // Try cache first
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached != null)
        {
            return JsonSerializer.Deserialize<List<Feature>>(cached)!;
        }

        // Fetch from database
        var features = await _inner.GetFeaturesAsync(region);

        // Cache result
        await _cache.SetStringAsync(cacheKey,
            JsonSerializer.Serialize(features),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
            });

        return features;
    }
}
```

---

## Deployment Checklist

- [ ] Configure production appsettings.json
- [ ] Set environment variables
- [ ] Enable HTTPS and HSTS
- [ ] Configure security headers
- [ ] Set up health checks
- [ ] Configure logging and monitoring
- [ ] Enable response compression
- [ ] Set up CDN for static assets
- [ ] Configure database connection pooling
- [ ] Set up automated backups
- [ ] Configure auto-scaling
- [ ] Test disaster recovery
- [ ] Document deployment process

---

*Last Updated: 2025-11-06*
*MapSDK Version: 1.0.0*
