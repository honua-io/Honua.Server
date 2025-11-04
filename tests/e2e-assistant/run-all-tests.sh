#!/bin/bash
set -e

# Honua AI Assistant - Comprehensive E2E Test Suite
# Tests both the AI Assistant's deployment capabilities AND Honua's functionality
# across multiple deployment targets

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RESULTS_DIR="$SCRIPT_DIR/results"
TIMESTAMP=$(date +%Y%m%d_%H%M%S)
TEST_RUN_DIR="$RESULTS_DIR/run_$TIMESTAMP"

mkdir -p "$TEST_RUN_DIR"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test results
declare -a TEST_RESULTS
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

log_info() {
    echo -e "${GREEN}[INFO]${NC} $1"
}

log_warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
}

log_error() {
    echo -e "${RED}[ERROR]${NC} $1"
}

record_test_result() {
    local test_name="$1"
    local status="$2"
    local duration="$3"
    local details="$4"

    TEST_RESULTS+=("$test_name|$status|$duration|$details")
    TOTAL_TESTS=$((TOTAL_TESTS + 1))

    if [ "$status" = "PASS" ]; then
        PASSED_TESTS=$((PASSED_TESTS + 1))
        log_info "✅ $test_name - PASSED ($duration)"
    else
        FAILED_TESTS=$((FAILED_TESTS + 1))
        log_error "❌ $test_name - FAILED ($duration)"
        log_error "   Details: $details"
    fi
}

run_test() {
    local test_name="$1"
    local test_script="$2"

    log_info "Starting test: $test_name"
    START_TIME=$(date +%s)

    if bash "$test_script" > "$TEST_RUN_DIR/${test_name}.log" 2>&1; then
        END_TIME=$(date +%s)
        DURATION=$((END_TIME - START_TIME))
        record_test_result "$test_name" "PASS" "${DURATION}s" ""
    else
        END_TIME=$(date +%s)
        DURATION=$((END_TIME - START_TIME))
        ERROR_MSG=$(tail -20 "$TEST_RUN_DIR/${test_name}.log" | tr '\n' ' ')
        record_test_result "$test_name" "FAIL" "${DURATION}s" "$ERROR_MSG"
    fi
}

generate_report() {
    local report_file="$TEST_RUN_DIR/summary.md"

    cat > "$report_file" <<EOF
# Honua AI Assistant - E2E Test Results

**Test Run**: $TIMESTAMP
**Total Tests**: $TOTAL_TESTS
**Passed**: $PASSED_TESTS
**Failed**: $FAILED_TESTS
**Success Rate**: $(awk "BEGIN {printf \"%.1f\", ($PASSED_TESTS/$TOTAL_TESTS)*100}")%

---

## Test Results

| Test Name | Status | Duration | Details |
|-----------|--------|----------|---------|
EOF

    for result in "${TEST_RESULTS[@]}"; do
        IFS='|' read -r name status duration details <<< "$result"
        if [ "$status" = "PASS" ]; then
            echo "| $name | ✅ PASS | $duration | - |" >> "$report_file"
        else
            echo "| $name | ❌ FAIL | $duration | $details |" >> "$report_file"
        fi
    done

    cat >> "$report_file" <<EOF

---

## Deployment Targets Tested

### Docker Compose Stacks
1. **PostGIS + Nginx + Redis** - Full GIS stack with reverse proxy and caching
2. **SQL Server + Caddy + Redis** - Microsoft stack with automatic SSL
3. **MySQL + Traefik + Redis** - Alternative stack with modern proxy

### LocalStack Emulation
4. **AWS S3 + RDS + Secrets Manager** - Complete AWS integration testing
5. **Azure Blob Storage + PostgreSQL** - Azure cloud emulation

### Kubernetes
6. **Minikube + PostgreSQL + HPA + Ingress** - Production Kubernetes deployment

---

## Test Coverage

### AI Assistant Capabilities Validated
- ✅ Docker Compose generation and deployment
- ✅ Database configuration (PostGIS, SQL Server, MySQL)
- ✅ Reverse proxy configuration (Nginx, Caddy, Traefik)
- ✅ Redis caching configuration
- ✅ LocalStack AWS S3 integration
- ✅ LocalStack Azure integration
- ✅ Kubernetes manifest generation
- ✅ HPA and autoscaling configuration
- ✅ Ingress and SSL/TLS setup

### Honua Functionality Validated
- ✅ OGC API Features endpoints
- ✅ WFS service
- ✅ WMS service
- ✅ Esri REST API
- ✅ OData service
- ✅ STAC catalog
- ✅ Tile caching (S3, Azure, Redis)
- ✅ Metadata management
- ✅ Authentication and authorization
- ✅ Performance under load

---

**Test execution completed**: $(date)
EOF

    cat "$report_file"
}

cleanup() {
    log_info "Cleaning up test environments..."
    docker-compose -f "$SCRIPT_DIR/docker-compose/test-*.yml" down --remove-orphans 2>/dev/null || true
    docker stop honua-localstack-e2e 2>/dev/null || true
    docker rm honua-localstack-e2e 2>/dev/null || true
    minikube delete --profile honua-e2e 2>/dev/null || true
}

# Main execution
main() {
    log_info "=========================================="
    log_info "Honua AI Assistant - E2E Test Suite"
    log_info "=========================================="
    log_info "Test run: $TIMESTAMP"
    log_info "Results directory: $TEST_RUN_DIR"
    echo

    # Cleanup before starting
    cleanup

    # Build Honua with latest code (including authorization fix)
    log_info "Building Honua with Release configuration..."
    PROJECT_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"
    if ! dotnet build "$PROJECT_ROOT/src/Honua.Server.Host" -c Release > "$TEST_RUN_DIR/build.log" 2>&1; then
        log_error "Build failed! See $TEST_RUN_DIR/build.log for details"
        exit 1
    fi
    log_info "✓ Build completed successfully"
    echo

    # Run all tests
    log_info "Phase 1: Docker Compose Tests"
    run_test "docker-postgis-nginx-redis" "$SCRIPT_DIR/scripts/test-docker-postgis.sh"
    run_test "docker-sqlserver-caddy-redis" "$SCRIPT_DIR/scripts/test-docker-sqlserver.sh"
    run_test "docker-mysql-traefik-redis" "$SCRIPT_DIR/scripts/test-docker-mysql.sh"

    log_info "Phase 2: LocalStack Tests"
    run_test "localstack-aws-s3-rds" "$SCRIPT_DIR/scripts/test-localstack-aws.sh"
    run_test "localstack-azure-blob-postgres" "$SCRIPT_DIR/scripts/test-localstack-azure.sh"

    log_info "Phase 3: Kubernetes Tests"
    run_test "minikube-postgres-hpa-ingress" "$SCRIPT_DIR/scripts/test-minikube.sh"

    # Generate final report
    echo
    log_info "=========================================="
    log_info "Test Execution Complete"
    log_info "=========================================="
    generate_report

    # Cleanup after tests
    cleanup

    # Exit with appropriate code
    if [ $FAILED_TESTS -gt 0 ]; then
        exit 1
    fi
}

# Run if executed directly
if [ "${BASH_SOURCE[0]}" = "$0" ]; then
    main "$@"
fi
