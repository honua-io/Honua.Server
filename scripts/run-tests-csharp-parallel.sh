#!/usr/bin/env bash
#
# run-tests-csharp-parallel.sh
# Runs all C# tests in parallel with optimized settings for 22-core systems
#
# Usage:
#   ./scripts/run-tests-csharp-parallel.sh [options]
#
# Options:
#   --filter FILTER     Test filter (e.g., "Category=Unit")
#   --no-build          Skip build step
#   --coverage          Collect code coverage
#   --verbose           Verbose output
#   --projects PROJECT  Comma-separated list of projects to test
#   --max-threads N     Maximum parallel threads (default: 6)

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
CONFIGURATION="Release"
FILTER_VALUE=""
NO_BUILD=""
COLLECT_COVERAGE=false
VERBOSE=""
TEST_PROJECTS=""
MAX_THREADS="${MAX_THREADS:-4}"

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --filter)
            FILTER_VALUE="$2"
            shift 2
            ;;
        --no-build)
            NO_BUILD="--no-build"
            shift
            ;;
        --coverage)
            COLLECT_COVERAGE=true
            shift
            ;;
        --verbose)
            VERBOSE="--verbosity detailed"
            shift
            ;;
        --projects)
            TEST_PROJECTS="$2"
            shift 2
            ;;
        --max-threads)
            MAX_THREADS="$2"
            shift 2
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

cd "$PROJECT_ROOT"

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Running C# Tests in Parallel${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "Configuration: ${GREEN}${CONFIGURATION}${NC}"
echo -e "Max parallel threads: ${GREEN}${MAX_THREADS}${NC}"
[[ -n "$FILTER_VALUE" ]] && echo -e "Filter: ${YELLOW}${FILTER_VALUE}${NC}"
echo ""

# Set environment for parallel execution
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
export MaxParallelThreads="$MAX_THREADS"
export HONUA_ALLOW_QUICKSTART=true
export ASPNETCORE_ENVIRONMENT=Test
export honua__authentication__allowQuickStart=true

# Create results directory
RESULTS_DIR="$PROJECT_ROOT/TestResults"
mkdir -p "$RESULTS_DIR"
rm -rf "$RESULTS_DIR"/*

# Determine test projects
if [[ -n "$TEST_PROJECTS" ]]; then
    IFS=',' read -ra PROJECTS <<< "$TEST_PROJECTS"
else
    # Find all test projects (including split Core.Tests.* projects)
    # Exclude stub/unimplemented projects: Build.Orchestrator.Tests (TDD stub with no implementation)
    mapfile -t PROJECTS < <(find tests \( -name "*.Tests.csproj" -o -name "*.E2ETests.csproj" -o -name "*Tests.*.csproj" \) ! -name "*Tests.Shared.csproj" ! -name "Honua.Build.Orchestrator.Tests.csproj" | sort)
fi

echo -e "${YELLOW}Test projects (${#PROJECTS[@]}):${NC}"
for project in "${PROJECTS[@]}"; do
    echo -e "  - $(basename "$project" .csproj)"
done
echo ""

# Build options
BUILD_OPTS="-c $CONFIGURATION"
[[ -n "$NO_BUILD" ]] && BUILD_OPTS="$BUILD_OPTS $NO_BUILD"

# Test options
TEST_OPTS="$BUILD_OPTS"
[[ -n "$VERBOSE" ]] && TEST_OPTS="$TEST_OPTS $VERBOSE"
TEST_OPTS="$TEST_OPTS --results-directory $RESULTS_DIR"
TEST_OPTS="$TEST_OPTS --logger:console;verbosity=normal"
TEST_OPTS="$TEST_OPTS --logger:trx"

# Coverage options
if [[ "$COLLECT_COVERAGE" == true ]]; then
    TEST_OPTS="$TEST_OPTS --collect:\"XPlat Code Coverage\""
    TEST_OPTS="$TEST_OPTS --settings coverlet.runsettings"
fi

# Pre-build solution to avoid parallel build race conditions
if [[ -z "$NO_BUILD" ]]; then
    echo -e "${YELLOW}Building solution (synchronous build)...${NC}"
    echo -e "${CYAN}This ensures all dependencies are copied before parallel test execution${NC}"
    # Build with force to ensure clean state and all dependencies copied
    if ! dotnet build Honua.sln -c $CONFIGURATION --force --verbosity minimal; then
        echo -e "${RED}✗ Build failed! Aborting tests.${NC}"
        exit 1
    fi
    echo -e "${GREEN}✓ Build successful (all dependencies copied)${NC}"
    echo ""
fi

# Always use --no-build during test execution to avoid race conditions
if [[ -z "$NO_BUILD" ]] || [[ "$NO_BUILD" == "--no-build" ]]; then
    TEST_OPTS="$TEST_OPTS --no-build"
fi

# Run tests
TEST_START=$(date +%s)
FAILED_PROJECTS=()
PASSED_PROJECTS=()

echo -e "${YELLOW}Running tests...${NC}"

# Run all test projects in TRUE parallel using background jobs
echo -e "${BLUE}Running ${#PROJECTS[@]} test projects in parallel (max $MAX_THREADS at a time)...${NC}"

# Track background jobs
declare -A PIDS
RUNNING=0

for project in "${PROJECTS[@]}"; do
    PROJECT_NAME=$(basename "$project" .csproj)
    LOG_FILE="$RESULTS_DIR/${PROJECT_NAME}.log"

    # Wait if we've hit the max parallel limit
    while [[ $RUNNING -ge $MAX_THREADS ]]; do
        for pid in "${!PIDS[@]}"; do
            if ! kill -0 "$pid" 2>/dev/null; then
                wait "$pid"
                PROJECT="${PIDS[$pid]}"
                unset PIDS[$pid]
                ((RUNNING--))
            fi
        done
        sleep 0.5
    done

    # Start test in background
    echo -e "${BLUE}Starting: ${PROJECT_NAME}${NC}"
    (
        # Execute test with proper argument handling
        if [[ -n "$FILTER_VALUE" ]]; then
            if dotnet test "$project" $TEST_OPTS --filter "$FILTER_VALUE" > "$LOG_FILE" 2>&1; then
                echo "$PROJECT_NAME:PASSED" >> "$RESULTS_DIR/status.txt"
            else
                echo "$PROJECT_NAME:FAILED" >> "$RESULTS_DIR/status.txt"
            fi
        else
            if dotnet test "$project" $TEST_OPTS > "$LOG_FILE" 2>&1; then
                echo "$PROJECT_NAME:PASSED" >> "$RESULTS_DIR/status.txt"
            else
                echo "$PROJECT_NAME:FAILED" >> "$RESULTS_DIR/status.txt"
            fi
        fi
    ) &

    PID=$!
    PIDS[$PID]="$PROJECT_NAME"
    ((RUNNING++))
done

# Wait for all remaining jobs
echo -e "${YELLOW}Waiting for all test projects to complete...${NC}"
for pid in "${!PIDS[@]}"; do
    wait "$pid"
done

# Aggregate results
echo ""
echo -e "${YELLOW}Aggregating results...${NC}"
while IFS=: read -r project status; do
    if [[ "$status" == "PASSED" ]]; then
        PASSED_PROJECTS+=("$project")
        echo -e "  ${GREEN}✓ $project${NC}"
    else
        FAILED_PROJECTS+=("$project")
        echo -e "  ${RED}✗ $project${NC}"
    fi

    # Show test output
    LOG_FILE="$RESULTS_DIR/${project}.log"
    if [[ -f "$LOG_FILE" ]]; then
        grep -E "(Passed!|Failed!)" "$LOG_FILE" || true
    fi
done < "$RESULTS_DIR/status.txt" 2>/dev/null || true

TEST_END=$(date +%s)
TEST_TIME=$((TEST_END - TEST_START))

# Generate coverage report if requested
if [[ "$COLLECT_COVERAGE" == true ]]; then
    echo ""
    echo -e "${YELLOW}Generating coverage report...${NC}"

    if command -v reportgenerator &> /dev/null; then
        reportgenerator \
            -reports:"$RESULTS_DIR/**/coverage.cobertura.xml" \
            -targetdir:"$RESULTS_DIR/CoverageReport" \
            -reporttypes:"Html;JsonSummary;Badges;MarkdownSummaryGithub" \
            -assemblyfilters:"-*.Tests;-*.Benchmarks" \
            -classfilters:"-*.Migrations.*;-*.DTO;-*.Models.Generated.*"

        echo -e "${GREEN}✓ Coverage report generated${NC}"
        echo -e "  Location: ${RESULTS_DIR}/CoverageReport/index.html"
    else
        echo -e "${YELLOW}⚠ reportgenerator not found, skipping coverage report${NC}"
        echo -e "  Install with: dotnet tool install -g dotnet-reportgenerator-globaltool"
    fi
fi

# Summary
echo ""
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Test Summary${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "Total time: ${GREEN}${TEST_TIME}s${NC}"

if [[ ${#FAILED_PROJECTS[@]} -gt 0 ]]; then
    echo -e "Passed: ${GREEN}${#PASSED_PROJECTS[@]}${NC}"
    echo -e "Failed: ${RED}${#FAILED_PROJECTS[@]}${NC}"
    echo ""
    echo -e "${RED}Failed projects:${NC}"
    for project in "${FAILED_PROJECTS[@]}"; do
        echo -e "  - ${RED}${project}${NC}"
    done
    echo ""
    exit 1
else
    echo -e "Status: ${GREEN}All tests passed ✓${NC}"
    [[ -n "$TEST_PROJECTS" ]] && echo -e "Passed: ${GREEN}${#PASSED_PROJECTS[@]}${NC}"
    echo ""
fi

echo -e "${GREEN}Test results: ${RESULTS_DIR}${NC}"
echo ""
