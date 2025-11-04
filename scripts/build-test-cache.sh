#!/usr/bin/env bash
#
# build-test-cache.sh
# Builds a cached Docker image with preloaded test data for fast parallel testing
#
# Usage:
#   ./scripts/build-test-cache.sh [--no-cache] [--push]
#
# Options:
#   --no-cache    Force rebuild without using Docker cache
#   --push        Push image to registry after building
#   --tag TAG     Custom tag (default: honua:test-cached)

set -euo pipefail

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Configuration
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
IMAGE_TAG="${IMAGE_TAG:-honua:test-cached}"
NO_CACHE=""
PUSH_IMAGE=false

# Parse arguments
while [[ $# -gt 0 ]]; do
    case $1 in
        --no-cache)
            NO_CACHE="--no-cache"
            shift
            ;;
        --push)
            PUSH_IMAGE=true
            shift
            ;;
        --tag)
            IMAGE_TAG="$2"
            shift 2
            ;;
        *)
            echo -e "${RED}Unknown option: $1${NC}"
            exit 1
            ;;
    esac
done

cd "$PROJECT_ROOT"

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}Building Cached Test Image${NC}"
echo -e "${BLUE}========================================${NC}"
echo -e "Image tag: ${GREEN}${IMAGE_TAG}${NC}"
echo -e "Project root: ${PROJECT_ROOT}"
echo ""

# Verify test data exists
echo -e "${YELLOW}Verifying test data...${NC}"
REQUIRED_FILES=(
    "tests/TestData/ogc-sample.db"
    "tests/TestData/test-metadata.json"
)

for file in "${REQUIRED_FILES[@]}"; do
    if [[ ! -f "$PROJECT_ROOT/$file" ]]; then
        echo -e "${RED}ERROR: Required test file not found: $file${NC}"
        echo -e "${YELLOW}Please ensure test data is generated before building cached image.${NC}"
        exit 1
    fi
    echo -e "  ${GREEN}✓${NC} Found: $file"
done

# Optional files (warn if missing but don't fail)
OPTIONAL_FILES=(
    "tests/TestData/stac-catalog.db"
    "tests/TestData/auth/auth.db"
)

for file in "${OPTIONAL_FILES[@]}"; do
    if [[ ! -f "$PROJECT_ROOT/$file" ]]; then
        echo -e "  ${YELLOW}⚠${NC} Optional file missing: $file"
    else
        echo -e "  ${GREEN}✓${NC} Found: $file"
    fi
done

echo ""

# Build the image
echo -e "${YELLOW}Building Docker image...${NC}"
BUILD_START=$(date +%s)

if docker build \
    $NO_CACHE \
    -f Dockerfile.test-cached \
    -t "$IMAGE_TAG" \
    . ; then

    BUILD_END=$(date +%s)
    BUILD_TIME=$((BUILD_END - BUILD_START))

    echo ""
    echo -e "${GREEN}✓ Image built successfully in ${BUILD_TIME}s${NC}"
    echo -e "  Tag: ${GREEN}${IMAGE_TAG}${NC}"

    # Show image size
    IMAGE_SIZE=$(docker images "$IMAGE_TAG" --format "{{.Size}}" | head -1)
    echo -e "  Size: ${IMAGE_SIZE}"

else
    echo -e "${RED}✗ Build failed${NC}"
    exit 1
fi

# Test the image
echo ""
echo -e "${YELLOW}Testing image startup...${NC}"
TEST_CONTAINER="honua-test-cache-validation-$$"

if docker run -d \
    --name "$TEST_CONTAINER" \
    -p 8081:8080 \
    "$IMAGE_TAG" >/dev/null; then

    echo -e "  Container started: ${TEST_CONTAINER}"

    # Wait for health check
    echo -e "  Waiting for health check..."
    HEALTH_RETRIES=0
    MAX_RETRIES=30

    while [[ $HEALTH_RETRIES -lt $MAX_RETRIES ]]; do
        HEALTH_STATUS=$(docker inspect --format='{{.State.Health.Status}}' "$TEST_CONTAINER" 2>/dev/null || echo "starting")

        if [[ "$HEALTH_STATUS" == "healthy" ]]; then
            echo -e "  ${GREEN}✓ Container is healthy${NC}"
            break
        elif [[ "$HEALTH_STATUS" == "unhealthy" ]]; then
            echo -e "  ${RED}✗ Container is unhealthy${NC}"
            docker logs "$TEST_CONTAINER" 2>&1 | tail -20
            docker rm -f "$TEST_CONTAINER" >/dev/null 2>&1
            exit 1
        fi

        sleep 1
        HEALTH_RETRIES=$((HEALTH_RETRIES + 1))
        printf "."
    done

    if [[ $HEALTH_RETRIES -ge $MAX_RETRIES ]]; then
        echo -e "\n  ${RED}✗ Health check timeout${NC}"
        docker logs "$TEST_CONTAINER" 2>&1 | tail -20
        docker rm -f "$TEST_CONTAINER" >/dev/null 2>&1
        exit 1
    fi

    # Verify endpoints
    echo -e "  Verifying endpoints..."
    if curl -sf http://localhost:8081/health >/dev/null; then
        echo -e "    ${GREEN}✓ /health${NC}"
    else
        echo -e "    ${RED}✗ /health${NC}"
    fi

    if curl -sf http://localhost:8081/ >/dev/null; then
        echo -e "    ${GREEN}✓ / (landing page)${NC}"
    else
        echo -e "    ${RED}✗ / (landing page)${NC}"
    fi

    # Cleanup test container
    echo -e "  Cleaning up test container..."
    docker rm -f "$TEST_CONTAINER" >/dev/null 2>&1

else
    echo -e "${RED}✗ Failed to start test container${NC}"
    exit 1
fi

# Push if requested
if [[ "$PUSH_IMAGE" == true ]]; then
    echo ""
    echo -e "${YELLOW}Pushing image to registry...${NC}"
    if docker push "$IMAGE_TAG"; then
        echo -e "${GREEN}✓ Image pushed successfully${NC}"
    else
        echo -e "${RED}✗ Push failed${NC}"
        exit 1
    fi
fi

# Summary
echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Build Complete${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo -e "Image: ${GREEN}${IMAGE_TAG}${NC}"
echo -e "Size: ${IMAGE_SIZE}"
echo -e "Build time: ${BUILD_TIME}s"
echo ""
echo -e "${YELLOW}Next steps:${NC}"
echo -e "  1. Start the test server:"
echo -e "     ${BLUE}docker-compose -f docker-compose.test-parallel.yml up -d${NC}"
echo -e ""
echo -e "  2. Run parallel tests:"
echo -e "     ${BLUE}./scripts/run-tests-parallel.sh${NC}"
echo -e ""
echo -e "  3. Or run tests manually:"
echo -e "     ${BLUE}pytest tests/python/ -n 5 -v${NC}"
echo -e "     ${BLUE}pytest tests/qgis/ -n 5 -v${NC}"
echo -e "     ${BLUE}dotnet test -c Release --filter Category!=Slow --parallel${NC}"
echo ""
