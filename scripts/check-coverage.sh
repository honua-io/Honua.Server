#!/bin/bash
# check-coverage.sh - Local code coverage analysis and threshold checking
# Usage: ./scripts/check-coverage.sh [--threshold-only]

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
COVERAGE_DIR="$PROJECT_ROOT/CoverageReport"
TEST_RESULTS_DIR="$PROJECT_ROOT/TestResults"

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
THRESHOLDS=(
    "Honua.Server.Core:65"
    "Honua.Server.Host:60"
    "Honua.Cli.AI:55"
    "Honua.Cli:50"
)
OVERALL_THRESHOLD=60

print_header() {
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}  Honua Code Coverage Analysis${NC}"
    echo -e "${BLUE}========================================${NC}"
    echo ""
}

print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

print_error() {
    echo -e "${RED}✗ $1${NC}"
}

print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

print_info() {
    echo -e "${BLUE}ℹ $1${NC}"
}

# Check if jq is installed
check_dependencies() {
    if ! command -v jq &> /dev/null; then
        print_error "jq is not installed. Please install it:"
        echo "  Ubuntu/Debian: sudo apt-get install jq"
        echo "  macOS: brew install jq"
        echo "  Windows: choco install jq"
        exit 1
    fi

    if ! command -v bc &> /dev/null; then
        print_error "bc is not installed. Please install it:"
        echo "  Ubuntu/Debian: sudo apt-get install bc"
        echo "  macOS: brew install bc"
        exit 1
    fi
}

# Run tests with coverage
run_tests() {
    print_info "Running tests with coverage collection..."
    echo ""

    cd "$PROJECT_ROOT"

    # Clean previous results
    rm -rf "$TEST_RESULTS_DIR" "$COVERAGE_DIR"

    # Run tests
    dotnet test Honua.sln \
        --configuration Release \
        --collect:"XPlat Code Coverage" \
        --results-directory "$TEST_RESULTS_DIR" \
        --settings coverlet.runsettings \
        --logger "console;verbosity=minimal" \
        --nologo

    echo ""
    print_success "Tests completed"
    echo ""
}

# Generate coverage report
generate_report() {
    print_info "Generating coverage report..."
    echo ""

    # Check if ReportGenerator is installed
    if ! command -v reportgenerator &> /dev/null; then
        print_warning "ReportGenerator not found. Installing..."
        dotnet tool install -g dotnet-reportgenerator-globaltool
        export PATH="$PATH:$HOME/.dotnet/tools"
    fi

    # Generate report
    reportgenerator \
        -reports:"$TEST_RESULTS_DIR/**/coverage.opencover.xml" \
        -targetdir:"$COVERAGE_DIR" \
        -reporttypes:"Html;JsonSummary;Badges;MarkdownSummaryGithub" \
        -assemblyfilters:"-*.Tests;-*.Benchmarks;-DataSeeder;-ProcessFrameworkTest" \
        -classfilters:"-*.Migrations.*;-*.DTO;-*.DTOs.*;-*.Models.Generated.*;-*.Contracts.*;-*GlobalUsings"

    echo ""
    print_success "Coverage report generated at: $COVERAGE_DIR"
    echo ""
}

# Check coverage thresholds
check_thresholds() {
    local summary_file="$COVERAGE_DIR/Summary.json"
    local exit_code=0

    if [ ! -f "$summary_file" ]; then
        print_error "Coverage summary file not found at: $summary_file"
        return 1
    fi

    print_info "Checking coverage thresholds..."
    echo ""

    # Print table header
    printf "%-25s %-12s %-12s %-10s\n" "Project" "Coverage" "Threshold" "Status"
    printf "%-25s %-12s %-12s %-10s\n" "-------" "--------" "---------" "------"

    # Check each project
    for threshold_spec in "${THRESHOLDS[@]}"; do
        IFS=':' read -r project threshold <<< "$threshold_spec"

        # Extract coverage for this project
        coverage=$(jq -r ".coverage.assemblies[] | select(.name | contains(\"$project\")) | .linecoverage" "$summary_file" 2>/dev/null | head -1)

        if [ -z "$coverage" ] || [ "$coverage" = "null" ]; then
            printf "%-25s %-12s %-12s " "$project" "N/A" "${threshold}%"
            print_warning "No data"
            continue
        fi

        # Compare to threshold
        if (( $(echo "$coverage >= $threshold" | bc -l) )); then
            printf "%-25s %-12s %-12s " "$project" "${coverage}%" "${threshold}%"
            print_success "Pass"
        else
            printf "%-25s %-12s %-12s " "$project" "${coverage}%" "${threshold}%"
            print_error "Fail"
            exit_code=1
        fi
    done

    echo ""

    # Check overall coverage
    overall_coverage=$(jq -r '.coverage.linecoverage' "$summary_file")
    print_info "Overall Coverage: ${overall_coverage}% (threshold: ${OVERALL_THRESHOLD}%)"

    if (( $(echo "$overall_coverage < $OVERALL_THRESHOLD" | bc -l) )); then
        print_error "Overall coverage is below minimum threshold"
        exit_code=1
    else
        print_success "Overall coverage meets minimum threshold"
    fi

    echo ""

    # Print summary
    if [ $exit_code -eq 0 ]; then
        print_success "All coverage thresholds met!"
    else
        print_error "Some coverage thresholds not met"
        echo ""
        print_info "To improve coverage:"
        echo "  1. Add unit tests for uncovered code"
        echo "  2. Review coverage report: $COVERAGE_DIR/index.html"
        echo "  3. Focus on critical business logic first"
    fi

    return $exit_code
}

# Open coverage report in browser
open_report() {
    local html_report="$COVERAGE_DIR/index.html"

    if [ ! -f "$html_report" ]; then
        print_warning "HTML report not found"
        return
    fi

    print_info "Opening coverage report..."

    # Detect OS and open report
    case "$(uname -s)" in
        Darwin)
            open "$html_report"
            ;;
        Linux)
            xdg-open "$html_report" &> /dev/null || print_warning "Could not open browser automatically"
            ;;
        MINGW*|MSYS*|CYGWIN*)
            start "$html_report"
            ;;
        *)
            print_warning "Unknown OS. Please open manually: $html_report"
            ;;
    esac
}

# Main execution
main() {
    print_header

    # Check for threshold-only mode
    if [ "$1" = "--threshold-only" ]; then
        check_dependencies
        check_thresholds
        exit $?
    fi

    # Full coverage analysis
    check_dependencies
    run_tests
    generate_report

    local threshold_result=0
    check_thresholds || threshold_result=$?

    echo ""
    print_info "HTML Report: $COVERAGE_DIR/index.html"
    print_info "JSON Summary: $COVERAGE_DIR/Summary.json"
    print_info "Markdown: $COVERAGE_DIR/SummaryGithub.md"
    echo ""

    # Ask to open report
    read -p "Open HTML report in browser? (y/n) " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        open_report
    fi

    exit $threshold_result
}

# Run main function
main "$@"
