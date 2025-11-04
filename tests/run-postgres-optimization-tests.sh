#!/bin/bash
# ============================================================================
# Honua GIS Server - PostgreSQL Optimization Tests Runner
# ============================================================================
# This script sets up a test database, runs migrations, loads test data,
# and executes the PostgreSQL optimization tests.
# ============================================================================

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}============================================================${NC}"
echo -e "${BLUE}Honua PostgreSQL Optimization Tests${NC}"
echo -e "${BLUE}============================================================${NC}"

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}Error: Docker is not running${NC}"
    exit 1
fi

# Configuration
COMPOSE_FILE="docker-compose.postgres-optimization-tests.yml"
TEST_DB_CONNECTION="Host=localhost;Port=5433;Database=honua_test;Username=postgres;Password=test"

# Parse command line arguments
RUN_BENCHMARKS=false
SKIP_SETUP=false
CLEANUP_AFTER=false

while [[ $# -gt 0 ]]; do
    case $1 in
        --benchmarks)
            RUN_BENCHMARKS=true
            shift
            ;;
        --skip-setup)
            SKIP_SETUP=true
            shift
            ;;
        --cleanup)
            CLEANUP_AFTER=true
            shift
            ;;
        --help)
            echo "Usage: $0 [OPTIONS]"
            echo ""
            echo "Options:"
            echo "  --benchmarks    Run performance benchmarks after tests"
            echo "  --skip-setup    Skip database setup (assumes already running)"
            echo "  --cleanup       Stop and remove containers after tests"
            echo "  --help          Show this help message"
            exit 0
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

# Function to wait for PostgreSQL to be ready
wait_for_postgres() {
    echo -e "${YELLOW}Waiting for PostgreSQL to be ready...${NC}"
    local max_attempts=30
    local attempt=1

    while [ $attempt -le $max_attempts ]; do
        if docker exec honua-postgres-optimization-test pg_isready -U postgres -d honua_test > /dev/null 2>&1; then
            echo -e "${GREEN}PostgreSQL is ready!${NC}"
            return 0
        fi

        echo -e "${YELLOW}Attempt $attempt/$max_attempts: PostgreSQL not ready yet...${NC}"
        sleep 2
        ((attempt++))
    done

    echo -e "${RED}Error: PostgreSQL failed to start${NC}"
    return 1
}

# Setup database
if [ "$SKIP_SETUP" = false ]; then
    echo -e "${BLUE}Starting PostgreSQL test container...${NC}"
    docker-compose -f "$COMPOSE_FILE" up -d postgres-test

    wait_for_postgres || exit 1

    echo -e "${BLUE}Running migrations...${NC}"
    docker exec -i honua-postgres-optimization-test psql -U postgres -d honua_test < ../src/Honua.Server.Core/Data/Migrations/014_PostgresOptimizations.sql

    echo -e "${BLUE}Loading test data...${NC}"
    docker exec -i honua-postgres-optimization-test psql -U postgres -d honua_test < ./Honua.Server.Integration.Tests/Data/TestData_PostgresOptimizations.sql

    echo -e "${GREEN}Database setup complete!${NC}"
else
    echo -e "${YELLOW}Skipping database setup${NC}"
fi

# Run unit tests
echo -e "${BLUE}============================================================${NC}"
echo -e "${BLUE}Running Unit Tests${NC}"
echo -e "${BLUE}============================================================${NC}"

dotnet test Honua.Server.Core.Tests/Honua.Server.Core.Tests.csproj \
    --filter "FullyQualifiedName~PostgresOptimization" \
    --logger "console;verbosity=normal"

if [ $? -eq 0 ]; then
    echo -e "${GREEN}Unit tests passed!${NC}"
else
    echo -e "${RED}Unit tests failed!${NC}"
    exit 1
fi

# Run integration tests
echo -e "${BLUE}============================================================${NC}"
echo -e "${BLUE}Running Integration Tests${NC}"
echo -e "${BLUE}============================================================${NC}"

export TEST_DATABASE_URL="$TEST_DB_CONNECTION"

dotnet test Honua.Server.Integration.Tests/Honua.Server.Integration.Tests.csproj \
    --filter "Category=PostgresOptimizations" \
    --logger "console;verbosity=normal"

if [ $? -eq 0 ]; then
    echo -e "${GREEN}Integration tests passed!${NC}"
else
    echo -e "${RED}Integration tests failed!${NC}"
    exit 1
fi

# Run benchmarks if requested
if [ "$RUN_BENCHMARKS" = true ]; then
    echo -e "${BLUE}============================================================${NC}"
    echo -e "${BLUE}Running Performance Benchmarks${NC}"
    echo -e "${BLUE}============================================================${NC}"

    export BENCHMARK_DATABASE_URL="$TEST_DB_CONNECTION"

    cd Honua.Server.Benchmarks
    dotnet run -c Release --filter "*PostgresOptimization*"
    cd ..

    echo -e "${GREEN}Benchmarks complete! Check BenchmarkDotNet.Artifacts/ for results.${NC}"
fi

# Cleanup if requested
if [ "$CLEANUP_AFTER" = true ]; then
    echo -e "${BLUE}Cleaning up...${NC}"
    docker-compose -f "$COMPOSE_FILE" down -v
    echo -e "${GREEN}Cleanup complete!${NC}"
else
    echo -e "${YELLOW}PostgreSQL test container is still running.${NC}"
    echo -e "${YELLOW}To stop it, run: docker-compose -f $COMPOSE_FILE down${NC}"
    echo -e "${YELLOW}To connect: psql '$TEST_DB_CONNECTION'${NC}"
fi

echo -e "${BLUE}============================================================${NC}"
echo -e "${GREEN}All tests completed successfully!${NC}"
echo -e "${BLUE}============================================================${NC}"
