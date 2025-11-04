#!/bin/bash
# Script to add Elastic License 2.0 headers to all C# source files

# License header to add
read -r -d '' LICENSE_HEADER << 'EOF'
// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

EOF

# Counter for tracking
count=0
skipped=0

# Find all .cs files in src/ directory
while IFS= read -r file; do
    # Check if file already has copyright header
    if head -n 1 "$file" | grep -q "Copyright"; then
        ((skipped++))
        continue
    fi

    # Create temp file with header + original content
    {
        echo "$LICENSE_HEADER"
        cat "$file"
    } > "${file}.tmp"

    # Replace original file
    mv "${file}.tmp" "$file"

    ((count++))

    # Progress indicator every 100 files
    if (( count % 100 == 0 )); then
        echo "Processed $count files..."
    fi
done < <(find src -name "*.cs")

echo ""
echo "License header addition complete!"
echo "Files updated: $count"
echo "Files skipped (already have copyright): $skipped"
