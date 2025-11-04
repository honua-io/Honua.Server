#!/usr/bin/env bash
#
# test-all.sh
# Unified test workflow: Clean → Build → Run All Tests in Parallel
#
# This script solves the problem of running tests efficiently:
# 1. Clean once (remove old artifacts)
# 2. Build once (compile all projects)
# 3. Run all tests in parallel (no rebuild, maximizes CPU usage)
#
# Usage:
#   ./scripts/test-all.sh [options]
#
# Options:
#   --skip-clean          Skip clean step (faster, may have stale artifacts)
#   --skip-build          Skip build step (only use if already built)
#   --csharp-only         Run only C# tests
#   --filter FILTER       Test filter expression
#   --max-threads N       Max parallel C# test threads (default: 4)
#   --coverage            Collect code coverage
#   --stop-on-fail        Stop on first failure
#   --sequential          Run test suites sequentially (not parallel)
#
# Examples:
#   ./scripts/test-all.sh                    # Full clean build and test
#   ./scripts/test-all.sh --skip-clean       # Build and test (no clean)
#   ./scripts/test-all.sh --csharp-only      # Only C# tests
#   ./scripts/test-all.sh --max-threads 6    # Use 6 parallel threads

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
CONFIGURATION="Release"

# Options
SKIP_CLEAN=false
SKIP_BUILD=false
CSHARP_ONLY=false
FILTER=""
MAX_THREADS=4  # Conservative default to avoid Docker resource issues
COVERAGE=false
STOP_ON_FAIL=false
SEQUENTIAL=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --skip-clean)
            SKIP_CLEAN=true
            shift
            ;;
        --skip-build)
            SKIP_BUILD=true
            shift
            ;;
        --csharp-only)
            CSHARP_ONLY=true
            shift
            ;;
        --filter)
            FILTER="$2"
            shift 2
            ;;
        --max-threads)
            MAX_THREADS="$2"
            shift 2
            ;;
        --coverage)
            COVERAGE=true
            shift
            ;;
        --stop-on-fail)
            STOP_ON_FAIL=true
            shift
            ;;
        --sequential)
            SEQUENTIAL=true
            shift
            ;;
        -h|--help)
            grep "^#" "$0" | grep -v "#!/usr/bin/env" | sed 's/^# //g' | sed 's/^#//g'
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            echo "Use --help for usage information"
            exit 1
            ;;
    esac
done

cd "$PROJECT_ROOT"

# Header
echo -e "${CYAN}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║${NC}  ${MAGENTA}HonuaIO Unified Test Workflow${NC}                          ${CYAN}║${NC}"
echo -e "${CYAN}║${NC}  Clean → Build → Test in Parallel                       ${CYAN}║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Configuration summary
echo -e "${BLUE}Configuration:${NC}"
echo -e "  Configuration: ${GREEN}${CONFIGURATION}${NC}"
echo -e "  Max C# threads: ${GREEN}${MAX_THREADS}${NC}"
[[ "$SKIP_CLEAN" == true ]] && echo -e "  ${YELLOW}⚠ Skipping clean step${NC}"
[[ "$SKIP_BUILD" == true ]] && echo -e "  ${YELLOW}⚠ Skipping build step${NC}"
[[ "$CSHARP_ONLY" == true ]] && echo -e "  Running: ${BLUE}C# tests only${NC}"
[[ -n "$FILTER" ]] && echo -e "  Filter: ${YELLOW}${FILTER}${NC}"
[[ "$COVERAGE" == true ]] && echo -e "  ${GREEN}✓${NC} Code coverage enabled"
[[ "$SEQUENTIAL" == true ]] && echo -e "  Mode: ${YELLOW}Sequential${NC}"
echo ""

# Start timer
TOTAL_START=$(date +%s)

# ============================================================================
# STEP 1: CLEAN
# ============================================================================
if [[ "$SKIP_CLEAN" == false ]]; then
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}STEP 1: Clean${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""

    CLEAN_START=$(date +%s)

    echo -e "${BLUE}Cleaning solution...${NC}"
    if dotnet clean Honua.sln -c "$CONFIGURATION" --verbosity quiet; then
        echo -e "${GREEN}✓ Clean successful${NC}"
    else
        echo -e "${RED}✗ Clean failed${NC}"
        exit 1
    fi

    # Clean TestResults directory
    echo -e "${BLUE}Cleaning TestResults directory...${NC}"
    rm -rf TestResults/*
    mkdir -p TestResults
    echo -e "${GREEN}✓ TestResults cleaned${NC}"

    CLEAN_END=$(date +%s)
    CLEAN_TIME=$((CLEAN_END - CLEAN_START))
    echo -e "${GREEN}Clean completed in ${CLEAN_TIME}s${NC}"
    echo ""
else
    echo -e "${YELLOW}⚠ Skipping clean step${NC}"
    echo ""
fi

# ============================================================================
# STEP 2: BUILD
# ============================================================================
if [[ "$SKIP_BUILD" == false ]]; then
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}STEP 2: Build${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo ""

    BUILD_START=$(date +%s)

    echo -e "${BLUE}Building solution in ${CONFIGURATION} mode...${NC}"
    echo -e "${CYAN}This ensures all dependencies are copied and ready for parallel testing${NC}"
    echo ""

    # Build with force to ensure clean state
    if dotnet build Honua.sln -c "$CONFIGURATION" --force --verbosity minimal; then
        echo ""
        echo -e "${GREEN}✓ Build successful${NC}"
    else
        echo ""
        echo -e "${RED}✗ Build failed! Cannot proceed with tests.${NC}"
        exit 1
    fi

    BUILD_END=$(date +%s)
    BUILD_TIME=$((BUILD_END - BUILD_START))
    echo -e "${GREEN}Build completed in ${BUILD_TIME}s${NC}"
    echo ""
else
    echo -e "${YELLOW}⚠ Skipping build step (using existing binaries)${NC}"
    echo ""
fi

# ============================================================================
# STEP 3: RUN TESTS
# ============================================================================
echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${YELLOW}STEP 3: Run Tests in Parallel${NC}"
echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo ""

TEST_START=$(date +%s)

# Build test command
TEST_CMD=("$SCRIPT_DIR/run-tests-csharp-parallel.sh")
TEST_CMD+=("--no-build")  # Already built, don't rebuild
TEST_CMD+=("--max-threads" "$MAX_THREADS")

[[ -n "$FILTER" ]] && TEST_CMD+=("--filter" "$FILTER")
[[ "$COVERAGE" == true ]] && TEST_CMD+=("--coverage")

echo -e "${CYAN}Running C# tests with ${MAX_THREADS} parallel threads (no rebuild)...${NC}"
echo ""

# Run tests
if "${TEST_CMD[@]}"; then
    TEST_RESULT="PASSED"
    TEST_EXIT_CODE=0
else
    TEST_RESULT="FAILED"
    TEST_EXIT_CODE=1
fi

TEST_END=$(date +%s)
TEST_TIME=$((TEST_END - TEST_START))

echo ""
if [[ "$TEST_RESULT" == "PASSED" ]]; then
    echo -e "${GREEN}✓ All tests passed in ${TEST_TIME}s${NC}"
else
    echo -e "${RED}✗ Tests failed (see details above)${NC}"
fi
echo ""

# ============================================================================
# SUMMARY
# ============================================================================
TOTAL_END=$(date +%s)
TOTAL_TIME=$((TOTAL_END - TOTAL_START))
MINUTES=$((TOTAL_TIME / 60))
SECONDS=$((TOTAL_TIME % 60))

echo -e "${CYAN}╔════════════════════════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║${NC}  ${MAGENTA}Workflow Summary${NC}                                       ${CYAN}║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════════════════════════╝${NC}"
echo ""

# Show timing breakdown
if [[ "$SKIP_CLEAN" == false ]]; then
    echo -e "  Clean:  ${CLEAN_TIME}s"
fi
if [[ "$SKIP_BUILD" == false ]]; then
    echo -e "  Build:  ${BUILD_TIME}s"
fi
echo -e "  Tests:  ${TEST_TIME}s"
echo -e "  ${BLUE}─────────────${NC}"
echo -e "  Total:  ${GREEN}${MINUTES}m ${SECONDS}s${NC}"
echo ""

# Results location
echo -e "${BLUE}Test Results:${NC}"
echo -e "  ${PROJECT_ROOT}/TestResults/"
if [[ "$COVERAGE" == true ]]; then
    echo -e "  ${PROJECT_ROOT}/TestResults/CoverageReport/index.html"
fi
echo ""

# Final status
if [[ $TEST_EXIT_CODE -eq 0 ]]; then
    echo -e "${GREEN}✓ Workflow completed successfully!${NC}"
    echo ""
    exit 0
else
    echo -e "${RED}✗ Workflow failed - see test results above${NC}"
    echo ""
    exit 1
fi
