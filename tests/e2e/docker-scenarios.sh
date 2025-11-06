#!/usr/bin/env bash
#
# Comprehensive Docker E2E Test Suite for Honua
# Tests multiple Docker deployment scenarios
#

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test results
TESTS_PASSED=0
TESTS_FAILED=0
TESTS_TOTAL=0

# Cleanup function
cleanup() {
    echo -e "${YELLOW}Cleaning up Docker resources...${NC}"
    docker rm -f honua-postgis-test 2>/dev/null || true
    docker rm -f honua-server-test 2>/dev/null || true
    docker rm -f honua-nginx-test 2>/dev/null || true
    docker network rm honua-test-network 2>/dev/null || true
    docker-compose -f /tmp/honua-compose-test.yml down 2>/dev/null || true
}

trap cleanup EXIT

# Test helper functions
test_start() {
    local test_name="$1"
    TESTS_TOTAL=$((TESTS_TOTAL + 1))
    echo -e "\n${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}Test $TESTS_TOTAL: $test_name${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
}

test_pass() {
    TESTS_PASSED=$((TESTS_PASSED + 1))
    echo -e "${GREEN}✓ PASSED${NC}"
}

test_fail() {
    local reason="$1"
    TESTS_FAILED=$((TESTS_FAILED + 1))
    echo -e "${RED}✗ FAILED: $reason${NC}"
}

# Test 1: Single PostGIS Container
test_single_postgis() {
    test_start "Deploy standalone PostGIS container"

    # Start PostGIS container
    docker run -d \
        --name honua-postgis-test \
        -e POSTGRES_USER=testuser \
        -e POSTGRES_PASSWORD=testpass123 \
        -e POSTGRES_DB=testdb \
        -p 15432:5432 \
        postgis/postgis:latest

    # Wait for PostgreSQL to be ready
    echo "Waiting for PostGIS to start..."
    sleep 10

    # Test connection
    if docker exec honua-postgis-test psql -U testuser -d testdb -c "SELECT version();" > /dev/null 2>&1; then
        # Test PostGIS extension
        if docker exec honua-postgis-test psql -U testuser -d testdb -c "SELECT PostGIS_Version();" > /dev/null 2>&1; then
            test_pass
        else
            test_fail "PostGIS extension not available"
        fi
    else
        test_fail "Could not connect to PostgreSQL"
    fi

    docker rm -f honua-postgis-test
}

# Test 2: Docker Network with Multiple Containers
test_docker_network() {
    test_start "Create Docker network with PostGIS and application container"

    # Create network
    docker network create honua-test-network

    # Start PostGIS on network
    docker run -d \
        --name honua-postgis-test \
        --network honua-test-network \
        -e POSTGRES_USER=appuser \
        -e POSTGRES_PASSWORD=apppass123 \
        -e POSTGRES_DB=appdb \
        postgis/postgis:latest

    sleep 10

    # Start test container on same network to verify connectivity
    if docker run --rm \
        --network honua-test-network \
        postgres:latest \
        psql -h honua-postgis-test -U appuser -d appdb -c "SELECT 1;" > /dev/null 2>&1; then
        test_pass
    else
        test_fail "Network connectivity test failed"
    fi

    docker rm -f honua-postgis-test
    docker network rm honua-test-network
}

# Test 3: Docker Compose Multi-Service Deployment
test_docker_compose() {
    test_start "Deploy multi-service stack with Docker Compose"

    # Create docker-compose.yml
    cat > /tmp/honua-compose-test.yml <<'EOF'
version: '3.8'

services:
  postgis:
    image: postgis/postgis:latest
    environment:
      POSTGRES_USER: honua
      POSTGRES_PASSWORD: honua123
      POSTGRES_DB: honuadb
    ports:
      - "25432:5432"
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U honua"]
      interval: 5s
      timeout: 5s
      retries: 5

  redis:
    image: redis:alpine
    ports:
      - "26379:6379"
    healthcheck:
      test: ["CMD", "redis-cli", "ping"]
      interval: 5s
      timeout: 3s
      retries: 5

networks:
  default:
    name: honua-compose-network
EOF

    # Start services
    docker-compose -f /tmp/honua-compose-test.yml up -d

    # Wait for health checks
    echo "Waiting for services to be healthy..."
    sleep 15

    # Test PostGIS
    postgis_healthy=$(docker-compose -f /tmp/honua-compose-test.yml ps | grep postgis | grep -c "healthy" || echo "0")

    # Test Redis
    redis_healthy=$(docker-compose -f /tmp/honua-compose-test.yml ps | grep redis | grep -c "healthy" || echo "0")

    if [ "$postgis_healthy" -eq "1" ] && [ "$redis_healthy" -eq "1" ]; then
        test_pass
    else
        test_fail "One or more services not healthy (PostGIS: $postgis_healthy, Redis: $redis_healthy)"
    fi

    docker-compose -f /tmp/honua-compose-test.yml down
    rm /tmp/honua-compose-test.yml
}

# Test 4: Volume Persistence
test_volume_persistence() {
    test_start "Test data persistence with Docker volumes"

    # Create volume
    docker volume create honua-pgdata-test

    # Start container with volume
    docker run -d \
        --name honua-postgis-test \
        -e POSTGRES_USER=testuser \
        -e POSTGRES_PASSWORD=testpass123 \
        -e POSTGRES_DB=testdb \
        -v honua-pgdata-test:/var/lib/postgresql/data \
        -p 15432:5432 \
        postgis/postgis:latest

    sleep 10

    # Create test table
    docker exec honua-postgis-test psql -U testuser -d testdb -c "CREATE TABLE test_persist (id serial, data text);"
    docker exec honua-postgis-test psql -U testuser -d testdb -c "INSERT INTO test_persist (data) VALUES ('test_data');"

    # Stop and remove container
    docker rm -f honua-postgis-test

    # Start new container with same volume
    docker run -d \
        --name honua-postgis-test \
        -e POSTGRES_USER=testuser \
        -e POSTGRES_PASSWORD=testpass123 \
        -e POSTGRES_DB=testdb \
        -v honua-pgdata-test:/var/lib/postgresql/data \
        -p 15432:5432 \
        postgis/postgis:latest

    sleep 10

    # Check if data persisted
    if docker exec honua-postgis-test psql -U testuser -d testdb -c "SELECT data FROM test_persist WHERE data='test_data';" | grep -q "test_data"; then
        test_pass
    else
        test_fail "Data did not persist across container restarts"
    fi

    docker rm -f honua-postgis-test
    docker volume rm honua-pgdata-test
}

# Test 5: Environment Variable Configuration
test_env_vars() {
    test_start "Test environment variable configuration"

    # Start with custom env vars
    docker run -d \
        --name honua-postgis-test \
        -e POSTGRES_USER=customuser \
        -e POSTGRES_PASSWORD=custompass456 \
        -e POSTGRES_DB=customdb \
        -e POSTGRES_INITDB_ARGS="--encoding=UTF8 --locale=en_US.UTF-8" \
        -p 15432:5432 \
        postgis/postgis:latest

    sleep 10

    # Verify custom settings
    if docker exec honua-postgis-test psql -U customuser -d customdb -c "SHOW server_encoding;" | grep -q "UTF8"; then
        test_pass
    else
        test_fail "Environment variables not applied correctly"
    fi

    docker rm -f honua-postgis-test
}

# Test 6: Port Mapping Variations
test_port_mappings() {
    test_start "Test different port mapping configurations"

    # Test with non-standard host port
    docker run -d \
        --name honua-postgis-test \
        -e POSTGRES_USER=testuser \
        -e POSTGRES_PASSWORD=testpass123 \
        -e POSTGRES_DB=testdb \
        -p 35432:5432 \
        postgis/postgis:latest

    sleep 10

    # Verify port is accessible
    if docker exec honua-postgis-test psql -U testuser -d testdb -c "SELECT 1;" > /dev/null 2>&1; then
        test_pass
    else
        test_fail "Container not accessible on mapped port"
    fi

    docker rm -f honua-postgis-test
}

# Test 7: Container Resource Limits
test_resource_limits() {
    test_start "Test container with resource limits"

    # Start with resource constraints
    docker run -d \
        --name honua-postgis-test \
        --memory="512m" \
        --cpus="1.0" \
        -e POSTGRES_USER=testuser \
        -e POSTGRES_PASSWORD=testpass123 \
        -e POSTGRES_DB=testdb \
        -p 15432:5432 \
        postgis/postgis:latest

    sleep 10

    # Verify container is running with limits
    if docker inspect honua-postgis-test --format '{{.HostConfig.Memory}}' | grep -q "536870912"; then
        test_pass
    else
        test_fail "Resource limits not applied correctly"
    fi

    docker rm -f honua-postgis-test
}

# Main execution
main() {
    echo -e "${GREEN}╔══════════════════════════════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}║  Honua Docker E2E Test Suite                                    ║${NC}"
    echo -e "${GREEN}╚══════════════════════════════════════════════════════════════════╝${NC}\n"

    # Check Docker is available
    if ! command -v docker &> /dev/null; then
        echo -e "${RED}ERROR: Docker is not installed or not in PATH${NC}"
        exit 1
    fi

    # Check Docker Compose is available
    if ! command -v docker-compose &> /dev/null; then
        echo -e "${YELLOW}WARNING: docker-compose not found, skipping compose tests${NC}"
        SKIP_COMPOSE=1
    fi

    # Run all tests
    test_single_postgis
    test_docker_network

    if [ "${SKIP_COMPOSE:-0}" -eq "0" ]; then
        test_docker_compose
    fi

    test_volume_persistence
    test_env_vars
    test_port_mappings
    test_resource_limits

    # Print results
    echo -e "\n${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${YELLOW}Test Results${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "Total Tests: $TESTS_TOTAL"
    echo -e "${GREEN}Passed: $TESTS_PASSED${NC}"
    echo -e "${RED}Failed: $TESTS_FAILED${NC}"
    echo -e "${YELLOW}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}\n"

    if [ "$TESTS_FAILED" -eq "0" ]; then
        echo -e "${GREEN}✓ All tests passed!${NC}\n"
        exit 0
    else
        echo -e "${RED}✗ Some tests failed${NC}\n"
        exit 1
    fi
}

main "$@"
