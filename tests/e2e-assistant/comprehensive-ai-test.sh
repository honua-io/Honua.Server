#!/bin/bash
# Comprehensive AI Consultant Test Suite
# Tests all deployment scenarios with real OpenAI

set -e

if [ -z "$OPENAI_API_KEY" ]; then
    echo "ERROR: OPENAI_API_KEY required"
    exit 1
fi

PROJECT_ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
RESULTS_DIR="/tmp/honua-ai-tests-$TIMESTAMP"
mkdir -p "$RESULTS_DIR"

echo "╔══════════════════════════════════════════════════════════╗"
echo "║  Honua AI Consultant - Comprehensive Test Suite         ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""
echo "Results: $RESULTS_DIR"
echo ""

# Test function
test_deployment() {
    local name=$1
    local prompt=$2
    local expected_file=$3

    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "TEST: $name"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "Prompt: $prompt"
    echo ""

    WORKSPACE="$RESULTS_DIR/$name"
    mkdir -p "$WORKSPACE"

    cd "$PROJECT_ROOT"
    OUTPUT=$(dotnet run --project src/Honua.Cli -- consultant \
        --prompt "$prompt" \
        --workspace "$WORKSPACE" \
        --mode multi-agent \
        --auto-approve \
        --no-log 2>&1)

    echo "$OUTPUT" > "$WORKSPACE/output.log"

    if [ -f "$WORKSPACE/$expected_file" ]; then
        echo "✓ PASSED - Generated $expected_file"
        cat "$WORKSPACE/$expected_file"
    else
        echo "✗ FAILED - No $expected_file found"
        ls -la "$WORKSPACE"
    fi

    echo ""
}

# Run all tests
test_deployment \
    "01-postgis" \
    "Deploy Honua with PostGIS database and Redis caching using Docker Compose for production" \
    "docker-compose.yml"

test_deployment \
    "02-mysql" \
    "Deploy Honua with MySQL database and Redis for development using Docker Compose" \
    "docker-compose.yml"

test_deployment \
    "03-sqlserver" \
    "Deploy Honua with SQL Server database and Redis using Docker Compose" \
    "docker-compose.yml"

test_deployment \
    "04-minimal" \
    "Deploy Honua with just PostgreSQL database using Docker Compose" \
    "docker-compose.yml"

test_deployment \
    "05-full-stack" \
    "Deploy complete production Honua stack with PostGIS, Redis, and Nginx reverse proxy using Docker Compose" \
    "docker-compose.yml"

test_deployment \
    "06-troubleshoot" \
    "My spatial queries are extremely slow and the database keeps running out of memory. Diagnose and fix performance issues." \
    "performance.json"

# Generate summary
echo "╔══════════════════════════════════════════════════════════╗"
echo "║  Test Results Summary                                    ║"
echo "╚══════════════════════════════════════════════════════════╝"
echo ""

PASSED=0
FAILED=0

for dir in "$RESULTS_DIR"/*/; do
    name=$(basename "$dir")
    if [ -f "$dir/docker-compose.yml" ] || [ -f "$dir/performance.json" ]; then
        echo "✓ $name"
        PASSED=$((PASSED + 1))
    else
        echo "✗ $name"
        FAILED=$((FAILED + 1))
    fi
done

echo ""
echo "Total: $((PASSED + FAILED)) | Passed: $PASSED | Failed: $FAILED"
echo ""
echo "Results saved to: $RESULTS_DIR"
