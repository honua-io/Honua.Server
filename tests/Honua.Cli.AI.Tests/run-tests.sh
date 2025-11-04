#!/bin/bash
# ============================================================================
# Integration Test Runner
# ============================================================================
# Runs integration tests using Docker Compose and Testcontainers.
#
# Usage:
#   ./run-tests.sh                 # Run all tests
#   ./run-tests.sh --filter Pattern  # Run tests matching filter
#   ./run-tests.sh --docker        # Run tests in Docker container
# ============================================================================

set -e

# Colors
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m'

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}HonuaIO AI Consultant - Integration Tests${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# Parse arguments
RUN_IN_DOCKER=false
TEST_FILTER=""

while [[ $# -gt 0 ]]; do
    case $1 in
        --docker)
            RUN_IN_DOCKER=true
            shift
            ;;
        --filter)
            TEST_FILTER="$2"
            shift 2
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

# Check Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}Error: Docker is not running${NC}"
    echo "Please start Docker and try again"
    exit 1
fi

if [ "$RUN_IN_DOCKER" = true ]; then
    echo -e "${YELLOW}Running tests in Docker container...${NC}"
    echo ""

    # Start all services including test runner
    docker compose -f docker-compose.test.yml up --build --abort-on-container-exit test-runner

    # Get test results
    TEST_EXIT_CODE=$?

    # Clean up
    echo ""
    echo -e "${YELLOW}Cleaning up containers...${NC}"
    docker compose -f docker-compose.test.yml down -v

    if [ $TEST_EXIT_CODE -eq 0 ]; then
        echo -e "${GREEN}✓ All tests passed${NC}"
    else
        echo -e "${RED}✗ Tests failed${NC}"
    fi

    exit $TEST_EXIT_CODE
else
    echo -e "${YELLOW}Running tests locally with Testcontainers...${NC}"
    echo ""
    echo -e "${YELLOW}Note: This will automatically start Docker containers${NC}"
    echo ""

    # Build test filter argument
    FILTER_ARG=""
    if [ -n "$TEST_FILTER" ]; then
        FILTER_ARG="--filter \"FullyQualifiedName~$TEST_FILTER\""
        echo -e "${YELLOW}Test filter: $TEST_FILTER${NC}"
        echo ""
    fi

    # Run tests
    cd "$(dirname "$0")"

    dotnet test \
        --logger "console;verbosity=detailed" \
        --logger "trx;LogFileName=test-results.trx" \
        $FILTER_ARG

    TEST_EXIT_CODE=$?

    if [ $TEST_EXIT_CODE -eq 0 ]; then
        echo ""
        echo -e "${GREEN}========================================${NC}"
        echo -e "${GREEN}✓ All tests passed${NC}"
        echo -e "${GREEN}========================================${NC}"
    else
        echo ""
        echo -e "${RED}========================================${NC}"
        echo -e "${RED}✗ Tests failed${NC}"
        echo -e "${RED}========================================${NC}"
    fi

    exit $TEST_EXIT_CODE
fi
