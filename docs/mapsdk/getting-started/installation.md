# Installation

This guide walks you through installing and configuring Honua.MapSDK in your Blazor application.

---

## Prerequisites

Before installing Honua.MapSDK, ensure you have:

- **.NET 8.0 SDK** or higher ([Download](https://dotnet.microsoft.com/download))
- A **Blazor Server** or **Blazor WebAssembly** project
- Basic knowledge of Blazor and C#

---

## Step 1: Install the NuGet Package

### Using .NET CLI

```bash
dotnet add package Honua.MapSDK
```

### Using Package Manager Console

```powershell
Install-Package Honua.MapSDK
```

### Using Visual Studio

1. Right-click on your project in Solution Explorer
2. Select **Manage NuGet Packages**
3. Search for **Honua.MapSDK**
4. Click **Install**

---

## Step 2: Configure Services

Add MapSDK services to your application's `Program.cs`:

### Blazor Server

```csharp
using Honua.MapSDK;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

// Add MudBlazor (if not already added)
builder.Services.AddMudServices();

// Add Honua MapSDK services
builder.Services.AddHonuaMapSDK();

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
```

### Blazor WebAssembly

```csharp
using Honua.MapSDK;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Add MudBlazor (if not already added)
builder.Services.AddMudServices();

// Add Honua MapSDK services
builder.Services.AddHonuaMapSDK();

await builder.Build().RunAsync();
```

---

## Step 3: Add CSS and JavaScript References

### For Blazor Server

Edit your `Pages/_Host.cshtml` (or `_Layout.cshtml` in .NET 8+):

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>My App</title>
    <base href="~/" />

    <!-- MudBlazor CSS -->
    <link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
    <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />

    <!-- MapLibre GL CSS -->
    <link href="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.css" rel="stylesheet" />

    <!-- Honua MapSDK CSS -->
    <link href="_content/Honua.MapSDK/css/honua-mapsdk.css" rel="stylesheet" />
</head>
<body>
    <component type="typeof(App)" render-mode="ServerPrerendered" />

    <!-- MudBlazor JS -->
    <script src="_content/MudBlazor/MudBlazor.min.js"></script>

    <!-- MapLibre GL JS -->
    <script src="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.js"></script>

    <!-- Chart.js (for charts) -->
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.js"></script>

    <!-- Blazor Server JS -->
    <script src="_framework/blazor.server.js"></script>
</body>
</html>
```

### For Blazor WebAssembly

Edit your `wwwroot/index.html`:

```html
<!DOCTYPE html>
<html lang="en">
<head>
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1.0" />
    <title>My App</title>
    <base href="/" />

    <!-- MudBlazor CSS -->
    <link href="https://fonts.googleapis.com/css?family=Roboto:300,400,500,700&display=swap" rel="stylesheet" />
    <link href="_content/MudBlazor/MudBlazor.min.css" rel="stylesheet" />

    <!-- MapLibre GL CSS -->
    <link href="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.css" rel="stylesheet" />

    <!-- Honua MapSDK CSS -->
    <link href="_content/Honua.MapSDK/css/honua-mapsdk.css" rel="stylesheet" />
</head>
<body>
    <div id="app">Loading...</div>

    <!-- MudBlazor JS -->
    <script src="_content/MudBlazor/MudBlazor.min.js"></script>

    <!-- MapLibre GL JS -->
    <script src="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.js"></script>

    <!-- Chart.js (for charts) -->
    <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.0/dist/chart.umd.js"></script>

    <!-- Blazor WebAssembly JS -->
    <script src="_framework/blazor.webassembly.js"></script>
</body>
</html>
```

---

## Step 4: Add Using Directives

Add the MapSDK namespaces to your `_Imports.razor`:

```razor
@using Honua.MapSDK
@using Honua.MapSDK.Components
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.DataGrid
@using Honua.MapSDK.Components.Chart
@using Honua.MapSDK.Components.Legend
@using Honua.MapSDK.Components.FilterPanel
@using Honua.MapSDK.Core
@using Honua.MapSDK.Core.Messages
@using Honua.MapSDK.Models
```

---

## Step 5: Verify Installation

Create a simple test page to verify everything is working:

**Pages/MapTest.razor**

```razor
@page "/map-test"
@using Honua.MapSDK.Components.Map

<PageTitle>Map Test</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Style="height: 600px;">
    <HonuaMap Id="test-map"
              Center="@(new[] { -122.4, 37.7 })"
              Zoom="10"
              MapStyle="https://demotiles.maplibre.org/style.json" />
</MudContainer>

@code {
    // Map should render successfully
}
```

Run your application and navigate to `/map-test`. You should see an interactive map.

---

## Optional: Configure MudBlazor Theme

If you haven't already configured MudBlazor, add the theme provider to your `App.razor` or `MainLayout.razor`:

```razor
<MudThemeProvider />
<MudDialogProvider />
<MudSnackbarProvider />

<MudLayout>
    @Body
</MudLayout>
```

---

## Troubleshooting

### Map not rendering

**Problem**: Map container appears empty or has no height.

**Solution**: Ensure the map container has an explicit height:

```razor
<div style="height: 500px;">
    <HonuaMap Id="map1" ... />
</div>
```

### JavaScript errors in console

**Problem**: "maplibregl is not defined" or similar errors.

**Solution**: Verify that MapLibre GL JS is loaded **before** Blazor scripts:

```html
<!-- MapLibre GL JS -->
<script src="https://unpkg.com/maplibre-gl@3.6.2/dist/maplibre-gl.js"></script>

<!-- Then Blazor scripts -->
<script src="_framework/blazor.server.js"></script>
```

### CSS styles not applied

**Problem**: Components don't look right or are unstyled.

**Solution**: Ensure all CSS files are referenced in the correct order:

1. MudBlazor CSS
2. MapLibre GL CSS
3. Honua MapSDK CSS

### NuGet package not found

**Problem**: Package restore fails or package not found.

**Solution**: Ensure you have the correct NuGet source configured:

```bash
dotnet nuget add source https://api.nuget.org/v3/index.json -n nuget.org
```

### Build errors after installation

**Problem**: Compilation errors about missing types or namespaces.

**Solution**:

1. Clean and rebuild the solution:
   ```bash
   dotnet clean
   dotnet build
   ```

2. Verify .NET SDK version:
   ```bash
   dotnet --version  # Should be 8.0 or higher
   ```

3. Check that `_Imports.razor` includes MapSDK namespaces

---

## Advanced Configuration

### Custom Map Tiles

Configure default map styles in your service configuration:

```csharp
builder.Services.AddHonuaMapSDK(options =>
{
    options.DefaultMapStyle = "https://your-tile-server.com/style.json";
    options.ApiKey = "your-api-key";  // If required by your tile provider
});
```

### Performance Options

Enable caching and performance optimizations:

```csharp
builder.Services.AddHonuaMapSDK(options =>
{
    options.EnableCaching = true;
    options.CacheDuration = TimeSpan.FromHours(24);
    options.EnableGPU = true;
});
```

### Logging

Enable detailed logging for debugging:

```csharp
builder.Logging.AddFilter("Honua.MapSDK", LogLevel.Debug);
```

---

## Next Steps

Now that MapSDK is installed, you're ready to build your first application:

- [Quick Start Guide](quick-start.md) - Create a simple map application
- [Your First Map](first-map.md) - Deep dive into map configuration
- [Your First Dashboard](your-first-dashboard.md) - Build a complete dashboard

---

## Version Compatibility

| MapSDK Version | .NET Version | Blazor Version | MudBlazor Version |
|----------------|--------------|----------------|-------------------|
| 1.0.x          | 8.0+         | .NET 8         | 6.0+              |

---

## Getting Help

If you encounter issues during installation:

1. Check the [Troubleshooting Guide](../recipes/troubleshooting.md)
2. Search [GitHub Issues](https://github.com/honua-io/Honua.Server/issues)
3. Ask in [GitHub Discussions](https://github.com/honua-io/Honua.Server/discussions)
4. Review the [FAQ](../recipes/troubleshooting.md#faq)

---

**Installation complete!** You're now ready to build amazing mapping applications with Honua.MapSDK.
