# GitOps Best Practices

**Version:** 1.0
**Last Updated:** 2025-10-23

Operational best practices for running GitOps in production.

---

## Repository Organization

### Directory Structure

**Recommended Layout:**

```
honua-config/
├── environments/
│   ├── production/
│   │   ├── metadata.json
│   │   ├── datasources.json
│   │   └── README.md
│   ├── staging/
│   │   ├── metadata.json
│   │   ├── datasources.json
│   │   └── README.md
│   ├── development/
│   │   ├── metadata.json
│   │   ├── datasources.json
│   │   └── README.md
│   └── common/
│       └── shared-config.json
├── docs/
│   ├── architecture.md
│   ├── deployment-process.md
│   └── troubleshooting.md
├── tests/
│   ├── validate-metadata.sh
│   └── validate-datasources.sh
├── .gitops/
│   └── deployment-policy.yaml
├── .github/
│   └── workflows/
│       ├── validate.yml
│       └── security-scan.yml
├── CODEOWNERS
└── README.md
```

**Best Practices:**

1. **Separate by Environment:** Clear directory separation prevents accidental production changes
2. **Include Documentation:** README in each environment directory explains purpose
3. **Version Control Tests:** Validation scripts in repository ensure consistency
4. **Common Configuration:** Shared settings in `common/` reduce duplication

### File Naming Conventions

```
✓ metadata.json          # Standard names
✓ datasources.json
✗ metadata-v2.json       # Avoid version numbers
✗ config.json            # Too generic
```

---

## Configuration Management

### Version Control Practices

**Atomic Commits:**

```bash
# Good: Single logical change
git commit -m "feat: Add bike lanes layer to transportation service"

# Bad: Multiple unrelated changes
git commit -m "Update production config"
```

**Meaningful Commit Messages:**

Follow conventional commits format:

```
<type>(<scope>): <subject>

[optional body]

[optional footer]

Types:
  feat:     New feature/layer
  fix:      Bug fix in configuration
  refactor: Restructure without behavior change
  docs:     Documentation only
  chore:    Maintenance tasks
  security: Security-related changes

Examples:
  feat(production): Add real-time traffic layer
  fix(datasources): Correct PostgreSQL connection string
  security(credentials): Rotate database passwords
  refactor(metadata): Split large services into modules
```

### Configuration Validation

**Pre-commit Validation:**

```bash
#!/bin/bash
# .git/hooks/pre-commit

echo "Validating configuration files..."

# Validate JSON syntax
for file in $(git diff --cached --name-only | grep '\.json$'); do
  echo "Checking $file..."
  jq empty "$file" || {
    echo "Invalid JSON in $file"
    exit 1
  }
done

# Validate required fields in metadata.json
for file in environments/*/metadata.json; do
  # Check services array exists
  jq -e '.services' "$file" > /dev/null || {
    echo "Missing services array in $file"
    exit 1
  }

  # Check each service has required fields
  jq -e '.services[] | select(.id == null or .title == null)' "$file" && {
    echo "Service missing required fields in $file"
    exit 1
  }
done

echo "Validation passed!"
```

**CI/CD Validation:**

```yaml
# .github/workflows/validate.yml
name: Validate Configuration

on: [push, pull_request]

jobs:
  validate:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Validate JSON syntax
        run: |
          for file in $(find environments -name "*.json"); do
            jq empty "$file"
          done

      - name: Check for secrets
        run: |
          if grep -rE '(password|secret|key)": *"[^$]' environments/; then
            echo "Found hardcoded secrets!"
            exit 1
          fi

      - name: Validate schema
        run: |
          npm install -g ajv-cli
          ajv validate -s metadata-schema.json -d "environments/*/metadata.json"
```

### Change Testing Strategy

**Progressive Rollout:**

```
1. Development → Test immediately
2. Staging      → Test for 24 hours
3. Production   → Deploy after staging validation
```

**Testing Checklist:**

```bash
# After deploying to staging
./tests/validate-deployment.sh staging

# Checklist:
# ✓ All services accessible
# ✓ All layers return data
# ✓ Database connections working
# ✓ No errors in logs
# ✓ Performance acceptable
# ✓ Security scans pass
```

---

## Branch Strategy

### Recommended: Trunk-Based Development

**Single Branch for All Environments:**

```
main (production-ready)
  ↑
feature branches
```

**Workflow:**

```bash
# 1. Create feature branch
git checkout -b feature/add-transit-routes

# 2. Make changes to development environment
vim environments/development/metadata.json

# 3. Commit and push
git commit -m "feat: Add transit routes layer"
git push origin feature/add-transit-routes

# 4. Create Pull Request
# 5. After approval, merge to main

# 6. Changes deploy to development automatically
# 7. Promote to staging
cp environments/development/metadata.json environments/staging/metadata.json
git commit -m "chore: Promote transit routes to staging"
git push

# 8. After staging validation, promote to production
cp environments/staging/metadata.json environments/production/metadata.json
git commit -m "feat: Deploy transit routes to production"
git push
```

### Alternative: Environment Branches

**Separate Branch per Environment:**

```
production (protected)
  ↑
staging
  ↑
development
  ↑
feature branches
```

**Workflow:**

```bash
# Develop in feature branch
git checkout -b feature/new-layer

# Merge to development
git checkout development
git merge feature/new-layer
git push  # Auto-deploys to dev environment

# After testing, promote to staging
git checkout staging
git merge development
git push  # Auto-deploys to staging

# After validation, promote to production
git checkout production
git merge staging
git push  # Auto-deploys to production
```

---

## Rollback Strategies

### Quick Rollback via Git Revert

**Preferred Method:**

```bash
# Revert most recent commit
git revert HEAD
git push origin main

# Revert specific commit
git revert abc123
git push origin main

# Revert multiple commits
git revert abc123..def456
git push origin main
```

**Why Revert > Reset:**
- Maintains complete history
- No force push required
- Safe for collaborative environments
- Audit trail preserved

### Emergency Rollback

**If GitOps is failing:**

```bash
# 1. Stop GitWatcher
sudo systemctl stop honua

# 2. Manually revert to last known good state
cd /var/honua/gitops-repo
git reset --hard <last-good-commit>

# 3. Restart Honua
sudo systemctl start honua

# 4. Fix issue and push corrected config
git push --force origin main  # Use with extreme caution
```

### Rollback Testing

**Test rollback capability regularly:**

```bash
# Monthly drill
# 1. Deploy intentional breaking change to staging
# 2. Verify auto-rollback works
# 3. Document time to recovery
# 4. Update runbooks if needed
```

---

## Multi-Environment Management

### Environment Parity

**Keep environments as similar as possible:**

```
Development  → Lower resources, same structure
Staging      → Production-like resources
Production   → Full resources
```

**Differences Should Be:**
- Resource sizing (CPU, memory, storage)
- Connection strings (different databases)
- External service endpoints
- Monitoring/alerting thresholds

**Should NOT differ:**
- Application version
- Configuration structure
- Feature flags (except for testing)
- Security controls

### Configuration Sharing

**Use common configuration for shared settings:**

```json
// environments/common/shared-config.json
{
  "timeouts": {
    "database": 30,
    "http": 60
  },
  "limits": {
    "maxFeatures": 10000,
    "maxQueryTime": 300
  }
}
```

**Environment-specific overrides:**

```json
// environments/production/metadata.json
{
  "includes": ["common/shared-config.json"],
  "overrides": {
    "limits": {
      "maxFeatures": 50000
    }
  }
}
```

### Promotion Process

**Manual Promotion (Recommended for Production):**

```bash
#!/bin/bash
# scripts/promote-to-production.sh

# 1. Verify staging is healthy
if ! ./tests/validate-deployment.sh staging; then
  echo "Staging validation failed!"
  exit 1
fi

# 2. Create promotion branch
git checkout -b promote-to-prod-$(date +%Y%m%d)

# 3. Copy staging config to production
cp environments/staging/metadata.json environments/production/metadata.json
cp environments/staging/datasources.json environments/production/datasources.json

# 4. Create Pull Request for review
gh pr create \
  --title "Promote to Production $(date +%Y-%m-%d)" \
  --body "$(cat <<EOF
## Changes
$(git diff main -- environments/production/)

## Validation
- [x] Staging deployment successful
- [x] Integration tests passing
- [x] Security scan clean
- [ ] Production approval required

## Rollback Plan
Revert commit: git revert HEAD
EOF
)"
```

---

## Change Management Process

### Standard Change Workflow

```
1. Request       → Create feature branch
2. Development   → Implement changes
3. Code Review   → Pull Request review
4. Testing       → Deploy to staging
5. Approval      → Production deployment approval
6. Deployment    → Merge to main, auto-deploy
7. Verification  → Validate production deployment
8. Documentation → Update change log
```

### Pull Request Template

```markdown
## Description
Brief description of changes

## Type of Change
- [ ] New layer/service
- [ ] Configuration update
- [ ] Bug fix
- [ ] Security update
- [ ] Breaking change

## Environments Affected
- [ ] Development
- [ ] Staging
- [ ] Production

## Testing
- [ ] JSON validation passed
- [ ] Deployed to development
- [ ] Deployed to staging
- [ ] Integration tests passed
- [ ] Security scan clean

## Deployment Plan
1. Merge to main
2. Monitor development deployment
3. Wait 24 hours
4. Promote to staging
5. Validate staging
6. Promote to production

## Rollback Plan
git revert <commit-sha>

## Checklist
- [ ] Configuration validated
- [ ] No hardcoded secrets
- [ ] Documentation updated
- [ ] Stakeholders notified
```

### Emergency Change Process

**For critical production fixes:**

1. Create hotfix branch from main
2. Make minimal necessary change
3. Fast-track review (single approver)
4. Deploy directly to production
5. Backport to other environments
6. Document in post-mortem

```bash
# Hotfix workflow
git checkout -b hotfix/critical-datasource-fix
# Make fix
git commit -m "fix(production): Restore database connection"
git push origin hotfix/critical-datasource-fix

# Fast-track PR
gh pr create --label "emergency" --reviewer @oncall-engineer

# After merge, backport
git checkout staging
git cherry-pick <hotfix-commit>
```

---

## Monitoring and Observability

### Key Metrics

**Deployment Metrics:**

```
- Deployment frequency
- Lead time (commit to production)
- Change failure rate
- Mean time to recovery (MTTR)
```

**GitOps Metrics:**

```
- Reconciliation success rate
- Reconciliation duration
- Git pull latency
- Approval wait time
- Drift detection count
```

### Logging Best Practices

**Structured Logging:**

```json
{
  "timestamp": "2025-10-23T14:30:22Z",
  "level": "Information",
  "source": "GitWatcher",
  "event": "reconciliation_completed",
  "environment": "production",
  "commit": "abc123",
  "duration_ms": 1234,
  "changes": {
    "added": 1,
    "modified": 2,
    "removed": 0
  }
}
```

**What to Log:**

```
✓ Every reconciliation attempt
✓ Every deployment state transition
✓ Every approval/rejection
✓ Every configuration load
✓ Every error and exception

✗ Secrets or credentials
✗ PII or sensitive data
✗ Excessive debug information (production)
```

### Alerting Strategy

**Alert Levels:**

```yaml
Critical (Page immediately):
  - Reconciliation failing > 3 consecutive attempts
  - Auto-rollback failed
  - State file corruption
  - Git authentication failure

Warning (Notify during business hours):
  - Reconciliation slow (> 60s)
  - Deployment awaiting approval > 4 hours
  - High change frequency (> 10/hour)

Info (Log only):
  - Successful deployments
  - Normal reconciliation
  - Approval granted
```

---

## Documentation Standards

### Repository README

**Must Include:**

```markdown
# Honua Configuration Repository

## Purpose
GitOps configuration for Honua GIS services

## Environments
- **Production**: Main production services
- **Staging**: Pre-production testing
- **Development**: Development testing

## Making Changes
See [CONTRIBUTING.md](CONTRIBUTING.md)

## Structure
- `environments/`: Environment-specific configs
- `tests/`: Validation scripts
- `docs/`: Documentation

## Contacts
- Operations Team: ops@example.com
- On-Call: oncall@example.com
- Security: security@example.com
```

### Change Documentation

**Keep a CHANGELOG:**

```markdown
# Changelog

## [2025-10-23] Production
### Added
- Transit routes layer with real-time updates
- Bike lane network service

### Changed
- Updated parking lots datasource connection string

### Fixed
- Corrected geometry type for city boundaries

## [2025-10-20] Production
### Added
- Historical traffic data layer
```

---

## Summary

**Key Takeaways:**

1. **Treat Configuration as Code:** Version control, code review, testing
2. **Environment Parity:** Keep environments similar, differ only in resources
3. **Atomic Changes:** One logical change per commit
4. **Progressive Rollout:** Development → Staging → Production
5. **Always Revertible:** Every change must be easily rolled back
6. **Monitor Everything:** Deployments, performance, errors
7. **Document Thoroughly:** README, runbooks, change logs
8. **Test Rollbacks:** Practice failure scenarios regularly
9. **Security First:** No secrets in Git, audit all changes
10. **Automate Validation:** Pre-commit hooks, CI/CD checks

---

**Last Updated:** 2025-10-23
**Version:** 1.0
