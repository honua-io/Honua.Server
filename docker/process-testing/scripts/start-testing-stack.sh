#!/bin/bash

# Start Process Framework Testing Stack
# This script starts all required services for testing the Honua Process Framework

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
DOCKER_DIR="$(dirname "$SCRIPT_DIR")"

# Colors for output
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
RED='\033[0;31m'
NC='\033[0m' # No Color

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Honua Process Framework Testing Stack${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# Check if Docker is running
if ! docker info > /dev/null 2>&1; then
    echo -e "${RED}Error: Docker is not running. Please start Docker and try again.${NC}"
    exit 1
fi

# Check if docker-compose is available
if ! command -v docker-compose &> /dev/null && ! docker compose version &> /dev/null 2>&1; then
    echo -e "${RED}Error: docker-compose is not installed.${NC}"
    exit 1
fi

# Determine docker-compose command
if docker compose version &> /dev/null 2>&1; then
    DOCKER_COMPOSE="docker compose"
else
    DOCKER_COMPOSE="docker-compose"
fi

echo -e "${YELLOW}Starting services...${NC}"
cd "$DOCKER_DIR"

# Start services in background
$DOCKER_COMPOSE up -d

echo ""
echo -e "${YELLOW}Waiting for services to be healthy...${NC}"

# Wait for services to be healthy
TIMEOUT=120
ELAPSED=0
INTERVAL=5

while [ $ELAPSED -lt $TIMEOUT ]; do
    REDIS_HEALTH=$($DOCKER_COMPOSE ps -q redis | xargs docker inspect --format='{{.State.Health.Status}}' 2>/dev/null || echo "starting")
    OTEL_HEALTH=$($DOCKER_COMPOSE ps -q otel-collector | xargs docker inspect --format='{{.State.Health.Status}}' 2>/dev/null || echo "starting")
    PROMETHEUS_HEALTH=$($DOCKER_COMPOSE ps -q prometheus | xargs docker inspect --format='{{.State.Health.Status}}' 2>/dev/null || echo "starting")
    GRAFANA_HEALTH=$($DOCKER_COMPOSE ps -q grafana | xargs docker inspect --format='{{.State.Health.Status}}' 2>/dev/null || echo "starting")
    LOKI_HEALTH=$($DOCKER_COMPOSE ps -q loki | xargs docker inspect --format='{{.State.Health.Status}}' 2>/dev/null || echo "starting")

    if [ "$REDIS_HEALTH" = "healthy" ] && \
       [ "$OTEL_HEALTH" = "healthy" ] && \
       [ "$PROMETHEUS_HEALTH" = "healthy" ] && \
       [ "$GRAFANA_HEALTH" = "healthy" ] && \
       [ "$LOKI_HEALTH" = "healthy" ]; then
        echo -e "${GREEN}All services are healthy!${NC}"
        break
    fi

    echo -e "  Redis: $REDIS_HEALTH | OTEL: $OTEL_HEALTH | Prometheus: $PROMETHEUS_HEALTH | Grafana: $GRAFANA_HEALTH | Loki: $LOKI_HEALTH"
    sleep $INTERVAL
    ELAPSED=$((ELAPSED + INTERVAL))
done

if [ $ELAPSED -ge $TIMEOUT ]; then
    echo -e "${YELLOW}Warning: Some services may not be fully healthy yet. Check logs with: $DOCKER_COMPOSE logs${NC}"
fi

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Services Started Successfully!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "${GREEN}Service URLs:${NC}"
echo -e "  Grafana:            http://localhost:3000"
echo -e "  Prometheus:         http://localhost:9090"
echo -e "  Loki:               http://localhost:3100"
echo -e "  OTLP Collector:     grpc://localhost:4317 (gRPC)"
echo -e "                      http://localhost:4318 (HTTP)"
echo -e "  Redis:              localhost:6379"
echo ""
echo -e "${GREEN}Grafana Credentials:${NC}"
echo -e "  Username: admin"
echo -e "  Password: admin"
echo ""
echo -e "${YELLOW}Dashboard:${NC}"
echo -e "  Process Framework:  http://localhost:3000/d/honua-process-framework"
echo ""
echo -e "${YELLOW}Useful Commands:${NC}"
echo -e "  View logs:          $DOCKER_COMPOSE logs -f"
echo -e "  Stop services:      $DOCKER_DIR/scripts/stop-testing-stack.sh"
echo -e "  Check health:       $DOCKER_DIR/scripts/verify-health.sh"
echo ""
echo -e "${GREEN}Configure Honua.Cli.AI:${NC}"
echo -e "  Use environment:    ASPNETCORE_ENVIRONMENT=Testing"
echo -e "  Or set in launch:   \"environmentVariables\": { \"ASPNETCORE_ENVIRONMENT\": \"Testing\" }"
echo ""
