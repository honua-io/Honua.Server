#!/bin/bash

# This script fixes common field naming patterns in Honua.Server.Host
# It replaces underscore-prefixed field references with this. prefixed references

echo "Fixing field naming errors in Honua.Server.Host..."

# Get list of unique files with errors
dotnet build src/Honua.Server.Host/Honua.Server.Host.csproj --nologo 2>&1 | \
  grep "error CS" | \
  awk -F':' '{print $1}' | \
  sort -u > /tmp/error_files.txt

echo "Found $(wc -l < /tmp/error_files.txt) files with errors"

# Common patterns to fix (be careful with these - they need context awareness)
files=$(cat /tmp/error_files.txt)

for file in $files; do
  if [ -f "$file" ]; then
    echo "Processing: $file"

    # Fix common underscore-prefixed field references
    # Note: This is a simplified approach - manual review is recommended
    sed -i 's/\b_next(/this.next(/g' "$file"
    sed -i 's/\b_logger\./this.logger./g' "$file"
    sed -i 's/\b_configuration\./this.configuration./g' "$file"
    sed -i 's/\b_connectionString\b/this.connectionString/g' "$file"
    sed -i 's/\b_metadataProvider\b/this.metadataProvider/g' "$file"
    sed -i 's/\b_changeNotifier\./this._changeNotifier./g' "$file"
    sed -i 's/\b_repository\./this.repository./g' "$file"
    sed -i 's/\b_cache\./this.cache./g' "$file"
    sed -i 's/\b_honuaConfig\b/this.honuaConfig/g' "$file"
    sed -i 's/\b_explicitConfiguration\b/this.explicitConfiguration/g' "$file"
    sed -i 's/\b_etag\b/this.etag/g' "$file"
    sed -i 's/\b_blobServiceClient\b/this.blobServiceClient/g' "$file"
    sed -i 's/\b_testContainer\b/this.testContainer/g' "$file"
    sed -i 's/\b_testBucket\b/this.testBucket/g' "$file"
    sed -i 's/\b_s3Client\b/this.s3Client/g' "$file"
    sed -i 's/\b_storageClient\b/this.storageClient/g' "$file"
    sed -i 's/\bcontributors\./this.contributors./g' "$file"
    sed -i 's/\bcacheKeys\./this.cacheKeys./g' "$file"
    sed -i 's/\bactiveJobs\./this.activeJobs./g' "$file"
    sed -i 's/\bcompletedJobs\./this.completedJobs./g' "$file"
    sed -i 's/\bchangeTokenRegistration\./this.changeTokenRegistration./g' "$file"
    sed -i 's/\bmeter\./this.meter./g' "$file"
    sed -i 's/\b_lastModified\b/this.lastModified/g' "$file"
    sed -i 's/\b_resourceType\b/this.resourceType/g' "$file"
    sed -i 's/\b_contentType\b/this.contentType/g' "$file"
    sed -i 's/\b_cacheControl\b/this.cacheControl/g' "$file"
    sed -i 's/\b_content\b/this.content/g' "$file"
  fi
done

echo "Field naming fixes applied. Running build to check results..."
error_count=$(dotnet build src/Honua.Server.Host/Honua.Server.Host.csproj --nologo 2>&1 | grep -c "error CS")
echo "Remaining errors: $error_count"

rm /tmp/error_files.txt
