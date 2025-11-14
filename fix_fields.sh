#!/bin/bash

# Script to fix field naming issues by adding underscore prefixes to private field declarations

PROJECT_DIR="/home/mike/projects/Honua.Server"
cd "$PROJECT_DIR"

echo "Analyzing build errors..."

# Get all CS0103 errors and extract unique field names
dotnet build src/Honua.Server.Host/Honua.Server.Host.csproj --nologo 2>&1 | \
  grep "error CS0103" | \
  sed -E "s/.*The name '([^']+)'.*/\1/" | \
  sort | uniq > /tmp/missing_fields.txt

echo "Found $(wc -l < /tmp/missing_fields.txt) unique missing field names"
echo ""

# For each missing field (which has underscore), we need to find the declaration without underscore
# and add the underscore prefix

total_fixes=0
files_modified=0

while IFS= read -r field; do
  # Skip if field doesn't start with underscore
  if [[ ! "$field" =~ ^_ ]]; then
    continue
  fi
  
  # Get the field name without underscore
  field_no_underscore="${field#_}"
  
  echo "Processing: $field_no_underscore -> $field"
  
  # Find files that declare this field as private readonly/const without underscore
  # Pattern: private readonly/const Type fieldName;
  
  # Search in src/Honua.Server.Host for private field declarations
  grep_pattern="private (readonly |const )?[A-Za-z0-9_<>?,\[\] ]+ ${field_no_underscore}[;=]"
  
  files=$(grep -rl "private.*[^_]${field_no_underscore}[;=]" src/Honua.Server.Host/ 2>/dev/null | grep "\.cs$" || true)
  
  for file in $files; do
    # Check if this file actually has the declaration and uses the underscore version
    if grep -q "_${field_no_underscore}" "$file" 2>/dev/null && \
       grep -q "private.*[^_]${field_no_underscore}[;=]" "$file" 2>/dev/null; then
      
      echo "  Fixing $file"
      
      # Replace private field declarations: 
      # Match patterns like:
      # - private readonly Type fieldName;
      # - private readonly Type fieldName =
      # - private Type fieldName;
      # And add underscore prefix to the field name
      
      # Create backup
      cp "$file" "$file.bak"
      
      # Use sed to replace the field declaration
      # This matches: private (readonly|const)? Type fieldName(;|=)
      # and replaces fieldName with _fieldName
      sed -i -E "s/(private (readonly |const )?[A-Za-z0-9_<>?,\[\] ]+[[:space:]]+)${field_no_underscore}([;=])/\1${field}\3/g" "$file"
      
      # Also fix constructor assignments: this.fieldName = -> this._fieldName =
      sed -i -E "s/(this\.)${field_no_underscore}([[:space:]]*=)/\1${field}\2/g" "$file"
      
      if ! diff -q "$file" "$file.bak" > /dev/null 2>&1; then
        ((total_fixes++))
        ((files_modified++))
        echo "    ✓ Modified"
        rm "$file.bak"
      else
        echo "    - No changes needed"
        mv "$file.bak" "$file"
      fi
    fi
  done
  
done < /tmp/missing_fields.txt

echo ""
echo "========================================="
echo "Summary:"
echo "  Files modified: $files_modified"
echo "  Total file modifications: $total_fixes"
echo "========================================="
echo ""

# Clean up
rm /tmp/missing_fields.txt

# Rebuild to check remaining errors
echo "Rebuilding to check remaining errors..."
error_count=$(dotnet build src/Honua.Server.Host/Honua.Server.Host.csproj --nologo 2>&1 | grep -c "error CS0103" || echo "0")
echo "Remaining CS0103 errors: $error_count"

