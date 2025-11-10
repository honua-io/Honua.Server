# Tutorial 03: Environmental Monitoring Application

> **Learning Objectives**: Build a real-time environmental monitoring application with time-series data visualization, timeline playback, charts, heatmaps, data grids, and alert systems.

---

## Prerequisites

- Completed [Tutorial 02: Property Dashboard](Tutorial_02_PropertyDashboard.md) OR
- Understanding of Blazor and Honua.MapSDK basics
- .NET 8.0 SDK installed

**Estimated Time**: 60 minutes

---

## Table of Contents

1. [Overview](#overview)
2. [Setup Data Models](#step-1-setup-data-models)
3. [Create Sensor Service](#step-2-create-sensor-service)
4. [Build Dashboard Layout](#step-3-build-dashboard-layout)
5. [Add Timeline Component](#step-4-add-timeline-component)
6. [Add Chart Component](#step-5-add-chart-component)
7. [Add Heatmap Visualization](#step-6-add-heatmap-visualization)
8. [Add Data Grid](#step-7-add-data-grid)
9. [Implement Real-time Updates](#step-8-implement-realtime-updates)
10. [Add Alert System](#step-9-add-alert-system)

---

## Overview

We'll build an environmental monitoring dashboard that:

- 📍 **Displays sensor locations** on a map
- 📊 **Visualizes time-series data** with interactive charts
- ⏰ **Timeline playback** to see historical data
- 🌡️ **Heatmap visualization** for temperature/pollution
- 📋 **Data grid** with sensor readings
- 🔴 **Real-time updates** via SignalR
- ⚠️ **Alert system** for threshold violations

---

## Step 1: Setup Data Models

Create `Models/Environmental.cs`:

```csharp
namespace EnvironmentalMonitoring.Models
{
    public class Sensor
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public SensorType Type { get; set; }
        public SensorStatus Status { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public string Location { get; set; } = string.Empty;
        public DateTime InstallationDate { get; set; }
        public DateTime LastReading { get; set; }
        public double? CurrentValue { get; set; }
        public string Unit { get; set; } = string.Empty;
    }

    public class SensorReading
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SensorId { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Unit { get; set; } = string.Empty;
        public double? Temperature { get; set; }
        public double? Humidity { get; set; }
        public double? AirQuality { get; set; }
        public double? Pressure { get; set; }
        public ReadingQuality Quality { get; set; }
        public double Latitude { get; set; }
        public double Longitude { get; set; }
    }

    public class TimeSeriesData
    {
        public string SensorId { get; set; } = string.Empty;
        public List<DataPoint> DataPoints { get; set; } = new();
    }

    public class DataPoint
    {
        public DateTime Timestamp { get; set; }
        public double Value { get; set; }
        public string Label { get; set; } = string.Empty;
    }

    public class Alert
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string SensorId { get; set; } = string.Empty;
        public string SensorName { get; set; } = string.Empty;
        public AlertType Type { get; set; }
        public AlertSeverity Severity { get; set; }
        public string Message { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public double Value { get; set; }
        public double Threshold { get; set; }
        public bool Acknowledged { get; set; }
    }

    public class HeatmapPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Intensity { get; set; }
    }

    public enum SensorType
    {
        Temperature,
        Humidity,
        AirQuality,
        Pressure,
        NoiseLevel,
        Radiation,
        WaterQuality
    }

    public enum SensorStatus
    {
        Active,
        Inactive,
        Maintenance,
        Error
    }

    public enum ReadingQuality
    {
        Good,
        Fair,
        Poor,
        Invalid
    }

    public enum AlertType
    {
        HighValue,
        LowValue,
        RapidChange,
        SensorOffline,
        DataQuality
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Critical
    }
}
```

---

## Step 2: Create Sensor Service

Create `Services/SensorService.cs`:

```csharp
using EnvironmentalMonitoring.Models;

namespace EnvironmentalMonitoring.Services
{
    public interface ISensorService
    {
        Task<List<Sensor>> GetSensorsAsync();
        Task<List<SensorReading>> GetReadingsAsync(string sensorId, DateTime start, DateTime end);
        Task<TimeSeriesData> GetTimeSeriesAsync(string sensorId, DateTime start, DateTime end);
        Task<List<HeatmapPoint>> GetHeatmapDataAsync(DateTime timestamp);
        Task<List<Alert>> GetAlertsAsync();
        Task<SensorReading> GetLatestReadingAsync(string sensorId);
    }

    public class SensorService : ISensorService
    {
        private readonly List<Sensor> _sensors;
        private readonly List<SensorReading> _readings;
        private readonly List<Alert> _alerts;
        private readonly Random _random = new(42);

        public SensorService()
        {
            _sensors = GenerateSensors();
            _readings = GenerateReadings();
            _alerts = GenerateAlerts();
        }

        public Task<List<Sensor>> GetSensorsAsync()
        {
            return Task.FromResult(_sensors);
        }

        public Task<List<SensorReading>> GetReadingsAsync(string sensorId, DateTime start, DateTime end)
        {
            var filtered = _readings
                .Where(r => r.SensorId == sensorId && r.Timestamp >= start && r.Timestamp <= end)
                .OrderBy(r => r.Timestamp)
                .ToList();
            return Task.FromResult(filtered);
        }

        public async Task<TimeSeriesData> GetTimeSeriesAsync(string sensorId, DateTime start, DateTime end)
        {
            var readings = await GetReadingsAsync(sensorId, start, end);
            var timeSeries = new TimeSeriesData
            {
                SensorId = sensorId,
                DataPoints = readings.Select(r => new DataPoint
                {
                    Timestamp = r.Timestamp,
                    Value = r.Value,
                    Label = r.Timestamp.ToString("HH:mm")
                }).ToList()
            };
            return timeSeries;
        }

        public Task<List<HeatmapPoint>> GetHeatmapDataAsync(DateTime timestamp)
        {
            // Get readings closest to the specified timestamp
            var targetTime = timestamp;
            var heatmapPoints = _sensors.Select(s =>
            {
                var reading = _readings
                    .Where(r => r.SensorId == s.Id)
                    .OrderBy(r => Math.Abs((r.Timestamp - targetTime).TotalSeconds))
                    .FirstOrDefault();

                return new HeatmapPoint
                {
                    Latitude = s.Latitude,
                    Longitude = s.Longitude,
                    Intensity = reading?.Value ?? 0
                };
            }).ToList();

            return Task.FromResult(heatmapPoints);
        }

        public Task<List<Alert>> GetAlertsAsync()
        {
            return Task.FromResult(_alerts.Where(a => !a.Acknowledged).OrderByDescending(a => a.Timestamp).ToList());
        }

        public Task<SensorReading> GetLatestReadingAsync(string sensorId)
        {
            var latest = _readings
                .Where(r => r.SensorId == sensorId)
                .OrderByDescending(r => r.Timestamp)
                .FirstOrDefault();
            return Task.FromResult(latest!);
        }

        private List<Sensor> GenerateSensors()
        {
            var sensors = new List<Sensor>();
            var locations = new[]
            {
                new { Name = "Downtown Station", Lat = 37.7749, Lon = -122.4194 },
                new { Name = "Industrial District", Lat = 37.7850, Lon = -122.4000 },
                new { Name = "Residential Area", Lat = 37.7650, Lon = -122.4300 },
                new { Name = "Park Sensor", Lat = 37.7700, Lon = -122.4100 },
                new { Name = "Waterfront", Lat = 37.7800, Lon = -122.3950 },
            };

            foreach (var loc in locations)
            {
                foreach (SensorType type in Enum.GetValues(typeof(SensorType)))
                {
                    if (_random.NextDouble() > 0.3) // 70% chance to have this sensor type
                    {
                        sensors.Add(new Sensor
                        {
                            Id = Guid.NewGuid().ToString(),
                            Name = $"{loc.Name} - {type}",
                            Type = type,
                            Status = SensorStatus.Active,
                            Latitude = loc.Lat + (_random.NextDouble() - 0.5) * 0.01,
                            Longitude = loc.Lon + (_random.NextDouble() - 0.5) * 0.01,
                            Location = loc.Name,
                            InstallationDate = DateTime.Now.AddMonths(-_random.Next(1, 24)),
                            LastReading = DateTime.Now.AddMinutes(-_random.Next(0, 60)),
                            CurrentValue = GenerateValue(type),
                            Unit = GetUnit(type)
                        });
                    }
                }
            }

            return sensors;
        }

        private List<SensorReading> GenerateReadings()
        {
            var readings = new List<SensorReading>();
            var now = DateTime.Now;

            foreach (var sensor in _sensors)
            {
                // Generate 24 hours of hourly data
                for (int hour = 0; hour < 24; hour++)
                {
                    var timestamp = now.AddHours(-24 + hour);
                    var baseValue = GenerateValue(sensor.Type);
                    var value = baseValue + (_random.NextDouble() - 0.5) * baseValue * 0.2;

                    readings.Add(new SensorReading
                    {
                        Id = Guid.NewGuid().ToString(),
                        SensorId = sensor.Id,
                        Timestamp = timestamp,
                        Value = value,
                        Unit = sensor.Unit,
                        Temperature = sensor.Type == SensorType.Temperature ? value : null,
                        Humidity = sensor.Type == SensorType.Humidity ? value : null,
                        AirQuality = sensor.Type == SensorType.AirQuality ? value : null,
                        Pressure = sensor.Type == SensorType.Pressure ? value : null,
                        Quality = _random.NextDouble() > 0.1 ? ReadingQuality.Good : ReadingQuality.Fair,
                        Latitude = sensor.Latitude,
                        Longitude = sensor.Longitude
                    });
                }
            }

            return readings;
        }

        private List<Alert> GenerateAlerts()
        {
            var alerts = new List<Alert>();
            var criticalSensors = _sensors.Take(3).ToList();

            foreach (var sensor in criticalSensors)
            {
                if (_random.NextDouble() > 0.5)
                {
                    alerts.Add(new Alert
                    {
                        SensorId = sensor.Id,
                        SensorName = sensor.Name,
                        Type = AlertType.HighValue,
                        Severity = AlertSeverity.Warning,
                        Message = $"High {sensor.Type} reading detected",
                        Timestamp = DateTime.Now.AddMinutes(-_random.Next(0, 120)),
                        Value = sensor.CurrentValue ?? 0,
                        Threshold = GetThreshold(sensor.Type),
                        Acknowledged = false
                    });
                }
            }

            return alerts;
        }

        private double GenerateValue(SensorType type)
        {
            return type switch
            {
                SensorType.Temperature => _random.Next(15, 35) + _random.NextDouble(),
                SensorType.Humidity => _random.Next(30, 80) + _random.NextDouble(),
                SensorType.AirQuality => _random.Next(20, 150) + _random.NextDouble(),
                SensorType.Pressure => _random.Next(980, 1030) + _random.NextDouble(),
                SensorType.NoiseLevel => _random.Next(40, 90) + _random.NextDouble(),
                SensorType.Radiation => _random.NextDouble() * 0.5,
                SensorType.WaterQuality => _random.Next(6, 9) + _random.NextDouble(),
                _ => 0
            };
        }

        private string GetUnit(SensorType type)
        {
            return type switch
            {
                SensorType.Temperature => "°C",
                SensorType.Humidity => "%",
                SensorType.AirQuality => "AQI",
                SensorType.Pressure => "hPa",
                SensorType.NoiseLevel => "dB",
                SensorType.Radiation => "μSv/h",
                SensorType.WaterQuality => "pH",
                _ => ""
            };
        }

        private double GetThreshold(SensorType type)
        {
            return type switch
            {
                SensorType.Temperature => 30,
                SensorType.Humidity => 70,
                SensorType.AirQuality => 100,
                SensorType.Pressure => 1020,
                SensorType.NoiseLevel => 80,
                SensorType.Radiation => 0.3,
                SensorType.WaterQuality => 8.5,
                _ => 0
            };
        }
    }
}
```

**Register in `Program.cs`:**
```csharp
builder.Services.AddScoped<ISensorService, SensorService>();
```

---

## Step 3: Build Dashboard Layout

Create `Pages/EnvironmentalDashboard.razor`:

```razor
@page "/environmental"
@using EnvironmentalMonitoring.Models
@using EnvironmentalMonitoring.Services
@using Honua.MapSDK.Components
@inject ISensorService SensorService
@inject ISnackbar Snackbar

<PageTitle>Environmental Monitoring Dashboard</PageTitle>

<MudContainer MaxWidth="MaxWidth.False" Class="pa-0" Style="height: 100vh; overflow: hidden;">
    <!-- Header -->
    <MudAppBar Elevation="4">
        <MudIcon Icon="@Icons.Material.Filled.Sensors" Size="Size.Large" Class="mr-3" />
        <MudText Typo="Typo.h5">Environmental Monitoring System</MudText>
        <MudSpacer />
        <MudChip Icon="@Icons.Material.Filled.Circle"
                 Color="@(_isLiveMode ? Color.Success : Color.Default)"
                 Size="Size.Small">
            @(_isLiveMode ? "LIVE" : "HISTORICAL")
        </MudChip>
        <MudIconButton Icon="@Icons.Material.Filled.Notifications"
                       Color="Color.Inherit"
                       Badge="@_activeAlerts.Count.ToString()"
                       BadgeColor="Color.Error"
                       OnClick="@(() => _showAlerts = !_showAlerts)" />
        <MudIconButton Icon="@Icons.Material.Filled.Settings" Color="Color.Inherit" />
    </MudAppBar>

    <!-- Alerts Panel -->
    @if (_showAlerts && _activeAlerts.Any())
    {
        <MudPaper Elevation="3" Class="pa-3 mx-3 mt-2">
            <MudText Typo="Typo.h6" Class="mb-2">
                <MudIcon Icon="@Icons.Material.Filled.Warning" Color="Color.Error" /> Active Alerts
            </MudText>
            @foreach (var alert in _activeAlerts.Take(5))
            {
                <MudAlert Severity="@GetAlertSeverity(alert.Severity)" Class="mb-2">
                    <strong>@alert.SensorName:</strong> @alert.Message (@alert.Timestamp.ToString("HH:mm"))
                </MudAlert>
            }
        </MudPaper>
    }

    <!-- Main Content -->
    <MudGrid Class="pa-3" Style="height: calc(100vh - 140px);">
        <!-- Left Panel: Map & Heatmap -->
        <MudItem xs="12" md="8" Style="height: 100%;">
            <MudStack Spacing="2" Style="height: 100%;">
                <!-- Map -->
                <MudPaper Elevation="3" Style="height: 65%; position: relative;">
                    <HonuaMap @ref="_map"
                              Id="env-map"
                              Center="@(new[] { -122.4194, 37.7749 })"
                              Zoom="12"
                              MapStyle="https://demotiles.maplibre.org/style.json"
                              OnMapReady="@HandleMapReady"
                              Style="height: 100%;" />

                    <!-- Map Controls -->
                    <div style="position: absolute; top: 10px; right: 10px; z-index: 1000;">
                        <MudButtonGroup OverrideStyles="false">
                            <MudButton Variant="@(_showHeatmap ? Variant.Filled : Variant.Outlined)"
                                       Color="Color.Primary"
                                       Size="Size.Small"
                                       OnClick="@ToggleHeatmap">
                                🌡️ Heatmap
                            </MudButton>
                            <MudButton Variant="@(_showSensors ? Variant.Filled : Variant.Outlined)"
                                       Color="Color.Primary"
                                       Size="Size.Small"
                                       OnClick="@ToggleSensors">
                                📍 Sensors
                            </MudButton>
                        </MudButtonGroup>
                    </div>
                </MudPaper>

                <!-- Timeline Component will be added here -->
                <MudPaper Elevation="3" Style="height: 33%; padding: 16px;">
                    <MudText Typo="Typo.h6">Timeline Controls</MudText>
                </MudPaper>
            </MudStack>
        </MudItem>

        <!-- Right Panel: Charts & Data -->
        <MudItem xs="12" md="4" Style="height: 100%;">
            <MudStack Spacing="2" Style="height: 100%;">
                <!-- Chart Component will be added here -->
                <MudPaper Elevation="3" Style="height: 50%; padding: 16px;">
                    <MudText Typo="Typo.h6">Time Series Chart</MudText>
                </MudPaper>

                <!-- Data Grid Component will be added here -->
                <MudPaper Elevation="3" Style="height: 48%; padding: 16px;">
                    <MudText Typo="Typo.h6">Sensor Readings</MudText>
                </MudPaper>
            </MudStack>
        </MudItem>
    </MudGrid>
</MudContainer>

@code {
    private HonuaMap? _map;
    private List<Sensor> _sensors = new();
    private List<Alert> _activeAlerts = new();
    private bool _showAlerts = false;
    private bool _isLiveMode = true;
    private bool _showHeatmap = true;
    private bool _showSensors = true;

    protected override async Task OnInitializedAsync()
    {
        _sensors = await SensorService.GetSensorsAsync();
        _activeAlerts = await SensorService.GetAlertsAsync();
    }

    private async Task HandleMapReady(MapReadyMessage message)
    {
        Console.WriteLine("Map ready");
        await RenderSensorsOnMap();
    }

    private async Task RenderSensorsOnMap()
    {
        if (_map == null) return;

        // Implementation will be added in Step 6
    }

    private void ToggleHeatmap()
    {
        _showHeatmap = !_showHeatmap;
    }

    private void ToggleSensors()
    {
        _showSensors = !_showSensors;
    }

    private Severity GetAlertSeverity(AlertSeverity severity)
    {
        return severity switch
        {
            AlertSeverity.Critical => Severity.Error,
            AlertSeverity.Warning => Severity.Warning,
            _ => Severity.Info
        };
    }
}
```

---

## Step 4: Add Timeline Component

Replace the timeline placeholder:

```razor
<MudPaper Elevation="3" Style="height: 33%;">
    <HonuaTimeline @ref="_timeline"
                   Id="env-timeline"
                   StartDate="@_timelineStart"
                   EndDate="@_timelineEnd"
                   CurrentDate="@_currentTime"
                   PlaybackSpeed="1000"
                   ShowPlayControls="true"
                   ShowDatePicker="true"
                   OnTimeChanged="@HandleTimeChanged"
                   OnPlayStateChanged="@HandlePlayStateChanged">
        <TimelineMarkers>
            @foreach (var alert in _activeAlerts)
            {
                <TimelineMarker Time="@alert.Timestamp"
                                Color="@GetMarkerColor(alert.Severity)"
                                Label="@alert.SensorName"
                                Icon="@Icons.Material.Filled.Warning" />
            }
        </TimelineMarkers>
    </HonuaTimeline>
</MudPaper>
```

```csharp
@code {
    private HonuaTimeline? _timeline;
    private DateTime _timelineStart = DateTime.Now.AddDays(-1);
    private DateTime _timelineEnd = DateTime.Now;
    private DateTime _currentTime = DateTime.Now;

    private async Task HandleTimeChanged(TimeChangedMessage message)
    {
        _currentTime = message.Timestamp;
        _isLiveMode = false;

        // Update heatmap for selected time
        if (_showHeatmap)
        {
            await UpdateHeatmap(_currentTime);
        }

        // Update charts
        await UpdateCharts(_currentTime);
    }

    private void HandlePlayStateChanged(PlayStateChangedMessage message)
    {
        Console.WriteLine($"Timeline {(message.IsPlaying ? "playing" : "paused")}");
    }

    private string GetMarkerColor(AlertSeverity severity)
    {
        return severity switch
        {
            AlertSeverity.Critical => "#f44336",
            AlertSeverity.Warning => "#ff9800",
            _ => "#2196f3"
        };
    }

    private async Task UpdateHeatmap(DateTime timestamp)
    {
        var heatmapData = await SensorService.GetHeatmapDataAsync(timestamp);
        // Update map heatmap layer
        if (_map != null)
        {
            await _map.UpdateHeatmapAsync("sensor-heatmap", heatmapData);
        }
    }

    private async Task UpdateCharts(DateTime timestamp)
    {
        // Update chart data based on timestamp
        // Implementation in Step 5
        await Task.CompletedTask;
    }
}
```

---

## Step 5: Add Chart Component

Replace the chart placeholder:

```razor
<MudPaper Elevation="3" Style="height: 50%;">
    <HonuaChart @ref="_chart"
                Id="env-chart"
                Type="ChartType.Line"
                Title="Temperature Over Time"
                SyncWith="env-map"
                Height="100%"
                ShowLegend="true"
                EnableZoom="true"
                OnPointClick="@HandleChartPointClick">
        <ChartData>
            @foreach (var sensor in _selectedSensors)
            {
                <DataSeries Label="@sensor.Name"
                            Data="@GetSensorData(sensor.Id)"
                            Color="@GetSensorColor(sensor.Type)"
                            BorderWidth="2" />
            }
        </ChartData>
    </HonuaChart>

    <!-- Sensor Selection -->
    <MudPaper Elevation="1" Class="pa-2">
        <MudSelect T="Sensor"
                   Label="Select Sensors"
                   MultiSelection="true"
                   @bind-SelectedValues="_selectedSensors"
                   Variant="Variant.Outlined"
                   Dense="true">
            @foreach (var sensor in _sensors.Where(s => s.Type == SensorType.Temperature))
            {
                <MudSelectItem Value="@sensor">@sensor.Name</MudSelectItem>
            }
        </MudSelect>
    </MudPaper>
</MudPaper>
```

```csharp
@code {
    private HonuaChart? _chart;
    private IEnumerable<Sensor> _selectedSensors = Enumerable.Empty<Sensor>();
    private Dictionary<string, TimeSeriesData> _sensorDataCache = new();

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // Select first 3 temperature sensors by default
        _selectedSensors = _sensors
            .Where(s => s.Type == SensorType.Temperature)
            .Take(3)
            .ToList();

        // Load time series data
        foreach (var sensor in _selectedSensors)
        {
            var data = await SensorService.GetTimeSeriesAsync(
                sensor.Id,
                _timelineStart,
                _timelineEnd);
            _sensorDataCache[sensor.Id] = data;
        }
    }

    private List<DataPoint> GetSensorData(string sensorId)
    {
        return _sensorDataCache.TryGetValue(sensorId, out var data)
            ? data.DataPoints
            : new List<DataPoint>();
    }

    private string GetSensorColor(SensorType type)
    {
        return type switch
        {
            SensorType.Temperature => "#f44336",
            SensorType.Humidity => "#2196f3",
            SensorType.AirQuality => "#4caf50",
            SensorType.Pressure => "#ff9800",
            _ => "#9c27b0"
        };
    }

    private async Task HandleChartPointClick(ChartPointClickedMessage message)
    {
        // Jump to time on timeline
        if (_timeline != null)
        {
            await _timeline.SeekToAsync(message.Timestamp);
        }
    }
}
```

---

## Step 6: Add Heatmap Visualization

Update the `RenderSensorsOnMap` method:

```csharp
private async Task RenderSensorsOnMap()
{
    if (_map == null) return;

    // Add sensor points
    if (_showSensors)
    {
        var sensorsGeoJson = new
        {
            type = "FeatureCollection",
            features = _sensors.Select(s => new
            {
                type = "Feature",
                geometry = new
                {
                    type = "Point",
                    coordinates = new[] { s.Longitude, s.Latitude }
                },
                properties = new
                {
                    id = s.Id,
                    name = s.Name,
                    type = s.Type.ToString(),
                    status = s.Status.ToString(),
                    value = s.CurrentValue,
                    unit = s.Unit
                }
            })
        };

        await _map.AddSourceAsync("sensors", new
        {
            type = "geojson",
            data = sensorsGeoJson
        });

        await _map.AddLayerAsync(new
        {
            id = "sensor-points",
            type = "circle",
            source = "sensors",
            paint = new
            {
                circle_radius = 8,
                circle_color = new object[] { "match", new[] { "get", "status" },
                    "Active", "#4caf50",
                    "Inactive", "#9e9e9e",
                    "Maintenance", "#ff9800",
                    "Error", "#f44336",
                    "#2196f3"
                },
                circle_stroke_width = 2,
                circle_stroke_color = "#ffffff"
            }
        });
    }

    // Add heatmap layer
    if (_showHeatmap)
    {
        var heatmapData = await SensorService.GetHeatmapDataAsync(_currentTime);

        await _map.AddSourceAsync("sensor-heatmap", new
        {
            type = "geojson",
            data = new
            {
                type = "FeatureCollection",
                features = heatmapData.Select(h => new
                {
                    type = "Feature",
                    geometry = new
                    {
                        type = "Point",
                        coordinates = new[] { h.Longitude, h.Latitude }
                    },
                    properties = new
                    {
                        intensity = h.Intensity
                    }
                })
            }
        });

        await _map.AddLayerAsync(new
        {
            id = "heatmap-layer",
            type = "heatmap",
            source = "sensor-heatmap",
            paint = new
            {
                heatmap_weight = new object[] { "interpolate", new[] { "linear" }, new[] { "get", "intensity" },
                    0, 0,
                    100, 1
                },
                heatmap_intensity = new object[] { "interpolate", new[] { "linear" }, new[] { "zoom" },
                    0, 1,
                    15, 3
                },
                heatmap_color = new object[] { "interpolate", new[] { "linear" }, new[] { "heatmap-density" },
                    0, "rgba(33,102,172,0)",
                    0.2, "rgb(103,169,207)",
                    0.4, "rgb(209,229,240)",
                    0.6, "rgb(253,219,199)",
                    0.8, "rgb(239,138,98)",
                    1, "rgb(178,24,43)"
                },
                heatmap_radius = new object[] { "interpolate", new[] { "linear" }, new[] { "zoom" },
                    0, 2,
                    15, 20
                },
                heatmap_opacity = 0.7
            }
        });
    }
}
```

---

## Step 7: Add Data Grid

Replace the data grid placeholder:

```razor
<MudPaper Elevation="3" Style="height: 48%; overflow: hidden;">
    <HonuaDataGrid TItem="SensorReading"
                   Items="@_currentReadings"
                   SyncWith="env-map"
                   Dense="true"
                   Height="100%"
                   ShowSearch="true"
                   ShowExport="true"
                   PageSize="10">
        <Columns>
            <PropertyColumn Property="r => r.Timestamp" Title="Time" Format="HH:mm:ss" />
            <PropertyColumn Property="r => r.Value" Title="Value" Format="F2" />
            <PropertyColumn Property="r => r.Unit" Title="Unit" />
            <PropertyColumn Property="r => r.Quality" Title="Quality">
                <CellTemplate>
                    <MudChip Size="Size.Small" Color="@GetQualityColor(context.Quality)">
                        @context.Quality
                    </MudChip>
                </CellTemplate>
            </PropertyColumn>
        </Columns>
    </HonuaDataGrid>
</MudPaper>
```

```csharp
@code {
    private List<SensorReading> _currentReadings = new();

    private async Task LoadCurrentReadings()
    {
        var tasks = _selectedSensors.Select(s =>
            SensorService.GetLatestReadingAsync(s.Id));
        var readings = await Task.WhenAll(tasks);
        _currentReadings = readings.Where(r => r != null).ToList();
    }

    private Color GetQualityColor(ReadingQuality quality)
    {
        return quality switch
        {
            ReadingQuality.Good => Color.Success,
            ReadingQuality.Fair => Color.Warning,
            ReadingQuality.Poor => Color.Error,
            _ => Color.Default
        };
    }
}
```

---

## Step 8: Implement Real-time Updates

Add SignalR for real-time updates:

```csharp
@using Microsoft.AspNetCore.SignalR.Client
@implements IAsyncDisposable

@code {
    private HubConnection? _hubConnection;

    protected override async Task OnInitializedAsync()
    {
        await base.OnInitializedAsync();

        // Setup SignalR connection
        _hubConnection = new HubConnectionBuilder()
            .WithUrl(NavigationManager.ToAbsoluteUri("/sensorHub"))
            .WithAutomaticReconnect()
            .Build();

        _hubConnection.On<SensorReading>("ReceiveReading", async (reading) =>
        {
            await HandleNewReading(reading);
        });

        _hubConnection.On<Alert>("ReceiveAlert", async (alert) =>
        {
            await HandleNewAlert(alert);
        });

        await _hubConnection.StartAsync();
    }

    private async Task HandleNewReading(SensorReading reading)
    {
        // Update sensor on map
        var sensor = _sensors.FirstOrDefault(s => s.Id == reading.SensorId);
        if (sensor != null)
        {
            sensor.CurrentValue = reading.Value;
            sensor.LastReading = reading.Timestamp;
        }

        // Add to current readings if sensor is selected
        if (_selectedSensors.Any(s => s.Id == reading.SensorId))
        {
            _currentReadings.Insert(0, reading);
            if (_currentReadings.Count > 100)
                _currentReadings.RemoveAt(_currentReadings.Count - 1);
        }

        // Update heatmap if in live mode
        if (_isLiveMode && _showHeatmap)
        {
            await UpdateHeatmap(DateTime.Now);
        }

        await InvokeAsync(StateHasChanged);
    }

    private async Task HandleNewAlert(Alert alert)
    {
        _activeAlerts.Insert(0, alert);
        _showAlerts = true;

        // Show snackbar notification
        Snackbar.Add(
            $"⚠️ {alert.SensorName}: {alert.Message}",
            GetAlertSeverity(alert.Severity),
            config =>
            {
                config.VisibleStateDuration = 5000;
                config.ShowCloseIcon = true;
            });

        await InvokeAsync(StateHasChanged);
    }

    public async ValueTask DisposeAsync()
    {
        if (_hubConnection != null)
        {
            await _hubConnection.DisposeAsync();
        }
    }
}
```

**Create `Hubs/SensorHub.cs`:**

```csharp
using Microsoft.AspNetCore.SignalR;
using EnvironmentalMonitoring.Models;

namespace EnvironmentalMonitoring.Hubs
{
    public class SensorHub : Hub
    {
        public async Task SendReading(SensorReading reading)
        {
            await Clients.All.SendAsync("ReceiveReading", reading);
        }

        public async Task SendAlert(Alert alert)
        {
            await Clients.All.SendAsync("ReceiveAlert", alert);
        }
    }
}
```

**Register in `Program.cs`:**
```csharp
app.MapHub<SensorHub>("/sensorHub");
```

---

## Step 9: Add Alert System

Add alert management UI:

```razor
<!-- Add Alert Management Dialog -->
<MudDialog @bind-IsVisible="_showAlertDialog">
    <TitleContent>
        <MudText Typo="Typo.h6">Configure Alerts</MudText>
    </TitleContent>
    <DialogContent>
        <MudSelect @bind-Value="_alertSensorType" Label="Sensor Type" Variant="Variant.Outlined">
            @foreach (SensorType type in Enum.GetValues(typeof(SensorType)))
            {
                <MudSelectItem Value="@type">@type</MudSelectItem>
            }
        </MudSelect>
        <MudNumericField @bind-Value="_alertThreshold" Label="Threshold Value" Variant="Variant.Outlined" Class="mt-3" />
        <MudSelect @bind-Value="_alertType" Label="Alert Type" Variant="Variant.Outlined" Class="mt-3">
            @foreach (AlertType type in Enum.GetValues(typeof(AlertType)))
            {
                <MudSelectItem Value="@type">@type</MudSelectItem>
            }
        </MudSelect>
        <MudSelect @bind-Value="_alertSeverity" Label="Severity" Variant="Variant.Outlined" Class="mt-3">
            @foreach (AlertSeverity severity in Enum.GetValues(typeof(AlertSeverity)))
            {
                <MudSelectItem Value="@severity">@severity</MudSelectItem>
            }
        </MudSelect>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@(() => _showAlertDialog = false)">Cancel</MudButton>
        <MudButton Color="Color.Primary" Variant="Variant.Filled" OnClick="@SaveAlertConfiguration">
            Save Alert
        </MudButton>
    </DialogActions>
</MudDialog>
```

```csharp
@code {
    private bool _showAlertDialog = false;
    private SensorType _alertSensorType;
    private double _alertThreshold;
    private AlertType _alertType;
    private AlertSeverity _alertSeverity;

    private void SaveAlertConfiguration()
    {
        // Save alert configuration
        Snackbar.Add($"Alert configured for {_alertSensorType} threshold {_alertThreshold}", Severity.Success);
        _showAlertDialog = false;
    }
}
```

---

## What You Learned

✅ **Time-series data** visualization with charts
✅ **Timeline component** with playback controls
✅ **Heatmap visualization** for spatial data
✅ **Real-time updates** with SignalR
✅ **Alert system** with threshold monitoring
✅ **Data quality** tracking and display
✅ **Multi-sensor** management and comparison

---

## Next Steps

- 📖 [Tutorial 04: Fleet Tracking](Tutorial_04_FleetTracking.md)
- 📖 [Performance Optimization Guide](../guides/PerformanceOptimization.md)
- 📖 [Real-time Data Patterns](../guides/realtime-patterns.md)

---

**Congratulations!** You've built a sophisticated environmental monitoring system!

*Last Updated: 2025-11-06*
*MapSDK Version: 1.0.0*
