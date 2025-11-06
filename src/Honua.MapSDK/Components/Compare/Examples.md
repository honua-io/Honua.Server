# HonuaCompare Examples

Practical examples demonstrating various use cases for the HonuaCompare component.

## Table of Contents

1. [Before/After Natural Disaster](#1-beforeafter-natural-disaster)
2. [Urban Development Over Time](#2-urban-development-over-time)
3. [Basemap Style Comparison](#3-basemap-style-comparison)
4. [Data vs Baseline Comparison](#4-data-vs-baseline-comparison)
5. [Multi-Mode Comparison Dashboard](#5-multi-mode-comparison-dashboard)
6. [Temporal Change Detection](#6-temporal-change-detection)
7. [Quality Assurance Workflow](#7-quality-assurance-workflow)

---

## 1. Before/After Natural Disaster

Compare satellite imagery before and after a hurricane to assess damage.

```razor
@page "/compare/hurricane-damage"
@using Honua.MapSDK.Components.Compare
@using Honua.MapSDK.Models

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Hurricane Damage Assessment</MudText>

    <HonuaCompare LeftMapStyle="@beforeHurricaneStyle"
                  RightMapStyle="@afterHurricaneStyle"
                  Mode="CompareMode.Swipe"
                  LeftLabel="Before Hurricane"
                  RightLabel="After Hurricane"
                  ShowTimestamps="true"
                  LeftTimestamp="@beforeTimestamp"
                  RightTimestamp="@afterTimestamp"
                  Center="@(new[] { -80.1918, 25.7617 })"
                  Zoom="13"
                  Height="700px"
                  OnModeChanged="HandleModeChanged" />

    <MudText Typo="Typo.body2" Class="mt-2">
        Use the swipe tool to compare before and after imagery.
        Switch to Flicker mode to quickly identify changes.
    </MudText>
</MudContainer>

@code {
    // Satellite imagery from different dates
    private string beforeHurricaneStyle = "https://api.maptiler.com/maps/satellite/{z}/{x}/{y}.jpg?key=YOUR_KEY&t=2023-08-15";
    private string afterHurricaneStyle = "https://api.maptiler.com/maps/satellite/{z}/{x}/{y}.jpg?key=YOUR_KEY&t=2023-09-15";

    private CompareTimestamp beforeTimestamp = new CompareTimestamp
    {
        Time = new DateTime(2023, 8, 15),
        Label = "August 15, 2023",
        Description = "Pre-hurricane conditions"
    };

    private CompareTimestamp afterTimestamp = new CompareTimestamp
    {
        Time = new DateTime(2023, 9, 15),
        Label = "September 15, 2023",
        Description = "Post-hurricane damage"
    };

    private void HandleModeChanged(CompareMode mode)
    {
        Console.WriteLine($"Switched to {mode} mode for damage assessment");
    }
}
```

---

## 2. Urban Development Over Time

Track urban growth and development over multiple years.

```razor
@page "/compare/urban-development"
@using Honua.MapSDK.Components.Compare
@using Honua.MapSDK.Models

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Urban Development: 2010 vs 2024</MudText>

    <MudPaper Class="pa-4 mb-4">
        <MudStack Row="true" Spacing="3" AlignItems="AlignItems.Center">
            <MudText>Select Time Period:</MudText>
            <MudSelect T="string"
                       Label="Before"
                       @bind-Value="selectedBeforeYear"
                       Variant="Variant.Outlined"
                       Dense="true">
                <MudSelectItem Value="@("2010")">2010</MudSelectItem>
                <MudSelectItem Value="@("2015")">2015</MudSelectItem>
                <MudSelectItem Value="@("2020")">2020</MudSelectItem>
            </MudSelect>
            <MudSelect T="string"
                       Label="After"
                       @bind-Value="selectedAfterYear"
                       Variant="Variant.Outlined"
                       Dense="true">
                <MudSelectItem Value="@("2015")">2015</MudSelectItem>
                <MudSelectItem Value="@("2020")">2020</MudSelectItem>
                <MudSelectItem Value="@("2024")">2024</MudSelectItem>
            </MudSelect>
            <MudButton Variant="Variant.Filled"
                       Color="Color.Primary"
                       OnClick="UpdateComparison">
                Update
            </MudButton>
        </MudStack>
    </MudPaper>

    <HonuaCompare @ref="compareRef"
                  LeftMapStyle="@GetStyleForYear(selectedBeforeYear)"
                  RightMapStyle="@GetStyleForYear(selectedAfterYear)"
                  Mode="CompareMode.Overlay"
                  LeftLabel="@selectedBeforeYear"
                  RightLabel="@selectedAfterYear"
                  Center="@(new[] { -118.2437, 34.0522 })"
                  Zoom="12"
                  Height="700px"
                  AllowModeSwitch="true"
                  OnPositionChanged="HandlePositionChanged" />

    <MudPaper Class="pa-3 mt-2">
        <MudText Typo="Typo.body2">
            <strong>Observations:</strong> Track new construction, infrastructure development,
            and land use changes over time. Use Overlay mode with opacity adjustment to
            identify subtle changes.
        </MudText>
    </MudPaper>
</MudContainer>

@code {
    private HonuaCompare? compareRef;
    private string selectedBeforeYear = "2010";
    private string selectedAfterYear = "2024";

    private Dictionary<string, string> yearStyles = new()
    {
        { "2010", "https://api.maptiler.com/maps/hybrid/{z}/{x}/{y}.jpg?key=YOUR_KEY&date=2010" },
        { "2015", "https://api.maptiler.com/maps/hybrid/{z}/{x}/{y}.jpg?key=YOUR_KEY&date=2015" },
        { "2020", "https://api.maptiler.com/maps/hybrid/{z}/{x}/{y}.jpg?key=YOUR_KEY&date=2020" },
        { "2024", "https://api.maptiler.com/maps/hybrid/{z}/{x}/{y}.jpg?key=YOUR_KEY&date=2024" }
    };

    private string GetStyleForYear(string year) => yearStyles.GetValueOrDefault(year, yearStyles["2024"]);

    private async Task UpdateComparison()
    {
        if (compareRef != null)
        {
            await compareRef.UpdateLeftStyle(GetStyleForYear(selectedBeforeYear));
            await compareRef.UpdateRightStyle(GetStyleForYear(selectedAfterYear));
        }
    }

    private void HandlePositionChanged(double position)
    {
        Console.WriteLine($"Divider at {position:P0}");
    }
}
```

---

## 3. Basemap Style Comparison

Compare different basemap styles to choose the best one for your application.

```razor
@page "/compare/basemap-styles"
@using Honua.MapSDK.Components.Compare
@using Honua.MapSDK.Models

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Basemap Style Comparison</MudText>

    <MudPaper Class="pa-4 mb-4">
        <MudStack Row="true" Spacing="3">
            <MudSelect T="BasemapStyle"
                       Label="Left Map"
                       @bind-Value="leftStyle"
                       Variant="Variant.Outlined">
                @foreach (var style in basemapStyles)
                {
                    <MudSelectItem Value="@style">@style.Name</MudSelectItem>
                }
            </MudSelect>
            <MudSelect T="BasemapStyle"
                       Label="Right Map"
                       @bind-Value="rightStyle"
                       Variant="Variant.Outlined">
                @foreach (var style in basemapStyles)
                {
                    <MudSelectItem Value="@style">@style.Name</MudSelectItem>
                }
            </MudSelect>
        </MudStack>
    </MudPaper>

    <HonuaCompare LeftMapStyle="@leftStyle.Url"
                  RightMapStyle="@rightStyle.Url"
                  Mode="CompareMode.Swipe"
                  LeftLabel="@leftStyle.Name"
                  RightLabel="@rightStyle.Name"
                  Center="@(new[] { -73.9857, 40.7484 })"
                  Zoom="12"
                  Height="700px"
                  AllowModeSwitch="true"
                  AllowOrientationSwitch="true" />
</MudContainer>

@code {
    private BasemapStyle leftStyle;
    private BasemapStyle rightStyle;

    private List<BasemapStyle> basemapStyles = new()
    {
        new BasemapStyle { Name = "Light", Url = "https://api.maptiler.com/maps/basic-v2/style.json?key=YOUR_KEY" },
        new BasemapStyle { Name = "Dark", Url = "https://api.maptiler.com/maps/basic-v2-dark/style.json?key=YOUR_KEY" },
        new BasemapStyle { Name = "Satellite", Url = "https://api.maptiler.com/maps/satellite/style.json?key=YOUR_KEY" },
        new BasemapStyle { Name = "Hybrid", Url = "https://api.maptiler.com/maps/hybrid/style.json?key=YOUR_KEY" },
        new BasemapStyle { Name = "Streets", Url = "https://api.maptiler.com/maps/streets-v2/style.json?key=YOUR_KEY" },
        new BasemapStyle { Name = "Topo", Url = "https://api.maptiler.com/maps/topo-v2/style.json?key=YOUR_KEY" },
        new BasemapStyle { Name = "Outdoor", Url = "https://api.maptiler.com/maps/outdoor-v2/style.json?key=YOUR_KEY" }
    };

    protected override void OnInitialized()
    {
        leftStyle = basemapStyles[0]; // Light
        rightStyle = basemapStyles[1]; // Dark
    }

    private class BasemapStyle
    {
        public string Name { get; set; } = "";
        public string Url { get; set; } = "";
    }
}
```

---

## 4. Data vs Baseline Comparison

Compare analysis results against a baseline or reference dataset.

```razor
@page "/compare/data-validation"
@using Honua.MapSDK.Components.Compare
@using Honua.MapSDK.Models

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Flood Risk Analysis Validation</MudText>

    <MudAlert Severity="Severity.Info" Class="mb-4">
        Compare model predictions against observed flood extent to validate accuracy.
    </MudAlert>

    <HonuaCompare LeftMapStyle="@baselineStyle"
                  RightMapStyle="@analysisStyle"
                  Mode="CompareMode.Overlay"
                  OverlayOpacity="0.6"
                  LeftLabel="Observed Data"
                  RightLabel="Model Prediction"
                  Center="@(new[] { -90.0715, 29.9511 })"
                  Zoom="11"
                  Height="700px"
                  OnModeChanged="HandleModeChanged" />

    <MudPaper Class="pa-4 mt-4">
        <MudText Typo="Typo.h6" Class="mb-2">Validation Metrics</MudText>
        <MudGrid>
            <MudItem xs="12" md="4">
                <MudPaper Class="pa-3" Elevation="0" Style="background-color: #f0f9ff;">
                    <MudText Typo="Typo.subtitle2" Color="Color.Primary">Match Percentage</MudText>
                    <MudText Typo="Typo.h4">87.3%</MudText>
                </MudPaper>
            </MudItem>
            <MudItem xs="12" md="4">
                <MudPaper Class="pa-3" Elevation="0" Style="background-color: #fef3c7;">
                    <MudText Typo="Typo.subtitle2" Color="Color.Warning">Over-prediction</MudText>
                    <MudText Typo="Typo.h4">8.2%</MudText>
                </MudPaper>
            </MudItem>
            <MudItem xs="12" md="4">
                <MudPaper Class="pa-3" Elevation="0" Style="background-color: #fee2e2;">
                    <MudText Typo="Typo.subtitle2" Color="Color.Error">Under-prediction</MudText>
                    <MudText Typo="Typo.h4">4.5%</MudText>
                </MudPaper>
            </MudItem>
        </MudGrid>
    </MudPaper>
</MudContainer>

@code {
    private string baselineStyle = "https://your-api/styles/flood-observed.json";
    private string analysisStyle = "https://your-api/styles/flood-predicted.json";

    private void HandleModeChanged(CompareMode mode)
    {
        if (mode == CompareMode.Overlay)
        {
            Console.WriteLine("Overlay mode is best for detecting differences in coverage");
        }
    }
}
```

---

## 5. Multi-Mode Comparison Dashboard

Interactive dashboard allowing users to switch between all comparison modes.

```razor
@page "/compare/interactive-dashboard"
@using Honua.MapSDK.Components.Compare
@using Honua.MapSDK.Models

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Interactive Comparison Dashboard</MudText>

    <MudPaper Class="pa-4 mb-4">
        <MudStack Row="true" Spacing="2">
            <MudChip Color="@(currentMode == CompareMode.SideBySide ? Color.Primary : Color.Default)"
                     OnClick="() => SetMode(CompareMode.SideBySide)">
                Side-by-Side
            </MudChip>
            <MudChip Color="@(currentMode == CompareMode.Swipe ? Color.Primary : Color.Default)"
                     OnClick="() => SetMode(CompareMode.Swipe)">
                Swipe
            </MudChip>
            <MudChip Color="@(currentMode == CompareMode.Overlay ? Color.Primary : Color.Default)"
                     OnClick="() => SetMode(CompareMode.Overlay)">
                Overlay
            </MudChip>
            <MudChip Color="@(currentMode == CompareMode.Flicker ? Color.Primary : Color.Default)"
                     OnClick="() => SetMode(CompareMode.Flicker)">
                Flicker
            </MudChip>
            <MudChip Color="@(currentMode == CompareMode.SpyGlass ? Color.Primary : Color.Default)"
                     OnClick="() => SetMode(CompareMode.SpyGlass)">
                Spy Glass
            </MudChip>
        </MudStack>
    </MudPaper>

    <HonuaCompare @ref="compareRef"
                  LeftMapStyle="@leftMap"
                  RightMapStyle="@rightMap"
                  Mode="@currentMode"
                  Center="@(new[] { -122.4194, 37.7749 })"
                  Zoom="13"
                  Height="700px"
                  AllowModeSwitch="false"
                  OnModeChanged="mode => currentMode = mode" />

    <MudPaper Class="pa-4 mt-4">
        <MudText Typo="Typo.h6" Class="mb-2">Mode Descriptions</MudText>
        <MudList>
            <MudListItem Icon="@Icons.Material.Filled.ViewColumn">
                <strong>Side-by-Side:</strong> Compare maps with a fixed divider
            </MudListItem>
            <MudListItem Icon="@Icons.Material.Filled.SwapHoriz">
                <strong>Swipe:</strong> Drag the divider to reveal/hide maps
            </MudListItem>
            <MudListItem Icon="@Icons.Material.Filled.Layers">
                <strong>Overlay:</strong> Adjust transparency to see both maps
            </MudListItem>
            <MudListItem Icon="@Icons.Material.Filled.FlashOn">
                <strong>Flicker:</strong> Rapidly alternate between maps
            </MudListItem>
            <MudListItem Icon="@Icons.Material.Filled.Search">
                <strong>Spy Glass:</strong> Move your mouse to explore with a magnifier
            </MudListItem>
        </MudList>
    </MudPaper>
</MudContainer>

@code {
    private HonuaCompare? compareRef;
    private CompareMode currentMode = CompareMode.Swipe;

    private string leftMap = "https://api.maptiler.com/maps/streets/style.json?key=YOUR_KEY";
    private string rightMap = "https://api.maptiler.com/maps/satellite/style.json?key=YOUR_KEY";

    private async Task SetMode(CompareMode mode)
    {
        if (compareRef != null)
        {
            await compareRef.SetMode(mode);
        }
    }
}
```

---

## 6. Temporal Change Detection

Analyze changes over time with multiple time periods.

```razor
@page "/compare/temporal-analysis"
@using Honua.MapSDK.Components.Compare
@using Honua.MapSDK.Models

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Deforestation Analysis: Amazon Rainforest</MudText>

    <MudPaper Class="pa-4 mb-4">
        <MudSlider @bind-Value="timeSliderValue"
                   Min="0"
                   Max="@(timePeriods.Count - 1)"
                   Step="1"
                   ValueLabel="true"
                   ValueLabelFormat="@GetTimePeriodLabel">
            Time Period Comparison
        </MudSlider>
    </MudPaper>

    <HonuaCompare @ref="compareRef"
                  LeftMapStyle="@GetLeftStyle()"
                  RightMapStyle="@GetRightStyle()"
                  Mode="CompareMode.Flicker"
                  FlickerInterval="2000"
                  LeftLabel="@GetLeftLabel()"
                  RightLabel="@GetRightLabel()"
                  ShowTimestamps="true"
                  LeftTimestamp="@GetLeftTimestamp()"
                  RightTimestamp="@GetRightTimestamp()"
                  Center="@(new[] { -62.2159, -3.4653 })"
                  Zoom="10"
                  Height="700px" />

    <MudPaper Class="pa-4 mt-4">
        <MudText Typo="Typo.h6" Class="mb-2">Analysis Summary</MudText>
        <MudText>
            Forest cover loss: <strong>@CalculateForestLoss()%</strong> between
            @GetLeftLabel() and @GetRightLabel()
        </MudText>
        <MudProgressLinear Value="@CalculateForestLoss()"
                          Color="Color.Error"
                          Size="Size.Large"
                          Class="mt-2" />
    </MudPaper>
</MudContainer>

@code {
    private HonuaCompare? compareRef;
    private int timeSliderValue = 0;

    private List<TimePeriod> timePeriods = new()
    {
        new TimePeriod { Year = 2000, StyleUrl = "https://api.example.com/forest-2000", ForestCover = 100 },
        new TimePeriod { Year = 2005, StyleUrl = "https://api.example.com/forest-2005", ForestCover = 95 },
        new TimePeriod { Year = 2010, StyleUrl = "https://api.example.com/forest-2010", ForestCover = 88 },
        new TimePeriod { Year = 2015, StyleUrl = "https://api.example.com/forest-2015", ForestCover = 79 },
        new TimePeriod { Year = 2020, StyleUrl = "https://api.example.com/forest-2020", ForestCover = 71 },
        new TimePeriod { Year = 2024, StyleUrl = "https://api.example.com/forest-2024", ForestCover = 64 }
    };

    private string GetTimePeriodLabel(int value) =>
        value < timePeriods.Count ? timePeriods[value].Year.ToString() : "";

    private string GetLeftStyle() => timePeriods[Math.Max(0, timeSliderValue - 1)].StyleUrl;
    private string GetRightStyle() => timePeriods[timeSliderValue].StyleUrl;
    private string GetLeftLabel() => timePeriods[Math.Max(0, timeSliderValue - 1)].Year.ToString();
    private string GetRightLabel() => timePeriods[timeSliderValue].Year.ToString();

    private CompareTimestamp GetLeftTimestamp() => new CompareTimestamp
    {
        Time = new DateTime(timePeriods[Math.Max(0, timeSliderValue - 1)].Year, 1, 1),
        Label = GetLeftLabel(),
        Description = $"{timePeriods[Math.Max(0, timeSliderValue - 1)].ForestCover}% forest cover"
    };

    private CompareTimestamp GetRightTimestamp() => new CompareTimestamp
    {
        Time = new DateTime(timePeriods[timeSliderValue].Year, 1, 1),
        Label = GetRightLabel(),
        Description = $"{timePeriods[timeSliderValue].ForestCover}% forest cover"
    };

    private double CalculateForestLoss()
    {
        var leftCover = timePeriods[Math.Max(0, timeSliderValue - 1)].ForestCover;
        var rightCover = timePeriods[timeSliderValue].ForestCover;
        return Math.Round(leftCover - rightCover, 1);
    }

    private class TimePeriod
    {
        public int Year { get; set; }
        public string StyleUrl { get; set; } = "";
        public double ForestCover { get; set; }
    }
}
```

---

## 7. Quality Assurance Workflow

Systematic QA workflow for validating new datasets.

```razor
@page "/compare/qa-workflow"
@using Honua.MapSDK.Components.Compare
@using Honua.MapSDK.Models

<MudContainer MaxWidth="MaxWidth.ExtraExtraLarge" Class="mt-4">
    <MudText Typo="Typo.h4" Class="mb-4">Dataset Quality Assurance</MudText>

    <MudStepper @ref="stepper" Linear="true">
        <MudStep Title="Visual Inspection">
            <MudText Class="mb-4">Compare new dataset against reference data</MudText>
            <HonuaCompare LeftMapStyle="@referenceData"
                          RightMapStyle="@newData"
                          Mode="CompareMode.Swipe"
                          LeftLabel="Reference Data"
                          RightLabel="New Data"
                          Center="@(new[] { -122.3321, 47.6062 })"
                          Zoom="12"
                          Height="500px" />
        </MudStep>

        <MudStep Title="Accuracy Assessment">
            <MudText Class="mb-4">Use spy glass mode for detailed inspection</MudText>
            <HonuaCompare LeftMapStyle="@referenceData"
                          RightMapStyle="@newData"
                          Mode="CompareMode.SpyGlass"
                          SpyGlassRadius="200"
                          LeftLabel="Reference"
                          RightLabel="New Data"
                          Center="@(new[] { -122.3321, 47.6062 })"
                          Zoom="14"
                          Height="500px" />
        </MudStep>

        <MudStep Title="Change Detection">
            <MudText Class="mb-4">Identify unexpected differences with flicker mode</MudText>
            <HonuaCompare LeftMapStyle="@referenceData"
                          RightMapStyle="@newData"
                          Mode="CompareMode.Flicker"
                          FlickerInterval="1500"
                          LeftLabel="Reference"
                          RightLabel="New Data"
                          Center="@(new[] { -122.3321, 47.6062 })"
                          Zoom="13"
                          Height="500px" />
        </MudStep>

        <MudStep Title="Final Review">
            <MudPaper Class="pa-4">
                <MudText Typo="Typo.h6" Class="mb-3">QA Results</MudText>
                <MudCheckBox @bind-Checked="@geometryAccurate" Label="Geometry accuracy verified" />
                <MudCheckBox @bind-Checked="@attributesCorrect" Label="Attributes are correct" />
                <MudCheckBox @bind-Checked="@noMissingData" Label="No missing data detected" />
                <MudCheckBox @bind-Checked="@coordinateSystemValid" Label="Coordinate system validated" />

                <MudButton Variant="Variant.Filled"
                          Color="Color.Success"
                          Class="mt-4"
                          Disabled="@(!AllChecksPass())"
                          OnClick="ApproveDataset">
                    Approve Dataset
                </MudButton>
            </MudPaper>
        </MudStep>
    </MudStepper>
</MudContainer>

@code {
    private MudStepper? stepper;
    private string referenceData = "https://api.example.com/reference-dataset";
    private string newData = "https://api.example.com/new-dataset";

    private bool geometryAccurate = false;
    private bool attributesCorrect = false;
    private bool noMissingData = false;
    private bool coordinateSystemValid = false;

    private bool AllChecksPass() =>
        geometryAccurate && attributesCorrect && noMissingData && coordinateSystemValid;

    private void ApproveDataset()
    {
        Console.WriteLine("Dataset approved and ready for production");
        // Implement approval logic
    }
}
```

---

## Tips for Effective Comparisons

1. **Choose the Right Mode**:
   - **Swipe**: Best for aligned imagery and precise comparisons
   - **Overlay**: Ideal for subtle change detection and semi-transparent overlays
   - **Flicker**: Quick change identification, good for before/after
   - **Side-by-Side**: Detailed analysis when both views need to be fully visible
   - **Spy Glass**: Localized exploration and detail inspection

2. **Optimize Performance**:
   - Use appropriate zoom levels for the data being compared
   - Consider tile size and resolution for smooth interaction
   - Enable sync navigation for aligned comparisons

3. **Enhance User Experience**:
   - Provide clear labels and timestamps
   - Allow mode switching for different perspectives
   - Use fullscreen for detailed analysis
   - Capture screenshots for documentation

4. **Data Preparation**:
   - Ensure coordinate systems match
   - Verify data alignment before comparison
   - Use consistent styling for similar features
   - Normalize date/time information

## Additional Resources

- See [README.md](./README.md) for complete API documentation
- Check component source code for advanced customization
- Review MapLibre GL JS documentation for map styling options
