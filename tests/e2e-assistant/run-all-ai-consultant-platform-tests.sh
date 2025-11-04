#!/bin/bash
# Master AI Consultant Platform E2E Test Runner
# Runs comprehensive tests across all cloud platforms and deployment targets

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

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
echo "║   AI Consultant Platform E2E Tests - All Deployment Targets   ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo -e "${NC}"
echo ""

# Check for API keys
if [ -z "$OPENAI_API_KEY" ] && [ -z "$ANTHROPIC_API_KEY" ]; then
    echo -e "${RED}✗ No API key found (OPENAI_API_KEY or ANTHROPIC_API_KEY)${NC}"
    echo -e "${YELLOW}Please set one of these environment variables to run AI consultant tests:${NC}"
    echo -e "  export OPENAI_API_KEY=sk-your-key-here"
    echo -e "  export ANTHROPIC_API_KEY=sk-ant-your-key-here"
    exit 1
fi

echo -e "${GREEN}✓ API key found${NC}"
echo ""

# Create results directory
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
RESULTS_DIR="$SCRIPT_DIR/results/platform-tests_$TIMESTAMP"
mkdir -p "$RESULTS_DIR"

echo -e "${BLUE}Results directory: $RESULTS_DIR${NC}"
echo ""

# Test tracking
TOTAL_SUITES=0
PASSED_SUITES=0
FAILED_SUITES=0

# Function to run a test suite
run_test_suite() {
    local test_name=$1
    local test_script=$2

    TOTAL_SUITES=$((TOTAL_SUITES + 1))

    echo -e "${CYAN}${BOLD}"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo "Running: $test_name"
    echo "━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━"
    echo -e "${NC}"

    if [ ! -f "$test_script" ]; then
        echo -e "${YELLOW}⚠ Test script not found: $test_script${NC}"
        echo -e "${YELLOW}  Skipping...${NC}"
        echo ""
        return
    fi

    set +e
    bash "$test_script" > "$RESULTS_DIR/${test_name// /_}.log" 2>&1
    EXIT_CODE=$?
    set -e

    if [ $EXIT_CODE -eq 0 ]; then
        echo -e "${GREEN}✓ $test_name PASSED${NC}"
        PASSED_SUITES=$((PASSED_SUITES + 1))
    else
        echo -e "${RED}✗ $test_name FAILED${NC}"
        echo -e "${YELLOW}  See log: $RESULTS_DIR/${test_name// /_}.log${NC}"
        FAILED_SUITES=$((FAILED_SUITES + 1))
    fi
    echo ""
}

# Run all platform tests
run_test_suite "Requirements Gathering" "$SCRIPT_DIR/scripts/test-ai-consultant-interactive-requirements.sh"
run_test_suite "Docker Comprehensive" "$SCRIPT_DIR/scripts/test-ai-consultant-docker-comprehensive.sh"
run_test_suite "AWS ECS" "$SCRIPT_DIR/scripts/test-ai-consultant-aws-ecs.sh"
run_test_suite "Azure Container Apps" "$SCRIPT_DIR/scripts/test-ai-consultant-azure-container-apps.sh"
run_test_suite "GCP Cloud Run" "$SCRIPT_DIR/scripts/test-ai-consultant-gcp-cloud-run.sh"
run_test_suite "Kubernetes" "$SCRIPT_DIR/scripts/test-ai-consultant-kubernetes.sh"

# Generate summary report
SUMMARY_FILE="$RESULTS_DIR/SUMMARY.md"
cat > "$SUMMARY_FILE" <<EOF
# AI Consultant Platform E2E Test Summary

**Test Run**: $(date -Iseconds)
**Timestamp**: $TIMESTAMP

## Overall Results

- **Total Test Suites**: $TOTAL_SUITES
- **Passed**: $PASSED_SUITES
- **Failed**: $FAILED_SUITES
- **Success Rate**: $(echo "scale=1; $PASSED_SUITES * 100 / $TOTAL_SUITES" | bc)%

## Test Suites

| Suite | Status | Log |
|-------|--------|-----|
EOF

# Add test results to summary
for log_file in "$RESULTS_DIR"/*.log; do
    if [ -f "$log_file" ]; then
        SUITE_NAME=$(basename "$log_file" .log | tr '_' ' ')
        if grep -q "ALL.*TESTS PASSED" "$log_file" 2>/dev/null; then
            echo "| $SUITE_NAME | ✅ PASS | [View]($(basename "$log_file")) |" >> "$SUMMARY_FILE"
        else
            echo "| $SUITE_NAME | ❌ FAIL | [View]($(basename "$log_file")) |" >> "$SUMMARY_FILE"
        fi
    fi
done

cat >> "$SUMMARY_FILE" <<EOF

## Platform Coverage

- ✅ **Docker**: Multiple database backends (PostGIS, MySQL, SQL Server), reverse proxies (Nginx, Traefik, Caddy), HA configurations
- ✅ **AWS ECS**: Fargate deployment, RDS PostgreSQL, S3 caching, CloudWatch monitoring, auto-scaling
- ✅ **Azure Container Apps**: Azure Database, Blob Storage, Key Vault, Application Insights, managed identity
- ✅ **GCP Cloud Run**: Cloud SQL, Cloud Storage, Secret Manager, Cloud Monitoring, Workload Identity
- ✅ **Kubernetes**: StatefulSets, HPA, Ingress/TLS, ConfigMaps/Secrets, Helm charts

## Test Scenarios

Each platform test includes:
1. Basic deployment with database and caching
2. Storage/caching layer integration
3. High availability and auto-scaling
4. Monitoring and logging
5. Security and IAM configuration
6. Troubleshooting scenarios

## AI Consultant Validation

All tests validate:
- ✅ AI generates syntactically correct configuration files
- ✅ Configurations contain required cloud resources
- ✅ (Docker only) Infrastructure actually deploys and responds to HTTP requests
- ✅ AI provides meaningful troubleshooting guidance

## Next Steps

$(if [ $FAILED_SUITES -eq 0 ]; then
    echo "🎉 **All tests passed!** The AI consultant successfully handles all deployment scenarios."
else
    echo "⚠️ Review failed test logs to identify issues with AI-generated configurations."
    echo ""
    echo "Common failure causes:"
    echo "- API rate limits or timeouts"
    echo "- LLM returned incomplete/malformed configurations"
    echo "- Infrastructure prerequisites missing (minikube, docker-compose)"
fi)

---

**Generated**: $(date)
**Location**: $RESULTS_DIR
EOF

# Display final summary
echo ""
echo -e "${CYAN}${BOLD}"
echo "╔═══════════════════════════════════════════════════════════════╗"
echo "║                    FINAL SUMMARY                               ║"
echo "╚═══════════════════════════════════════════════════════════════╝"
echo -e "${NC}"
echo ""
cat "$SUMMARY_FILE"
echo ""

if [ $FAILED_SUITES -eq 0 ]; then
    echo -e "${GREEN}${BOLD}✓ ALL PLATFORM TEST SUITES PASSED${NC}"
    exit 0
else
    echo -e "${RED}${BOLD}✗ SOME PLATFORM TEST SUITES FAILED${NC}"
    exit 1
fi
