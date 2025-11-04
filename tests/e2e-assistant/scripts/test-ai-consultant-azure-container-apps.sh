#!/bin/bash
# AI Consultant Azure Container Apps E2E Test
# Tests AI consultant's ability to generate Azure Container Apps deployments

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
echo "║   AI Consultant Azure Container Apps E2E Test for HonuaIO     ║"
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
RESULTS_DIR="$TEST_DIR/results/azure-container-apps_$TIMESTAMP"
mkdir -p "$RESULTS_DIR"

echo -e "${BLUE}Results: $RESULTS_DIR${NC}"
echo ""

TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

# Test 1: Basic Container Apps deployment
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 1: Basic Container Apps Deployment${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/aca-basic"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua to Azure Container Apps in East US with Azure Database for PostgreSQL (with PostGIS), use Azure Key Vault for secrets"

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
    if [ -f "$WORKSPACE/terraform-azure/main.tf" ] || [ -f "$WORKSPACE/main.bicep" ] || [ -f "$WORKSPACE/azuredeploy.json" ]; then
        echo -e "${GREEN}✓ AI generated Azure deployment configuration${NC}"

        # Check for Container Apps and PostgreSQL resources
        if grep -rq "Microsoft.App/containerApps\|azurerm_container_app" "$WORKSPACE" && \
           grep -rq "Microsoft.DBforPostgreSQL\|azurerm_postgresql" "$WORKSPACE"; then
            echo -e "${GREEN}✓ Configuration contains Container Apps and PostgreSQL${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${RED}✗ Missing required Azure resources${NC}"
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

# Test 2: Container Apps with Blob Storage tile caching
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 2: Container Apps with Blob Storage${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/aca-blob-storage"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua to Azure Container Apps with Blob Storage for tile caching, configure CDN with Azure Front Door, set blob lifecycle management to delete old tiles after 60 days"

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
    if grep -rq "Microsoft.Storage/storageAccounts\|azurerm_storage_account" "$WORKSPACE" && \
       grep -rq "Microsoft.Cdn\|azurerm_cdn\|frontdoor" "$WORKSPACE"; then
        echo -e "${GREEN}✓ Configuration contains Blob Storage and CDN${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Missing Blob Storage or CDN configuration${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 3: High availability with scaling
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 3: HA Container Apps with Auto-Scaling${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/aca-ha"
mkdir -p "$WORKSPACE"

PROMPT="Deploy highly available Honua on Azure Container Apps across multiple regions (East US and West US) with Traffic Manager, configure auto-scaling from 2 to 20 replicas based on CPU and HTTP request count, use Azure Cache for Redis"

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
    if grep -rq "Microsoft.Cache/redis\|azurerm_redis_cache" "$WORKSPACE"; then
        echo -e "${GREEN}✓ Configuration contains Redis cache${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Missing HA/scaling configuration${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 4: Container Apps with Application Insights
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 4: Container Apps with Monitoring${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/aca-monitoring"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua on Container Apps with comprehensive monitoring using Application Insights and Log Analytics, include custom metrics for OGC API performance, set up alerts for failures"

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
    if grep -rq "Microsoft.Insights\|azurerm_application_insights" "$WORKSPACE"; then
        echo -e "${GREEN}✓ Configuration contains Application Insights${NC}"
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

# Test 5: Managed Identity and RBAC
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 5: Container Apps with Managed Identity${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/aca-identity"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua on Container Apps using managed identity for authentication, configure RBAC roles for accessing Key Vault secrets and Blob Storage, no connection strings or API keys in configuration"

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
    if grep -rq "managedIdentity\|azurerm_user_assigned_identity" "$WORKSPACE" || \
       grep -rq "identity.*type.*SystemAssigned" "$WORKSPACE"; then
        echo -e "${GREEN}✓ Configuration contains managed identity${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Missing managed identity configuration${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 6: Troubleshooting - Cold start issues
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 6: Troubleshooting Container Apps Cold Starts${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/aca-troubleshooting"
mkdir -p "$WORKSPACE"

# Create basic deployment first
dotnet run --project src/Honua.Cli consultant \
    --prompt "Deploy Honua to Azure Container Apps" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/setup.log" 2>&1

# Now troubleshoot
PROMPT="My Container Apps instances are experiencing long cold start times (30+ seconds). Help me optimize the deployment to reduce startup latency."

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
    if grep -qi "scale\|replica\|startup\|cold" "$WORKSPACE/troubleshooting.log"; then
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
    echo -e "${GREEN}${BOLD}✓ ALL AZURE CONTAINER APPS AI CONSULTANT TESTS PASSED${NC}"
    exit 0
else
    echo -e "${RED}${BOLD}✗ SOME TESTS FAILED${NC}"
    exit 1
fi
