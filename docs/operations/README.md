# GitOps Operations Documentation

Comprehensive operational documentation for deploying and managing Honua GitOps in production.

---

## Documentation Index

### Core Operations Guides

1. **[GitOps Deployment Runbook](./gitops-deployment-runbook.md)**
   - Step-by-step deployment procedures
   - Initial setup and configuration
   - Day-to-day operations
   - First deployment walkthrough
   - Complete with validation steps and verification

2. **[GitOps Configuration Reference](./gitops-configuration-reference.md)**
   - Complete appsettings.json schema
   - All configuration options explained
   - Environment-specific configurations
   - Deployment policies reference
   - Performance tuning options

3. **[GitOps Troubleshooting Guide](./gitops-troubleshooting-guide.md)**
   - Problem-solution pairs for common issues
   - Diagnostic procedures
   - Recovery procedures
   - Emergency rollback instructions
   - Comprehensive error resolution

4. **[GitOps Security Guide](./gitops-security-guide.md)**
   - Security best practices
   - Authentication and authorization
   - Secrets management
   - Access control
   - Network security
   - Audit and compliance
   - Incident response procedures

5. **[GitOps Best Practices](./gitops-best-practices.md)**
   - Repository organization
   - Configuration management
   - Branch strategies
   - Rollback strategies
   - Multi-environment management
   - Change management process

6. **[GitOps Monitoring Dashboard](./gitops-monitoring-dashboard.md)**
   - Prometheus configuration
   - Alert rules
   - Grafana dashboards
   - Log aggregation queries
   - Performance metrics
   - SLA tracking

---

## Production Deployment Examples

### Docker Compose

**[docker-compose.gitops.yml](../../samples/production/docker-compose.gitops.yml)**

Production-ready Docker Compose configuration including:
- Honua server with GitOps enabled
- PostgreSQL database
- Prometheus for metrics
- Grafana for visualization
- Volume mounts for persistent state
- Health checks and resource limits
- Security configurations

### Kubernetes

**[gitops-deployment.yaml](../../samples/production/kubernetes/gitops-deployment.yaml)**

Complete Kubernetes manifests including:
- Deployment with high availability (2+ replicas)
- ConfigMaps for application settings
- Secrets for credentials
- PersistentVolumes for state storage
- Init containers for Git repository cloning
- Services and Ingress
- HorizontalPodAutoscaler
- PodDisruptionBudget
- NetworkPolicy for security

---

## Quick Start

### For Operations Teams

**New to GitOps?** Start here:

1. Read [GitOps Deployment Runbook - Prerequisites](./gitops-deployment-runbook.md#prerequisites)
2. Review [GitOps Security Guide](./gitops-security-guide.md)
3. Follow [GitOps Deployment Runbook - Initial Setup](./gitops-deployment-runbook.md#initial-setup)
4. Configure monitoring using [GitOps Monitoring Dashboard](./gitops-monitoring-dashboard.md)
5. Bookmark [GitOps Troubleshooting Guide](./gitops-troubleshooting-guide.md) for quick reference

### For DevOps Engineers

**Setting up GitOps?** Follow this path:

1. Review [GitOps Configuration Reference](./gitops-configuration-reference.md)
2. Choose deployment method:
   - Docker: [docker-compose.gitops.yml](../../samples/production/docker-compose.gitops.yml)
   - Kubernetes: [gitops-deployment.yaml](../../samples/production/kubernetes/gitops-deployment.yaml)
3. Implement security controls from [GitOps Security Guide](./gitops-security-guide.md)
4. Set up monitoring per [GitOps Monitoring Dashboard](./gitops-monitoring-dashboard.md)
5. Adopt practices from [GitOps Best Practices](./gitops-best-practices.md)

### For Troubleshooting

**Something broken?** Quick troubleshooting:

1. Check [Quick Diagnostics](./gitops-troubleshooting-guide.md#quick-diagnostics)
2. Find your issue in [GitOps Troubleshooting Guide](./gitops-troubleshooting-guide.md)
3. If not found, collect diagnostics and contact support

---

## Architecture Overview

```
┌─────────────────────────────────────────────┐
│         Git Repository (Source of Truth)    │
│                                             │
│  environments/                              │
│  ├── production/                            │
│  │   ├── metadata.json                      │
│  │   └── datasources.json                   │
│  ├── staging/                               │
│  └── development/                           │
└─────────────┬───────────────────────────────┘
              │
              │ Pull every 60s
              ▼
┌─────────────────────────────────────────────┐
│         GitWatcher (Background Service)     │
│  - Polls Git repository                     │
│  - Detects changes                          │
│  - Filters by environment                   │
└─────────────┬───────────────────────────────┘
              │
              │ Triggers on changes
              ▼
┌─────────────────────────────────────────────┐
│      HonuaReconciler (Applies Changes)      │
│  - Compares desired (Git) vs actual state   │
│  - Validates configuration                  │
│  - Applies metadata updates                 │
│  - Reloads services                         │
└─────────────┬───────────────────────────────┘
              │
              │ Updates
              ▼
┌─────────────────────────────────────────────┐
│         Deployment State Store              │
│  - Tracks deployment history                │
│  - Stores current state                     │
│  - Manages approvals                        │
│  - Enables rollback                         │
└─────────────────────────────────────────────┘
```

---

## Key Concepts

### GitOps Principles

1. **Declarative:** Entire system state described in Git
2. **Versioned:** All changes tracked in version control
3. **Immutable:** Changes create new commits, not modify existing
4. **Pulled:** System pulls desired state (vs push-based CD)
5. **Continuously Reconciled:** Automatic drift detection and correction

### Components

**GitWatcher:**
- Background service polling Git repository
- Detects new commits on configured branch
- Filters changes by environment path
- Triggers reconciliation on relevant changes

**HonuaReconciler:**
- Compares desired state (Git) with actual state (deployed)
- Validates configuration files
- Applies changes to running system
- Reloads metadata registry without restart

**Deployment State Store:**
- Persists deployment history
- Tracks current deployment state
- Manages approval workflow
- Enables rollback to previous states

**Approval Service:**
- Enforces deployment policies
- Manages approval workflow
- Tracks approval/rejection decisions
- Handles approval timeouts

---

## Common Tasks

### Deploy a Configuration Change

```bash
# 1. Edit configuration in Git
vim environments/production/metadata.json

# 2. Commit and push
git add environments/production/metadata.json
git commit -m "feat: Add new transit layer"
git push origin main

# 3. Monitor deployment
sudo journalctl -u honua -f | grep -i reconcil

# 4. Verify deployment
curl https://honua.example.com/ogc/collections
```

### Rollback a Deployment

```bash
# Revert last commit
git revert HEAD
git push origin main

# Monitor rollback
sudo journalctl -u honua -f
```

### Approve a Deployment

```bash
# Get deployment ID
DEPLOYMENT_ID=$(sudo cat /var/honua/deployments/production.json | jq -r '.currentDeployment.id')

# Approve (manual file creation - CLI coming soon)
# See GitOps Troubleshooting Guide for detailed steps
```

### Check Deployment Status

```bash
# View current state
sudo cat /var/honua/deployments/production.json | jq '{
  state: .currentDeployment.state,
  health: .health,
  commit: .deployedCommit[0:7],
  lastUpdate: .lastUpdated
}'
```

---

## Critical Operations

### Emergency Procedures

**Stop GitOps (Emergency):**
```bash
sudo systemctl stop honua
```

**Emergency Rollback:**
```bash
# See GitOps Deployment Runbook - Emergency Procedures
cd /var/honua/gitops-repo
git reset --hard <last-good-commit>
sudo systemctl restart honua
```

**State File Recovery:**
```bash
# See GitOps Troubleshooting Guide - State File Corruption
sudo cp /var/honua/backups/deployments/production.json \
        /var/honua/deployments/production.json
```

---

## Support and Resources

### Documentation

- **Developer Docs:** [/docs/dev/gitops-*.md](../dev/)
- **Architecture:** [/docs/dev/gitops-architecture.md](../dev/gitops-architecture.md)
- **Implementation:** [/docs/dev/gitops-implementation-summary.md](../dev/gitops-implementation-summary.md)

### Getting Help

**Before asking for help:**

1. Check [GitOps Troubleshooting Guide](./gitops-troubleshooting-guide.md)
2. Collect diagnostics:
   ```bash
   sudo journalctl -u honua --since "1 hour ago" > honua-logs.txt
   sudo cat /var/honua/deployments/production.json > deployment-state.json
   git -C /var/honua/gitops-repo log --oneline -10 > git-history.txt
   ```
3. Include:
   - Error messages from logs
   - Deployment state
   - Recent Git commits
   - Steps to reproduce

**Contact:**

- Operations Team: ops@example.com
- On-Call: oncall@example.com
- Security Issues: security@example.com

---

## Maintenance

### Regular Tasks

**Daily:**
- Review deployment logs
- Check alert status
- Verify reconciliation success

**Weekly:**
- Review deployment history
- Check disk usage (state files, Git repo)
- Validate backup integrity

**Monthly:**
- Rotate SSH keys (per security policy)
- Review and update documentation
- Test rollback procedures

**Quarterly:**
- Security audit
- Performance review
- Capacity planning
- Disaster recovery drill

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-10-23 | Initial operations documentation |

---

**Maintained by:** Operations Team
**Last Review:** 2025-10-23
**Next Review:** 2026-01-23
