# GitOps for Honua Server

## Overview

The GitOps feature provides declarative infrastructure-as-code capabilities for Honua Server, similar to ArgoCD and Flux CD. It automatically synchronizes your desired state (stored in Git) with your actual deployment state, enabling:

- **Declarative Configuration**: Define all server configuration in Git using YAML/JSON
- **Automated Reconciliation**: Continuously sync Git state to deployed environment
- **Multi-Environment Support**: Manage production, staging, and development from one repository
- **Approval Workflows**: Require manual approval before applying changes (Enterprise)
- **Drift Detection**: Automatically detect and remediate configuration drift
- **Audit Trail**: Complete history of all deployments and changes
- **Rollback Support**: Easily revert to any previous configuration
- **Notifications**: Slack and email notifications for deployment events

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Git Repository                           │
│  ┌────────────────────────────────────────────────────────┐ │
│  │ environments/                                          │ │
│  │   ├── production/                                      │ │
│  │   │   ├── metadata.yaml                                │ │
│  │   │   ├── datasources.yaml                             │ │
│  │   │   ├── collections.yaml                             │ │
│  │   │   └── certificates.yaml                            │ │
│  │   ├── staging/                                         │ │
│  │   └── common/                                          │ │
│  │       └── shared-config.yaml                           │ │
│  └────────────────────────────────────────────────────────┘ │
└────────────────────┬────────────────────────────────────────┘
                     │ Git Pull
                     ▼
         ┌───────────────────────┐
         │     GitWatcher        │  ← Background Service
         │  (Polls every 30s)    │
         └───────────┬───────────┘
                     │ Detects Changes
                     ▼
         ┌───────────────────────┐
         │   HonuaReconciler     │  ← Reconciliation Engine
         │                       │
         │  • MetadataRegistry   │
         │  • StacCatalogStore   │
         │  • DatabaseMigrations │
         │  • CertificateRenewal │
         └───────────┬───────────┘
                     │ Apply Changes
                     ▼
         ┌───────────────────────┐
         │  Deployment State     │  ← Persistent State
         │  (FileStateStore)     │
         └───────────────────────┘
                     │
                     ▼
         ┌───────────────────────┐
         │  Notifications        │
         │  • Slack              │
         │  • Email              │
         └───────────────────────┘
```

## Components

### GitWatcher
Background service that polls the Git repository for changes and triggers reconciliation.

**Responsibilities:**
- Poll Git repository at configured interval (default: 30 seconds)
- Detect new commits on watched branch
- Filter changes by environment path
- Trigger reconciliation for relevant changes
- Track last known commit

**Configuration:**
```json
{
  "GitOps": {
    "Watcher": {
      "Branch": "main",
      "Environment": "production",
      "PollIntervalSeconds": 30
    }
  }
}
```

### HonuaReconciler
Reconciliation engine that applies desired state from Git to the actual deployment.

**Responsibilities:**
- Load configuration files from Git repository
- Compare desired state with current state
- Apply changes to metadata, datasources, certificates, etc.
- Track deployment state and history
- Send notifications on success/failure
- Handle approval workflows (if configured)

**Features:**
- OpenTelemetry instrumentation for observability
- Polly retry policies for transient failures
- Dry-run mode for validation
- Rollback support via deployment history

### LibGit2SharpRepository
Git operations implementation using LibGit2Sharp library.

**Capabilities:**
- Clone/pull repositories
- Fetch commit information
- Compare commits and get changed files
- Read file contents from specific commits
- Support SSH and HTTPS authentication

### State Management
Persistent storage of deployment state and history using `FileStateStore`.

**Stored Information:**
- Deployment ID and timestamp
- Commit SHA and author
- Applied configuration files
- Deployment status (success/failure)
- Error messages and logs

## Configuration

### Required Configuration

```json
{
  "GitOps": {
    "RepositoryPath": "/data/gitops-repo",
    "StateDirectory": "/data/gitops-state",
    "DryRun": false,

    "Watcher": {
      "Branch": "main",
      "Environment": "production",
      "PollIntervalSeconds": 30
    },

    "Credentials": {
      "Username": "git-user",
      "Password": "***"
    }
  }
}
```

### Configuration Validation

On startup, the GitOps service validates:
1. `RepositoryPath` is provided and exists
2. `StateDirectory` is provided
3. Repository path is a valid Git repository
4. Credentials are valid (if authentication required)

**Validation Errors:**
```csharp
throw new InvalidOperationException(
    "GitOps:RepositoryPath configuration is required when GitOps is enabled. " +
    "Please provide a valid path to the Git repository.");
```

### Optional Features

#### Notifications

```json
{
  "GitOps": {
    "Notifications": {
      "Enabled": true,
      "Slack": {
        "Enabled": true,
        "WebhookUrl": "https://hooks.slack.com/services/...",
        "Channel": "#deployments"
      },
      "Email": {
        "Enabled": true,
        "SmtpServer": "smtp.example.com",
        "FromAddress": "gitops@honua.io",
        "ToAddresses": ["ops-team@example.com"]
      }
    }
  }
}
```

#### Approval Workflows

Configure manual approval requirement before deployments:

```json
{
  "GitOps": {
    "Approvals": {
      "Enabled": true,
      "RequireApproval": true,
      "Approvers": ["alice@example.com", "bob@example.com"],
      "AutoApproveAuthors": ["ci-bot@example.com"]
    }
  }
}
```

## Repository Structure

### Standard Directory Layout

```
gitops-repo/
├── environments/
│   ├── production/
│   │   ├── metadata.yaml          # Service metadata
│   │   ├── datasources.yaml       # Data source configurations
│   │   ├── collections.yaml       # Collection definitions
│   │   ├── stac-catalog.yaml      # STAC catalog configuration
│   │   └── certificates.yaml      # SSL certificate configuration
│   ├── staging/
│   │   └── ...                    # Same structure as production
│   ├── development/
│   │   └── ...
│   └── common/
│       └── shared-config.yaml     # Shared configuration
├── migrations/
│   └── V001__initial.sql          # Database migrations
└── README.md
```

### Environment Filtering

GitWatcher only triggers reconciliation when files change in:
- `environments/{configured_environment}/`
- `environments/common/`

**Example:**
```csharp
var relevantFiles = changedFiles.FindAll(f =>
    f.StartsWith($"environments/{_options.Environment}/") ||
    f.StartsWith($"environments/common/"));
```

## Usage

### 1. Initialize Git Repository

```bash
# Create GitOps repository
mkdir gitops-repo && cd gitops-repo
git init

# Create environment structure
mkdir -p environments/production
mkdir -p environments/staging
mkdir -p environments/common

# Create initial configuration
cat > environments/production/metadata.yaml <<EOF
service:
  name: Honua Geospatial API
  version: 1.0.0
  description: Enterprise geospatial data platform
EOF

git add .
git commit -m "Initial GitOps configuration"
```

### 2. Configure Honua Server

Update `appsettings.Production.json`:

```json
{
  "GitOps": {
    "RepositoryPath": "/data/gitops-repo",
    "StateDirectory": "/data/gitops-state",
    "Watcher": {
      "Branch": "main",
      "Environment": "production",
      "PollIntervalSeconds": 30
    },
    "Notifications": {
      "Enabled": true,
      "Slack": {
        "Enabled": true,
        "WebhookUrl": "${SLACK_WEBHOOK_URL}"
      }
    }
  }
}
```

### 3. Enable GitOps in Application

In `Program.cs`:

```csharp
// Add GitOps services (Enterprise feature)
if (builder.Configuration.GetValue<bool>("GitOps:Enabled", false))
{
    builder.Services.AddGitOps(builder.Configuration);
}
```

### 4. Make Configuration Changes

```bash
# Edit configuration
vi environments/production/datasources.yaml

# Commit and push
git add environments/production/datasources.yaml
git commit -m "Add new PostGIS datasource"
git push origin main
```

### 5. Automatic Reconciliation

Within 30 seconds (default poll interval):
1. GitWatcher detects new commit
2. HonuaReconciler loads changed files
3. Changes are applied to deployment
4. Deployment state is saved
5. Notification sent to Slack/Email

## Reconciliation Process

### Reconciliation Workflow

```
┌─────────────────────────────────────────────────────────────┐
│ 1. Load Configuration from Git                              │
│    • Read environment-specific files                         │
│    • Read common shared configuration                        │
│    • Parse YAML/JSON to objects                              │
└────────────────┬────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────┐
│ 2. Request Approval (if configured)                          │
│    • Check if approval required                              │
│    • Wait for manual approval                                │
│    • Auto-approve for trusted authors                        │
└────────────────┬────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────┐
│ 3. Apply Configuration Changes                               │
│    • Update metadata registry                                │
│    • Update STAC catalog                                     │
│    • Run database migrations                                 │
│    • Renew certificates                                      │
└────────────────┬────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────┐
│ 4. Save Deployment State                                     │
│    • Record successful deployment                            │
│    • Store commit SHA and timestamp                          │
│    • Track applied files                                     │
└────────────────┬────────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────────────────────────┐
│ 5. Send Notifications                                        │
│    • Slack message with deployment details                   │
│    • Email notification                                      │
│    • Include commit info and changed files                   │
└─────────────────────────────────────────────────────────────┘
```

### Retry Logic

HonuaReconciler uses Polly for transient failure handling:

```csharp
var retryPolicy = Policy
    .Handle<Exception>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: attempt => TimeSpan.FromSeconds(Math.Pow(2, attempt)),
        onRetry: (exception, timeSpan, retryCount, context) =>
        {
            _logger.LogWarning(
                "Reconciliation failed, retrying in {Delay}s (attempt {RetryCount}/3): {Error}",
                timeSpan.TotalSeconds, retryCount, exception.Message);
        });
```

### Dry Run Mode

Validate configuration without applying changes:

```json
{
  "GitOps": {
    "DryRun": true
  }
}
```

**Dry Run Output:**
```
[WARNING] GitOps reconciler is running in DRY RUN mode - no actual changes will be applied
[INFO] Would update metadata registry with 5 changes
[INFO] Would apply 2 database migrations
[INFO] Would renew 1 certificate
```

## Observability

### OpenTelemetry Instrumentation

HonuaReconciler includes comprehensive telemetry:

```csharp
using var activity = ActivitySource.StartActivity("Reconcile");
activity?.SetTag("environment", environment);
activity?.SetTag("commit", commit);
activity?.SetTag("initiatedBy", initiatedBy);

// Metrics
var timer = Stopwatch.StartNew();
// ... reconciliation logic ...
activity?.SetTag("duration_ms", timer.ElapsedMilliseconds);
activity?.SetTag("status", success ? "success" : "failure");
```

**Available Traces:**
- `gitops.reconcile` - Overall reconciliation span
- `gitops.apply_metadata` - Metadata updates
- `gitops.apply_stac` - STAC catalog updates
- `gitops.apply_migrations` - Database migrations
- `gitops.apply_certificates` - Certificate renewals

### Logging

Structured logging with context:

```csharp
_logger.LogInformation(
    "Reconciliation completed for environment {Environment} at commit {Commit}. " +
    "Duration: {Duration}ms, Success: {Success}",
    environment, commit, duration, success);
```

**Log Levels:**
- `INFO` - Normal operations, deployment success
- `WARNING` - Retries, dry-run mode
- `ERROR` - Reconciliation failures, configuration errors
- `DEBUG` - Detailed file changes, state transitions

## Deployment State

### State Storage

Deployment state is persisted to `{StateDirectory}/deployments.json`:

```json
{
  "deployments": [
    {
      "id": "dep_20250130_143022",
      "environment": "production",
      "commit": "a1b2c3d4",
      "author": "Alice <alice@example.com>",
      "timestamp": "2025-01-30T14:30:22Z",
      "status": "success",
      "appliedFiles": [
        "environments/production/metadata.yaml",
        "environments/production/datasources.yaml"
      ],
      "duration_ms": 2341
    }
  ]
}
```

### Querying State

```csharp
var stateStore = serviceProvider.GetRequiredService<IDeploymentStateStore>();

// Get latest deployment
var latest = await stateStore.GetLatestDeploymentAsync("production");

// Get deployment history
var history = await stateStore.GetDeploymentHistoryAsync("production", limit: 10);

// Get specific deployment
var deployment = await stateStore.GetDeploymentAsync("dep_20250130_143022");
```

## Rollback

### Automatic Rollback

To rollback to a previous configuration:

```bash
# Find previous good commit
git log --oneline

# Revert to previous commit
git revert HEAD
git push origin main

# GitWatcher will detect and apply the revert
```

### Manual Rollback via CLI

```bash
# List deployment history
honua gitops deployments --environment production

# Rollback to specific deployment
honua gitops rollback --deployment dep_20250130_143022

# Rollback to specific commit
honua gitops rollback --commit a1b2c3d4
```

## Security Considerations

### Git Credentials

**SSH Key Authentication (Recommended):**
```bash
# Generate SSH key
ssh-keygen -t ed25519 -C "gitops@honua.io"

# Add to SSH agent
eval "$(ssh-agent -s)"
ssh-add ~/.ssh/id_ed25519

# Configure in appsettings (no credentials needed)
{
  "GitOps": {
    "RepositoryPath": "/data/gitops-repo"
    // No Credentials section needed for SSH
  }
}
```

**HTTPS Authentication:**
```json
{
  "GitOps": {
    "Credentials": {
      "Username": "git-user",
      "Password": "${GIT_PASSWORD}"  // Use environment variable
    }
  }
}
```

### Access Control

**Repository Access:**
- Use read-only deploy keys for production
- Restrict write access to GitOps repository
- Enable branch protection rules
- Require pull request reviews
- Enable commit signing

**Approval Workflows:**
```json
{
  "GitOps": {
    "Approvals": {
      "Enabled": true,
      "RequireApproval": true,
      "Approvers": ["ops-lead@example.com"],
      "AutoApproveAuthors": ["ci-bot@example.com"],
      "MinApprovers": 2  // Require 2 approvals
    }
  }
}
```

### Secrets Management

**Never commit secrets to Git!**

Use environment variable substitution:

```yaml
# environments/production/datasources.yaml
datasources:
  - name: main-db
    connection_string: "${DATABASE_CONNECTION_STRING}"

  - name: cache
    redis_url: "${REDIS_URL}"
```

Configure environment variables:
```bash
export DATABASE_CONNECTION_STRING="Host=db.example.com;..."
export REDIS_URL="redis://cache.example.com:6379"
```

## Troubleshooting

### GitWatcher Not Detecting Changes

**Symptoms:**
- Changes committed to Git but not applied
- No logs from GitWatcher

**Solutions:**
1. Check GitWatcher is running:
```bash
# In server logs
grep "GitWatcher started" logs/honua-server.log
```

2. Verify branch configuration:
```json
{
  "GitOps": {
    "Watcher": {
      "Branch": "main"  // Must match your Git branch
    }
  }
}
```

3. Check file paths:
```bash
# Changes must be in environment-specific path
git log --oneline --name-only | grep "environments/production"
```

### Authentication Failures

**Symptoms:**
```
Failed to pull from Git repository: authentication failed
```

**Solutions:**
1. Verify credentials:
```bash
# Test Git access
cd /data/gitops-repo
git pull origin main
```

2. Check SSH key permissions:
```bash
chmod 600 ~/.ssh/id_ed25519
ssh -T git@github.com
```

3. Use verbose logging:
```json
{
  "Logging": {
    "LogLevel": {
      "Honua.Server.Enterprise.GitOps": "Debug"
    }
  }
}
```

### Reconciliation Failures

**Symptoms:**
- Deployment state shows "failure" status
- Error notifications sent

**Solutions:**
1. Check error logs:
```bash
grep "Reconciliation failed" logs/honua-server.log
```

2. Validate configuration files:
```bash
# Test YAML syntax
yamllint environments/production/*.yaml
```

3. Use dry-run mode:
```json
{
  "GitOps": {
    "DryRun": true
  }
}
```

4. Check OpenTelemetry traces for detailed error context

### Repository Validation Errors

**Symptoms:**
```
InvalidOperationException: Path is not a valid Git repository
```

**Solutions:**
1. Verify repository exists:
```bash
ls -la /data/gitops-repo/.git
```

2. Initialize if needed:
```bash
cd /data/gitops-repo
git init
```

3. Clone from remote:
```bash
git clone https://github.com/org/gitops-repo.git /data/gitops-repo
```

## Advanced Usage

### Custom Reconciliation Logic

Extend `HonuaReconciler` for custom reconciliation:

```csharp
public class CustomReconciler : HonuaReconciler
{
    protected override async Task ApplyCustomChangesAsync(
        string environment,
        string commit,
        CancellationToken cancellationToken)
    {
        // Custom reconciliation logic
        await ApplyNetworkPoliciesAsync(environment);
        await UpdateServiceMeshConfigAsync(environment);
    }
}
```

### Multi-Cluster Deployments

Configure multiple environments with different clusters:

```json
{
  "GitOps": {
    "Environments": [
      {
        "Name": "production-us-east",
        "Branch": "main",
        "RepositoryPath": "/data/gitops-repo",
        "PollIntervalSeconds": 30
      },
      {
        "Name": "production-eu-west",
        "Branch": "main",
        "RepositoryPath": "/data/gitops-repo",
        "PollIntervalSeconds": 30
      }
    ]
  }
}
```

### Progressive Rollouts

Use Git branches for progressive rollouts:

```bash
# Create canary branch
git checkout -b canary/v2.0
# ... make changes ...
git push origin canary/v2.0

# Configure canary environment
{
  "GitOps": {
    "Watcher": {
      "Branch": "canary/v2.0",
      "Environment": "canary"
    }
  }
}

# After validation, merge to main
git checkout main
git merge canary/v2.0
git push origin main
```

## Related Documentation

- [Enterprise Features Overview](../ENTERPRISE_FEATURES.md)
- [Data Versioning](../Versioning/README.md)
- [Multitenancy](../Multitenancy/README.md)
- [Admin Dashboard](../AdminDashboard/README.md)

## References

- [LibGit2Sharp Documentation](https://github.com/libgit2/libgit2sharp)
- [ArgoCD Concepts](https://argo-cd.readthedocs.io/en/stable/core_concepts/)
- [Flux CD Documentation](https://fluxcd.io/docs/)
- [GitOps Principles](https://opengitops.dev/)
