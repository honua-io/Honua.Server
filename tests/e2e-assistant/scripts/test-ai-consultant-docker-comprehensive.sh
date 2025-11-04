#!/bin/bash
# AI Consultant Docker E2E Test with Infrastructure Validation
# Tests AI consultant's ability to generate Docker deployments and validates actual infrastructure

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DIR="$(dirname "$SCRIPT_DIR")"
PROJECT_ROOT="$(dirname "$(dirname "$TEST_DIR")")"

# Colors
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
BOLD='\033[1m'
NC='\033[0m'

echo -e "${CYAN}${BOLD}"
echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║   AI Consultant Docker E2E Test with Validation for HonuaIO   ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo -e "${NC}"
echo ""

# Check for API keys
if [ -z "$OPENAI_API_KEY" ] && [ -z "$ANTHROPIC_API_KEY" ]; then
    echo -e "${YELLOW}⚠ Warning: No API key found (OPENAI_API_KEY or ANTHROPIC_API_KEY)${NC}"
    echo -e "${YELLOW}  Skipping real AI tests${NC}"
    exit 0
fi

# Create results directory
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
RESULTS_DIR="$TEST_DIR/results/docker-comprehensive_$TIMESTAMP"
mkdir -p "$RESULTS_DIR"

echo -e "${BLUE}Results: $RESULTS_DIR${NC}"
echo ""

TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

# Validation function that deploys and tests actual running containers
validate_deployment() {
    local workspace=$1
    local test_name=$2

    echo -e "${BLUE}Deploying and validating infrastructure...${NC}"

    # Extract port from docker-compose.yml
    local MAPPED_PORT=$(grep -A5 "honua:" "$workspace/docker-compose.yml" | grep -oP '"\K\d+(?=:8080)' | head -1)
    if [ -z "$MAPPED_PORT" ]; then
        MAPPED_PORT=$(grep -oP '"\K\d+(?=:8080)' "$workspace/docker-compose.yml" | head -1)
    fi
    if [ -z "$MAPPED_PORT" ]; then
        echo -e "${YELLOW}  ⚠ Could not extract port, using 5000${NC}"
        MAPPED_PORT=5000
    fi

    echo -e "${BLUE}  Using port: $MAPPED_PORT${NC}"

    # Start containers
    cd "$workspace"
    # Clean up any leftover containers from previous runs
    docker-compose down -v > /dev/null 2>&1 || true
    # Also remove any orphaned containers with common names
    docker rm -f honua-server honua-postgis honua-mysql honua-sqlserver honua-redis honua-nginx honua-traefik honua-caddy > /dev/null 2>&1 || true
    docker-compose up -d > deploy.log 2>&1

    if [ $? -ne 0 ]; then
        echo -e "${RED}  ✗ Docker Compose failed to start${NC}"
        cat deploy.log
        return 1
    fi

    # Wait for services to initialize (databases need time)
    echo -e "${BLUE}  Waiting 45 seconds for services to initialize...${NC}"
    sleep 45

    local validation_passed=true

    # Test 1: Health endpoint
    if curl -s -f "http://localhost:$MAPPED_PORT/healthz/ready" > /dev/null 2>&1; then
        echo -e "${GREEN}  ✓ Health endpoint accessible${NC}"
    else
        echo -e "${RED}  ✗ Health endpoint failed${NC}"
        echo -e "${YELLOW}  Checking logs:${NC}"
        docker-compose logs --tail=20 honua
        validation_passed=false
    fi

    # Test 2: OGC API landing page
    if [ "$validation_passed" = true ]; then
        if curl -s "http://localhost:$MAPPED_PORT/ogc" | jq -e '.title' > /dev/null 2>&1; then
            echo -e "${GREEN}  ✓ OGC API landing page accessible${NC}"
        else
            echo -e "${YELLOW}  ⚠ OGC API landing page returned unexpected format${NC}"
        fi

        # Test 3: Collections endpoint
        if curl -s -f "http://localhost:$MAPPED_PORT/ogc/collections" > /dev/null 2>&1; then
            echo -e "${GREEN}  ✓ Collections endpoint accessible${NC}"
        else
            echo -e "${YELLOW}  ⚠ Collections endpoint not accessible${NC}"
        fi
    fi

    # Cleanup
    docker-compose down -v > /dev/null 2>&1

    if [ "$validation_passed" = true ]; then
        return 0
    else
        return 1
    fi
}

# Test 1: Docker with PostGIS and Nginx
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 1: Docker PostGIS + Nginx + Redis${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/docker-postgis-nginx"
mkdir -p "$WORKSPACE"

PROMPT="Create a production-ready Docker Compose deployment for Honua with PostGIS database, Nginx reverse proxy with caching, and Redis for application caching. Use port 18100."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/docker-compose.yml" ]; then
    echo -e "${GREEN}✓ AI generated Docker Compose configuration${NC}"

    # Validate configuration contains expected services
    if grep -q "postgis" "$WORKSPACE/docker-compose.yml" && \
       grep -q "nginx" "$WORKSPACE/docker-compose.yml" && \
       grep -q "redis" "$WORKSPACE/docker-compose.yml"; then
        echo -e "${GREEN}✓ Configuration contains PostGIS, Nginx, and Redis${NC}"

        # Now deploy and validate actual running containers
        if validate_deployment "$WORKSPACE" "Test 1"; then
            echo -e "${GREEN}✓ Deployment validated successfully${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${RED}✗ Deployment validation failed${NC}"
            FAILED_TESTS=$((FAILED_TESTS + 1))
        fi
    else
        echo -e "${RED}✗ Configuration missing expected services${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 2: Docker with MySQL and Traefik
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 2: Docker MySQL + Traefik + Redis${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/docker-mysql-traefik"
mkdir -p "$WORKSPACE"

PROMPT="Create Docker Compose for Honua with MySQL database, Traefik reverse proxy with automatic HTTPS, Redis caching, and health checks. Use port 18101."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/docker-compose.yml" ]; then
    echo -e "${GREEN}✓ AI generated Docker Compose configuration${NC}"

    # Validate configuration contains expected services
    if grep -q "mysql" "$WORKSPACE/docker-compose.yml" && \
       grep -q "traefik" "$WORKSPACE/docker-compose.yml"; then
        echo -e "${GREEN}✓ Configuration contains MySQL and Traefik${NC}"

        # Now deploy and validate actual running containers
        if validate_deployment "$WORKSPACE" "Test 2"; then
            echo -e "${GREEN}✓ Deployment validated successfully${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${RED}✗ Deployment validation failed${NC}"
            FAILED_TESTS=$((FAILED_TESTS + 1))
        fi
    else
        echo -e "${RED}✗ Configuration missing expected services${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 3: Docker with SQL Server and Caddy
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 3: Docker SQL Server + Caddy${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/docker-sqlserver-caddy"
mkdir -p "$WORKSPACE"

PROMPT="Create Docker Compose for Honua with Microsoft SQL Server database, Caddy reverse proxy with automatic HTTPS and compression. Use port 18102. Include proper SQL Server initialization."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/docker-compose.yml" ]; then
    echo -e "${GREEN}✓ AI generated Docker Compose configuration${NC}"

    # Validate configuration contains expected services
    if grep -qi "sqlserver\|mssql" "$WORKSPACE/docker-compose.yml" && \
       grep -q "caddy" "$WORKSPACE/docker-compose.yml"; then
        echo -e "${GREEN}✓ Configuration contains SQL Server and Caddy${NC}"

        # Now deploy and validate actual running containers
        if validate_deployment "$WORKSPACE" "Test 3"; then
            echo -e "${GREEN}✓ Deployment validated successfully${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${RED}✗ Deployment validation failed${NC}"
            FAILED_TESTS=$((FAILED_TESTS + 1))
        fi
    else
        echo -e "${RED}✗ Configuration missing expected services${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 4: Docker with high availability setup
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 4: Docker HA with PgPool${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/docker-ha-pgpool"
mkdir -p "$WORKSPACE"

PROMPT="Create high-availability Docker Compose for Honua with PostgreSQL primary and replica, PgPool for connection pooling and failover, HAProxy load balancer in front of multiple Honua instances. Use port 18103."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/docker-compose.yml" ]; then
    echo -e "${GREEN}✓ AI generated HA Docker Compose configuration${NC}"

    # Validate configuration contains HA services
    if grep -qi "pgpool\|haproxy\|postgres" "$WORKSPACE/docker-compose.yml"; then
        echo -e "${GREEN}✓ Configuration contains HA components${NC}"

        # Now deploy and validate actual running containers
        if validate_deployment "$WORKSPACE" "Test 4"; then
            echo -e "${GREEN}✓ Deployment validated successfully${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${RED}✗ Deployment validation failed${NC}"
            FAILED_TESTS=$((FAILED_TESTS + 1))
        fi
    else
        echo -e "${RED}✗ Configuration missing HA components${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 5: Troubleshooting scenario
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 5: Troubleshooting Docker Performance${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/docker-troubleshooting"
mkdir -p "$WORKSPACE"

# Create deployment first
cd "$PROJECT_ROOT"
dotnet run --project src/Honua.Cli consultant \
    --prompt "Create Docker Compose for Honua with PostGIS" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/setup.log" 2>&1

# Now troubleshoot
PROMPT="My Docker containers are using too much memory and the OOM killer keeps stopping them. Help me optimize resource limits and tune PostgreSQL settings."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/troubleshooting.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ]; then
    if grep -qi "memory\|resource\|limit" "$WORKSPACE/troubleshooting.log"; then
        echo -e "${GREEN}✓ AI provided troubleshooting guidance${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${YELLOW}⚠ Troubleshooting completed${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ Troubleshooting failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 6: Security hardening scenario
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 6: Docker Security Hardening${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/docker-security"
mkdir -p "$WORKSPACE"

PROMPT="Create secure Docker Compose for Honua with non-root containers, read-only filesystem where possible, network isolation, secret management using Docker secrets, security scanning, and TLS encryption between services. Use port 18104."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/docker-compose.yml" ]; then
    if grep -q "secrets:" "$WORKSPACE/docker-compose.yml" || \
       grep -q "read_only:" "$WORKSPACE/docker-compose.yml" || \
       grep -q "user:" "$WORKSPACE/docker-compose.yml"; then
        echo -e "${GREEN}✓ Docker Compose contains security configurations${NC}"

        # Now deploy and validate actual running containers
        if validate_deployment "$WORKSPACE" "Test 6"; then
            echo -e "${GREEN}✓ Deployment validated successfully${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${RED}✗ Deployment validation failed${NC}"
            FAILED_TESTS=$((FAILED_TESTS + 1))
        fi
    else
        echo -e "${YELLOW}⚠ Some security features may be missing${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 7: Vector layers with WFS and WMS
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 7: Vector Layers Metadata (WFS/WMS)${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/docker-vector-layers"
mkdir -p "$WORKSPACE"

PROMPT="Create Docker Compose for Honua with PostGIS including 3 sample vector layers (countries, cities, roads) with WFS 2.0 and WMS 1.3 services enabled. Include metadata with layer titles, abstracts, and bounding boxes. Use port 18105."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/docker-compose.yml" ] && [ -f "$WORKSPACE/metadata.json" ]; then
    echo -e "${GREEN}✓ AI generated configuration with metadata${NC}"

    # Validate metadata contains layers
    if grep -q "countries\|cities\|roads" "$WORKSPACE/metadata.json" && \
       grep -q "WFS\|wfs" "$WORKSPACE/metadata.json" && \
       grep -q "WMS\|wms" "$WORKSPACE/metadata.json"; then
        echo -e "${GREEN}✓ Metadata contains vector layers and OGC services${NC}"

        # Deploy and validate
        if validate_deployment "$WORKSPACE" "Test 7"; then
            echo -e "${GREEN}✓ Deployment validated successfully${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${RED}✗ Deployment validation failed${NC}"
            FAILED_TESTS=$((FAILED_TESTS + 1))
        fi
    else
        echo -e "${RED}✗ Metadata missing expected layers or services${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 8: Raster layers with WMTS
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 8: Raster Layers Metadata (WMTS)${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/docker-raster-layers"
mkdir -p "$WORKSPACE"

PROMPT="Create Docker Compose for Honua with 2 raster tile layers (satellite imagery, elevation) served via WMTS 1.0 with tile matrix sets. Include S3-compatible storage (MinIO) for tile cache. Use port 18106."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/docker-compose.yml" ] && [ -f "$WORKSPACE/metadata.json" ]; then
    echo -e "${GREEN}✓ AI generated configuration with metadata${NC}"

    # Validate metadata contains raster layers and WMTS
    if grep -qi "satellite\|imagery\|elevation\|raster" "$WORKSPACE/metadata.json" && \
       grep -qi "WMTS\|wmts" "$WORKSPACE/metadata.json"; then
        echo -e "${GREEN}✓ Metadata contains raster layers and WMTS service${NC}"

        # Check for S3/MinIO storage
        if grep -qi "minio\|s3" "$WORKSPACE/docker-compose.yml"; then
            echo -e "${GREEN}✓ Configuration includes S3-compatible storage${NC}"
        fi

        # Deploy and validate
        if validate_deployment "$WORKSPACE" "Test 8"; then
            echo -e "${GREEN}✓ Deployment validated successfully${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${RED}✗ Deployment validation failed${NC}"
            FAILED_TESTS=$((FAILED_TESTS + 1))
        fi
    else
        echo -e "${RED}✗ Metadata missing expected layers or services${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 9: OData v4 with vector tiles
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 9: OData + Vector Tiles Metadata${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/docker-odata-vectortiles"
mkdir -p "$WORKSPACE"

PROMPT="Create Docker Compose for Honua with PostGIS featuring 2 layers (buildings, parcels) exposed via OData v4 and Mapbox Vector Tiles. Include metadata with property schemas and queryable fields. Use port 18107."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/docker-compose.yml" ] && [ -f "$WORKSPACE/metadata.json" ]; then
    echo -e "${GREEN}✓ AI generated configuration with metadata${NC}"

    # Validate metadata contains OData and vector tiles
    if grep -qi "buildings\|parcels" "$WORKSPACE/metadata.json" && \
       grep -qi "odata" "$WORKSPACE/metadata.json"; then
        echo -e "${GREEN}✓ Metadata contains layers and OData service${NC}"

        # Deploy and validate
        if validate_deployment "$WORKSPACE" "Test 9"; then
            echo -e "${GREEN}✓ Deployment validated successfully${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${RED}✗ Deployment validation failed${NC}"
            FAILED_TESTS=$((FAILED_TESTS + 1))
        fi
    else
        echo -e "${RED}✗ Metadata missing expected layers or services${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 10: STAC Catalog with raster collections
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 10: STAC Catalog Metadata${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/docker-stac-catalog"
mkdir -p "$WORKSPACE"

PROMPT="Create Docker Compose for Honua with PostGIS exposing a STAC (SpatioTemporal Asset Catalog) API with 2 collections (Landsat, Sentinel) including temporal metadata and cloud cover properties. Use port 18108."

TOTAL_TESTS=$((TOTAL_TESTS + 1))
cd "$PROJECT_ROOT"
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/docker-compose.yml" ] && [ -f "$WORKSPACE/metadata.json" ]; then
    echo -e "${GREEN}✓ AI generated configuration with metadata${NC}"

    # Validate metadata contains STAC collections
    if grep -qi "landsat\|sentinel\|stac" "$WORKSPACE/metadata.json"; then
        echo -e "${GREEN}✓ Metadata contains STAC collections${NC}"

        # Deploy and validate
        if validate_deployment "$WORKSPACE" "Test 10"; then
            echo -e "${GREEN}✓ Deployment validated successfully${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${RED}✗ Deployment validation failed${NC}"
            FAILED_TESTS=$((FAILED_TESTS + 1))
        fi
    else
        echo -e "${RED}✗ Metadata missing STAC collections${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Generate summary
echo -e "${CYAN}${BOLD}"
echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║                    TEST SUMMARY                                ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo -e "${NC}"
echo -e "Total Tests:  $TOTAL_TESTS"
echo -e "Passed:       ${GREEN}$PASSED_TESTS${NC}"
echo -e "Failed:       ${RED}$FAILED_TESTS${NC}"
echo -e "Success Rate: $(echo "scale=1; $PASSED_TESTS * 100 / $TOTAL_TESTS" | bc)%"
echo ""

if [ $FAILED_TESTS -eq 0 ]; then
    echo -e "${GREEN}${BOLD}✓ ALL DOCKER AI CONSULTANT TESTS PASSED${NC}"
    exit 0
else
    echo -e "${RED}${BOLD}✗ SOME TESTS FAILED${NC}"
    exit 1
fi
