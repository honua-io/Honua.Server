# Accessibility Guide

This guide covers accessibility best practices for Honua.MapSDK applications, ensuring WCAG 2.1 compliance and inclusive user experiences.

---

## Table of Contents

1. [WCAG Compliance](#wcag-compliance)
2. [Keyboard Navigation](#keyboard-navigation)
3. [Screen Readers](#screen-readers)
4. [High Contrast Mode](#high-contrast-mode)
5. [Focus Management](#focus-management)
6. [ARIA Labels and Roles](#aria-labels-and-roles)

---

## WCAG Compliance

### WCAG 2.1 AA Checklist

#### Perceivable

- [ ] **Text Alternatives**: All images and icons have alt text
- [ ] **Captions**: Videos have captions or transcripts
- [ ] **Adaptable**: Content can be presented in different ways
- [ ] **Distinguishable**: Use sufficient color contrast (4.5:1 for normal text)

#### Operable

- [ ] **Keyboard Accessible**: All functionality available via keyboard
- [ ] **Enough Time**: Users have enough time to read and use content
- [ ] **Seizures**: No content flashes more than 3 times per second
- [ ] **Navigable**: Provide ways to navigate and find content

#### Understandable

- [ ] **Readable**: Text is readable and understandable
- [ ] **Predictable**: Pages appear and operate in predictable ways
- [ ] **Input Assistance**: Help users avoid and correct mistakes

#### Robust

- [ ] **Compatible**: Content is compatible with current and future tools

---

## Keyboard Navigation

### Map Keyboard Controls

```razor
<HonuaMap @ref="_map"
          Id="accessible-map"
          OnMapReady="@HandleMapReady"
          EnableKeyboardNavigation="true"
          TabIndex="0"
          @onkeydown="@HandleKeyDown" />

@code {
    private HonuaMap? _map;

    private async Task HandleKeyDown(KeyboardEventArgs e)
    {
        const double panAmount = 50;
        const double zoomStep = 1;

        switch (e.Key)
        {
            case "ArrowUp":
                await _map!.PanAsync(0, -panAmount);
                break;
            case "ArrowDown":
                await _map!.PanAsync(0, panAmount);
                break;
            case "ArrowLeft":
                await _map!.PanAsync(-panAmount, 0);
                break;
            case "ArrowRight":
                await _map!.PanAsync(panAmount, 0);
                break;
            case "+":
            case "=":
                await _map!.ZoomInAsync(zoomStep);
                break;
            case "-":
                await _map!.ZoomOutAsync(zoomStep);
                break;
            case "Home":
                await _map!.ResetViewAsync();
                break;
            case "Escape":
                await _map!.ClearSelectionAsync();
                break;
        }
    }

    private async Task HandleMapReady(MapReadyMessage message)
    {
        // Show keyboard shortcuts
        await ShowKeyboardShortcuts();
    }

    private async Task ShowKeyboardShortcuts()
    {
        Snackbar.Add(@"
            Keyboard shortcuts:
            Arrow keys: Pan map
            +/-: Zoom in/out
            Home: Reset view
            Esc: Clear selection
        ", Severity.Info, config => config.VisibleStateDuration = 10000);
    }
}
```

### Component Keyboard Navigation

```razor
<HonuaDataGrid TItem="Feature"
               Items="@_features"
               EnableKeyboardNavigation="true"
               OnKeyDown="@HandleGridKeyDown">
    <Columns>
        <PropertyColumn Property="f => f.Name" />
        <PropertyColumn Property="f => f.Type" />
    </Columns>
</HonuaDataGrid>

@code {
    private async Task HandleGridKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Enter" && _selectedFeature != null)
        {
            await ShowFeatureDetails(_selectedFeature);
        }
        else if (e.Key == "Delete" && _selectedFeature != null)
        {
            await DeleteFeature(_selectedFeature);
        }
    }
}
```

### Skip Links

```razor
<a href="#main-content" class="skip-link">Skip to main content</a>
<a href="#map-controls" class="skip-link">Skip to map controls</a>

<style>
    .skip-link {
        position: absolute;
        top: -40px;
        left: 0;
        background: #000;
        color: #fff;
        padding: 8px;
        z-index: 100;
        text-decoration: none;
    }

    .skip-link:focus {
        top: 0;
    }
</style>

<main id="main-content" tabindex="-1">
    <HonuaMap Id="main-map" />
</main>

<div id="map-controls" tabindex="-1">
    <MudButton>Controls</MudButton>
</div>
```

---

## Screen Readers

### ARIA Labels

```razor
<!-- Map with ARIA labels -->
<div role="application" aria-label="Interactive map">
    <HonuaMap @ref="_map"
              Id="screen-reader-map"
              aria-label="Interactive map showing property locations"
              aria-describedby="map-description" />
</div>

<div id="map-description" class="sr-only">
    Use arrow keys to pan the map. Press + or - to zoom. Press Enter to select a feature.
</div>

<!-- Data Grid with ARIA -->
<HonuaDataGrid TItem="Feature"
               Items="@_features"
               role="grid"
               aria-label="Property listings"
               aria-rowcount="@_features.Count">
    <Columns>
        <PropertyColumn Property="f => f.Name" aria-label="Property name" />
        <PropertyColumn Property="f => f.Price" aria-label="Property price" />
    </Columns>
</HonuaDataGrid>

<style>
    .sr-only {
        position: absolute;
        width: 1px;
        height: 1px;
        padding: 0;
        margin: -1px;
        overflow: hidden;
        clip: rect(0, 0, 0, 0);
        white-space: nowrap;
        border-width: 0;
    }
</style>
```

### Live Regions

```razor
<!-- Announce map updates to screen readers -->
<div aria-live="polite" aria-atomic="true" class="sr-only">
    @_announceText
</div>

@code {
    private string _announceText = "";

    private async Task HandleExtentChanged(MapExtentChangedMessage message)
    {
        // Announce zoom level changes
        _announceText = $"Map zoomed to level {message.Zoom:F1}";
        StateHasChanged();

        // Clear announcement after delay
        await Task.Delay(1000);
        _announceText = "";
        StateHasChanged();
    }

    private async Task HandleFeatureSelected(Feature feature)
    {
        _announceText = $"Selected {feature.Name}, {feature.Type}. Press Enter for details.";
        StateHasChanged();
    }
}
```

### Descriptive Alternative Text

```razor
<!-- Good: Descriptive alt text -->
<img src="/images/property.jpg"
     alt="Three-story Victorian house with blue exterior and white trim" />

<!-- Bad: Non-descriptive alt text -->
<img src="/images/property.jpg" alt="Property image" />

<!-- Map features with descriptions -->
@foreach (var feature in _features)
{
    <button @onclick="@(() => SelectFeature(feature))"
            aria-label="@GetFeatureDescription(feature)">
        <MudIcon Icon="@GetFeatureIcon(feature)" />
    </button>
}

@code {
    private string GetFeatureDescription(Feature feature)
    {
        return $"{feature.Name}, {feature.Type}, located at {feature.Address}. " +
               $"Price: {feature.Price:C}, {feature.Bedrooms} bedrooms, {feature.Bathrooms} bathrooms";
    }
}
```

---

## High Contrast Mode

### Detect High Contrast

```javascript
// wwwroot/js/accessibility.js
window.detectHighContrast = function() {
    const div = document.createElement('div');
    div.style.backgroundColor = 'rgb(0, 255, 0)';
    div.style.color = 'rgb(255, 0, 0)';
    document.body.appendChild(div);

    const computedStyle = window.getComputedStyle(div);
    const isHighContrast =
        computedStyle.backgroundColor === computedStyle.color;

    document.body.removeChild(div);
    return isHighContrast;
};
```

### High Contrast Styles

```razor
@inject IJSRuntime JS

@code {
    private bool _isHighContrast = false;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            _isHighContrast = await JS.InvokeAsync<bool>("detectHighContrast");
            StateHasChanged();
        }
    }
}

<style>
    @media (prefers-contrast: high) {
        .honua-map {
            border: 3px solid #000;
        }

        .map-marker {
            border: 2px solid #000;
            background: #fff;
        }

        .selected {
            outline: 3px solid #000;
            outline-offset: 2px;
        }

        button {
            border: 2px solid #000;
        }

        button:focus {
            outline: 3px solid #000;
            outline-offset: 2px;
        }
    }

    /* High contrast map styles */
    .high-contrast-map {
        filter: contrast(1.5) brightness(1.2);
    }

    .high-contrast-map .map-layer-fill {
        stroke: #000;
        stroke-width: 2px;
    }
</style>

<div class="@(_isHighContrast ? "high-contrast-map" : "")">
    <HonuaMap Id="contrast-map" />
</div>
```

### Color Contrast Compliance

```csharp
// Ensure sufficient color contrast (WCAG AA requires 4.5:1)
public static bool HasSufficientContrast(string foreground, string background)
{
    var fgColor = ParseColor(foreground);
    var bgColor = ParseColor(background);

    var fgLuminance = GetRelativeLuminance(fgColor);
    var bgLuminance = GetRelativeLuminance(bgColor);

    var contrast = (Math.Max(fgLuminance, bgLuminance) + 0.05) /
                   (Math.Min(fgLuminance, bgLuminance) + 0.05);

    return contrast >= 4.5; // WCAG AA for normal text
}

private static double GetRelativeLuminance((int r, int g, int b) color)
{
    double RsRGB = color.r / 255.0;
    double GsRGB = color.g / 255.0;
    double BsRGB = color.b / 255.0;

    double R = RsRGB <= 0.03928 ? RsRGB / 12.92 : Math.Pow((RsRGB + 0.055) / 1.055, 2.4);
    double G = GsRGB <= 0.03928 ? GsRGB / 12.92 : Math.Pow((GsRGB + 0.055) / 1.055, 2.4);
    double B = BsRGB <= 0.03928 ? BsRGB / 12.92 : Math.Pow((BsRGB + 0.055) / 1.055, 2.4);

    return 0.2126 * R + 0.7152 * G + 0.0722 * B;
}
```

---

## Focus Management

### Focus Indicators

```css
/* Clear focus indicators */
*:focus {
    outline: 3px solid #4A90E2;
    outline-offset: 2px;
}

/* Remove default outline for mouse users */
*:focus:not(:focus-visible) {
    outline: none;
}

/* Show outline for keyboard users */
*:focus-visible {
    outline: 3px solid #4A90E2;
    outline-offset: 2px;
}

/* Custom focus for map components */
.honua-map:focus {
    outline: 3px solid #4A90E2;
    outline-offset: -3px;
}

.map-marker:focus {
    transform: scale(1.2);
    z-index: 1000;
}

/* Button focus */
.mud-button:focus {
    box-shadow: 0 0 0 3px rgba(74, 144, 226, 0.5);
}
```

### Focus Trapping in Dialogs

```razor
@implements IDisposable

<MudDialog @bind-IsVisible="_isOpen" @ref="_dialog">
    <TitleContent>
        <MudText Typo="Typo.h6" id="dialog-title">Feature Details</MudText>
    </TitleContent>
    <DialogContent>
        <div role="document" aria-labelledby="dialog-title">
            <MudTextField @bind-Value="_feature.Name"
                          Label="Name"
                          @ref="_firstFocusElement" />
            <MudTextField @bind-Value="_feature.Description" Label="Description" />
        </div>
    </DialogContent>
    <DialogActions>
        <MudButton OnClick="@Cancel">Cancel</MudButton>
        <MudButton Color="Color.Primary" OnClick="@Save" @ref="_lastFocusElement">
            Save
        </MudButton>
    </DialogActions>
</MudDialog>

@code {
    private MudDialog? _dialog;
    private MudTextField<string>? _firstFocusElement;
    private MudButton? _lastFocusElement;
    private bool _isOpen;
    private ElementReference _previouslyFocusedElement;

    private async Task Open()
    {
        // Store previously focused element
        _previouslyFocusedElement = await JS.InvokeAsync<ElementReference>("document.activeElement");

        _isOpen = true;

        // Focus first element after render
        await Task.Delay(100);
        await _firstFocusElement!.FocusAsync();

        // Setup tab trap
        await SetupFocusTrap();
    }

    private async Task Close()
    {
        _isOpen = false;

        // Restore focus to previously focused element
        await JS.InvokeVoidAsync("eval", $"arguments[0].focus()", _previouslyFocusedElement);
    }

    private async Task SetupFocusTrap()
    {
        await JS.InvokeVoidAsync("setupFocusTrap", _firstFocusElement, _lastFocusElement);
    }

    public void Dispose()
    {
        // Cleanup focus trap
    }
}
```

```javascript
// wwwroot/js/focus-trap.js
window.setupFocusTrap = function(firstElement, lastElement) {
    lastElement.addEventListener('keydown', (e) => {
        if (e.key === 'Tab' && !e.shiftKey) {
            e.preventDefault();
            firstElement.focus();
        }
    });

    firstElement.addEventListener('keydown', (e) => {
        if (e.key === 'Tab' && e.shiftKey) {
            e.preventDefault();
            lastElement.focus();
        }
    });
};
```

---

## ARIA Labels and Roles

### Proper ARIA Usage

```razor
<!-- Navigation -->
<nav aria-label="Main navigation">
    <MudNavLink href="/" aria-current="@(IsCurrentPage("/") ? "page" : null)">
        Home
    </MudNavLink>
</nav>

<!-- Search -->
<div role="search">
    <MudTextField @bind-Value="_searchQuery"
                  Label="Search properties"
                  aria-label="Search for properties by name or address"
                  aria-describedby="search-help" />
    <MudText id="search-help" class="sr-only">
        Enter property name or address to search
    </MudText>
</div>

<!-- Status messages -->
<div role="status" aria-live="polite">
    @if (_isLoading)
    {
        <MudText>Loading properties...</MudText>
    }
</div>

<!-- Alert messages -->
<div role="alert" aria-live="assertive">
    @if (_error != null)
    {
        <MudAlert Severity="Severity.Error">@_error</MudAlert>
    }
</div>

<!-- Tabs -->
<MudTabs role="tablist" aria-label="Map views">
    <MudTabPanel Text="Map View" role="tab" aria-selected="@(_activeTab == 0)">
        <HonuaMap Id="map" />
    </MudTabPanel>
    <MudTabPanel Text="List View" role="tab" aria-selected="@(_activeTab == 1)">
        <HonuaDataGrid Items="@_features" />
    </MudTabPanel>
</MudTabs>

<!-- Expandable sections -->
<MudExpansionPanel Text="Advanced Filters"
                   role="region"
                   aria-label="Advanced filter options"
                   aria-expanded="@_filtersExpanded">
    <!-- Filter content -->
</MudExpansionPanel>

<!-- Progress indicators -->
<MudProgressLinear Value="@_progress"
                   role="progressbar"
                   aria-valuenow="@_progress"
                   aria-valuemin="0"
                   aria-valuemax="100"
                   aria-label="Upload progress" />
```

### Complex Widgets

```razor
<!-- Custom combobox with ARIA -->
<div role="combobox"
     aria-expanded="@_isDropdownOpen"
     aria-owns="listbox-id"
     aria-haspopup="listbox">
    <input type="text"
           @bind="_searchText"
           aria-autocomplete="list"
           aria-controls="listbox-id"
           aria-activedescendant="@_activeOptionId" />

    @if (_isDropdownOpen)
    {
        <ul id="listbox-id" role="listbox">
            @foreach (var option in _filteredOptions)
            {
                <li role="option"
                    id="@option.Id"
                    aria-selected="@(_selectedOption == option)"
                    @onclick="@(() => SelectOption(option))">
                    @option.Label
                </li>
            }
        </ul>
    }
</div>

<!-- Custom slider -->
<div role="slider"
     aria-label="Price range"
     aria-valuemin="0"
     aria-valuemax="5000000"
     aria-valuenow="@_priceValue"
     aria-valuetext="@_priceValue.ToString("C0")"
     tabindex="0"
     @onkeydown="@HandleSliderKeyDown">
    <div class="slider-track">
        <div class="slider-thumb" style="left: @GetThumbPosition()"></div>
    </div>
</div>
```

---

## Accessibility Testing Tools

### Automated Testing

```bash
# Install axe-core
npm install @axe-core/playwright

# Run accessibility tests
dotnet test --filter Category=Accessibility
```

```csharp
using Microsoft.Playwright;
using Xunit;

public class AccessibilityTests
{
    [Fact]
    public async Task Map_Page_Has_No_Accessibility_Violations()
    {
        await using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync();
        var page = await browser.NewPageAsync();

        await page.GotoAsync("https://localhost:5001/map");

        // Inject axe-core
        await page.AddScriptTagAsync(new() { Path = "node_modules/axe-core/axe.min.js" });

        // Run axe
        var violations = await page.EvaluateAsync<dynamic>(@"
            async () => {
                const results = await axe.run();
                return results.violations;
            }
        ");

        Assert.Empty(violations);
    }
}
```

---

## Accessibility Checklist

- [ ] All interactive elements are keyboard accessible
- [ ] Focus indicators are visible and clear
- [ ] Color contrast ratios meet WCAG AA standards
- [ ] All images have descriptive alt text
- [ ] ARIA labels used appropriately
- [ ] Screen reader announcements for dynamic content
- [ ] Skip links provided for main content
- [ ] Form fields have associated labels
- [ ] Error messages are descriptive and linked to fields
- [ ] Tables have proper headers
- [ ] Dialogs trap focus appropriately
- [ ] High contrast mode is supported
- [ ] Text can be resized to 200% without loss of functionality
- [ ] No keyboard traps exist
- [ ] Time limits can be extended or disabled

---

## Further Reading

- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)
- [ARIA Authoring Practices](https://www.w3.org/WAI/ARIA/apg/)
- [WebAIM Resources](https://webaim.org/resources/)

---

*Last Updated: 2025-11-06*
*MapSDK Version: 1.0.0*
