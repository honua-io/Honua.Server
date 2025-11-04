#!/bin/bash
# Package CloudWatch Synthetics canaries into zip files

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OUTPUT_DIR="${SCRIPT_DIR}/../canary-packages"

echo "Creating output directory: ${OUTPUT_DIR}"
mkdir -p "${OUTPUT_DIR}"

# Package each canary
for canary in liveness readiness ogc-api ogc-conformance; do
  echo "Packaging ${canary}-canary.js..."

  # Create temp directory
  TEMP_DIR=$(mktemp -d)

  # Copy canary script
  cp "${SCRIPT_DIR}/${canary}-canary.js" "${TEMP_DIR}/nodejs/node_modules/${canary}-canary.js"

  # Create zip
  cd "${TEMP_DIR}"
  zip -r "${OUTPUT_DIR}/${canary}-canary.zip" nodejs/

  # Cleanup
  rm -rf "${TEMP_DIR}"

  echo "Created ${OUTPUT_DIR}/${canary}-canary.zip"
done

echo ""
echo "All canaries packaged successfully!"
echo "Upload these files to S3 or use them with Terraform."
