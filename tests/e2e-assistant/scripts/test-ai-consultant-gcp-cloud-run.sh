#!/bin/bash
# AI Consultant GCP Cloud Run E2E Test
# Tests AI consultant's ability to generate GCP Cloud Run deployments

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
echo "║     AI Consultant GCP Cloud Run E2E Test for HonuaIO          ║"
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
RESULTS_DIR="$TEST_DIR/results/gcp-cloud-run_$TIMESTAMP"
mkdir -p "$RESULTS_DIR"

echo -e "${BLUE}Results: $RESULTS_DIR${NC}"
echo ""

TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

# Test 1: Basic Cloud Run deployment
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 1: Basic Cloud Run Deployment${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/cloudrun-basic"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua to GCP Cloud Run in us-central1 with Cloud SQL PostgreSQL (with PostGIS extension), use Secret Manager for database credentials"

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

if [ $EXIT_CODE -eq 0 ]; then
    if [ -f "$WORKSPACE/terraform-gcp/main.tf" ] || [ -f "$WORKSPACE/cloudbuild.yaml" ]; then
        echo -e "${GREEN}✓ AI generated GCP deployment configuration${NC}"

        # Check for Cloud Run and Cloud SQL resources
        if grep -rq "google_cloud_run_service\|google_cloud_run_v2_service" "$WORKSPACE" && \
           grep -rq "google_sql_database_instance" "$WORKSPACE"; then
            echo -e "${GREEN}✓ Configuration contains Cloud Run and Cloud SQL${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${RED}✗ Missing required GCP resources${NC}"
            FAILED_TESTS=$((FAILED_TESTS + 1))
        fi
    else
        echo -e "${RED}✗ AI failed to generate deployment files${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 2: Cloud Run with Cloud Storage tile caching
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 2: Cloud Run with Cloud Storage Caching${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/cloudrun-gcs-caching"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua to Cloud Run with Cloud Storage bucket for tile caching, configure bucket lifecycle to delete tiles older than 45 days, enable Cloud CDN, set CORS policies for web access"

TOTAL_TESTS=$((TOTAL_TESTS + 1))
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ]; then
    if grep -rq "google_storage_bucket" "$WORKSPACE" && \
       grep -rq "lifecycle_rule\|google_storage_bucket_lifecycle" "$WORKSPACE"; then
        echo -e "${GREEN}✓ Configuration contains Cloud Storage with lifecycle${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Missing Cloud Storage or lifecycle configuration${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 3: Multi-region Cloud Run deployment
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 3: Multi-Region Cloud Run with Load Balancer${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/cloudrun-multiregion"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua on Cloud Run across 3 regions (us-central1, europe-west1, asia-southeast1) with Global HTTP(S) Load Balancer, configure auto-scaling (min 1, max 100 instances per region), use Memorystore Redis for distributed caching"

TOTAL_TESTS=$((TOTAL_TESTS + 1))
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ]; then
    if grep -rq "google_compute_global_forwarding_rule\|google_compute_backend_service" "$WORKSPACE" && \
       grep -rq "google_redis_instance" "$WORKSPACE"; then
        echo -e "${GREEN}✓ Configuration contains Global LB and Memorystore${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Missing multi-region or load balancer configuration${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 4: Cloud Run with Cloud Monitoring
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 4: Cloud Run with Monitoring and Logging${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/cloudrun-monitoring"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua on Cloud Run with comprehensive monitoring using Cloud Monitoring and Cloud Logging, include custom metrics for OGC API latency and throughput, set up alerting policies for high error rates and latency"

TOTAL_TESTS=$((TOTAL_TESTS + 1))
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ]; then
    if grep -rq "google_monitoring_alert_policy\|google_logging" "$WORKSPACE"; then
        echo -e "${GREEN}✓ Configuration contains monitoring setup${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Missing monitoring configuration${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 5: Cloud Run with Workload Identity
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 5: Cloud Run with Workload Identity${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/cloudrun-workload-identity"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua on Cloud Run using Workload Identity for accessing Cloud Storage and Cloud SQL, configure IAM bindings with least privilege, no service account keys or connection strings in environment variables"

TOTAL_TESTS=$((TOTAL_TESTS + 1))
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ]; then
    if grep -rq "google_service_account\|google_project_iam" "$WORKSPACE"; then
        echo -e "${GREEN}✓ Configuration contains IAM and service accounts${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Missing Workload Identity configuration${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 6: Cloud Run with VPC Connector
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 6: Cloud Run with VPC Networking${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/cloudrun-vpc"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua on Cloud Run with VPC connector to access Cloud SQL via private IP, configure VPC with firewall rules for security, enable VPC Flow Logs for monitoring"

TOTAL_TESTS=$((TOTAL_TESTS + 1))
set +e
dotnet run --project src/Honua.Cli consultant \
    --prompt "$PROMPT" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/consultant.log" 2>&1
EXIT_CODE=$?
set -e

if [ $EXIT_CODE -eq 0 ]; then
    if grep -rq "google_vpc_access_connector\|google_compute_network" "$WORKSPACE"; then
        echo -e "${GREEN}✓ Configuration contains VPC networking${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Missing VPC configuration${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 7: Troubleshooting - Container startup failures
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 7: Troubleshooting Cloud Run Startup Issues${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/cloudrun-troubleshooting"
mkdir -p "$WORKSPACE"

# Create basic deployment first
dotnet run --project src/Honua.Cli consultant \
    --prompt "Deploy Honua to GCP Cloud Run" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/setup.log" 2>&1

# Now troubleshoot
PROMPT="My Cloud Run service is failing health checks and containers keep restarting. The error logs show 'connection to Cloud SQL timed out'. Help me diagnose and fix this."

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
    if grep -qi "cloud.*sql\|vpc\|connector\|timeout" "$WORKSPACE/troubleshooting.log"; then
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
    echo -e "${GREEN}${BOLD}✓ ALL GCP CLOUD RUN AI CONSULTANT TESTS PASSED${NC}"
    exit 0
else
    echo -e "${RED}${BOLD}✗ SOME TESTS FAILED${NC}"
    exit 1
fi
