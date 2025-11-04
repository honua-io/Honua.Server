# GitOps Deployment Runbook

**Version:** 1.0
**Last Updated:** 2025-10-23
**Audience:** Operations Teams, SRE, DevOps Engineers

This runbook provides step-by-step procedures for deploying and operating the Honua GitOps system in production environments.

---

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Initial Setup](#initial-setup)
3. [Server Configuration](#server-configuration)
4. [Deployment Steps](#deployment-steps)
5. [First Deployment](#first-deployment)
6. [Day-to-Day Operations](#day-to-day-operations)
7. [Troubleshooting](#troubleshooting)
8. [Monitoring and Alerting](#monitoring-and-alerting)

---

## Prerequisites

### System Requirements

**Minimum:**
- CPU: 2 cores
- Memory: 4 GB RAM
- Disk: 20 GB available space (for Git repository and state storage)
- OS: Linux (Ubuntu 20.04+, RHEL 8+, or equivalent)

**Recommended (Production):**
- CPU: 4+ cores
- Memory: 8 GB RAM
- Disk: 50 GB available space (SSD preferred)
- OS: Linux with systemd

### Software Dependencies

| Software | Version | Purpose |
|----------|---------|---------|
| .NET Runtime | 9.0+ | Run Honua server |
| Git | 2.0+ | Repository operations |
| LibGit2Sharp | 0.30.0+ | Included in Honua.Server.Core |

### Access Requirements

**Git Repository Access:**
- Read access to the configuration Git repository
- SSH key or HTTPS credentials (Personal Access Token)
- Network connectivity to Git provider (GitHub, GitLab, Bitbucket, etc.)

**File System Permissions:**
- Write access to repository clone directory (e.g., `/var/honua/gitops-repo`)
- Write access to state directory (e.g., `/var/honua/deployments`)
- Write access to approval directory (e.g., `/var/honua/approvals`)

**Network Requirements:**
- Outbound HTTPS (443) or SSH (22) to Git provider
- Internal network access to databases/data sources
- (Optional) Webhook receiver port if using push-based triggers

### Required Knowledge

Operations team members should be familiar with:
- Basic Git operations (clone, pull, commit, push)
- JSON/YAML configuration file formats
- Linux file permissions and systemd services
- Basic troubleshooting and log analysis

---

## Initial Setup

### Step 1: Create Configuration Git Repository

**1.1 Create Repository Structure**

Create a new Git repository with the following structure:

```
honua-config/
├── environments/
│   ├── development/
│   │   ├── metadata.json       # Service/layer definitions
│   │   ├── datasources.json    # Database connections
│   │   └── appsettings.json    # (Optional) App settings
│   ├── staging/
│   │   ├── metadata.json
│   │   ├── datasources.json
│   │   └── appsettings.json
│   ├── production/
│   │   ├── metadata.json
│   │   ├── datasources.json
│   │   └── appsettings.json
│   └── common/
│       └── shared-config.json  # (Optional) Shared settings
├── .gitops/
│   └── deployment-policy.yaml  # (Future) Deployment policies
└── README.md
```

**1.2 Initialize Repository**

```bash
# Create repository directory
mkdir honua-config
cd honua-config

# Initialize Git
git init

# Create environment directories
mkdir -p environments/{development,staging,production,common}
mkdir -p .gitops

# Create README
cat > README.md <<'EOF'
# Honua Configuration Repository

This repository contains declarative configuration for Honua GIS services.

## Structure
- `environments/development/` - Development environment config
- `environments/staging/` - Staging environment config
- `environments/production/` - Production environment config
- `environments/common/` - Shared configuration across environments

## Making Changes
1. Create a feature branch
2. Make configuration changes
3. Test in development environment
4. Submit pull request for review
5. Merge to main branch
6. GitOps will automatically deploy changes
EOF

# Initial commit
git add .
git commit -m "Initial repository structure"

# Add remote and push (replace with your Git provider URL)
git remote add origin git@github.com:your-org/honua-config.git
git push -u origin main
```

**1.3 Branch Strategy**

**Recommended Strategy:**

- `main` branch - Production deployments
- `staging` branch - Staging deployments (optional)
- `feature/*` branches - Development changes
- Tags for releases (e.g., `v1.0.0`, `v1.1.0`)

**Branch Protection Rules (GitHub example):**

```yaml
# Configure in GitHub repository settings
main:
  require_pull_request_review: true
  required_approving_review_count: 2
  dismiss_stale_reviews: true
  require_code_owner_reviews: true
  require_status_checks: true
```

### Step 2: Set Up Access Control

**2.1 Create SSH Key for GitOps**

```bash
# Generate dedicated SSH key for Honua GitOps
ssh-keygen -t ed25519 -C "honua-gitops@example.com" -f ~/.ssh/honua_gitops -N ""

# Display public key to add to Git provider
cat ~/.ssh/honua_gitops.pub
```

**2.2 Add Deploy Key to Git Provider**

**GitHub:**
1. Navigate to repository Settings > Deploy keys
2. Click "Add deploy key"
3. Title: "Honua GitOps Production"
4. Paste public key content
5. Leave "Allow write access" UNCHECKED (read-only)
6. Click "Add key"

**GitLab:**
1. Navigate to Settings > Repository > Deploy Keys
2. Click "Add key"
3. Title: "Honua GitOps Production"
4. Paste public key content
5. Leave "Grant write permissions" UNCHECKED
6. Click "Add key"

**2.3 Set File Permissions**

```bash
# Set restrictive permissions on private key
chmod 600 ~/.ssh/honua_gitops

# Verify ownership
ls -la ~/.ssh/honua_gitops
# Should show: -rw------- 1 honua honua
```

---

## Server Configuration

### Step 1: Configure appsettings.json

Create or modify `/etc/honua/appsettings.Production.json`:

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
    "DryRun": false
  },
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Honua.Server.Core.GitOps": "Information",
        "Honua.Server.Core.Deployment": "Information"
      }
    }
  }
}
```

**Configuration Options Explained:**

| Option | Description | Default | Production Recommendation |
|--------|-------------|---------|---------------------------|
| `RepositoryPath` | Local path where Git repository will be cloned | Required | `/var/honua/gitops-repo` |
| `StateDirectory` | Directory for deployment state files | Required | `/var/honua/deployments` |
| `Credentials.Username` | Git username (for HTTPS auth) | `""` | Use SSH instead |
| `Credentials.Password` | Git password/token (for HTTPS auth) | `""` | Use SSH instead |
| `Watcher.Branch` | Git branch to watch | `"main"` | `"main"` (production) |
| `Watcher.Environment` | Environment name | Required | `"production"` |
| `Watcher.PollIntervalSeconds` | How often to check for changes | `30` | `30-60` seconds |
| `DryRun` | Simulate changes without applying | `false` | `false` |

**IMPORTANT:** For SSH authentication, LibGit2Sharp will automatically use SSH keys from the user's `.ssh` directory. Ensure the Honua server runs as a user with access to the SSH key.

### Step 2: Set Up Directories

```bash
# Create required directories
sudo mkdir -p /var/honua/gitops-repo
sudo mkdir -p /var/honua/deployments
sudo mkdir -p /var/honua/approvals

# Set ownership to Honua service user
sudo chown -R honua:honua /var/honua

# Set permissions
sudo chmod 755 /var/honua
sudo chmod 700 /var/honua/gitops-repo
sudo chmod 700 /var/honua/deployments
sudo chmod 700 /var/honua/approvals

# Verify
ls -la /var/honua
```

**Expected output:**
```
drwxr-xr-x 5 honua honua 4096 Oct 23 10:00 .
drwxr-xr-x 14 root  root  4096 Oct 23 09:00 ..
drwx------ 2 honua honua 4096 Oct 23 10:00 approvals
drwx------ 2 honua honua 4096 Oct 23 10:00 deployments
drwx------ 8 honua honua 4096 Oct 23 10:00 gitops-repo
```

### Step 3: Clone Repository Manually (First Time)

**IMPORTANT:** Before starting the GitOps service, manually clone the repository to verify access:

```bash
# Switch to Honua service user
sudo -u honua -s

# Clone repository
cd /var/honua
git clone git@github.com:your-org/honua-config.git gitops-repo

# Verify clone
cd gitops-repo
git status
git log --oneline -5

# Exit back to admin user
exit
```

**Troubleshooting Clone Issues:**

If clone fails with "Permission denied":
```bash
# Test SSH connection
sudo -u honua ssh -T git@github.com

# Should see: "Hi username! You've successfully authenticated..."
```

If clone fails with "Repository not found":
- Verify repository URL is correct
- Ensure deploy key is added to repository
- Check repository is accessible

### Step 4: Environment Variables (Optional)

For sensitive credentials, use environment variables instead of config files:

```bash
# /etc/systemd/system/honua.service
[Service]
Environment="GitOps__Credentials__Username=git"
Environment="GitOps__Credentials__Password=ghp_yourpersonalaccesstoken"
Environment="DB_CONNECTION_STRING=Host=prod-db;Database=gis;Username=honua;Password=secretpass"
```

**SECURITY WARNING:** Storing credentials in systemd service files is better than appsettings.json, but consider using a secrets manager (e.g., HashiCorp Vault, AWS Secrets Manager) for production.

---

## Deployment Steps

### Step 1: Build and Deploy Honua Server

**1.1 Build Release Package**

```bash
# On build server or CI/CD pipeline
cd /path/to/HonuaIO
dotnet publish src/Honua.Server.Host/Honua.Server.Host.csproj \
  -c Release \
  -o /tmp/honua-release \
  --self-contained false \
  -p:PublishSingleFile=false
```

**1.2 Transfer to Production Server**

```bash
# Create release tarball
tar -czf honua-release.tar.gz -C /tmp/honua-release .

# Transfer to production
scp honua-release.tar.gz honua@prod-server:/tmp/

# On production server
cd /opt/honua
sudo tar -xzf /tmp/honua-release.tar.gz
sudo chown -R honua:honua /opt/honua
```

### Step 2: Verify GitOps Services Registration

**2.1 Check Service Registration**

The GitOps services are registered via the `AddGitOps()` extension method. Verify this is called in your startup:

```bash
# Check if GitOps is configured
grep -r "AddGitOps" /opt/honua/

# Expected: Should find reference in ServiceCollectionExtensions or similar
```

**2.2 Validate Configuration**

```bash
# Validate appsettings.json syntax
cat /etc/honua/appsettings.Production.json | jq .

# Check GitOps section exists
cat /etc/honua/appsettings.Production.json | jq '.GitOps'
```

### Step 3: Start the Server

**3.1 Start via systemd**

```bash
# Start Honua service
sudo systemctl start honua

# Check status
sudo systemctl status honua

# Follow logs
sudo journalctl -u honua -f
```

**3.2 Verify GitWatcher Started**

Look for these log messages:

```
[Information] GitWatcher started. Watching branch 'main' every 30 seconds
[Information] Initial commit: abc123def456...
[Information] GitWatcher: Polling for changes on branch 'main'
```

If you don't see these messages, check [Troubleshooting](#troubleshooting) section.

### Step 4: Validate Polling is Working

**4.1 Monitor First Poll Cycle**

Wait 30 seconds (or configured poll interval) and verify polling logs:

```bash
# Watch for polling activity
sudo journalctl -u honua -f | grep -i "poll\|gitwatcher"
```

**Expected logs:**
```
[Debug] Polling for changes on branch 'main'
[Debug] No new commits detected
```

**4.2 Test Change Detection**

Make a trivial change to test GitWatcher detection:

```bash
# On your workstation
cd honua-config
echo "# Last updated: $(date)" >> environments/production/README.md
git add environments/production/README.md
git commit -m "Test: Trigger GitWatcher detection"
git push origin main
```

Wait for next poll cycle (30-60 seconds) and watch logs:

```bash
sudo journalctl -u honua -f | grep -i "detect\|new commit"
```

**Expected logs:**
```
[Information] Detected new commit: abc123 -> def456
[Information] Commit by: John Doe
[Information] Message: Test: Trigger GitWatcher detection
[Information] Changed files: 1
```

---

## First Deployment

### Step 1: Create Initial Configuration

**1.1 Create metadata.json**

In your Git repository, create `environments/production/metadata.json`:

```json
{
  "services": [
    {
      "id": "gis-service",
      "title": "Production GIS Service",
      "description": "Production geospatial data services",
      "layers": [
        {
          "id": "cities",
          "title": "World Cities",
          "description": "Major cities worldwide",
          "datasource": "postgres-prod",
          "table": "public.cities",
          "geometryColumn": "geom",
          "geometryType": "Point",
          "srid": 4326,
          "properties": [
            { "name": "name", "type": "string" },
            { "name": "population", "type": "integer" },
            { "name": "country", "type": "string" }
          ]
        }
      ]
    }
  ]
}
```

**1.2 Create datasources.json**

Create `environments/production/datasources.json`:

```json
{
  "datasources": [
    {
      "id": "postgres-prod",
      "type": "PostgreSQL",
      "connectionString": "${DB_CONNECTION_STRING}",
      "description": "Production PostgreSQL database"
    }
  ]
}
```

**1.3 Commit and Push**

```bash
cd honua-config
git add environments/production/
git commit -m "feat: Add initial production configuration"
git push origin main
```

### Step 2: Monitor First Reconciliation

**2.1 Watch Logs in Real-Time**

```bash
# Follow GitOps logs
sudo journalctl -u honua -f | grep -E "GitWatcher|Reconcil"
```

**2.2 Expected Log Sequence**

```
[Information] GitWatcher detected new commit: abc123 -> def456
[Information] Commit by: Operations Team
[Information] Message: feat: Add initial production configuration
[Information] Changed files: 2
[Information] Found 2 relevant files for environment 'production'
[Information] Starting reconciliation for environment 'production' at commit 'def456'
[Information] Reconciling environment-specific configuration from 'environments/production'
[Information] Retrieving 'metadata' configuration from Git: environments/production/metadata.json
[Debug] Successfully parsed JSON for 'metadata'
[Information] Applying 'metadata' configuration for environment 'production'
[Information] Reloading metadata registry from updated configuration
[Information] Successfully reloaded metadata registry with 1 services
[Information] Successfully reconciled 'metadata' for environment 'production'
[Information] Retrieving 'datasources' configuration from Git: environments/production/datasources.json
[Information] Successfully completed reconciliation for environment 'production' in 1234ms
```

**IMPORTANT:** If reconciliation fails, see [Troubleshooting](#troubleshooting) section.

### Step 3: Verify Metadata Reload

**3.1 Check Metadata Registry**

```bash
# Query OGC API Collections endpoint
curl -s https://your-honua-server.com/ogc/collections | jq '.collections[].id'

# Should include: "cities"
```

**3.2 Test Layer Access**

```bash
# Get features from the new layer
curl -s "https://your-honua-server.com/ogc/collections/cities/items?limit=10" | jq .
```

### Step 4: Verify Deployment State

**4.1 Check Deployment State File**

```bash
# View deployment state
sudo cat /var/honua/deployments/production.json | jq .

# Key fields to check:
# - currentDeployment.state should be "Completed"
# - currentDeployment.health should be "Healthy"
# - deployedCommit should match your Git commit SHA
```

**Expected state file:**
```json
{
  "environment": "production",
  "currentDeployment": {
    "id": "production-20251023-143022",
    "environment": "production",
    "commit": "def456abc123...",
    "branch": "main",
    "state": "Completed",
    "health": "Healthy",
    "syncStatus": "Synced",
    "startedAt": "2025-10-23T14:30:22Z",
    "completedAt": "2025-10-23T14:30:24Z",
    "initiatedBy": "GitWatcher"
  },
  "lastSuccessfulDeployment": { ... },
  "deployedCommit": "def456abc123...",
  "health": "Healthy",
  "syncStatus": "Synced"
}
```

---

## Day-to-Day Operations

### Making Configuration Changes

**Standard Change Process:**

1. **Create Feature Branch**
   ```bash
   cd honua-config
   git checkout -b feature/add-roads-layer
   ```

2. **Make Configuration Changes**
   ```bash
   # Edit metadata.json to add new layer
   vim environments/production/metadata.json
   ```

3. **Test in Development First**
   ```bash
   # Apply changes to development environment first
   git checkout main
   git merge feature/add-roads-layer
   git push origin main  # This updates development (if using branch-based environments)
   ```

4. **Create Pull Request**
   - Submit PR with detailed description
   - Include change impact analysis
   - Request review from team

5. **Review and Approve**
   - At least 2 approvers (production changes)
   - Review deployment plan
   - Verify no breaking changes

6. **Merge to Main Branch**
   ```bash
   # After PR approval
   git checkout main
   git pull origin main
   # GitWatcher will detect within 30 seconds
   ```

### Reviewing Deployment Plans

**FUTURE FEATURE:** When deployment plan generation is implemented:

```bash
# View pending deployment plan
honua deployment plan --environment production

# Example output:
# Deployment Plan for production
# Commit: def456 -> abc789
# Risk Level: Medium
#
# Added:
#   - Layer: roads (environments/production/metadata.json)
#
# Modified:
#   - Layer: cities (updated properties)
#
# Migrations:
#   - None
#
# Estimated Duration: 15 seconds
```

### Approving Deployments

**For environments requiring approval:**

**Manual Approval (Current Implementation):**

1. **Check Approval Status**
   ```bash
   sudo cat /var/honua/approvals/production-20251023-143022.json | jq .
   ```

2. **Approve via File**
   ```bash
   # Create approval file
   cat > /tmp/approval.json <<'EOF'
   {
     "deploymentId": "production-20251023-143022",
     "environment": "production",
     "state": "Approved",
     "responder": "ops-team@example.com",
     "respondedAt": "2025-10-23T14:35:00Z"
   }
   EOF

   sudo mv /tmp/approval.json /var/honua/approvals/production-20251023-143022.json
   ```

**FUTURE:** CLI-based approval:
```bash
honua deployment approve production-20251023-143022 \
  --approver "ops-team@example.com"
```

### Monitoring Deployment Status

**Real-Time Monitoring:**

```bash
# Watch deployment logs
sudo journalctl -u honua -f | grep -E "Deploy|Reconcil"

# Check current deployment state
watch -n 5 'cat /var/honua/deployments/production.json | jq ".currentDeployment | {state, health, commit: .commit[0:7]}"'
```

**Deployment States:**

| State | Description | Next Steps |
|-------|-------------|------------|
| `Pending` | Deployment created, waiting to start | Automatically transitions to `Applying` |
| `Applying` | Reconciliation in progress | Monitor logs for progress |
| `AwaitingApproval` | Waiting for manual approval | Approve or reject deployment |
| `Completed` | Successfully deployed | No action needed |
| `Failed` | Deployment failed | Check logs, may auto-rollback |
| `RolledBack` | Rolled back to previous state | Investigate failure reason |

### Handling Failures

**When Deployment Fails:**

1. **Check Error Logs**
   ```bash
   sudo journalctl -u honua --since "5 minutes ago" | grep -i error
   ```

2. **Review Deployment State**
   ```bash
   sudo cat /var/honua/deployments/production.json | jq '.currentDeployment | {state, errorMessage, stateHistory}'
   ```

3. **Common Failure Reasons:**
   - Invalid JSON syntax in configuration
   - Database connection failure
   - Missing datasource reference
   - Git repository access issues

4. **Rollback (if auto-rollback enabled):**
   - System automatically rolls back to last successful deployment
   - Check logs for rollback confirmation:
     ```
     [Warning] Deployment failed, initiating automatic rollback
     [Information] Rolling back to deployment: production-20251023-120000
     [Information] Rollback completed successfully
     ```

5. **Manual Intervention:**
   ```bash
   # If auto-rollback is disabled or failed
   # Revert Git commit
   cd /var/honua/gitops-repo
   git revert HEAD
   git push origin main

   # GitWatcher will detect and deploy the revert
   ```

### Emergency Procedures

**Emergency Rollback:**

```bash
# Option 1: Git revert (recommended)
cd honua-config
git revert HEAD
git push origin main

# Option 2: Force push previous commit (use with caution)
git reset --hard HEAD~1
git push --force origin main
```

**CRITICAL WARNING:** Force pushing can cause issues if multiple GitWatcher instances are running. Always prefer `git revert`.

**Pause GitOps (Emergency Stop):**

```bash
# Temporarily stop GitWatcher
sudo systemctl stop honua

# Make manual changes directly on server (NOT RECOMMENDED)
# This defeats the purpose of GitOps - only for emergencies

# Restart when ready
sudo systemctl start honua
```

---

## Troubleshooting

### Problem: GitWatcher Not Detecting Changes

**Symptoms:**
- Git commits not triggering reconciliation
- No polling logs in journalctl
- GitWatcher appears to be running but inactive

**Diagnosis:**

1. **Check GitWatcher is running:**
   ```bash
   sudo journalctl -u honua | grep -i "GitWatcher started"
   ```

2. **Verify polling interval:**
   ```bash
   cat /etc/honua/appsettings.Production.json | jq '.GitOps.Watcher.PollIntervalSeconds'
   ```

3. **Check for Git errors:**
   ```bash
   sudo journalctl -u honua | grep -i "git\|pull\|fetch" | tail -20
   ```

**Solutions:**

**Issue: SSH key not accessible**
```bash
# Verify SSH key exists and has correct permissions
sudo -u honua ls -la ~/.ssh/honua_gitops
# Should be: -rw------- 1 honua honua

# Test Git access
sudo -u honua git -C /var/honua/gitops-repo pull
```

**Issue: Wrong branch configured**
```bash
# Check configured branch
cat /etc/honua/appsettings.Production.json | jq '.GitOps.Watcher.Branch'

# Check actual branch
git -C /var/honua/gitops-repo branch -a
```

**Issue: Network connectivity**
```bash
# Test network access to Git provider
sudo -u honua ssh -T git@github.com

# Check DNS resolution
nslookup github.com
```

### Problem: Deployments Stuck in AwaitingApproval

**Symptoms:**
- Deployment state is `AwaitingApproval`
- No progress after configured timeout
- Approval timeout has not expired

**Diagnosis:**

```bash
# Check approval status
sudo cat /var/honua/approvals/production-*.json | jq .

# Check deployment policy
cat /etc/honua/appsettings.Production.json | jq '.GitOps.DeploymentPolicy'
```

**Solutions:**

**Manual Approval:**
```bash
# Find deployment ID
DEPLOYMENT_ID=$(sudo cat /var/honua/deployments/production.json | jq -r '.currentDeployment.id')

# Approve
sudo -u honua honua deployment approve $DEPLOYMENT_ID --approver "$(whoami)"
```

**Adjust Approval Policy (if appropriate):**
```json
{
  "GitOps": {
    "DeploymentPolicy": {
      "RequiresApproval": false,  // Disable approval for this environment
      "AutoRollback": true
    }
  }
}
```

### Problem: Reconciliation Failing

**Symptoms:**
- Deployment state transitions to `Failed`
- Error messages in logs
- Metadata not updating

**Common Errors and Fixes:**

**Error: "Invalid JSON in configuration file"**

**Diagnosis:**
```bash
# Check JSON syntax
cat environments/production/metadata.json | jq .
```

**Solution:**
```bash
# Fix JSON syntax errors
vim environments/production/metadata.json

# Validate before committing
jq . environments/production/metadata.json

# Commit fix
git add environments/production/metadata.json
git commit -m "fix: Correct JSON syntax in metadata.json"
git push origin main
```

**Error: "Failed to reload metadata registry"**

**Diagnosis:**
```bash
sudo journalctl -u honua | grep -i "metadata\|registry" | tail -50
```

**Possible Causes:**
- MetadataRegistry not properly wired in DI
- Configuration file not in expected format
- Database connection failure

**Solution:**
```bash
# Verify MetadataRegistry is registered
sudo systemctl restart honua

# Check startup logs
sudo journalctl -u honua -n 100 | grep -i "metadata"
```

**Error: "Database connection failure"**

**Diagnosis:**
```bash
# Check connection string
sudo cat /var/honua/gitops-repo/environments/production/datasources.json | jq '.datasources[].connectionString'

# Test database connectivity
psql "$(echo $DB_CONNECTION_STRING)" -c "SELECT version();"
```

**Solution:**
```bash
# Update connection string in Git
vim environments/production/datasources.json

# Or update environment variable
sudo systemctl edit honua
# Add: Environment="DB_CONNECTION_STRING=Host=..."

sudo systemctl restart honua
```

### Problem: Performance Issues

**Symptoms:**
- Slow polling (taking longer than configured interval)
- High CPU or memory usage
- Large reconciliation duration

**Diagnosis:**

```bash
# Check reconciliation duration in logs
sudo journalctl -u honua | grep "completed reconciliation" | tail -20

# Monitor resource usage
top -u honua

# Check Git repository size
du -sh /var/honua/gitops-repo
```

**Solutions:**

**Large Repository:**
```bash
# Enable Git shallow clone (in future version)
# For now, clean up Git history if too large
cd /var/honua/gitops-repo
git gc --aggressive --prune=now
```

**Increase Poll Interval:**
```json
{
  "GitOps": {
    "Watcher": {
      "PollIntervalSeconds": 60  // Increase from 30 to 60 seconds
    }
  }
}
```

**Large Configuration Files:**
- Split large metadata.json into multiple services
- Use pagination for large layer lists
- Consider caching strategies

### Problem: State File Corruption

**Symptoms:**
- JSON parse errors when reading state files
- Missing deployment history
- Inconsistent deployment state

**Diagnosis:**

```bash
# Validate state file JSON
cat /var/honua/deployments/production.json | jq .

# Check file permissions
ls -la /var/honua/deployments/
```

**Recovery:**

**Option 1: Restore from backup (recommended)**
```bash
# Restore from daily backup
sudo cp /var/honua/backups/deployments/production-20251023.json \
        /var/honua/deployments/production.json

# Verify
cat /var/honua/deployments/production.json | jq .
```

**Option 2: Manual reconstruction**
```bash
# Get current Git commit
CURRENT_COMMIT=$(git -C /var/honua/gitops-repo rev-parse HEAD)

# Create minimal state file
cat > /tmp/production-state.json <<EOF
{
  "environment": "production",
  "currentDeployment": {
    "id": "production-$(date +%Y%m%d-%H%M%S)",
    "environment": "production",
    "commit": "$CURRENT_COMMIT",
    "branch": "main",
    "state": "Completed",
    "health": "Healthy",
    "syncStatus": "Synced",
    "startedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
    "completedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
    "initiatedBy": "manual-recovery"
  },
  "deployedCommit": "$CURRENT_COMMIT",
  "health": "Healthy",
  "syncStatus": "Synced",
  "lastUpdated": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "history": []
}
EOF

sudo mv /tmp/production-state.json /var/honua/deployments/production.json
sudo chown honua:honua /var/honua/deployments/production.json
```

**Prevention:**
```bash
# Set up daily backups
sudo crontab -e -u honua

# Add:
0 2 * * * cp /var/honua/deployments/*.json /var/honua/backups/deployments/$(date +\%Y\%m\%d)/
```

---

## Monitoring and Alerting

### Key Metrics to Monitor

The GitOps system exposes OpenTelemetry metrics that should be monitored:

**Reconciliation Metrics:**

| Metric | Type | Description | Alert Threshold |
|--------|------|-------------|-----------------|
| `honua.gitops.reconciliations.total` | Counter | Total reconciliation attempts | N/A |
| `honua.gitops.reconciliations.success` | Counter | Successful reconciliations | Drop to 0 |
| `honua.gitops.reconciliations.failure` | Counter | Failed reconciliations | > 3 in 1 hour |
| `honua.gitops.reconciliation.duration` | Histogram | Reconciliation duration (seconds) | > 60 seconds |

**Deployment Metrics (Future):**

| Metric | Description | Alert Threshold |
|--------|-------------|-----------------|
| `honua.gitops.deployments.pending` | Deployments in pending state | > 5 |
| `honua.gitops.deployments.awaiting_approval` | Deployments waiting approval | Age > 4 hours |
| `honua.gitops.approval.timeout` | Approval timeouts | > 1 per day |

### Alert Conditions

**Critical Alerts:**

```yaml
# Prometheus AlertManager rules

- alert: GitOpsReconciliationFailed
  expr: increase(honua_gitops_reconciliations_failure[5m]) > 2
  for: 5m
  labels:
    severity: critical
  annotations:
    summary: "GitOps reconciliation failing repeatedly"
    description: "{{ $value }} reconciliation failures in the last 5 minutes"

- alert: GitOpsReconciliationStalled
  expr: rate(honua_gitops_reconciliations_total[10m]) == 0
  for: 10m
  labels:
    severity: warning
  annotations:
    summary: "GitOps reconciliation has stalled"
    description: "No reconciliation attempts in the last 10 minutes"

- alert: GitOpsReconciliationSlow
  expr: histogram_quantile(0.95, honua_gitops_reconciliation_duration) > 60
  for: 5m
  labels:
    severity: warning
  annotations:
    summary: "GitOps reconciliation is slow"
    description: "95th percentile reconciliation duration is {{ $value }}s"
```

**Warning Alerts:**

```yaml
- alert: GitOpsDeploymentPending
  expr: honua_gitops_deployments_pending > 3
  for: 15m
  labels:
    severity: warning
  annotations:
    summary: "Multiple deployments pending"
    description: "{{ $value }} deployments are in pending state"

- alert: GitOpsApprovalTimeout
  expr: honua_gitops_approval_timeout > 0
  for: 1m
  labels:
    severity: warning
  annotations:
    summary: "Deployment approval timed out"
    description: "A deployment approval has timed out"
```

### Health Check Endpoints

**FUTURE:** When HTTP endpoints are implemented:

```bash
# Check GitOps health
curl http://localhost:5000/health/gitops

# Expected response:
{
  "status": "Healthy",
  "components": {
    "GitWatcher": "Healthy",
    "GitRepository": "Healthy",
    "DeploymentStateStore": "Healthy"
  },
  "lastReconciliation": "2025-10-23T14:30:22Z",
  "nextPoll": "2025-10-23T14:31:00Z"
}
```

**Current Workaround:** Monitor via logs and state files:

```bash
# Create health check script
cat > /usr/local/bin/honua-gitops-health <<'EOF'
#!/bin/bash
STATE_FILE="/var/honua/deployments/production.json"
LAST_UPDATE=$(jq -r '.lastUpdated' $STATE_FILE)
LAST_UPDATE_EPOCH=$(date -d "$LAST_UPDATE" +%s)
NOW_EPOCH=$(date +%s)
AGE=$((NOW_EPOCH - LAST_UPDATE_EPOCH))

if [ $AGE -gt 300 ]; then
  echo "CRITICAL: Last update was $AGE seconds ago"
  exit 2
elif [ $AGE -gt 120 ]; then
  echo "WARNING: Last update was $AGE seconds ago"
  exit 1
else
  echo "OK: Last update was $AGE seconds ago"
  exit 0
fi
EOF

chmod +x /usr/local/bin/honua-gitops-health

# Test
/usr/local/bin/honua-gitops-health
```

### Performance Considerations

**Recommended Monitoring Intervals:**

- Health checks: Every 60 seconds
- Metric scraping: Every 15 seconds
- Log aggregation: Real-time streaming
- Alert evaluation: Every 30 seconds

**Resource Limits:**

```yaml
# Kubernetes resource limits (example)
resources:
  requests:
    memory: "512Mi"
    cpu: "250m"
  limits:
    memory: "2Gi"
    cpu: "1000m"
```

**State File Rotation:**

```bash
# Clean up old deployment history
# Add to daily cron job
find /var/honua/deployments/*.json -mtime +90 -exec \
  jq '.history = .history[0:10]' {} \; > /tmp/cleaned.json && \
  mv /tmp/cleaned.json {}
```

---

## Summary

This runbook covers the complete lifecycle of GitOps deployment and operations for Honua. Key takeaways:

1. **Always test changes in development first** before promoting to production
2. **Monitor reconciliation logs** to catch issues early
3. **Set up proper alerting** for failed reconciliations and slow performance
4. **Back up state files daily** to enable recovery from corruption
5. **Use Git best practices** (PR reviews, branch protection) for production changes
6. **Never bypass GitOps** by making manual changes on the server
7. **Document all manual interventions** and create issues to prevent recurrence

For additional guidance, see:
- [GitOps Configuration Reference](./gitops-configuration-reference.md)
- [GitOps Troubleshooting Guide](./gitops-troubleshooting-guide.md)
- [GitOps Security Guide](./gitops-security-guide.md)
- [GitOps Best Practices](./gitops-best-practices.md)

---

**Last Updated:** 2025-10-23
**Version:** 1.0
**Feedback:** operations@example.com
