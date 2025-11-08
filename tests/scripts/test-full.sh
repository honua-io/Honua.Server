#!/bin/bash
# FULL Mode: All Database Providers
# Best for: Release validation, nightly builds
# Duration: ~15-20 minutes
# Requirements: Docker must be running

set -e

echo "========================================"
echo "Running Tests in FULL Mode"
echo "========================================"
echo ""
echo "Providers: SQLite, PostgreSQL, MySQL, SQL Server, DuckDB"
echo "Docker Required: Yes (except DuckDB)"
echo "Estimated Duration: 15-20 minutes"
echo ""

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
  echo "ERROR: Docker is not running. FULL mode requires Docker."
  echo "Please start Docker and try again."
  exit 1
fi

export HONUA_DATABASE_TEST_MODE=full

# Run tests with detailed logging
dotnet test --filter "Category=Integration&Database!=None" \
  --configuration Release \
  --verbosity normal \
  --logger "console;verbosity=detailed" \
  --logger "trx;LogFileName=test-results-full.trx"

echo ""
echo "========================================"
echo "FULL Mode Tests Complete"
echo "========================================"
echo ""
echo "Test results saved to: TestResults/test-results-full.trx"
