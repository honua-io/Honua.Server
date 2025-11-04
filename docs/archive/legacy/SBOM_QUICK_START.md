# SBOM Quick Start Guide

Quick reference for working with Honua SBOMs.

## Download SBOM

```bash
# Latest version
cosign download sbom ghcr.io/honuaio/honua:latest > sbom.json

# Specific version
cosign download sbom ghcr.io/honuaio/honua:v1.2.3 > sbom.json

# By digest
cosign download sbom ghcr.io/honuaio/honua@sha256:abc123... > sbom.json
```

## Verify SBOM

```bash
# Quick verification
./scripts/verify-sbom.sh latest

# Manual verification
cosign verify ghcr.io/honuaio/honua:latest
cosign verify-attestation --type https://spdx.dev/Document ghcr.io/honuaio/honua:latest
```

## Scan for Vulnerabilities

```bash
# Using Grype
grype sbom:sbom.json

# Using Trivy
trivy sbom sbom.json

# Fail on high severity
grype sbom:sbom.json --fail-on high
```

## Common Queries

```bash
# Package count
jq '.packages | length' sbom.json

# Find package
jq '.packages[] | select(.name | contains("System.Text.Json"))' sbom.json

# List licenses
jq -r '.packages[].licenseConcluded' sbom.json | sort | uniq

# Find version
jq -r '.packages[] | select(.name=="Honua.Server.Core") | .versionInfo' sbom.json
```

## Pre-deployment Check

```bash
#!/bin/bash
IMAGE="ghcr.io/honuaio/honua:$VERSION"

# Verify
cosign verify "$IMAGE" || exit 1

# Download SBOM
cosign download sbom "$IMAGE" > sbom.json

# Scan
grype sbom:sbom.json --fail-on high || exit 1

# Deploy
kubectl apply -f deployment.yaml
```

## Troubleshooting

### Authentication Required
```bash
docker login ghcr.io
```

### Signature Verification Fails
```bash
# Use keyless verification
cosign verify ghcr.io/honuaio/honua:latest \
  --certificate-identity-regexp='.*' \
  --certificate-oidc-issuer-regexp='.*'
```

### SBOM Not Found
```bash
# Check attestations
cosign tree ghcr.io/honuaio/honua:latest

# Use digest instead of tag
docker inspect ghcr.io/honuaio/honua:latest | jq -r '.[0].RepoDigests[0]'
```

## Installation

### Cosign
```bash
curl -O -L "https://github.com/sigstore/cosign/releases/latest/download/cosign-linux-amd64"
sudo mv cosign-linux-amd64 /usr/local/bin/cosign
sudo chmod +x /usr/local/bin/cosign
```

### Grype
```bash
curl -sSfL https://raw.githubusercontent.com/anchore/grype/main/install.sh | sh -s -- -b /usr/local/bin
```

### Trivy
```bash
# Ubuntu/Debian
wget -qO - https://aquasecurity.github.io/trivy-repo/deb/public.key | sudo apt-key add -
echo "deb https://aquasecurity.github.io/trivy-repo/deb $(lsb_release -sc) main" | sudo tee -a /etc/apt/sources.list.d/trivy.list
sudo apt-get update && sudo apt-get install trivy
```

## CI/CD Integration

### GitHub Actions
```yaml
- name: Verify SBOM
  run: |
    cosign verify ${{ env.IMAGE }}
    cosign download sbom ${{ env.IMAGE }} > sbom.json
    grype sbom:sbom.json --fail-on high
```

### GitLab CI
```yaml
verify-sbom:
  script:
    - cosign verify $CI_REGISTRY_IMAGE:$CI_COMMIT_TAG
    - cosign download sbom $CI_REGISTRY_IMAGE:$CI_COMMIT_TAG > sbom.json
    - grype sbom:sbom.json --fail-on high
```

## See Also

- [Full SBOM Guide](SBOM_GUIDE.md) - Comprehensive documentation
- [Verification Script](../scripts/verify-sbom.sh) - Automated verification
- [SBOM Workflow](../.github/workflows/sbom.yml) - CI/CD pipeline
