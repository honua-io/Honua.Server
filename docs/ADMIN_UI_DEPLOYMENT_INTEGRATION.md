# Admin UI, GitOps, and Blue/Green Deployment Integration

**Date:** 2025-11-03
**Status:** Architecture Integration Guide

---

## The Big Picture: Three Complementary Systems

You have **three powerful deployment/publishing mechanisms** that work together:

1. **Admin UI Publishing Workflow** - For interactive metadata management
2. **GitOps** - For infrastructure-as-code, audit trail, and AI-driven changes
3. **Blue/Green Deployment** - For zero-downtime infrastructure upgrades

**They're not competing - they're complementary!** Here's how they fit together:

```
┌─────────────────────────────────────────────────────────────────────────┐
│                         HonuaIO Deployment Ecosystem                     │
└─────────────────────────────────────────────────────────────────────────┘

                    ┌──────────────────────┐
                    │  METADATA CHANGES    │
                    │ (Services/Layers/etc)│
                    └──────────┬───────────┘
                               │
            ┌──────────────────┴──────────────────┐
            │                                      │
            ↓                                      ↓
    ┌───────────────────┐                ┌────────────────────┐
    │   ADMIN UI PATH   │                │   GITOPS PATH      │
    │  (Interactive)    │                │  (Code-First)      │
    └────────┬──────────┘                └────────┬───────────┘
             │                                     │
             │ 1. Edit in UI                      │ 1. Edit YAML in Git
             │ 2. Validate                        │ 2. Create PR
             │ 3. Health Check                    │ 3. AI/Human Review
             │ 4. Snapshot                        │ 4. Merge to main
             │ 5. Publish                         │ 5. GitOps Reconciler
             │                                     │
             └────────────┬────────────────────────┘
                          │
                          ↓
              ┌───────────────────────┐
              │ IMutableMetadataProvider│ ← BOTH write here!
              │  (PostgreSQL/Redis)    │
              └────────────┬───────────┘
                           │
                           │ MetadataChanged event
                           │
        ┌──────────────────┴──────────────────┐
        │                                      │
        ↓                                      ↓
┌──────────────────┐                  ┌──────────────────┐
│ PUBLIC API       │                  │ PUBLIC API       │
│ SERVER 1         │                  │ SERVER 2         │
│ Reloads metadata │                  │ Reloads metadata │
└──────────────────┘                  └──────────────────┘


───────────────────────────────────────────────────────────────────────────

                ┌────────────────────────────────┐
                │ INFRASTRUCTURE UPGRADES        │
                │ (HonuaIO server version, etc.) │
                └────────────┬───────────────────┘
                             │
                             ↓
                ┌────────────────────────────┐
                │  BLUE/GREEN DEPLOYMENT     │
                │  (YARP Traffic Manager)    │
                └─────────────┬──────────────┘
                              │
                  ┌───────────┴────────────┐
                  │                        │
                  ↓                        ↓
        ┌──────────────────┐    ┌──────────────────┐
        │ BLUE ENVIRONMENT │    │ GREEN ENVIRONMENT│
        │ v1.0 (100%)      │    │ v2.0 (0%)        │
        │                  │    │                  │
        │ Gradually shift: │    │                  │
        │ 90% → 50% → 0%   │    │ 10% → 50% → 100% │
        └──────────────────┘    └──────────────────┘

      Both environments use the SAME metadata provider
          (so metadata changes apply to both)
```

---

## Use Case Matrix

| Scenario | Use Which System? | Why? |
|----------|------------------|------|
| **Admin adds new WMS layer** | Admin UI | Interactive, immediate feedback, validation |
| **AI Consultant suggests metadata changes** | GitOps | Audit trail, PR review, infrastructure-as-code |
| **Bulk metadata changes (50+ services)** | GitOps | Version controlled, reviewable diffs |
| **Emergency metadata fix** | Admin UI (dev/staging), GitOps (production) | Fast in dev, safe in production |
| **Upgrade Honua Server v1 → v2** | Blue/Green | Zero downtime, gradual rollout, instant rollback |
| **Daily config tweaks** | Admin UI | Fast iteration for trusted admins |
| **Compliance-regulated changes** | GitOps | Full audit trail required |
| **Multi-environment promotion** | GitOps | dev → staging → prod via Git branches |

---

## Integration Point 1: Admin UI + GitOps

### The Question
"If I have GitOps, why do I need an Admin UI?"

### The Answer
They serve different workflows:

**Admin UI** is for:
- ✅ Real-time interactive editing
- ✅ Instant preview/testing
- ✅ Visual feedback (validation errors, health checks)
- ✅ Ad-hoc changes by GIS admins
- ✅ Learning/exploring the system

**GitOps** is for:
- ✅ Audit trail (who changed what when)
- ✅ AI-driven changes (AI creates PR, human reviews)
- ✅ Multi-environment promotion (dev → staging → prod)
- ✅ Bulk changes (update 100 layers at once)
- ✅ Compliance requirements (SOC2 needs code review)

### How They Work Together

**Option 1: Admin UI Writes to Git (Recommended)**

```
User edits in Admin UI
        ↓
Validation & Health Checks Pass
        ↓
Snapshot Created (automatic - enables rollback)
        ↓
Admin UI creates Git commit (via API)
        ↓
GitOps reconciler detects commit
        ↓
Applies to metadata provider
        ↓
All servers reload
```

**Implementation:**

```csharp
// Admin API endpoint
app.MapPost("/admin/metadata/services/{id}/publish", async (
    string id,
    PublishingWorkflowOptions options,
    IMetadataValidationService validationService,
    IMetadataHealthCheckService healthCheckService,
    IMetadataSnapshotRepository snapshotRepo,
    IMutableMetadataProvider metadataProvider,
    IGitRepository gitRepo) =>  // ⬅️ NEW: Git integration
{
    // 1. VALIDATE (server blocks if fails)
    var validationResult = await validationService.ValidateServiceAsync(service);

    if (!validationResult.IsValid && options.Environment == "Production")
        return Results.BadRequest(...);

    // 2. HEALTH CHECK (server blocks if fails)
    var healthCheckResult = await healthCheckService.CheckServiceHealthAsync(service);

    if (healthCheckResult.OverallStatus != HealthStatus.Healthy && options.Environment == "Production")
        return Results.BadRequest(...);

    // 3. CREATE SNAPSHOT (automatic - enables rollback)
    var snapshot = await snapshotRepo.CreateSnapshotAsync(
        entityType: "Service",
        entityId: id,
        data: service,
        createdBy: httpContext.User.GetUserId());

    // 4. PUBLISH
    // If GitOps enabled, write to Git instead of direct to provider
    if (options.GitOpsIntegration?.Enabled == true)
    {
        // 1. Serialize metadata to YAML
        var yaml = YamlSerializer.Serialize(service);

        // 2. Write to Git repo
        await gitRepo.CommitAsync(
            filePath: $"environments/{options.Environment}/metadata.yaml",
            content: yaml,
            message: $"Admin UI: Update service '{service.Name}'",
            author: httpContext.User.GetDisplayName());

        // 3. GitOps reconciler will detect and apply
        // (within polling interval, e.g., 30 seconds)

        return Results.Accepted(new
        {
            Message = "Change committed to Git. Will be applied by GitOps reconciler.",
            GitCommit = await gitRepo.GetCurrentCommitAsync(),
            SnapshotId = snapshot.Id
        });
    }
    else
    {
        // Direct publish (no GitOps)
        await metadataProvider.SaveAsync(service);
        return Results.Ok(new {
            Message = "Published directly",
            SnapshotId = snapshot.Id
        });
    }
})
.RequireAuthorization("RequireAdministrator");
```

**Configuration:**

```json
{
  "Metadata": {
    "PublishingWorkflow": {
      "Environment": "Production",
      "GitOpsIntegration": {
        "Enabled": true,             // ⬅️ Enable GitOps for Admin UI
        "RepositoryPath": "/etc/honua/config-repo",
        "AutoCommit": true,          // Auto-commit on publish
        "RequirePullRequest": false, // Or true for PR workflow
        "CommitMessage": "Admin UI: {operation} {resource}"
      }
    }
  }
}
```

**Benefits:**
- ✅ Admin UI changes go through Git (audit trail)
- ✅ GitOps handles actual metadata application
- ✅ Consistent deployment mechanism
- ✅ Can still review changes in Git before they go live

**Option 2: Admin UI Writes Directly (Simpler)**

```
User edits in Admin UI
        ↓
Validation & Health Checks Pass
        ↓
Snapshot Created (automatic - enables rollback)
        ↓
Admin UI writes to IMutableMetadataProvider
        ↓
MetadataChanged event fires
        ↓
All servers reload
```

**When to use:**
- Development/staging environments
- Small teams without compliance requirements
- When GitOps isn't needed

---

## Integration Point 2: Publishing Workflow + Blue/Green

### The Question
"How does blue/green deployment interact with the publishing workflow?"

### The Answer
They operate at **different layers**:

- **Publishing Workflow** = Metadata changes (services, layers, config)
- **Blue/Green** = Infrastructure changes (server version upgrades)

### How They Work Together

**Scenario: Upgrade HonuaIO Server v1.0 → v2.0 with Blue/Green**

```
┌─────────────────────────────────────────────────────────────────┐
│ Step 1: Deploy Green Environment (v2.0)                         │
└─────────────────────────────────────────────────────────────────┘

Blue (v1.0):  │███████████████████████████│ 100% traffic
              │ Metadata Provider: postgres://metadata_prod

Green (v2.0): │                           │ 0% traffic
              │ Metadata Provider: SAME postgres://metadata_prod
              │                              ↑
              └──────────────────────────────┴─── Both use SAME metadata!


┌─────────────────────────────────────────────────────────────────┐
│ Step 2: Admin makes metadata change (add new layer)            │
└─────────────────────────────────────────────────────────────────┘

Admin UI → Validation → Health Checks → Snapshot → Publish
                                            │
                                            ↓
                            IMutableMetadataProvider (PostgreSQL)
                │
                ├── MetadataChanged event fires
                │
        ┌───────┴───────┐
        ↓               ↓
Blue (v1.0) reloads   Green (v2.0) reloads
        ↓               ↓
Both have new layer!  Both have new layer!


┌─────────────────────────────────────────────────────────────────┐
│ Step 3: Gradual traffic shift (10% → 50% → 100%)               │
└─────────────────────────────────────────────────────────────────┘

Blue (v1.0):  │███████│               │ 50% traffic
Green (v2.0): │       │███████        │ 50% traffic

Metadata is IDENTICAL on both (same provider)
Only infrastructure version differs


┌─────────────────────────────────────────────────────────────────┐
│ Step 4: Complete cutover                                        │
└─────────────────────────────────────────────────────────────────┘

Blue (v1.0):  │                       │ 0% traffic (can decommission)
Green (v2.0): │███████████████████████│ 100% traffic (now primary)
```

**Key Insight:** Metadata changes apply to **both** blue and green environments because they share the same `IMutableMetadataProvider`.

**Why this works:**
- ✅ Metadata is data, not code (version-agnostic)
- ✅ Both v1.0 and v2.0 can serve the same WMS/WFS layers
- ✅ Metadata changes are non-breaking (just config updates)
- ✅ If green (v2.0) has issues, rollback still has same metadata

**When Blue/Green for Metadata Makes Sense:**

If you're making a **breaking metadata change** (e.g., schema migration):

```json
// Option: Use separate metadata providers for blue/green
{
  "Blue": {
    "Metadata": {
      "Provider": "postgres",
      "Schema": "metadata_v1"  // Old schema
    }
  },
  "Green": {
    "Metadata": {
      "Provider": "postgres",
      "Schema": "metadata_v2"  // New schema
    }
  }
}
```

Then you can test the schema migration in green before cutting over.

---

## Integration Point 3: GitOps + Blue/Green

### How GitOps and Blue/Green Work Together

**Scenario: Promote config from staging to production via GitOps with blue/green**

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. Developer creates PR to production branch                    │
└─────────────────────────────────────────────────────────────────┘

git branch: production
Changes: environments/production/metadata.yaml
         - Add new bike_lanes layer


┌─────────────────────────────────────────────────────────────────┐
│ 2. GitOps reconciler detects commit (within 30s)               │
└─────────────────────────────────────────────────────────────────┘

HonuaReconciler:
  - Fetches git://environments/production/metadata.yaml
  - Compares to current state
  - Creates deployment plan
  - Requires approval (production policy)


┌─────────────────────────────────────────────────────────────────┐
│ 3. Admin approves deployment                                    │
└─────────────────────────────────────────────────────────────────┘

$ honua deployment approve prod-20241104...

GitOps Reconciler:
  - Applies changes to IMutableMetadataProvider
  - MetadataChanged event fires
  - Blue and Green both reload metadata


┌─────────────────────────────────────────────────────────────────┐
│ 4. Blue/Green deployment (if needed for infra upgrade)         │
└─────────────────────────────────────────────────────────────────┘

BlueGreenTrafficManager:
  - Gradual shift: 10% → 25% → 50% → 100%
  - Both blue and green serve new bike_lanes layer
  - Health checks pass, complete cutover
```

---

## Decision Matrix: Which System When?

### For Metadata Changes

| If You Need... | Use This | Reason |
|---------------|----------|---------|
| Interactive UI | Admin UI | Visual feedback, validation |
| Audit trail | GitOps | All changes in Git history |
| AI-generated changes | GitOps | AI creates PR, human reviews |
| Multi-environment promotion | GitOps | dev → staging → prod via branches |
| Emergency hotfix in dev | Admin UI | Fastest path |
| Bulk changes (100+ layers) | GitOps | YAML editing is easier |
| Compliance requirements | GitOps | Required code review |
| Learning the system | Admin UI | Visual exploration |

### For Infrastructure Changes

| If You Need... | Use This | Reason |
|---------------|----------|---------|
| Zero-downtime server upgrade | Blue/Green | Gradual traffic shift |
| Test new Honua version | Blue/Green | Run both versions side-by-side |
| Breaking schema changes | Blue/Green + separate metadata schemas | Isolated testing |
| Instant rollback of server version | Blue/Green | One API call to shift traffic |

---

## Recommended Workflows

### Workflow 1: Development Environment (Fast Iteration)

```
Admin UI (Direct Publish)
    ↓
IMutableMetadataProvider
    ↓
All dev servers reload (~100ms)

No GitOps (not needed for dev)
No Blue/Green (not needed for dev)
```

**Why:** Maximum speed, minimal ceremony for trusted developers.

### Workflow 2: Staging Environment (Test GitOps)

```
Admin UI → Git Commit → GitOps Reconciler → Metadata Provider
                                  ↓
                            Auto-approve
                                  ↓
                          Apply to staging servers

Optional: Blue/Green for testing upgrade flow
```

**Why:** Test GitOps workflow before production, but auto-approve for speed.

### Workflow 3: Production Environment (Maximum Safety)

**For Metadata Changes:**
```
Option A (Interactive):
  Admin UI → Validation → Health Checks → Snapshot → Git Commit → GitOps
                                                                      ↓
                                                                Require Approval
                                                                      ↓
                                                                Apply to production

Option B (Code-First):
  YAML Edit → PR → CI Validation → Merge → GitOps → Approval → Apply
```

**For Infrastructure Upgrades:**
```
Blue/Green Deployment:
  Deploy Green → Health Checks → 10% → 25% → 50% → 100% cutover
                      ↓
              Metadata changes apply to BOTH blue and green
```

**Why:** Maximum safety, audit trail, approval workflow, zero downtime.

### Workflow 4: AI-Driven Changes (Best Practice)

```
AI Consultant → Generates YAML → Creates PR → Human Reviews → Merge
                                                                 ↓
                                                          GitOps Reconciler
                                                                 ↓
                                                            Apply changes
                                                                 ↓
                                                    Metadata hot-reloads on all servers
```

**Why:** AI generates code, humans review, GitOps deploys safely.

---

## Configuration Examples

### Combined: Admin UI + GitOps

```json
// appsettings.Production.json
{
  "Metadata": {
    "Provider": "postgres",
    "ConnectionString": "...",

    "PublishingWorkflow": {
      "Mode": "Staged",
      "Environment": "Production",
      "ValidationLevel": "Error",
      "HealthCheckLevel": "Error",
      "BlockUnhealthyPublish": true,

      "GitOpsIntegration": {
        "Enabled": true,  // ⬅️ Admin UI writes to Git
        "RepositoryPath": "/etc/honua/config-repo",
        "AutoCommit": true,
        "CommitMessage": "Admin UI: {operation} {resource} by {user}"
      }
    }
  },

  "GitOps": {
    "Enabled": true,
    "Repository": {
      "Url": "https://github.com/your-org/honua-config.git",
      "Branch": "main",
      "Path": "/etc/honua/config-repo"
    },
    "Environment": "production",
    "Polling": {
      "IntervalSeconds": 30
    }
  }
}
```

### Blue/Green with Shared Metadata

```json
// Blue Environment (v1.0)
{
  "Metadata": {
    "Provider": "postgres",
    "ConnectionString": "postgres://db/honua_production",
    "Schema": "public"  // ⬅️ SAME schema as Green
  }
}

// Green Environment (v2.0)
{
  "Metadata": {
    "Provider": "postgres",
    "ConnectionString": "postgres://db/honua_production",  // ⬅️ SAME DB
    "Schema": "public"  // ⬅️ SAME schema as Blue
  },

  "BlueGreen": {
    "Role": "green",
    "BlueEndpoint": "https://blue.honua.internal",
    "GreenEndpoint": "https://green.honua.internal"
  }
}
```

---

## Common Questions

### Q1: "Should Admin UI changes go through GitOps or direct to metadata provider?"

**Answer:** Depends on your compliance/audit requirements:

- **Production with compliance requirements:** Admin UI → Git → GitOps
- **Development/Staging:** Admin UI → Direct to provider (faster)
- **Configurable:** Use `GitOpsIntegration.Enabled` flag

### Q2: "What happens if I make a metadata change during a blue/green deployment?"

**Answer:** The change applies to **both** blue and green:

1. Admin publishes metadata change
2. `MetadataChanged` event fires
3. Both blue (v1.0) and green (v2.0) reload metadata
4. Both serve the updated configuration
5. Traffic shift continues as planned

**This is safe** because metadata is version-agnostic data.

### Q3: "Can I use blue/green for metadata schema migrations?"

**Answer:** Yes, but you need separate metadata schemas:

```
Blue:  metadata_v1 schema → IMutableMetadataProvider("v1")
Green: metadata_v2 schema → IMutableMetadataProvider("v2")

Then migrate data: v1 → v2
Then shift traffic: blue → green
Then decommission v1
```

### Q4: "Do I need all three systems?"

**Answer:** No! Start simple:

- **Minimum:** Admin UI with direct publish (dev/small deployments)
- **Recommended:** Admin UI + GitOps (audit trail + safety)
- **Enterprise:** Admin UI + GitOps + Blue/Green (maximum safety + zero downtime)

### Q5: "What if GitOps and Admin UI try to change the same thing?"

**Answer:** Last write wins (standard behavior for IMutableMetadataProvider).

**Best practice:**
- In production: Disable Admin UI direct-publish, require GitOps (`GitOpsIntegration.Enabled = true`)
- In dev: Allow both (conflicts rare with small teams)

---

## Summary

**Admin UI Publishing Workflow:**
- For: Interactive metadata management
- When: Daily config changes, visual feedback needed
- How: REST API → Validation → Health Checks → Snapshot → Publish

**GitOps:**
- For: Infrastructure-as-code, audit trail, multi-env
- When: AI-driven changes, compliance requirements, bulk changes
- How: YAML in Git → PR → Merge → Reconciler → Apply

**Blue/Green Deployment:**
- For: Zero-downtime infrastructure upgrades
- When: Upgrading Honua Server versions
- How: Deploy green → Health checks → Gradual traffic shift → Cutover

**They're complementary, not competing!**

- Use **Admin UI** for day-to-day interactive management
- Use **GitOps** for code-first, auditable, multi-environment workflows
- Use **Blue/Green** for server version upgrades without downtime

**All three can coexist:**
- Admin UI writes to Git → GitOps applies → Both blue and green reload metadata
