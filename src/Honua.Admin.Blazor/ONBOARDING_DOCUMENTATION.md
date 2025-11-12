# Honua Interactive Tutorial & Onboarding System

Comprehensive documentation for the Honua Admin platform's interactive tutorial and onboarding system.

## Table of Contents

1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Components](#components)
4. [Services](#services)
5. [Pre-Built Tours](#pre-built-tours)
6. [Creating Custom Tours](#creating-custom-tours)
7. [Sample Data System](#sample-data-system)
8. [Integration Guide](#integration-guide)
9. [API Reference](#api-reference)

---

## Overview

The Honua Onboarding System provides a comprehensive first-time user experience through:

- **Interactive Guided Tours**: Step-by-step walkthroughs using Shepherd.js
- **Onboarding Checklist**: Progress tracking for key onboarding tasks
- **Sample Data**: Pre-loaded datasets for exploration and testing
- **Tour Management UI**: Admin interface for managing and creating tours

### Key Features

✅ **5 Pre-Built Tours**:
- Welcome Tour (platform introduction)
- Map Creation Tour
- Data Upload Tour
- Dashboard Creation Tour
- Sharing & Collaboration Tour

✅ **Smart Progress Tracking**:
- LocalStorage-based tour completion tracking
- Onboarding checklist with per-item status
- Progress indicators and milestone celebrations

✅ **Interactive Elements**:
- Element highlighting with spotlight effect
- Tooltips and popovers with navigation
- Progress dots and step indicators
- Confetti celebration on completion

✅ **Flexible & Extensible**:
- JSON-based tour definitions
- Programmatic tour creation
- Custom tour builder UI
- Easy integration into existing pages

---

## Architecture

### Technology Stack

- **Frontend Framework**: Blazor Server (.NET 9.0)
- **UI Components**: MudBlazor
- **Tour Library**: Shepherd.js v11.2.0
- **State Management**: Services with scoped lifecycle
- **Storage**: Browser LocalStorage (via JSInterop)

### File Structure

```
src/Honua.Admin.Blazor/
├── wwwroot/
│   └── js/
│       └── tour-framework.js          # Shepherd.js wrapper & tour engine
├── Services/
│   ├── TourService.cs                 # Tour management service
│   ├── OnboardingService.cs           # Onboarding progress tracking
│   ├── TourDefinitions.cs             # Pre-built tour definitions
│   └── SampleDataLoader.cs            # Sample data management
├── Components/
│   └── Shared/
│       └── OnboardingChecklist.razor  # Checklist UI component
├── Pages/
│   ├── TourManagement.razor           # Tour admin UI
│   └── Home.razor                     # Dashboard with onboarding
└── Pages/
    └── _Layout.cshtml                 # Shepherd.js CDN references
```

---

## Components

### OnboardingChecklist.razor

**Location**: `Components/Shared/OnboardingChecklist.razor`

Interactive checklist component that displays user onboarding progress.

**Features**:
- Categorized task list (Getting Started, Data Management, Visualization, etc.)
- Checkbox completion tracking
- Quick action buttons (Start Tour, Go There)
- Progress bar with percentage
- Completion celebration
- Dismissible UI

**Usage**:

```razor
@page "/"
@using Honua.Admin.Blazor.Services

@inject OnboardingService OnboardingService

@if (await OnboardingService.ShouldShowOnboardingAsync())
{
    <OnboardingChecklist />
}
```

**Props**: None (uses injected services)

**Events**:
- Auto-updates on progress changes
- Listens to tour completion events
- Syncs with OnboardingService state

---

## Services

### 1. TourService

**Location**: `Services/TourService.cs`

Manages interactive guided tours using Shepherd.js.

#### Key Methods

```csharp
// Start a tour
Task StartTourAsync(string tourId, TourConfiguration config)

// Cancel active tour
Task CancelActiveTourAsync()

// Check completion status
Task<bool> IsTourCompletedAsync(string tourId)

// Reset tour completion
Task ResetTourAsync(string tourId)
Task ResetAllToursAsync()

// Get progress statistics
Task<TourProgress> GetTourProgressAsync()

// Get completed tours
Task<List<string>> GetCompletedToursAsync()
```

#### Tour Configuration Model

```csharp
public class TourConfiguration
{
    public List<TourStep> Steps { get; set; }
    public bool UseModalOverlay { get; set; } = true;
    public Dictionary<string, object>? DefaultStepOptions { get; set; }
    public string? OnCompleteFunction { get; set; }
    public string? OnCancelFunction { get; set; }
}

public class TourStep
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Text { get; set; }  // HTML content
    public TourStepAttachment? AttachTo { get; set; }
    public string? BeforeShowFunction { get; set; }
    public List<TourButton>? CustomButtons { get; set; }
}
```

#### Example Usage

```csharp
@inject TourService TourService

private async Task StartMyTour()
{
    var tour = new TourConfiguration
    {
        Steps = new List<TourStep>
        {
            new()
            {
                Id = "step1",
                Title = "Welcome!",
                Text = "<p>This is your first step.</p>",
                AttachTo = new TourStepAttachment
                {
                    Element = ".my-button",
                    Position = TourStepPosition.Bottom
                }
            }
        }
    };

    await TourService.StartTourAsync("my-tour", tour);
}
```

---

### 2. OnboardingService

**Location**: `Services/OnboardingService.cs`

Tracks user onboarding progress and checklist completion.

#### Key Methods

```csharp
// Get current progress
Task<OnboardingProgress> GetProgressAsync()

// Mark item as completed/incomplete
Task CompleteItemAsync(string itemId)
Task UncompleteItemAsync(string itemId)

// Reset progress
Task ResetProgressAsync()

// Show/hide checklist
Task DismissChecklistAsync()
Task ShowChecklistAsync()

// Check if onboarding should be shown
Task<bool> ShouldShowOnboardingAsync()
```

#### Onboarding Progress Model

```csharp
public class OnboardingProgress
{
    public List<OnboardingChecklistItem> ChecklistItems { get; set; }
    public DateTimeOffset StartedAt { get; set; }
    public bool IsDismissed { get; set; }

    // Computed properties
    public int CompletedItemsCount { get; }
    public int TotalItemsCount { get; }
    public int ProgressPercentage { get; }
    public bool IsFullyCompleted { get; }
}

public class OnboardingChecklistItem
{
    public string Id { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }
    public string? ActionUrl { get; set; }  // Navigation target
    public string? TourId { get; set; }     // Associated tour
    public string Category { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}
```

#### Default Checklist Items

1. **Getting Started**
   - Complete Welcome Tour
   - Create First Service

2. **Data Management**
   - Upload Spatial Data

3. **Visualization**
   - Create a Map
   - Build a Dashboard

4. **Collaboration**
   - Invite Team Members

5. **Advanced**
   - Explore the API

---

### 3. TourDefinitions

**Location**: `Services/TourDefinitions.cs`

Static class containing all pre-built tour configurations.

#### Available Tours

```csharp
// Get a specific tour
var welcomeTour = TourDefinitions.WelcomeTour;
var mapTour = TourDefinitions.MapCreationTour;
var uploadTour = TourDefinitions.DataUploadTour;
var dashboardTour = TourDefinitions.DashboardTour;
var sharingTour = TourDefinitions.SharingTour;

// Get all tours
var allTours = TourDefinitions.GetAllTours();

// Get tour by ID
var tour = TourDefinitions.GetTourById("welcome-tour");
```

---

### 4. SampleDataLoader

**Location**: `Services/SampleDataLoader.cs`

Manages sample datasets, example maps, and dashboard templates.

#### Key Methods

```csharp
// Get available sample datasets
Task<List<SampleDataset>> GetSampleDatasetsAsync()

// Get example maps
Task<List<ExampleMap>> GetExampleMapsAsync()

// Get dashboard templates
Task<List<DashboardTemplate>> GetDashboardTemplatesAsync()

// Import sample data
Task<bool> ImportSampleDatasetAsync(string datasetId)

// Create example map
Task<bool> CreateExampleMapAsync(string mapId)

// Create dashboard from template
Task<bool> CreateDashboardFromTemplateAsync(string templateId)
```

#### Available Sample Datasets

1. **World Cities** (GeoJSON, 15K features)
   - Population data
   - Proportional circle styling

2. **Country Boundaries** (GeoJSON, 177 features)
   - Demographics
   - Choropleth styling

3. **Recent Earthquakes** (GeoJSON, ~8K features)
   - USGS real-time data
   - Magnitude-based styling

4. **Major Roads** (GeoJSON, 12K features)
   - Transportation networks
   - Line styling

5. **National Parks** (GeoJSON, 4.5K features)
   - Protected areas
   - Polygon fills

---

## Pre-Built Tours

### 1. Welcome Tour (`welcome-tour`)

**Steps**: 6 steps
**Duration**: ~2 minutes
**Auto-start**: Yes (for first-time users)

**Coverage**:
- Platform overview
- Navigation menu
- Global search
- Theme toggle
- License tier badge
- Next steps

**Trigger**:
```csharp
var tour = TourDefinitions.WelcomeTour;
await TourService.StartTourAsync("welcome-tour", tour);
```

---

### 2. Map Creation Tour (`map-creation-tour`)

**Steps**: 6 steps
**Duration**: ~3 minutes
**Auto-start**: No

**Coverage**:
- Maps overview
- Creating new maps
- Adding layers
- Styling layers
- Sharing maps
- Best practices

**Best Used**: On `/maps` page

---

### 3. Data Upload Tour (`data-upload-tour`)

**Steps**: 6 steps
**Duration**: ~3 minutes
**Auto-start**: No

**Coverage**:
- Supported formats
- Drag & drop upload
- Import configuration
- Progress tracking
- Data availability
- API access

**Best Used**: On `/import` page

---

### 4. Dashboard Tour (`dashboard-tour`)

**Steps**: 7 steps
**Duration**: ~3 minutes
**Auto-start**: No

**Coverage**:
- Dashboard widgets overview
- Widget types
- Adding widgets
- Widget configuration
- Layout arrangement
- Sharing dashboards
- Pro tips

**Best Used**: On dashboard pages

---

### 5. Sharing & Collaboration Tour (`sharing-tour`)

**Steps**: 6 steps
**Duration**: ~2.5 minutes
**Auto-start**: No

**Coverage**:
- Team member invitation
- Role-based access control
- Public sharing
- API access
- Embedding
- Best practices

**Best Used**: On `/users` or `/roles` pages

---

## Creating Custom Tours

### Method 1: Programmatic Tour Creation

Create tours directly in code:

```csharp
var customTour = new TourConfiguration
{
    Steps = new List<TourStep>
    {
        new()
        {
            Id = "intro",
            Title = "Welcome to Custom Feature",
            Text = @"
                <p>This tour will guide you through our custom feature.</p>
                <ul>
                    <li>Feature A</li>
                    <li>Feature B</li>
                </ul>
            "
        },
        new()
        {
            Id = "feature-button",
            Title = "Click Here to Start",
            Text = "This button activates the feature.",
            AttachTo = new TourStepAttachment
            {
                Element = "#my-feature-button",
                Position = TourStepPosition.Bottom
            }
        },
        new()
        {
            Id = "settings",
            Title = "Configure Settings",
            Text = "Adjust these settings to customize behavior.",
            AttachTo = new TourStepAttachment
            {
                Element = ".settings-panel",
                Position = TourStepPosition.Left
            }
        }
    ],
    UseModalOverlay = true
};

await TourService.StartTourAsync("custom-feature-tour", customTour);
```

### Method 2: Add to TourDefinitions

For reusable tours, add them to `TourDefinitions.cs`:

```csharp
public static class TourDefinitions
{
    // ... existing tours ...

    public static TourConfiguration CustomFeatureTour => new()
    {
        Steps = new List<TourStep>
        {
            // Your steps here
        },
        UseModalOverlay = true
    };

    public static Dictionary<string, TourConfiguration> GetAllTours() => new()
    {
        // ... existing tours ...
        { "custom-feature-tour", CustomFeatureTour }
    };
}
```

### Best Practices for Custom Tours

1. **Keep steps concise**: 5-8 steps is ideal
2. **Use HTML for formatting**: Lists, bold, code blocks
3. **Test element selectors**: Ensure CSS selectors are stable
4. **Provide context**: Explain "why" not just "what"
5. **Include next steps**: Guide users to related features
6. **Add to checklist**: Link tours to onboarding items when relevant

---

## Sample Data System

### Using Sample Data in Tours

Link sample data to onboarding tasks:

```csharp
// In OnboardingService, add a checklist item
new OnboardingChecklistItem
{
    Id = "explore-sample-data",
    Title = "Explore Sample Data",
    Description = "Load and visualize sample earthquake data",
    ActionUrl = "/import",
    TourId = "data-upload-tour",
    Category = "Getting Started"
}
```

### Programmatic Sample Data Loading

```csharp
@inject SampleDataLoader SampleData

private async Task LoadSampleDataAsync()
{
    // Get available datasets
    var datasets = await SampleData.GetSampleDatasetsAsync();

    // Import a specific dataset
    var success = await SampleData.ImportSampleDatasetAsync("sample-cities");

    if (success)
    {
        // Mark onboarding item complete
        await OnboardingService.CompleteItemAsync("upload-data");
    }
}
```

### Creating Example Content

```csharp
// Create an example map
await SampleData.CreateExampleMapAsync("example-population-density");

// Create a dashboard from template
await SampleData.CreateDashboardFromTemplateAsync("template-analytics");
```

---

## Integration Guide

### Adding Onboarding to a New Page

**Step 1**: Import required services

```razor
@using Honua.Admin.Blazor.Services
@inject TourService TourService
@inject OnboardingService OnboardingService
```

**Step 2**: Add tour trigger button

```razor
<MudButton StartIcon="@Icons.Material.Filled.Tour"
           Color="Color.Primary"
           OnClick="StartPageTourAsync">
    Take a Tour
</MudButton>
```

**Step 3**: Implement tour logic

```razor
@code {
    private async Task StartPageTourAsync()
    {
        var tour = TourDefinitions.GetTourById("my-page-tour");
        if (tour != null)
        {
            await TourService.StartTourAsync("my-page-tour", tour);
        }
    }

    protected override async Task OnInitializedAsync()
    {
        // Auto-start tour for first-time visitors (optional)
        var completed = await TourService.IsTourCompletedAsync("my-page-tour");
        if (!completed)
        {
            await Task.Delay(1000); // Let page render
            await StartPageTourAsync();
        }
    }
}
```

### Linking Tours to Checklist Items

When a tour completes, automatically mark the related checklist item:

```csharp
protected override async Task OnInitializedAsync()
{
    TourService.OnTourCompleted += async (tourId) =>
    {
        if (tourId == "map-creation-tour")
        {
            await OnboardingService.CompleteItemAsync("create-map");
        }
    };
}
```

The `OnboardingChecklist` component already does this automatically by listening to `TourService.OnTourCompleted` events.

---

## API Reference

### JavaScript API (window.HonuaTours)

The tour framework exposes a JavaScript API for advanced scenarios:

```javascript
// Initialize the tour system
window.HonuaTours.initialize();

// Create and start a tour
const tour = window.HonuaTours.createTour({
    id: 'my-tour',
    steps: [
        {
            id: 'step1',
            title: 'Step 1',
            text: 'Description',
            attachTo: {
                element: '.my-element',
                on: 'bottom'
            }
        }
    ]
});
tour.start();

// Check if tour is completed
const isCompleted = window.HonuaTours.isTourCompleted('my-tour');

// Reset tour
window.HonuaTours.resetTour('my-tour');

// Get progress
const progress = window.HonuaTours.getTourProgress();
// { completed: 2, total: 5, percentage: 40 }
```

### LocalStorage Keys

The system uses these LocalStorage keys:

- `honua-completed-tours`: Array of completed tour IDs
- `honua-onboarding-progress`: JSON object with checklist progress

**Example**:

```javascript
// Manually access tour completion data
const completed = JSON.parse(localStorage.getItem('honua-completed-tours') || '[]');
console.log('Completed tours:', completed);

// Manually access onboarding progress
const progress = JSON.parse(localStorage.getItem('honua-onboarding-progress'));
console.log('Onboarding progress:', progress);
```

---

## Customization

### Styling Tours

Override CSS variables in your theme:

```css
/* Custom tour colors */
:root {
    --shepherd-primary: #1976d2;
    --shepherd-success: #4caf50;
}

/* Custom spotlight effect */
.honua-tour-highlight {
    box-shadow: 0 0 0 6px rgba(25, 118, 210, 0.5),
                0 0 0 99999px rgba(0, 0, 0, 0.6);
}
```

### Custom Confetti Colors

Modify in `tour-framework.js`:

```javascript
celebrateCompletion: function() {
    const colors = ['#FF5733', '#33FF57', '#3357FF', '#F333FF'];
    // ... confetti logic
}
```

### Disable Auto-Start Tours

In `Home.razor`, remove or comment out:

```csharp
// Auto-start welcome tour for first-time users
var hasCompletedWelcomeTour = await TourService.IsTourCompletedAsync("welcome-tour");
if (!hasCompletedWelcomeTour && _shouldShowOnboarding)
{
    // await StartWelcomeTourAsync(); // Disabled
}
```

---

## Troubleshooting

### Tours Not Starting

**Problem**: Tour doesn't appear when triggered

**Solutions**:
1. Check browser console for JavaScript errors
2. Verify Shepherd.js is loaded: `typeof Shepherd !== 'undefined'`
3. Ensure target elements exist: `document.querySelector('.my-element')`
4. Check if another tour is active: `window.HonuaTours.activeTour`

### Element Highlighting Not Working

**Problem**: Target elements aren't highlighted

**Solutions**:
1. Use specific CSS selectors (ID or class)
2. Ensure elements are visible (not `display: none`)
3. Check z-index conflicts
4. Verify `attachTo.element` selector is correct

### Progress Not Saving

**Problem**: Tour completion not persisting

**Solutions**:
1. Check LocalStorage is enabled in browser
2. Verify no browser extensions blocking storage
3. Check for localStorage quota errors in console
4. Test in incognito mode to rule out storage issues

### Onboarding Checklist Not Showing

**Problem**: Checklist doesn't appear on dashboard

**Solutions**:
1. Check `_shouldShowOnboarding` is true
2. Verify `OnboardingService.ShouldShowOnboardingAsync()` logic
3. Ensure checklist hasn't been dismissed
4. Check for component rendering errors in console

---

## Advanced Scenarios

### Context-Aware Tours

Adapt tours based on user state:

```csharp
private async Task StartAdaptiveTourAsync()
{
    var hasServices = await ServiceApi.GetServicesAsync();
    var tour = hasServices.Any()
        ? TourDefinitions.MapCreationTour  // User has data
        : TourDefinitions.DataUploadTour;  // User needs to upload first

    await TourService.StartTourAsync("adaptive-tour", tour);
}
```

### Multi-Page Tours

Create tours that navigate between pages:

```csharp
new TourStep
{
    Id = "navigate-step",
    Title = "Let's Go to Maps",
    Text = "Click Next to navigate to the Maps page.",
    BeforeShowFunction = "() => window.location.href = '/maps'"
}
```

### Tour Analytics

Track tour engagement:

```csharp
TourService.OnTourCompleted += async (tourId) =>
{
    // Log completion to analytics
    await AnalyticsService.TrackEventAsync("tour_completed", new
    {
        tour_id = tourId,
        timestamp = DateTime.UtcNow
    });
};
```

---

## Migration & Maintenance

### Updating Tour Content

When UI changes, update tour definitions:

1. **Test existing tours** after UI updates
2. **Update CSS selectors** if elements changed
3. **Refresh screenshots** in documentation
4. **Version tour definitions** for rollback

### Adding New Checklist Items

Extend `OnboardingService.CreateDefaultProgress()`:

```csharp
new OnboardingChecklistItem
{
    Id = "new-feature",
    Title = "Try New Feature",
    Description = "Explore our latest feature",
    Icon = "NewIcon",
    ActionUrl = "/new-feature",
    TourId = "new-feature-tour",
    Category = "Advanced"
}
```

**Note**: Existing users won't see new items automatically. Consider:
- Showing a "What's New" notification
- Adding items on next login
- Providing a "Check for Updates" button

---

## Future Enhancements

Potential improvements for the onboarding system:

- [ ] **Tour Builder UI**: Visual editor for creating tours
- [ ] **Video Tours**: Embedded video walkthroughs
- [ ] **Interactive Tutorials**: Hands-on exercises with validation
- [ ] **Personalized Paths**: Role-based onboarding flows
- [ ] **Gamification**: Badges, points, and achievements
- [ ] **Tour Scheduling**: Time-based tour suggestions
- [ ] **A/B Testing**: Experiment with different tour approaches
- [ ] **Analytics Dashboard**: Tour completion rates and drop-off points
- [ ] **Localization**: Multi-language tour support
- [ ] **Accessibility**: Screen reader support for tours

---

## Support

For questions or issues with the onboarding system:

1. Check this documentation
2. Review example tours in `TourDefinitions.cs`
3. Inspect browser console for JavaScript errors
4. Test in the Tour Management UI (`/tours`)
5. Contact the development team

---

## License

Copyright (c) 2025 HonuaIO
Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
