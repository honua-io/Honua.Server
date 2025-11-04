#!/usr/bin/env bash
#
# run-tests-parallel.sh
# Master test orchestration script for parallel execution across all test suites
#
# Optimized for 22-core systems:
# - C# tests: 6 parallel xUnit collections (10-12 cores)
# - Python tests: 5 parallel workers (5 cores)
# - QGIS tests: 5 parallel workers (5 cores)
#
# Usage:
#   ./scripts/run-tests-parallel.sh [options]
#
# Options:
#   --csharp-only          Run only C# tests
#   --python-only          Run only Python tests
#   --qgis-only            Run only QGIS tests
#   --no-build             Skip building test cache image
#   --filter FILTER        Filter tests (e.g., "smoke", "Unit")
#   --coverage             Collect code coverage
#   --html                 Generate HTML reports
#   --csharp-threads N     C# parallel threads (default: 6)
#   --python-workers N     Python parallel workers (default: 5)
#   --qgis-workers N       QGIS parallel workers (default: 5)
#   --sequential           Run test suites sequentially instead of concurrently
#   --stop-on-fail         Stop all tests if any suite fails

set -euo pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
MAGENTA='\033[0;35m'
NC='\033[0m'

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"

# Test suite selection
RUN_CSHARP=true
RUN_PYTHON=true
RUN_QGIS=true

# Options
BUILD_CACHE=true
FILTER=""
COVERAGE=false
HTML=false
SEQUENTIAL=false
STOP_ON_FAIL=false

# Worker counts
CSHARP_THREADS="${CSHARP_THREADS:-6}"
PYTHON_WORKERS="${PYTHON_WORKERS:-5}"
QGIS_WORKERS="${QGIS_WORKERS:-5}"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --csharp-only)
            RUN_PYTHON=false
            RUN_QGIS=false
            shift
            ;;
        --python-only)
            RUN_CSHARP=false
            RUN_QGIS=false
            shift
            ;;
        --qgis-only)
            RUN_CSHARP=false
            RUN_PYTHON=false
            shift
            ;;
        --no-build)
            BUILD_CACHE=false
            shift
            ;;
        --filter)
            FILTER="$2"
            shift 2
            ;;
        --coverage)
            COVERAGE=true
            shift
            ;;
        --html)
            HTML=true
            shift
            ;;
        --csharp-threads)
            CSHARP_THREADS="$2"
            shift 2
            ;;
        --python-workers)
            PYTHON_WORKERS="$2"
            shift 2
            ;;
        --qgis-workers)
            QGIS_WORKERS="$2"
            shift 2
            ;;
        --sequential)
            SEQUENTIAL=true
            shift
            ;;
        --stop-on-fail)
            STOP_ON_FAIL=true
            shift
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

cd "$PROJECT_ROOT"

# Header
echo -e "${CYAN}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║${NC}  ${MAGENTA}HonuaIO Parallel Test Suite${NC}                            ${CYAN}║${NC}"
echo -e "${CYAN}║${NC}  Optimized for 22-core systems                          ${CYAN}║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Configuration summary
echo -e "${BLUE}Configuration:${NC}"
echo -e "  Test Suites:"
[[ "$RUN_CSHARP" == true ]] && echo -e "    ${GREEN}✓${NC} C# (xUnit) - ${CSHARP_THREADS} parallel threads"
[[ "$RUN_PYTHON" == true ]] && echo -e "    ${GREEN}✓${NC} Python (pytest) - ${PYTHON_WORKERS} parallel workers"
[[ "$RUN_QGIS" == true ]] && echo -e "    ${GREEN}✓${NC} QGIS (pytest) - ${QGIS_WORKERS} parallel workers"
echo ""
echo -e "  Options:"
[[ "$BUILD_CACHE" == true ]] && echo -e "    - Build cached test image"
[[ -n "$FILTER" ]] && echo -e "    - Filter: ${YELLOW}${FILTER}${NC}"
[[ "$COVERAGE" == true ]] && echo -e "    - Collect code coverage"
[[ "$HTML" == true ]] && echo -e "    - Generate HTML reports"
[[ "$SEQUENTIAL" == true ]] && echo -e "    - ${YELLOW}Sequential mode${NC} (suites run one at a time)"
[[ "$STOP_ON_FAIL" == true ]] && echo -e "    - ${YELLOW}Stop on first failure${NC}"
echo ""

# Start timer
TOTAL_START=$(date +%s)

# Build test cache image if needed
if [[ "$BUILD_CACHE" == true ]] && [[ "$RUN_PYTHON" == true || "$RUN_QGIS" == true ]]; then
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}Building Cached Test Image${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

    if [[ -f "$SCRIPT_DIR/build-test-cache.sh" ]]; then
        "$SCRIPT_DIR/build-test-cache.sh"
    else
        echo -e "${RED}✗ build-test-cache.sh not found${NC}"
        exit 1
    fi
    echo ""
fi

# Create results directory
RESULTS_DIR="$PROJECT_ROOT/TestResults"
mkdir -p "$RESULTS_DIR"

# Export worker counts
export MaxParallelThreads="$CSHARP_THREADS"
export WORKERS="$PYTHON_WORKERS"

# Function to run C# tests
run_csharp_tests() {
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${CYAN}Running C# Tests (xUnit)${NC}"
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

    CSHARP_OPTS="--max-threads $CSHARP_THREADS"
    [[ -n "$FILTER" ]] && CSHARP_OPTS="$CSHARP_OPTS --filter \"$FILTER\""
    [[ "$COVERAGE" == true ]] && CSHARP_OPTS="$CSHARP_OPTS --coverage"

    if "$SCRIPT_DIR/run-tests-csharp-parallel.sh" $CSHARP_OPTS; then
        echo "PASS" > "$RESULTS_DIR/.csharp-result"
        return 0
    else
        echo "FAIL" > "$RESULTS_DIR/.csharp-result"
        return 1
    fi
}

# Function to run Python tests
run_python_tests() {
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${CYAN}Running Python Tests (pytest)${NC}"
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

    PYTHON_OPTS="-n $PYTHON_WORKERS"
    [[ -n "$FILTER" ]] && PYTHON_OPTS="$PYTHON_OPTS --filter \"$FILTER\""
    [[ "$COVERAGE" == true ]] && PYTHON_OPTS="$PYTHON_OPTS --coverage"
    [[ "$HTML" == true ]] && PYTHON_OPTS="$PYTHON_OPTS --html"

    if "$SCRIPT_DIR/run-tests-python-parallel.sh" $PYTHON_OPTS; then
        echo "PASS" > "$RESULTS_DIR/.python-result"
        return 0
    else
        echo "FAIL" > "$RESULTS_DIR/.python-result"
        return 1
    fi
}

# Function to run QGIS tests
run_qgis_tests() {
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${CYAN}Running QGIS Tests (pytest)${NC}"
    echo -e "${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

    QGIS_OPTS="-n $QGIS_WORKERS"
    [[ -n "$FILTER" ]] && QGIS_OPTS="$QGIS_OPTS --filter \"$FILTER\""
    [[ "$HTML" == true ]] && QGIS_OPTS="$QGIS_OPTS --html"

    if "$SCRIPT_DIR/run-tests-qgis-parallel.sh" $QGIS_OPTS; then
        echo "PASS" > "$RESULTS_DIR/.qgis-result"
        return 0
    else
        echo "FAIL" > "$RESULTS_DIR/.qgis-result"
        return 1
    fi
}

# Clean up previous results
rm -f "$RESULTS_DIR"/.{csharp,python,qgis}-result

# Run tests
if [[ "$SEQUENTIAL" == true ]]; then
    # Sequential execution
    echo -e "${YELLOW}Running test suites sequentially...${NC}"
    echo ""

    FAILED_SUITES=()

    if [[ "$RUN_CSHARP" == true ]]; then
        if ! run_csharp_tests; then
            FAILED_SUITES+=("C#")
            [[ "$STOP_ON_FAIL" == true ]] && exit 1
        fi
        echo ""
    fi

    if [[ "$RUN_PYTHON" == true ]]; then
        if ! run_python_tests; then
            FAILED_SUITES+=("Python")
            [[ "$STOP_ON_FAIL" == true ]] && exit 1
        fi
        echo ""
    fi

    if [[ "$RUN_QGIS" == true ]]; then
        if ! run_qgis_tests; then
            FAILED_SUITES+=("QGIS")
            [[ "$STOP_ON_FAIL" == true ]] && exit 1
        fi
        echo ""
    fi
else
    # Concurrent execution (default)
    echo -e "${GREEN}Running test suites concurrently...${NC}"
    echo ""

    PIDS=()

    # Start C# tests in background
    if [[ "$RUN_CSHARP" == true ]]; then
        run_csharp_tests &
        PIDS+=($!)
        echo -e "${BLUE}Started C# tests (PID: $!)${NC}"
    fi

    # Start Python tests in background
    if [[ "$RUN_PYTHON" == true ]]; then
        run_python_tests &
        PIDS+=($!)
        echo -e "${BLUE}Started Python tests (PID: $!)${NC}"
    fi

    # Start QGIS tests in background
    if [[ "$RUN_QGIS" == true ]]; then
        run_qgis_tests &
        PIDS+=($!)
        echo -e "${BLUE}Started QGIS tests (PID: $!)${NC}"
    fi

    echo ""
    echo -e "${YELLOW}Waiting for all test suites to complete...${NC}"
    echo ""

    # Wait for all background jobs
    FAILED_SUITES=()
    for pid in "${PIDS[@]}"; do
        if ! wait "$pid"; then
            # Check which suite failed
            [[ -f "$RESULTS_DIR/.csharp-result" ]] && [[ "$(cat "$RESULTS_DIR/.csharp-result")" == "FAIL" ]] && FAILED_SUITES+=("C#")
            [[ -f "$RESULTS_DIR/.python-result" ]] && [[ "$(cat "$RESULTS_DIR/.python-result")" == "FAIL" ]] && FAILED_SUITES+=("Python")
            [[ -f "$RESULTS_DIR/.qgis-result" ]] && [[ "$(cat "$RESULTS_DIR/.qgis-result")" == "FAIL" ]] && FAILED_SUITES+=("QGIS")
        fi
    done
fi

# End timer
TOTAL_END=$(date +%s)
TOTAL_TIME=$((TOTAL_END - TOTAL_START))

# Format time
MINUTES=$((TOTAL_TIME / 60))
SECONDS=$((TOTAL_TIME % 60))

# Summary
echo ""
echo -e "${CYAN}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║${NC}  ${MAGENTA}Test Execution Summary${NC}                                 ${CYAN}║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Results by suite
if [[ "$RUN_CSHARP" == true ]]; then
    if [[ -f "$RESULTS_DIR/.csharp-result" ]] && [[ "$(cat "$RESULTS_DIR/.csharp-result")" == "PASS" ]]; then
        echo -e "  C# Tests:     ${GREEN}✓ PASSED${NC}"
    else
        echo -e "  C# Tests:     ${RED}✗ FAILED${NC}"
    fi
fi

if [[ "$RUN_PYTHON" == true ]]; then
    if [[ -f "$RESULTS_DIR/.python-result" ]] && [[ "$(cat "$RESULTS_DIR/.python-result")" == "PASS" ]]; then
        echo -e "  Python Tests: ${GREEN}✓ PASSED${NC}"
    else
        echo -e "  Python Tests: ${RED}✗ FAILED${NC}"
    fi
fi

if [[ "$RUN_QGIS" == true ]]; then
    if [[ -f "$RESULTS_DIR/.qgis-result" ]] && [[ "$(cat "$RESULTS_DIR/.qgis-result")" == "PASS" ]]; then
        echo -e "  QGIS Tests:   ${GREEN}✓ PASSED${NC}"
    else
        echo -e "  QGIS Tests:   ${RED}✗ FAILED${NC}"
    fi
fi

echo ""
echo -e "  Total Time: ${GREEN}${MINUTES}m ${SECONDS}s${NC}"
echo ""

# Results location
echo -e "${BLUE}Detailed Results:${NC}"
echo -e "  ${RESULTS_DIR}/"
[[ "$RUN_CSHARP" == true ]] && echo -e "    ├── C# test results (TRX, coverage)"
[[ "$RUN_PYTHON" == true ]] && echo -e "    ├── python/"
[[ "$RUN_PYTHON" == true && "$HTML" == true ]] && echo -e "    │   └── report.html"
[[ "$RUN_QGIS" == true ]] && echo -e "    └── qgis/"
[[ "$RUN_QGIS" == true && "$HTML" == true ]] && echo -e "        └── report.html"
echo ""

# Exit code
if [[ ${#FAILED_SUITES[@]} -gt 0 ]]; then
    echo -e "${RED}Failed test suites: ${FAILED_SUITES[*]}${NC}"
    echo ""
    exit 1
else
    echo -e "${GREEN}✓ All test suites passed!${NC}"
    echo ""
    exit 0
fi
