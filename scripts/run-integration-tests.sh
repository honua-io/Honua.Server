#!/bin/bash
# Run integration tests for Honua Server
# This script runs tests that require real databases via TestContainers

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

echo -e "${BLUE}==>${NC} Running integration tests..."
echo ""
echo -e "${YELLOW}Note:${NC} Integration tests require Docker to be running for TestContainers"
echo ""

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${YELLOW}Warning:${NC} Docker does not appear to be running."
    echo "TestContainers requires Docker to create test databases."
    echo "Please start Docker and try again."
    exit 1
fi

echo -e "${BLUE}==>${NC} Docker is running. Starting tests..."
echo ""

# Run integration tests only
dotnet test \
    --filter "Category=Integration" \
    /p:CollectCoverage=true \
    /p:CoverletOutputFormat=opencover \
    /p:CoverletOutput=./TestResults/Integration/ \
    --logger "console;verbosity=normal"

echo ""
echo -e "${GREEN}✓${NC} Integration tests completed!"
echo ""
