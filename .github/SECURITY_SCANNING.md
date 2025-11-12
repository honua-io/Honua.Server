# Automated Security Scanning Guide - Honua

This document provides a comprehensive overview of all automated security scanning tools and workflows integrated into the Honua project.

## Quick Start

Security scanning is already enabled and running automatically. Here's what's happening:

| Tool | Trigger | Frequency | Status |
|------|---------|-----------|--------|
| **Dependabot** | Weekly schedule | Every Monday 09:00 UTC | ✅ Enabled |
| **CodeQL** | Push, PR, schedule | Weekly Monday 06:00 UTC | ✅ Enabled |
| **Dependency Review** | Pull requests | Real-time on each PR | ✅ Enabled |
| **SBOM Generation** | Release, manual | On each release | ✅ Enabled |

## Security Tools Explained

### 1. GitHub Dependabot

**Purpose**: Automatically detect and update vulnerable dependencies

**What it monitors**:
- .NET packages (NuGet)
- Node.js packages (npm)
- Docker base images
- GitHub Actions

**How to use**:
1. Go to: **Repository → Settings → Security & analysis → Dependabot alerts**
2. View all detected vulnerabilities
3. Filter by severity, ecosystem, or status
4. Click each alert to see details and remediation options

**When it runs**:
- Scans every Monday at 09:00 UTC
- Creates pull requests for available updates
- Groups minor/patch updates together
- Limits to 10 open NuGet PRs, 5 for others

**Responding to Alerts**:
1. Review the vulnerability description
2. Check the PR for dependency updates
3. Run tests to ensure compatibility
4. Merge when ready, or close if not applicable
5. Mark as "Closed without merging" with reason if dismissing

**Configuration**: `.github/dependabot.yml`

---

### 2. CodeQL Static Analysis

**Purpose**: Find security vulnerabilities in source code using semantic analysis

**What it detects**:
- SQL injection vulnerabilities
- Path traversal bugs
- Unsafe use of reflection
- XSS (Cross-site Scripting)
- Hardcoded credentials
- Other security-relevant code patterns

**Language**: C# (primary), JavaScript (if analyzed)

**How to view results**:
1. Go to: **Repository → Security → Code scanning alerts**
2. View vulnerabilities sorted by severity
3. Click each alert to see code context
4. Review the SARIF file for detailed analysis

**When it runs**:
- Every push to: `main`, `master`, `develop`
- Pull requests to: `main`, `master`
- Weekly schedule: Monday 06:00 UTC
- Manual trigger: Workflow dispatch

**What's analyzed**:
- Paths: `src/`, `tools/`
- Excludes: `tests/`, `benchmarks/`, `Dependencies/`, `node_modules/`, `obj/`, `bin/`

**Addressing Findings**:
1. Review the vulnerability alert
2. Check the code context provided
3. Fix the issue in your code
4. Verify the alert auto-resolves on next push
5. Or manually dismiss if it's a false positive

**Configuration**: `.github/codeql/codeql-config.yml`

**Workflow**: `.github/workflows/codeql.yml`

---

### 3. Dependency Review

**Purpose**: Block pull requests that introduce vulnerable dependencies

**What it does**:
- Scans all dependency changes in PRs
- Checks against CVE databases
- Comments on PRs with vulnerability details
- Fails checks for critical vulnerabilities
- Alerts on license issues

**Severity Handling**:
- **Critical**: PR check fails (cannot merge without override)
- **High**: Warning in PR comments
- **Medium/Low**: Informational comments

**How it helps**:
1. Prevents vulnerable code from being merged
2. Educates developers on vulnerabilities
3. Provides remediation guidance
4. Works seamlessly without configuration

**When it runs**:
- Automatically on all pull requests
- Runs before code review completion
- Blocking check (critical vulns)

**Configuration**: `.github/workflows/dependency-review.yml`

---

### 4. SBOM Generation & Attestation

**Purpose**: Generate and verify Software Bill of Materials

**What it creates**:
- **Application SBOMs**: All dependencies of your code
  - CycloneDX format (JSON)
  - SPDX format (JSON)
- **Container SBOMs**: All runtime components
  - Runtime dependencies
  - Base image contents
  - Multi-format (SPDX, CycloneDX, Syft)

**Why it matters**:
- Regulatory compliance (EO 14028, SSRF)
- Supply chain security
- License tracking
- Vulnerability auditing
- Transparency and reproducibility

**When it runs**:
- Push to `master`, `dev` branches
- Release published
- Manual trigger: Workflow dispatch

**Accessing SBOMs**:
1. From releases: **Releases → Assets** (download JSON files)
2. From actions: **Actions → SBOM Generation → Artifacts**
3. From container registry: `cosign download sbom [image]`

**Using SBOMs**:
- Upload to SCA tools (FOSSA, Black Duck, WhiteSource)
- Scan with Grype for vulnerabilities
- Analyze with SBOM tools
- Include in compliance audits
- Check with license scanners

**Validation**:
- All artifacts are signed with Cosign
- Verify with: `cosign verify [image]`
- Check SBOM attestation: `cosign verify-attestation [image]`

**Configuration**: `.github/workflows/sbom.yml`

---

## Security Scanning Coverage

### Code Analysis
- ✅ Static code analysis (CodeQL)
- ✅ Dependency vulnerability scanning
- ✅ Pull request blocking on critical vulns
- ❌ Dynamic analysis (DAST) - Not currently enabled
- ❌ Container scanning (Trivy) - Can be added
- ❌ Secret scanning - Existing workflows disabled

### Dependency Management
- ✅ NuGet package monitoring (Dependabot)
- ✅ npm package monitoring (Dependabot)
- ✅ Docker image monitoring (Dependabot)
- ✅ GitHub Actions monitoring (Dependabot)
- ✅ Dependency vulnerability review on PRs
- ✅ SBOM generation for dependencies

### Supply Chain Security
- ✅ SBOM generation (CycloneDX, SPDX)
- ✅ Container image signing (Cosign)
- ✅ Provenance attestation (SLSA)
- ✅ Artifact integrity verification

### Compliance & Transparency
- ✅ Dependency tracking (Dependabot)
- ✅ License tracking (SBOM)
- ✅ Vulnerability disclosure (GitHub)
- ✅ Security policy (SECURITY.md)

---

## Understanding Security Alerts

### Severity Levels

**Critical** 🔴
- Actively exploited vulnerabilities
- Remote code execution potential
- Authentication bypass
- Action: Fix immediately, deploy patch
- Blocks: PR merges, deployment

**High** 🟠
- Significant security impact
- Privilege escalation
- Data exposure
- Action: Fix within week
- Blocks: Depends on policy

**Medium** 🟡
- Moderate security impact
- Requires specific conditions
- Limited scope
- Action: Plan for fix within month

**Low** 🟢
- Minor security impact
- Difficult to exploit
- Minimal damage potential
- Action: Include in regular updates

### Alert Types

**Dependabot**:
- Security vulnerability in package
- Version updates available
- May include compatible upgrades

**CodeQL**:
- Code pattern issues
- Potential exploits
- Best practice violations

**Dependency Review**:
- Vulnerable version introduced in PR
- License mismatch
- New vulnerable transitive dep

**SBOM**:
- Complete component inventory
- Supply chain transparency
- Compliance documentation

---

## Workflows & Configuration

### Dependabot Configuration
**File**: `.github/dependabot.yml`

```yaml
# Monitors on weekly schedule
updates:
  - package-ecosystem: "nuget"     # .NET packages
  - package-ecosystem: "npm"       # JavaScript packages (3 directories)
  - package-ecosystem: "docker"    # Container images
  - package-ecosystem: "github-actions"  # CI/CD actions
```

**Features**:
- Weekly Monday scans at 09:00 UTC
- Groups minor/patch updates
- Sensible PR limits to prevent spam
- Commit message prefixes for organization
- Reviewer assignments

### CodeQL Workflow
**File**: `.github/workflows/codeql.yml`

**Triggers**:
- Push to: main, master, develop (ignores docs)
- PRs to: main, master (ignores docs)
- Weekly: Monday 06:00 UTC
- Manual: Workflow dispatch

**Configuration**:
- Language: C#
- .NET version: 9.0.x
- Builds with Release configuration
- Caches NuGet packages for speed
- Uses config file for query selection

### Dependency Review Workflow
**File**: `.github/workflows/dependency-review.yml`

**Triggers**:
- All pull requests
- Manual workflow dispatch

**Configuration**:
- Fails on critical severity
- Comments vulnerability details
- Includes license checking capability
- Fast execution (no build needed)

### SBOM Workflow
**File**: `.github/workflows/sbom.yml`

**Triggers**:
- Push to: master, dev
- Releases published
- Manual workflow dispatch

**Generates**:
- Application SBOMs (CycloneDX, SPDX)
- Container SBOMs (via Syft)
- Signed attestations (via Cosign)
- Multi-platform images (amd64, arm64)

---

## Enabling/Disabling Workflows

### Current Status
All security workflows are **enabled** (no `.disabled` extension).

### To Disable a Workflow
1. Rename file to add `.disabled`: `name.yml.disabled`
2. GitHub will not schedule or trigger it
3. Can still be enabled in Actions tab

### To Enable a Disabled Workflow
1. Rename file to remove `.disabled`
2. Commit and push
3. Workflow will be active next trigger

### Temporary Pause
```bash
# In workflow YAML, comment out triggers:
# on:
#   push:
#     branches: [ "main" ]
```

---

## Handling Security Findings

### High Priority (Critical/High Vulns)

```
1. Create branch: git checkout -b security/fix-cve-2024-xxxx
2. Update dependency: dotnet add package Vulnerable --version X.Y.Z
3. Test thoroughly: dotnet test
4. Create PR with description of fix
5. Link to security advisory if available
6. Merge after review
7. Deploy patch release
```

### False Positives

If CodeQL finds a false positive:
1. Go to: Security → Code scanning alerts
2. Click the alert
3. Select "Dismiss"
4. Choose reason: "Won't fix" or "False positive"
5. Add comment explaining why
6. Click "Dismiss alert"

### License Issues

If Dependabot/SBOM reports license concerns:
1. Review the package license in PR
2. Check if compatible with your license
3. Document decision if accepting non-compatible
4. Add to license exceptions if needed
5. Reference license decision in commit

---

## Integration & Automation

### GitHub Status Checks
Security tools block merge if:
- CodeQL finds critical issues (can override)
- Dependency Review finds critical vulns (can override)
- Other branch protection rules violated

### Notifications
- Email alerts for new Dependabot findings
- Slack integration (if configured)
- GitHub notifications (default)

### CI/CD Pipeline
- CodeQL integrates with branch protection
- Dependency Review part of required checks
- SBOM generated as part of release workflow

---

## Best Practices

1. **Review Dependabot PRs Promptly**
   - Don't let them pile up
   - Group updates together for testing
   - Merge to keep dependencies current

2. **Investigate CodeQL Alerts**
   - Don't dismiss without understanding
   - Fix real issues promptly
   - Document false positives properly

3. **Use SBOMs in Your Process**
   - Attach to releases
   - Share with customers
   - Include in security audits
   - Upload to SCA tools

4. **Keep Security Policies Updated**
   - Review SECURITY.md regularly
   - Update supported versions
   - Document new security features
   - Link to security advisories

5. **Monitor Advisories**
   - Check NVD (National Vulnerability Database)
   - Subscribe to security mailing lists
   - Review GitHub Security alerts
   - Follow upstream project advisories

---

## Troubleshooting

### CodeQL Workflow Failing
```bash
# Check logs in Actions tab
# Common issues:
# 1. Build failure - run `dotnet build` locally
# 2. Missing dependencies - run `dotnet restore`
# 3. .NET version mismatch - check dotnet-version
# 4. Timeout - may need to increase timeout or optimize build
```

### Dependabot Not Creating PRs
1. Check workflow is enabled (no `.disabled` extension)
2. Verify configuration in `.github/dependabot.yml`
3. Check for updates: Settings → Security & analysis
4. Look for errors in Dependabot logs

### SBOM Generation Timeout
1. SBOMs can be slow for large projects
2. Check Actions → SBOM Generation for logs
3. Consider running manually if delays
4. May need to increase timeout in workflow

### Cosign Verification Failing
1. Ensure OIDC token is available (GitHub Actions)
2. Try without OIDC for local testing
3. Check cosign version compatibility
4. Verify image is signed

---

## Next Steps

1. ✅ Workflows are configured and enabled
2. ✅ Dependabot monitoring is active
3. ✅ CodeQL scans are running
4. ✅ SBOMs are generated on releases
5. 📋 Consider adding:
   - Secret scanning workflow
   - Container image scanning (Trivy)
   - License compliance scanning
   - DAST (Dynamic Application Security Testing)

---

## Related Documentation

- **SECURITY.md**: Main security policy
- **.github/SBOM.md**: Detailed SBOM guide
- **docs/architecture/security.md**: Security architecture
- **Dependabot config**: `.github/dependabot.yml`
- **CodeQL config**: `.github/codeql/codeql-config.yml`

## Resources

- [GitHub Security Documentation](https://docs.github.com/en/code-security)
- [CodeQL Documentation](https://codeql.github.com/docs/)
- [GitHub Dependabot Docs](https://docs.github.com/en/code-security/dependabot)
- [SBOM Specifications](https://sbom.github.io/)
- [Cosign Documentation](https://docs.sigstore.dev/)
