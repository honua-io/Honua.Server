# HonuaTimeline Component

A powerful, production-ready timeline component for animating temporal data in geospatial dashboards. Features smooth playback controls, customizable time ranges, and seamless integration with HonuaMap through ComponentBus.

## Features

- **Smooth Playback**: Play, pause, step forward/backward through time
- **Speed Control**: Multiple playback speeds (0.25x to 8x)
- **Time Modes**: Absolute dates, relative time, index-based, or custom steps
- **Responsive Design**: Adapts to desktop, tablet, and mobile
- **Keyboard Shortcuts**: Space, arrows, Home/End for quick navigation
- **Bookmarks**: Mark important times for quick access
- **Loop Playback**: Continuous playback with optional reverse
- **ComponentBus Integration**: Publishes TimeChangedMessage for syncing with maps and other components
- **Accessibility**: Full ARIA support and keyboard navigation

## Quick Start

### Basic Usage

```razor
@page "/timeline-demo"
@using Honua.MapSDK.Components.Map
@using Honua.MapSDK.Components.Timeline

<HonuaMap Id="map1" />

<HonuaTimeline SyncWith="map1"
               TimeField="timestamp"
               StartTime="@startDate"
               EndTime="@endDate" />

@code {
    private DateTime startDate = DateTime.UtcNow.AddDays(-7);
    private DateTime endDate = DateTime.UtcNow;
}
```

### Timeline with Playback Controls

```razor
<HonuaTimeline SyncWith="map1"
               TimeField="datetime"
               PlaybackSpeed="1000"
               SpeedMultiplier="1.0"
               ShowSpeedControls="true"
               ShowStepButtons="true"
               ShowJumpButtons="true"
               Loop="true"
               AutoPlay="false" />
```

### Custom Time Steps

```razor
<HonuaTimeline SyncWith="map1"
               TimeSteps="@customTimeSteps"
               TimeFormat="yyyy-MM-dd HH:mm"
               ShowBookmarks="true"
               Bookmarks="@bookmarks" />

@code {
    private List<DateTime> customTimeSteps = new()
    {
        new DateTime(2024, 1, 1, 0, 0, 0),
        new DateTime(2024, 1, 1, 6, 0, 0),
        new DateTime(2024, 1, 1, 12, 0, 0),
        new DateTime(2024, 1, 1, 18, 0, 0),
        new DateTime(2024, 1, 2, 0, 0, 0)
    };

    private List<TimeBookmark> bookmarks = new()
    {
        new TimeBookmark
        {
            Time = new DateTime(2024, 1, 1, 12, 0, 0),
            Label = "Peak Activity",
            Color = "#ef4444"
        }
    };
}
```

### Compact Mode

```razor
<HonuaTimeline SyncWith="map1"
               Compact="true"
               Width="400px"
               ShowDateRange="false"
               Position="TimelinePosition.BottomCenter" />
```

## Parameters

### Core Configuration

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Id` | `string` | Auto-generated | Unique identifier for the timeline |
| `SyncWith` | `string?` | `null` | Map ID to sync with |
| `TimeField` | `string?` | `null` | Field name containing timestamp data |

### Time Range

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `StartTime` | `DateTime?` | 24 hours ago | Timeline start time |
| `EndTime` | `DateTime?` | Now | Timeline end time |
| `TimeSteps` | `List<DateTime>?` | Auto-generated | Custom time steps |
| `TimeFormat` | `string` | `"yyyy-MM-dd HH:mm"` | Time display format |
| `StepUnit` | `TimeStepUnit` | `Auto` | Time step unit (Minutes, Hours, Days, etc.) |
| `TotalStepsOverride` | `int?` | Auto-calculated | Number of steps to generate |

### Playback

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `PlaybackSpeed` | `int` | `1000` | Speed in milliseconds per step |
| `SpeedMultiplier` | `double` | `1.0` | Speed multiplier (0.25x to 8x) |
| `Loop` | `bool` | `true` | Enable loop playback |
| `AutoPlay` | `bool` | `false` | Start playing on load |
| `EnableReverse` | `bool` | `false` | Enable reverse playback |

### UI Controls

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `ShowSpeedControls` | `bool` | `true` | Show speed selector |
| `ShowStepButtons` | `bool` | `true` | Show step forward/backward buttons |
| `ShowJumpButtons` | `bool` | `true` | Show jump to start/end buttons |
| `ShowDateRange` | `bool` | `true` | Show date range labels |
| `ShowCurrentTime` | `bool` | `true` | Show current time display |
| `ShowBookmarks` | `bool` | `false` | Show bookmark markers |
| `Compact` | `bool` | `false` | Enable compact mode |

### Appearance

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `Width` | `string` | `"100%"` | Component width |
| `Position` | `TimelinePosition` | `None` | Fixed position (TopLeft, BottomCenter, etc.) |
| `Theme` | `string` | `"light"` | Theme (light or dark) |
| `CssClass` | `string?` | `null` | Additional CSS class |
| `Style` | `string?` | `null` | Custom inline style |

### Features

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `EnableKeyboardShortcuts` | `bool` | `true` | Enable keyboard navigation |
| `TimeZone` | `TimeZoneDisplay` | `Local` | Display in Local or UTC |
| `Bookmarks` | `List<TimeBookmark>?` | `null` | Bookmark list |

### Events

| Event | Type | Description |
|-------|------|-------------|
| `OnTimeChanged` | `EventCallback<TimeChangedMessage>` | Fired when time position changes |
| `OnStateChanged` | `EventCallback<PlaybackState>` | Fired when playback state changes |

## Time Modes

### Absolute Time

Display specific dates and times:

```razor
<HonuaTimeline StartTime="@(new DateTime(2024, 1, 1))"
               EndTime="@(new DateTime(2024, 12, 31))"
               TimeFormat="yyyy-MM-dd" />
```

### Relative Time

Display time relative to now:

```razor
<HonuaTimeline StartTime="@DateTime.UtcNow.AddHours(-24)"
               EndTime="@DateTime.UtcNow"
               TimeFormat="HH:mm" />
```

### Custom Steps

Define your own time points:

```razor
<HonuaTimeline TimeSteps="@customSteps" />

@code {
    private List<DateTime> customSteps = new()
    {
        DateTime.Parse("2024-01-01T00:00:00"),
        DateTime.Parse("2024-01-01T06:00:00"),
        DateTime.Parse("2024-01-01T12:00:00"),
        DateTime.Parse("2024-01-01T18:00:00")
    };
}
```

## ComponentBus Integration

The timeline automatically publishes `TimeChangedMessage` when the time position changes:

```csharp
public record TimeChangedMessage
{
    DateTime CurrentTime;        // Current time position
    DateTime StartTime;          // Timeline start
    DateTime EndTime;            // Timeline end
    string ComponentId;          // Timeline ID
    string? TimeField;           // Field name for filtering
    int CurrentStep;             // Current step index
    int TotalSteps;              // Total steps
    double Progress;             // Progress (0-100%)
    bool IsPlaying;              // Playback state
    int Direction;               // 1 = forward, -1 = reverse
}
```

### Listening to Time Changes

```razor
@inject ComponentBus Bus

@code {
    protected override void OnInitialized()
    {
        Bus.Subscribe<TimeChangedMessage>(async args =>
        {
            var time = args.Message.CurrentTime;
            Console.WriteLine($"Time changed to: {time}");

            // Filter data by time
            await FilterDataByTime(time);
        });
    }
}
```

### Temporal Filtering

The timeline automatically publishes temporal filters:

```csharp
// Published when time changes
new FilterAppliedMessage
{
    FilterId = "timeline-temporal-filter",
    Type = FilterType.Temporal,
    Expression = ["==", ["get", "timestamp"], currentTime]
}
```

## Keyboard Shortcuts

| Key | Action |
|-----|--------|
| Space | Play/Pause |
| → (Right Arrow) | Step forward |
| ← (Left Arrow) | Step backward |
| Home | Jump to start |
| End | Jump to end |

## Positioning

Position the timeline anywhere on screen:

```razor
<!-- Fixed at bottom center -->
<HonuaTimeline Position="TimelinePosition.BottomCenter" />

<!-- Fixed at top right -->
<HonuaTimeline Position="TimelinePosition.TopRight" />

<!-- Inline with page content -->
<HonuaTimeline Position="TimelinePosition.None" />
```

Available positions:
- `TopLeft`
- `TopCenter`
- `TopRight`
- `BottomLeft`
- `BottomCenter`
- `BottomRight`
- `None` (inline)

## Bookmarks

Mark important times with visual indicators:

```razor
<HonuaTimeline ShowBookmarks="true"
               Bookmarks="@bookmarks" />

@code {
    private List<TimeBookmark> bookmarks = new()
    {
        new TimeBookmark
        {
            Time = new DateTime(2024, 1, 15, 10, 30, 0),
            Label = "Event Start",
            Description = "Main event begins",
            Color = "#3b82f6"
        },
        new TimeBookmark
        {
            Time = new DateTime(2024, 1, 15, 12, 0, 0),
            Label = "Peak Activity",
            Color = "#ef4444"
        }
    };
}
```

## Programmatic Control

Access timeline methods for programmatic control:

```razor
<HonuaTimeline @ref="timeline" />
<MudButton OnClick="PlayTimeline">Play</MudButton>
<MudButton OnClick="PauseTimeline">Pause</MudButton>
<MudButton OnClick="JumpToTime">Jump to Noon</MudButton>

@code {
    private HonuaTimeline timeline;

    private async Task PlayTimeline()
    {
        await timeline.Play();
    }

    private async Task PauseTimeline()
    {
        await timeline.Pause();
    }

    private async Task JumpToTime()
    {
        var noon = DateTime.Today.AddHours(12);
        await timeline.JumpToTime(noon);
    }
}
```

### Available Methods

- `Play()` - Start playback
- `Pause()` - Pause playback
- `Stop()` - Stop and reset to start
- `StepForward()` - Move one step forward
- `StepBackward()` - Move one step backward
- `JumpToStart()` - Jump to beginning
- `JumpToEnd()` - Jump to end
- `JumpToTime(DateTime)` - Jump to specific time

## Styling

### Custom Themes

```razor
<!-- Dark theme -->
<HonuaTimeline Theme="dark" />

<!-- Custom styling -->
<HonuaTimeline CssClass="my-timeline"
               Style="background: linear-gradient(to right, #667eea, #764ba2);" />
```

### CSS Variables

Customize appearance with CSS:

```css
.my-timeline {
    --timeline-primary: #3b82f6;
    --timeline-background: #ffffff;
    --timeline-text: #1f2937;
    --timeline-slider-height: 8px;
}
```

## Use Cases

### Vehicle Tracking

```razor
<HonuaMap Id="vehicle-map" />
<HonuaTimeline SyncWith="vehicle-map"
               TimeField="gps_timestamp"
               StartTime="@tripStart"
               EndTime="@tripEnd"
               TimeFormat="HH:mm:ss"
               PlaybackSpeed="500"
               AutoPlay="true" />
```

### Weather Animation

```razor
<HonuaMap Id="weather-map" />
<HonuaTimeline SyncWith="weather-map"
               TimeField="forecast_time"
               StartTime="@DateTime.UtcNow"
               EndTime="@DateTime.UtcNow.AddDays(7)"
               TimeFormat="MMM dd HH:mm"
               StepUnit="TimeStepUnit.Hours"
               TotalStepsOverride="168" />
```

### Historical Imagery

```razor
<HonuaMap Id="imagery-map" />
<HonuaTimeline SyncWith="imagery-map"
               TimeSteps="@imageryDates"
               TimeFormat="MMMM yyyy"
               ShowStepButtons="true"
               Loop="false" />

@code {
    private List<DateTime> imageryDates = new()
    {
        new DateTime(2020, 1, 1),
        new DateTime(2021, 1, 1),
        new DateTime(2022, 1, 1),
        new DateTime(2023, 1, 1),
        new DateTime(2024, 1, 1)
    };
}
```

### Sensor Data

```razor
<HonuaMap Id="sensor-map" />
<HonuaTimeline SyncWith="sensor-map"
               TimeField="reading_time"
               StartTime="@DateTime.UtcNow.AddHours(-24)"
               EndTime="@DateTime.UtcNow"
               TimeFormat="HH:mm"
               PlaybackSpeed="100"
               SpeedMultiplier="4.0" />
```

## Responsive Design

The timeline automatically adapts to screen size:

- **Desktop**: Full controls with all features
- **Tablet**: Optimized spacing, icon-only speed controls
- **Mobile**: Minimal controls (play/pause, slider)

### Mobile Optimization

```razor
<HonuaTimeline Compact="true"
               ShowStepButtons="false"
               ShowJumpButtons="false"
               ShowSpeedControls="false"
               Position="TimelinePosition.BottomCenter" />
```

## Accessibility

### ARIA Support

All controls have proper ARIA labels:
- Slider: `role="slider"` with `aria-valuemin`, `aria-valuemax`, `aria-valuenow`
- Buttons: `aria-label` descriptions
- Time display: `aria-live="polite"` for screen readers

### Keyboard Navigation

Full keyboard support:
- Tab navigation through controls
- Space/Enter to activate buttons
- Arrow keys for slider
- Home/End for quick navigation

### Screen Reader

Time changes are announced:
- Current time position
- Playback state changes
- Progress updates

## Performance

### Optimization Tips

1. **Limit Steps**: Don't use more than 1000 steps for smooth performance
2. **Pause on Hide**: Timeline automatically pauses when tab is hidden
3. **Throttle Updates**: Use higher `PlaybackSpeed` values for large datasets
4. **Dispose Properly**: Timeline automatically cleans up resources

### Large Datasets

```razor
<!-- Optimize for large time ranges -->
<HonuaTimeline StartTime="@startDate"
               EndTime="@endDate"
               TotalStepsOverride="500"
               PlaybackSpeed="1000" />
```

## Troubleshooting

### Timeline Not Updating

Ensure `SyncWith` matches the map ID:

```razor
<HonuaMap Id="my-map" />
<HonuaTimeline SyncWith="my-map" /> <!-- Must match -->
```

### Performance Issues

Reduce number of steps:

```razor
<HonuaTimeline TotalStepsOverride="100" /> <!-- Limit to 100 steps -->
```

### Time Format Issues

Use valid .NET DateTime format strings:

```razor
<HonuaTimeline TimeFormat="yyyy-MM-dd HH:mm:ss" />
```

## Browser Support

- Chrome 90+
- Firefox 88+
- Safari 14+
- Edge 90+

## License

Part of Honua.MapSDK - MIT License

## Related Components

- [HonuaMap](../Map/README.md) - Interactive map component
- [HonuaChart](../Chart/README.md) - Data visualization
- [HonuaFilterPanel](../FilterPanel/README.md) - Data filtering
- [HonuaDataGrid](../DataGrid/README.md) - Tabular data display
