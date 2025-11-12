# Feature Flags & Licensing Integration Guide

**Version:** 1.0
**Date:** 2025-11-06
**Status:** Implementation Complete

---

## Overview

Honua.Server implements a comprehensive feature flag system integrated with licensing to control access to Enterprise features. This system ensures that users only see and can access features available in their license tier.

### Key Benefits

1. **Automatic UI Hiding** - Enterprise features are hidden from users without proper licensing
2. **Real-time Checks** - Feature availability is checked in real-time based on current license
3. **Graceful Degradation** - System defaults to Free tier when licensing service is unavailable
4. **Performance Optimized** - Aggressive caching (5-minute TTL) minimizes database queries

---

## License Tiers

### Free Tier
- **Vector Tiles**: ✅ Enabled
- **Max Users**: 1
- **Max Collections**: 10
- **API Requests/Day**: 10,000
- **Storage**: 5 GB

### Professional Tier
- **Vector Tiles**: ✅
- **Advanced Analytics**: ✅
- **Cloud Integrations**: ✅
- **STAC Catalog**: ✅
- **Raster Processing**: ✅
- **Max Users**: 10
- **Max Collections**: 100
- **API Requests/Day**: 100,000
- **Storage**: 100 GB

### Enterprise Tier (All Features)
- **All Professional Features**: ✅
- **GeoETL Workflows**: ✅ (Exclusive)
- **Versioning & Branching**: ✅ (Exclusive)
- **Oracle Database Support**: ✅ (Exclusive)
- **Elasticsearch Support**: ✅ (Exclusive)
- **Priority Support**: ✅
- **Max Users**: Unlimited
- **Max Collections**: Unlimited
- **API Requests/Day**: Unlimited
- **Storage**: Unlimited

---

## Architecture

### Server-Side Components

```
┌─────────────────────────────────────────────────────┐
│        LicenseStore (Database)                      │
│  - Stores license information                       │
│  - Tracks expiration and status                     │
└────────────────┬────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────┐
│    LicenseFeatureFlagService                        │
│  - Loads license from database                      │
│  - Caches for 5 minutes                             │
│  - Maps features to boolean flags                   │
└────────────────┬────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────┐
│    /admin/feature-flags API Endpoint                │
│  - Returns FeatureFlagState JSON                    │
│  - No authentication required for local dev         │
└────────────────┬────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────┐
│    Blazor FeatureFlagService (Client)               │
│  - Fetches flags from API                           │
│  - Caches for 5 minutes                             │
│  - Provides IsEnabledAsync() method                 │
└────────────────┬────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────┐
│    UI Components (Razor Pages)                      │
│  - Inject FeatureFlagService                        │
│  - Check flags before rendering                     │
│  - Hide/disable enterprise features                 │
└─────────────────────────────────────────────────────┘
```

---

## Usage Examples

### Example 1: Hide Navigation Item

```razor
@page "/navigation"
@inject FeatureFlagService FeatureFlags

<MudNavMenu>
    <!-- Always visible items -->
    <MudNavLink Href="/" Icon="@Icons.Material.Filled.Home">Home</MudNavLink>
    <MudNavLink Href="/layers" Icon="@Icons.Material.Filled.Layers">Layers</MudNavLink>

    @if (_showGeoEtl)
    {
        <!-- Enterprise feature: only shown if licensed -->
        <MudNavGroup Title="GeoETL" Icon="@Icons.Material.Filled.Transform" Expanded="false">
            <MudNavLink Href="/geoetl/workflows">Workflows</MudNavLink>
            <MudNavLink Href="/geoetl/designer">Workflow Designer</MudNavLink>
        </MudNavGroup>
    }

    @if (_showVersioning)
    {
        <!-- Enterprise feature: only shown if licensed -->
        <MudNavLink Href="/versioning" Icon="@Icons.Material.Filled.AccountTree">Versioning</MudNavLink>
    }
</MudNavMenu>

@code {
    private bool _showGeoEtl;
    private bool _showVersioning;

    protected override async Task OnInitializedAsync()
    {
        _showGeoEtl = await FeatureFlags.IsEnabledAsync("geoetl");
        _showVersioning = await FeatureFlags.IsEnabledAsync("versioning");
    }
}
```

### Example 2: Disable Button with Tooltip

```razor
@page "/layers/detail/{id}"
@inject FeatureFlagService FeatureFlags

<MudButton Variant="Variant.Filled"
           Color="Color.Primary"
           Disabled="!_canUseOracle"
           OnClick="MigrateToOracle">
    Migrate to Oracle
</MudButton>

@if (!_canUseOracle)
{
    <MudText Typo="Typo.caption" Color="Color.Error">
        Oracle support requires Enterprise license. <MudLink Href="/upgrade">Upgrade now</MudLink>
    </MudText>
}

@code {
    private bool _canUseOracle;

    protected override async Task OnInitializedAsync()
    {
        _canUseOracle = await FeatureFlags.IsEnabledAsync("oracle");
    }

    private async Task MigrateToOracle()
    {
        // Only called if _canUseOracle is true
        // Migration logic here
    }
}
```

### Example 3: Show License Badge

```razor
@inject FeatureFlagService FeatureFlags

<MudAppBar>
    <MudText Typo="Typo.h6">Honua Admin</MudText>
    <MudSpacer />

    @if (_flags != null)
    {
        <MudChip Color="@GetTierColor()" Size="Size.Small">
            @_flags.Tier Tier
        </MudChip>

        @if (_flags.DaysUntilExpiration <= 7)
        {
            <MudChip Color="Color.Warning" Size="Size.Small" Icon="@Icons.Material.Filled.Warning">
                Expires in @_flags.DaysUntilExpiration days
            </MudChip>
        }
    }
</MudAppBar>

@code {
    private FeatureFlagState? _flags;

    protected override async Task OnInitializedAsync()
    {
        _flags = await FeatureFlags.GetFeatureFlagsAsync();
    }

    private Color GetTierColor() => _flags?.Tier switch
    {
        LicenseTier.Enterprise => Color.Success,
        LicenseTier.Professional => Color.Info,
        _ => Color.Default
    };
}
```

### Example 4: Conditional Page Access

```razor
@page "/geoetl/workflows"
@inject FeatureFlagService FeatureFlags
@inject NavigationManager Navigation

<PageTitle>GeoETL Workflows - Enterprise Feature</PageTitle>

@if (_hasAccess)
{
    <MudText Typo="Typo.h4">GeoETL Workflows</MudText>
    <!-- Workflow content here -->
}
else
{
    <MudAlert Severity="Severity.Warning">
        <MudText Typo="Typo.h6">Enterprise Feature Required</MudText>
        <MudText>
            GeoETL workflows require an Enterprise license.
        </MudText>
        <MudButton Variant="Variant.Filled" Color="Color.Primary" Href="/upgrade" Class="mt-4">
            Upgrade to Enterprise
        </MudButton>
    </MudAlert>
}

@code {
    private bool _hasAccess;

    protected override async Task OnInitializedAsync()
    {
        _hasAccess = await FeatureFlags.IsEnabledAsync("geoetl");

        if (!_hasAccess)
        {
            // Optional: redirect to upgrade page
            // Navigation.NavigateTo("/upgrade");
        }
    }
}
```

### Example 5: Get All Feature Flags

```razor
@inject FeatureFlagService FeatureFlags

<MudSimpleTable Dense="true">
    <thead>
        <tr>
            <th>Feature</th>
            <th>Status</th>
        </tr>
    </thead>
    <tbody>
        @if (_flags != null)
        {
            <tr>
                <td>Tier</td>
                <td><MudChip Size="Size.Small">@_flags.Tier</MudChip></td>
            </tr>
            <tr>
                <td>GeoETL</td>
                <td><MudIcon Icon="@GetStatusIcon(_flags.GeoEtl)" /></td>
            </tr>
            <tr>
                <td>Versioning</td>
                <td><MudIcon Icon="@GetStatusIcon(_flags.Versioning)" /></td>
            </tr>
            <tr>
                <td>Oracle Support</td>
                <td><MudIcon Icon="@GetStatusIcon(_flags.OracleSupport)" /></td>
            </tr>
            <tr>
                <td>Elasticsearch</td>
                <td><MudIcon Icon="@GetStatusIcon(_flags.ElasticsearchSupport)" /></td>
            </tr>
            <tr>
                <td>Max Collections</td>
                <td>@(_flags.MaxCollections == 0 ? "Unlimited" : _flags.MaxCollections.ToString())</td>
            </tr>
        }
    </tbody>
</MudSimpleTable>

@code {
    private FeatureFlagState? _flags;

    protected override async Task OnInitializedAsync()
    {
        _flags = await FeatureFlags.GetFeatureFlagsAsync();
    }

    private string GetStatusIcon(bool enabled) =>
        enabled ? Icons.Material.Filled.CheckCircle : Icons.Material.Filled.Cancel;
}
```

---

## API Reference

### FeatureFlagService Methods

#### `IsEnabledAsync(string featureName)`
Checks if a specific feature is enabled.

```csharp
bool isEnabled = await FeatureFlags.IsEnabledAsync("geoetl");
```

**Supported Feature Names** (case-insensitive):
- `geoetl`, `geo-etl`, `etl` → GeoETL workflows
- `versioning`, `branching` → Versioning and branching
- `oracle`, `oracle-support` → Oracle database support
- `elasticsearch`, `elasticsearch-support` → Elasticsearch support
- `advanced-analytics` → Advanced analytics
- `cloud-integrations` → Cloud integrations
- `stac`, `stac-catalog` → STAC catalog
- `raster`, `raster-processing` → Raster processing
- `vector-tiles` → Vector tile generation

#### `GetFeatureFlagsAsync()`
Gets all feature flags as a FeatureFlagState object.

```csharp
FeatureFlagState? flags = await FeatureFlags.GetFeatureFlagsAsync();
if (flags != null && flags.GeoEtl)
{
    // Show GeoETL features
}
```

#### `ClearCache()`
Clears the client-side cache, forcing a reload on next access.

```csharp
FeatureFlags.ClearCache();
```

---

## Server-Side API Endpoints

### GET /admin/feature-flags
Returns the current feature flag state.

**Response:**
```json
{
  "tier": "Enterprise",
  "isValid": true,
  "daysUntilExpiration": 365,
  "advancedAnalytics": true,
  "cloudIntegrations": true,
  "stacCatalog": true,
  "rasterProcessing": true,
  "vectorTiles": true,
  "prioritySupport": true,
  "geoEtl": true,
  "versioning": true,
  "oracleSupport": true,
  "elasticsearchSupport": true,
  "maxUsers": 0,
  "maxCollections": 0,
  "maxApiRequestsPerDay": 0,
  "maxStorageGb": 0
}
```

### GET /admin/feature-flags/{featureName}
Checks if a specific feature is enabled.

**Example:** `GET /admin/feature-flags/geoetl`

**Response:**
```json
{
  "feature": "geoetl",
  "enabled": true
}
```

---

## Testing

### Testing Without License
When no license is found, the system defaults to Free tier:
- Only VectorTiles enabled
- Limited quotas
- No enterprise features

### Testing with Mock License
Create a test license in the database:

```sql
INSERT INTO licenses (id, customer_id, license_key, tier, status, issued_at, expires_at, features, email)
VALUES (
    gen_random_uuid(),
    'test-customer',
    'mock-jwt-key',
    'Enterprise',
    'Active',
    NOW(),
    NOW() + INTERVAL '365 days',
    '{"GeoEtl": true, "Versioning": true, "OracleSupport": true, "ElasticsearchSupport": true, "MaxUsers": 0, "MaxCollections": 0}',
    'test@example.com'
);
```

### Clear Cache During Testing
```csharp
@inject FeatureFlagService FeatureFlags

@code {
    private async Task RefreshLicense()
    {
        FeatureFlags.ClearCache();
        await OnInitializedAsync(); // Reload
    }
}
```

---

## Best Practices

1. **Check Features Early**: Check feature flags in `OnInitializedAsync()` to avoid flickering
2. **Cache State Variables**: Store boolean flags as component fields to avoid repeated async calls
3. **Provide Upgrade Links**: Always show a path to upgrade when features are disabled
4. **Graceful Degradation**: Don't break the UI when features are unavailable
5. **Clear Error Messages**: Explain why a feature is disabled (licensing)
6. **Test Both States**: Test components with features both enabled and disabled

---

## Implementation Checklist

When adding a new enterprise feature to the UI:

- [ ] Check feature flag in component initialization
- [ ] Hide/disable UI elements when feature is not available
- [ ] Show informative message about license requirements
- [ ] Provide upgrade link or contact information
- [ ] Test with Free, Professional, and Enterprise tiers
- [ ] Document the feature's license requirement
- [ ] Add feature name to supported list in documentation

---

## Troubleshooting

### Feature flags not loading
- Check that `/admin/feature-flags` endpoint is accessible
- Verify licensing service is configured and database connection is valid
- Check browser console for HTTP errors
- Clear cache and reload: `FeatureFlags.ClearCache()`

### Always showing Free tier
- Verify license exists in database
- Check license status is "Active"
- Verify license has not expired
- Check ILicenseStore.GetFirstActiveLicenseAsync() query

### Features showing when they shouldn't
- Clear server-side cache (restart application)
- Clear client-side cache: `FeatureFlags.ClearCache()`
- Verify tier configuration in LicenseFeatures.GetDefaultForTier()

---

## Future Enhancements

- **Multi-tenant Support**: Pass customer ID to API for multi-tenant deployments
- **Real-time Updates**: Use SignalR to push license changes to connected clients
- **Feature Usage Tracking**: Log when users attempt to access disabled features
- **A/B Testing**: Support feature flags independent of licensing
- **Admin UI**: License management dashboard for administrators
