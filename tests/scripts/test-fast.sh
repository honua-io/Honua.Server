#!/bin/bash
# FAST Mode: SQLite Only
# Best for: Local development, PR checks
# Duration: ~2-3 minutes
# Requirements: No Docker needed

set -e

echo "========================================"
echo "Running Tests in FAST Mode (SQLite only)"
echo "========================================"
echo ""
echo "Mode: SQLite only"
echo "Docker Required: No"
echo "Estimated Duration: 2-3 minutes"
echo ""

export HONUA_DATABASE_TEST_MODE=fast

# Run tests
dotnet test --filter "Category=Integration&Database!=None" \
  --configuration Release \
  --verbosity normal \
  --logger "console;verbosity=detailed"

echo ""
echo "========================================"
echo "FAST Mode Tests Complete"
echo "========================================"
