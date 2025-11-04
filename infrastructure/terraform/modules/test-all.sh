#!/bin/bash
# Terraform Module Test Runner
# Runs validation and plan tests for all serverless modules

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Counters
TESTS_RUN=0
TESTS_PASSED=0
TESTS_FAILED=0

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Terraform Module Test Runner${NC}"
echo -e "${BLUE}Testing Honua Serverless Modules${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Function to print section header
print_header() {
    echo ""
    echo -e "${BLUE}========================================${NC}"
    echo -e "${BLUE}$1${NC}"
    echo -e "${BLUE}========================================${NC}"
}

# Function to print success
print_success() {
    echo -e "${GREEN}✓ $1${NC}"
}

# Function to print error
print_error() {
    echo -e "${RED}✗ $1${NC}"
}

# Function to print warning
print_warning() {
    echo -e "${YELLOW}⚠ $1${NC}"
}

# Function to run tests for a module
test_module() {
    local module=$1
    local test_type=$2  # unit or integration

    print_header "Testing $module module - $test_type tests"

    local test_dir="$module/tests/$test_type"

    if [ ! -d "$test_dir" ]; then
        print_warning "Test directory $test_dir not found, skipping"
        return 0
    fi

    cd "$test_dir"

    # Terraform init
    echo "Running terraform init..."
    TESTS_RUN=$((TESTS_RUN + 1))
    if terraform init -backend=false -upgrade > /dev/null 2>&1; then
        print_success "terraform init passed"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    else
        print_error "terraform init failed"
        TESTS_FAILED=$((TESTS_FAILED + 1))
        cd - > /dev/null
        return 1
    fi

    # Terraform validate
    echo "Running terraform validate..."
    TESTS_RUN=$((TESTS_RUN + 1))
    if terraform validate; then
        print_success "terraform validate passed"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    else
        print_error "terraform validate failed"
        TESTS_FAILED=$((TESTS_FAILED + 1))
        cd - > /dev/null
        return 1
    fi

    # Terraform fmt check
    echo "Running terraform fmt check..."
    TESTS_RUN=$((TESTS_RUN + 1))
    if terraform fmt -check -recursive; then
        print_success "terraform fmt check passed"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    else
        print_warning "terraform fmt check failed (non-blocking)"
        # Don't count as failure
    fi

    # Terraform plan
    echo "Running terraform plan..."
    TESTS_RUN=$((TESTS_RUN + 1))
    if terraform plan -out=tfplan > /dev/null 2>&1; then
        print_success "terraform plan passed"
        TESTS_PASSED=$((TESTS_PASSED + 1))
    else
        print_error "terraform plan failed"
        TESTS_FAILED=$((TESTS_FAILED + 1))
        cd - > /dev/null
        return 1
    fi

    # Clean up
    rm -f tfplan
    rm -rf .terraform
    rm -f .terraform.lock.hcl

    cd - > /dev/null
    return 0
}

# List of serverless modules to test
MODULES=("cloud-run" "lambda" "container-apps" "cdn")

# Test each module
for module in "${MODULES[@]}"; do
    if [ ! -d "$module" ]; then
        print_warning "Module directory $module not found, skipping"
        continue
    fi

    # Run unit tests
    test_module "$module" "unit" || true

    # Run integration tests
    test_module "$module" "integration" || true
done

# Print summary
echo ""
print_header "Test Summary"
echo "Total tests run: $TESTS_RUN"
echo -e "${GREEN}Passed: $TESTS_PASSED${NC}"
if [ $TESTS_FAILED -gt 0 ]; then
    echo -e "${RED}Failed: $TESTS_FAILED${NC}"
else
    echo -e "${GREEN}Failed: $TESTS_FAILED${NC}"
fi

# Exit with error if any tests failed
if [ $TESTS_FAILED -gt 0 ]; then
    echo ""
    print_error "Some tests failed!"
    exit 1
else
    echo ""
    print_success "All tests passed!"
    exit 0
fi
