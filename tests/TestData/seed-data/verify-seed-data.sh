#!/bin/bash
# Verify Seed Data - Test All API Endpoints with Seed Data
#
# This script verifies that all Honua API endpoints work correctly with the seed data.
# It tests WFS, WMS, WMTS, OGC API Features, STAC, and GeoServices REST APIs.
#
# Usage:
#   ./verify-seed-data.sh [base_url]
#
# Example:
#   ./verify-seed-data.sh http://localhost:5005

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
BASE_URL="${1:-http://localhost:5005}"
VERBOSE="${VERBOSE:-false}"
PASSED=0
FAILED=0
SKIPPED=0

# Test results array
declare -a RESULTS

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Honua Seed Data Verification${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "Base URL: ${YELLOW}$BASE_URL${NC}\n"

# Helper function to test endpoint
test_endpoint() {
    local name="$1"
    local url="$2"
    local expected_status="${3:-200}"
    local description="$4"

    if [ "$VERBOSE" = "true" ]; then
        echo -e "${BLUE}Testing:${NC} $name"
        echo -e "${BLUE}URL:${NC} $url"
    fi

    # Make request and get status code
    status=$(curl -s -o /dev/null -w "%{http_code}" "$url" 2>/dev/null || echo "000")

    if [ "$status" = "$expected_status" ]; then
        echo -e "${GREEN}✓${NC} $name ${BLUE}($status)${NC}"
        ((PASSED++))
        RESULTS+=("PASS: $name")
    elif [ "$status" = "000" ]; then
        echo -e "${RED}✗${NC} $name ${RED}(Connection failed)${NC}"
        ((FAILED++))
        RESULTS+=("FAIL: $name - Connection failed")
    else
        echo -e "${RED}✗${NC} $name ${RED}(Expected $expected_status, got $status)${NC}"
        ((FAILED++))
        RESULTS+=("FAIL: $name - Expected $expected_status, got $status")
    fi

    if [ -n "$description" ] && [ "$VERBOSE" = "true" ]; then
        echo -e "   ${description}"
    fi
    echo
}

# Check if server is reachable
echo -e "${YELLOW}Checking server health...${NC}"
if ! curl -sf "$BASE_URL/health" > /dev/null 2>&1; then
    echo -e "${RED}✗ Server is not reachable at $BASE_URL${NC}"
    echo -e "${YELLOW}Please start Honua server first:${NC}"
    echo -e "  docker-compose -f docker-compose.seed.yml up -d"
    echo -e "  OR"
    echo -e "  dotnet run --project src/Honua.Server.Host --urls $BASE_URL"
    exit 1
fi
echo -e "${GREEN}✓ Server is healthy${NC}\n"

# ============================================================================
#  WFS 2.0 Tests
# ============================================================================
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}WFS 2.0 API Tests${NC}"
echo -e "${BLUE}========================================${NC}\n"

test_endpoint \
    "WFS GetCapabilities" \
    "$BASE_URL/wfs?service=WFS&request=GetCapabilities&version=2.0.0" \
    200 \
    "Retrieve WFS service capabilities"

test_endpoint \
    "WFS GetFeature - Cities" \
    "$BASE_URL/wfs?service=WFS&version=2.0.0&request=GetFeature&typeName=cities&count=10&outputFormat=application/json" \
    200 \
    "Get first 10 cities in GeoJSON format"

test_endpoint \
    "WFS GetFeature - Roads with filter" \
    "$BASE_URL/wfs?service=WFS&version=2.0.0&request=GetFeature&typeName=roads&cql_filter=lanes>4&outputFormat=application/json" \
    200 \
    "Get roads with more than 4 lanes"

test_endpoint \
    "WFS DescribeFeatureType - POI" \
    "$BASE_URL/wfs?service=WFS&version=2.0.0&request=DescribeFeatureType&typeName=poi" \
    200 \
    "Get schema definition for POI layer"

# ============================================================================
#  WMS 1.3.0 Tests
# ============================================================================
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}WMS 1.3.0 API Tests${NC}"
echo -e "${BLUE}========================================${NC}\n"

test_endpoint \
    "WMS GetCapabilities" \
    "$BASE_URL/wms?service=WMS&request=GetCapabilities&version=1.3.0" \
    200 \
    "Retrieve WMS service capabilities"

test_endpoint \
    "WMS GetMap - Cities" \
    "$BASE_URL/wms?service=WMS&version=1.3.0&request=GetMap&layers=seed-data:cities&crs=EPSG:4326&bbox=-180,-90,180,90&width=800&height=400&format=image/png" \
    200 \
    "Render cities layer as PNG"

test_endpoint \
    "WMS GetFeatureInfo - Cities" \
    "$BASE_URL/wms?service=WMS&version=1.3.0&request=GetFeatureInfo&layers=seed-data:cities&query_layers=seed-data:cities&crs=EPSG:4326&bbox=-180,-90,180,90&width=800&height=400&i=400&j=200&info_format=application/json" \
    200 \
    "Get feature info at map coordinates"

# ============================================================================
#  WMTS 1.0.0 Tests
# ============================================================================
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}WMTS 1.0.0 API Tests${NC}"
echo -e "${BLUE}========================================${NC}\n"

test_endpoint \
    "WMTS GetCapabilities" \
    "$BASE_URL/wmts?service=WMTS&request=GetCapabilities&version=1.0.0" \
    200 \
    "Retrieve WMTS service capabilities"

test_endpoint \
    "WMTS GetTile - Cities (Zoom 0)" \
    "$BASE_URL/wmts?service=WMTS&request=GetTile&version=1.0.0&layer=seed-data:cities&style=default&format=image/png&TileMatrixSet=WorldWebMercatorQuad&TileMatrix=0&TileRow=0&TileCol=0" \
    200 \
    "Get tile at zoom level 0"

# ============================================================================
#  OGC API - Features Tests
# ============================================================================
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}OGC API - Features Tests${NC}"
echo -e "${BLUE}========================================${NC}\n"

test_endpoint \
    "OGC API Landing Page" \
    "$BASE_URL/ogc/" \
    200 \
    "Get API landing page"

test_endpoint \
    "OGC API Conformance" \
    "$BASE_URL/ogc/conformance" \
    200 \
    "Get conformance declaration"

test_endpoint \
    "OGC API Collections" \
    "$BASE_URL/ogc/collections" \
    200 \
    "List all feature collections"

test_endpoint \
    "OGC API Collection - Cities" \
    "$BASE_URL/ogc/collections/seed-data::cities" \
    200 \
    "Get cities collection metadata"

test_endpoint \
    "OGC API Items - Cities" \
    "$BASE_URL/ogc/collections/seed-data::cities/items?limit=10" \
    200 \
    "Get first 10 city features"

test_endpoint \
    "OGC API Items with BBOX - Parks" \
    "$BASE_URL/ogc/collections/seed-data::parks/items?bbox=-180,-90,180,90&limit=5" \
    200 \
    "Get parks within global bounding box"

# ============================================================================
#  OGC API - Tiles Tests
# ============================================================================
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}OGC API - Tiles Tests${NC}"
echo -e "${BLUE}========================================${NC}\n"

test_endpoint \
    "OGC API Tile Matrix Sets" \
    "$BASE_URL/ogc/tileMatrixSets" \
    200 \
    "List available tile matrix sets"

test_endpoint \
    "OGC API Tiles - Cities" \
    "$BASE_URL/ogc/collections/seed-data::cities/tiles/WorldWebMercatorQuad/0/0/0?f=mvt" \
    200 \
    "Get vector tile for cities"

# ============================================================================
#  WCS 2.0 Tests (if raster data available)
# ============================================================================
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}WCS 2.0 API Tests${NC}"
echo -e "${BLUE}========================================${NC}\n"

test_endpoint \
    "WCS GetCapabilities" \
    "$BASE_URL/wcs?service=WCS&request=GetCapabilities&version=2.0.1" \
    200 \
    "Retrieve WCS service capabilities"

# ============================================================================
#  STAC API Tests
# ============================================================================
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}STAC 1.0 API Tests${NC}"
echo -e "${BLUE}========================================${NC}\n"

test_endpoint \
    "STAC Root Catalog" \
    "$BASE_URL/stac/" \
    200 \
    "Get STAC root catalog"

test_endpoint \
    "STAC Collections" \
    "$BASE_URL/stac/collections" \
    200 \
    "List all STAC collections"

test_endpoint \
    "STAC Search - Cities" \
    "$BASE_URL/stac/search?collections=seed-data::cities&limit=10" \
    200 \
    "Search for city features"

test_endpoint \
    "STAC Conformance" \
    "$BASE_URL/stac/conformance" \
    200 \
    "Get STAC conformance classes"

# ============================================================================
#  GeoServices REST API Tests
# ============================================================================
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}GeoServices REST API Tests${NC}"
echo -e "${BLUE}========================================${NC}\n"

test_endpoint \
    "GeoServices FeatureServer Root" \
    "$BASE_URL/rest/services/seed-data/FeatureServer" \
    200 \
    "Get FeatureServer metadata"

test_endpoint \
    "GeoServices Query - Cities" \
    "$BASE_URL/rest/services/seed-data/FeatureServer/0/query?where=1=1&f=json&resultRecordCount=10" \
    200 \
    "Query cities layer"

test_endpoint \
    "GeoServices Query with filter" \
    "$BASE_URL/rest/services/seed-data/FeatureServer/0/query?where=population>1000000&f=json" \
    200 \
    "Query cities with population > 1M"

# ============================================================================
#  Summary
# ============================================================================
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Test Summary${NC}"
echo -e "${BLUE}========================================${NC}\n"

TOTAL=$((PASSED + FAILED + SKIPPED))
PASS_RATE=0
if [ "$TOTAL" -gt 0 ]; then
    PASS_RATE=$((PASSED * 100 / TOTAL))
fi

echo -e "Total Tests:   ${BLUE}$TOTAL${NC}"
echo -e "Passed:        ${GREEN}$PASSED${NC}"
echo -e "Failed:        ${RED}$FAILED${NC}"
echo -e "Skipped:       ${YELLOW}$SKIPPED${NC}"
echo -e "Pass Rate:     ${BLUE}${PASS_RATE}%${NC}\n"

if [ "$FAILED" -eq 0 ]; then
    echo -e "${GREEN}✓ All tests passed!${NC}"
    echo -e "${GREEN}Seed data is loaded and all API endpoints are working correctly.${NC}\n"
    exit 0
else
    echo -e "${RED}✗ Some tests failed${NC}"
    echo -e "${YELLOW}Please check the Honua server logs for details.${NC}\n"

    echo -e "${YELLOW}Failed tests:${NC}"
    for result in "${RESULTS[@]}"; do
        if [[ $result == FAIL* ]]; then
            echo -e "  ${RED}✗${NC} ${result#FAIL: }"
        fi
    done
    echo

    exit 1
fi
