#!/usr/bin/env bash
#
# test-integration-locally.sh - Run integration tests locally with emulators
#
# This script replicates the CI/CD integration test workflow locally for
# development and debugging purposes.
#
# Usage:
#   ./scripts/test-integration-locally.sh [options]
#
# Options:
#   --unit-only          Run only unit tests
#   --integration-only   Run only integration tests
#   --skip-build         Skip build step (use existing build)
#   --no-cleanup         Don't stop emulators after tests
#   --help               Show this help message

set -euo pipefail

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "${SCRIPT_DIR}/.." && pwd)"
DOCKER_COMPOSE_FILE="${PROJECT_ROOT}/tests/Honua.Server.Core.Tests/docker-compose.storage-emulators.yml"
SOLUTION_FILE="${PROJECT_ROOT}/Honua.sln"

# Default options
RUN_UNIT=true
RUN_INTEGRATION=true
SKIP_BUILD=false
CLEANUP=true

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m'

# Logging
log_info() { echo -e "${BLUE}[INFO]${NC} $*"; }
log_success() { echo -e "${GREEN}[SUCCESS]${NC} $*"; }
log_warning() { echo -e "${YELLOW}[WARNING]${NC} $*"; }
log_error() { echo -e "${RED}[ERROR]${NC} $*"; }
log_section() { echo -e "\n${BLUE}==== $* ====${NC}\n"; }

# Parse arguments
parse_args() {
    while [[ $# -gt 0 ]]; do
        case $1 in
            --unit-only)
                RUN_UNIT=true
                RUN_INTEGRATION=false
                shift
                ;;
            --integration-only)
                RUN_UNIT=false
                RUN_INTEGRATION=true
                shift
                ;;
            --skip-build)
                SKIP_BUILD=true
                shift
                ;;
            --no-cleanup)
                CLEANUP=false
                shift
                ;;
            --help)
                show_help
                exit 0
                ;;
            *)
                log_error "Unknown option: $1"
                show_help
                exit 1
                ;;
        esac
    done
}

show_help() {
    cat << EOF
test-integration-locally.sh - Run integration tests locally with emulators

This script replicates the CI/CD integration test workflow locally for
development and debugging purposes.

Usage:
  ./scripts/test-integration-locally.sh [options]

Options:
  --unit-only          Run only unit tests (skip integration tests)
  --integration-only   Run only integration tests (skip unit tests)
  --skip-build         Skip build step (use existing build)
  --no-cleanup         Don't stop emulators after tests
  --help               Show this help message

Examples:
  # Run all tests (default)
  ./scripts/test-integration-locally.sh

  # Run only unit tests
  ./scripts/test-integration-locally.sh --unit-only

  # Run only integration tests
  ./scripts/test-integration-locally.sh --integration-only

  # Run tests without building (faster for iterations)
  ./scripts/test-integration-locally.sh --skip-build

  # Keep emulators running for debugging
  ./scripts/test-integration-locally.sh --no-cleanup
EOF
}

# Check prerequisites
check_prerequisites() {
    log_section "Checking Prerequisites"

    local missing=()

    if ! command -v docker &> /dev/null; then
        missing+=("docker")
    fi

    if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null; then
        missing+=("docker-compose")
    fi

    if ! command -v dotnet &> /dev/null; then
        missing+=("dotnet")
    fi

    if [ ${#missing[@]} -gt 0 ]; then
        log_error "Missing required tools: ${missing[*]}"
        exit 1
    fi

    log_success "All prerequisites satisfied"
}

# Restore NuGet packages
restore_dependencies() {
    log_section "Restoring Dependencies"
    cd "${PROJECT_ROOT}"
    dotnet restore "${SOLUTION_FILE}"
    log_success "Dependencies restored"
}

# Build solution
build_solution() {
    if [ "$SKIP_BUILD" = true ]; then
        log_section "Skipping Build (--skip-build)"
        return 0
    fi

    log_section "Building Solution"
    cd "${PROJECT_ROOT}"
    dotnet build "${SOLUTION_FILE}" --configuration Release --no-restore
    log_success "Build completed"
}

# Start emulators
start_emulators() {
    log_section "Starting Cloud Storage Emulators"

    cd "$(dirname "${DOCKER_COMPOSE_FILE}")"

    # Stop any existing containers
    docker-compose -f "$(basename "${DOCKER_COMPOSE_FILE}")" down -v &> /dev/null || true

    # Start emulators
    docker-compose -f "$(basename "${DOCKER_COMPOSE_FILE}")" up -d

    # Show container status
    docker-compose -f "$(basename "${DOCKER_COMPOSE_FILE}")" ps

    log_success "Emulators started"
}

# Wait for emulators
wait_for_emulators() {
    log_section "Waiting for Emulators to be Healthy"

    if [ -x "${SCRIPT_DIR}/wait-for-emulators.sh" ]; then
        "${SCRIPT_DIR}/wait-for-emulators.sh"
    else
        log_warning "wait-for-emulators.sh not found, using basic health checks"
        sleep 10
    fi
}

# Run unit tests
run_unit_tests() {
    log_section "Running Unit Tests"

    cd "${PROJECT_ROOT}"

    dotnet test "${SOLUTION_FILE}" \
        --configuration Release \
        --no-build \
        --filter "FullyQualifiedName!~IntegrationTests" \
        --logger "console;verbosity=normal" \
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

    log_success "Unit tests completed"
}

# Run integration tests
run_integration_tests() {
    log_section "Running Integration Tests"

    cd "${PROJECT_ROOT}"

    dotnet test "${SOLUTION_FILE}" \
        --configuration Release \
        --no-build \
        --filter "FullyQualifiedName~IntegrationTests" \
        --logger "console;verbosity=detailed" \
        -- DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencover

    log_success "Integration tests completed"
}

# Stop emulators
stop_emulators() {
    if [ "$CLEANUP" = false ]; then
        log_section "Keeping Emulators Running (--no-cleanup)"
        log_info "To stop emulators manually, run:"
        log_info "  cd $(dirname "${DOCKER_COMPOSE_FILE}") && docker-compose -f $(basename "${DOCKER_COMPOSE_FILE}") down -v"
        return 0
    fi

    log_section "Stopping Emulators"

    cd "$(dirname "${DOCKER_COMPOSE_FILE}")"
    docker-compose -f "$(basename "${DOCKER_COMPOSE_FILE}")" down -v

    log_success "Emulators stopped and cleaned up"
}

# Show emulator logs on failure
show_emulator_logs() {
    log_section "Emulator Logs"

    log_info "LocalStack logs:"
    docker logs honua-storage-test-localstack 2>&1 | tail -50 || log_warning "Could not retrieve LocalStack logs"

    log_info "Azurite logs:"
    docker logs honua-storage-test-azurite 2>&1 | tail -50 || log_warning "Could not retrieve Azurite logs"

    log_info "GCS Emulator logs:"
    docker logs honua-storage-test-gcs 2>&1 | tail -50 || log_warning "Could not retrieve GCS Emulator logs"
}

# Cleanup on exit
cleanup_on_exit() {
    local exit_code=$?

    if [ $exit_code -ne 0 ]; then
        log_error "Tests failed with exit code: $exit_code"
        show_emulator_logs
    fi

    if [ "$CLEANUP" = true ]; then
        stop_emulators
    fi

    exit $exit_code
}

# Main execution
main() {
    parse_args "$@"

    trap cleanup_on_exit EXIT

    log_info "Starting local integration test run"
    log_info "Configuration:"
    log_info "  Unit tests: ${RUN_UNIT}"
    log_info "  Integration tests: ${RUN_INTEGRATION}"
    log_info "  Skip build: ${SKIP_BUILD}"
    log_info "  Cleanup: ${CLEANUP}"
    log_info ""

    check_prerequisites

    restore_dependencies

    build_solution

    if [ "$RUN_INTEGRATION" = true ]; then
        start_emulators
        wait_for_emulators
    fi

    if [ "$RUN_UNIT" = true ]; then
        run_unit_tests
    fi

    if [ "$RUN_INTEGRATION" = true ]; then
        run_integration_tests
    fi

    log_section "Test Run Complete"
    log_success "All tests completed successfully!"

    if [ "$CLEANUP" = false ]; then
        log_info ""
        log_info "Emulators are still running for debugging:"
        log_info "  LocalStack (S3):     http://localhost:4566"
        log_info "  Azurite (Azure):     http://localhost:10000"
        log_info "  GCS Emulator:        http://localhost:4443"
    fi
}

# Run main function
main "$@"
