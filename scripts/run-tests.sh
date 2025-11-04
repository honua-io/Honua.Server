#!/bin/bash
set -euo pipefail

# Comprehensive Test Execution Script for Honua
# Supports: Unit, Integration, E2E, Performance, and Security tests

# Configuration
TEST_TYPE="${1:-all}"
CONFIGURATION="${CONFIGURATION:-Release}"
OUTPUT_DIR="${OUTPUT_DIR:-./TestResults}"
PARALLEL="${PARALLEL:-true}"
COVERAGE="${COVERAGE:-true}"
VERBOSE="${VERBOSE:-false}"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

log_step() {
    echo -e "${BLUE}[STEP]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log_step "Checking prerequisites..."

    if ! command -v dotnet &> /dev/null; then
        log_error ".NET SDK not installed"
        exit 1
    fi

    dotnet --version

    # Create output directory
    mkdir -p "$OUTPUT_DIR"

    log_info "Prerequisites check passed"
}

# Build solution
build_solution() {
    log_step "Building solution..."

    dotnet build Honua.sln \
        --configuration "$CONFIGURATION" \
        --nologo

    if [ $? -eq 0 ]; then
        log_info "Build successful"
    else
        log_error "Build failed"
        exit 1
    fi
}

# Run unit tests
run_unit_tests() {
    log_step "Running unit tests..."

    local test_args=(
        --configuration "$CONFIGURATION"
        --no-build
        --logger "trx;LogFileName=unit-tests.trx"
        --results-directory "$OUTPUT_DIR/unit"
        --filter "Category!=Integration&Category!=E2E&Category!=Performance"
    )

    if [ "$COVERAGE" = "true" ]; then
        test_args+=(--collect:"XPlat Code Coverage")
    fi

    if [ "$VERBOSE" = "true" ]; then
        test_args+=(--verbosity detailed)
    else
        test_args+=(--verbosity normal)
    fi

    if [ "$PARALLEL" = "true" ]; then
        test_args+=(--parallel)
    fi

    # Run tests excluding integration and E2E tests
    dotnet test \
        tests/**/*Tests.csproj \
        --filter "FullyQualifiedName!~Integration&FullyQualifiedName!~E2E" \
        "${test_args[@]}"

    local exit_code=$?

    if [ $exit_code -eq 0 ]; then
        log_info "Unit tests passed ✓"
    else
        log_error "Unit tests failed ✗"
        return $exit_code
    fi
}

# Run integration tests
run_integration_tests() {
    log_step "Running integration tests..."

    # Start dependencies with Docker Compose
    if [ -f "docker-compose.test.yml" ]; then
        log_info "Starting test dependencies..."
        docker-compose -f docker-compose.test.yml up -d postgres redis
        sleep 10
    fi

    local test_args=(
        --configuration "$CONFIGURATION"
        --no-build
        --logger "trx;LogFileName=integration-tests.trx"
        --results-directory "$OUTPUT_DIR/integration"
        --filter "Category=Integration"
    )

    if [ "$COVERAGE" = "true" ]; then
        test_args+=(--collect:"XPlat Code Coverage")
    fi

    if [ "$VERBOSE" = "true" ]; then
        test_args+=(--verbosity detailed)
    fi

    # Set connection strings for integration tests
    export ConnectionStrings__DefaultConnection="Host=localhost;Port=5432;Database=honua_test;Username=testuser;Password=testpassword"
    export ConnectionStrings__Redis="localhost:6379"

    # Run integration tests
    dotnet test \
        tests/**/*Integration.Tests.csproj \
        "${test_args[@]}"

    local exit_code=$?

    # Cleanup
    if [ -f "docker-compose.test.yml" ]; then
        log_info "Stopping test dependencies..."
        docker-compose -f docker-compose.test.yml down -v
    fi

    if [ $exit_code -eq 0 ]; then
        log_info "Integration tests passed ✓"
    else
        log_error "Integration tests failed ✗"
        return $exit_code
    fi
}

# Run E2E tests
run_e2e_tests() {
    log_step "Running end-to-end tests..."

    # Start application with Docker Compose
    log_info "Starting application for E2E tests..."
    docker-compose -f docker-compose.test.yml up -d

    # Wait for application to be ready
    log_info "Waiting for application..."
    for i in {1..60}; do
        if curl -sSf http://localhost:8080/healthz/ready > /dev/null 2>&1; then
            log_info "Application is ready"
            break
        fi
        if [ $i -eq 60 ]; then
            log_error "Application did not become ready in time"
            docker-compose -f docker-compose.test.yml logs
            docker-compose -f docker-compose.test.yml down -v
            exit 1
        fi
        sleep 2
    done

    local test_args=(
        --configuration "$CONFIGURATION"
        --logger "trx;LogFileName=e2e-tests.trx"
        --results-directory "$OUTPUT_DIR/e2e"
        --filter "Category=E2E"
    )

    if [ "$VERBOSE" = "true" ]; then
        test_args+=(--verbosity detailed)
    fi

    # Set E2E test environment variables
    export E2E_BASE_URL="http://localhost:8080"

    # Run E2E tests
    dotnet test \
        tests/**/*E2ETests.csproj \
        "${test_args[@]}"

    local exit_code=$?

    # Cleanup
    log_info "Stopping application..."
    docker-compose -f docker-compose.test.yml down -v

    if [ $exit_code -eq 0 ]; then
        log_info "E2E tests passed ✓"
    else
        log_error "E2E tests failed ✗"
        return $exit_code
    fi
}

# Run performance tests
run_performance_tests() {
    log_step "Running performance tests..."

    if [ ! -d "benchmarks" ]; then
        log_warn "No benchmark projects found, skipping performance tests"
        return 0
    fi

    dotnet run \
        --project benchmarks/**/*.csproj \
        --configuration Release \
        -- --artifacts "$OUTPUT_DIR/benchmarks"

    local exit_code=$?

    if [ $exit_code -eq 0 ]; then
        log_info "Performance tests completed ✓"
    else
        log_error "Performance tests failed ✗"
        return $exit_code
    fi
}

# Run security tests
run_security_tests() {
    log_step "Running security tests..."

    # SAST with Semgrep
    if command -v semgrep &> /dev/null; then
        log_info "Running Semgrep SAST scan..."
        semgrep --config auto --json --output "$OUTPUT_DIR/semgrep-results.json" src/ || true
    else
        log_warn "Semgrep not installed, skipping SAST"
    fi

    # Dependency vulnerability scan
    log_info "Scanning for vulnerable dependencies..."
    dotnet list package --vulnerable --include-transitive > "$OUTPUT_DIR/dependency-scan.txt" || true

    log_info "Security tests completed ✓"
}

# Generate code coverage report
generate_coverage_report() {
    if [ "$COVERAGE" != "true" ]; then
        return 0
    fi

    log_step "Generating code coverage report..."

    if ! command -v reportgenerator &> /dev/null; then
        log_info "Installing ReportGenerator..."
        dotnet tool install --global dotnet-reportgenerator-globaltool || true
    fi

    # Find all coverage files
    local coverage_files=$(find "$OUTPUT_DIR" -name "coverage.cobertura.xml" -o -name "*.coverage")

    if [ -z "$coverage_files" ]; then
        log_warn "No coverage files found"
        return 0
    fi

    # Generate report
    reportgenerator \
        "-reports:$OUTPUT_DIR/**/coverage.cobertura.xml" \
        "-targetdir:$OUTPUT_DIR/coverage" \
        "-reporttypes:Html;HtmlSummary;Badges;Cobertura" \
        "-sourcedirs:src" || true

    log_info "Coverage report generated at: $OUTPUT_DIR/coverage/index.html"

    # Display coverage summary
    if [ -f "$OUTPUT_DIR/coverage/Summary.txt" ]; then
        echo ""
        cat "$OUTPUT_DIR/coverage/Summary.txt"
        echo ""
    fi
}

# Generate test summary
generate_test_summary() {
    log_step "Generating test summary..."

    local summary_file="$OUTPUT_DIR/test-summary.md"

    cat > "$summary_file" << EOF
# Test Summary

**Date:** $(date)
**Configuration:** $CONFIGURATION

## Test Results

EOF

    # Count test results
    local total_tests=0
    local passed_tests=0
    local failed_tests=0
    local skipped_tests=0

    for trx_file in $(find "$OUTPUT_DIR" -name "*.trx"); do
        if [ -f "$trx_file" ]; then
            # Parse TRX file (simplified)
            local file_total=$(grep -c "<UnitTestResult" "$trx_file" 2>/dev/null || echo 0)
            local file_passed=$(grep -c 'outcome="Passed"' "$trx_file" 2>/dev/null || echo 0)
            local file_failed=$(grep -c 'outcome="Failed"' "$trx_file" 2>/dev/null || echo 0)
            local file_skipped=$(grep -c 'outcome="NotExecuted"' "$trx_file" 2>/dev/null || echo 0)

            ((total_tests += file_total))
            ((passed_tests += file_passed))
            ((failed_tests += file_failed))
            ((skipped_tests += file_skipped))
        fi
    done

    cat >> "$summary_file" << EOF
- **Total Tests:** $total_tests
- **Passed:** $passed_tests ✓
- **Failed:** $failed_tests ✗
- **Skipped:** $skipped_tests

## Test Types Run

EOF

    [ -d "$OUTPUT_DIR/unit" ] && echo "- Unit Tests ✓" >> "$summary_file"
    [ -d "$OUTPUT_DIR/integration" ] && echo "- Integration Tests ✓" >> "$summary_file"
    [ -d "$OUTPUT_DIR/e2e" ] && echo "- E2E Tests ✓" >> "$summary_file"
    [ -d "$OUTPUT_DIR/benchmarks" ] && echo "- Performance Tests ✓" >> "$summary_file"

    log_info "Test summary saved to: $summary_file"

    # Display summary
    echo ""
    cat "$summary_file"
}

# Main execution
main() {
    log_info "Starting test execution..."
    log_info "Test Type: $TEST_TYPE"
    log_info "Configuration: $CONFIGURATION"
    log_info "Output Directory: $OUTPUT_DIR"
    echo ""

    check_prerequisites
    build_solution

    local exit_code=0

    case "$TEST_TYPE" in
        unit)
            run_unit_tests || exit_code=$?
            ;;
        integration)
            run_integration_tests || exit_code=$?
            ;;
        e2e)
            run_e2e_tests || exit_code=$?
            ;;
        performance)
            run_performance_tests || exit_code=$?
            ;;
        security)
            run_security_tests || exit_code=$?
            ;;
        all)
            run_unit_tests || exit_code=$?
            run_integration_tests || exit_code=$?
            run_e2e_tests || exit_code=$?
            run_performance_tests || exit_code=$?
            run_security_tests || exit_code=$?
            ;;
        *)
            log_error "Unknown test type: $TEST_TYPE"
            usage
            exit 1
            ;;
    esac

    generate_coverage_report
    generate_test_summary

    echo ""
    if [ $exit_code -eq 0 ]; then
        log_info "All tests completed successfully! ✓"
    else
        log_error "Some tests failed! ✗"
    fi

    exit $exit_code
}

# Usage
usage() {
    cat << EOF
Usage: $0 [TEST_TYPE]

Run Honua tests

TEST TYPES:
    unit            Run unit tests only
    integration     Run integration tests only
    e2e            Run end-to-end tests only
    performance    Run performance tests only
    security       Run security tests only
    all            Run all tests (default)

ENVIRONMENT VARIABLES:
    CONFIGURATION  Build configuration (default: Release)
    OUTPUT_DIR     Test results directory (default: ./TestResults)
    PARALLEL       Run tests in parallel (default: true)
    COVERAGE       Collect code coverage (default: true)
    VERBOSE        Verbose output (default: false)

EXAMPLES:
    $0                         # Run all tests
    $0 unit                    # Run unit tests only
    COVERAGE=true $0 unit      # Run unit tests with coverage

EOF
}

# Parse arguments
if [ $# -gt 0 ] && [ "$1" = "--help" ]; then
    usage
    exit 0
fi

main
