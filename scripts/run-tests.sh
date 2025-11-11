#!/bin/bash
# Run all tests with code coverage for Honua Server
# Supports filtering by test category (unit, integration, all)

set -e

# Colors for output
GREEN='\033[0;32m'
BLUE='\033[0;34m'
YELLOW='\033[1;33m'
NC='\033[0m'

# Get the script directory and project root
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$( cd "$SCRIPT_DIR/.." && pwd )"

cd "$PROJECT_ROOT"

# Parse command line arguments
TEST_CATEGORY="${1:-unit}"

case "$TEST_CATEGORY" in
    unit)
        echo -e "${BLUE}==>${NC} Running unit tests with code coverage..."
        FILTER="Category!=Integration"
        ;;
    integration)
        echo -e "${BLUE}==>${NC} Running integration tests..."
        echo -e "${YELLOW}Note:${NC} Integration tests require Docker for TestContainers"
        FILTER="Category=Integration"
        ;;
    all)
        echo -e "${BLUE}==>${NC} Running all tests with code coverage..."
        FILTER=""
        ;;
    *)
        echo "Usage: $0 [unit|integration|all]"
        echo ""
        echo "  unit        - Run unit tests only (default)"
        echo "  integration - Run integration tests only (requires Docker)"
        echo "  all         - Run all tests"
        exit 1
        ;;
esac

echo ""

# Build filter argument
FILTER_ARG=""
if [ -n "$FILTER" ]; then
    FILTER_ARG="--filter \"$FILTER\""
fi

# Run tests with coverage
eval dotnet test \
    $FILTER_ARG \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=opencover \
    /p:CoverletOutput=./TestResults/ \
    /p:ExcludeByFile="**/Migrations/**" \
    /p:Exclude="[*.Tests]*" \
    --logger "console;verbosity=normal"

echo ""
echo -e "${GREEN}✓${NC} Tests completed!"
echo ""
echo "To view detailed coverage report:"
echo "  • Install reportgenerator: dotnet tool install -g dotnet-reportgenerator-globaltool"
echo "  • Generate report: reportgenerator -reports:\"**/TestResults/coverage.opencover.xml\" -targetdir:TestResults/Report"
echo "  • Open: TestResults/Report/index.html"
echo ""
