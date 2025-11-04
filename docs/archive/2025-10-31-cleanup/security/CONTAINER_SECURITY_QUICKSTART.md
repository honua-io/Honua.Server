# Container Security Quick Start Guide

Quick reference for container security scanning in the Honua project.

## TL;DR

```bash
# Build image
docker build -t honua:local .

# Scan for vulnerabilities (HIGH/CRITICAL only)
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock \
  aquasec/trivy image --severity HIGH,CRITICAL honua:local

# View results in GitHub Security tab
# https://github.com/honua/honua.next/security/code-scanning
```

## CI/CD Integration

### Automatic Scans

Scans run automatically on:
- ✅ Every push to `master`, `main`, `dev`
- ✅ Every pull request
- ✅ Daily at 2:00 AM UTC (scheduled)
- ✅ Manual workflow dispatch

### Build Policy

The build **FAILS** if:
- Any **CRITICAL** vulnerabilities detected
- Any **HIGH** vulnerabilities detected

## Viewing Results

### GitHub Security Tab

1. Go to: **Repository** → **Security** → **Code scanning**
2. Filter by:
   - `trivy-os` - Operating system packages
   - `trivy-library` - .NET libraries
   - `trivy-config` - Configuration issues
   - `grype` - Alternative scanner
   - `docker-scout` - Docker Hub scanner

### Workflow Artifacts

1. Go to: **Actions** → **Container Security Scanning**
2. Select latest run
3. Download artifacts:
   - `trivy-scan-results` - Main results
   - `grype-scan-results` - Alternative scan
   - `docker-scout-scan-results` - Docker scan

## Local Scanning

### Install Trivy

```bash
# macOS
brew install trivy

# Linux (Debian/Ubuntu)
sudo apt-get install wget apt-transport-https gnupg lsb-release
wget -qO - https://aquasecurity.github.io/trivy-repo/deb/public.key | gpg --dearmor | sudo tee /usr/share/keyrings/trivy.gpg > /dev/null
echo "deb [signed-by=/usr/share/keyrings/trivy.gpg] https://aquasecurity.github.io/trivy-repo/deb $(lsb_release -sc) main" | sudo tee -a /etc/apt/sources.list.d/trivy.list
sudo apt-get update
sudo apt-get install trivy
```

### Scan Commands

```bash
# Quick scan (HIGH/CRITICAL only)
trivy image --severity HIGH,CRITICAL honua:local

# Full scan
trivy image honua:local

# Scan with JSON output
trivy image --format json --output results.json honua:local

# Scan only OS packages
trivy image --vuln-type os honua:local

# Scan only libraries
trivy image --vuln-type library honua:local

# Scan Dockerfile
trivy config Dockerfile

# Scan with table output
trivy image --format table honua:local
```

## Understanding Results

### Severity Levels

| Severity | CVSS Score | Response Time | Action |
|----------|------------|---------------|--------|
| CRITICAL | 9.0-10.0   | Immediate     | Hotfix within 24h |
| HIGH     | 7.0-8.9    | 1-3 days      | Next patch release |
| MEDIUM   | 4.0-6.9    | 1-2 weeks     | Next minor release |
| LOW      | 0.1-3.9    | Next sprint   | Prioritize and review |

### Sample Output

```
honua:latest (debian 12.8)
Total: 12 (CRITICAL: 0, HIGH: 2, MEDIUM: 5, LOW: 5)

┌──────────┬─────────────────┬──────────┬───────────────┬─────────────┐
│ Library  │ Vulnerability   │ Severity │ Installed Ver │ Fixed Ver   │
├──────────┼─────────────────┼──────────┼───────────────┼─────────────┤
│ libssl3  │ CVE-2024-5535   │ HIGH     │ 3.1.4-2       │ 3.1.5-1     │
└──────────┴─────────────────┴──────────┴───────────────┴─────────────┘
```

**Columns**:
- **Library**: Affected package
- **Vulnerability**: CVE ID
- **Severity**: Risk level
- **Installed Ver**: Current version
- **Fixed Ver**: Version with fix

## Common Tasks

### 1. Fix a Vulnerability

```bash
# Check current base image
grep "FROM" Dockerfile

# Update to latest patch
# Edit Dockerfile:
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled

# Rebuild
docker build -t honua:patched .

# Re-scan
trivy image --severity HIGH,CRITICAL honua:patched
```

### 2. Suppress False Positive

Create `.trivyignore`:

```
# CVE-2024-99999: False positive - Windows-only vulnerability
# We run on Linux debian-chiseled base
# Reference: https://github.com/package/issue/999
CVE-2024-99999
```

### 3. Generate Report for Security Review

```bash
# Generate comprehensive HTML report
trivy image --format json honua:local | \
  jq > trivy-report.json

# Generate markdown summary
trivy image --format table honua:local > security-report.txt
```

### 4. Compare Two Images

```bash
# Scan old version
trivy image --format json honua:v1.0 > old.json

# Scan new version
trivy image --format json honua:v2.0 > new.json

# Compare (requires jq)
diff <(jq -S '.Results[].Vulnerabilities' old.json) \
     <(jq -S '.Results[].Vulnerabilities' new.json)
```

## Troubleshooting

### Error: Database Download Failed

```bash
# Manually download database
trivy image --download-db-only

# Or clear cache and retry
rm -rf ~/.cache/trivy
trivy image honua:local
```

### Error: Image Not Found

```bash
# List local images
docker images | grep honua

# Pull if missing
docker pull honua:latest

# Build if needed
docker build -t honua:local .
```

### Scan Takes Too Long

```bash
# Use cached database
export TRIVY_CACHE_DIR=/tmp/trivy-cache
trivy image honua:local

# Skip database update (not recommended for production)
trivy image --skip-db-update honua:local
```

## Security Workflow

### On Vulnerability Discovery

1. **Assess Impact**
   - Check if vulnerability affects Honua's usage
   - Review CVSS score and attack vector
   - Determine priority based on severity

2. **Remediate**
   - Update base image or package
   - Rebuild and test
   - Deploy to staging
   - Validate fix with re-scan

3. **Document**
   - Update security tracking issue
   - Add entry to changelog
   - Notify security team

4. **Monitor**
   - Track remediation in GitHub Security tab
   - Verify fix in next scheduled scan
   - Update security metrics

## Best Practices

✅ **DO**:
- Run scans before every deployment
- Update base images regularly
- Document exceptions in `.trivyignore`
- Review scan results in PR reviews
- Track remediation time metrics

❌ **DON'T**:
- Ignore HIGH/CRITICAL vulnerabilities
- Skip scans to speed up CI
- Suppress vulnerabilities without justification
- Use outdated base images
- Disable security checks in production

## Resources

- 📘 [Full Documentation](../CONTAINER_SECURITY.md)
- 📊 [Sample Scan Output](../SAMPLE_TRIVY_SCAN_OUTPUT.md)
- 🔒 [GitHub Security Tab](https://github.com/honua/honua.next/security)
- 🔧 [Trivy Documentation](https://aquasecurity.github.io/trivy/)
- 📈 [Workflow Runs](https://github.com/honua/honua.next/actions/workflows/container-security.yml)

## Support

Questions? Issues?
- Check [GitHub Security Alerts](https://github.com/honua/honua.next/security/code-scanning)
- Review [Workflow Logs](https://github.com/honua/honua.next/actions)
- Open issue with `security` label
- Tag: `@honua/security`

---

**Last Updated**: 2025-10-18
