#!/bin/bash
# Test script for log aggregation setup
# Validates configuration and tests basic functionality

set -e

SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

echo "======================================"
echo "Log Aggregation Setup Test"
echo "======================================"
echo ""

# Colors
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Test counter
TESTS_PASSED=0
TESTS_FAILED=0

test_file() {
    local file=$1
    local description=$2

    if [ -f "$file" ]; then
        echo -e "${GREEN}✓${NC} $description exists: $file"
        ((TESTS_PASSED++))
        return 0
    else
        echo -e "${RED}✗${NC} $description missing: $file"
        ((TESTS_FAILED++))
        return 1
    fi
}

test_yaml() {
    local file=$1
    local description=$2

    if python3 -c "import yaml; yaml.safe_load(open('$file'))" 2>/dev/null; then
        echo -e "${GREEN}✓${NC} $description is valid YAML"
        ((TESTS_PASSED++))
        return 0
    else
        echo -e "${RED}✗${NC} $description has invalid YAML"
        ((TESTS_FAILED++))
        return 1
    fi
}

test_json() {
    local file=$1
    local description=$2

    if python3 -c "import json; json.load(open('$file'))" 2>/dev/null; then
        echo -e "${GREEN}✓${NC} $description is valid JSON"
        ((TESTS_PASSED++))
        return 0
    else
        echo -e "${RED}✗${NC} $description has invalid JSON"
        ((TESTS_FAILED++))
        return 1
    fi
}

cd "$PROJECT_DIR"

echo "Testing Configuration Files..."
echo "------------------------------"

# Test Loki configuration
test_file "loki/loki-config.yaml" "Loki configuration"
test_yaml "loki/loki-config.yaml" "Loki configuration"

# Test Promtail configuration
test_file "promtail/promtail-config.yaml" "Promtail configuration"
test_yaml "promtail/promtail-config.yaml" "Promtail configuration"

# Test Grafana datasources
test_file "grafana/provisioning/datasources/datasources.yml" "Grafana datasources"
test_yaml "grafana/provisioning/datasources/datasources.yml" "Grafana datasources"

# Test Grafana logs dashboard
test_file "grafana/dashboards/logs-dashboard.json" "Grafana logs dashboard"
test_json "grafana/dashboards/logs-dashboard.json" "Grafana logs dashboard"

# Test docker-compose
test_file "docker-compose.yml" "Docker Compose file"

echo ""
echo "Testing Documentation..."
echo "------------------------"

# Test documentation files
test_file "LOG_AGGREGATION.md" "Log aggregation guide"
test_file "loki/QUERY_EXAMPLES.md" "Query examples guide"
test_file "LOG_AGGREGATION_SUMMARY.md" "Implementation summary"

echo ""
echo "Testing Docker Compose Configuration..."
echo "---------------------------------------"

# Check if docker or docker-compose is available
if command -v docker &> /dev/null; then
    if docker compose version &> /dev/null; then
        if docker compose config --quiet &> /dev/null; then
            echo -e "${GREEN}✓${NC} Docker Compose configuration is valid"
            ((TESTS_PASSED++))
        else
            echo -e "${RED}✗${NC} Docker Compose configuration has errors"
            ((TESTS_FAILED++))
        fi
    elif command -v docker-compose &> /dev/null; then
        if docker-compose config --quiet &> /dev/null; then
            echo -e "${GREEN}✓${NC} Docker Compose configuration is valid"
            ((TESTS_PASSED++))
        else
            echo -e "${RED}✗${NC} Docker Compose configuration has errors"
            ((TESTS_FAILED++))
        fi
    else
        echo -e "${YELLOW}⚠${NC} docker-compose not available, skipping validation"
    fi
else
    echo -e "${YELLOW}⚠${NC} Docker not available, skipping validation"
fi

# Check for required directories
echo ""
echo "Testing Directory Structure..."
echo "------------------------------"

test_file "loki" "Loki directory" || mkdir -p loki
test_file "promtail" "Promtail directory" || mkdir -p promtail
test_file "grafana/dashboards" "Grafana dashboards directory" || mkdir -p grafana/dashboards
test_file "grafana/provisioning/datasources" "Grafana datasources directory" || mkdir -p grafana/provisioning/datasources

echo ""
echo "Testing Loki Configuration Keys..."
echo "-----------------------------------"

# Validate key configuration values
if grep -q "retention_period: 720h" loki/loki-config.yaml; then
    echo -e "${GREEN}✓${NC} Loki retention period set to 30 days"
    ((TESTS_PASSED++))
else
    echo -e "${RED}✗${NC} Loki retention period not properly configured"
    ((TESTS_FAILED++))
fi

if grep -q "retention_enabled: true" loki/loki-config.yaml; then
    echo -e "${GREEN}✓${NC} Loki retention enabled"
    ((TESTS_PASSED++))
else
    echo -e "${RED}✗${NC} Loki retention not enabled"
    ((TESTS_FAILED++))
fi

echo ""
echo "Testing Promtail Configuration Keys..."
echo "---------------------------------------"

if grep -q "url: http://loki:3100/loki/api/v1/push" promtail/promtail-config.yaml; then
    echo -e "${GREEN}✓${NC} Promtail configured to send to Loki"
    ((TESTS_PASSED++))
else
    echo -e "${RED}✗${NC} Promtail not properly configured for Loki"
    ((TESTS_FAILED++))
fi

if grep -q "docker_sd_configs:" promtail/promtail-config.yaml; then
    echo -e "${GREEN}✓${NC} Promtail Docker service discovery enabled"
    ((TESTS_PASSED++))
else
    echo -e "${RED}✗${NC} Promtail Docker service discovery not configured"
    ((TESTS_FAILED++))
fi

if grep -q "job_name: honua-cli-ai" promtail/promtail-config.yaml; then
    echo -e "${GREEN}✓${NC} Promtail has Honua.Cli.AI job configuration"
    ((TESTS_PASSED++))
else
    echo -e "${RED}✗${NC} Promtail missing Honua.Cli.AI job configuration"
    ((TESTS_FAILED++))
fi

echo ""
echo "Testing Docker Compose Services..."
echo "-----------------------------------"

if grep -q "loki:" docker-compose.yml; then
    echo -e "${GREEN}✓${NC} Loki service defined in docker-compose.yml"
    ((TESTS_PASSED++))
else
    echo -e "${RED}✗${NC} Loki service missing from docker-compose.yml"
    ((TESTS_FAILED++))
fi

if grep -q "promtail:" docker-compose.yml; then
    echo -e "${GREEN}✓${NC} Promtail service defined in docker-compose.yml"
    ((TESTS_PASSED++))
else
    echo -e "${RED}✗${NC} Promtail service missing from docker-compose.yml"
    ((TESTS_FAILED++))
fi

if grep -q "promtail-positions:" docker-compose.yml; then
    echo -e "${GREEN}✓${NC} Promtail positions volume defined"
    ((TESTS_PASSED++))
else
    echo -e "${RED}✗${NC} Promtail positions volume missing"
    ((TESTS_FAILED++))
fi

echo ""
echo "======================================"
echo "Test Summary"
echo "======================================"
echo -e "Passed: ${GREEN}$TESTS_PASSED${NC}"
echo -e "Failed: ${RED}$TESTS_FAILED${NC}"
echo ""

if [ $TESTS_FAILED -eq 0 ]; then
    echo -e "${GREEN}✓ All tests passed!${NC}"
    echo ""
    echo "Log aggregation is properly configured."
    echo ""
    echo "Next steps:"
    echo "1. Start the stack: docker-compose up -d"
    echo "2. Wait for services: ./scripts/verify-health.sh"
    echo "3. Access Grafana: http://localhost:3000"
    echo "4. View logs dashboard: http://localhost:3000/d/honua-logs"
    echo ""
    exit 0
else
    echo -e "${RED}✗ Some tests failed.${NC}"
    echo ""
    echo "Please review the errors above and fix the configuration."
    echo ""
    exit 1
fi
