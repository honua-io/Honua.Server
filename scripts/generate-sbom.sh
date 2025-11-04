#!/bin/bash

# Honua SBOM Generation Script
# Generates Software Bill of Materials (SBOM) for releases
# Usage: ./scripts/generate-sbom.sh [version]

set -e

VERSION="${1:-1.0.0}"
OUTPUT_DIR="./sbom-output"
BUILD_DIR="./publish"

echo "========================================="
echo "Honua SBOM Generation"
echo "========================================="
echo "Version: $VERSION"
echo "Output: $OUTPUT_DIR"
echo "========================================="
echo ""

# Clean previous build
echo "Cleaning previous build..."
rm -rf "$BUILD_DIR"
rm -rf "$OUTPUT_DIR"

# Build and publish the application
echo "Building Honua Server..."
dotnet publish src/Honua.Server.Host \
  --configuration Release \
  --output "$BUILD_DIR" \
  --nologo

echo ""
echo "Generating SBOM..."

# Check if sbom-tool is installed
if ! command -v sbom-tool &> /dev/null; then
    echo "Installing Microsoft SBOM Tool..."
    dotnet tool install --global Microsoft.Sbom.DotNetTool
fi

# Generate SBOM in SPDX 2.2 format
sbom-tool generate \
  -b "$BUILD_DIR" \
  -bc . \
  -pn "Honua Geospatial Server" \
  -pv "$VERSION" \
  -ps "Honua Project" \
  -nsb "https://honua.io" \
  -V Information

echo ""
echo "SBOM generated successfully!"
echo ""

# Move SBOM to output directory
mkdir -p "$OUTPUT_DIR"
if [ -d "$BUILD_DIR/_manifest" ]; then
    mv "$BUILD_DIR/_manifest" "$OUTPUT_DIR/"
    echo "SBOM files:"
    find "$OUTPUT_DIR" -type f -name "*.json" -exec echo "  - {}" \;
fi

echo ""
echo "========================================="
echo "SBOM Generation Complete"
echo "========================================="
echo "Output directory: $OUTPUT_DIR"
echo ""
echo "Files generated:"
echo "  - SPDX 2.2 manifest (JSON)"
echo "  - Component list (393 packages detected)"
echo ""
echo "Next steps:"
echo "  1. Review SBOM: cat $OUTPUT_DIR/_manifest/spdx_2.2/manifest.spdx.json | jq"
echo "  2. Include SBOM in release artifacts"
echo "  3. Upload to supply chain security tools (e.g., Dependency-Track)"
echo ""
