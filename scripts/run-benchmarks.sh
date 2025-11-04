#!/bin/bash
set -e

# Run Honua Server Performance Benchmarks
# Usage: ./scripts/run-benchmarks.sh [filter] [options]
#
# Examples:
#   ./scripts/run-benchmarks.sh                    # Run all benchmarks
#   ./scripts/run-benchmarks.sh "*OgcApi*"         # Run only OGC API benchmarks
#   ./scripts/run-benchmarks.sh "*Spatial*"        # Run only spatial benchmarks
#   ./scripts/run-benchmarks.sh --list             # List all benchmarks

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"
BENCHMARK_PROJECT="$PROJECT_ROOT/tests/Honua.Server.Benchmarks"
RESULTS_DIR="$PROJECT_ROOT/BenchmarkDotNet.Artifacts/results"
BASELINE_DIR="$PROJECT_ROOT/tests/Honua.Server.Benchmarks/baseline"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}======================================${NC}"
echo -e "${BLUE}Honua Server Performance Benchmarks${NC}"
echo -e "${BLUE}======================================${NC}"
echo ""

# Parse arguments
FILTER="$1"
SAVE_BASELINE=false
COMPARE_BASELINE=false
LIST_BENCHMARKS=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --save-baseline)
            SAVE_BASELINE=true
            shift
            ;;
        --compare)
            COMPARE_BASELINE=true
            shift
            ;;
        --list)
            LIST_BENCHMARKS=true
            shift
            ;;
        *)
            FILTER="$1"
            shift
            ;;
    esac
done

# List benchmarks if requested
if [ "$LIST_BENCHMARKS" = true ]; then
    echo -e "${YELLOW}Available benchmarks:${NC}"
    dotnet run -c Release --project "$BENCHMARK_PROJECT" --list flat
    exit 0
fi

# Ensure benchmark project exists
if [ ! -d "$BENCHMARK_PROJECT" ]; then
    echo -e "${RED}Error: Benchmark project not found at $BENCHMARK_PROJECT${NC}"
    exit 1
fi

# Create results directory
mkdir -p "$RESULTS_DIR"
mkdir -p "$BASELINE_DIR"

# Run benchmarks
echo -e "${YELLOW}Running benchmarks...${NC}"
echo ""

if [ -n "$FILTER" ]; then
    echo -e "${BLUE}Filter: $FILTER${NC}"
    dotnet run -c Release --project "$BENCHMARK_PROJECT" --filter "$FILTER"
else
    echo -e "${BLUE}Running all benchmarks${NC}"
    dotnet run -c Release --project "$BENCHMARK_PROJECT"
fi

BENCHMARK_EXIT_CODE=$?

if [ $BENCHMARK_EXIT_CODE -ne 0 ]; then
    echo -e "${RED}Benchmarks failed with exit code $BENCHMARK_EXIT_CODE${NC}"
    exit $BENCHMARK_EXIT_CODE
fi

echo ""
echo -e "${GREEN}Benchmarks completed successfully${NC}"
echo ""

# Find the most recent results
LATEST_RESULTS=$(find "$RESULTS_DIR" -name "*-report-full.json" -type f -printf '%T@ %p\n' | sort -rn | head -1 | cut -d' ' -f2-)

if [ -z "$LATEST_RESULTS" ]; then
    echo -e "${YELLOW}Warning: No benchmark results found${NC}"
    exit 0
fi

echo -e "${BLUE}Results saved to: $LATEST_RESULTS${NC}"
echo ""

# Save baseline if requested
if [ "$SAVE_BASELINE" = true ]; then
    BASELINE_FILE="$BASELINE_DIR/baseline-$(date +%Y%m%d-%H%M%S).json"
    cp "$LATEST_RESULTS" "$BASELINE_FILE"
    echo -e "${GREEN}Baseline saved to: $BASELINE_FILE${NC}"

    # Also create a symlink to latest baseline
    ln -sf "$(basename "$BASELINE_FILE")" "$BASELINE_DIR/latest.json"
    echo -e "${GREEN}Latest baseline link updated${NC}"
    echo ""
fi

# Compare with baseline if requested
if [ "$COMPARE_BASELINE" = true ]; then
    BASELINE_FILE="$BASELINE_DIR/latest.json"

    if [ ! -f "$BASELINE_FILE" ]; then
        echo -e "${YELLOW}Warning: No baseline found at $BASELINE_FILE${NC}"
        echo -e "${YELLOW}Run with --save-baseline to create a baseline${NC}"
    else
        echo -e "${BLUE}Comparing with baseline: $BASELINE_FILE${NC}"
        echo ""

        # Run comparison script
        "$SCRIPT_DIR/compare-benchmarks.sh" "$BASELINE_FILE" "$LATEST_RESULTS"
    fi
fi

# Print summary
echo ""
echo -e "${BLUE}======================================${NC}"
echo -e "${BLUE}Summary${NC}"
echo -e "${BLUE}======================================${NC}"
echo -e "Results directory: ${YELLOW}$RESULTS_DIR${NC}"

if [ "$SAVE_BASELINE" = true ]; then
    echo -e "Baseline saved: ${GREEN}Yes${NC}"
else
    echo -e "Baseline saved: ${YELLOW}No${NC} (use --save-baseline to save)"
fi

if [ "$COMPARE_BASELINE" = true ]; then
    echo -e "Compared with baseline: ${GREEN}Yes${NC}"
else
    echo -e "Compared with baseline: ${YELLOW}No${NC} (use --compare to compare)"
fi

echo ""
echo -e "${GREEN}Done!${NC}"
