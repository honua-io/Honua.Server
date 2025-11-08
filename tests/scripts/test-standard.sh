#!/bin/bash
# STANDARD Mode: SQLite + PostgreSQL + MySQL
# Best for: Integration testing, merge to main
# Duration: ~5-10 minutes
# Requirements: Docker must be running

set -e

echo "========================================"
echo "Running Tests in STANDARD Mode"
echo "========================================"
echo ""
echo "Providers: SQLite, PostgreSQL, MySQL"
echo "Docker Required: Yes"
echo "Estimated Duration: 5-10 minutes"
echo ""

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
  echo "ERROR: Docker is not running. STANDARD mode requires Docker."
  echo "Please start Docker and try again."
  exit 1
fi

export HONUA_DATABASE_TEST_MODE=standard

# Run tests
dotnet test --filter "Category=Integration&Database!=None" \
  --configuration Release \
  --verbosity normal \
  --logger "console;verbosity=detailed"

echo ""
echo "========================================"
echo "STANDARD Mode Tests Complete"
echo "========================================"
