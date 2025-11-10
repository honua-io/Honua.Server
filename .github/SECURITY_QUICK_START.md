# Security Scanning Quick Start - Honua

## What's Running?

Security scanning is **already enabled and running automatically**. Three tools watch your code and dependencies:

| Tool | Checks For | When | Location |
|------|-----------|------|----------|
| **Dependabot** | Outdated/vulnerable packages | Mondays 09:00 UTC | Pull Requests |
| **CodeQL** | Security bugs in code | Every push + Mondays 06:00 UTC | Security tab |
| **Dependency Review** | Vulnerable deps in PRs | Every PR (instant) | PR checks |

## I'm Getting Security Alerts, What Do I Do?

### Dependabot Pull Request
```
1. Wait for the PR to be created (Mondays)
2. Click the PR link in GitHub
3. Read the changelog for the new version
4. Click "Run checks" to test it
5. Merge if tests pass
6. Done! The dependency is updated
```

### CodeQL Alert
```
1. Go to: Security → Code scanning alerts
2. Click the alert to see the vulnerability
3. Review the code context
4. Fix the issue in your code (e.g., use parameterized queries)
5. Push the fix
6. Alert auto-resolves when the scan runs
```

### Dependency Review in PR
- ✅ You'll see a comment on your PR listing any vulnerabilities
- ✅ If **not critical**: You can merge (just be aware)
- ❌ If **critical**: You must fix before merging
  - Update the vulnerable package
  - Re-run PR checks
  - Then merge

## Checking Status

### Check for Alerts
```bash
# Dependabot alerts
GitHub → Settings → Security & analysis → Dependabot alerts

# Code scanning
GitHub → Security → Code scanning alerts
```

### Download SBOMs (on Release)
```bash
# Go to: Releases → Assets
# Download: sbom-*.json files
```

## Running Locally (Optional)

### CodeQL Scan
```bash
# Install tools
npm install -g @github/codeql

# Scan your project
codeql database create /tmp/codeql-honua --language=csharp
codeql database analyze /tmp/codeql-honua --format=SARIF --output=/tmp/results.sarif
```

### Generate SBOM
```bash
# Install
dotnet tool install --global CycloneDX

# Generate
dotnet CycloneDX Honua.sln -o ./sbom -f honua-sbom.json -j

# View
cat ./sbom/honua-sbom.json | jq '.'
```

### Check Dependencies for Vulns (Grype)
```bash
# Install
curl -sSfL https://raw.githubusercontent.com/anchore/grype/main/install.sh | sh -s -- -b /usr/local/bin

# Scan your dependencies
grype .

# Scan SBOM
grype sbom:./sbom/honua-sbom.json
```

## Common Questions

**Q: Do I need to do anything?**
A: No! Everything runs automatically. Just respond to PRs and alerts.

**Q: Can I disable these?**
A: Not recommended. If needed, ask maintainers.

**Q: Why is CodeQL failing my build?**
A: Check the error in Actions tab. Usually it's a build issue, not CodeQL.

**Q: When will my PR get reviewed by Dependabot?**
A: Dependabot creates PRs on Mondays (09:00 UTC).

**Q: Can I override a vulnerable dep?**
A: Only for critical vulns in Dependency Review. Explain in the commit.

**Q: Where are the security results?**
A: GitHub → Security tab (Code scanning, Dependabot alerts, etc.)

## Key Files

| File | Purpose |
|------|---------|
| `.github/dependabot.yml` | Configures which packages to monitor |
| `.github/workflows/codeql.yml` | Runs CodeQL analysis |
| `.github/workflows/dependency-review.yml` | Blocks PRs with vulns |
| `.github/workflows/sbom.yml` | Generates SBOMs on release |
| `.github/codeql/codeql-config.yml` | CodeQL analysis rules |
| `SECURITY.md` | Security policies |

## Learn More

- **Full Guide**: `.github/SECURITY_SCANNING.md`
- **SBOM Details**: `.github/SBOM.md`
- **Security Policy**: `SECURITY.md`

## Need Help?

1. Check **SECURITY.md** for policies
2. Check **.github/SECURITY_SCANNING.md** for detailed info
3. Check **.github/SBOM.md** for SBOM questions
4. Open a GitHub Discussion for help
