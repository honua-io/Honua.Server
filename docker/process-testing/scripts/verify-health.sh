#!/bin/bash

# Verify Health of Process Framework Testing Stack
# This script checks the health of all services and endpoints

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$(dirname "$SCRIPT_DIR")"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Determine docker-compose command
if docker compose version &> /dev/null 2>&1; then
    DOCKER_COMPOSE="docker compose"
else
    DOCKER_COMPOSE="docker-compose"
fi

cd "$DOCKER_DIR"

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Process Framework Stack Health Check${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Function to check HTTP endpoint
check_http() {
    local name=$1
    local url=$2
    local expected_code=${3:-200}

    if curl -s -o /dev/null -w "%{http_code}" "$url" | grep -q "$expected_code"; then
        echo -e "${GREEN}✓${NC} $name: ${GREEN}OK${NC} ($url)"
        return 0
    else
        echo -e "${RED}✗${NC} $name: ${RED}FAILED${NC} ($url)"
        return 1
    fi
}

# Function to check TCP endpoint
check_tcp() {
    local name=$1
    local host=$2
    local port=$3

    if timeout 2 bash -c "echo > /dev/tcp/$host/$port" 2>/dev/null; then
        echo -e "${GREEN}✓${NC} $name: ${GREEN}OK${NC} ($host:$port)"
        return 0
    else
        echo -e "${RED}✗${NC} $name: ${RED}FAILED${NC} ($host:$port)"
        return 1
    fi
}

# Function to check docker container health
check_container_health() {
    local name=$1
    local container=$2

    local health=$($DOCKER_COMPOSE ps -q "$container" | xargs docker inspect --format='{{.State.Health.Status}}' 2>/dev/null || echo "unknown")

    case $health in
        "healthy")
            echo -e "${GREEN}✓${NC} $name Container: ${GREEN}HEALTHY${NC}"
            return 0
            ;;
        "unhealthy")
            echo -e "${RED}✗${NC} $name Container: ${RED}UNHEALTHY${NC}"
            return 1
            ;;
        "starting")
            echo -e "${YELLOW}⚠${NC} $name Container: ${YELLOW}STARTING${NC}"
            return 1
            ;;
        *)
            echo -e "${RED}✗${NC} $name Container: ${RED}NOT RUNNING${NC}"
            return 1
            ;;
    esac
}

# Check if services are running
echo -e "${YELLOW}Checking Docker Containers...${NC}"
check_container_health "Redis" "redis"
REDIS_HEALTH=$?
check_container_health "OpenTelemetry Collector" "otel-collector"
OTEL_HEALTH=$?
check_container_health "Prometheus" "prometheus"
PROM_HEALTH=$?
check_container_health "Grafana" "grafana"
GRAFANA_HEALTH=$?
check_container_health "Loki" "loki"
LOKI_HEALTH=$?
check_container_health "Tempo" "tempo"
TEMPO_HEALTH=$?

echo ""
echo -e "${YELLOW}Checking Service Endpoints...${NC}"

# Check Redis
check_tcp "Redis" "localhost" "6379"
REDIS_TCP=$?

# Check OpenTelemetry Collector
check_tcp "OTLP gRPC" "localhost" "4317"
OTLP_GRPC=$?
check_tcp "OTLP HTTP" "localhost" "4318"
OTLP_HTTP=$?
check_http "OTLP Health" "http://localhost:13133"
OTLP_HEALTH_HTTP=$?

# Check Prometheus
check_http "Prometheus" "http://localhost:9090/-/healthy"
PROM_HTTP=$?
check_http "Prometheus Targets" "http://localhost:9090/api/v1/targets"
PROM_TARGETS=$?

# Check Loki
check_http "Loki Ready" "http://localhost:3100/ready"
LOKI_HTTP=$?

# Check Tempo
check_http "Tempo Ready" "http://localhost:3200/ready"
TEMPO_HTTP=$?
check_tcp "Tempo OTLP gRPC" "localhost" "4317"
TEMPO_GRPC=$?
check_tcp "Tempo OTLP HTTP" "localhost" "4318"
TEMPO_HTTP_PORT=$?

# Check Grafana
check_http "Grafana API" "http://localhost:3000/api/health"
GRAFANA_HTTP=$?

echo ""
echo -e "${YELLOW}Checking Grafana Data Sources...${NC}"

# Get Grafana datasources
DATASOURCES=$(curl -s -u admin:admin "http://localhost:3000/api/datasources" 2>/dev/null || echo "[]")

if echo "$DATASOURCES" | grep -q "Prometheus"; then
    echo -e "${GREEN}✓${NC} Grafana Datasource: ${GREEN}Prometheus configured${NC}"
else
    echo -e "${YELLOW}⚠${NC} Grafana Datasource: ${YELLOW}Prometheus not configured${NC}"
fi

if echo "$DATASOURCES" | grep -q "Loki"; then
    echo -e "${GREEN}✓${NC} Grafana Datasource: ${GREEN}Loki configured${NC}"
else
    echo -e "${YELLOW}⚠${NC} Grafana Datasource: ${YELLOW}Loki not configured${NC}"
fi

if echo "$DATASOURCES" | grep -q "Tempo"; then
    echo -e "${GREEN}✓${NC} Grafana Datasource: ${GREEN}Tempo configured${NC}"
else
    echo -e "${YELLOW}⚠${NC} Grafana Datasource: ${YELLOW}Tempo not configured${NC}"
fi

echo ""
echo -e "${YELLOW}Checking Prometheus Targets...${NC}"

# Check Prometheus targets
TARGETS=$(curl -s "http://localhost:9090/api/v1/targets" 2>/dev/null || echo '{"data":{"activeTargets":[]}}')
ACTIVE_TARGETS=$(echo "$TARGETS" | grep -o '"activeTargets":\[.*\]' | grep -o '"job":"[^"]*"' | wc -l)

echo -e "  Active Targets: $ACTIVE_TARGETS"

if echo "$TARGETS" | grep -q '"job":"otel-collector"'; then
    echo -e "  ${GREEN}✓${NC} otel-collector target configured"
fi

if echo "$TARGETS" | grep -q '"job":"honua-process-framework"'; then
    echo -e "  ${GREEN}✓${NC} honua-process-framework target configured"
fi

echo ""
echo -e "${BLUE}========================================${NC}"

# Calculate overall health
TOTAL_CHECKS=17
FAILED_CHECKS=0

[ $REDIS_HEALTH -ne 0 ] && ((FAILED_CHECKS++))
[ $OTEL_HEALTH -ne 0 ] && ((FAILED_CHECKS++))
[ $PROM_HEALTH -ne 0 ] && ((FAILED_CHECKS++))
[ $GRAFANA_HEALTH -ne 0 ] && ((FAILED_CHECKS++))
[ $LOKI_HEALTH -ne 0 ] && ((FAILED_CHECKS++))
[ $TEMPO_HEALTH -ne 0 ] && ((FAILED_CHECKS++))
[ $REDIS_TCP -ne 0 ] && ((FAILED_CHECKS++))
[ $OTLP_GRPC -ne 0 ] && ((FAILED_CHECKS++))
[ $OTLP_HTTP -ne 0 ] && ((FAILED_CHECKS++))
[ $OTLP_HEALTH_HTTP -ne 0 ] && ((FAILED_CHECKS++))
[ $PROM_HTTP -ne 0 ] && ((FAILED_CHECKS++))
[ $PROM_TARGETS -ne 0 ] && ((FAILED_CHECKS++))
[ $LOKI_HTTP -ne 0 ] && ((FAILED_CHECKS++))
[ $TEMPO_HTTP -ne 0 ] && ((FAILED_CHECKS++))
[ $TEMPO_GRPC -ne 0 ] && ((FAILED_CHECKS++))
[ $TEMPO_HTTP_PORT -ne 0 ] && ((FAILED_CHECKS++))
[ $GRAFANA_HTTP -ne 0 ] && ((FAILED_CHECKS++))

PASSED_CHECKS=$((TOTAL_CHECKS - FAILED_CHECKS))

if [ $FAILED_CHECKS -eq 0 ]; then
    echo -e "${GREEN}All health checks passed! ($PASSED_CHECKS/$TOTAL_CHECKS)${NC}"
    echo -e "${GREEN}Stack is ready for testing.${NC}"
    exit 0
elif [ $FAILED_CHECKS -le 3 ]; then
    echo -e "${YELLOW}Some health checks failed. ($PASSED_CHECKS/$TOTAL_CHECKS passed)${NC}"
    echo -e "${YELLOW}Stack may still be starting up. Wait a few moments and retry.${NC}"
    exit 1
else
    echo -e "${RED}Multiple health checks failed! ($FAILED_CHECKS/$TOTAL_CHECKS failed)${NC}"
    echo -e "${RED}Check logs with: $DOCKER_COMPOSE logs${NC}"
    exit 1
fi
