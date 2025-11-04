# Container Image Security Scanning

This document describes the container image security scanning infrastructure implemented for the Honua project.

## Overview

Honua implements a comprehensive multi-tool security scanning strategy to identify vulnerabilities in production Docker images. The scanning infrastructure includes:

- **Trivy** - Primary vulnerability scanner (OS and library packages)
- **Grype** - Alternative vulnerability scanner for cross-validation
- **Docker Scout** - Docker-native security scanning
- **Configuration scanning** - Dockerfile and Kubernetes manifest analysis

## Scanning Strategy

### CI/CD Integration

Security scanning is integrated into two GitHub Actions workflows:

1. **`ci.yml`** - Runs on every push/PR to validate production images
2. **`container-security.yml`** - Comprehensive daily scans with multiple tools

### Scan Types

#### 1. OS Package Vulnerabilities
Scans base image and installed system packages for known CVEs.

**Tool**: Trivy, Grype, Docker Scout
**Frequency**: Every commit + daily
**Severity Filter**: CRITICAL, HIGH, MEDIUM, LOW

#### 2. Library/Application Vulnerabilities
Scans .NET runtime, libraries, and application dependencies.

**Tool**: Trivy, Grype
**Frequency**: Every commit + daily
**Severity Filter**: CRITICAL, HIGH, MEDIUM, LOW

#### 3. Configuration Issues
Analyzes Dockerfile for security misconfigurations and best practice violations.

**Tool**: Trivy config scanner
**Frequency**: Every commit + daily
**Examples**:
- Missing USER directive
- Running as root
- Exposed secrets
- Outdated base images

## CI Workflow (`ci.yml`)

### Docker Build Job

The `docker-build` job builds the production Docker image and performs Trivy scanning:

```yaml
jobs:
  docker-build:
    name: Build & Scan Docker Image
    runs-on: ubuntu-latest
    needs: build
    permissions:
      security-events: write
    steps:
      - Build production image
      - Run Trivy (SARIF format) - fails on HIGH/CRITICAL
      - Run Trivy (table format) - human-readable output
      - Run Trivy (JSON format) - machine-readable output
      - Upload to GitHub Security tab
      - Upload artifacts for review
```

### Build Failure Policy

The CI workflow **FAILS** the build if:
- Any **CRITICAL** severity vulnerabilities are detected
- Any **HIGH** severity vulnerabilities are detected

```yaml
exit-code: '1'  # Fail the build on HIGH/CRITICAL vulnerabilities
```

### Scan Outputs

Three output formats are generated:

1. **SARIF** - Uploaded to GitHub Security tab
2. **Table** - Human-readable text format
3. **JSON** - Machine-readable format for automation

## Comprehensive Security Workflow (`container-security.yml`)

### Scheduled Scanning

Runs daily at 2:00 AM UTC to detect newly disclosed vulnerabilities:

```yaml
on:
  schedule:
    - cron: '0 2 * * *'
```

### Multi-Tool Scanning

#### Job 1: Trivy Scan (Matrix Strategy)

Scans both OS and library vulnerabilities separately:

```yaml
strategy:
  matrix:
    scan-type: ['os', 'library']
```

- **OS scan**: System packages from base image
- **Library scan**: .NET runtime and application dependencies

#### Job 2: Trivy Config Scan

Analyzes Dockerfile and Kubernetes manifests for misconfigurations:

```yaml
scan-type: 'config'
scan-ref: '.'
```

#### Job 3: Grype Scan

Alternative vulnerability scanner using Anchore's database:

```yaml
- uses: anchore/scan-action@v3
  with:
    severity-cutoff: high
    fail-build: false
```

#### Job 4: Docker Scout

Docker-native vulnerability scanning:

```yaml
- uses: docker/scout-action@v1
  with:
    command: cves
    only-severities: critical,high
```

### Security Summary Job

Aggregates all scan results and generates a comprehensive summary:

- Downloads all scan artifacts
- Generates GitHub Step Summary with results
- Comments on PRs with security findings
- Provides download links for detailed reports

## Reviewing Scan Results

### GitHub Security Tab

All scan results are automatically uploaded to the **GitHub Security** tab:

1. Navigate to repository → **Security** → **Code scanning**
2. Filter by tool: `trivy-os`, `trivy-library`, `grype`, `docker-scout`
3. Review vulnerabilities by severity
4. Click on vulnerability for detailed information

### Workflow Artifacts

Detailed scan results are available as workflow artifacts:

1. Go to **Actions** → Select workflow run
2. Scroll to **Artifacts** section
3. Download:
   - `trivy-scan-results` - Main Trivy results
   - `trivy-os-scan-results` - OS vulnerabilities
   - `trivy-library-scan-results` - Library vulnerabilities
   - `grype-scan-results` - Grype results
   - `docker-scout-scan-results` - Docker Scout results

### Artifact Contents

Each artifact contains multiple formats:

```
trivy-os-scan-results/
├── trivy-os-results.sarif    # SARIF format (GitHub integration)
├── trivy-os-results.txt      # Table format (human-readable)
└── trivy-os-results.json     # JSON format (automation)
```

### Reading Trivy Table Output

```
honua:latest (debian 12.8)
==========================

Total: 47 (UNKNOWN: 0, LOW: 29, MEDIUM: 13, HIGH: 4, CRITICAL: 1)

┌────────────┬─────────────────┬──────────┬────────┬─────────────────┬───────────────┬──────────────┐
│  Library   │  Vulnerability  │ Severity │ Status │ Installed Ver.  │  Fixed Ver.   │    Title     │
├────────────┼─────────────────┼──────────┼────────┼─────────────────┼───────────────┼──────────────┤
│ libssl3    │ CVE-2024-12345  │ CRITICAL │ fixed  │ 3.0.11-1~deb12  │ 3.0.12-1~deb  │ OpenSSL...   │
│ libcrypto3 │ CVE-2024-12345  │ CRITICAL │ fixed  │ 3.0.11-1~deb12  │ 3.0.12-1~deb  │ OpenSSL...   │
└────────────┴─────────────────┴──────────┴────────┴─────────────────┴───────────────┴──────────────┘
```

**Fields**:
- **Library**: Affected package
- **Vulnerability**: CVE identifier
- **Severity**: CRITICAL, HIGH, MEDIUM, LOW
- **Status**: `fixed` (patch available), `will_not_fix`, `affected`
- **Installed Version**: Current version in image
- **Fixed Version**: Version with the fix
- **Title**: Short description

### Reading JSON Output

The JSON format provides detailed information for automation:

```json
{
  "Results": [
    {
      "Target": "honua:latest (debian 12.8)",
      "Type": "debian",
      "Vulnerabilities": [
        {
          "VulnerabilityID": "CVE-2024-12345",
          "PkgName": "libssl3",
          "InstalledVersion": "3.0.11-1~deb12u1",
          "FixedVersion": "3.0.12-1~deb12u1",
          "Severity": "CRITICAL",
          "Description": "OpenSSL vulnerability...",
          "References": [
            "https://cve.mitre.org/cgi-bin/cvename.cgi?name=CVE-2024-12345"
          ],
          "CVSS": {
            "nvd": {
              "V3Vector": "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H",
              "V3Score": 9.8
            }
          }
        }
      ]
    }
  ]
}
```

## Responding to Vulnerabilities

### Priority Levels

| Severity  | Response Time | Action Required |
|-----------|---------------|-----------------|
| CRITICAL  | Immediate     | Hotfix release within 24 hours |
| HIGH      | 1-3 days      | Include in next patch release |
| MEDIUM    | 1-2 weeks     | Include in next minor release |
| LOW       | Next sprint   | Review and prioritize |

### Remediation Process

#### 1. Verify the Vulnerability

- Check if the vulnerability affects your specific usage
- Review CVE details and CVSS score
- Determine if the vulnerable code path is reachable

#### 2. Update Dependencies

For OS package vulnerabilities:

```dockerfile
# Update Dockerfile base image
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled AS runtime
# Ensure you're using the latest patch version
```

For .NET library vulnerabilities:

```bash
# Update NuGet packages
dotnet list package --vulnerable --include-transitive
dotnet add package <PackageName> --version <FixedVersion>
```

#### 3. Rebuild and Re-scan

```bash
# Build new image
docker build -t honua:latest .

# Run local scan
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock \
  aquasec/trivy image --severity HIGH,CRITICAL honua:latest
```

#### 4. Document Exceptions

If a vulnerability cannot be fixed immediately, document it:

```yaml
# .trivyignore
# CVE-2024-12345: Affects libfoo but we don't use the vulnerable function
# Waiting for upstream fix - tracked in issue #123
CVE-2024-12345
```

### False Positives

If a vulnerability is a false positive:

1. Verify it's truly a false positive
2. Add to `.trivyignore` with detailed comment
3. Document in security tracking issue
4. Re-evaluate periodically

Example `.trivyignore`:

```
# CVE-2024-99999: False positive - affects Windows binaries only
# We're running on Linux (debian-chiseled base)
# Verified: https://github.com/package/issue/999
CVE-2024-99999
```

## Local Scanning

### Install Trivy

```bash
# macOS
brew install aquasecurity/trivy/trivy

# Linux
wget -qO - https://aquasecurity.github.io/trivy-repo/deb/public.key | sudo apt-key add -
echo "deb https://aquasecurity.github.io/trivy-repo/deb $(lsb_release -sc) main" | sudo tee -a /etc/apt/sources.list.d/trivy.list
sudo apt-get update
sudo apt-get install trivy
```

### Scan Local Image

```bash
# Build image
docker build -t honua:dev .

# Quick scan (HIGH/CRITICAL only)
trivy image --severity HIGH,CRITICAL honua:dev

# Full scan with all severities
trivy image honua:dev

# Generate JSON report
trivy image --format json --output results.json honua:dev

# Scan specific vulnerability types
trivy image --vuln-type os honua:dev
trivy image --vuln-type library honua:dev
```

### Scan Dockerfile

```bash
# Scan Dockerfile for misconfigurations
trivy config Dockerfile

# Scan all config files
trivy config .
```

## Best Practices

### 1. Use Minimal Base Images

The Dockerfile uses `mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled`:

- **Chiseled images** contain only runtime dependencies
- Reduces attack surface (no package manager, shell, etc.)
- Fewer vulnerabilities to scan

### 2. Multi-Stage Builds

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
# Build stage - larger image with build tools

FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled AS runtime
# Runtime stage - minimal image
```

### 3. Don't Run as Root

```dockerfile
USER app  # Non-root user
```

### 4. Regular Base Image Updates

Monitor for new base image versions:

```bash
# Check for updates
docker pull mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled

# Compare SHAs
docker inspect mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled | jq '.[0].Id'
```

### 5. Pin Base Image Versions

Use digest pinning for reproducibility:

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:9.0-noble-chiseled@sha256:abc123...
```

### 6. Minimize Installed Packages

Only install necessary tools:

```dockerfile
# ❌ Don't install unnecessary packages
RUN apt-get install curl wget netcat vim

# ✅ Only install what's needed
COPY --from=busybox:stable-glibc /bin/wget /bin/wget
```

## Vulnerability Databases

Trivy aggregates vulnerabilities from multiple sources:

- **NVD (National Vulnerability Database)** - NIST CVE database
- **Debian Security Tracker** - Debian-specific advisories
- **Red Hat Security Data** - RHEL/CentOS advisories
- **Ubuntu Security Notices** - Ubuntu-specific advisories
- **Alpine SecDB** - Alpine Linux security database
- **GitHub Advisory Database** - Language-specific advisories

Database updates are automatic and run daily in the scanner.

## Integration with GitHub Security

### Security Tab Features

1. **Code Scanning Alerts** - All Trivy/Grype/Scout findings
2. **Dependency Graph** - Visual dependency tree
3. **Dependabot Alerts** - .NET package vulnerabilities
4. **Secret Scanning** - Exposed credentials detection

### Alert Management

- **Dismiss alerts** with reason (false positive, risk accepted, etc.)
- **Create issues** directly from alerts
- **Track remediation** with linked PRs
- **Export data** via GraphQL API

## Metrics and Reporting

### Vulnerability Trends

Track vulnerability counts over time:

```bash
# Extract vulnerability counts from JSON
jq '.Results[].Vulnerabilities | group_by(.Severity) | map({severity: .[0].Severity, count: length})' trivy-results.json
```

### SLA Compliance

Monitor remediation times:

- **CRITICAL**: 0-24 hours
- **HIGH**: 1-3 days
- **MEDIUM**: 1-2 weeks
- **LOW**: Best effort

### Dashboard Metrics

Recommended metrics to track:

1. **Vulnerability count by severity** (time series)
2. **Mean time to remediate (MTTR)** by severity
3. **Percentage of vulnerabilities with available fixes**
4. **Image scan failure rate**
5. **False positive rate**

## Troubleshooting

### Scan Failures

**Error**: `failed to analyze image`

**Solution**: Ensure Docker daemon is running and image exists:

```bash
docker images | grep honua
docker pull honua:latest  # If missing
```

**Error**: `database error: failed to download vulnerability DB`

**Solution**: Trivy database update failed. Run manually:

```bash
trivy image --download-db-only
```

### High Scan Times

If scans take too long (>5 minutes):

1. Use layer caching: `--cache-dir /tmp/trivy-cache`
2. Scan specific vulnerability types: `--vuln-type os`
3. Skip database update: `--skip-db-update` (not recommended for production)

### Rate Limiting

GitHub Actions may hit rate limits with frequent scans.

**Solution**: Use workflow caching:

```yaml
- name: Cache Trivy DB
  uses: actions/cache@v4
  with:
    path: .trivy-cache
    key: trivy-db-${{ github.run_id }}
    restore-keys: trivy-db-
```

## References

- [Trivy Documentation](https://aquasecurity.github.io/trivy/)
- [Grype Documentation](https://github.com/anchore/grype)
- [Docker Scout Documentation](https://docs.docker.com/scout/)
- [GitHub Security Features](https://docs.github.com/en/code-security)
- [NIST National Vulnerability Database](https://nvd.nist.gov/)

## Support

For questions or issues with container security scanning:

1. Check [GitHub Security Tab](https://github.com/honua/honua.next/security)
2. Review [Workflow Runs](https://github.com/honua/honua.next/actions/workflows/container-security.yml)
3. Open an issue with the `security` label
4. Tag security team: `@honua/security`

---

**Last Updated**: 2025-10-18
**Maintained By**: Security Team
