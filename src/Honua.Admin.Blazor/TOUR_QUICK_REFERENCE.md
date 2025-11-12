# Honua Tours & Onboarding - Quick Reference

Quick reference guide for developers working with the Honua onboarding system.

## Quick Start

### Add a Tour Button to Your Page

```razor
@inject TourService TourService

<MudButton StartIcon="@Icons.Material.Filled.Tour"
           OnClick="@(() => StartTourAsync("my-tour-id"))">
    Start Tour
</MudButton>

@code {
    private async Task StartTourAsync(string tourId)
    {
        var tour = TourDefinitions.GetTourById(tourId);
        if (tour != null)
        {
            await TourService.StartTourAsync(tourId, tour);
        }
    }
}
```

### Create a Simple Tour

```csharp
var tour = new TourConfiguration
{
    Steps = new List<TourStep>
    {
        new() { Title = "Welcome", Text = "<p>Hello!</p>" },
        new()
        {
            Title = "Click Here",
            Text = "<p>This button does something cool.</p>",
            AttachTo = new TourStepAttachment
            {
                Element = "#my-button",
                Position = TourStepPosition.Bottom
            }
        }
    }
};

await TourService.StartTourAsync("quick-tour", tour);
```

## Common Tasks

### Check if Tour is Completed

```csharp
var isCompleted = await TourService.IsTourCompletedAsync("welcome-tour");
```

### Show Onboarding Checklist

```razor
<OnboardingChecklist />
```

### Mark Checklist Item Complete

```csharp
await OnboardingService.CompleteItemAsync("create-first-service");
```

### Get Tour Progress

```csharp
var progress = await TourService.GetTourProgressAsync();
// progress.Completed, progress.Total, progress.Percentage
```

### Reset a Tour

```csharp
await TourService.ResetTourAsync("welcome-tour");
```

## Tour Step Positions

```csharp
TourStepPosition.Top
TourStepPosition.Bottom
TourStepPosition.Left
TourStepPosition.Right
TourStepPosition.Auto  // Automatically chooses best position
```

## HTML in Tour Steps

```csharp
Text = @"
    <p>Use HTML for rich formatting!</p>
    <ul>
        <li><strong>Bold text</strong></li>
        <li><em>Italic text</em></li>
        <li><code>Code snippets</code></li>
    </ul>
"
```

## Pre-Built Tour IDs

```csharp
"welcome-tour"           // Platform introduction
"map-creation-tour"      // How to create maps
"data-upload-tour"       // Importing data
"dashboard-tour"         // Building dashboards
"sharing-tour"           // Collaboration features
```

## Sample Data

```csharp
@inject SampleDataLoader SampleData

// Get available datasets
var datasets = await SampleData.GetSampleDatasetsAsync();

// Import sample data
await SampleData.ImportSampleDatasetAsync("sample-cities");

// Create example map
await SampleData.CreateExampleMapAsync("example-population-density");
```

## Onboarding Checklist Categories

- **Getting Started**: Introduction tasks
- **Data Management**: Data import and management
- **Visualization**: Maps and dashboards
- **Collaboration**: Sharing and team features
- **Advanced**: API and advanced features

## CSS Selectors for Tour Targets

### Good Selectors (Stable)
```
#my-unique-id
.specific-component-class
[data-tour-target="my-element"]
```

### Avoid (Fragile)
```
div > div > span  // Too specific, breaks easily
.mud-button      // Too generic, multiple matches
```

## Events

### Tour Completed Event

```csharp
protected override async Task OnInitializedAsync()
{
    TourService.OnTourCompleted += HandleTourCompleted;
}

private async Task HandleTourCompleted(string tourId)
{
    Console.WriteLine($"Tour completed: {tourId}");
}
```

### Onboarding Progress Changed

```csharp
protected override async Task OnInitializedAsync()
{
    OnboardingService.OnProgressChanged += HandleProgressChanged;
}

private async Task HandleProgressChanged()
{
    var progress = await OnboardingService.GetProgressAsync();
    StateHasChanged();
}
```

## Service Registration

Already registered in `Program.cs`:

```csharp
builder.Services.AddScoped<TourService>();
builder.Services.AddScoped<OnboardingService>();
builder.Services.AddScoped<SampleDataLoader>();
```

## JavaScript API

```javascript
// Check if tour completed
window.HonuaTours.isTourCompleted('my-tour');

// Get progress
window.HonuaTours.getTourProgress();

// Reset tour
window.HonuaTours.resetTour('my-tour');

// Cancel active tour
window.HonuaTours.cancelActiveTour();
```

## Customization

### Custom Button Layout

```csharp
CustomButtons = new List<TourButton>
{
    new() { Text = "Skip", Classes = "shepherd-button-secondary" },
    new() { Text = "Continue", Action = "next" }
}
```

### Disable Modal Overlay

```csharp
var tour = new TourConfiguration
{
    UseModalOverlay = false,  // No dark overlay
    Steps = ...
};
```

## Tour Management UI

Access at: `/tours`

Features:
- View all available tours
- Start any tour
- Check completion status
- Reset tour progress
- View onboarding statistics
- Manage sample data

## Tips

1. **Test selectors**: Use browser DevTools to verify CSS selectors
2. **Keep it short**: 5-7 steps is ideal
3. **Add delays**: Use `await Task.Delay(1000)` before auto-starting tours
4. **Provide context**: Explain "why" not just "what"
5. **Test on mobile**: Tours should work on all screen sizes
6. **Use categories**: Organize checklist items into logical groups

## Troubleshooting

### Tour not showing?
- Check console for errors
- Verify element selector exists
- Ensure Shepherd.js is loaded

### Progress not saving?
- Check LocalStorage is enabled
- Verify no browser extensions blocking storage

### Element not highlighting?
- Element must be visible (not `display: none`)
- Check z-index conflicts
- Use specific selectors

## Example: Complete Integration

```razor
@page "/my-feature"
@using Honua.Admin.Blazor.Services
@inject TourService TourService
@inject OnboardingService OnboardingService

<MudButton id="feature-button" OnClick="DoSomething">
    My Feature
</MudButton>

<MudButton StartIcon="@Icons.Material.Filled.Tour"
           OnClick="StartFeatureTourAsync">
    Take Tour
</MudButton>

@code {
    protected override async Task OnInitializedAsync()
    {
        // Auto-start tour for first-time users
        var completed = await TourService.IsTourCompletedAsync("my-feature-tour");
        if (!completed)
        {
            await Task.Delay(1000);
            await StartFeatureTourAsync();
        }
    }

    private async Task StartFeatureTourAsync()
    {
        var tour = new TourConfiguration
        {
            Steps = new List<TourStep>
            {
                new()
                {
                    Title = "Welcome to My Feature",
                    Text = "<p>Let me show you how this works!</p>"
                },
                new()
                {
                    Title = "This Button",
                    Text = "<p>Click here to activate the feature.</p>",
                    AttachTo = new TourStepAttachment
                    {
                        Element = "#feature-button",
                        Position = TourStepPosition.Bottom
                    }
                }
            }
        };

        await TourService.StartTourAsync("my-feature-tour", tour);
    }

    private void DoSomething()
    {
        // Feature logic
    }
}
```

---

**Full Documentation**: See `ONBOARDING_DOCUMENTATION.md`
