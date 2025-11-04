#!/usr/bin/env bash
#
# run-tests-python-parallel.sh
# Runs Python tests in parallel using pytest-xdist
#
# Usage:
#   ./scripts/run-tests-python-parallel.sh [options]
#
# Options:
#   -n N               Number of parallel workers (default: 5)
#   --filter MARKER    Run only tests matching marker (e.g., "smoke")
#   --verbose          Verbose output
#   --coverage         Collect code coverage
#   --html             Generate HTML report
#   --no-server        Don't start test server (use existing)

set -euo pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
WORKERS="${WORKERS:-5}"
FILTER=""
VERBOSE=""
COLLECT_COVERAGE=false
HTML_REPORT=false
START_SERVER=true

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        -n)
            WORKERS="$2"
            shift 2
            ;;
        --filter)
            FILTER="-m $2"
            shift 2
            ;;
        --verbose)
            VERBOSE="-v"
            shift
            ;;
        --coverage)
            COLLECT_COVERAGE=true
            shift
            ;;
        --html)
            HTML_REPORT=true
            shift
            ;;
        --no-server)
            START_SERVER=false
            shift
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

cd "$PROJECT_ROOT"

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Running Python Tests in Parallel${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "Workers: ${GREEN}${WORKERS}${NC}"
[[ -n "$FILTER" ]] && echo -e "Filter: ${YELLOW}${FILTER}${NC}"
echo ""

# Check Python environment
if [[ ! -d "tests/python/venv" ]] && [[ ! -f "tests/python/.venv/bin/activate" ]]; then
    echo -e "${YELLOW}Setting up Python virtual environment...${NC}"
    cd tests/python
    python3 -m venv venv
    source venv/bin/activate
    pip install -r requirements.txt
    cd "$PROJECT_ROOT"
else
    echo -e "${GREEN}✓ Python environment found${NC}"
    source tests/python/venv/bin/activate 2>/dev/null || source tests/python/.venv/bin/activate
fi

# Verify pytest-xdist is installed
if ! python -c "import xdist" 2>/dev/null; then
    echo -e "${YELLOW}Installing pytest-xdist...${NC}"
    pip install pytest-xdist
fi

# Start test server if needed
SERVER_PID=""
if [[ "$START_SERVER" == true ]]; then
    echo -e "${YELLOW}Starting test server...${NC}"

    # Check if server is already running
    if curl -sf http://localhost:8080/health >/dev/null 2>&1; then
        echo -e "${GREEN}✓ Test server already running${NC}"
    else
        # Start using docker-compose
        docker-compose -f docker-compose.test-parallel.yml up -d

        # Wait for health check
        echo -e "  Waiting for server to be healthy..."
        RETRIES=0
        MAX_RETRIES=60

        while [[ $RETRIES -lt $MAX_RETRIES ]]; do
            if curl -sf http://localhost:8080/health >/dev/null 2>&1; then
                echo -e "  ${GREEN}✓ Server is healthy${NC}"
                break
            fi
            sleep 1
            RETRIES=$((RETRIES + 1))
            printf "."
        done

        if [[ $RETRIES -ge $MAX_RETRIES ]]; then
            echo -e "\n${RED}✗ Server health check timeout${NC}"
            docker-compose -f docker-compose.test-parallel.yml logs
            exit 1
        fi
        echo ""
    fi
fi

# Export environment variables
export HONUA_API_BASE_URL="http://localhost:8080"
export HONUA_API_BEARER=""  # No auth for tests

# Build pytest command
PYTEST_OPTS="-n $WORKERS"
[[ -n "$VERBOSE" ]] && PYTEST_OPTS="$PYTEST_OPTS $VERBOSE"
[[ -n "$FILTER" ]] && PYTEST_OPTS="$PYTEST_OPTS $FILTER"

# Results directory
RESULTS_DIR="$PROJECT_ROOT/TestResults/python"
mkdir -p "$RESULTS_DIR"

# Add output options
PYTEST_OPTS="$PYTEST_OPTS --tb=short --color=yes"
PYTEST_OPTS="$PYTEST_OPTS --junitxml=$RESULTS_DIR/junit.xml"

# Coverage options
if [[ "$COLLECT_COVERAGE" == true ]]; then
    PYTEST_OPTS="$PYTEST_OPTS --cov=tests/python --cov-report=html:$RESULTS_DIR/coverage --cov-report=term"
fi

# HTML report
if [[ "$HTML_REPORT" == true ]]; then
    # Check if pytest-html is installed
    if ! python -c "import pytest_html" 2>/dev/null; then
        echo -e "${YELLOW}Installing pytest-html...${NC}"
        pip install pytest-html
    fi
    PYTEST_OPTS="$PYTEST_OPTS --html=$RESULTS_DIR/report.html --self-contained-html"
fi

# Run tests
echo -e "${YELLOW}Running tests...${NC}"
TEST_START=$(date +%s)

cd tests/python

if pytest $PYTEST_OPTS .; then
    TEST_RESULT="${GREEN}✓ All tests passed${NC}"
    EXIT_CODE=0
else
    TEST_RESULT="${RED}✗ Some tests failed${NC}"
    EXIT_CODE=1
fi

TEST_END=$(date +%s)
TEST_TIME=$((TEST_END - TEST_START))

cd "$PROJECT_ROOT"

# Stop test server if we started it
if [[ "$START_SERVER" == true ]] && [[ -z "$SERVER_PID" ]]; then
    echo ""
    echo -e "${YELLOW}Stopping test server...${NC}"
    # docker-compose -f docker-compose.test-parallel.yml down
    echo -e "${BLUE}(Leaving server running for additional test runs)${NC}"
fi

# Summary
echo ""
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Test Summary${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "Status: $TEST_RESULT"
echo -e "Time: ${GREEN}${TEST_TIME}s${NC}"
echo -e "Workers: ${GREEN}${WORKERS}${NC}"
echo ""
echo -e "${GREEN}Results: ${RESULTS_DIR}${NC}"
[[ "$HTML_REPORT" == true ]] && echo -e "HTML Report: ${GREEN}${RESULTS_DIR}/report.html${NC}"
[[ "$COLLECT_COVERAGE" == true ]] && echo -e "Coverage: ${GREEN}${RESULTS_DIR}/coverage/index.html${NC}"
echo ""

exit $EXIT_CODE
