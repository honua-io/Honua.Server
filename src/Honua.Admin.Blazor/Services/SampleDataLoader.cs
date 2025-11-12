// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.Text.Json;

namespace Honua.Admin.Blazor.Services;

/// <summary>
/// Service for loading sample data and examples for new users
/// </summary>
public class SampleDataLoader
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SampleDataLoader> _logger;

    public SampleDataLoader(HttpClient httpClient, ILogger<SampleDataLoader> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Load sample datasets for onboarding
    /// </summary>
    public async Task<List<SampleDataset>> GetSampleDatasetsAsync()
    {
        return new List<SampleDataset>
        {
            new()
            {
                Id = "sample-cities",
                Name = "World Cities",
                Description = "Major cities around the world with population data",
                Type = "Point",
                Format = "GeoJSON",
                Size = "2.4 MB",
                FeatureCount = 15000,
                PreviewUrl = "/sample-data/world-cities-preview.png",
                DownloadUrl = "/sample-data/world-cities.geojson",
                Tags = new[] { "Cities", "Population", "Global" },
                SampleStyle = new
                {
                    type = "circle",
                    paint = new
                    {
                        circleRadius = new[] { "interpolate", new[] { "linear" }, new[] { "get", "population" }, 100000, 4, 10000000, 20 },
                        circleColor = new[] { "interpolate", new[] { "linear" }, new[] { "get", "population" }, 100000, "#ffffb2", 1000000, "#fd8d3c", 10000000, "#bd0026" },
                        circleOpacity = 0.8
                    }
                }
            },
            new()
            {
                Id = "sample-countries",
                Name = "Country Boundaries",
                Description = "World country boundaries with demographic statistics",
                Type = "Polygon",
                Format = "GeoJSON",
                Size = "8.1 MB",
                FeatureCount = 177,
                PreviewUrl = "/sample-data/countries-preview.png",
                DownloadUrl = "/sample-data/countries.geojson",
                Tags = new[] { "Countries", "Boundaries", "Demographics" },
                SampleStyle = new
                {
                    type = "fill",
                    paint = new
                    {
                        fillColor = "#088",
                        fillOpacity = 0.5,
                        fillOutlineColor = "#fff"
                    }
                }
            },
            new()
            {
                Id = "sample-earthquakes",
                Name = "Recent Earthquakes",
                Description = "Earthquake events from the past 30 days (USGS data)",
                Type = "Point",
                Format = "GeoJSON",
                Size = "1.2 MB",
                FeatureCount = 8742,
                PreviewUrl = "/sample-data/earthquakes-preview.png",
                DownloadUrl = "/sample-data/earthquakes.geojson",
                Tags = new[] { "Earthquakes", "Natural Events", "Real-time" },
                SampleStyle = new
                {
                    type = "circle",
                    paint = new
                    {
                        circleRadius = new[] { "interpolate", new[] { "linear" }, new[] { "get", "mag" }, 0, 2, 8, 30 },
                        circleColor = new[] { "interpolate", new[] { "linear" }, new[] { "get", "mag" }, 0, "#ffffb2", 4, "#fecc5c", 6, "#fd8d3c", 8, "#e31a1c" },
                        circleOpacity = 0.6
                    }
                }
            },
            new()
            {
                Id = "sample-roads",
                Name = "Major Roads Network",
                Description = "Primary and secondary road networks for select regions",
                Type = "LineString",
                Format = "GeoJSON",
                Size = "5.7 MB",
                FeatureCount = 12543,
                PreviewUrl = "/sample-data/roads-preview.png",
                DownloadUrl = "/sample-data/roads.geojson",
                Tags = new[] { "Roads", "Transportation", "Infrastructure" },
                SampleStyle = new
                {
                    type = "line",
                    paint = new
                    {
                        lineColor = "#ff6b6b",
                        lineWidth = 2,
                        lineOpacity = 0.8
                    }
                }
            },
            new()
            {
                Id = "sample-parks",
                Name = "National Parks",
                Description = "Protected areas and national parks worldwide",
                Type = "Polygon",
                Format = "GeoJSON",
                Size = "3.8 MB",
                FeatureCount = 4521,
                PreviewUrl = "/sample-data/parks-preview.png",
                DownloadUrl = "/sample-data/parks.geojson",
                Tags = new[] { "Parks", "Protected Areas", "Conservation" },
                SampleStyle = new
                {
                    type = "fill",
                    paint = new
                    {
                        fillColor = "#228B22",
                        fillOpacity = 0.4,
                        fillOutlineColor = "#006400"
                    }
                }
            }
        };
    }

    /// <summary>
    /// Get example maps with pre-configured styles
    /// </summary>
    public async Task<List<ExampleMap>> GetExampleMapsAsync()
    {
        return new List<ExampleMap>
        {
            new()
            {
                Id = "example-population-density",
                Name = "Population Density Heatmap",
                Description = "Visualize global population density using color gradients",
                Category = "Demographics",
                PreviewUrl = "/sample-data/example-population-map.png",
                Datasets = new[] { "sample-cities", "sample-countries" },
                Features = new[] { "Choropleth styling", "Data-driven colors", "Interactive tooltips" }
            },
            new()
            {
                Id = "example-seismic-activity",
                Name = "Seismic Activity Monitor",
                Description = "Real-time earthquake visualization with magnitude scaling",
                Category = "Natural Events",
                PreviewUrl = "/sample-data/example-earthquake-map.png",
                Datasets = new[] { "sample-earthquakes" },
                Features = new[] { "Proportional symbols", "Time animation", "Magnitude filters" }
            },
            new()
            {
                Id = "example-transportation",
                Name = "Transportation Network",
                Description = "Multi-layer map showing roads, cities, and regions",
                Category = "Infrastructure",
                PreviewUrl = "/sample-data/example-transport-map.png",
                Datasets = new[] { "sample-roads", "sample-cities", "sample-countries" },
                Features = new[] { "Multi-layer styling", "Label placement", "Zoom-based visibility" }
            },
            new()
            {
                Id = "example-conservation",
                Name = "Conservation Areas",
                Description = "Protected areas and national parks with contextual data",
                Category = "Environment",
                PreviewUrl = "/sample-data/example-parks-map.png",
                Datasets = new[] { "sample-parks", "sample-countries" },
                Features = new[] { "Pattern fills", "Custom legends", "Area calculations" }
            }
        };
    }

    /// <summary>
    /// Get template dashboards
    /// </summary>
    public async Task<List<DashboardTemplate>> GetDashboardTemplatesAsync()
    {
        return new List<DashboardTemplate>
        {
            new()
            {
                Id = "template-analytics",
                Name = "Spatial Analytics Dashboard",
                Description = "Comprehensive analytics with maps, charts, and statistics",
                Widgets = new[]
                {
                    "Map widget showing feature distribution",
                    "Bar chart of features by category",
                    "Stats cards with key metrics",
                    "Time series chart of temporal data",
                    "Data table with filtering"
                },
                PreviewUrl = "/sample-data/template-analytics.png",
                UseCase = "Data exploration and analysis"
            },
            new()
            {
                Id = "template-monitoring",
                Name = "Real-Time Monitoring",
                Description = "Live data monitoring with alerts and notifications",
                Widgets = new[]
                {
                    "Live map with auto-refresh",
                    "Gauge widgets for KPIs",
                    "Alert timeline",
                    "Status indicators"
                },
                PreviewUrl = "/sample-data/template-monitoring.png",
                UseCase = "Operations monitoring"
            },
            new()
            {
                Id = "template-executive",
                Name = "Executive Summary",
                Description = "High-level overview for stakeholders",
                Widgets = new[]
                {
                    "Summary statistics",
                    "Trend charts",
                    "Geographic overview map",
                    "Top-level metrics"
                },
                PreviewUrl = "/sample-data/template-executive.png",
                UseCase = "Leadership reporting"
            }
        };
    }

    /// <summary>
    /// Import a sample dataset
    /// </summary>
    public async Task<bool> ImportSampleDatasetAsync(string datasetId)
    {
        try
        {
            var datasets = await GetSampleDatasetsAsync();
            var dataset = datasets.FirstOrDefault(d => d.Id == datasetId);

            if (dataset == null)
            {
                _logger.LogWarning("Sample dataset not found: {DatasetId}", datasetId);
                return false;
            }

            // In a real implementation, this would:
            // 1. Download the sample data file
            // 2. Import it via the ImportApiClient
            // 3. Create a service with appropriate styling

            _logger.LogInformation("Importing sample dataset: {DatasetName}", dataset.Name);

            // Placeholder for actual implementation
            await Task.Delay(100);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to import sample dataset: {DatasetId}", datasetId);
            return false;
        }
    }

    /// <summary>
    /// Create an example map
    /// </summary>
    public async Task<bool> CreateExampleMapAsync(string mapId)
    {
        try
        {
            var maps = await GetExampleMapsAsync();
            var map = maps.FirstOrDefault(m => m.Id == mapId);

            if (map == null)
            {
                _logger.LogWarning("Example map not found: {MapId}", mapId);
                return false;
            }

            // In a real implementation, this would:
            // 1. Ensure required datasets are imported
            // 2. Create a map via MapApiClient
            // 3. Configure layers and styling
            // 4. Set initial view

            _logger.LogInformation("Creating example map: {MapName}", map.Name);

            // Placeholder for actual implementation
            await Task.Delay(100);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create example map: {MapId}", mapId);
            return false;
        }
    }

    /// <summary>
    /// Create a dashboard from template
    /// </summary>
    public async Task<bool> CreateDashboardFromTemplateAsync(string templateId)
    {
        try
        {
            var templates = await GetDashboardTemplatesAsync();
            var template = templates.FirstOrDefault(t => t.Id == templateId);

            if (template == null)
            {
                _logger.LogWarning("Dashboard template not found: {TemplateId}", templateId);
                return false;
            }

            // In a real implementation, this would:
            // 1. Create a new dashboard
            // 2. Add and configure widgets
            // 3. Set up layout and styling

            _logger.LogInformation("Creating dashboard from template: {TemplateName}", template.Name);

            // Placeholder for actual implementation
            await Task.Delay(100);

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create dashboard from template: {TemplateId}", templateId);
            return false;
        }
    }
}

/// <summary>
/// Sample dataset definition
/// </summary>
public class SampleDataset
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Format { get; set; } = string.Empty;
    public string Size { get; set; } = string.Empty;
    public int FeatureCount { get; set; }
    public string PreviewUrl { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string[] Tags { get; set; } = Array.Empty<string>();
    public object? SampleStyle { get; set; }
}

/// <summary>
/// Example map definition
/// </summary>
public class ExampleMap
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string PreviewUrl { get; set; } = string.Empty;
    public string[] Datasets { get; set; } = Array.Empty<string>();
    public string[] Features { get; set; } = Array.Empty<string>();
}

/// <summary>
/// Dashboard template definition
/// </summary>
public class DashboardTemplate
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string[] Widgets { get; set; } = Array.Empty<string>();
    public string PreviewUrl { get; set; } = string.Empty;
    public string UseCase { get; set; } = string.Empty;
}
