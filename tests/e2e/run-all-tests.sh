#!/usr/bin/env bash
#
# Master E2E Test Runner for Honua
# Runs all E2E test suites and generates a comprehensive report
#

set -euo pipefail

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

# Test suite results
SUITE_RESULTS=()

# Script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"

# Run a test suite
run_suite() {
    local suite_name="$1"
    local suite_script="$2"

    echo -e "\n${CYAN}╔════════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${CYAN}║  Running: $suite_name${NC}"
    echo -e "${CYAN}╚════════════════════════════════════════════════════════════════════╝${NC}\n"

    local start_time=$(date +%s)

    if bash "$suite_script"; then
        local end_time=$(date +%s)
        local duration=$((end_time - start_time))
        SUITE_RESULTS+=("${GREEN}✓${NC} $suite_name - PASSED (${duration}s)")
        return 0
    else
        local end_time=$(date +%s)
        local duration=$((end_time - start_time))
        SUITE_RESULTS+=("${RED}✗${NC} $suite_name - FAILED (${duration}s)")
        return 1
    fi
}

# Check prerequisites
check_prerequisites() {
    echo -e "${BLUE}Checking prerequisites...${NC}\n"

    local missing_deps=()

    # Check Docker
    if ! command -v docker &> /dev/null; then
        missing_deps+=("docker")
    else
        echo -e "${GREEN}✓${NC} Docker installed"
    fi

    # Check docker-compose
    if ! command -v docker-compose &> /dev/null; then
        echo -e "${YELLOW}⚠${NC} docker-compose not found (some tests will be skipped)"
    else
        echo -e "${GREEN}✓${NC} docker-compose installed"
    fi

    # Check AWS CLI
    if ! command -v aws &> /dev/null; then
        echo -e "${YELLOW}⚠${NC} AWS CLI not found (LocalStack tests will be skipped)"
    else
        echo -e "${GREEN}✓${NC} AWS CLI installed"
    fi

    # Check kubectl
    if ! command -v kubectl &> /dev/null; then
        echo -e "${YELLOW}⚠${NC} kubectl not found (K8s tests will be skipped)"
    else
        echo -e "${GREEN}✓${NC} kubectl installed"
    fi

    # Check kind
    if ! command -v kind &> /dev/null; then
        echo -e "${YELLOW}⚠${NC} kind not found (K8s tests will be skipped)"
    else
        echo -e "${GREEN}✓${NC} kind installed"
    fi

    if [ ${#missing_deps[@]} -gt 0 ]; then
        echo -e "\n${RED}ERROR: Missing required dependencies: ${missing_deps[*]}${NC}"
        exit 1
    fi

    echo
}

# Print header
print_header() {
    echo -e "${BLUE}╔════════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${BLUE}║                                                                    ║${NC}"
    echo -e "${BLUE}║        Honua Comprehensive E2E Test Suite                          ║${NC}"
    echo -e "${BLUE}║        Testing Docker, LocalStack, and Kubernetes                  ║${NC}"
    echo -e "${BLUE}║                                                                    ║${NC}"
    echo -e "${BLUE}╚════════════════════════════════════════════════════════════════════╝${NC}\n"
}

# Print summary
print_summary() {
    echo -e "\n${CYAN}╔════════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${CYAN}║  Test Suite Summary                                                ║${NC}"
    echo -e "${CYAN}╚════════════════════════════════════════════════════════════════════╝${NC}\n"

    for result in "${SUITE_RESULTS[@]}"; do
        echo -e "$result"
    done

    echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}\n"

    # Count passed and failed
    local passed=0
    local failed=0
    for result in "${SUITE_RESULTS[@]}"; do
        if echo "$result" | grep -q "PASSED"; then
            passed=$((passed + 1))
        else
            failed=$((failed + 1))
        fi
    done

    echo -e "Total Suites: ${#SUITE_RESULTS[@]}"
    echo -e "${GREEN}Passed: $passed${NC}"
    echo -e "${RED}Failed: $failed${NC}"

    echo -e "\n${CYAN}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}\n"

    if [ "$failed" -eq "0" ]; then
        echo -e "${GREEN}╔════════════════════════════════════════════════════════════════════╗${NC}"
        echo -e "${GREEN}║                    ✓ ALL TESTS PASSED!                             ║${NC}"
        echo -e "${GREEN}╚════════════════════════════════════════════════════════════════════╝${NC}\n"
        return 0
    else
        echo -e "${RED}╔════════════════════════════════════════════════════════════════════╗${NC}"
        echo -e "${RED}║                    ✗ SOME TESTS FAILED                             ║${NC}"
        echo -e "${RED}╚════════════════════════════════════════════════════════════════════╝${NC}\n"
        return 1
    fi
}

# Main execution
main() {
    local start_time=$(date +%s)

    print_header
    check_prerequisites

    # Run Docker scenarios
    if [ -f "$SCRIPT_DIR/docker-scenarios.sh" ]; then
        run_suite "Docker Scenarios" "$SCRIPT_DIR/docker-scenarios.sh" || true
    fi

    # Run LocalStack scenarios
    if [ -f "$SCRIPT_DIR/localstack-scenarios.sh" ] && command -v aws &> /dev/null; then
        run_suite "LocalStack AWS Scenarios" "$SCRIPT_DIR/localstack-scenarios.sh" || true
    else
        echo -e "${YELLOW}Skipping LocalStack tests (AWS CLI not found or script missing)${NC}"
    fi

    # Run K8s scenarios
    if [ -f "$SCRIPT_DIR/k8s-scenarios.sh" ] && command -v kind &> /dev/null && command -v kubectl &> /dev/null; then
        run_suite "Kubernetes Scenarios" "$SCRIPT_DIR/k8s-scenarios.sh" || true
    else
        echo -e "${YELLOW}Skipping K8s tests (kind/kubectl not found or script missing)${NC}"
    fi

    local end_time=$(date +%s)
    local total_duration=$((end_time - start_time))

    echo -e "\n${BLUE}Total execution time: ${total_duration}s${NC}\n"

    print_summary
}

# Parse command line arguments
HELP=0
while [[ $# -gt 0 ]]; do
    case $1 in
        -h|--help)
            HELP=1
            shift
            ;;
        *)
            echo "Unknown option: $1"
            HELP=1
            shift
            ;;
    esac
done

if [ "$HELP" -eq "1" ]; then
    echo "Honua E2E Test Runner"
    echo
    echo "Usage: $0"
    echo
    echo "This script runs all available E2E test suites:"
    echo "  - Docker deployment scenarios"
    echo "  - LocalStack AWS service integration"
    echo "  - Kubernetes deployment scenarios"
    echo
    echo "Prerequisites:"
    echo "  - Docker"
    echo "  - docker-compose (optional)"
    echo "  - AWS CLI (for LocalStack tests)"
    echo "  - kubectl and kind (for K8s tests)"
    echo
    exit 0
fi

main "$@"
