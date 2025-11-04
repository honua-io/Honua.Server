# GitOps Configuration Reference

**Version:** 1.0
**Last Updated:** 2025-10-23
**Audience:** DevOps Engineers, System Administrators

Complete reference for all GitOps configuration options in Honua.

---

## Table of Contents

1. [appsettings.json Schema](#appsettingsjson-schema)
2. [Environment-Specific Configuration](#environment-specific-configuration)
3. [Deployment Policies](#deployment-policies)
4. [Security Configuration](#security-configuration)
5. [Performance Tuning](#performance-tuning)
6. [Advanced Options](#advanced-options)

---

## appsettings.json Schema

### Complete GitOps Configuration

```json
{
  "GitOps": {
    "RepositoryPath": "/var/honua/gitops-repo",
    "StateDirectory": "/var/honua/deployments",
    "Credentials": {
      "Username": "",
      "Password": ""
    },
    "Watcher": {
      "Branch": "main",
      "Environment": "production",
      "PollIntervalSeconds": 30
    },
    "DeploymentPolicy": {
      "RequiresApproval": true,
      "ApprovalTimeout": "24:00:00",
      "AllowedDays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
      "AllowedHours": {
        "Start": "09:00",
        "End": "17:00"
      },
      "AutoRollback": true,
      "MinimumRiskLevelForApproval": "Medium"
    },
    "DryRun": false,
    "EnableMetrics": true,
    "EnableTracing": true
  }
}
```

### Configuration Options Reference

#### GitOps Root Section

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `RepositoryPath` | string | Yes | N/A | Local path where Git repository is/will be cloned |
| `StateDirectory` | string | Yes | N/A | Directory for deployment state JSON files |
| `Credentials` | object | No | `{}` | Git authentication credentials (prefer SSH keys) |
| `Watcher` | object | Yes | N/A | GitWatcher configuration |
| `DeploymentPolicy` | object | No | See below | Deployment approval and scheduling policies |
| `DryRun` | boolean | No | `false` | When true, simulate deployments without applying changes |
| `EnableMetrics` | boolean | No | `true` | Enable OpenTelemetry metrics |
| `EnableTracing` | boolean | No | `true` | Enable distributed tracing |

#### RepositoryPath

**Type:** `string`
**Required:** Yes
**Example:** `/var/honua/gitops-repo`

The local filesystem path where the Git configuration repository will be cloned.

**Requirements:**
- Must be an absolute path
- Parent directory must exist
- User running Honua must have write permissions
- Should be persistent storage (not `/tmp`)
- Recommended: Dedicated directory on fast disk (SSD)

**Notes:**
- Directory will be created if it doesn't exist
- Must contain a valid Git repository (initialized or cloned)
- Repository is cloned automatically on first startup if directory is empty

**Production Example:**
```json
{
  "GitOps": {
    "RepositoryPath": "/var/honua/gitops-repo"
  }
}
```

**Development Example:**
```json
{
  "GitOps": {
    "RepositoryPath": "/home/developer/honua-config"
  }
}
```

#### StateDirectory

**Type:** `string`
**Required:** Yes
**Example:** `/var/honua/deployments`

Directory where deployment state files are stored.

**Requirements:**
- Must be an absolute path
- User running Honua must have write permissions
- Should be backed up regularly
- Recommended: Separate from repository path

**State Files Created:**
- `{environment}.json` - Current state for each environment
- Example: `production.json`, `staging.json`, `development.json`

**State File Contents:**
- Current deployment information
- Last successful deployment
- Deployment history (last 50 deployments)
- Environment health and sync status

**Production Example:**
```json
{
  "GitOps": {
    "StateDirectory": "/var/honua/deployments"
  }
}
```

**Backup Strategy:**
```bash
# Daily backup cron job
0 2 * * * tar -czf /var/honua/backups/deployments-$(date +\%Y\%m\%d).tar.gz /var/honua/deployments/
```

#### Credentials

**Type:** `object`
**Required:** No (if using SSH keys)

Git authentication credentials. **Strongly recommend using SSH keys instead.**

**Sub-Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Username` | string | `""` | Git username (for HTTPS) |
| `Password` | string | `""` | Git password or Personal Access Token |

**SSH Authentication (Recommended):**
```json
{
  "GitOps": {
    "Credentials": {
      "Username": "",
      "Password": ""
    }
  }
}
```

LibGit2Sharp will automatically use SSH keys from `~/.ssh/` for the user running Honua.

**HTTPS Authentication:**
```json
{
  "GitOps": {
    "Credentials": {
      "Username": "git",
      "Password": "ghp_YourPersonalAccessToken"
    }
  }
}
```

**Using Environment Variables (Recommended for HTTPS):**
```bash
# In systemd service file or .env
export GitOps__Credentials__Username="git"
export GitOps__Credentials__Password="${GITHUB_TOKEN}"
```

**Security Warning:** Storing credentials in `appsettings.json` is NOT recommended for production. Use:
1. SSH keys (best)
2. Environment variables
3. Secrets manager (Vault, AWS Secrets Manager, Azure Key Vault)

#### Watcher

**Type:** `object`
**Required:** Yes

Configuration for the GitWatcher background service.

**Sub-Properties:**

| Property | Type | Required | Default | Description |
|----------|------|----------|---------|-------------|
| `Branch` | string | Yes | `"main"` | Git branch to watch |
| `Environment` | string | Yes | N/A | Environment name (must match directory in `environments/{name}`) |
| `PollIntervalSeconds` | integer | No | `30` | How often to poll Git for changes (in seconds) |

**Branch:**

```json
{
  "GitOps": {
    "Watcher": {
      "Branch": "main"
    }
  }
}
```

**Common Strategies:**
- Single branch for all environments: `"main"`
- Branch per environment: `"production"`, `"staging"`, `"development"`
- Release branches: `"release/v1.0"`, `"release/v2.0"`

**Environment:**

```json
{
  "GitOps": {
    "Watcher": {
      "Environment": "production"
    }
  }
}
```

Must match a directory in your Git repository:
```
environments/
  production/      <- Matches "production"
  staging/         <- Matches "staging"
  development/     <- Matches "development"
```

**PollIntervalSeconds:**

```json
{
  "GitOps": {
    "Watcher": {
      "PollIntervalSeconds": 30
    }
  }
}
```

**Recommendations by Environment:**

| Environment | Recommended Interval | Rationale |
|-------------|---------------------|-----------|
| Production | 60 seconds | Balance between responsiveness and load |
| Staging | 30 seconds | Faster feedback for testing |
| Development | 15 seconds | Rapid iteration |

**Performance Considerations:**
- Lower interval = more frequent Git pulls = higher network/CPU usage
- Higher interval = longer time to detect changes = slower deployments
- Minimum recommended: 10 seconds
- Maximum recommended: 300 seconds (5 minutes)

#### DeploymentPolicy

**Type:** `object`
**Required:** No

Controls deployment approvals, scheduling windows, and rollback behavior.

**Complete Schema:**

```json
{
  "GitOps": {
    "DeploymentPolicy": {
      "RequiresApproval": true,
      "ApprovalTimeout": "24:00:00",
      "AllowedDays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"],
      "AllowedHours": {
        "Start": "09:00",
        "End": "17:00"
      },
      "AutoRollback": true,
      "MinimumRiskLevelForApproval": "Medium"
    }
  }
}
```

**Sub-Properties:**

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `RequiresApproval` | boolean | `false` | Whether deployments require manual approval |
| `ApprovalTimeout` | TimeSpan | `"24:00:00"` | How long to wait for approval before timing out |
| `AllowedDays` | string[] | `[]` (all days) | Days of week when deployments are allowed |
| `AllowedHours` | object | `null` (all hours) | Time window when deployments are allowed |
| `AutoRollback` | boolean | `true` | Automatically rollback failed deployments |
| `MinimumRiskLevelForApproval` | string | `null` | Risk level that triggers approval requirement |

**RequiresApproval:**

```json
{
  "DeploymentPolicy": {
    "RequiresApproval": true
  }
}
```

When `true`:
- Deployments pause in `AwaitingApproval` state
- Operator must manually approve via CLI or API
- Deployment proceeds only after approval
- Deployment times out if not approved within `ApprovalTimeout`

**Recommended Settings:**
- Production: `true`
- Staging: `false` (or `true` for high-risk changes)
- Development: `false`

**ApprovalTimeout:**

```json
{
  "DeploymentPolicy": {
    "ApprovalTimeout": "24:00:00"  // 24 hours
  }
}
```

Format: `"HH:MM:SS"` or `"D.HH:MM:SS"`

Examples:
- `"01:00:00"` - 1 hour
- `"04:00:00"` - 4 hours
- `"24:00:00"` - 24 hours
- `"2.00:00:00"` - 2 days

**AllowedDays:**

```json
{
  "DeploymentPolicy": {
    "AllowedDays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday"]
  }
}
```

Valid values: `"Monday"`, `"Tuesday"`, `"Wednesday"`, `"Thursday"`, `"Friday"`, `"Saturday"`, `"Sunday"`

**Use Cases:**
- Prevent weekend deployments: Monday-Friday only
- Maintenance windows: Saturday-Sunday only
- No restrictions: Empty array `[]`

**AllowedHours:**

```json
{
  "DeploymentPolicy": {
    "AllowedHours": {
      "Start": "09:00",
      "End": "17:00"
    }
  }
}
```

24-hour format: `"HH:MM"`

**Examples:**

Business hours only:
```json
{
  "AllowedHours": {
    "Start": "09:00",
    "End": "17:00"
  }
}
```

Night maintenance window:
```json
{
  "AllowedHours": {
    "Start": "22:00",
    "End": "06:00"  // Spans midnight
  }
}
```

**AutoRollback:**

```json
{
  "DeploymentPolicy": {
    "AutoRollback": true
  }
}
```

When `true`:
- Failed deployments automatically revert to last successful state
- Rollback is immediate (no approval required)
- Original error is preserved in deployment history

When `false`:
- Failed deployments remain in `Failed` state
- Manual intervention required to recover
- Useful for debugging deployment issues

**Recommended:**
- Production: `true` (minimize downtime)
- Staging: `true`
- Development: `false` (useful for debugging)

**MinimumRiskLevelForApproval:**

```json
{
  "DeploymentPolicy": {
    "MinimumRiskLevelForApproval": "Medium"
  }
}
```

Valid values: `"Low"`, `"Medium"`, `"High"`, `"Critical"`, `null`

**Behavior:**
- Deployments with risk level >= this threshold require approval
- Overrides `RequiresApproval` setting for high-risk changes
- Risk level is calculated based on:
  - Number of modified resources
  - Presence of database migrations
  - Breaking changes detected
  - Manual risk annotation in deployment plan

**Example Configurations:**

Production (strict):
```json
{
  "DeploymentPolicy": {
    "RequiresApproval": true,
    "ApprovalTimeout": "24:00:00",
    "AllowedDays": ["Monday", "Tuesday", "Wednesday", "Thursday"],
    "AllowedHours": {
      "Start": "10:00",
      "End": "16:00"
    },
    "AutoRollback": true,
    "MinimumRiskLevelForApproval": "Medium"
  }
}
```

Staging (moderate):
```json
{
  "DeploymentPolicy": {
    "RequiresApproval": false,
    "AutoRollback": true,
    "MinimumRiskLevelForApproval": "High"
  }
}
```

Development (permissive):
```json
{
  "DeploymentPolicy": {
    "RequiresApproval": false,
    "AutoRollback": false
  }
}
```

#### DryRun

**Type:** `boolean`
**Required:** No
**Default:** `false`

```json
{
  "GitOps": {
    "DryRun": true
  }
}
```

When `true`:
- GitWatcher polls and detects changes normally
- Reconciliation simulates all operations
- No actual changes are applied to the system
- Useful for testing GitOps setup without affecting running services

**Use Cases:**
- Initial GitOps setup verification
- Testing new configuration changes
- Validating deployment policies
- Training and demonstrations

**Production:** Should always be `false`

#### EnableMetrics

**Type:** `boolean`
**Required:** No
**Default:** `true`

```json
{
  "GitOps": {
    "EnableMetrics": true
  }
}
```

Enables OpenTelemetry metrics collection for GitOps operations.

**Metrics Collected:**
- `honua.gitops.reconciliations.total` - Total reconciliation attempts
- `honua.gitops.reconciliations.success` - Successful reconciliations
- `honua.gitops.reconciliations.failure` - Failed reconciliations
- `honua.gitops.reconciliation.duration` - Reconciliation duration histogram

**Production:** Should be `true` for monitoring

#### EnableTracing

**Type:** `boolean`
**Required:** No
**Default:** `true`

```json
{
  "GitOps": {
    "EnableTracing": true
  }
}
```

Enables distributed tracing for GitOps operations using OpenTelemetry.

**Traces Include:**
- GitWatcher polling cycles
- Reconciliation operations
- Configuration file processing
- Deployment state updates

**Production:** Should be `true` for observability

---

## Environment-Specific Configuration

### Multi-Environment Setup

**Single Server, Multiple Environments:**

Not recommended. Each environment should have its own server instance.

**Multiple Servers, Different Environments:**

**Server 1 (Production):**
```json
{
  "GitOps": {
    "RepositoryPath": "/var/honua/gitops-repo",
    "StateDirectory": "/var/honua/deployments",
    "Watcher": {
      "Branch": "main",
      "Environment": "production"
    }
  }
}
```

**Server 2 (Staging):**
```json
{
  "GitOps": {
    "RepositoryPath": "/var/honua/gitops-repo",
    "StateDirectory": "/var/honua/deployments",
    "Watcher": {
      "Branch": "main",
      "Environment": "staging"
    }
  }
}
```

**Server 3 (Development):**
```json
{
  "GitOps": {
    "RepositoryPath": "/var/honua/gitops-repo",
    "StateDirectory": "/var/honua/deployments",
    "Watcher": {
      "Branch": "main",
      "Environment": "development"
    }
  }
}
```

All servers watch the same Git repository but different environment directories.

### Branch-Based Environments

**Alternative Strategy:** Different branches for different environments.

**Production Server:**
```json
{
  "GitOps": {
    "Watcher": {
      "Branch": "production",
      "Environment": "production"
    }
  }
}
```

**Staging Server:**
```json
{
  "GitOps": {
    "Watcher": {
      "Branch": "staging",
      "Environment": "staging"
    }
  }
}
```

**Git Workflow:**
```bash
# Develop in feature branch
git checkout -b feature/new-layer

# Merge to staging
git checkout staging
git merge feature/new-layer
git push origin staging
# Staging server deploys automatically

# After testing, promote to production
git checkout production
git merge staging
git push origin production
# Production server deploys automatically
```

---

## Deployment Policies

### Default Policies by Environment

The `FileApprovalService` provides default policies when not explicitly configured:

**Production:**
```csharp
RequiresApproval = true
ApprovalTimeout = TimeSpan.FromHours(24)
AutoRollback = true
MinimumRiskLevelForApproval = RiskLevel.Medium
```

**Staging:**
```csharp
RequiresApproval = false
ApprovalTimeout = TimeSpan.FromHours(4)
AutoRollback = true
MinimumRiskLevelForApproval = RiskLevel.High
```

**Development:**
```csharp
RequiresApproval = false
ApprovalTimeout = TimeSpan.FromHours(1)
AutoRollback = false
MinimumRiskLevelForApproval = RiskLevel.Critical
```

### Override Default Policies

**In appsettings.json:**

```json
{
  "GitOps": {
    "DeploymentPolicy": {
      "RequiresApproval": true,
      "ApprovalTimeout": "12:00:00",
      "AllowedDays": ["Monday", "Tuesday", "Wednesday"],
      "AllowedHours": {
        "Start": "10:00",
        "End": "14:00"
      },
      "AutoRollback": true,
      "MinimumRiskLevelForApproval": "Low"
    }
  }
}
```

### Risk Level Calculations

Risk levels are automatically calculated based on deployment changes:

| Risk Level | Criteria |
|------------|----------|
| **Low** | Minor configuration changes, no migrations, no breaking changes |
| **Medium** | Multiple resource changes, minor schema updates |
| **High** | Database migrations, breaking changes, major refactoring |
| **Critical** | Data deletion, irreversible operations, security-critical changes |

**Approval Requirements:**

```
If (DeploymentPolicy.RequiresApproval == true)
  OR (Plan.RiskLevel >= DeploymentPolicy.MinimumRiskLevelForApproval)
  OR (Plan.HasBreakingChanges == true)
  OR (Plan.Migrations.Count > 0)
Then:
  Deployment requires approval
```

---

## Security Configuration

### Authentication Methods

**1. SSH Keys (Recommended)**

**Setup:**
```bash
# Generate key
ssh-keygen -t ed25519 -C "honua-gitops@example.com" -f ~/.ssh/honua_gitops

# Set permissions
chmod 600 ~/.ssh/honua_gitops
chmod 644 ~/.ssh/honua_gitops.pub

# Add to Git provider
cat ~/.ssh/honua_gitops.pub
```

**Configuration:**
```json
{
  "GitOps": {
    "Credentials": {
      "Username": "",
      "Password": ""
    }
  }
}
```

LibGit2Sharp automatically uses SSH keys.

**2. HTTPS with Personal Access Token**

**GitHub:**
```json
{
  "GitOps": {
    "Credentials": {
      "Username": "git",
      "Password": "${GITHUB_TOKEN}"
    }
  }
}
```

**Required Token Permissions:**
- `repo` (full control of private repositories)

**GitLab:**
```json
{
  "GitOps": {
    "Credentials": {
      "Username": "git",
      "Password": "${GITLAB_TOKEN}"
    }
  }
}
```

**Required Token Permissions:**
- `read_repository`

### Securing State Files

**File Permissions:**

```bash
# Set restrictive permissions
chmod 700 /var/honua/deployments
chmod 600 /var/honua/deployments/*.json

# Verify
ls -la /var/honua/deployments/
# drwx------ 2 honua honua 4096 Oct 23 10:00 .
# -rw------- 1 honua honua 2048 Oct 23 10:00 production.json
```

**Encryption at Rest:**

Consider using filesystem-level encryption for sensitive deployment state:

```bash
# LUKS encryption for state directory
cryptsetup luksFormat /dev/sdb1
cryptsetup open /dev/sdb1 honua-state
mkfs.ext4 /dev/mapper/honua-state
mount /dev/mapper/honua-state /var/honua/deployments
```

### Access Control

**Limit Server Access:**

```bash
# Only Honua service user can access
chown -R honua:honua /var/honua
chmod 700 /var/honua

# SELinux policies (RHEL/CentOS)
semanage fcontext -a -t honua_var_t "/var/honua(/.*)?"
restorecon -R /var/honua
```

**Audit Logging:**

```bash
# Audit all access to deployment state
auditctl -w /var/honua/deployments -p wa -k honua-deployments
```

---

## Performance Tuning

### Poll Interval Optimization

**Formula:**

```
Optimal Interval = (Average Reconciliation Duration) + (Buffer Time)
```

**Example:**
- Average reconciliation: 5 seconds
- Buffer time: 25 seconds
- Optimal interval: 30 seconds

**Monitoring:**

```bash
# Check reconciliation duration
journalctl -u honua | grep "completed reconciliation" | \
  grep -oP "in \K\d+" | \
  awk '{sum+=$1; count++} END {print "Average:", sum/count, "ms"}'
```

### Git Performance

**Shallow Clone (Future Enhancement):**

```bash
# Reduce repository size
git clone --depth 1 --single-branch --branch main \
  git@github.com:your-org/honua-config.git
```

**Git Garbage Collection:**

```bash
# Add to weekly cron
0 3 * * 0 git -C /var/honua/gitops-repo gc --aggressive
```

### State File Performance

**Limit History Size:**

Current limit: 50 deployments per environment

**Manual Cleanup:**

```bash
# Keep only last 10 deployments in history
jq '.history = .history[0:10]' /var/honua/deployments/production.json > /tmp/cleaned.json
mv /tmp/cleaned.json /var/honua/deployments/production.json
```

### Resource Limits

**systemd Resource Limits:**

```ini
# /etc/systemd/system/honua.service
[Service]
MemoryMax=2G
CPUQuota=100%
TasksMax=256
```

**Kubernetes Resource Limits:**

```yaml
resources:
  requests:
    memory: "512Mi"
    cpu: "250m"
  limits:
    memory: "2Gi"
    cpu: "1000m"
```

---

## Advanced Options

### Custom Logging Configuration

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Honua.Server.Core.GitOps.GitWatcher": "Debug",
        "Honua.Server.Core.GitOps.HonuaReconciler": "Information",
        "Honua.Server.Core.Deployment": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "File",
        "Args": {
          "path": "/var/log/honua/gitops-.log",
          "rollingInterval": "Day",
          "retainedFileCountLimit": 30
        }
      }
    ]
  }
}
```

### OpenTelemetry Configuration

```json
{
  "OpenTelemetry": {
    "ServiceName": "honua-gitops",
    "ServiceVersion": "1.0.0",
    "Exporters": {
      "Jaeger": {
        "Enabled": true,
        "Endpoint": "http://jaeger:14268/api/traces"
      },
      "Prometheus": {
        "Enabled": true,
        "Port": 9090
      }
    }
  }
}
```

### Environment Variable Override

Any configuration value can be overridden via environment variables using double-underscore notation:

```bash
# Override poll interval
export GitOps__Watcher__PollIntervalSeconds=60

# Override repository path
export GitOps__RepositoryPath="/custom/path/repo"

# Override deployment policy
export GitOps__DeploymentPolicy__RequiresApproval=true
```

---

## Configuration Examples

### Example 1: Production (Strict)

```json
{
  "GitOps": {
    "RepositoryPath": "/var/honua/gitops-repo",
    "StateDirectory": "/var/honua/deployments",
    "Credentials": {
      "Username": "",
      "Password": ""
    },
    "Watcher": {
      "Branch": "main",
      "Environment": "production",
      "PollIntervalSeconds": 60
    },
    "DeploymentPolicy": {
      "RequiresApproval": true,
      "ApprovalTimeout": "24:00:00",
      "AllowedDays": ["Monday", "Tuesday", "Wednesday", "Thursday"],
      "AllowedHours": {
        "Start": "10:00",
        "End": "16:00"
      },
      "AutoRollback": true,
      "MinimumRiskLevelForApproval": "Medium"
    },
    "DryRun": false,
    "EnableMetrics": true,
    "EnableTracing": true
  }
}
```

### Example 2: Staging (Moderate)

```json
{
  "GitOps": {
    "RepositoryPath": "/var/honua/gitops-repo",
    "StateDirectory": "/var/honua/deployments",
    "Watcher": {
      "Branch": "staging",
      "Environment": "staging",
      "PollIntervalSeconds": 30
    },
    "DeploymentPolicy": {
      "RequiresApproval": false,
      "AutoRollback": true,
      "MinimumRiskLevelForApproval": "High"
    },
    "DryRun": false,
    "EnableMetrics": true,
    "EnableTracing": true
  }
}
```

### Example 3: Development (Permissive)

```json
{
  "GitOps": {
    "RepositoryPath": "/home/developer/honua-config",
    "StateDirectory": "/tmp/honua-deployments",
    "Watcher": {
      "Branch": "development",
      "Environment": "development",
      "PollIntervalSeconds": 15
    },
    "DeploymentPolicy": {
      "RequiresApproval": false,
      "AutoRollback": false
    },
    "DryRun": false,
    "EnableMetrics": true,
    "EnableTracing": false
  }
}
```

---

## Validation

### Configuration Validation

The GitOps system performs validation on startup:

**Required Fields:**
- `GitOps.RepositoryPath` must be set and valid
- `GitOps.StateDirectory` must be set and writable
- `GitOps.Watcher.Environment` must be set
- Repository path must be a valid Git repository

**Validation Errors:**

```
GitOps:RepositoryPath configuration is required when GitOps is enabled.
GitOps:StateDirectory configuration is required when GitOps is enabled.
GitOps repository path does not exist: /invalid/path
GitOps repository path is not a valid Git repository: /var/honua/gitops-repo
```

### Runtime Validation

```bash
# Validate configuration syntax
cat /etc/honua/appsettings.Production.json | jq .

# Validate GitOps section
cat /etc/honua/appsettings.Production.json | jq '.GitOps'

# Test repository access
sudo -u honua git -C /var/honua/gitops-repo pull
```

---

## Summary

Key configuration decisions:

1. **Repository Path**: Use dedicated directory on persistent storage
2. **Authentication**: Prefer SSH keys over HTTPS tokens
3. **Poll Interval**: Balance responsiveness vs. resource usage (30-60s recommended)
4. **Deployment Policies**: Strict for production, permissive for development
5. **Auto-Rollback**: Enable for production to minimize downtime
6. **Metrics/Tracing**: Always enable for observability

For operational procedures, see:
- [GitOps Deployment Runbook](./gitops-deployment-runbook.md)
- [GitOps Security Guide](./gitops-security-guide.md)
- [GitOps Best Practices](./gitops-best-practices.md)

---

**Last Updated:** 2025-10-23
**Version:** 1.0
