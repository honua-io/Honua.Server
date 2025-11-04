#!/bin/bash

# Honua SBOM Verification Script
# Verifies SBOM attestations on container images
# Usage: ./scripts/verify-sbom.sh <image-reference>

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REGISTRY="${REGISTRY:-ghcr.io}"
IMAGE_NAME="${IMAGE_NAME:-honuaio/honua}"
VERSION="${1:-latest}"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo "========================================="
echo "Honua SBOM Verification"
echo "========================================="
echo ""

# Function to print colored output
print_status() {
    local status=$1
    local message=$2
    case $status in
        "success")
            echo -e "${GREEN}✓${NC} $message"
            ;;
        "error")
            echo -e "${RED}✗${NC} $message"
            ;;
        "warning")
            echo -e "${YELLOW}⚠${NC} $message"
            ;;
        "info")
            echo -e "${BLUE}ℹ${NC} $message"
            ;;
    esac
}

# Check prerequisites
check_prerequisites() {
    print_status "info" "Checking prerequisites..."

    local missing_tools=()

    if ! command -v cosign &> /dev/null; then
        missing_tools+=("cosign")
    fi

    if ! command -v docker &> /dev/null; then
        missing_tools+=("docker")
    fi

    if ! command -v jq &> /dev/null; then
        missing_tools+=("jq")
    fi

    if [ ${#missing_tools[@]} -gt 0 ]; then
        print_status "error" "Missing required tools: ${missing_tools[*]}"
        echo ""
        echo "Installation instructions:"
        echo "  cosign:  https://docs.sigstore.dev/cosign/installation/"
        echo "  docker:  https://docs.docker.com/get-docker/"
        echo "  jq:      sudo apt-get install jq (or brew install jq)"
        exit 1
    fi

    print_status "success" "All prerequisites installed"
    echo ""
}

# Construct image reference
construct_image_ref() {
    if [[ $VERSION == *"@sha256:"* ]]; then
        # Already a digest reference
        IMAGE_REF="$REGISTRY/$IMAGE_NAME@${VERSION#*@}"
    elif [[ $VERSION == sha256:* ]]; then
        # Digest without @ prefix
        IMAGE_REF="$REGISTRY/$IMAGE_NAME@$VERSION"
    else
        # Tag reference
        IMAGE_REF="$REGISTRY/$IMAGE_NAME:$VERSION"
    fi

    print_status "info" "Image: $IMAGE_REF"
    echo ""
}

# Verify image signature with Cosign
verify_signature() {
    print_status "info" "Verifying image signature..."

    if cosign verify "$IMAGE_REF" --certificate-identity-regexp='.*' --certificate-oidc-issuer-regexp='.*' 2>/dev/null; then
        print_status "success" "Image signature verified"
        return 0
    else
        # Try keyless verification
        if cosign verify "$IMAGE_REF" 2>/dev/null; then
            print_status "success" "Image signature verified (keyless)"
            return 0
        else
            print_status "warning" "Could not verify signature (may require authentication or public key)"
            return 1
        fi
    fi
}

# Verify SBOM attestation
verify_sbom_attestation() {
    print_status "info" "Verifying SBOM attestation..."

    local output_file="sbom-attestation.json"

    if cosign verify-attestation \
        --type https://spdx.dev/Document \
        "$IMAGE_REF" \
        --certificate-identity-regexp='.*' \
        --certificate-oidc-issuer-regexp='.*' \
        > "$output_file" 2>/dev/null; then
        print_status "success" "SBOM attestation verified"
        return 0
    else
        # Try without certificate constraints
        if cosign verify-attestation \
            --type https://spdx.dev/Document \
            "$IMAGE_REF" \
            > "$output_file" 2>/dev/null; then
            print_status "success" "SBOM attestation verified"
            return 0
        else
            print_status "warning" "Could not verify SBOM attestation"
            rm -f "$output_file"
            return 1
        fi
    fi
}

# Download SBOM
download_sbom() {
    print_status "info" "Downloading SBOM..."

    local output_file="sbom-downloaded.json"

    if cosign download sbom "$IMAGE_REF" > "$output_file" 2>/dev/null; then
        print_status "success" "SBOM downloaded successfully"

        # Validate JSON
        if jq empty "$output_file" 2>/dev/null; then
            print_status "success" "SBOM is valid JSON"
            return 0
        else
            print_status "error" "SBOM is not valid JSON"
            return 1
        fi
    else
        print_status "warning" "Could not download SBOM"
        return 1
    fi
}

# Analyze SBOM content
analyze_sbom() {
    local sbom_file="${1:-sbom-downloaded.json}"

    if [ ! -f "$sbom_file" ]; then
        print_status "warning" "SBOM file not found, skipping analysis"
        return 1
    fi

    print_status "info" "Analyzing SBOM content..."
    echo ""

    # Detect SBOM format
    local format="unknown"
    if jq -e '.spdxVersion' "$sbom_file" >/dev/null 2>&1; then
        format="SPDX"
    elif jq -e '.bomFormat' "$sbom_file" >/dev/null 2>&1; then
        format="CycloneDX"
    fi

    echo "SBOM Format: $format"

    # Extract statistics based on format
    if [ "$format" == "SPDX" ]; then
        local doc_name=$(jq -r '.name // "N/A"' "$sbom_file")
        local spdx_version=$(jq -r '.spdxVersion // "N/A"' "$sbom_file")
        local package_count=$(jq '.packages | length' "$sbom_file" 2>/dev/null || echo "0")
        local creation_time=$(jq -r '.creationInfo.created // "N/A"' "$sbom_file")

        echo "Document Name: $doc_name"
        echo "SPDX Version: $spdx_version"
        echo "Package Count: $package_count"
        echo "Created: $creation_time"

        # List top packages
        echo ""
        echo "Sample packages (first 10):"
        jq -r '.packages[0:10] | .[] | "  - \(.name) (\(.versionInfo // "no version"))"' "$sbom_file" 2>/dev/null || true

    elif [ "$format" == "CycloneDX" ]; then
        local spec_version=$(jq -r '.specVersion // "N/A"' "$sbom_file")
        local component_count=$(jq '.components | length' "$sbom_file" 2>/dev/null || echo "0")
        local metadata_component=$(jq -r '.metadata.component.name // "N/A"' "$sbom_file")

        echo "Spec Version: $spec_version"
        echo "Component Count: $component_count"
        echo "Main Component: $metadata_component"

        # List top components
        echo ""
        echo "Sample components (first 10):"
        jq -r '.components[0:10] | .[] | "  - \(.name) (\(.version // "no version"))"' "$sbom_file" 2>/dev/null || true
    fi

    echo ""
    print_status "success" "SBOM analysis complete"
    echo ""
}

# Check for vulnerabilities (optional - requires additional tools)
check_vulnerabilities() {
    local sbom_file="${1:-sbom-downloaded.json}"

    if [ ! -f "$sbom_file" ]; then
        return 0
    fi

    print_status "info" "Checking for vulnerability scanning tools..."

    # Check if grype is available
    if command -v grype &> /dev/null; then
        print_status "info" "Running Grype vulnerability scan on SBOM..."

        if grype "sbom:$sbom_file" --output table > grype-results.txt 2>&1; then
            print_status "success" "Vulnerability scan completed"

            # Show summary
            echo ""
            echo "Vulnerability Summary:"
            cat grype-results.txt | head -20
            echo ""
            echo "Full results saved to: grype-results.txt"
        else
            print_status "warning" "Vulnerability scan failed"
        fi
    else
        print_status "info" "Grype not installed (optional). Install from: https://github.com/anchore/grype"
    fi
}

# Verify provenance attestation
verify_provenance() {
    print_status "info" "Verifying provenance attestation..."

    local output_file="provenance-attestation.json"

    if cosign verify-attestation \
        --type slsaprovenance \
        "$IMAGE_REF" \
        --certificate-identity-regexp='.*' \
        --certificate-oidc-issuer-regexp='.*' \
        > "$output_file" 2>/dev/null; then
        print_status "success" "Provenance attestation verified"

        # Extract build info
        if [ -f "$output_file" ]; then
            echo ""
            echo "Build Information:"
            jq -r '.payload | @base64d | fromjson | .predicate |
                "  Builder: \(.builder.id // "N/A")\n  Invocation ID: \(.metadata.buildInvocationId // "N/A")"' \
                "$output_file" 2>/dev/null || echo "  Could not extract build info"
        fi
        return 0
    else
        print_status "warning" "Could not verify provenance attestation"
        rm -f "$output_file"
        return 1
    fi
}

# Generate verification report
generate_report() {
    local report_file="sbom-verification-report.txt"

    {
        echo "========================================="
        echo "SBOM Verification Report"
        echo "========================================="
        echo ""
        echo "Image: $IMAGE_REF"
        echo "Date: $(date -u +"%Y-%m-%d %H:%M:%S UTC")"
        echo "Host: $(hostname)"
        echo ""
        echo "Verification Results:"
        echo ""

        if [ -f sbom-downloaded.json ]; then
            echo "✓ SBOM downloaded successfully"
        else
            echo "✗ SBOM not available"
        fi

        if [ -f sbom-attestation.json ]; then
            echo "✓ SBOM attestation verified"
        else
            echo "○ SBOM attestation not verified"
        fi

        if [ -f provenance-attestation.json ]; then
            echo "✓ Provenance attestation verified"
        else
            echo "○ Provenance attestation not verified"
        fi

        echo ""
        echo "Files generated:"
        ls -lh *.json *.txt 2>/dev/null | awk '{print "  " $9 " (" $5 ")"}'

    } > "$report_file"

    print_status "success" "Verification report generated: $report_file"
    echo ""
    cat "$report_file"
}

# Main execution
main() {
    check_prerequisites
    construct_image_ref

    # Run verifications
    verify_signature || true
    echo ""

    verify_sbom_attestation || true
    echo ""

    download_sbom || true
    echo ""

    if [ -f sbom-downloaded.json ]; then
        analyze_sbom "sbom-downloaded.json"
    fi

    verify_provenance || true
    echo ""

    check_vulnerabilities "sbom-downloaded.json" || true
    echo ""

    generate_report

    echo ""
    echo "========================================="
    echo "Verification Complete"
    echo "========================================="
}

# Run main function
main "$@"
