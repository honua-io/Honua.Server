# GitOps Troubleshooting Guide

**Version:** 1.0
**Last Updated:** 2025-10-23
**Audience:** Operations Teams, SRE, DevOps Engineers

Comprehensive troubleshooting guide for diagnosing and resolving GitOps issues in production.

---

## Table of Contents

1. [Quick Diagnostics](#quick-diagnostics)
2. [GitWatcher Issues](#gitwatcher-issues)
3. [Deployment Issues](#deployment-issues)
4. [Reconciliation Issues](#reconciliation-issues)
5. [Performance Issues](#performance-issues)
6. [State Management Issues](#state-management-issues)
7. [Git Repository Issues](#git-repository-issues)
8. [Configuration Issues](#configuration-issues)

---

## Quick Diagnostics

### Health Check Checklist

Run these commands to get a quick overview of GitOps system health:

```bash
# 1. Check Honua service is running
sudo systemctl status honua

# 2. Check recent GitOps logs
sudo journalctl -u honua --since "10 minutes ago" | grep -i "gitops\|gitwatcher\|reconcil"

# 3. Check deployment state
sudo cat /var/honua/deployments/production.json | jq '{
  health: .health,
  syncStatus: .syncStatus,
  lastUpdate: .lastUpdated,
  currentDeployment: .currentDeployment.state
}'

# 4. Check Git repository
sudo -u honua git -C /var/honua/gitops-repo status

# 5. Check file permissions
ls -la /var/honua/

# 6. Check recent errors
sudo journalctl -u honua --since "1 hour ago" | grep -i error | tail -20
```

### Common Log Patterns

**Healthy System:**
```
[Information] GitWatcher started. Watching branch 'main' every 30 seconds
[Debug] Polling for changes on branch 'main'
[Debug] No new commits detected
```

**System with Issues:**
```
[Error] Failed to pull from remote repository
[Warning] Deployment failed, initiating automatic rollback
[Error] Error during reconciliation
```

---

## GitWatcher Issues

### Problem: GitWatcher Not Starting

**Symptoms:**
- No "GitWatcher started" message in logs
- Service starts but GitOps inactive
- No polling activity

**Diagnosis:**

```bash
# Check if GitOps is configured
cat /etc/honua/appsettings.Production.json | jq '.GitOps'

# Check for configuration errors
sudo journalctl -u honua --since "5 minutes ago" | grep -i "gitops\|configuration\|required"

# Verify GitOps services registered
sudo journalctl -u honua --since "5 minutes ago" | grep -i "addgitops\|gitwatcher\|reconciler"
```

**Common Causes and Solutions:**

#### Cause 1: GitOps Not Enabled

**Check:**
```bash
# GitOps section might be missing or incomplete
cat /etc/honua/appsettings.Production.json | jq '.GitOps'
```

**Solution:**
```bash
# Add complete GitOps configuration
sudo vim /etc/honua/appsettings.Production.json

# Add:
{
  "GitOps": {
    "RepositoryPath": "/var/honua/gitops-repo",
    "StateDirectory": "/var/honua/deployments",
    "Watcher": {
      "Branch": "main",
      "Environment": "production",
      "PollIntervalSeconds": 30
    }
  }
}

# Restart service
sudo systemctl restart honua
```

#### Cause 2: Invalid Repository Path

**Error Message:**
```
GitOps repository path does not exist: /var/honua/gitops-repo
GitOps repository path is not a valid Git repository
```

**Solution:**
```bash
# Create directory and clone repository
sudo mkdir -p /var/honua/gitops-repo
sudo chown honua:honua /var/honua/gitops-repo
sudo -u honua git clone git@github.com:your-org/honua-config.git /var/honua/gitops-repo

# Restart service
sudo systemctl restart honua
```

#### Cause 3: Missing State Directory

**Error Message:**
```
GitOps:StateDirectory configuration is required when GitOps is enabled
```

**Solution:**
```bash
# Create state directory
sudo mkdir -p /var/honua/deployments
sudo chown honua:honua /var/honua/deployments
sudo chmod 700 /var/honua/deployments

# Restart service
sudo systemctl restart honua
```

**Prevention:**
- Validate configuration before deployment
- Use configuration schema validation
- Document required directories in deployment checklist

---

### Problem: GitWatcher Not Detecting Changes

**Symptoms:**
- Git commits pushed to repository
- No reconciliation triggered
- Polling logs show "No new commits detected"

**Diagnosis:**

```bash
# Check GitWatcher is polling
sudo journalctl -u honua -f | grep -i poll

# Check current commit in Git repository
cd /var/honua/gitops-repo
git log --oneline -5

# Check last known commit in logs
sudo journalctl -u honua | grep "Initial commit\|new commit" | tail -5

# Manual pull to test
sudo -u honua git -C /var/honua/gitops-repo pull
```

**Common Causes and Solutions:**

#### Cause 1: Wrong Branch Configured

**Check:**
```bash
# Check configured branch
cat /etc/honua/appsettings.Production.json | jq '.GitOps.Watcher.Branch'

# Check actual Git branch
git -C /var/honua/gitops-repo branch -a
```

**Solution:**
```bash
# If watching wrong branch, update configuration
sudo vim /etc/honua/appsettings.Production.json

# Change:
"Branch": "correct-branch-name"

# Or checkout correct branch in repository
cd /var/honua/gitops-repo
git checkout main
git pull origin main

# Restart service
sudo systemctl restart honua
```

#### Cause 2: Git Pull Failures

**Error Message:**
```
[Error] Error polling for changes
[Error] Failed to pull from remote repository
```

**Check:**
```bash
# Test manual pull
sudo -u honua git -C /var/honua/gitops-repo pull

# Check Git remote
git -C /var/honua/gitops-repo remote -v
```

**Solution:**

**If SSH authentication fails:**
```bash
# Test SSH connection
sudo -u honua ssh -T git@github.com

# If fails, check SSH key
ls -la /home/honua/.ssh/

# Ensure key exists and has correct permissions
chmod 600 /home/honua/.ssh/id_ed25519
chown honua:honua /home/honua/.ssh/id_ed25519

# Test again
sudo -u honua ssh -T git@github.com
```

**If network issues:**
```bash
# Check DNS resolution
nslookup github.com

# Check connectivity
curl -I https://github.com

# Check firewall rules
sudo iptables -L | grep -i git
```

#### Cause 3: Changes in Non-Relevant Paths

**Diagnosis:**

GitWatcher only reconciles when files in specific paths change:
- `environments/{environment}/`
- `environments/common/`

**Check:**
```bash
# View changed files in recent commit
cd /var/honua/gitops-repo
git show --name-only HEAD

# If files are outside environments/, they're ignored
```

**Solution:**

Ensure changes are in correct directories:
```bash
# Correct paths:
environments/production/metadata.json        # Will trigger
environments/production/datasources.json     # Will trigger
environments/common/shared-config.json       # Will trigger

# Ignored paths:
README.md                                    # Ignored
docs/architecture.md                         # Ignored
.gitops/deployment-policy.yaml               # Ignored (future use)
```

**Prevention:**
- Document expected directory structure
- Use Git hooks to validate commits
- Add pre-commit checks for file locations

---

### Problem: GitWatcher Polling But Not Reconciling

**Symptoms:**
- Polling logs appear normal
- Changes detected in logs
- But reconciliation doesn't run

**Diagnosis:**

```bash
# Check full log sequence
sudo journalctl -u honua --since "5 minutes ago" | grep -E "poll|detect|reconcil"

# Look for errors between detection and reconciliation
sudo journalctl -u honua --since "5 minutes ago" | grep -i error
```

**Common Causes and Solutions:**

#### Cause 1: Reconciler Not Registered

**Check:**
```bash
# Look for reconciler initialization
sudo journalctl -u honua --since boot | grep -i "reconciler\|addgitops"
```

**Solution:**

This is a dependency injection issue. Verify `AddGitOps()` is called in startup:

```bash
# Check service configuration
# This requires code inspection

# Temporary workaround: Restart service
sudo systemctl restart honua
```

#### Cause 2: Exception During Reconciliation Startup

**Check:**
```bash
# Look for exceptions right after "Starting reconciliation"
sudo journalctl -u honua | grep -A 20 "Starting reconciliation"
```

**Solution:**

Based on exception, fix underlying issue (see [Reconciliation Issues](#reconciliation-issues))

---

## Deployment Issues

### Problem: Deployments Stuck in Pending

**Symptoms:**
- Deployment created but never transitions to "Applying"
- State remains "Pending" indefinitely

**Diagnosis:**

```bash
# Check deployment state
sudo cat /var/honua/deployments/production.json | jq '.currentDeployment | {
  id,
  state,
  startedAt,
  stateHistory
}'

# Check for errors
sudo journalctl -u honua | grep -A 10 "Deployment created"
```

**Common Causes and Solutions:**

#### Cause 1: Deployment Policy Blocking

**Check:**
```bash
# Check deployment policy
cat /etc/honua/appsettings.Production.json | jq '.GitOps.DeploymentPolicy'

# Check current time against allowed hours
date +"%A %H:%M"
```

**Solution:**

**If outside allowed hours:**
```
Wait until deployment window opens, or update policy
```

**If outside allowed days:**
```json
// Update policy to include current day
"AllowedDays": ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"]
```

#### Cause 2: Service Stalled

**Check:**
```bash
# Check service is responsive
sudo systemctl status honua

# Check CPU/memory
top -u honua
```

**Solution:**
```bash
# Restart service
sudo systemctl restart honua
```

**Prevention:**
- Monitor service health
- Set up watchdog timer
- Alert on deployment state staleness

---

### Problem: Deployments Stuck in AwaitingApproval

**Symptoms:**
- Deployment state is "AwaitingApproval"
- No way to approve (CLI not implemented)
- Approval timeout not yet reached

**Diagnosis:**

```bash
# Check approval status
DEPLOYMENT_ID=$(sudo cat /var/honua/deployments/production.json | jq -r '.currentDeployment.id')
sudo cat /var/honua/approvals/${DEPLOYMENT_ID}.json | jq .

# Check time remaining
# RequestedAt + ApprovalTimeout - Now
```

**Solutions:**

#### Option 1: Manual Approval via File (Current Implementation)

```bash
# Get deployment ID
DEPLOYMENT_ID=$(sudo cat /var/honua/deployments/production.json | jq -r '.currentDeployment.id')

# Create approval file
cat > /tmp/approval.json <<EOF
{
  "deploymentId": "${DEPLOYMENT_ID}",
  "environment": "production",
  "state": "Approved",
  "responder": "$(whoami)@$(hostname)",
  "respondedAt": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "reason": "Manual approval via troubleshooting"
}
EOF

# Move to approvals directory
sudo mv /tmp/approval.json /var/honua/approvals/${DEPLOYMENT_ID}.json
sudo chown honua:honua /var/honua/approvals/${DEPLOYMENT_ID}.json

# Watch for deployment to proceed
sudo journalctl -u honua -f | grep -i "approved\|applying"
```

#### Option 2: Disable Approval Requirement

```bash
# Update configuration
sudo vim /etc/honua/appsettings.Production.json

# Set:
{
  "GitOps": {
    "DeploymentPolicy": {
      "RequiresApproval": false
    }
  }
}

# Restart service
sudo systemctl restart honua
```

**CAUTION:** This disables approval for ALL future deployments

#### Option 3: Wait for Timeout

If `ApprovalTimeout` is reasonable, wait for automatic timeout and rejection.

**Prevention:**
- Implement CLI approval command (future work)
- Set reasonable approval timeouts
- Monitor approval queue
- Alert on pending approvals

---

### Problem: Deployments Failing Immediately

**Symptoms:**
- Deployment transitions from "Pending" to "Failed" immediately
- Error message in deployment state
- Auto-rollback may trigger

**Diagnosis:**

```bash
# Check error message
sudo cat /var/honua/deployments/production.json | jq '.currentDeployment | {
  state,
  errorMessage,
  stateHistory
}'

# Check logs around failure time
sudo journalctl -u honua | grep -i "error\|fail" | tail -30
```

**Common Causes:**

See [Reconciliation Issues](#reconciliation-issues) for specific error resolutions

**Immediate Actions:**

```bash
# If auto-rollback enabled, system will recover automatically
# Monitor rollback progress
sudo journalctl -u honua -f | grep -i rollback

# If auto-rollback disabled, manual revert required
cd /var/honua/gitops-repo
git revert HEAD
git push origin main
```

---

## Reconciliation Issues

### Problem: Invalid JSON in Configuration Files

**Symptoms:**
- Reconciliation fails with JSON parse error
- Error message mentions specific file

**Error Message:**
```
[Error] Invalid JSON in configuration file 'environments/production/metadata.json'
```

**Diagnosis:**

```bash
# Validate JSON locally
cat /var/honua/gitops-repo/environments/production/metadata.json | jq .

# If error, jq will show line/column
```

**Common JSON Errors:**

**Trailing comma:**
```json
{
  "services": [
    {
      "id": "service1",
    }  // <- Trailing comma not allowed
  ]
}
```

**Missing quotes:**
```json
{
  id: "service1"  // <- Missing quotes around property name
}
```

**Missing comma:**
```json
{
  "id": "service1"
  "title": "Service"  // <- Missing comma
}
```

**Solution:**

```bash
# Fix JSON in Git repository (on workstation)
cd honua-config
vim environments/production/metadata.json

# Validate before committing
jq . environments/production/metadata.json

# Commit and push fix
git add environments/production/metadata.json
git commit -m "fix: Correct JSON syntax in metadata.json"
git push origin main

# Monitor reconciliation
sudo journalctl -u honua -f | grep -i reconcil
```

**Prevention:**
- Use JSON schema validation in CI/CD
- Configure editor with JSON linting
- Use pre-commit hooks to validate JSON
- Add JSON validation tests

**Pre-commit Hook Example:**
```bash
#!/bin/bash
# .git/hooks/pre-commit

for file in $(git diff --cached --name-only | grep '\.json$'); do
  jq . "$file" > /dev/null 2>&1
  if [ $? -ne 0 ]; then
    echo "Invalid JSON in $file"
    exit 1
  fi
done
```

---

### Problem: Database Connection Failures

**Symptoms:**
- Reconciliation fails when processing datasources.json
- Error mentions connection or authentication

**Error Messages:**
```
[Error] Failed to validate datasource connections
[Error] Could not connect to database
[Error] Authentication failed for user
```

**Diagnosis:**

```bash
# Check datasources configuration
sudo cat /var/honua/gitops-repo/environments/production/datasources.json | jq .

# Test connection manually
# Extract connection string (replace with actual from datasources.json)
psql "Host=prod-db;Database=gis;Username=honua;Password=secret" -c "SELECT version();"
```

**Common Causes and Solutions:**

#### Cause 1: Incorrect Connection String

**Solution:**
```bash
# Update connection string in Git
cd honua-config
vim environments/production/datasources.json

# Fix connection string
{
  "datasources": [
    {
      "id": "postgres-prod",
      "type": "PostgreSQL",
      "connectionString": "Host=correct-host;Database=gis;Username=honua;Password=secret"
    }
  ]
}

# Commit and push
git add environments/production/datasources.json
git commit -m "fix: Correct database connection string"
git push origin main
```

#### Cause 2: Environment Variable Not Set

**If using environment variables:**
```json
{
  "connectionString": "${DB_CONNECTION_STRING}"
}
```

**Check:**
```bash
# Check environment variable is set
sudo systemctl show honua | grep DB_CONNECTION_STRING
```

**Solution:**
```bash
# Set environment variable
sudo systemctl edit honua

# Add:
[Service]
Environment="DB_CONNECTION_STRING=Host=prod-db;Database=gis;Username=honua;Password=secret"

# Reload and restart
sudo systemctl daemon-reload
sudo systemctl restart honua
```

#### Cause 3: Network/Firewall Issues

**Check:**
```bash
# Test network connectivity to database
telnet prod-db 5432

# Check firewall rules
sudo iptables -L | grep 5432
```

**Solution:**
```bash
# Add firewall rule if needed
sudo iptables -A OUTPUT -p tcp --dport 5432 -j ACCEPT
```

**Prevention:**
- Validate connection strings in CI/CD
- Use connection pooling
- Monitor database connectivity
- Set up alerts for connection failures

---

### Problem: Metadata Registry Not Reloading

**Symptoms:**
- Reconciliation completes successfully
- Logs show "Successfully reloaded metadata registry"
- But new layers/services not appearing

**Diagnosis:**

```bash
# Check reconciliation logs
sudo journalctl -u honua | grep -i "metadata\|reload" | tail -20

# Test OGC API
curl http://localhost:5000/ogc/collections | jq '.collections[].id'

# Check metadata registry directly (if endpoint exists)
curl http://localhost:5000/admin/metadata/snapshot | jq .
```

**Common Causes and Solutions:**

#### Cause 1: MetadataRegistry Not Wired to Reconciler

**This is a dependency injection issue.**

**Check:**
```bash
# Look for MetadataRegistry injection warnings
sudo journalctl -u honua --since boot | grep -i "metadata.*null\|metadataregistry"
```

**Solution:**

Verify `IMetadataRegistry` is registered and passed to `HonuaReconciler`:

```bash
# This requires code verification
# Temporary workaround: Restart entire service
sudo systemctl restart honua
```

#### Cause 2: Metadata File Not in Expected Format

**Check:**
```bash
# Verify metadata.json structure matches expected schema
cat /var/honua/gitops-repo/environments/production/metadata.json | jq '.services'
```

**Solution:**

Ensure metadata follows correct structure:
```json
{
  "services": [
    {
      "id": "string",
      "title": "string",
      "description": "string",
      "layers": [...]
    }
  ]
}
```

#### Cause 3: Caching Issues

**If metadata is cached:**

**Solution:**
```bash
# Clear metadata cache (if applicable)
# Method depends on caching implementation

# Or restart service to force reload
sudo systemctl restart honua
```

**Prevention:**
- Add metadata validation in reconciliation
- Log metadata service count before/after reload
- Add health check for metadata registry
- Implement cache invalidation on reload

---

### Problem: Reconciliation Timing Out or Very Slow

**Symptoms:**
- Reconciliation takes > 60 seconds
- Timeout errors in logs
- High CPU usage during reconciliation

**Diagnosis:**

```bash
# Check reconciliation duration
sudo journalctl -u honua | grep "completed reconciliation" | tail -20

# Monitor resource usage during reconciliation
top -u honua

# Check for I/O wait
iostat -x 1
```

**Common Causes and Solutions:**

#### Cause 1: Large Configuration Files

**Check:**
```bash
# Check file sizes
du -sh /var/honua/gitops-repo/environments/production/*.json
ls -lh /var/honua/gitops-repo/environments/production/
```

**Solution:**
- Split large metadata.json into multiple files (future enhancement)
- Optimize JSON structure
- Remove unnecessary properties
- Consider pagination for large layer lists

#### Cause 2: Slow Database Migrations

**Check:**
```bash
# Look for migration logs
sudo journalctl -u honua | grep -i migration | tail -20
```

**Solution:**
- Run heavy migrations outside of GitOps
- Use migration timeout configuration
- Split large migrations into smaller chunks

#### Cause 3: Network Latency (Git Pull)

**Check:**
```bash
# Time a manual pull
time sudo -u honua git -C /var/honua/gitops-repo pull
```

**Solution:**
- Use local Git server/mirror
- Enable Git shallow clone
- Increase network timeout
- Use Git LFS for large files

**Prevention:**
- Monitor reconciliation duration metric
- Alert on slow reconciliations
- Optimize configuration file size
- Use CDN for Git repository (if applicable)

---

## Performance Issues

### Problem: High CPU Usage

**Symptoms:**
- Honua process consuming > 50% CPU continuously
- Server becomes slow during reconciliation

**Diagnosis:**

```bash
# Check CPU usage
top -u honua

# Check what's consuming CPU
sudo perf top -p $(pgrep -f honua)

# Check reconciliation frequency
sudo journalctl -u honua | grep "Starting reconciliation" | tail -20
```

**Common Causes and Solutions:**

#### Cause 1: Poll Interval Too Low

**Check:**
```bash
cat /etc/honua/appsettings.Production.json | jq '.GitOps.Watcher.PollIntervalSeconds'
```

**Solution:**
```bash
# Increase poll interval
sudo vim /etc/honua/appsettings.Production.json

# Change:
"PollIntervalSeconds": 60  # Increase from 30 to 60

sudo systemctl restart honua
```

#### Cause 2: Git Operations Intensive

**Solution:**
```bash
# Optimize Git repository
cd /var/honua/gitops-repo
git gc --aggressive
git prune

# Enable Git compression
git config core.compression 9
```

**Prevention:**
- Monitor CPU usage metric
- Set appropriate poll intervals
- Optimize Git repository regularly
- Consider rate limiting

---

### Problem: High Memory Usage

**Symptoms:**
- Honua process consuming > 2 GB RAM
- Out of memory errors

**Diagnosis:**

```bash
# Check memory usage
top -u honua

# Check for memory leaks
sudo journalctl -u honua | grep -i "memory\|oom"

# Check deployment state file sizes
du -sh /var/honua/deployments/
```

**Common Causes and Solutions:**

#### Cause 1: Large Deployment History

**Check:**
```bash
# Check history size in state files
sudo cat /var/honua/deployments/production.json | jq '.history | length'
```

**Solution:**
```bash
# Trim history to last 10 deployments
sudo jq '.history = .history[0:10]' /var/honua/deployments/production.json > /tmp/trimmed.json
sudo mv /tmp/trimmed.json /var/honua/deployments/production.json
sudo chown honua:honua /var/honua/deployments/production.json
```

#### Cause 2: Memory Leak

**Solution:**
```bash
# Restart service to clear memory
sudo systemctl restart honua

# If persists, report as bug
```

**Prevention:**
- Set memory limits in systemd
- Monitor memory usage
- Rotate state files regularly
- Alert on high memory usage

---

## State Management Issues

### Problem: State File Corruption

**Symptoms:**
- JSON parse errors when reading state files
- Deployment state inconsistent
- "Invalid state file" errors

**Diagnosis:**

```bash
# Validate state file JSON
sudo cat /var/honua/deployments/production.json | jq .

# If error, backup and inspect
sudo cp /var/honua/deployments/production.json /tmp/corrupted-state.json
sudo cat /tmp/corrupted-state.json
```

**Recovery Procedures:**

#### Option 1: Restore from Backup

```bash
# List available backups
ls -lh /var/honua/backups/deployments/

# Restore from yesterday's backup
sudo cp /var/honua/backups/deployments/20251022/production.json \
        /var/honua/deployments/production.json
sudo chown honua:honua /var/honua/deployments/production.json

# Verify
sudo cat /var/honua/deployments/production.json | jq .
```

#### Option 2: Manual Reconstruction

```bash
# Get current Git commit
CURRENT_COMMIT=$(sudo -u honua git -C /var/honua/gitops-repo rev-parse HEAD)

# Create minimal valid state file
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
    "initiatedBy": "manual-recovery",
    "stateHistory": []
  },
  "deployedCommit": "$CURRENT_COMMIT",
  "health": "Healthy",
  "syncStatus": "Synced",
  "lastUpdated": "$(date -u +%Y-%m-%dT%H:%M:%SZ)",
  "history": []
}
EOF

# Validate
jq . /tmp/production-state.json

# Install
sudo mv /tmp/production-state.json /var/honua/deployments/production.json
sudo chown honua:honua /var/honua/deployments/production.json
sudo chmod 600 /var/honua/deployments/production.json
```

**Prevention:**
```bash
# Set up automated backups
sudo crontab -e -u honua

# Add daily backup at 2 AM
0 2 * * * mkdir -p /var/honua/backups/deployments/$(date +\%Y\%m\%d) && cp /var/honua/deployments/*.json /var/honua/backups/deployments/$(date +\%Y\%m\%d)/
```

---

## Git Repository Issues

### Problem: Git Pull Failures

**Symptoms:**
- "Failed to pull from remote repository"
- Authentication errors
- Network errors

**Diagnosis:**

```bash
# Test manual pull
sudo -u honua git -C /var/honua/gitops-repo pull

# Check Git configuration
git -C /var/honua/gitops-repo config -l

# Check remote URL
git -C /var/honua/gitops-repo remote -v
```

**Common Causes and Solutions:**

#### Cause 1: SSH Key Issues

**Error Message:**
```
Permission denied (publickey)
```

**Solution:**
```bash
# Test SSH connection
sudo -u honua ssh -T git@github.com

# If fails, check key permissions
ls -la /home/honua/.ssh/
chmod 600 /home/honua/.ssh/id_ed25519
chmod 644 /home/honua/.ssh/id_ed25519.pub

# Ensure key is added to ssh-agent
eval "$(ssh-agent -s)"
ssh-add /home/honua/.ssh/id_ed25519

# Test again
sudo -u honua ssh -T git@github.com
```

#### Cause 2: Network Connectivity

**Solution:**
```bash
# Test network
ping github.com
curl -I https://github.com

# Check firewall
sudo iptables -L | grep -E "22|443"

# Check DNS
nslookup github.com
```

#### Cause 3: Git Repository Locked

**Error Message:**
```
index.lock: File exists
```

**Solution:**
```bash
# Remove stale lock file
sudo rm /var/honua/gitops-repo/.git/index.lock

# Try pull again
sudo -u honua git -C /var/honua/gitops-repo pull
```

**Prevention:**
- Monitor Git operation success rate
- Set up SSH key rotation
- Use connection pooling
- Alert on authentication failures

---

### Problem: Merge Conflicts

**Symptoms:**
- Git pull fails with merge conflict
- Repository in conflicted state

**Diagnosis:**

```bash
# Check repository status
git -C /var/honua/gitops-repo status

# Check for conflicts
git -C /var/honua/gitops-repo diff
```

**Solution:**

```bash
# OPTION 1: Reset to remote (discard local changes)
cd /var/honua/gitops-repo
git fetch origin
git reset --hard origin/main

# OPTION 2: Stash local changes and pull
cd /var/honua/gitops-repo
git stash
git pull origin main
git stash pop
```

**CRITICAL:** GitOps should never make local commits. If conflicts occur, it indicates:
1. Manual changes were made directly to the repository
2. Multiple instances are writing to the same repository

**Prevention:**
- NEVER modify files in the GitOps repository directly
- Use read-only Git access (deploy keys without write permission)
- Monitor for unexpected local changes
- Alert on repository state issues

---

## Configuration Issues

### Problem: Configuration Not Loading

**Symptoms:**
- Configuration changes in Git not being applied
- Reconciliation runs but doesn't update configuration

**Diagnosis:**

```bash
# Check reconciled configuration
ls -la /var/honua/gitops-repo/reconciled/production/

# Check timestamps
ls -lt /var/honua/gitops-repo/reconciled/production/

# Compare with Git
diff /var/honua/gitops-repo/reconciled/production/metadata.json \
     /var/honua/gitops-repo/environments/production/metadata.json
```

**Solution:**

Ensure reconciliation is completing successfully (see [Reconciliation Issues](#reconciliation-issues))

---

## Emergency Procedures

### Emergency Rollback

**When to Use:**
- Critical production issue caused by deployment
- Need immediate recovery
- Auto-rollback failed or disabled

**Procedure:**

```bash
# 1. Stop GitWatcher to prevent additional deployments
sudo systemctl stop honua

# 2. Identify last successful deployment
sudo cat /var/honua/deployments/production.json | jq '.lastSuccessfulDeployment | {id, commit}'

# 3. Revert Git repository
cd /var/honua/gitops-repo
LAST_GOOD_COMMIT="<commit-from-step-2>"
git reset --hard $LAST_GOOD_COMMIT

# 4. Restart Honua
sudo systemctl start honua

# 5. Monitor recovery
sudo journalctl -u honua -f
```

### Complete GitOps Reset

**When to Use:**
- Multiple failures
- State corruption beyond recovery
- Starting fresh required

**Procedure:**

```bash
# 1. Stop service
sudo systemctl stop honua

# 2. Backup everything
sudo tar -czf /tmp/honua-backup-$(date +%Y%m%d-%H%M%S).tar.gz /var/honua/

# 3. Remove all state
sudo rm -rf /var/honua/deployments/*
sudo rm -rf /var/honua/approvals/*

# 4. Re-clone repository
sudo rm -rf /var/honua/gitops-repo
sudo -u honua git clone git@github.com:your-org/honua-config.git /var/honua/gitops-repo

# 5. Restart service
sudo systemctl start honua

# 6. Monitor initialization
sudo journalctl -u honua -f
```

---

## Getting Help

### Collect Diagnostics

Before reporting issues, collect comprehensive diagnostics:

```bash
#!/bin/bash
# Save as: /usr/local/bin/honua-gitops-diagnostics

TIMESTAMP=$(date +%Y%m%d-%H%M%S)
DIAG_DIR="/tmp/honua-diagnostics-$TIMESTAMP"

mkdir -p $DIAG_DIR

echo "Collecting Honua GitOps diagnostics..."

# System info
uname -a > $DIAG_DIR/system-info.txt
date >> $DIAG_DIR/system-info.txt

# Service status
systemctl status honua > $DIAG_DIR/service-status.txt

# Recent logs
journalctl -u honua --since "1 hour ago" > $DIAG_DIR/logs-recent.txt

# Configuration (sanitized)
cat /etc/honua/appsettings.Production.json | \
  jq '.GitOps.Credentials.Password = "REDACTED"' > $DIAG_DIR/configuration.json

# Deployment state
cp /var/honua/deployments/*.json $DIAG_DIR/ 2>/dev/null

# Git repository info
git -C /var/honua/gitops-repo status > $DIAG_DIR/git-status.txt 2>&1
git -C /var/honua/gitops-repo log --oneline -10 > $DIAG_DIR/git-log.txt 2>&1

# File permissions
ls -laR /var/honua/ > $DIAG_DIR/permissions.txt

# Create tarball
tar -czf /tmp/honua-diagnostics-$TIMESTAMP.tar.gz -C /tmp honua-diagnostics-$TIMESTAMP

echo "Diagnostics collected: /tmp/honua-diagnostics-$TIMESTAMP.tar.gz"
```

### Support Resources

- **Documentation:** `/docs/operations/`
- **Issue Tracker:** [GitHub Issues](https://github.com/your-org/HonuaIO/issues)
- **Slack Channel:** #honua-support
- **Email:** support@example.com

---

**Last Updated:** 2025-10-23
**Version:** 1.0
