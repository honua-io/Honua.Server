# Admin UI Publishing Workflow - Architecture Decision

**Date:** 2025-11-03
**Status:** ✅ **Decision Made - Option B with Environment-Aware Strictness**

---

## Executive Summary

**Decision:** Implement **Staged Publishing Workflow (Option B)** with environment-aware validation.

**Key Features:**
- 🚀 **Fast in Dev**: Auto-publish with warnings (~2 seconds, "next next next")
- 🛡️ **Strict in Production**: Server-enforced health checks (blocks unhealthy publishes)
- ⚡ **Progressive Strictness**: Warnings → Errors as you promote to production
- 🔒 **Server-Side Guardrails**: Can't bypass validation via UI hacks
- 📸 **Automatic Snapshots**: Every publish creates a snapshot (enables instant rollback)
- 🔄 **Rollback Without Git**: Snapshots stored in database (Git optional for audit trail)

**Workflow Summary:**
```
Development:  Edit → Validate (warnings) → Snapshot → Auto-Publish
Staging:      Edit → Validate (errors) → Health Checks (warnings) → Snapshot → Auto-Publish
Production:   Edit → Validate (errors) → Health Checks (errors) → Snapshot → Manual Publish
              ↑ Server blocks if unhealthy ↑           ↑ Enables rollback ↑

CRITICAL SEQUENCE (Server-Enforced):
  1. Validate   (server blocks if fails in production)
  2. Health Check (server blocks if fails in production)
  3. Snapshot   (AUTOMATIC - always happens before publish)
  4. Publish    (only after 1-3 pass)
  5. Rollback   (restore previous snapshot if needed)
```

---

## Rollback Strategy: Snapshots First, Git Optional

**IMPORTANT:** Rollback works **WITHOUT Git** via automatic database snapshots.

### How Snapshots Work

Every time you publish a change, the system **automatically** creates a snapshot BEFORE applying the change:

```
User clicks "Publish"
        ↓
1. Validate (server blocks if fails)
        ↓
2. Health Check (server blocks if fails)
        ↓
3. CREATE SNAPSHOT ← AUTOMATIC (stores current state in database)
        ↓
4. Publish (write to metadata provider)
        ↓
5. All servers reload new config
```

**If something goes wrong:**
```
Admin clicks "Rollback"
        ↓
Restore previous snapshot (from database)
        ↓
All servers reload old config (~100ms)
```

### Snapshots vs Git

| Feature | Snapshots (Built-In) | Git (Optional Add-On) |
|---------|---------------------|----------------------|
| **Required?** | ✅ Yes (mandatory) | ❌ No (optional) |
| **Rollback Speed** | ~100ms (instant) | ~30s (via GitOps polling) |
| **Storage** | Database (postgres/redis) | External Git repo |
| **Audit Trail** | Database table with timestamps | Git commit history |
| **External Review** | Export database | Standard Git tools |
| **Enables** | Instant rollback | Code review, AI PRs, multi-env |

**Bottom Line:** You get rollback out-of-the-box via snapshots. Git is an optional enhancement for audit trails and code review workflows.

---

## The Question

Should metadata changes go live immediately (hot reload), or should there be a publishing workflow with validation, health checks, and staged deployment?

---

## Approach 1: Direct Hot Reload (Current Design)

**How it works:**
1. Admin saves change in UI
2. Change writes to metadata provider
3. All servers reload metadata immediately (~100ms)
4. Public APIs serve new configuration instantly

**Pros:**
- ✅ **Simple workflow** - no complex state management
- ✅ **Instant updates** - changes live in ~100ms
- ✅ **Zero downtime** - no restarts needed
- ✅ **Good for small teams** - trusted admins, low change volume
- ✅ **Versioning exists** - can roll back via metadata snapshots

**Cons:**
- ❌ **No validation** - bad config goes live immediately
- ❌ **No testing** - can't preview changes before publish
- ❌ **Risky for production** - typo in WMS config = immediate outage
- ❌ **No approval workflow** - single admin can break everything
- ❌ **Limited rollback** - manual process, not instant

**Best for:**
- Development environments
- Small deployments (1-3 admins)
- Internal/non-critical GIS services
- Teams with strong config review practices

---

## Approach 2: Staged Publishing Workflow

**How it works:**
1. Admin creates/edits metadata in **draft mode**
2. System validates configuration
3. Admin previews changes via **staging endpoint**
4. Health checks run automatically
5. Admin clicks **"Publish"** (or auto-publish after checks pass)
6. Changes go live to production

**Architecture:**

```
┌─────────────────────────────────────────────────────────┐
│ Metadata States                                         │
│                                                          │
│  DRAFT → VALIDATION → STAGING → PUBLISHED → LIVE       │
│    ↓         ↓           ↓          ↓         ↓        │
│  Editable  Health     Preview    Approved   Serving    │
│            Checks     Endpoint              Requests   │
└─────────────────────────────────────────────────────────┘
```

**Implementation:**

```csharp
// Enhanced metadata model
public class ServiceMetadata
{
    public string Id { get; set; }
    public string Name { get; set; }
    // ... other properties

    // Publishing workflow properties
    public PublishingState State { get; set; }  // Draft, Staging, Published
    public DateTime? LastPublishedAt { get; set; }
    public string? PublishedByUserId { get; set; }
    public ValidationResult? LastValidation { get; set; }
    public HealthCheckResult? LastHealthCheck { get; set; }
}

public enum PublishingState
{
    Draft,          // Being edited, not visible to public
    Staging,        // In preview/testing mode
    Published,      // Live on public API
    Archived        // Removed from public, retained for history
}

// API endpoints
POST   /admin/metadata/services                    // Creates in Draft state
PUT    /admin/metadata/services/{id}              // Edits (if Draft or Staging)
POST   /admin/metadata/services/{id}/validate     // Run validation checks
POST   /admin/metadata/services/{id}/stage        // Move to staging (creates preview)
POST   /admin/metadata/services/{id}/publish      // Publish to production
POST   /admin/metadata/services/{id}/rollback     // Revert to previous published version
GET    /admin/metadata/services/{id}/preview      // Preview staging version
```

**Validation Checks:**

```csharp
public class MetadataValidationService
{
    public async Task<ValidationResult> ValidateServiceAsync(ServiceMetadata service)
    {
        var errors = new List<ValidationError>();

        // 1. Schema validation
        if (string.IsNullOrWhiteSpace(service.Name))
            errors.Add(new ValidationError("Name is required"));

        // 2. Reference validation
        foreach (var layer in service.Layers)
        {
            // Check if referenced style exists
            if (!await _styleRepository.ExistsAsync(layer.StyleId))
                errors.Add(new ValidationError($"Layer '{layer.Name}' references non-existent style '{layer.StyleId}'"));

            // Check if data source is accessible
            if (!await _dataSourceValidator.CanConnectAsync(layer.DataSourceId))
                errors.Add(new ValidationError($"Cannot connect to data source for layer '{layer.Name}'"));
        }

        // 3. Security validation
        if (service.ConnectionString?.Contains("password=") == true)
            errors.Add(new ValidationError("Connection string contains hardcoded password. Use Key Vault."));

        // 4. Performance validation
        if (service.Layers.Count > 50 && !service.CachingEnabled)
            errors.Add(new ValidationError($"Service has {service.Layers.Count} layers without caching. Performance impact: High"));

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors,
            Warnings = GetWarnings(service),
            Timestamp = DateTime.UtcNow
        };
    }
}
```

**Health Checks (Before Publishing):**

```csharp
public class MetadataHealthCheckService
{
    public async Task<HealthCheckResult> CheckServiceHealthAsync(ServiceMetadata service)
    {
        var checks = new List<HealthCheck>();

        // 1. Data source connectivity
        foreach (var layer in service.Layers)
        {
            var canConnect = await _dataSourceValidator.CanConnectAsync(layer.DataSourceId);
            checks.Add(new HealthCheck
            {
                Name = $"DataSource_{layer.DataSourceId}",
                Status = canConnect ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Message = canConnect ? "Connected" : "Cannot connect to data source"
            });
        }

        // 2. Spatial reference validation
        foreach (var layer in service.Layers)
        {
            var crsValid = await _crsValidator.ValidateAsync(layer.CRS);
            checks.Add(new HealthCheck
            {
                Name = $"CRS_{layer.Name}",
                Status = crsValid ? HealthStatus.Healthy : HealthStatus.Unhealthy,
                Message = crsValid ? "Valid CRS" : $"Invalid or unsupported CRS: {layer.CRS}"
            });
        }

        // 3. Style rendering test (if applicable)
        if (service.Type == ServiceType.WMS)
        {
            var styleRenderable = await _styleValidator.CanRenderAsync(service.Layers.First());
            checks.Add(new HealthCheck
            {
                Name = "StyleRendering",
                Status = styleRenderable ? HealthStatus.Healthy : HealthStatus.Warning,
                Message = styleRenderable ? "Style renders successfully" : "Style may have rendering issues"
            });
        }

        // 4. Quota/limits check
        var exceedsLimits = await _quotaService.WouldExceedQuotaAsync(service);
        checks.Add(new HealthCheck
        {
            Name = "QuotaCheck",
            Status = exceedsLimits ? HealthStatus.Warning : HealthStatus.Healthy,
            Message = exceedsLimits ? "Publishing will exceed tile cache quota" : "Within quota limits"
        });

        return new HealthCheckResult
        {
            OverallStatus = checks.All(c => c.Status == HealthStatus.Healthy)
                ? HealthStatus.Healthy
                : HealthStatus.Warning,
            Checks = checks,
            Timestamp = DateTime.UtcNow
        };
    }
}
```

**Preview/Staging Endpoint:**

```csharp
// Staging environment configuration
{
  "Metadata": {
    "PublishingMode": "Staged",  // or "Direct"
    "StagingProvider": {
      "Type": "Postgres",
      "ConnectionString": "...",
      "Schema": "metadata_staging"  // Separate schema for staging
    },
    "ProductionProvider": {
      "Type": "Postgres",
      "ConnectionString": "...",
      "Schema": "metadata_production"
    }
  }
}

// Preview endpoint for staging changes
// GET /preview/wms?SERVICE=WMS&REQUEST=GetCapabilities&SERVICE_ID={id}
// Uses staging metadata provider instead of production
app.MapGet("/preview/{serviceType}/{*path}", async (
    string serviceType,
    string path,
    HttpContext context,
    IStagingMetadataProvider stagingProvider) =>
{
    // Serve request using staging metadata
    var stagingSnapshot = await stagingProvider.LoadAsync();

    // Process WMS/WFS/etc request with staging config
    return await HandleRequestWithMetadata(context, stagingSnapshot);
})
.RequireAuthorization("RequireAdministrator");  // Only admins can preview
```

**Pros:**
- ✅ **Safe deployments** - validate before going live
- ✅ **Testing capability** - preview changes via `/preview` endpoint
- ✅ **Health checks** - catch issues before users see them
- ✅ **Approval workflow** - require review for critical changes
- ✅ **Better rollback** - instant revert to last published version
- ✅ **Compliance** - meets SOC2/ISO requirements for change control
- ✅ **Confidence** - admins can experiment without fear

**Cons:**
- ❌ **More complex** - additional state management
- ❌ **Slower workflow** - extra steps to publish
- ❌ **Infrastructure overhead** - needs staging environment (or separate schema)
- ❌ **UI complexity** - more buttons, states to manage

**Best for:**
- Production environments serving critical data
- Large teams with multiple admins
- Compliance requirements (SOC2, ISO 27001, HIPAA)
- Organizations with change approval processes
- Multi-tenant SaaS deployments

---

## Hybrid Approach (Recommended)

**Make it configurable based on deployment needs:**

```json
// appsettings.json
{
  "Metadata": {
    "PublishingWorkflow": {
      "Mode": "Staged",  // "Direct" or "Staged"

      // Staged mode configuration
      "RequireValidation": true,
      "RequireHealthChecks": true,
      "RequireApproval": false,  // Optional manual approval step
      "AutoPublishAfterChecks": false,  // Or auto-publish if all checks pass
      "StagingEndpointEnabled": true,
      "RollbackHistoryDepth": 10  // Keep last 10 published versions
    }
  }
}
```

**Implementation Strategy:**

### Phase 1 (MVP - Current): Direct Mode
- Simple, immediate updates
- Versioning/rollback via existing metadata snapshots
- Good enough for most deployments

### Phase 2 (Enhancement): Add Validation
- Validate on save (even in direct mode)
- Show warnings/errors in UI
- Admin can choose to publish anyway (with confirmation)

### Phase 3 (Advanced): Full Staged Workflow
- Draft/Staging/Published states
- Preview endpoint
- Health checks
- Optional approval workflow
- One-click rollback

**Decision Tree:**

```
Is this a production deployment serving critical data?
├─ No → Use Direct mode
│   └─ Fast, simple, good for dev/test/internal
│
└─ Yes → Does your org have change approval requirements?
    ├─ No → Use Staged mode with auto-publish
    │   └─ Validation + health checks, but no manual approval
    │
    └─ Yes → Use Staged mode with approval workflow
        └─ Full governance, compliance-ready
```

---

## Rollback Capability (Both Modes)

Even in Direct mode, we should have quick rollback:

```csharp
// Rollback endpoint (works in both modes)
app.MapPost("/admin/metadata/services/{id}/rollback", async (
    string id,
    IMutableMetadataProvider metadataProvider,
    IMetadataSnapshotRepository snapshotRepo) =>
{
    // Get previous published version
    var previousSnapshot = await snapshotRepo.GetPreviousVersionAsync(id);

    if (previousSnapshot == null)
        return Results.NotFound("No previous version available");

    // Restore previous version
    await metadataProvider.RestoreSnapshotAsync(previousSnapshot.Id);

    // Metadata provider fires MetadataChanged event
    // All servers reload with previous config

    return Results.Ok(new { Message = "Rolled back to previous version", Timestamp = previousSnapshot.CreatedAt });
})
.RequireAuthorization("RequireAdministrator");
```

**Rollback UI:**

```razor
<MudButton OnClick="RollbackAsync" Color="Color.Warning">
    <MudIcon Icon="@Icons.Material.Filled.Undo" />
    Rollback to Previous Version
</MudButton>

<MudTimeline>
    @foreach (var version in _versionHistory)
    {
        <MudTimelineItem>
            <MudText>@version.Description</MudText>
            <MudText Typo="Typo.Caption">
                Published by @version.PublishedBy at @version.Timestamp
            </MudText>
            @if (!version.IsCurrent)
            {
                <MudButton Size="Size.Small" OnClick="() => RestoreVersionAsync(version.Id)">
                    Restore This Version
                </MudButton>
            }
        </MudTimelineItem>
    }
</MudTimeline>
```

---

## Comparison Matrix

| Feature | Direct Mode | Staged Mode |
|---------|-------------|-------------|
| **Time to Live** | ~100ms | Minutes (with manual approval) |
| **Validation** | Optional (warning only) | Required before publish |
| **Health Checks** | None | Required before publish |
| **Preview** | ❌ | ✅ Via `/preview` endpoint |
| **Rollback** | Manual (via snapshot restore) | One-click (instant) |
| **Approval Workflow** | ❌ | ✅ (optional) |
| **Infrastructure** | Single metadata provider | Staging + Production schemas/instances |
| **UI Complexity** | Low (Edit → Save → Live) | Medium (Edit → Validate → Stage → Publish) |
| **Risk Level** | High (typos go live) | Low (validated before publish) |
| **Best For** | Dev/Test, Small teams | Production, Compliance, Large teams |
| **Cost** | Low | Medium (staging infrastructure) |

---

## Recommended Approach: Environment-Aware Staged Workflow ✅

**Decision: Option B (Staged) with intelligent defaults**

### Core Principle: Fast in Dev, Strict in Production

The publishing workflow adapts based on environment:

| Environment | Validation | Health Checks | Auto-Publish | Server Blocks Unhealthy |
|-------------|-----------|---------------|--------------|------------------------|
| **Development** | ⚠️ Warnings only | ⚠️ Warnings only | ✅ Yes (instant) | ❌ No (warnings only) |
| **Staging** | ✅ Required | ✅ Required | ✅ Yes (if valid) | ⚠️ Warns but allows |
| **Production** | ✅ Required | ✅ Required | ❌ No (manual) | ✅ **Yes (blocks invalid)** |

### Development Experience: "Next, Next, Next"

**In dev environments:**

```json
// appsettings.Development.json
{
  "Metadata": {
    "PublishingWorkflow": {
      "Mode": "Staged",
      "Environment": "Development",
      "ValidationLevel": "Warning",        // Show warnings, don't block
      "HealthCheckLevel": "Warning",       // Show warnings, don't block
      "AutoPublishAfterValidation": true,  // Auto-publish if no errors
      "RequireManualApproval": false       // No manual approval needed
    }
  }
}
```

**Workflow in dev:**
1. Admin edits service → clicks Save
2. System validates (shows warnings if any)
3. **Auto-publishes immediately** if no blocking errors
4. Done! (~2 seconds)

**UI Flow in Dev:**
```
┌────────────────────────────────────────┐
│ Edit Service "Water Services"          │
│                                         │
│ [Service configuration form...]         │
│                                         │
│ ⚠️ 2 Warnings:                         │
│   • 5 layers without caching (perf)    │
│   • Style 'blue' not optimized         │
│                                         │
│ [Save & Publish]  [Cancel]             │
│                                         │
│ ✓ Published successfully!              │
└────────────────────────────────────────┘

Total clicks: 1 (Save & Publish)
Time: ~2 seconds
```

### Production Experience: Server-Enforced Guardrails

**In production environments:**

```json
// appsettings.Production.json
{
  "Metadata": {
    "PublishingWorkflow": {
      "Mode": "Staged",
      "Environment": "Production",
      "ValidationLevel": "Error",          // Validation failures BLOCK publish
      "HealthCheckLevel": "Error",         // Health check failures BLOCK publish
      "AutoPublishAfterValidation": false, // Manual publish click required
      "RequireManualApproval": false,      // Optional: add approval workflow later
      "BlockUnhealthyPublish": true        // ⭐ SERVER ENFORCES THIS
    }
  }
}
```

**Server-side enforcement:**

```csharp
// POST /admin/metadata/services/{id}/publish
app.MapPost("/admin/metadata/services/{id}/publish", async (
    string id,
    PublishingWorkflowOptions options,
    IMetadataValidationService validationService,
    IMetadataHealthCheckService healthCheckService,
    IMutableMetadataProvider metadataProvider) =>
{
    var service = await metadataProvider.GetServiceAsync(id);

    // ⭐ SERVER-ENFORCED VALIDATION (can't bypass from UI)
    var validationResult = await validationService.ValidateServiceAsync(service);

    if (options.Environment == "Production" && !validationResult.IsValid)
    {
        // 🛑 BLOCK: Server refuses to publish invalid config in production
        return Results.BadRequest(new ProblemDetails
        {
            Title = "Cannot publish: validation failed",
            Detail = "Production deployments require all validation checks to pass",
            Status = 400,
            Extensions =
            {
                ["errors"] = validationResult.Errors,
                ["reason"] = "Production environment enforces strict validation"
            }
        });
    }

    // ⭐ SERVER-ENFORCED HEALTH CHECKS (can't bypass from UI)
    var healthCheckResult = await healthCheckService.CheckServiceHealthAsync(service);

    if (options.Environment == "Production" && healthCheckResult.OverallStatus != HealthStatus.Healthy)
    {
        // 🛑 BLOCK: Server refuses to publish unhealthy config in production
        return Results.BadRequest(new ProblemDetails
        {
            Title = "Cannot publish: health checks failed",
            Detail = "Production deployments require all health checks to pass",
            Status = 400,
            Extensions =
            {
                ["healthChecks"] = healthCheckResult.Checks,
                ["reason"] = "Production environment enforces health requirements"
            }
        });
    }

    // ✅ All checks passed - safe to publish
    service.State = PublishingState.Published;
    service.LastPublishedAt = DateTime.UtcNow;
    service.PublishedByUserId = httpContext.User.GetUserId();

    await metadataProvider.SaveAsync(service);

    // Metadata provider fires MetadataChanged event
    // All production servers reload with new config

    return Results.Ok(new
    {
        Message = "Published successfully",
        ValidationResult = validationResult,
        HealthCheckResult = healthCheckResult
    });
})
.RequireAuthorization("RequireAdministrator");
```

**UI Flow in Production:**
```
┌────────────────────────────────────────┐
│ Edit Service "Critical Infrastructure" │
│                                         │
│ [Service configuration form...]         │
│                                         │
│ [Save as Draft]                         │
│                                         │
│ ✓ Saved as draft                       │
│                                         │
│ Running validation...                   │
│ ✅ Validation passed                   │
│                                         │
│ Running health checks...                │
│ ✅ All systems healthy                 │
│   • Data source: Connected             │
│   • CRS: Valid                          │
│   • Style: Renderable                   │
│   • Quota: Within limits                │
│                                         │
│ [Publish to Production]  [Preview]     │
└────────────────────────────────────────┘

Click "Publish to Production"
↓
Server validates + health checks
↓
✅ Published (if healthy)
🛑 Blocked (if unhealthy, with clear error message)
```

**If unhealthy, server blocks:**
```
┌────────────────────────────────────────┐
│ ❌ Cannot Publish                      │
│                                         │
│ Production deployments require all     │
│ health checks to pass.                  │
│                                         │
│ Failed Checks:                          │
│ • DataSource_postgres_db: Cannot       │
│   connect (connection timeout)          │
│ • CRS_EPSG:9999: Invalid CRS           │
│                                         │
│ Fix these issues before publishing.     │
│                                         │
│ [Fix Issues]  [Cancel]                 │
└────────────────────────────────────────┘

Server returned HTTP 400 Bad Request
Publishing blocked by server-side validation
```

### Configuration by Environment

```csharp
// Program.cs - Configure based on environment
builder.Services.Configure<PublishingWorkflowOptions>(options =>
{
    var env = builder.Environment;

    if (env.IsDevelopment())
    {
        // Fast workflow for dev
        options.Environment = "Development";
        options.ValidationLevel = ValidationLevel.Warning;
        options.HealthCheckLevel = HealthCheckLevel.Warning;
        options.AutoPublishAfterValidation = true;
        options.BlockUnhealthyPublish = false;  // Allow publish with warnings
    }
    else if (env.IsStaging())
    {
        // Moderate strictness for staging
        options.Environment = "Staging";
        options.ValidationLevel = ValidationLevel.Error;
        options.HealthCheckLevel = HealthCheckLevel.Warning;
        options.AutoPublishAfterValidation = true;
        options.BlockUnhealthyPublish = false;  // Allow with health warnings
    }
    else // Production
    {
        // ⭐ Strict enforcement for production
        options.Environment = "Production";
        options.ValidationLevel = ValidationLevel.Error;
        options.HealthCheckLevel = HealthCheckLevel.Error;
        options.AutoPublishAfterValidation = false;  // Manual publish click
        options.BlockUnhealthyPublish = true;  // ⭐ SERVER BLOCKS UNHEALTHY
    }
});
```

### Benefits of This Approach

**For Developers:**
- ✅ Fast workflow in dev (almost "next next next")
- ✅ Can experiment freely
- ✅ Still see warnings to improve quality

**For Production:**
- ✅ **Server enforces safety** (can't bypass via UI hacks)
- ✅ Clear error messages when blocked
- ✅ Forces fixes before going live
- ✅ Compliance-ready (audit trail of validation)

**For Everyone:**
- ✅ Same workflow in all environments (consistency)
- ✅ Progressive strictness (warnings → errors as you promote)
- ✅ Server-side enforcement (security-focused)
- ✅ Can't accidentally break production

---

## Implementation Roadmap

### Phase 1: Core Publishing Workflow (Weeks 1-2)

**Backend:**
1. ✅ Add `PublishingState` enum to `ServiceMetadata`, `LayerMetadata`, etc.
2. ✅ Implement `PublishingWorkflowOptions` configuration
3. ✅ Create `IMetadataValidationService` interface and implementation
   - Schema validation
   - Reference validation (styles, data sources exist)
   - Security validation (no hardcoded passwords)
   - Performance validation (caching recommendations)
4. ✅ Create `IMetadataHealthCheckService` interface and implementation
   - Data source connectivity checks
   - CRS validation
   - Style rendering test
   - Quota/limits check
5. ✅ Add publishing endpoints:
   - `POST /admin/metadata/services/{id}/validate`
   - `POST /admin/metadata/services/{id}/publish`
   - `POST /admin/metadata/services/{id}/rollback`
6. ✅ **Server-side enforcement logic** (blocks unhealthy publishes in production)

**Frontend:**
1. ✅ Add "Save as Draft" vs "Save & Publish" buttons
2. ✅ Show validation results in UI (warnings/errors with fix suggestions)
3. ✅ Show health check results before publish
4. ✅ Add "Publish to Production" button (with confirmation)
5. ✅ Handle server-side blocking (show clear error messages)
6. ✅ Add publishing state indicators (Draft badge, Published badge, etc.)

### Phase 2: Preview & Staging (Weeks 3-4)

**Backend:**
1. ✅ Implement staging metadata provider (separate schema or instance)
2. ✅ Add `/preview/{serviceType}/*` endpoints
   - Serve WMS/WFS/etc requests using staging metadata
   - Require admin authentication
3. ✅ Add staging promotion:
   - `POST /admin/metadata/services/{id}/stage` - copy to staging
   - `GET /admin/metadata/services/{id}/preview` - get staging version
   - `POST /admin/metadata/services/{id}/publish` - promote staging → production

**Frontend:**
1. ✅ Add "Preview" button (opens staging endpoint in new tab)
2. ✅ Add staging status indicators
3. ✅ Show diff between draft and published versions

### Phase 3: Rollback & Version History (Week 5)

**Backend:**
1. ✅ Extend `IMetadataSnapshotRepository` for rollback
2. ✅ Store validation/health check results with each snapshot
3. ✅ Add version comparison endpoint

**Frontend:**
1. ✅ Build version history timeline UI
2. ✅ Add one-click rollback
3. ✅ Show who published when
4. ✅ Show validation results for each version

### Phase 4: Advanced (Future)

**Optional enhancements:**
- Multi-step approval workflow (requires approval from 2 admins)
- Scheduled publishing (publish at specific time)
- A/B testing (gradual rollout to subset of servers)
- Blue/green deployment support
- Integration with CI/CD pipelines

---

## Implementation Checklist

### Must Have (Phase 1)
- [ ] `PublishingState` enum and state machine
- [ ] Environment-aware `PublishingWorkflowOptions`
- [ ] `MetadataValidationService` with 4+ validation types
- [ ] `MetadataHealthCheckService` with 3+ health checks
- [ ] Server-side enforcement (blocks unhealthy publishes)
- [ ] Publishing API endpoints (`/validate`, `/publish`, `/rollback`)
- [ ] UI workflow (Draft → Validate → Publish)
- [ ] Clear error messages when server blocks publish
- [ ] Unit tests for validation logic
- [ ] Integration tests for publishing workflow

### Should Have (Phase 2)
- [ ] Staging metadata provider (separate schema)
- [ ] Preview endpoints (`/preview/wms`, `/preview/wfs`, etc.)
- [ ] Staging promotion workflow
- [ ] Diff view (draft vs published)
- [ ] Preview button in UI

### Nice to Have (Phase 3)
- [ ] Version history timeline UI
- [ ] One-click rollback
- [ ] Audit trail (who published when, why)
- [ ] Validation results stored with snapshots

---

## Key Architectural Decisions

✅ **Server enforces safety** - API refuses unhealthy publishes (can't bypass from UI)
✅ **Environment-aware strictness** - warnings in dev, errors in production
✅ **Progressive enhancement** - start simple, add features as needed
✅ **Backward compatible** - can disable workflow (`Mode: "Direct"`) if needed
✅ **OAuth/SAML compatible** - uses same bearer token authentication
✅ **Works in both deployments** - combined and detached modes

---

## Success Criteria

**Development Experience:**
- ⏱️ Publish in dev takes <3 seconds (including validation)
- 🎯 1-2 clicks to go from edit to published (in dev)
- ⚠️ Warnings are visible but don't block

**Production Safety:**
- 🛑 Server blocks 100% of unhealthy publishes
- ✅ Validation catches common mistakes (missing refs, bad CRS, etc.)
- ⚡ Health checks verify connectivity before going live
- 📊 Audit trail shows who published what and when
- 🔄 Rollback works in <10 seconds

**Compliance:**
- 📝 All publishes logged with user identity
- 🔍 Validation results auditable
- 🔒 Server-side enforcement (not just UI warnings)
- ✅ Meets SOC2/ISO change control requirements
