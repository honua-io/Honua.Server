#!/bin/bash
#
# Test script for seeded Honua deployment
# Verifies that all services are running and endpoints are accessible
#

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Configuration
BASE_URL="${HONUA_BASE_URL:-http://localhost:8080}"
TIMEOUT=300  # 5 minutes max wait time
INTERVAL=5   # Check every 5 seconds

echo "=================================================="
echo "Honua Seeded Deployment Test Script"
echo "=================================================="
echo ""

# Function to print colored messages
print_success() {
    echo -e "${GREEN}✓${NC} $1"
}

print_error() {
    echo -e "${RED}✗${NC} $1"
}

print_info() {
    echo -e "${YELLOW}ℹ${NC} $1"
}

# Function to check if URL is accessible
check_url() {
    local url=$1
    local expected_status=${2:-200}

    status_code=$(curl -s -o /dev/null -w "%{http_code}" "$url" 2>/dev/null || echo "000")

    if [ "$status_code" = "$expected_status" ]; then
        return 0
    else
        return 1
    fi
}

# Function to wait for service
wait_for_service() {
    local url=$1
    local service_name=$2
    local elapsed=0

    print_info "Waiting for $service_name to be ready..."

    while [ $elapsed -lt $TIMEOUT ]; do
        if check_url "$url"; then
            print_success "$service_name is ready!"
            return 0
        fi

        sleep $INTERVAL
        elapsed=$((elapsed + INTERVAL))
        echo -n "."
    done

    echo ""
    print_error "$service_name failed to start within ${TIMEOUT}s"
    return 1
}

# Function to test endpoint
test_endpoint() {
    local url=$1
    local name=$2
    local expected_status=${3:-200}

    if check_url "$url" "$expected_status"; then
        print_success "$name: OK"
        return 0
    else
        print_error "$name: FAILED"
        return 1
    fi
}

# Check if Docker Compose is running
echo "Checking Docker Compose services..."
if ! docker-compose -f docker-compose.seed.yml ps | grep -q "honua-server-seed"; then
    print_error "Services are not running. Start them with:"
    echo "    docker-compose -f docker-compose.seed.yml up -d"
    exit 1
fi
print_success "Docker Compose services are running"
echo ""

# Wait for health endpoint
echo "Waiting for Honua server to be healthy..."
wait_for_service "$BASE_URL/healthz/live" "Honua Server (liveness)"
wait_for_service "$BASE_URL/healthz/ready" "Honua Server (readiness)"
echo ""

# Test health endpoints
echo "Testing health endpoints..."
test_endpoint "$BASE_URL/health" "Comprehensive health check"
test_endpoint "$BASE_URL/healthz/live" "Liveness probe"
test_endpoint "$BASE_URL/healthz/ready" "Readiness probe"
test_endpoint "$BASE_URL/healthz/startup" "Startup probe"
echo ""

# Test OGC API endpoints
echo "Testing OGC API - Features endpoints..."
test_endpoint "$BASE_URL/" "OGC API landing page"
test_endpoint "$BASE_URL/conformance" "OGC API conformance"
test_endpoint "$BASE_URL/collections" "OGC API collections"
echo ""

# Test WFS endpoints
echo "Testing WFS endpoints..."
test_endpoint "$BASE_URL/wfs?service=WFS&version=2.0.0&request=GetCapabilities" "WFS GetCapabilities"
echo ""

# Test WMS endpoints
echo "Testing WMS endpoints..."
test_endpoint "$BASE_URL/wms?service=WMS&version=1.3.0&request=GetCapabilities" "WMS GetCapabilities"
echo ""

# Test STAC endpoints
echo "Testing STAC endpoints..."
test_endpoint "$BASE_URL/stac" "STAC landing page"
test_endpoint "$BASE_URL/stac/collections" "STAC collections"
echo ""

# Test GeoServices REST endpoints
echo "Testing GeoServices REST endpoints..."
test_endpoint "$BASE_URL/rest/services" "GeoServices REST catalog"
echo ""

# Check if seeding completed
echo "Checking if database seeding completed..."
SEED_LOGS=$(docker-compose -f docker-compose.seed.yml logs seed-loader 2>&1)

if echo "$SEED_LOGS" | grep -q "Database seeding completed successfully"; then
    print_success "Database seeding completed successfully"
elif echo "$SEED_LOGS" | grep -q "Seeding failed"; then
    print_error "Database seeding failed"
    echo "$SEED_LOGS" | tail -20
    exit 1
else
    print_info "Database seeding may still be in progress"
    print_info "Check logs with: docker-compose -f docker-compose.seed.yml logs seed-loader"
fi
echo ""

# Database connectivity test
echo "Testing database connectivity..."
if docker exec honua-postgres-seed pg_isready -U honua -d honua > /dev/null 2>&1; then
    print_success "PostgreSQL is ready and accepting connections"
else
    print_error "PostgreSQL is not responding"
    exit 1
fi

# Check PostGIS extension
echo "Checking PostGIS extension..."
POSTGIS_VERSION=$(docker exec honua-postgres-seed psql -U honua -d honua -t -c "SELECT PostGIS_version();" 2>/dev/null | xargs)
if [ -n "$POSTGIS_VERSION" ]; then
    print_success "PostGIS is installed: $POSTGIS_VERSION"
else
    print_error "PostGIS extension is not available"
    exit 1
fi
echo ""

# Test data queries (if seeding completed)
if echo "$SEED_LOGS" | grep -q "Database seeding completed successfully"; then
    echo "Testing seeded data..."

    # Query feature count
    FEATURE_COUNT=$(docker exec honua-postgres-seed psql -U honua -d honua -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema='public' AND table_type='BASE TABLE';" 2>/dev/null | xargs)
    if [ -n "$FEATURE_COUNT" ] && [ "$FEATURE_COUNT" -gt 0 ]; then
        print_success "Found $FEATURE_COUNT tables in the database"
    else
        print_error "No tables found in the database"
    fi
    echo ""
fi

# Summary
echo "=================================================="
echo "Test Summary"
echo "=================================================="
print_success "All critical tests passed!"
echo ""
echo "Your Honua seeded instance is ready to use!"
echo ""
echo "Available endpoints:"
echo "  - OGC API:  $BASE_URL/"
echo "  - Health:   $BASE_URL/health"
echo "  - WFS:      $BASE_URL/wfs"
echo "  - WMS:      $BASE_URL/wms"
echo "  - STAC:     $BASE_URL/stac"
echo "  - REST API: $BASE_URL/rest"
echo ""
echo "Database access:"
echo "  docker exec -it honua-postgres-seed psql -U honua -d honua"
echo ""
echo "View logs:"
echo "  docker-compose -f docker-compose.seed.yml logs -f"
echo ""

exit 0
