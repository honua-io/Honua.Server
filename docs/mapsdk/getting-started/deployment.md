# Deployment Guide

Learn how to deploy your Honua.MapSDK application to production environments including Azure, AWS, and static hosting platforms.

---

## Table of Contents

1. [Build for Production](#build-for-production)
2. [Deployment Platforms](#deployment-platforms)
3. [Optimization](#optimization)
4. [Environment Configuration](#environment-configuration)
5. [CDN Configuration](#cdn-configuration)
6. [Troubleshooting](#troubleshooting)

---

## Build for Production

### Blazor Server

Build and publish your Blazor Server application:

```bash
# Clean previous builds
dotnet clean

# Build in Release mode
dotnet build -c Release

# Publish to output folder
dotnet publish -c Release -o ./publish
```

### Blazor WebAssembly

Build and optimize for WebAssembly:

```bash
# Build with AOT compilation (recommended)
dotnet publish -c Release -o ./publish /p:RunAOTCompilation=true

# Without AOT (faster build, larger size)
dotnet publish -c Release -o ./publish
```

---

## Optimization

### Bundle Size Optimization

#### 1. Enable Trimming

Update your `.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.BlazorWebAssembly">
  <PropertyGroup>
    <PublishTrimmed>true</PublishTrimmed>
    <TrimMode>link</TrimMode>
  </PropertyGroup>
</Project>
```

#### 2. Enable Compression

Configure compression in `Program.cs`:

**Blazor Server:**
```csharp
builder.Services.AddResponseCompression(opts =>
{
    opts.MimeTypes = ResponseCompressionDefaults.MimeTypes.Concat(
        new[] { "application/octet-stream" });
});

var app = builder.Build();

app.UseResponseCompression();
```

**Blazor WebAssembly:**

Add to `wwwroot/web.config`:
```xml
<configuration>
  <system.webServer>
    <staticContent>
      <remove fileExtension=".dat" />
      <remove fileExtension=".dll" />
      <remove fileExtension=".json" />
      <remove fileExtension=".wasm" />
      <remove fileExtension=".woff" />
      <remove fileExtension=".woff2" />
      <mimeMap fileExtension=".dat" mimeType="application/octet-stream" />
      <mimeMap fileExtension=".dll" mimeType="application/octet-stream" />
      <mimeMap fileExtension=".json" mimeType="application/json" />
      <mimeMap fileExtension=".wasm" mimeType="application/wasm" />
      <mimeMap fileExtension=".woff" mimeType="font/woff" />
      <mimeMap fileExtension=".woff2" mimeType="font/woff2" />
    </staticContent>
    <rewrite>
      <rules>
        <rule name="Compress Files" stopProcessing="false">
          <match url=".*" />
          <conditions>
            <add input="{HTTP_ACCEPT_ENCODING}" pattern="gzip" />
          </conditions>
          <action type="Rewrite" url="{R:0}.gz" />
        </rule>
        <rule name="SPA Routes" stopProcessing="true">
          <match url=".*" />
          <conditions logicalGrouping="MatchAll">
            <add input="{REQUEST_FILENAME}" matchType="IsFile" negate="true" />
            <add input="{REQUEST_FILENAME}" matchType="IsDirectory" negate="true" />
          </conditions>
          <action type="Rewrite" url="/" />
        </rule>
      </rules>
    </rewrite>
    <urlCompression doStaticCompression="true" doDynamicCompression="true" />
  </system.webServer>
</configuration>
```

#### 3. Lazy Loading

Load map components only when needed:

```razor
@code {
    private bool _loadMap = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await Task.Delay(100); // Allow page to render
            _loadMap = true;
            StateHasChanged();
        }
    }
}

@if (_loadMap)
{
    <HonuaMap Id="map1" />
}
```

---

## Deployment Platforms

### Azure App Service (Blazor Server)

#### 1. Create Azure App Service

```bash
# Create resource group
az group create --name MyResourceGroup --location eastus

# Create App Service plan
az appservice plan create \
  --name MyAppServicePlan \
  --resource-group MyResourceGroup \
  --sku B1 \
  --is-linux

# Create web app
az webapp create \
  --resource-group MyResourceGroup \
  --plan MyAppServicePlan \
  --name my-mapview-app \
  --runtime "DOTNETCORE:8.0"
```

#### 2. Deploy via Azure CLI

```bash
# Publish locally
dotnet publish -c Release -o ./publish

# Create deployment package
cd publish
zip -r ../deploy.zip .
cd ..

# Deploy to Azure
az webapp deployment source config-zip \
  --resource-group MyResourceGroup \
  --name my-mapview-app \
  --src deploy.zip
```

#### 3. Deploy via GitHub Actions

Create `.github/workflows/azure-deploy.yml`:

```yaml
name: Deploy to Azure

on:
  push:
    branches: [ main ]

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v2

      - name: Setup .NET
        uses: actions/setup-dotnet@v1
        with:
          dotnet-version: '8.0.x'

      - name: Build and publish
        run: |
          dotnet restore
          dotnet build --configuration Release
          dotnet publish -c Release -o ./publish

      - name: Deploy to Azure Web App
        uses: azure/webapps-deploy@v2
        with:
          app-name: 'my-mapview-app'
          publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
          package: ./publish
```

---

### Azure Static Web Apps (Blazor WebAssembly)

#### 1. Create via Azure Portal

1. Go to Azure Portal
2. Create new "Static Web App"
3. Connect to your GitHub repository
4. Set build details:
   - App location: `/`
   - API location: (leave empty)
   - Output location: `wwwroot`

#### 2. Configure Routing

Create `staticwebapp.config.json` in `wwwroot`:

```json
{
  "navigationFallback": {
    "rewrite": "/index.html",
    "exclude": ["/_framework/*", "/css/*", "/js/*", "*.{css,js,woff,woff2,ttf,svg,png,jpg,jpeg,gif,ico}"]
  },
  "mimeTypes": {
    ".json": "application/json",
    ".wasm": "application/wasm"
  },
  "globalHeaders": {
    "cache-control": "public, max-age=31536000, immutable"
  },
  "routes": [
    {
      "route": "/_framework/*",
      "headers": {
        "cache-control": "public, max-age=31536000, immutable"
      }
    }
  ]
}
```

---

### AWS Elastic Beanstalk (Blazor Server)

#### 1. Install AWS CLI and EB CLI

```bash
pip install awsebcli --upgrade
```

#### 2. Initialize EB Application

```bash
eb init -p "64bit Amazon Linux 2 v2.5.4 running .NET 8" my-mapview-app --region us-east-1
```

#### 3. Create Environment and Deploy

```bash
eb create production-env
eb deploy
```

#### 4. Configuration

Create `.ebextensions/01-app.config`:

```yaml
option_settings:
  aws:elasticbeanstalk:container:dotnet:apppool:
    Enable 32-bit Applications: false
  aws:elasticbeanstalk:environment:proxy:
    ProxyServer: nginx
```

---

### AWS S3 + CloudFront (Blazor WebAssembly)

#### 1. Build and Upload to S3

```bash
# Build
dotnet publish -c Release -o ./publish

# Create S3 bucket
aws s3 mb s3://my-mapview-app

# Upload files
aws s3 sync ./publish/wwwroot s3://my-mapview-app --acl public-read

# Configure for static website
aws s3 website s3://my-mapview-app --index-document index.html --error-document index.html
```

#### 2. Configure CloudFront

```bash
# Create CloudFront distribution (via AWS Console or CLI)
aws cloudfront create-distribution \
  --origin-domain-name my-mapview-app.s3.amazonaws.com \
  --default-root-object index.html
```

---

### Docker Deployment

#### Blazor Server Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["MyMapApp/MyMapApp.csproj", "MyMapApp/"]
RUN dotnet restore "MyMapApp/MyMapApp.csproj"

COPY . .
WORKDIR "/src/MyMapApp"
RUN dotnet build "MyMapApp.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MyMapApp.csproj" -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .

EXPOSE 80
EXPOSE 443

ENTRYPOINT ["dotnet", "MyMapApp.dll"]
```

#### Build and Run Docker Container

```bash
# Build image
docker build -t my-mapview-app .

# Run container
docker run -d -p 8080:80 --name mapview my-mapview-app

# Push to registry
docker tag my-mapview-app myregistry.azurecr.io/my-mapview-app
docker push myregistry.azurecr.io/my-mapview-app
```

---

## Environment Configuration

### appsettings.json

Create environment-specific settings:

**appsettings.Production.json:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Honua.MapSDK": "Information"
    }
  },
  "MapSDK": {
    "DefaultMapStyle": "https://tiles.yourdomain.com/style.json",
    "ApiKey": "your-production-api-key",
    "TileServerUrl": "https://tiles.yourdomain.com",
    "EnableCaching": true,
    "CacheDuration": "24:00:00"
  },
  "AllowedHosts": "yourdomain.com"
}
```

### Environment Variables

Set environment variables:

**Linux/Mac:**
```bash
export ASPNETCORE_ENVIRONMENT=Production
export MapSDK__ApiKey=your-api-key
export MapSDK__TileServerUrl=https://tiles.yourdomain.com
```

**Windows:**
```powershell
$env:ASPNETCORE_ENVIRONMENT="Production"
$env:MapSDK__ApiKey="your-api-key"
$env:MapSDK__TileServerUrl="https://tiles.yourdomain.com"
```

**Azure App Service:**
```bash
az webapp config appsettings set \
  --resource-group MyResourceGroup \
  --name my-mapview-app \
  --settings MapSDK__ApiKey="your-api-key" MapSDK__TileServerUrl="https://tiles.yourdomain.com"
```

---

## CDN Configuration

### MapLibre GL JS via CDN

For faster loading, use CDN for MapLibre:

```html
<!-- Use versioned CDN URLs -->
<link href="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.css" rel="stylesheet" />
<script src="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.js"></script>
```

### Static Assets via CDN

Configure Azure CDN or CloudFront:

1. **Create CDN endpoint** pointing to your storage
2. **Update references** in HTML:

```html
<link href="https://cdn.yourdomain.com/css/honua-mapsdk.css" rel="stylesheet" />
<script src="https://cdn.yourdomain.com/js/honua-map.js"></script>
```

3. **Set cache headers**:

```csharp
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = ctx =>
    {
        ctx.Context.Response.Headers.Append(
            "Cache-Control", "public,max-age=31536000");
    }
});
```

---

## Performance Checklist

Before deploying to production:

- [ ] Enable response compression
- [ ] Configure caching headers
- [ ] Use CDN for static assets
- [ ] Enable AOT compilation (WebAssembly)
- [ ] Minify and bundle CSS/JS
- [ ] Optimize images and icons
- [ ] Use versioned CDN URLs
- [ ] Configure health checks
- [ ] Set up monitoring and logging
- [ ] Test on target environment
- [ ] Load test with expected traffic
- [ ] Review security headers

---

## Monitoring and Logging

### Application Insights (Azure)

```csharp
builder.Services.AddApplicationInsightsTelemetry(
    builder.Configuration["ApplicationInsights:ConnectionString"]);
```

### Custom Logging

```csharp
@inject ILogger<MyComponent> Logger

@code {
    private void HandleMapReady(MapReadyMessage msg)
    {
        Logger.LogInformation("Map {MapId} initialized at {Time}",
            msg.MapId, DateTime.UtcNow);
    }
}
```

---

## Troubleshooting Deployment Issues

### Issue: 404 errors on refresh

**Problem**: Blazor WebAssembly routes return 404 when refreshed.

**Solution**: Configure URL rewriting (see Azure Static Web Apps section).

### Issue: Large initial download

**Problem**: App takes too long to load.

**Solutions:**
- Enable AOT compilation
- Use lazy loading
- Split into smaller assemblies
- Use PWA for caching

### Issue: Maps not rendering in production

**Problem**: Map shows blank in production but works locally.

**Solutions:**
- Check console for CORS errors
- Verify tile server URLs are accessible
- Ensure API keys are configured
- Check CSP headers

### Issue: WebSocket errors (Blazor Server)

**Problem**: WebSocket connection failures.

**Solutions:**
- Enable WebSockets on App Service
- Configure SignalR for long polling fallback:

```csharp
builder.Services.AddServerSideBlazor()
    .AddHubOptions(options =>
    {
        options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
        options.HandshakeTimeout = TimeSpan.FromSeconds(30);
    });
```

---

## Security Considerations

### HTTPS

Always use HTTPS in production:

```csharp
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}
```

### Content Security Policy

Add CSP headers:

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("Content-Security-Policy",
        "default-src 'self'; " +
        "script-src 'self' 'unsafe-inline' 'unsafe-eval' https://unpkg.com; " +
        "style-src 'self' 'unsafe-inline' https://unpkg.com https://fonts.googleapis.com; " +
        "font-src 'self' https://fonts.gstatic.com; " +
        "img-src 'self' data: https:; " +
        "connect-src 'self' https://tiles.yourdomain.com wss://yourdomain.com;");

    await next();
});
```

### API Keys

Never commit API keys to source control. Use:

- Azure Key Vault
- AWS Secrets Manager
- Environment variables
- User secrets (development only)

---

## Next Steps

- [Performance Tips](../recipes/performance-tips.md)
- [Troubleshooting](../recipes/troubleshooting.md)
- [Best Practices](../recipes/best-practices.md)

---

**Your application is now ready for production!**
