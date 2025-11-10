# Software Bill of Materials (SBOM) - Honua

This document describes the Software Bill of Materials (SBOM) generation and usage for the Honua Geospatial Server project.

## Overview

A Software Bill of Materials (SBOM) is a complete list of all software components, libraries, and dependencies included in a product. SBOMs are essential for:

- **Supply Chain Security**: Track all components to identify vulnerabilities
- **Compliance**: Meet regulatory requirements (Executive Order 14028, SSRF Act)
- **License Management**: Ensure license compliance across all dependencies
- **Transparency**: Know exactly what's in your software
- **Vulnerability Response**: Quickly determine if you're affected by a CVE

## SBOM Formats

Honua generates SBOMs in multiple industry-standard formats:

### CycloneDX
- **Format**: JSON/XML
- **Use**: Dependency tracking, component analysis
- **Tools**: Dependabot, OWASP, many SCA tools
- **File**: `sbom-cyclonedx.json`

### SPDX
- **Format**: JSON (also available: JSON-LD, RDF, YAML, XML, tagvalue)
- **Use**: Regulatory compliance, license tracking
- **Tools**: Open source standard, widely supported
- **File**: `sbom-spdx.json`

### Syft JSON
- **Format**: JSON
- **Use**: Container image analysis, detailed inventory
- **Tools**: Anchore Syft, Grype vulnerability scanner
- **File**: `container-sbom-syft.json`

## SBOM Generation

### Automatic Generation (CI/CD)

SBOMs are automatically generated when:

1. **Push to release branches**: `master`, `dev`
2. **Release published**: Creating a GitHub release
3. **Manual trigger**: Workflow dispatch via GitHub UI

**Workflow**: `.github/workflows/sbom.yml`

#### For Application Dependencies

```bash
# CycloneDX format
dotnet CycloneDX Honua.sln -o ./sbom -f honua-cyclonedx.json -j

# SPDX format (via Microsoft SBOM tool)
sbom-tool generate -b ./sbom/spdx -bc . -pn "Honua" -ps "Honua Project"
```

#### For Container Images

```bash
# Generate container SBOM using Syft
syft "$IMAGE_REF" -o spdx-json=./sbom/container-sbom-spdx.json
syft "$IMAGE_REF" -o cyclonedx-json=./sbom/container-sbom-cyclonedx.json
syft "$IMAGE_REF" -o syft-json=./sbom/container-sbom-syft.json
```

### Manual Generation (Local)

#### Prerequisites

```bash
# .NET tool for CycloneDX
dotnet tool install --global CycloneDX

# Microsoft SBOM tool
dotnet tool install --global Microsoft.Sbom.DotNetTool

# Syft for container analysis
curl -sSfL https://raw.githubusercontent.com/anchore/syft/main/install.sh | sh -s -- -b /usr/local/bin

# Cosign for signing (optional)
curl -LO https://github.com/sigstore/cosign/releases/latest/download/cosign-linux-amd64
sudo mv cosign-linux-amd64 /usr/local/bin/cosign
chmod +x /usr/local/bin/cosign
```

#### Generate Application SBOM

```bash
cd /path/to/Honua

# Restore dependencies first
dotnet restore

# Generate CycloneDX SBOM
mkdir -p ./sbom
dotnet CycloneDX Honua.sln -o ./sbom -f honua-cyclonedx.json -j

# Generate SPDX SBOM
sbom-tool generate \
  -b ./sbom/spdx \
  -bc . \
  -pn "Honua Geospatial Server" \
  -pv "1.0.0" \
  -ps "Honua Project" \
  -nsb https://honua.io

# View generated SBOMs
ls -lah ./sbom/
```

#### Generate Container SBOM

```bash
# Build and tag the image
docker build -t honua:latest .

# Generate SBOM for the container
syft honua:latest -o spdx-json=sbom-container-spdx.json
syft honua:latest -o cyclonedx-json=sbom-container-cyclonedx.json
syft honua:latest -o syft-json=sbom-container-syft.json

# With registry image
syft ghcr.io/honua-io/honua:latest -o spdx-json=sbom-container-spdx.json
```

## Using SBOMs

### View SBOM Contents

```bash
# Pretty print SBOM
jq '.' sbom-cyclonedx.json | less

# Count dependencies
jq '.components | length' sbom-cyclonedx.json

# List all packages
jq '.components[] | .name' sbom-cyclonedx.json

# Find specific package
jq '.components[] | select(.name | contains("Newtonsoft"))' sbom-cyclonedx.json
```

### Scan for Vulnerabilities

Using Grype (by Anchore):

```bash
# Install Grype
curl -sSfL https://raw.githubusercontent.com/anchore/grype/main/install.sh | sh -s -- -b /usr/local/bin

# Scan SBOM
grype sbom:sbom-spdx.json

# Generate report
grype sbom:sbom-spdx.json -o json=vulnerability-report.json
```

### Check License Compliance

```bash
# Extract licenses from SBOM
jq '.components[] | {name, license: .licenses[0].license.name}' sbom-spdx.json

# Find GPL dependencies (check for license restrictions)
jq '.components[] | select(.licenses[0].license.name | contains("GPL"))' sbom-spdx.json

# Use SBOM tools
scancode --license sbom-spdx.json
```

### Upload to Software Composition Analysis (SCA) Tools

Popular tools that accept SBOMs:

1. **Dependabot** (GitHub native)
   - Upload to Security → Supply chain → SBOM

2. **FOSSA**
   - `fossa analyze --include-all --format json`

3. **Black Duck/Synopsys**
   - Supports CycloneDX and SPDX

4. **WhiteSource**
   - REST API for SBOM upload

5. **OpenLogic**
   - SPDX format support

### Compliance & Audits

```bash
# Generate compliance report
jq '{
  "total_components": (.components | length),
  "components": [
    .components[] | {
      name,
      version,
      licenses: .licenses[0].license.name,
      purl
    }
  ]
}' sbom-spdx.json > compliance-report.json
```

## SBOM Attestation

SBOMs can be signed and attached as attestations to container images:

### Sign with Cosign

```bash
# Sign container image
cosign sign --yes $IMAGE_REF

# Attach SBOM as attestation
cosign attest --yes \
  --predicate ./sbom-spdx.json \
  --type https://spdx.dev/Document \
  $IMAGE_REF

# Verify signature
cosign verify $IMAGE_REF

# Verify and download SBOM attestation
cosign verify-attestation \
  --type https://spdx.dev/Document \
  $IMAGE_REF | jq '.payload | @base64d | fromjson'
```

## Accessing Generated SBOMs

### From GitHub Releases

1. Go to: Releases page
2. Find the release
3. Expand "Assets"
4. Download:
   - `sbom-cyclonedx.json`
   - `sbom-spdx.json`
   - `container-sbom-spdx.json`
   - `container-sbom-cyclonedx.json`
   - `container-sbom-syft.json`

### From GitHub Actions

1. Go to: Actions → SBOM Generation & Attestation
2. Click the latest run
3. Download artifacts:
   - `sbom-{commit-sha}`

### From Container Registry

```bash
# Download SBOM from container attestation
cosign download sbom ghcr.io/honua-io/honua:latest > sbom.json

# Download attestation
cosign download attestation ghcr.io/honua-io/honua:latest > attestation.json
```

## Workflow Details

The SBOM generation workflow (`.github/workflows/sbom.yml`) performs:

### 1. Application Analysis
- Restores all NuGet dependencies
- Generates CycloneDX SBOM from project files
- Generates SPDX SBOM using Microsoft tool

### 2. Container Analysis
- Builds Docker image with BuildKit
- Generates container SBOM with Syft (SPDX, CycloneDX, Syft formats)
- Includes both application and runtime dependencies

### 3. Artifact Management
- Uploads SBOMs as GitHub Actions artifacts (90-day retention)
- Attaches SBOMs to releases (if release event)

### 4. Integrity & Attestation
- Signs images with Cosign using GitHub OIDC
- Attaches SBOM as provenance attestation
- Generates SLSA provenance for build traceability

### 5. Verification
- Separate job to verify attestations
- Downloads and validates SBOM signatures
- Provides CLI commands for manual verification

## Best Practices

### Regular Updates
- Review SBOMs with each release
- Track dependency updates from Dependabot
- Monitor vulnerability databases (NVD, OSV)

### Integration
- Integrate SBOM generation into your build pipeline
- Attach SBOMs to all release artifacts
- Store SBOMs in source control or artifact repository

### Automation
- Use Dependabot for continuous updates
- Use Grype or similar tools for automated vulnerability scanning
- Integrate with SCA platforms for policy enforcement

### Compliance
- Maintain SBOM records for audit periods
- Use SBOMs for export compliance (EAR, ITAR)
- Document any excluded components

### Documentation
- Document why components are used
- Track license obligations
- Record security decisions and exemptions

## Troubleshooting

### CycloneDX Tool Issues

```bash
# Update the tool
dotnet tool update --global CycloneDX

# Check supported formats
dotnet CycloneDX --help

# Specify .NET version if needed
dotnet CycloneDX Honua.sln --use-repo-metadata
```

### Syft Issues

```bash
# Check version
syft --version

# Enable debug output
syft honua:latest -vv -o spdx-json

# Check for OS-specific issues
syft honua:latest -o json | jq '.' | head -50
```

### Cosign Signing Issues

```bash
# Verify OIDC token availability (in GitHub Actions)
echo "$ACTIONS_ID_TOKEN_REQUEST_URL"
echo "$ACTIONS_ID_TOKEN_REQUEST_TOKEN"

# Test cosign locally
cosign version

# Sign with local key (for testing)
cosign sign-blob --key cosign.key file.txt
```

## Related Documentation

- **SECURITY.md**: Security policies and vulnerability reporting
- **.github/workflows/sbom.yml**: SBOM generation workflow
- **docs/architecture/security.md**: Security architecture details

## References

- [CycloneDX Specification](https://cyclonedx.org/)
- [SPDX Specification](https://spdx.dev/)
- [Syft GitHub](https://github.com/anchore/syft)
- [Cosign Documentation](https://docs.sigstore.dev/cosign/overview/)
- [Executive Order 14028 - Improving Cybersecurity](https://www.whitehouse.gov/briefing-room/presidential-actions/2021/05/12/executive-order-on-improving-the-nations-cybersecurity/)
