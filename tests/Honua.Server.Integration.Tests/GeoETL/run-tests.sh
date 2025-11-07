#!/bin/bash
# GeoETL Integration Tests Runner
# Convenience script for running GeoETL integration tests

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}=== GeoETL Integration Tests ===${NC}"

# Check if PostgreSQL is running
check_postgres() {
    echo "Checking PostgreSQL connection..."
    if pg_isready -h ${POSTGRES_HOST:-localhost} -p ${POSTGRES_PORT:-5432} > /dev/null 2>&1; then
        echo -e "${GREEN}✓ PostgreSQL is running${NC}"
        return 0
    else
        echo -e "${YELLOW}⚠ PostgreSQL not detected${NC}"
        return 1
    fi
}

# Start PostgreSQL with Docker if needed
start_postgres() {
    echo "Starting PostgreSQL with Docker..."
    docker run -d --name honua-geoetl-test-db \
        -e POSTGRES_PASSWORD=testpass \
        -e POSTGRES_DB=honua_test \
        -p 5432:5432 \
        postgis/postgis:16-3.4-alpine || true

    echo "Waiting for PostgreSQL to start..."
    sleep 10

    if pg_isready -h localhost -p 5432 > /dev/null 2>&1; then
        echo -e "${GREEN}✓ PostgreSQL started successfully${NC}"
    else
        echo -e "${RED}✗ Failed to start PostgreSQL${NC}"
        exit 1
    fi
}

# Run tests based on category
run_tests() {
    local category=$1
    local description=$2

    echo ""
    echo -e "${YELLOW}Running ${description}...${NC}"

    dotnet test \
        --filter "Category=Integration&Category=GeoETL${category}" \
        --logger "console;verbosity=normal" \
        --results-directory ./TestResults \
        -- RunConfiguration.MaxCpuCount=4

    if [ $? -eq 0 ]; then
        echo -e "${GREEN}✓ ${description} passed${NC}"
    else
        echo -e "${RED}✗ ${description} failed${NC}"
        exit 1
    fi
}

# Parse command line arguments
MODE=${1:-all}

case $MODE in
    setup)
        echo "Setting up test environment..."
        if ! check_postgres; then
            start_postgres
        fi
        echo -e "${GREEN}✓ Setup complete${NC}"
        ;;

    execution)
        check_postgres || start_postgres
        run_tests "&FullyQualifiedName~WorkflowExecutionIntegrationTests" "Workflow Execution Tests"
        ;;

    storage)
        check_postgres || start_postgres
        run_tests "&FullyQualifiedName~WorkflowStorageIntegrationTests" "Storage Tests"
        ;;

    formats)
        check_postgres || start_postgres
        run_tests "&FullyQualifiedName~GdalFormatIntegrationTests" "GDAL Format Tests"
        ;;

    scenarios)
        check_postgres || start_postgres
        run_tests "&FullyQualifiedName~EndToEndScenarioTests" "End-to-End Scenario Tests"
        ;;

    ai)
        check_postgres || start_postgres
        run_tests "&FullyQualifiedName~AiGenerationIntegrationTests" "AI Generation Tests"
        ;;

    performance)
        check_postgres || start_postgres
        run_tests "&Category=Performance" "Performance Tests"
        ;;

    all)
        check_postgres || start_postgres
        echo -e "${YELLOW}Running ALL GeoETL Integration Tests...${NC}"

        dotnet test \
            --filter "Category=Integration&Category=GeoETL" \
            --logger "console;verbosity=normal" \
            --logger "trx;LogFileName=geoetl-test-results.trx" \
            --collect:"XPlat Code Coverage" \
            --results-directory ./TestResults

        if [ $? -eq 0 ]; then
            echo -e "${GREEN}✓ All tests passed${NC}"

            # Generate coverage report if reportgenerator is available
            if command -v reportgenerator &> /dev/null; then
                echo "Generating coverage report..."
                reportgenerator \
                    -reports:"./TestResults/**/coverage.cobertura.xml" \
                    -targetdir:"./TestResults/CoverageReport" \
                    -reporttypes:Html
                echo -e "${GREEN}✓ Coverage report generated at ./TestResults/CoverageReport${NC}"
            fi
        else
            echo -e "${RED}✗ Some tests failed${NC}"
            exit 1
        fi
        ;;

    cleanup)
        echo "Cleaning up test environment..."
        docker stop honua-geoetl-test-db 2>/dev/null || true
        docker rm honua-geoetl-test-db 2>/dev/null || true
        rm -rf ./TestResults
        echo -e "${GREEN}✓ Cleanup complete${NC}"
        ;;

    *)
        echo "Usage: $0 {setup|execution|storage|formats|scenarios|ai|performance|all|cleanup}"
        echo ""
        echo "Commands:"
        echo "  setup       - Set up test environment (PostgreSQL)"
        echo "  execution   - Run workflow execution tests"
        echo "  storage     - Run storage/database tests"
        echo "  formats     - Run GDAL format tests"
        echo "  scenarios   - Run end-to-end scenario tests"
        echo "  ai          - Run AI generation tests"
        echo "  performance - Run performance tests"
        echo "  all         - Run all integration tests (default)"
        echo "  cleanup     - Clean up test environment and results"
        exit 1
        ;;
esac

echo ""
echo -e "${GREEN}=== Complete ===${NC}"
