# Honua GitOps Controller Design

## The Problem with Push-Based Deployments

### GitHub Actions (Push-Based) Issues:

```
GitHub Actions → Firewall → Production Network → Honua Servers
                    ❌              ❌                ❌
```

**Problems:**
1. **Network Access** - Production networks often block incoming connections
2. **Firewall Rules** - Opening holes for GitHub runners is a security risk
3. **Credentials** - GitHub needs credentials to access production (secret sprawl)
4. **NAT/VPN Issues** - Complex networking setups break push deployments
5. **Outage Risk** - If GitHub is down, you can't deploy (or rollback!)
6. **Audit Trail** - Hard to prove who deployed what when it's a GitHub service account

## Pull-Based GitOps Controller (ArgoCD/FluxCD Pattern)

### Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                     PRODUCTION NETWORK                           │
│  ┌──────────────────────────────────────────────────────────┐  │
│  │              Honua GitOps Controller                      │  │
│  │                                                            │  │
│  │  ┌─────────────┐    ┌──────────────┐   ┌──────────────┐ │  │
│  │  │Git Watcher  │───▶│ Reconciler   │──▶│  Deployer    │ │  │
│  │  │(Poll/Webhook)│    │(Diff Engine) │   │(State Machine│ │  │
│  │  └─────────────┘    └──────────────┘   └──────────────┘ │  │
│  │         │                                       │         │  │
│  └─────────┼───────────────────────────────────────┼─────────┘  │
│            │ Outbound HTTPS                        │             │
│            │ (Firewall-friendly)                   │             │
│            ▼                                       ▼             │
│  ┌──────────────────┐                   ┌──────────────────┐   │
│  │   GitHub.com     │                   │ Honua Servers    │   │
│  │  (Git Repo)      │                   │   Database       │   │
│  └──────────────────┘                   │   Topology       │   │
│                                          └──────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### Benefits of Pull-Based:

✅ **No Inbound Firewall Rules** - Only outbound HTTPS (port 443)
✅ **Works Behind NAT/VPN** - Controller initiates all connections
✅ **No Cloud Credentials in GitHub** - Secrets stay in production network
✅ **Resilient to GitHub Outages** - Can still operate from Git cache
✅ **Clear Audit Trail** - Controller runs with service account, logs everything
✅ **Multi-Cluster** - One controller can manage dev/staging/prod

## Honua GitOps Controller Components

### 1. Git Watcher

**Responsibilities:**
- Poll Git repository for changes
- Or listen to webhooks (if firewall allows)
- Detect which environments are affected
- Clone/pull repository

**Implementation:**

```csharp
// src/Honua.GitOps.Controller/GitWatcher.cs
public class GitWatcher : BackgroundService
{
    private readonly string _repositoryUrl;
    private readonly string _branch;
    private readonly TimeSpan _pollInterval;
    private readonly IDeploymentOrchestrator _orchestrator;
    private string _lastCommit = "";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var currentCommit = await GetLatestCommitAsync();

                if (currentCommit != _lastCommit)
                {
                    _logger.LogInformation(
                        "New commit detected: {OldCommit} → {NewCommit}",
                        _lastCommit, currentCommit);

                    // Determine what changed
                    var changes = await DetectChangesAsync(_lastCommit, currentCommit);

                    // Trigger reconciliation
                    await _orchestrator.ReconcileAsync(changes, stoppingToken);

                    _lastCommit = currentCommit;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in Git watcher");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }
    }

    private async Task<string> GetLatestCommitAsync()
    {
        // Use LibGit2Sharp or run git command
        using var repo = new Repository(_localPath);

        // Fetch from remote
        var remote = repo.Network.Remotes["origin"];
        var refSpecs = remote.FetchRefSpecs.Select(x => x.Specification);

        Commands.Fetch(repo, remote.Name, refSpecs, null, "");

        var remoteBranch = repo.Branches[$"origin/{_branch}"];
        return remoteBranch.Tip.Sha;
    }

    private async Task<List<EnvironmentChange>> DetectChangesAsync(
        string oldCommit,
        string newCommit)
    {
        using var repo = new Repository(_localPath);

        var oldTree = repo.Lookup<Commit>(oldCommit)?.Tree;
        var newTree = repo.Lookup<Commit>(newCommit)?.Tree;

        var changes = new List<EnvironmentChange>();

        foreach (var change in repo.Diff.Compare<TreeChanges>(oldTree, newTree))
        {
            // Parse path: environments/production/layers/parcels.yaml
            var pathParts = change.Path.Split('/');

            if (pathParts[0] == "environments" && pathParts.Length >= 2)
            {
                var environment = pathParts[1];

                changes.Add(new EnvironmentChange
                {
                    Environment = environment,
                    Path = change.Path,
                    Status = change.Status,
                    OldCommit = oldCommit,
                    NewCommit = newCommit
                });
            }
        }

        return changes;
    }
}
```

### 2. Reconciler (Diff Engine)

**Responsibilities:**
- Compare desired state (Git) vs actual state (deployed)
- Generate deployment plan
- Calculate diff
- Assess risk

**Implementation:**

```csharp
// src/Honua.GitOps.Controller/Reconciler.cs
public class Reconciler
{
    private readonly IMetadataRegistry _deployedMetadata;
    private readonly IDeploymentStateStore _stateStore;

    public async Task<ReconciliationPlan> ReconcileAsync(
        string environment,
        string gitCommit,
        CancellationToken cancellationToken)
    {
        // 1. Load desired state from Git
        var desiredState = await LoadStateFromGitAsync(environment, gitCommit);

        // 2. Load actual state from deployed environment
        var actualState = await LoadDeployedStateAsync(environment);

        // 3. Calculate diff
        var diff = CalculateDiff(actualState, desiredState);

        // 4. Check if already synced
        if (!diff.HasChanges)
        {
            _logger.LogInformation(
                "Environment {Environment} is already synced at {Commit}",
                environment, gitCommit);

            await _stateStore.UpdateSyncStatusAsync(
                environment,
                SyncStatus.Synced);

            return ReconciliationPlan.NoChanges;
        }

        // 5. Mark as out of sync
        await _stateStore.UpdateSyncStatusAsync(
            environment,
            SyncStatus.OutOfSync);

        // 6. Generate deployment plan
        var plan = await GenerateDeploymentPlanAsync(
            environment,
            diff,
            gitCommit);

        return plan;
    }

    private StateDiff CalculateDiff(
        EnvironmentState actual,
        EnvironmentState desired)
    {
        var diff = new StateDiff();

        // Compare layers
        var actualLayers = actual.Layers.ToDictionary(l => l.Name);
        var desiredLayers = desired.Layers.ToDictionary(l => l.Name);

        // Added layers
        foreach (var layer in desiredLayers.Values)
        {
            if (!actualLayers.ContainsKey(layer.Name))
            {
                diff.Added.Add(new ResourceChange
                {
                    Type = "Layer",
                    Name = layer.Name,
                    Path = layer.SourcePath
                });
            }
        }

        // Removed layers
        foreach (var layer in actualLayers.Values)
        {
            if (!desiredLayers.ContainsKey(layer.Name))
            {
                diff.Removed.Add(new ResourceChange
                {
                    Type = "Layer",
                    Name = layer.Name,
                    IsBreaking = true  // Removing layers is breaking
                });
            }
        }

        // Modified layers
        foreach (var layer in desiredLayers.Values)
        {
            if (actualLayers.TryGetValue(layer.Name, out var actualLayer))
            {
                if (!LayersAreEqual(actualLayer, layer))
                {
                    diff.Modified.Add(new ResourceChange
                    {
                        Type = "Layer",
                        Name = layer.Name,
                        Path = layer.SourcePath,
                        Diff = GenerateLayerDiff(actualLayer, layer),
                        IsBreaking = IsBreakingLayerChange(actualLayer, layer)
                    });
                }
            }
        }

        return diff;
    }
}
```

### 3. Deployment Orchestrator

**Responsibilities:**
- Execute deployment plan
- Coordinate with topology
- Handle rollback
- Update state

**Implementation:**

```csharp
// src/Honua.GitOps.Controller/DeploymentOrchestrator.cs
public class DeploymentOrchestrator
{
    private readonly Reconciler _reconciler;
    private readonly IDeploymentStateStore _stateStore;
    private readonly ITopologyProvider _topology;
    private readonly IPolicyValidator _policyValidator;

    public async Task ReconcileAsync(
        List<EnvironmentChange> changes,
        CancellationToken cancellationToken)
    {
        // Group changes by environment
        var changesByEnvironment = changes
            .GroupBy(c => c.Environment)
            .ToDictionary(g => g.Key, g => g.ToList());

        foreach (var (environment, envChanges) in changesByEnvironment)
        {
            try
            {
                await ReconcileEnvironmentAsync(
                    environment,
                    envChanges,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to reconcile environment {Environment}",
                    environment);
            }
        }
    }

    private async Task ReconcileEnvironmentAsync(
        string environment,
        List<EnvironmentChange> changes,
        CancellationToken cancellationToken)
    {
        var latestCommit = changes.First().NewCommit;

        // 1. Generate reconciliation plan
        var plan = await _reconciler.ReconcileAsync(
            environment,
            latestCommit,
            cancellationToken);

        if (plan.IsNoOp)
        {
            _logger.LogInformation(
                "No changes needed for {Environment}",
                environment);
            return;
        }

        // 2. Validate against policies
        var policyResult = await _policyValidator.ValidateAsync(
            environment,
            plan);

        if (!policyResult.IsValid)
        {
            _logger.LogWarning(
                "Deployment blocked by policy: {Reason}",
                policyResult.Reason);

            // Create deployment record in "AwaitingApproval" state
            await _stateStore.CreateDeploymentAsync(
                environment,
                latestCommit,
                autoRollback: true,
                cancellationToken: cancellationToken);

            return;
        }

        // 3. Check auto-sync policy
        var autoSync = await ShouldAutoSyncAsync(environment, plan);

        if (!autoSync)
        {
            _logger.LogInformation(
                "Auto-sync disabled for {Environment}, awaiting approval",
                environment);

            await _stateStore.CreateDeploymentAsync(
                environment,
                latestCommit,
                autoRollback: true,
                cancellationToken: cancellationToken);

            return;
        }

        // 4. Execute deployment
        await ExecuteDeploymentAsync(environment, plan, latestCommit, cancellationToken);
    }

    private async Task ExecuteDeploymentAsync(
        string environment,
        ReconciliationPlan plan,
        string commit,
        CancellationToken cancellationToken)
    {
        // Create deployment record
        var deployment = await _stateStore.CreateDeploymentAsync(
            environment,
            commit,
            branch: "main",
            initiatedBy: "gitops-controller",
            autoRollback: true,
            cancellationToken: cancellationToken);

        try
        {
            // Run deployment state machine
            await RunDeploymentStateMachineAsync(deployment, plan, cancellationToken);

            // Mark as synced
            await _stateStore.UpdateSyncStatusAsync(
                deployment.Id,
                SyncStatus.Synced,
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Deployment failed: {DeploymentId}", deployment.Id);

            if (deployment.AutoRollback)
            {
                await RollbackAsync(deployment, cancellationToken);
            }
        }
    }
}
```

### 4. Webhook Listener (Optional)

**For faster reconciliation when firewall allows incoming webhooks:**

```csharp
// src/Honua.GitOps.Controller/WebhookListener.cs
[ApiController]
[Route("api/gitops/webhook")]
public class GitHubWebhookController : ControllerBase
{
    private readonly IDeploymentOrchestrator _orchestrator;
    private readonly IConfiguration _config;

    [HttpPost("github")]
    public async Task<IActionResult> HandleGitHubWebhook(
        [FromBody] GitHubWebhookPayload payload,
        [FromHeader(Name = "X-Hub-Signature-256")] string signature)
    {
        // 1. Verify webhook signature
        if (!VerifySignature(payload, signature))
        {
            return Unauthorized();
        }

        // 2. Handle push event
        if (payload.Ref == $"refs/heads/{_config["Git:Branch"]}")
        {
            _logger.LogInformation(
                "Webhook received: Push to {Branch} ({Commit})",
                _config["Git:Branch"],
                payload.After);

            // Trigger immediate reconciliation
            _ = Task.Run(async () =>
            {
                var changes = await DetectChangesAsync(
                    payload.Before,
                    payload.After);

                await _orchestrator.ReconcileAsync(
                    changes,
                    CancellationToken.None);
            });

            return Ok();
        }

        return Ok();
    }

    private bool VerifySignature(GitHubWebhookPayload payload, string signature)
    {
        var secret = _config["GitHub:WebhookSecret"];
        var json = JsonSerializer.Serialize(payload);

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(json));
        var computed = "sha256=" + BitConverter.ToString(hash)
            .Replace("-", "").ToLower();

        return computed == signature;
    }
}
```

## Configuration

### Controller Configuration

```yaml
# config/gitops-controller.yaml
apiVersion: honua.io/v1
kind: GitOpsController
metadata:
  name: honua-controller
  environment: production

spec:
  git:
    url: https://github.com/city/honua-config.git
    branch: main
    credentials:
      type: ssh-key
      secretRef: git-deploy-key

    # Poll-based (works everywhere)
    poll:
      enabled: true
      interval: 30s  # Check for changes every 30s

    # Webhook-based (optional, faster)
    webhook:
      enabled: false  # Enable if firewall allows
      port: 9000
      path: /api/gitops/webhook/github
      secret: ${WEBHOOK_SECRET}

  reconciliation:
    # How often to reconcile (even if no Git changes)
    interval: 5m

    # Retry failed reconciliations
    retry:
      enabled: true
      maxAttempts: 3
      backoff: exponential
      initialDelay: 1m

  sync:
    # Auto-sync policies per environment
    dev:
      autoSync: true
      prune: true  # Delete resources not in Git

    staging:
      autoSync: true
      prune: false

    production:
      autoSync: false  # Require manual approval
      prune: false

  notifications:
    slack:
      enabled: true
      webhook: ${SLACK_WEBHOOK}
      channel: "#gis-deployments"
      events:
        - sync-started
        - sync-completed
        - sync-failed
        - out-of-sync-detected

  health:
    # Health check configuration
    checks:
      - type: git-connectivity
        interval: 1m
      - type: deployment-state
        interval: 30s
```

## Deployment Modes

### Mode 1: Fully Automatic (Dev)

```yaml
environments:
  dev:
    autoSync: true
    prune: true
```

**Behavior:**
```
Git commit → Controller detects → Auto-deploys → Updates state
```

### Mode 2: Manual Approval (Production)

```yaml
environments:
  production:
    autoSync: false
```

**Behavior:**
```
Git commit → Controller detects → Creates deployment (AwaitingApproval)
          → Human approves via CLI/UI → Deploys → Updates state
```

### Mode 3: Hybrid (Staging)

```yaml
environments:
  staging:
    autoSync: true
    conditions:
      - type: time-window
        days: [Mon, Tue, Wed, Thu, Fri]
        hours: "09:00-17:00"
```

**Behavior:**
```
Git commit during window → Auto-deploys
Git commit outside window → Awaits approval
```

## Integration with Existing Components

### With Deployment State Machine

```csharp
// Controller uses the same state machine
var deployment = await _stateStore.CreateDeploymentAsync(...);

// Transitions through states
await _stateStore.TransitionAsync(deployment.Id, DeploymentState.Validating);
await _stateStore.TransitionAsync(deployment.Id, DeploymentState.Planning);
// ... etc
```

### With Topology

```csharp
// Controller loads topology and coordinates changes
var topology = await _topology.LoadAsync(environment);

// Execute topology-aware deployment
await topology.ExecuteDeploymentAsync(deployment, plan);
```

### With CLI/UI

Users can interact with controller-managed deployments:

```bash
# View controller status
$ honua controller status

┌─ GitOps Controller Status ──────────────────────────────────────┐
│ Status:          ● Running                                       │
│ Git Connected:   ✓ Yes (last check: 5s ago)                     │
│ Last Sync:       2m ago                                          │
│ Watching:        github.com/city/honua-config (main)            │
│ Next Poll:       25s                                             │
└──────────────────────────────────────────────────────────────────┘

# Trigger manual sync
$ honua controller sync production

# Approve pending deployment
$ honua deployment approve production-20251004-153344
```

## Security Considerations

### 1. Git Credentials

Store as Kubernetes secret or environment variable:

```bash
# SSH key (recommended)
kubectl create secret generic git-deploy-key \
  --from-file=ssh-privatekey=/path/to/key \
  --from-file=known_hosts=/path/to/known_hosts

# Or personal access token
kubectl create secret generic git-token \
  --from-literal=token=ghp_xxxxxxxxxxxx
```

### 2. RBAC for Controller

```yaml
# Controller service account permissions
apiVersion: v1
kind: ServiceAccount
metadata:
  name: honua-gitops-controller

---
apiVersion: rbac.authorization.k8s.io/v1
kind: ClusterRole
metadata:
  name: honua-gitops-controller
rules:
  - apiGroups: [""]
    resources: ["configmaps", "secrets"]
    verbs: ["get", "list", "watch", "update"]

  - apiGroups: ["honua.io"]
    resources: ["deployments", "topologies"]
    verbs: ["get", "list", "watch", "create", "update", "patch"]
```

## Comparison: Push vs Pull

| Aspect | Push (GitHub Actions) | Pull (Controller) |
|--------|----------------------|-------------------|
| **Firewall** | ❌ Requires inbound rules | ✅ Outbound only |
| **NAT/VPN** | ❌ Complex | ✅ Works seamlessly |
| **Credentials** | ❌ In GitHub secrets | ✅ Local to environment |
| **GitHub Outage** | ❌ Can't deploy | ✅ Can still operate |
| **Multi-Environment** | ❌ Complex workflows | ✅ Natural fit |
| **Audit Trail** | ⚠️ GitHub service account | ✅ Clear controller identity |
| **Real-time Sync** | ⚠️ On push only | ✅ Continuous reconciliation |

## Implementation Plan

### Phase 1: Basic Controller
- [x] Git polling
- [x] Change detection
- [x] Reconciliation loop
- [x] Integration with state store

### Phase 2: Advanced Features
- [ ] Webhook support
- [ ] Sync waves
- [ ] Health checks
- [ ] Notifications

### Phase 3: HA & Scale
- [ ] Multi-replica controller
- [ ] Leader election
- [ ] Distributed state

## Summary

**Controller replaces GitHub Actions for deployment execution while keeping GitHub for approvals:**

```
User → AI → Git commit → PR (GitHub) → Merge
                                         ↓
                            Controller detects → Reconciles → Deploys
```

This gives you:
- ✅ Firewall-friendly (outbound only)
- ✅ Secure (no cloud credentials in GitHub)
- ✅ Resilient (works during GitHub outages)
- ✅ Continuous (always reconciling desired vs actual state)
- ✅ GitOps-native (pull-based, declarative)

The controller runs inside your network and pulls changes from Git, just like ArgoCD/FluxCD!
