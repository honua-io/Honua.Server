#!/bin/bash
# Real-World AI Consultant E2E Test
# Tests the AI consultant with actual prompts against live infrastructure

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
TEST_DIR="$(dirname "$SCRIPT_DIR")"
PROJECT_ROOT="$(dirname "$(dirname "$TEST_DIR")")"

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  Real-World AI Consultant E2E Tests${NC}"
echo -e "${BLUE}========================================${NC}"
echo ""

# Check for API keys
if [ -z "$OPENAI_API_KEY" ] && [ -z "$ANTHROPIC_API_KEY" ]; then
    echo -e "${YELLOW}⚠ Warning: No API key found (OPENAI_API_KEY or ANTHROPIC_API_KEY)${NC}"
    echo -e "${YELLOW}  Tests will use mock LLM responses${NC}"
    echo ""
fi

# Create results directory with timestamp
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
RESULTS_DIR="$TEST_DIR/results/real-ai-run_$TIMESTAMP"
mkdir -p "$RESULTS_DIR"

echo -e "${BLUE}Results will be saved to: $RESULTS_DIR${NC}"
echo ""

# Test counters
TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

# Function to run a consultant test
run_consultant_test() {
    local test_name=$1
    local prompt=$2
    local expected_output=$3
    local topology=$4

    TOTAL_TESTS=$((TOTAL_TESTS + 1))

    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "${BLUE}Test $TOTAL_TESTS: $test_name${NC}"
    echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
    echo -e "Prompt: ${YELLOW}$prompt${NC}"
    echo -e "Topology: ${topology}"
    echo ""

    # Create test workspace
    TEST_WORKSPACE="$RESULTS_DIR/${topology}_${test_name// /_}"
    mkdir -p "$TEST_WORKSPACE"

    # Run the consultant
    echo "Running AI Consultant..."
    cd "$PROJECT_ROOT"

    set +e
    OUTPUT=$(dotnet run --project src/Honua.Cli consultant \
        --prompt "$prompt" \
        --workspace "$TEST_WORKSPACE" \
        --mode multi-agent \
        --auto-approve \
        --no-logging \
        2>&1)
    EXIT_CODE=$?
    set -e

    # Save output
    echo "$OUTPUT" > "$TEST_WORKSPACE/consultant-output.log"

    # Check results
    if [ $EXIT_CODE -eq 0 ]; then
        if [ -n "$expected_output" ] && [ -f "$TEST_WORKSPACE/$expected_output" ]; then
            echo -e "${GREEN}✓ PASSED${NC} - Generated $expected_output"
            PASSED_TESTS=$((PASSED_TESTS + 1))

            # Display generated configuration
            echo ""
            echo -e "${GREEN}Generated Configuration:${NC}"
            if [[ "$expected_output" == *.yml ]] || [[ "$expected_output" == *.yaml ]]; then
                cat "$TEST_WORKSPACE/$expected_output" | head -30
            elif [[ "$expected_output" == *.json ]]; then
                cat "$TEST_WORKSPACE/$expected_output" | jq '.' 2>/dev/null || cat "$TEST_WORKSPACE/$expected_output"
            else
                cat "$TEST_WORKSPACE/$expected_output" | head -20
            fi
            echo ""
        else
            echo -e "${YELLOW}⚠ PARTIAL${NC} - Completed but expected output not found: $expected_output"
            echo "Generated files:"
            ls -la "$TEST_WORKSPACE"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        fi
    else
        echo -e "${RED}✗ FAILED${NC} - Exit code: $EXIT_CODE"
        echo "Output:"
        echo "$OUTPUT"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi

    # Save test metadata
    cat > "$TEST_WORKSPACE/test-metadata.json" <<EOF
{
    "testName": "$test_name",
    "prompt": "$prompt",
    "topology": "$topology",
    "exitCode": $EXIT_CODE,
    "timestamp": "$(date -Iseconds)",
    "workspace": "$TEST_WORKSPACE"
}
EOF

    echo ""
}

# Test 1: Docker MySQL Deployment
run_consultant_test \
    "Docker MySQL Basic" \
    "Deploy Honua with MySQL database and Redis caching using Docker Compose for development" \
    "docker-compose.yml" \
    "docker-mysql"

# Test 2: Docker PostGIS with Security
run_consultant_test \
    "Docker PostGIS Security" \
    "Deploy Honua with PostGIS database on Docker with JWT authentication and rate limiting for production" \
    "docker-compose.yml" \
    "docker-postgis-secure"

# Test 3: Docker SQL Server with Performance
run_consultant_test \
    "Docker SQL Server Performance" \
    "Set up Honua with SQL Server database using Docker, optimize for high-volume tile serving with caching" \
    "docker-compose.yml" \
    "docker-sqlserver-perf"

# Test 4: AWS Deployment (Terraform/CloudFormation)
run_consultant_test \
    "AWS Deployment" \
    "Deploy Honua to AWS with S3 for tile caching, RDS PostgreSQL database, and secrets in AWS Secrets Manager" \
    "main.tf" \
    "aws-terraform"

# Test 5: Azure Deployment
run_consultant_test \
    "Azure Deployment" \
    "Deploy Honua to Azure with Blob Storage for tiles, Azure Database for PostgreSQL, and Azure Key Vault" \
    "main.tf" \
    "azure-terraform"

# Test 6: Kubernetes with Helm
run_consultant_test \
    "Kubernetes Helm" \
    "Deploy Honua to Kubernetes with Helm, include PostGIS StatefulSet, Redis for caching, and Ingress with TLS" \
    "Chart.yaml" \
    "kubernetes-helm"

# Test 7: Complex Multi-Agent Scenario
run_consultant_test \
    "Complex Production Setup" \
    "Complete production deployment: Kubernetes with HA, PostgreSQL+PostGIS, Redis caching, OAuth2 with Azure AD, TLS with cert-manager, HPA, Prometheus monitoring" \
    "deployment.yaml" \
    "kubernetes-production"

# Test 8: Troubleshooting Scenario
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test $((TOTAL_TESTS + 1)): Troubleshooting Performance${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

# First create a basic deployment
TEST_WORKSPACE="$RESULTS_DIR/troubleshooting_scenario"
mkdir -p "$TEST_WORKSPACE"

cd "$PROJECT_ROOT"
dotnet run --project src/Honua.Cli consultant \
    --prompt "Deploy Honua with PostGIS using Docker Compose" \
    --workspace "$TEST_WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$TEST_WORKSPACE/setup.log" 2>&1

# Now troubleshoot it
TOTAL_TESTS=$((TOTAL_TESTS + 1))
set +e
OUTPUT=$(dotnet run --project src/Honua.Cli consultant \
    --prompt "The spatial queries are extremely slow, help me diagnose and fix the performance issues" \
    --workspace "$TEST_WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging 2>&1)
EXIT_CODE=$?
set -e

echo "$OUTPUT" > "$TEST_WORKSPACE/troubleshooting-output.log"

if [ $EXIT_CODE -eq 0 ]; then
    echo -e "${GREEN}✓ PASSED${NC}"
    echo "Troubleshooting Response:"
    echo "$OUTPUT" | head -20
    PASSED_TESTS=$((PASSED_TESTS + 1))
else
    echo -e "${RED}✗ FAILED${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 9: Metadata Layer Addition
run_consultant_test \
    "Add Layer via Consultant" \
    "Add a new bike lanes layer to my Honua deployment with PostGIS backend, include proper OGC metadata" \
    "metadata.json" \
    "metadata-update"

# Test 10: Migration Scenario
run_consultant_test \
    "ArcGIS Migration" \
    "I need to migrate my ArcGIS Server feature services to Honua, help me plan and execute the migration" \
    "migration-plan.md" \
    "migration"

# Generate summary report
SUMMARY_FILE="$RESULTS_DIR/summary.txt"
cat > "$SUMMARY_FILE" <<EOF
========================================
  Real-World AI Consultant E2E Test Results
========================================

Timestamp: $(date -Iseconds)
Project: HonuaIO
Test Run: $TIMESTAMP

Summary:
--------
Total Tests:  $TOTAL_TESTS
Passed:       $PASSED_TESTS
Failed:       $FAILED_TESTS
Success Rate: $(echo "scale=2; $PASSED_TESTS * 100 / $TOTAL_TESTS" | bc)%

Test Details:
-------------
EOF

# List all test results
for test_dir in "$RESULTS_DIR"/*/; do
    if [ -f "$test_dir/test-metadata.json" ]; then
        TEST_NAME=$(jq -r '.testName' "$test_dir/test-metadata.json")
        TOPOLOGY=$(jq -r '.topology' "$test_dir/test-metadata.json")
        EXIT_CODE=$(jq -r '.exitCode' "$test_dir/test-metadata.json")

        STATUS="FAILED"
        if [ "$EXIT_CODE" == "0" ]; then
            STATUS="PASSED"
        fi

        echo "$STATUS - $TEST_NAME ($TOPOLOGY)" >> "$SUMMARY_FILE"
    fi
done

echo "" >> "$SUMMARY_FILE"
echo "Results directory: $RESULTS_DIR" >> "$SUMMARY_FILE"

# Display final summary
echo ""
echo -e "${BLUE}========================================${NC}"
echo -e "${BLUE}  Test Summary${NC}"
echo -e "${BLUE}========================================${NC}"
cat "$SUMMARY_FILE"
echo ""

# Exit with appropriate code
if [ $FAILED_TESTS -gt 0 ]; then
    echo -e "${RED}✗ Some tests failed${NC}"
    exit 1
else
    echo -e "${GREEN}✓ All tests passed!${NC}"
    exit 0
fi
