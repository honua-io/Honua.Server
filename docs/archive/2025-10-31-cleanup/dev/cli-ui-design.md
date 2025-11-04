# Honua CLI UI Design

## Overview

Rich terminal UI for visualizing system state, deployments, and operations. Inspired by ArgoCD CLI, kubectl, and k9s.

## CLI Commands & Output

### 1. `honua status` - Overall System Status

```bash
$ honua status

┌─ HONUA ENVIRONMENTS ────────────────────────────────────────────────────┐
│ ENVIRONMENT   HEALTH      SYNC        DEPLOYED           COMMIT          │
├─────────────────────────────────────────────────────────────────────────┤
│ dev           ● Healthy   ✓ Synced    5m ago             abc123d         │
│ staging       ● Healthy   ✓ Synced    2h ago             def456e         │
│ production    ● Healthy   ⚠ OutOfSync 1d ago             789abcd         │
└─────────────────────────────────────────────────────────────────────────┘

┌─ ACTIVE DEPLOYMENTS ────────────────────────────────────────────────────┐
│ No deployments in progress                                              │
└─────────────────────────────────────────────────────────────────────────┘

Sync Status:
  ✓ dev: In sync with Git (main@abc123d)
  ✓ staging: In sync with Git (main@def456e)
  ⚠ production: 3 commits behind (main@789abcd, latest: main@abc123d)

Run 'honua env production' for details
Run 'honua sync production' to sync production
```

### 2. `honua env <environment>` - Environment Details

```bash
$ honua env production

┌─ ENVIRONMENT: PRODUCTION ───────────────────────────────────────────────┐
│ Health:        ● Healthy                                                │
│ Sync Status:   ⚠ OutOfSync                                              │
│ Deployed:      abc123d (1 day ago)                                      │
│ Latest:        def456e (5 minutes ago)                                  │
│ Drift:         3 commits behind                                         │
└─────────────────────────────────────────────────────────────────────────┘

┌─ LAST DEPLOYMENT ───────────────────────────────────────────────────────┐
│ ID:            production-20251003-143022                               │
│ Status:        ✓ Completed                                              │
│ Started:       2025-10-03 14:30:22 PDT                                  │
│ Duration:      4m 56s                                                   │
│ Initiated by:  john@example.com                                         │
│ Commit:        abc123d                                                  │
│ Message:       "Add bike lanes layer"                                   │
└─────────────────────────────────────────────────────────────────────────┘

┌─ DEPLOYED RESOURCES ────────────────────────────────────────────────────┐
│ TYPE        NAME                      STATUS     VERSION                │
├─────────────────────────────────────────────────────────────────────────┤
│ Service     roads                     ✓ Healthy  1.2.0                  │
│ Service     parcels                   ✓ Healthy  1.1.0                  │
│ Service     transportation            ✓ Healthy  1.3.0                  │
│ Layer       roads::highways           ✓ Healthy  -                      │
│ Layer       roads::streets            ✓ Healthy  -                      │
│ Layer       parcels::parcels          ✓ Healthy  -                      │
│ Layer       transportation::bike-lanes ✓ Healthy -                      │
│ DataSource  postgis-primary           ✓ Healthy  -                      │
└─────────────────────────────────────────────────────────────────────────┘

┌─ PENDING CHANGES (3 commits ahead) ─────────────────────────────────────┐
│ def456e  2h ago   Update parcel layer styling                           │
│ fed123c  4h ago   Add zoning layer                                      │
│ cba987d  5h ago   Fix bike lanes metadata                               │
└─────────────────────────────────────────────────────────────────────────┘

Commands:
  honua diff production           # Show detailed diff
  honua sync production           # Deploy pending changes
  honua history production        # View deployment history
  honua rollback production       # Rollback to previous deployment
```

### 3. `honua deployments` - All Deployments

```bash
$ honua deployments

┌─ RECENT DEPLOYMENTS ────────────────────────────────────────────────────┐
│ ID                           ENV        STATUS        STARTED    DURATION│
├─────────────────────────────────────────────────────────────────────────┤
│ dev-20251004-091533          dev        ✓ Completed   2h ago     2m 15s │
│ staging-20251004-084422      staging    ✓ Completed   3h ago     3m 42s │
│ production-20251003-143022   production ✓ Completed   1d ago     4m 56s │
│ staging-20251003-121015      staging    ✗ Failed      1d ago     1m 22s │
│ dev-20251003-103344          dev        ✓ Completed   1d ago     1m 58s │
└─────────────────────────────────────────────────────────────────────────┘

Run 'honua deployment <id>' for details
```

### 4. `honua deployment <id>` - Deployment Details

```bash
$ honua deployment production-20251004-143022

┌─ DEPLOYMENT: production-20251004-143022 ────────────────────────────────┐
│ Environment:   production                                               │
│ Status:        ✓ Completed                                              │
│ Health:        ● Healthy                                                │
│ Initiated by:  john@example.com                                         │
│ Started:       2025-10-04 14:30:22 PDT                                  │
│ Completed:     2025-10-04 14:35:18 PDT                                  │
│ Duration:      4m 56s                                                   │
│ Commit:        abc123def456                                             │
│ Branch:        main                                                     │
│ Backup:        backup-20251004-143025                                   │
└─────────────────────────────────────────────────────────────────────────┘

┌─ STATE TRANSITIONS ─────────────────────────────────────────────────────┐
│ 14:30:22  Pending           Deployment created                          │
│ 14:30:25  Validating        Running pre-deployment checks               │
│ 14:30:35  Planning          Generating deployment plan                  │
│ 14:30:38  BackingUp         Creating database snapshot                  │
│ 14:31:12  Applying          Deploying changes                           │
│ 14:34:45  PostValidating    Running health checks                       │
│ 14:35:18  Completed         Deployment successful                       │
└─────────────────────────────────────────────────────────────────────────┘

┌─ DEPLOYMENT PLAN ───────────────────────────────────────────────────────┐
│ Changes:                                                                │
│   + 1 layer added (bike-lanes)                                          │
│   ~ 1 service modified (transportation)                                 │
│   - 0 resources removed                                                 │
│                                                                          │
│ Migrations:                                                             │
│   None                                                                  │
│                                                                          │
│ Risk Level:     LOW                                                     │
│ Breaking:       No                                                      │
└─────────────────────────────────────────────────────────────────────────┘

┌─ VALIDATION RESULTS ────────────────────────────────────────────────────┐
│ TYPE               STATUS     MESSAGE                                   │
├─────────────────────────────────────────────────────────────────────────┤
│ syntax             ✓ Passed   YAML syntax valid                         │
│ policy             ✓ Passed   Within deployment window                  │
│ breaking-changes   ✓ Passed   No breaking changes detected              │
│ health-check       ✓ Passed   All services responding                   │
│ ogc-conformance    ✓ Passed   OGC API tests passed (48/48)              │
│ performance        ✓ Passed   Response time: 95ms (target: <200ms)      │
└─────────────────────────────────────────────────────────────────────────┘

┌─ TOPOLOGY ACTIONS ──────────────────────────────────────────────────────┐
│ COMPONENT         ACTION                        STATUS    DURATION      │
├─────────────────────────────────────────────────────────────────────────┤
│ Database          Create backup                 ✓ Done    34s           │
│ App Servers       Rolling update (3 servers)    ✓ Done    3m 15s        │
│   - Server 1      Health check                  ✓ Done    5s            │
│   - Server 2      Health check                  ✓ Done    4s            │
│   - Server 3      Health check                  ✓ Done    6s            │
│ CDN               Selective cache purge         ✓ Done    12s           │
│ Tile Cache        Queue regeneration (zoom 0-5) ✓ Done    8s            │
│ Load Balancer     Update health check           ✓ Done    3s            │
└─────────────────────────────────────────────────────────────────────────┘

Commands:
  honua logs production-20251004-143022    # View deployment logs
  honua rollback production                # Rollback this deployment
```

### 5. `honua diff <environment>` - Show Pending Changes

```bash
$ honua diff production

┌─ GIT DIFF: production ──────────────────────────────────────────────────┐
│ Deployed:  abc123d (1 day ago)                                          │
│ Latest:    def456e (5 minutes ago)                                      │
│ Commits:   3 commits ahead                                              │
└─────────────────────────────────────────────────────────────────────────┘

┌─ RESOURCE CHANGES ──────────────────────────────────────────────────────┐
│ ACTION  TYPE     NAME                      FILE                         │
├─────────────────────────────────────────────────────────────────────────┤
│ +       Layer    zoning                    layers/zoning.yaml           │
│ ~       Layer    parcels                   layers/parcels.yaml          │
│ ~       Layer    bike-lanes                layers/bike-lanes.yaml       │
└─────────────────────────────────────────────────────────────────────────┘

┌─ FILE: environments/production/layers/parcels.yaml ─────────────────────┐
│  spec:                                                                  │
│    title: "Property Parcels"                                            │
│ -  defaultStyle: parcels-default                                        │
│ +  defaultStyle: parcels-styled                                         │
│                                                                          │
│    rendering:                                                           │
│ +    fillColor: "#3388ff"                                               │
│ +    fillOpacity: 0.6                                                   │
└─────────────────────────────────────────────────────────────────────────┘

┌─ FILE: environments/production/layers/bike-lanes.yaml ──────────────────┐
│  spec:                                                                  │
│    geometryType: LineString                                             │
│ -  maxRecordCount: 1000                                                 │
│ +  maxRecordCount: 5000                                                 │
└─────────────────────────────────────────────────────────────────────────┘

┌─ FILE: environments/production/layers/zoning.yaml (new) ────────────────┐
│ +apiVersion: honua.io/v1                                                │
│ +kind: Layer                                                            │
│ +metadata:                                                              │
│ +  name: zoning                                                         │
│ +  service: planning                                                    │
│ +spec:                                                                  │
│ +  title: "Zoning Districts"                                            │
│ +  geometryType: Polygon                                                │
│ +  storage:                                                             │
│ +    table: zoning_districts                                            │
└─────────────────────────────────────────────────────────────────────────┘

Summary:
  1 layer added (zoning)
  2 layers modified (parcels, bike-lanes)
  0 resources removed

Risk Assessment:
  Risk Level:        LOW
  Breaking Changes:  No
  Migrations:        None

Commands:
  honua sync production    # Deploy these changes
```

### 6. `honua sync <environment>` - Deploy Changes (Live Progress)

```bash
$ honua sync production

┌─ DEPLOYMENT PLAN ───────────────────────────────────────────────────────┐
│ Environment:  production                                                │
│ Commit:       def456e                                                   │
│ Changes:      +1 layer, ~2 layers                                       │
│ Risk:         LOW                                                       │
│ Duration:     ~5 minutes                                                │
└─────────────────────────────────────────────────────────────────────────┘

⚠  This deployment requires approval (production policy)
   Approval link: https://github.com/city/honua-config/deployments/123

Waiting for approval... (timeout: 24h)

✓ Approved by: admin@example.com

┌─ DEPLOYMENT PROGRESS ───────────────────────────────────────────────────┐
│                                                                          │
│ [●●●●●●●○○○○○○○○○○○○○] 35% - Applying changes...                        │
│                                                                          │
│ Phase 1: Validation         ✓ Completed (10s)                           │
│ Phase 2: Planning           ✓ Completed (5s)                            │
│ Phase 3: Backup             ✓ Completed (32s)                           │
│ Phase 4: Applying           ⟳ In Progress (1m 15s)                      │
│   - Update server 1         ✓ Done (25s)                                │
│   - Update server 2         ⟳ In Progress (15s)                         │
│   - Update server 3         ○ Pending                                   │
│ Phase 5: Validation         ○ Pending                                   │
│                                                                          │
│ Deployment ID: production-20251004-153344                               │
│ Started: 2025-10-04 15:33:44 PDT                                        │
│ Elapsed: 2m 2s                                                          │
└─────────────────────────────────────────────────────────────────────────┘

[Ctrl+C to cancel (will not interrupt running deployment)]
```

**After completion:**

```bash
✓ Deployment completed successfully!

┌─ DEPLOYMENT SUMMARY ────────────────────────────────────────────────────┐
│ ID:           production-20251004-153344                                │
│ Status:       ✓ Completed                                               │
│ Duration:     4m 48s                                                    │
│ Health:       ● Healthy                                                 │
│ Sync:         ✓ Synced                                                  │
└─────────────────────────────────────────────────────────────────────────┘

┌─ ACTIONS COMPLETED ─────────────────────────────────────────────────────┐
│ ✓ Database backup created (backup-20251004-153347)                      │
│ ✓ 3 application servers updated (rolling update, 0 downtime)            │
│ ✓ CDN cache purged (273 objects)                                        │
│ ✓ Tile cache regeneration queued (background)                           │
│ ✓ Health checks passed (all endpoints responding)                       │
│ ✓ OGC conformance tests passed (48/48)                                  │
└─────────────────────────────────────────────────────────────────────────┘

View details: honua deployment production-20251004-153344
```

### 7. `honua watch` - Live Monitoring (k9s-style)

```bash
$ honua watch

┏━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┓
┃ HONUA LIVE MONITOR                           [q]uit [h]elp [r]efresh  ┃
┣━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┫
┃                                                                        ┃
┃ Environments                                         Updated: 15:45:22 ┃
┃ ┌────────────────────────────────────────────────────────────────────┐ ┃
┃ │ ENV        HEALTH      SYNC      DEPLOYED  COMMIT   UPTIME         │ ┃
┃ ├────────────────────────────────────────────────────────────────────┤ ┃
┃ │ dev        ● Healthy   ✓ Synced  2m ago    def456e  3d 5h          │ ┃
┃ │ staging    ● Healthy   ✓ Synced  5m ago    def456e  7d 12h         │ ┃
┃ │ production ⚠ Degraded  ✓ Synced  2h ago    abc123d  42d 8h         │ ┃
┃ └────────────────────────────────────────────────────────────────────┘ ┃
┃                                                                        ┃
┃ Active Deployments                                                     ┃
┃ ┌────────────────────────────────────────────────────────────────────┐ ┃
┃ │ ID                    ENV     STATE      PROGRESS  ELAPSED          │ ┃
┃ ├────────────────────────────────────────────────────────────────────┤ ┃
┃ │ staging-20251004-1545 staging Applying   [●●●●○○]  2m 15s           │ ┃
┃ └────────────────────────────────────────────────────────────────────┘ ┃
┃                                                                        ┃
┃ System Metrics                                                         ┃
┃ ┌────────────────────────────────────────────────────────────────────┐ ┃
┃ │ ENVIRONMENT  REQUESTS/s  LATENCY (p95)  ERROR RATE  CACHE HIT %   │ ┃
┃ ├────────────────────────────────────────────────────────────────────┤ ┃
┃ │ dev          145          82ms           0.01%       94%           │ ┃
┃ │ staging      423          95ms           0.02%       96%           │ ┃
┃ │ production   2,341        145ms          0.15%       98%           │ ┃
┃ └────────────────────────────────────────────────────────────────────┘ ┃
┃                                                                        ┃
┃ Recent Events                                                          ┃
┃ ┌────────────────────────────────────────────────────────────────────┐ ┃
┃ │ 15:45:18  staging     Deployment started (staging-20251004-1545)   │ ┃
┃ │ 15:42:33  production  Health check: WARNING - High latency         │ ┃
┃ │ 15:40:12  dev         Sync completed (def456e)                     │ ┃
┃ │ 15:38:44  staging     CDN cache purged                             │ ┃
┃ └────────────────────────────────────────────────────────────────────┘ ┃
┃                                                                        ┃
┗━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━┛

[↑↓] Select  [Enter] Details  [l] Logs  [d] Diff  [s] Sync  [h] Help
```

### 8. `honua rollback <environment>` - Rollback Deployment

```bash
$ honua rollback production

┌─ ROLLBACK: production ──────────────────────────────────────────────────┐
│ Current:       def456e (deployed 2h ago)                                │
│ Rollback to:   abc123d (deployed 1d ago)                               │
│ Backup:        backup-20251004-153347                                   │
└─────────────────────────────────────────────────────────────────────────┘

⚠  This will:
  - Restore database from backup
  - Revert metadata to commit abc123d
  - Restart application servers
  - Invalidate CDN and tile caches

Estimated downtime: <1 minute

Continue? [y/N]: y

Rolling back...

┌─ ROLLBACK PROGRESS ─────────────────────────────────────────────────────┐
│ [●●●●●●●●●●●●●●●●●○○○] 85% - Validating rollback...                     │
│                                                                          │
│ ✓ Stopped new requests                                                  │
│ ✓ Restored database from backup (backup-20251004-153347)                │
│ ✓ Reverted metadata to abc123d                                          │
│ ✓ Restarted application servers                                         │
│ ✓ Invalidated CDN cache                                                 │
│ ✓ Invalidated tile cache                                                │
│ ⟳ Running health checks...                                              │
└─────────────────────────────────────────────────────────────────────────┘

✓ Rollback completed successfully!

Production is now running commit abc123d
Downtime: 48 seconds
```

### 9. `honua topology <environment>` - View Topology

```bash
$ honua topology production

┌─ TOPOLOGY: production ──────────────────────────────────────────────────┐
│ Name:     production-us-west                                            │
│ Region:   us-west-2                                                     │
│ Version:  1.0.0                                                         │
└─────────────────────────────────────────────────────────────────────────┘

Architecture:
┌────────────────────────────────────────────────────────────────────────┐
│                                                                        │
│  [User Request]                                                        │
│        ↓                                                               │
│   ┌─────────┐    ┌─────────┐    ┌──────────────┐                     │
│   │   DNS   │ →  │   CDN   │ →  │ Load Balancer│                     │
│   │Cloudflare   │Cloudflare│    │   AWS ALB    │                     │
│   └─────────┘    └─────────┘    └──────────────┘                     │
│                       ↓                  ↓                             │
│                 ┌──────────┐      ┌────────────────┐                  │
│                 │Tile Cache│      │ Honua Servers  │                  │
│                 │   (S3)   │      │ ECS (3 tasks)  │                  │
│                 └──────────┘      └────────────────┘                  │
│                                           ↓                            │
│                                   ┌───────────────┐                   │
│                                   │   Database    │                   │
│                                   │PostgreSQL RDS │                   │
│                                   └───────────────┘                   │
│                                                                        │
└────────────────────────────────────────────────────────────────────────┘

┌─ COMPONENTS ────────────────────────────────────────────────────────────┐
│ COMPONENT         PROVIDER      STATUS     DETAILS                      │
├─────────────────────────────────────────────────────────────────────────┤
│ DNS               Cloudflare    ● Healthy  api.gis.example.com          │
│ CDN               Cloudflare    ● Healthy  Cache hit: 98%               │
│ WAF               Cloudflare    ● Healthy  12 rules active              │
│ Load Balancer     AWS ALB       ● Healthy  3 targets healthy            │
│ App Servers (3)   AWS ECS       ● Healthy  Auto-scaling: 2-10           │
│   - Server 1      ECS Task      ● Healthy  us-west-2a                   │
│   - Server 2      ECS Task      ● Healthy  us-west-2b                   │
│   - Server 3      ECS Task      ● Healthy  us-west-2c                   │
│ Tile Cache        S3            ● Healthy  honua-tiles-production       │
│ Database          RDS Postgres  ● Healthy  db.t3.large (multi-AZ)       │
│ Monitoring        Datadog       ● Healthy  15 alerts configured         │
└─────────────────────────────────────────────────────────────────────────┘

┌─ DEPLOYMENT ACTIONS ────────────────────────────────────────────────────┐
│ PHASE       COMPONENT      ACTION                                       │
├─────────────────────────────────────────────────────────────────────────┤
│ Pre-flight  All            Validate connectivity                        │
│ Database    RDS            Create snapshot                              │
│             RDS            Run migrations                                │
│             RDS            Rebuild spatial indexes (concurrent)          │
│ Application ECS            Rolling update (max unavailable: 0)          │
│             ECS            Health check (interval: 30s)                  │
│ Edge        Cloudflare CDN Selective cache purge                        │
│             S3 Tile Cache  Invalidate + queue regen                     │
│             ALB            Update health check                           │
│ Validation  All            Run OGC conformance tests                    │
│             All            Performance benchmark                         │
└─────────────────────────────────────────────────────────────────────────┘

Commands:
  honua topology discover production   # Auto-discover infrastructure
  honua topology validate production   # Run connectivity tests
```

## Implementation Notes

### Libraries to Use

1. **Spectre.Console** - Rich terminal UI
   ```bash
   dotnet add package Spectre.Console
   ```

2. **Color output:**
   - ● Green = Healthy
   - ⚠ Yellow = Degraded/Warning
   - ✗ Red = Failed/Unhealthy
   - ○ Gray = Pending/Unknown
   - ✓ Checkmark = Completed
   - ⟳ Spinner = In Progress

3. **Live updates:**
   - Use `Spectre.Console.Live` for live progress
   - Update every 1-2 seconds
   - Handle terminal resize

### Key Features

1. **Consistent formatting** - All tables use same box style
2. **Color coding** - Status indicators are immediately visible
3. **Contextual help** - Show relevant commands at bottom
4. **Live updates** - Progress bars and spinners for long operations
5. **Keyboard shortcuts** - Quick navigation in watch mode
6. **Responsive** - Adapts to terminal width

### Example Implementation Snippet

```csharp
using Spectre.Console;

public class StatusRenderer
{
    public void RenderEnvironmentStatus(List<EnvironmentState> environments)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("Environment")
            .AddColumn("Health")
            .AddColumn("Sync")
            .AddColumn("Deployed")
            .AddColumn("Commit");

        foreach (var env in environments)
        {
            table.AddRow(
                env.Environment,
                FormatHealth(env.Health),
                FormatSyncStatus(env.SyncStatus),
                FormatTimestamp(env.LastUpdated),
                env.DeployedCommit ?? "unknown"
            );
        }

        AnsiConsole.Write(table);
    }

    private string FormatHealth(DeploymentHealth health)
    {
        return health switch
        {
            DeploymentHealth.Healthy => "[green]● Healthy[/]",
            DeploymentHealth.Degraded => "[yellow]⚠ Degraded[/]",
            DeploymentHealth.Unhealthy => "[red]✗ Unhealthy[/]",
            _ => "[gray]○ Unknown[/]"
        };
    }
}
```

This CLI UI provides clear, actionable information at a glance while supporting detailed investigation when needed.
