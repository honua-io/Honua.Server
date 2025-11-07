# HonuaTimeline Examples

Comprehensive examples demonstrating various use cases and features of the HonuaTimeline component.

## Table of Contents

1. [Basic Timeline](#basic-timeline)
2. [Vehicle Tracking](#vehicle-tracking)
3. [Weather Animation](#weather-animation)
4. [Historical Imagery Comparison](#historical-imagery-comparison)
5. [Sensor Data Replay](#sensor-data-replay)
6. [Event Timeline](#event-timeline)
7. [Multi-Layer Temporal Sync](#multi-layer-temporal-sync)
8. [Custom Time Steps](#custom-time-steps)
9. [Bookmarks and Annotations](#bookmarks-and-annotations)
10. [Real-Time Data Streaming](#real-time-data-streaming)

---

## Basic Timeline

Simple timeline for replaying 24 hours of data:

```razor
@page "/timeline-basic"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Timeline

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudPaper Elevation="2" Class="pa-4">
        <MudText Typo="Typo.h4" Class="mb-4">Basic Timeline</MudText>

        <div style="height: 600px;">
            <HonuaMap Id="basic-map"
                      Center="@(new[] { -122.4194, 37.7749 })"
                      Zoom="12" />
        </div>

        <HonuaTimeline SyncWith="basic-map"
                       TimeField="timestamp"
                       StartTime="@startTime"
                       EndTime="@endTime"
                       Class="mt-4" />
    </MudPaper>
</MudContainer>

@code {
    private DateTime startTime = DateTime.UtcNow.AddHours(-24);
    private DateTime endTime = DateTime.UtcNow;
}
```

---

## Vehicle Tracking

Track vehicle movements over time with playback controls:

```razor
@page "/timeline-vehicle"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Timeline
@inject ComponentBus Bus

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudPaper Elevation="2" Class="pa-4">
        <MudText Typo="Typo.h4" Class="mb-2">Vehicle Tracking</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mb-4">
            Track: @currentVehicle | Speed: @currentSpeed mph | Location: @currentLocation
        </MudText>

        <div style="height: 600px;">
            <HonuaMap Id="vehicle-map"
                      Center="@vehicleCenter"
                      Zoom="14"
                      OnMapReady="OnMapReady" />
        </div>

        <HonuaTimeline @ref="timeline"
                       SyncWith="vehicle-map"
                       TimeField="gps_timestamp"
                       StartTime="@tripStart"
                       EndTime="@tripEnd"
                       TimeFormat="HH:mm:ss"
                       PlaybackSpeed="500"
                       SpeedMultiplier="2.0"
                       ShowStepButtons="true"
                       ShowJumpButtons="true"
                       Loop="false"
                       OnTimeChanged="OnTimeChanged"
                       Class="mt-4" />

        <MudStack Row="true" Class="mt-4" Spacing="2">
            <MudButton Variant="Variant.Filled"
                       Color="Color.Primary"
                       OnClick="@(() => timeline.Play())"
                       StartIcon="@Icons.Material.Filled.PlayArrow">
                Play
            </MudButton>
            <MudButton Variant="Variant.Filled"
                       Color="Color.Secondary"
                       OnClick="@(() => timeline.Pause())"
                       StartIcon="@Icons.Material.Filled.Pause">
                Pause
            </MudButton>
            <MudButton Variant="Variant.Outlined"
                       OnClick="@(() => timeline.JumpToStart())"
                       StartIcon="@Icons.Material.Filled.SkipPrevious">
                Reset
            </MudButton>
        </MudStack>
    </MudPaper>
</MudContainer>

@code {
    private HonuaTimeline timeline;
    private DateTime tripStart = new DateTime(2024, 11, 6, 8, 0, 0);
    private DateTime tripEnd = new DateTime(2024, 11, 6, 18, 30, 0);
    private double[] vehicleCenter = new[] { -122.4194, 37.7749 };

    private string currentVehicle = "Vehicle 001";
    private double currentSpeed = 0;
    private string currentLocation = "San Francisco, CA";

    private async Task OnMapReady(MapReadyMessage message)
    {
        // Load vehicle tracking data
        await LoadVehicleData();
    }

    private async Task OnTimeChanged(TimeChangedMessage message)
    {
        // Update vehicle info based on current time
        var vehicleData = await GetVehicleDataAtTime(message.CurrentTime);
        currentSpeed = vehicleData.Speed;
        currentLocation = vehicleData.Location;
        StateHasChanged();
    }

    private async Task LoadVehicleData()
    {
        // Load GeoJSON with vehicle tracking points
        // Each point has gps_timestamp, speed, and location properties
        await Task.CompletedTask;
    }

    private async Task<VehicleData> GetVehicleDataAtTime(DateTime time)
    {
        // Fetch vehicle data for specific time
        return new VehicleData
        {
            Speed = 35.5,
            Location = "Market St & 4th St"
        };
    }

    private class VehicleData
    {
        public double Speed { get; set; }
        public string Location { get; set; }
    }
}
```

---

## Weather Animation

Animate weather forecast data over 7 days:

```razor
@page "/timeline-weather"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Timeline
@using Honua.MapSDK.Components.Legend

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudPaper Elevation="2" Class="pa-4">
        <MudText Typo="Typo.h4" Class="mb-2">Weather Forecast Animation</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mb-4">
            Temperature: @currentTemp°F | Conditions: @currentConditions
        </MudText>

        <div style="height: 600px; position: relative;">
            <HonuaMap Id="weather-map"
                      Center="@(new[] { -95.7129, 37.0902 })"
                      Zoom="4" />

            <HonuaLegend SyncWith="weather-map"
                         Title="Temperature (°F)"
                         Position="TopRight" />
        </div>

        <HonuaTimeline SyncWith="weather-map"
                       TimeField="forecast_time"
                       StartTime="@forecastStart"
                       EndTime="@forecastEnd"
                       TimeFormat="MMM dd, HH:mm"
                       StepUnit="TimeStepUnit.Hours"
                       TotalStepsOverride="168"
                       PlaybackSpeed="800"
                       Loop="true"
                       AutoPlay="true"
                       OnTimeChanged="OnWeatherTimeChanged"
                       Class="mt-4" />

        <MudGrid Class="mt-4">
            <MudItem xs="12" md="3">
                <MudCard>
                    <MudCardContent>
                        <MudText Typo="Typo.h6">High</MudText>
                        <MudText Typo="Typo.h3" Color="Color.Error">@highTemp°F</MudText>
                    </MudCardContent>
                </MudCard>
            </MudItem>
            <MudItem xs="12" md="3">
                <MudCard>
                    <MudCardContent>
                        <MudText Typo="Typo.h6">Low</MudText>
                        <MudText Typo="Typo.h3" Color="Color.Info">@lowTemp°F</MudText>
                    </MudCardContent>
                </MudCard>
            </MudItem>
            <MudItem xs="12" md="3">
                <MudCard>
                    <MudCardContent>
                        <MudText Typo="Typo.h6">Wind</MudText>
                        <MudText Typo="Typo.h3">@windSpeed mph</MudText>
                    </MudCardContent>
                </MudCard>
            </MudItem>
            <MudItem xs="12" md="3">
                <MudCard>
                    <MudCardContent>
                        <MudText Typo="Typo.h6">Humidity</MudText>
                        <MudText Typo="Typo.h3">@humidity%</MudText>
                    </MudCardContent>
                </MudCard>
            </MudItem>
        </MudGrid>
    </MudPaper>
</MudContainer>

@code {
    private DateTime forecastStart = DateTime.UtcNow;
    private DateTime forecastEnd = DateTime.UtcNow.AddDays(7);

    private double currentTemp = 72;
    private string currentConditions = "Partly Cloudy";
    private double highTemp = 78;
    private double lowTemp = 65;
    private double windSpeed = 12;
    private double humidity = 65;

    private async Task OnWeatherTimeChanged(TimeChangedMessage message)
    {
        var weatherData = await GetWeatherDataAtTime(message.CurrentTime);
        currentTemp = weatherData.Temperature;
        currentConditions = weatherData.Conditions;
        highTemp = weatherData.High;
        lowTemp = weatherData.Low;
        windSpeed = weatherData.WindSpeed;
        humidity = weatherData.Humidity;
        StateHasChanged();
    }

    private async Task<WeatherData> GetWeatherDataAtTime(DateTime time)
    {
        // Fetch weather data for specific forecast time
        return new WeatherData
        {
            Temperature = 72 + Random.Shared.Next(-10, 10),
            Conditions = "Partly Cloudy",
            High = 78,
            Low = 65,
            WindSpeed = 12,
            Humidity = 65
        };
    }

    private class WeatherData
    {
        public double Temperature { get; set; }
        public string Conditions { get; set; }
        public double High { get; set; }
        public double Low { get; set; }
        public double WindSpeed { get; set; }
        public double Humidity { get; set; }
    }
}
```

---

## Historical Imagery Comparison

Compare satellite imagery from different years:

```razor
@page "/timeline-imagery"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Timeline

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudPaper Elevation="2" Class="pa-4">
        <MudText Typo="Typo.h4" Class="mb-2">Historical Imagery</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mb-4">
            Viewing imagery from: @currentImageryDate.ToString("MMMM yyyy")
        </MudText>

        <div style="height: 600px;">
            <HonuaMap Id="imagery-map"
                      Center="@(new[] { -80.1918, 25.7617 })"
                      Zoom="11"
                      MapStyle="@GetImageryStyle()" />
        </div>

        <HonuaTimeline SyncWith="imagery-map"
                       TimeSteps="@imageryDates"
                       TimeFormat="MMMM yyyy"
                       ShowStepButtons="true"
                       ShowJumpButtons="true"
                       Loop="false"
                       PlaybackSpeed="2000"
                       OnTimeChanged="OnImageryTimeChanged"
                       ShowBookmarks="true"
                       Bookmarks="@imageryBookmarks"
                       Class="mt-4" />

        <MudText Typo="Typo.body2" Class="mt-4">
            <strong>Note:</strong> Use step buttons to compare changes over time.
            Bookmarks indicate significant events (hurricanes, development).
        </MudText>
    </MudPaper>
</MudContainer>

@code {
    private List<DateTime> imageryDates = new()
    {
        new DateTime(2015, 1, 1),
        new DateTime(2016, 1, 1),
        new DateTime(2017, 1, 1),
        new DateTime(2017, 9, 15), // Post-Hurricane Irma
        new DateTime(2018, 1, 1),
        new DateTime(2019, 1, 1),
        new DateTime(2020, 1, 1),
        new DateTime(2021, 1, 1),
        new DateTime(2022, 1, 1),
        new DateTime(2023, 1, 1),
        new DateTime(2024, 1, 1)
    };

    private List<TimeBookmark> imageryBookmarks = new()
    {
        new TimeBookmark
        {
            Time = new DateTime(2017, 9, 15),
            Label = "Hurricane Irma",
            Description = "Category 5 hurricane impact",
            Color = "#ef4444"
        },
        new TimeBookmark
        {
            Time = new DateTime(2020, 1, 1),
            Label = "Development Boom",
            Description = "Major infrastructure projects",
            Color = "#3b82f6"
        }
    };

    private DateTime currentImageryDate = new DateTime(2024, 1, 1);

    private async Task OnImageryTimeChanged(TimeChangedMessage message)
    {
        currentImageryDate = message.CurrentTime;
        StateHasChanged();
    }

    private string GetImageryStyle()
    {
        // Return different map styles based on current imagery date
        // In production, this would load different tile layers
        return "https://demotiles.maplibre.org/style.json";
    }
}
```

---

## Sensor Data Replay

Replay 24 hours of IoT sensor readings:

```razor
@page "/timeline-sensors"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Timeline
@using Honua.MapSDK.Components.Chart

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudPaper Elevation="2" Class="pa-4">
        <MudText Typo="Typo.h4" Class="mb-4">Sensor Data Replay</MudText>

        <MudGrid>
            <MudItem xs="12" md="8">
                <div style="height: 500px;">
                    <HonuaMap Id="sensor-map"
                              Center="@(new[] { -122.3321, 47.6062 })"
                              Zoom="13" />
                </div>
            </MudItem>
            <MudItem xs="12" md="4">
                <HonuaChart SyncWith="sensor-map"
                            Type="ChartType.Line"
                            Field="temperature"
                            TimeField="reading_time"
                            Title="Temperature Over Time"
                            Style="height: 500px;" />
            </MudItem>
        </MudGrid>

        <HonuaTimeline SyncWith="sensor-map"
                       TimeField="reading_time"
                       StartTime="@sensorStart"
                       EndTime="@sensorEnd"
                       TimeFormat="HH:mm:ss"
                       PlaybackSpeed="100"
                       SpeedMultiplier="4.0"
                       Loop="true"
                       OnTimeChanged="OnSensorTimeChanged"
                       Class="mt-4" />

        <MudGrid Class="mt-4">
            <MudItem xs="6" md="3">
                <MudCard>
                    <MudCardContent>
                        <MudText Typo="Typo.body2">Temperature</MudText>
                        <MudText Typo="Typo.h4">@currentSensorData.Temperature°F</MudText>
                    </MudCardContent>
                </MudCard>
            </MudItem>
            <MudItem xs="6" md="3">
                <MudCard>
                    <MudCardContent>
                        <MudText Typo="Typo.body2">Humidity</MudText>
                        <MudText Typo="Typo.h4">@currentSensorData.Humidity%</MudText>
                    </MudCardContent>
                </MudCard>
            </MudItem>
            <MudItem xs="6" md="3">
                <MudCard>
                    <MudCardContent>
                        <MudText Typo="Typo.body2">Air Quality</MudText>
                        <MudText Typo="Typo.h4">@currentSensorData.AirQuality</MudText>
                    </MudCardContent>
                </MudCard>
            </MudItem>
            <MudItem xs="6" md="3">
                <MudCard>
                    <MudCardContent>
                        <MudText Typo="Typo.body2">Active Sensors</MudText>
                        <MudText Typo="Typo.h4">@activeSensorCount</MudText>
                    </MudCardContent>
                </MudCard>
            </MudItem>
        </MudGrid>
    </MudPaper>
</MudContainer>

@code {
    private DateTime sensorStart = DateTime.UtcNow.AddHours(-24);
    private DateTime sensorEnd = DateTime.UtcNow;
    private int activeSensorCount = 15;

    private SensorData currentSensorData = new()
    {
        Temperature = 72.5,
        Humidity = 65,
        AirQuality = 45
    };

    private async Task OnSensorTimeChanged(TimeChangedMessage message)
    {
        currentSensorData = await GetSensorDataAtTime(message.CurrentTime);
        StateHasChanged();
    }

    private async Task<SensorData> GetSensorDataAtTime(DateTime time)
    {
        // Simulate fetching sensor data
        return new SensorData
        {
            Temperature = 70 + Random.Shared.NextDouble() * 15,
            Humidity = 50 + Random.Shared.Next(0, 30),
            AirQuality = Random.Shared.Next(0, 100)
        };
    }

    private class SensorData
    {
        public double Temperature { get; set; }
        public int Humidity { get; set; }
        public int AirQuality { get; set; }
    }
}
```

---

## Event Timeline

Display events occurring at specific times:

```razor
@page "/timeline-events"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Timeline

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudPaper Elevation="2" Class="pa-4">
        <MudText Typo="Typo.h4" Class="mb-4">Emergency Response Timeline</MudText>

        <div style="height: 600px;">
            <HonuaMap Id="event-map"
                      Center="@(new[] { -118.2437, 34.0522 })"
                      Zoom="12" />
        </div>

        <HonuaTimeline SyncWith="event-map"
                       TimeSteps="@eventTimes"
                       TimeFormat="HH:mm:ss"
                       ShowBookmarks="true"
                       Bookmarks="@eventBookmarks"
                       OnTimeChanged="OnEventTimeChanged"
                       PlaybackSpeed="1500"
                       Class="mt-4" />

        @if (currentEvent != null)
        {
            <MudAlert Severity="@GetEventSeverity(currentEvent.Type)" Class="mt-4">
                <MudText Typo="Typo.h6">@currentEvent.Title</MudText>
                <MudText Typo="Typo.body2">@currentEvent.Description</MudText>
                <MudText Typo="Typo.caption">@currentEvent.Time.ToString("HH:mm:ss")</MudText>
            </MudAlert>
        }

        <MudTimeline Class="mt-4">
            @foreach (var evt in events.OrderBy(e => e.Time))
            {
                <MudTimelineItem Color="@GetTimelineColor(evt.Type)">
                    <MudText Typo="Typo.body1"><strong>@evt.Time.ToString("HH:mm")</strong></MudText>
                    <MudText Typo="Typo.body2">@evt.Title</MudText>
                </MudTimelineItem>
            }
        </MudTimeline>
    </MudPaper>
</MudContainer>

@code {
    private List<EmergencyEvent> events = new()
    {
        new() { Time = DateTime.Today.AddHours(8).AddMinutes(12), Type = EventType.Call, Title = "911 Call Received", Description = "Report of structure fire at 123 Main St" },
        new() { Time = DateTime.Today.AddHours(8).AddMinutes(15), Type = EventType.Dispatch, Title = "Units Dispatched", Description = "Engine 1, Engine 2, Ladder 1 en route" },
        new() { Time = DateTime.Today.AddHours(8).AddMinutes(20), Type = EventType.Arrival, Title = "First Unit Arrival", Description = "Engine 1 on scene" },
        new() { Time = DateTime.Today.AddHours(8).AddMinutes(35), Type = EventType.Update, Title = "Fire Under Control", Description = "All clear signal" },
        new() { Time = DateTime.Today.AddHours(9).AddMinutes(10), Type = EventType.Cleared, Title = "Scene Cleared", Description = "Units returning to station" }
    };

    private List<DateTime> eventTimes;
    private List<TimeBookmark> eventBookmarks;
    private EmergencyEvent currentEvent;

    protected override void OnInitialized()
    {
        eventTimes = events.Select(e => e.Time).ToList();
        eventBookmarks = events.Select(e => new TimeBookmark
        {
            Time = e.Time,
            Label = e.Title,
            Color = GetEventColor(e.Type)
        }).ToList();
    }

    private async Task OnEventTimeChanged(TimeChangedMessage message)
    {
        currentEvent = events
            .OrderBy(e => Math.Abs((e.Time - message.CurrentTime).TotalSeconds))
            .FirstOrDefault();
        StateHasChanged();
    }

    private string GetEventColor(EventType type) => type switch
    {
        EventType.Call => "#ef4444",
        EventType.Dispatch => "#f97316",
        EventType.Arrival => "#eab308",
        EventType.Update => "#3b82f6",
        EventType.Cleared => "#10b981",
        _ => "#6b7280"
    };

    private Severity GetEventSeverity(EventType type) => type switch
    {
        EventType.Call => Severity.Error,
        EventType.Dispatch => Severity.Warning,
        EventType.Arrival => Severity.Info,
        EventType.Update => Severity.Normal,
        EventType.Cleared => Severity.Success,
        _ => Severity.Normal
    };

    private Color GetTimelineColor(EventType type) => type switch
    {
        EventType.Call => Color.Error,
        EventType.Dispatch => Color.Warning,
        EventType.Arrival => Color.Info,
        EventType.Update => Color.Primary,
        EventType.Cleared => Color.Success,
        _ => Color.Default
    };

    private enum EventType
    {
        Call,
        Dispatch,
        Arrival,
        Update,
        Cleared
    }

    private class EmergencyEvent
    {
        public DateTime Time { get; set; }
        public EventType Type { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
    }
}
```

---

## Multi-Layer Temporal Sync

Sync multiple map layers with different time fields:

```razor
@page "/timeline-multilayer"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Timeline
@inject ComponentBus Bus

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudPaper Elevation="2" Class="pa-4">
        <MudText Typo="Typo.h4" Class="mb-4">Multi-Layer Temporal Sync</MudText>

        <div style="height: 600px;">
            <HonuaMap Id="multilayer-map"
                      Center="@(new[] { -73.935242, 40.730610 })"
                      Zoom="13" />
        </div>

        <MudTabs>
            <MudTabPanel Text="Vehicles">
                <HonuaTimeline SyncWith="multilayer-map"
                               TimeField="vehicle_timestamp"
                               StartTime="@startTime"
                               EndTime="@endTime"
                               TimeFormat="HH:mm"
                               OnTimeChanged="OnVehicleTimeChanged" />
            </MudTabPanel>
            <MudTabPanel Text="Weather">
                <HonuaTimeline SyncWith="multilayer-map"
                               TimeField="weather_timestamp"
                               StartTime="@startTime"
                               EndTime="@endTime"
                               TimeFormat="HH:mm"
                               OnTimeChanged="OnWeatherTimeChanged" />
            </MudTabPanel>
            <MudTabPanel Text="Traffic">
                <HonuaTimeline SyncWith="multilayer-map"
                               TimeField="traffic_timestamp"
                               StartTime="@startTime"
                               EndTime="@endTime"
                               TimeFormat="HH:mm"
                               OnTimeChanged="OnTrafficTimeChanged" />
            </MudTabPanel>
        </MudTabs>

        <MudText Typo="Typo.body2" Class="mt-4">
            Each layer has its own timeline. Switch tabs to control different data layers independently.
        </MudText>
    </MudPaper>
</MudContainer>

@code {
    private DateTime startTime = DateTime.Today.AddHours(6);
    private DateTime endTime = DateTime.Today.AddHours(22);

    private async Task OnVehicleTimeChanged(TimeChangedMessage message)
    {
        // Update vehicle layer
        await Bus.PublishAsync(new FilterAppliedMessage
        {
            FilterId = "vehicle-temporal",
            Type = FilterType.Temporal,
            Expression = new object[] { "==", new object[] { "get", "vehicle_timestamp" }, message.CurrentTime },
            AffectedLayers = new[] { "vehicles-layer" }
        }, "timeline");
    }

    private async Task OnWeatherTimeChanged(TimeChangedMessage message)
    {
        // Update weather layer
        await Bus.PublishAsync(new FilterAppliedMessage
        {
            FilterId = "weather-temporal",
            Type = FilterType.Temporal,
            Expression = new object[] { "==", new object[] { "get", "weather_timestamp" }, message.CurrentTime },
            AffectedLayers = new[] { "weather-layer" }
        }, "timeline");
    }

    private async Task OnTrafficTimeChanged(TimeChangedMessage message)
    {
        // Update traffic layer
        await Bus.PublishAsync(new FilterAppliedMessage
        {
            FilterId = "traffic-temporal",
            Type = FilterType.Temporal,
            Expression = new object[] { "==", new object[] { "get", "traffic_timestamp" }, message.CurrentTime },
            AffectedLayers = new[] { "traffic-layer" }
        }, "timeline");
    }
}
```

---

## Custom Time Steps

Use irregular time intervals:

```razor
@page "/timeline-custom"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Timeline

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudPaper Elevation="2" Class="pa-4">
        <MudText Typo="Typo.h4" Class="mb-4">Custom Time Steps</MudText>

        <div style="height: 600px;">
            <HonuaMap Id="custom-map"
                      Center="@(new[] { 0, 0 })"
                      Zoom="2" />
        </div>

        <HonuaTimeline SyncWith="custom-map"
                       TimeSteps="@customSteps"
                       TimeFormat="MMMM d, yyyy 'at' h:mm tt"
                       ShowStepButtons="true"
                       Loop="false"
                       OnTimeChanged="OnCustomTimeChanged"
                       Class="mt-4" />

        <MudText Typo="Typo.body2" Class="mt-4">
            <strong>Custom time steps:</strong> This timeline uses irregularly spaced time points
            based on actual data availability, not uniform intervals.
        </MudText>
    </MudPaper>
</MudContainer>

@code {
    // Irregular time steps based on data collection times
    private List<DateTime> customSteps = new()
    {
        DateTime.Parse("2024-01-01T00:00:00"),
        DateTime.Parse("2024-01-01T03:30:00"),
        DateTime.Parse("2024-01-01T08:15:00"),
        DateTime.Parse("2024-01-01T12:00:00"),
        DateTime.Parse("2024-01-01T14:45:00"),
        DateTime.Parse("2024-01-01T18:30:00"),
        DateTime.Parse("2024-01-01T23:59:00"),
        DateTime.Parse("2024-01-02T06:00:00"),
        DateTime.Parse("2024-01-02T12:00:00"),
        DateTime.Parse("2024-01-02T18:00:00"),
        DateTime.Parse("2024-01-03T00:00:00")
    };

    private async Task OnCustomTimeChanged(TimeChangedMessage message)
    {
        // Handle time change
        Console.WriteLine($"Time changed to: {message.CurrentTime}");
    }
}
```

---

## Real-Time Data Streaming

Combine timeline with live data updates:

```razor
@page "/timeline-realtime"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Timeline
@implements IDisposable

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudPaper Elevation="2" Class="pa-4">
        <MudText Typo="Typo.h4" Class="mb-2">Real-Time Data Stream</MudText>
        <MudText Typo="Typo.body2" Color="Color.Secondary" Class="mb-4">
            Live data updates every 5 seconds | Last update: @lastUpdate.ToString("HH:mm:ss")
        </MudText>

        <div style="height: 600px;">
            <HonuaMap Id="realtime-map"
                      Center="@(new[] { -122.4194, 37.7749 })"
                      Zoom="12" />
        </div>

        <HonuaTimeline @ref="timeline"
                       SyncWith="realtime-map"
                       TimeField="timestamp"
                       StartTime="@realtimeStart"
                       EndTime="@realtimeEnd"
                       TimeFormat="HH:mm:ss"
                       AutoPlay="true"
                       Loop="false"
                       Class="mt-4" />

        <MudSwitch @bind-Checked="liveMode" Color="Color.Primary" Class="mt-2">
            Live Mode (auto-advance timeline)
        </MudSwitch>
    </MudPaper>
</MudContainer>

@code {
    private HonuaTimeline timeline;
    private DateTime realtimeStart = DateTime.UtcNow.AddMinutes(-30);
    private DateTime realtimeEnd = DateTime.UtcNow;
    private DateTime lastUpdate = DateTime.UtcNow;
    private System.Threading.Timer updateTimer;
    private bool liveMode = true;

    protected override void OnInitialized()
    {
        // Start update timer
        updateTimer = new System.Threading.Timer(
            async _ => await UpdateRealTimeData(),
            null,
            TimeSpan.Zero,
            TimeSpan.FromSeconds(5)
        );
    }

    private async Task UpdateRealTimeData()
    {
        if (!liveMode) return;

        await InvokeAsync(async () =>
        {
            // Extend timeline end time to now
            realtimeEnd = DateTime.UtcNow;
            lastUpdate = DateTime.UtcNow;

            // Jump timeline to latest data
            if (timeline != null)
            {
                await timeline.JumpToEnd();
            }

            StateHasChanged();
        });
    }

    public void Dispose()
    {
        updateTimer?.Dispose();
    }
}
```

---

## Advanced: Timeline with Export

Export timeline state and timeline-filtered data:

```razor
@page "/timeline-export"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Timeline
@using System.Text.Json

<MudContainer MaxWidth="MaxWidth.ExtraLarge" Class="mt-4">
    <MudPaper Elevation="2" Class="pa-4">
        <MudText Typo="Typo.h4" Class="mb-4">Timeline with Export</MudText>

        <div style="height: 600px;">
            <HonuaMap Id="export-map"
                      Center="@(new[] { -77.0369, 38.9072 })"
                      Zoom="12" />
        </div>

        <HonuaTimeline @ref="timeline"
                       SyncWith="export-map"
                       TimeField="timestamp"
                       StartTime="@startTime"
                       EndTime="@endTime"
                       OnTimeChanged="OnTimeChanged"
                       Class="mt-4" />

        <MudStack Row="true" Class="mt-4" Spacing="2">
            <MudButton Variant="Variant.Filled"
                       Color="Color.Primary"
                       OnClick="ExportCurrentTime"
                       StartIcon="@Icons.Material.Filled.Download">
                Export Current Time
            </MudButton>
            <MudButton Variant="Variant.Filled"
                       Color="Color.Secondary"
                       OnClick="ExportTimeRange"
                       StartIcon="@Icons.Material.Filled.DateRange">
                Export Time Range
            </MudButton>
            <MudButton Variant="Variant.Outlined"
                       OnClick="ExportTimelineState"
                       StartIcon="@Icons.Material.Filled.Save">
                Export Timeline State
            </MudButton>
        </MudStack>
    </MudPaper>
</MudContainer>

@code {
    private HonuaTimeline timeline;
    private DateTime startTime = DateTime.Today.AddHours(6);
    private DateTime endTime = DateTime.Today.AddHours(18);
    private DateTime currentTime = DateTime.Today.AddHours(12);

    private async Task OnTimeChanged(TimeChangedMessage message)
    {
        currentTime = message.CurrentTime;
    }

    private async Task ExportCurrentTime()
    {
        var export = new
        {
            ExportType = "CurrentTime",
            Timestamp = currentTime,
            Data = "Filtered data for current time would go here"
        };

        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        await DownloadFile("timeline-current.json", json);
    }

    private async Task ExportTimeRange()
    {
        var export = new
        {
            ExportType = "TimeRange",
            StartTime = startTime,
            EndTime = endTime,
            CurrentTime = currentTime,
            Data = "All data in time range would go here"
        };

        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        await DownloadFile("timeline-range.json", json);
    }

    private async Task ExportTimelineState()
    {
        var export = new
        {
            ExportType = "TimelineState",
            StartTime = startTime,
            EndTime = endTime,
            CurrentTime = currentTime,
            Configuration = new
            {
                TimeField = "timestamp",
                PlaybackSpeed = 1000,
                Loop = true
            }
        };

        var json = JsonSerializer.Serialize(export, new JsonSerializerOptions { WriteIndented = true });
        await DownloadFile("timeline-state.json", json);
    }

    private async Task DownloadFile(string filename, string content)
    {
        // Use JS interop to download file
        // Implementation details omitted for brevity
        Console.WriteLine($"Downloading {filename}");
    }
}
```

---

## Tips and Best Practices

### Performance

1. **Limit Steps**: Keep total steps under 1000 for smooth performance
2. **Use Appropriate Speed**: Match playback speed to data update frequency
3. **Optimize Filters**: Use efficient filter expressions

### User Experience

1. **Provide Context**: Show current time and total duration
2. **Add Bookmarks**: Mark significant events
3. **Enable Keyboard Shortcuts**: Allow power users to navigate quickly
4. **Use Compact Mode**: On mobile or in sidebars

### Data Management

1. **Cache Data**: Pre-load temporal data when possible
2. **Stream Large Datasets**: Use pagination for very large time ranges
3. **Handle Missing Data**: Gracefully handle gaps in temporal data

---

## Related Documentation

- [HonuaTimeline README](./README.md) - Full component documentation
- [ComponentBus](../../Core/README.md) - Message bus documentation
- [HonuaMap](../Map/README.md) - Map component
- [Time Filtering](../../docs/filtering.md) - Advanced filtering techniques
