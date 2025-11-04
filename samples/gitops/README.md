# Honua GitOps Configuration Example

This directory demonstrates a complete GitOps setup for Honua with multiple environments.

## Repository Structure

```
honua-config/                    # Git repository
├── environments/
│   ├── development/
│   │   ├── metadata.yaml        # Dev-specific metadata
│   │   ├── datasources.yaml     # Dev database connections
│   │   └── config.yaml          # Dev server configuration
│   │
│   ├── staging/
│   │   ├── metadata.yaml        # Staging metadata
│   │   ├── datasources.yaml     # Staging database connections
│   │   └── config.yaml          # Staging server configuration
│   │
│   ├── production/
│   │   ├── metadata.yaml        # Production metadata
│   │   ├── datasources.yaml     # Production database connections
│   │   └── config.yaml          # Production server configuration
│   │
│   └── common/
│       └── base-metadata.yaml   # Shared metadata (base layers, etc.)
│
├── .gitops/
│   └── deployment-policy.yaml   # Deployment policies and approval rules
│
└── README.md
```

## How It Works

### 1. GitOps Controller (Pull-Based)

The Honua GitOps controller runs on each environment's server:

```yaml
# Honua server configuration
gitops:
  enabled: true
  repository:
    url: https://github.com/your-org/honua-config.git
    branch: main
    path: /etc/honua/config-repo

  environment: production  # or development, staging

  polling:
    intervalSeconds: 30    # Check for changes every 30 seconds

  stateStore:
    type: file
    path: /var/lib/honua/gitops-state
```

### 2. Deployment Flow

```
Developer/AI → Creates PR → Merges to main
                    ↓
            Git Repository Updated
                    ↓
    Production Server (polls every 30s)
                    ↓
         Detects New Commit
                    ↓
            Reconciler Runs
                    ↓
      Compare Desired vs Actual
                    ↓
         Create Deployment Plan
                    ↓
    Apply Changes (if no approval needed)
         OR
    Wait for Approval (production)
                    ↓
         Reload Metadata
                    ↓
              Done!
```

### 3. Multi-Environment Strategy

**Development:**
- Auto-deploy on merge (no approval)
- Uses `environments/development/` + `environments/common/`
- Fast feedback loop

**Staging:**
- Auto-deploy on merge (no approval)
- Uses `environments/staging/` + `environments/common/`
- Pre-production testing

**Production:**
- Requires manual approval
- Uses `environments/production/` + `environments/common/`
- Approval via API or CLI:
  ```bash
  honua deployment approve <deployment-id>
  ```

### 4. Deployment Policies

```yaml
# .gitops/deployment-policy.yaml
apiVersion: honua.io/v1
kind: DeploymentPolicy

environments:
  development:
    autoApprove: true
    requiresValidation: true
    allowBreakingChanges: true

  staging:
    autoApprove: true
    requiresValidation: true
    allowBreakingChanges: true

  production:
    autoApprove: false      # Manual approval required
    requiresValidation: true
    allowBreakingChanges: false

    # Approval required if:
    approvalRules:
      - resourcesRemoved: true
      - resourcesModified: "> 10"
      - breakingChanges: true

    # Additional safety checks
    safety:
      requireBackup: true
      healthCheckTimeout: 300
      rollbackOnFailure: true
```

## Usage Examples

### Example 1: Add a New Layer (via AI Consultant)

```bash
# User talks to AI Consultant
$ honua ai "Add a new layer for bike lanes from table public.bike_lanes"

# AI Consultant:
# 1. Analyzes database schema
# 2. Generates metadata YAML
# 3. Creates PR with changes to environments/production/metadata.yaml

# User reviews PR
$ gh pr view 42

# Merge PR
$ gh pr merge 42

# Production GitOps controller (within 30 seconds):
# - Detects new commit
# - Creates deployment plan
# - Waits for approval (production requires it)

# User approves deployment
$ honua deployment approve prod-20241004123456-abc123

# GitOps controller:
# - Applies changes
# - Reloads metadata
# - New layer is live!
```

### Example 2: Modify Existing Configuration

```bash
# Edit configuration in Git
$ git clone https://github.com/your-org/honua-config.git
$ cd honua-config

# Make changes
$ vim environments/production/metadata.yaml
# Change caching TTL for parcels layer

$ git add environments/production/metadata.yaml
$ git commit -m "Increase cache TTL for parcels layer"
$ git push

# Production server automatically:
# - Detects commit
# - Creates deployment plan:
#     Modified: environments/production/metadata.yaml
#     Changes: parcels.caching.ttl: 3600 -> 7200
# - Requires approval
# - After approval: reloads metadata
```

### Example 3: Emergency Rollback

```bash
# Something went wrong, rollback!
$ honua rollback production

# GitOps controller:
# - Finds last successful deployment
# - Creates rollback deployment
# - Reverts Git to previous commit
# - Reloads metadata
```

## Monitoring Deployments

### Check Environment Status

```bash
$ honua status

╭─ Honua GitOps Status ─────────────────────────╮
│                                               │
│ Environment: production                       │
│ Current Commit: a1b2c3d                      │
│ Sync Status: ✓ Synced                        │
│ Health: ✓ Healthy                            │
│                                               │
│ Last Deployment:                              │
│   ID: prod-20241004123456-abc123             │
│   State: Completed                            │
│   Duration: 2.3s                              │
│   By: mike                                    │
│                                               │
╰───────────────────────────────────────────────╯
```

### View Deployment History

```bash
$ honua deployments production

┌──────────────────────────────┬───────────┬──────────┬──────────┐
│ Deployment ID                │ State     │ Duration │ By       │
├──────────────────────────────┼───────────┼──────────┼──────────┤
│ prod-20241004123456-abc123  │ Completed │ 2.3s     │ mike     │
│ prod-20241004120000-def456  │ Completed │ 1.8s     │ ai-agent │
│ prod-20241003180000-ghi789  │ Failed    │ 0.5s     │ jane     │
└──────────────────────────────┴───────────┴──────────┴──────────┘
```

### Watch Deployments Live

```bash
$ honua watch

# Live-updating dashboard showing:
# - Current deployment status
# - Health metrics
# - Recent changes
# - Sync status
```

## Advantages of This Approach

### ✅ Works Behind Firewalls
- Pull-based (outbound HTTPS only)
- No inbound connections needed
- No cloud credentials in GitHub Actions

### ✅ Audit Trail
- All changes in Git history
- Deployment state tracked
- Easy to see who changed what when

### ✅ Rollback
- `git revert` = instant rollback
- Deployment history preserved
- Can rollback to any previous state

### ✅ Multi-Environment
- Same codebase, different configs
- Promote changes dev → staging → prod
- Environment-specific policies

### ✅ AI-Friendly
- AI Consultant creates PRs
- Humans review before merge
- GitOps deploys automatically

## Integration with CI/CD

### GitHub Actions (Optional)

```yaml
# .github/workflows/validate.yml
name: Validate Honua Configuration

on:
  pull_request:
    paths:
      - 'environments/**'

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Validate YAML
        run: |
          # Run Honua configuration validator
          honua validate --path environments/

      - name: Check for breaking changes
        run: |
          # Detect breaking changes
          honua diff --from ${{ github.base_ref }} --to ${{ github.head_ref }}
```

## Next Steps

1. **Set up Git repository** for your configuration
2. **Configure Honua servers** to enable GitOps
3. **Deploy GitOps controller** on each environment
4. **Configure AI Consultant** to create PRs
5. **Test the flow** with a simple change

See `docs/dev/gitops-controller-design.md` for detailed architecture.
