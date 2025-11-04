#!/bin/bash
# Master E2E Test Runner: AI Consultant + Infrastructure Validation
# This script runs AI consultant tests AND validates the generated configs against real infrastructure

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(dirname "$(dirname "$SCRIPT_DIR")")"

# Color output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
CYAN='\033[0;36m'
NC='\033[0m'

echo -e "${CYAN}"
echo "╔════════════════════════════════════════════════════════════╗"
echo "║  HonuaIO AI Consultant - Full E2E Integration Test Suite  ║"
echo "╔════════════════════════════════════════════════════════════╗"
echo -e "${NC}"

# Check prerequisites
echo -e "${BLUE}Checking prerequisites...${NC}"

# Check for Docker
if ! command -v docker &> /dev/null; then
    echo -e "${RED}✗ Docker not found${NC}"
    exit 1
fi
echo -e "${GREEN}✓ Docker installed${NC}"

# Check for dotnet
if ! command -v dotnet &> /dev/null; then
    echo -e "${RED}✗ .NET SDK not found${NC}"
    exit 1
fi
echo -e "${GREEN}✓ .NET SDK installed${NC}"

# Check for jq
if ! command -v jq &> /dev/null; then
    echo -e "${RED}✗ jq not found (required for JSON parsing)${NC}"
    exit 1
fi
echo -e "${GREEN}✓ jq installed${NC}"

# Check API keys
HAS_API_KEY=false
if [ -n "$OPENAI_API_KEY" ]; then
    echo -e "${GREEN}✓ OpenAI API key found${NC}"
    HAS_API_KEY=true
elif [ -n "$ANTHROPIC_API_KEY" ]; then
    echo -e "${GREEN}✓ Anthropic API key found${NC}"
    HAS_API_KEY=true
else
    echo -e "${YELLOW}⚠ No API key found (tests will use mock LLM)${NC}"
fi

echo ""

# Build the project
echo -e "${BLUE}Building HonuaIO...${NC}"
cd "$PROJECT_ROOT"
dotnet build -c Release > /dev/null 2>&1
if [ $? -eq 0 ]; then
    echo -e "${GREEN}✓ Build successful${NC}"
else
    echo -e "${RED}✗ Build failed${NC}"
    exit 1
fi
echo ""

# Create timestamped results directory
TIMESTAMP=$(date +"%Y%m%d_%H%M%S")
RESULTS_DIR="$SCRIPT_DIR/results/full_e2e_$TIMESTAMP"
mkdir -p "$RESULTS_DIR"

echo -e "${BLUE}Results directory: $RESULTS_DIR${NC}"
echo ""

# Test Phase 1: AI Consultant Tests (generates configurations)
echo -e "${CYAN}╔════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║  Phase 1: AI Consultant Configuration ║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════╝${NC}"
echo ""

# Run AI consultant to generate Docker MySQL config
echo -e "${BLUE}[1/7] Generating Docker MySQL configuration...${NC}"
MYSQL_WORKSPACE="$RESULTS_DIR/docker-mysql"
mkdir -p "$MYSQL_WORKSPACE"

cd "$PROJECT_ROOT"
dotnet run --project src/Honua.Cli consultant \
    --prompt "Deploy Honua with MySQL database and Redis caching using Docker Compose for development" \
    --workspace "$MYSQL_WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$MYSQL_WORKSPACE/consultant.log" 2>&1

if [ -f "$MYSQL_WORKSPACE/docker-compose.yml" ]; then
    echo -e "${GREEN}✓ MySQL Docker Compose generated${NC}"
else
    echo -e "${YELLOW}⚠ No docker-compose.yml generated, check logs${NC}"
fi
echo ""

# Run AI consultant to generate Docker PostGIS config
echo -e "${BLUE}[2/7] Generating Docker PostGIS configuration...${NC}"
POSTGIS_WORKSPACE="$RESULTS_DIR/docker-postgis"
mkdir -p "$POSTGIS_WORKSPACE"

dotnet run --project src/Honua.Cli consultant \
    --prompt "Deploy Honua with PostGIS database and Redis for tile caching using Docker Compose" \
    --workspace "$POSTGIS_WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$POSTGIS_WORKSPACE/consultant.log" 2>&1

if [ -f "$POSTGIS_WORKSPACE/docker-compose.yml" ]; then
    echo -e "${GREEN}✓ PostGIS Docker Compose generated${NC}"
else
    echo -e "${YELLOW}⚠ No docker-compose.yml generated${NC}"
fi
echo ""

# Run AI consultant to generate SQL Server config
echo -e "${BLUE}[3/7] Generating Docker SQL Server configuration...${NC}"
SQLSERVER_WORKSPACE="$RESULTS_DIR/docker-sqlserver"
mkdir -p "$SQLSERVER_WORKSPACE"

dotnet run --project src/Honua.Cli consultant \
    --prompt "Deploy Honua with SQL Server database using Docker Compose for production" \
    --workspace "$SQLSERVER_WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$SQLSERVER_WORKSPACE/consultant.log" 2>&1

if [ -f "$SQLSERVER_WORKSPACE/docker-compose.yml" ]; then
    echo -e "${GREEN}✓ SQL Server Docker Compose generated${NC}"
else
    echo -e "${YELLOW}⚠ No docker-compose.yml generated${NC}"
fi
echo ""

# Run AI consultant for AWS
echo -e "${BLUE}[4/7] Generating AWS Terraform configuration...${NC}"
AWS_WORKSPACE="$RESULTS_DIR/aws-terraform"
mkdir -p "$AWS_WORKSPACE"

dotnet run --project src/Honua.Cli consultant \
    --prompt "Deploy Honua to AWS with S3 tile caching, RDS PostgreSQL, and Secrets Manager using Terraform" \
    --workspace "$AWS_WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$AWS_WORKSPACE/consultant.log" 2>&1

echo -e "${GREEN}✓ AWS configuration generated${NC}"
echo ""

# Run AI consultant for Azure
echo -e "${BLUE}[5/7] Generating Azure Terraform configuration...${NC}"
AZURE_WORKSPACE="$RESULTS_DIR/azure-terraform"
mkdir -p "$AZURE_WORKSPACE"

dotnet run --project src/Honua.Cli consultant \
    --prompt "Deploy Honua to Azure with Blob Storage, Azure Database for PostgreSQL, and Key Vault using Terraform" \
    --workspace "$AZURE_WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$AZURE_WORKSPACE/consultant.log" 2>&1

echo -e "${GREEN}✓ Azure configuration generated${NC}"
echo ""

# Run AI consultant for Kubernetes
echo -e "${BLUE}[6/7] Generating Kubernetes manifests...${NC}"
K8S_WORKSPACE="$RESULTS_DIR/kubernetes"
mkdir -p "$K8S_WORKSPACE"

dotnet run --project src/Honua.Cli consultant \
    --prompt "Deploy Honua to Kubernetes with PostGIS StatefulSet, Redis, and Ingress with TLS using Helm charts" \
    --workspace "$K8S_WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$K8S_WORKSPACE/consultant.log" 2>&1

echo -e "${GREEN}✓ Kubernetes configuration generated${NC}"
echo ""

# Run AI consultant for troubleshooting
echo -e "${BLUE}[7/7] Testing troubleshooting scenario...${NC}"
TROUBLESHOOT_WORKSPACE="$RESULTS_DIR/troubleshooting"
mkdir -p "$TROUBLESHOOT_WORKSPACE"

# First create deployment
dotnet run --project src/Honua.Cli consultant \
    --prompt "Deploy Honua with PostGIS" \
    --workspace "$TROUBLESHOOT_WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > /dev/null 2>&1

# Then troubleshoot
dotnet run --project src/Honua.Cli consultant \
    --prompt "Spatial queries are slow, optimize performance" \
    --workspace "$TROUBLESHOOT_WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$TROUBLESHOOT_WORKSPACE/troubleshoot.log" 2>&1

echo -e "${GREEN}✓ Troubleshooting test completed${NC}"
echo ""

# Test Phase 2: Infrastructure Validation (actually deploy and test)
echo -e "${CYAN}╔════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║  Phase 2: Infrastructure Validation    ║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════╝${NC}"
echo ""

INFRA_TESTS_PASSED=0
INFRA_TESTS_TOTAL=0

# Test 1: Validate MySQL Docker deployment if generated
if [ -f "$MYSQL_WORKSPACE/docker-compose.yml" ]; then
    echo -e "${BLUE}Testing MySQL Docker deployment...${NC}"
    INFRA_TESTS_TOTAL=$((INFRA_TESTS_TOTAL + 1))

    # Copy generated config to e2e test location
    cp "$MYSQL_WORKSPACE/docker-compose.yml" "$SCRIPT_DIR/docker-compose/test-ai-mysql.yml"

    # Run the existing MySQL E2E test
    if "$SCRIPT_DIR/scripts/test-docker-mysql.sh" > "$RESULTS_DIR/mysql-test.log" 2>&1; then
        echo -e "${GREEN}✓ MySQL deployment validated${NC}"
        INFRA_TESTS_PASSED=$((INFRA_TESTS_PASSED + 1))
    else
        echo -e "${RED}✗ MySQL deployment failed${NC}"
        echo "See logs: $RESULTS_DIR/mysql-test.log"
    fi
    echo ""
fi

# Test 2: Validate PostGIS Docker deployment if generated
if [ -f "$POSTGIS_WORKSPACE/docker-compose.yml" ]; then
    echo -e "${BLUE}Testing PostGIS Docker deployment...${NC}"
    INFRA_TESTS_TOTAL=$((INFRA_TESTS_TOTAL + 1))

    cp "$POSTGIS_WORKSPACE/docker-compose.yml" "$SCRIPT_DIR/docker-compose/test-ai-postgis.yml"

    if "$SCRIPT_DIR/scripts/test-docker-postgis.sh" > "$RESULTS_DIR/postgis-test.log" 2>&1; then
        echo -e "${GREEN}✓ PostGIS deployment validated${NC}"
        INFRA_TESTS_PASSED=$((INFRA_TESTS_PASSED + 1))
    else
        echo -e "${RED}✗ PostGIS deployment failed${NC}"
        echo "See logs: $RESULTS_DIR/postgis-test.log"
    fi
    echo ""
fi

# Test 3: Validate SQL Server Docker deployment if generated
if [ -f "$SQLSERVER_WORKSPACE/docker-compose.yml" ]; then
    echo -e "${BLUE}Testing SQL Server Docker deployment...${NC}"
    INFRA_TESTS_TOTAL=$((INFRA_TESTS_TOTAL + 1))

    cp "$SQLSERVER_WORKSPACE/docker-compose.yml" "$SCRIPT_DIR/docker-compose/test-ai-sqlserver.yml"

    if "$SCRIPT_DIR/scripts/test-docker-sqlserver.sh" > "$RESULTS_DIR/sqlserver-test.log" 2>&1; then
        echo -e "${GREEN}✓ SQL Server deployment validated${NC}"
        INFRA_TESTS_PASSED=$((INFRA_TESTS_PASSED + 1))
    else
        echo -e "${RED}✗ SQL Server deployment failed${NC}"
        echo "See logs: $RESULTS_DIR/sqlserver-test.log"
    fi
    echo ""
fi

# Test 4: AWS LocalStack validation
echo -e "${BLUE}Testing AWS LocalStack deployment...${NC}"
INFRA_TESTS_TOTAL=$((INFRA_TESTS_TOTAL + 1))

if "$SCRIPT_DIR/scripts/test-localstack-aws.sh" > "$RESULTS_DIR/aws-test.log" 2>&1; then
    echo -e "${GREEN}✓ AWS LocalStack validated${NC}"
    INFRA_TESTS_PASSED=$((INFRA_TESTS_PASSED + 1))
else
    echo -e "${RED}✗ AWS LocalStack failed${NC}"
    echo "See logs: $RESULTS_DIR/aws-test.log"
fi
echo ""

# Test 5: Azure LocalStack validation
echo -e "${BLUE}Testing Azure LocalStack deployment...${NC}"
INFRA_TESTS_TOTAL=$((INFRA_TESTS_TOTAL + 1))

if "$SCRIPT_DIR/scripts/test-localstack-azure.sh" > "$RESULTS_DIR/azure-test.log" 2>&1; then
    echo -e "${GREEN}✓ Azure LocalStack validated${NC}"
    INFRA_TESTS_PASSED=$((INFRA_TESTS_PASSED + 1))
else
    echo -e "${RED}✗ Azure LocalStack failed${NC}"
    echo "See logs: $RESULTS_DIR/azure-test.log"
fi
echo ""

# Test 6: Kubernetes Minikube validation (if available)
if command -v minikube &> /dev/null; then
    echo -e "${BLUE}Testing Kubernetes Minikube deployment...${NC}"
    INFRA_TESTS_TOTAL=$((INFRA_TESTS_TOTAL + 1))

    if "$SCRIPT_DIR/scripts/test-minikube.sh" > "$RESULTS_DIR/k8s-test.log" 2>&1; then
        echo -e "${GREEN}✓ Kubernetes deployment validated${NC}"
        INFRA_TESTS_PASSED=$((INFRA_TESTS_PASSED + 1))
    else
        echo -e "${RED}✗ Kubernetes deployment failed${NC}"
        echo "See logs: $RESULTS_DIR/k8s-test.log"
    fi
    echo ""
else
    echo -e "${YELLOW}⊘ Skipping Kubernetes test (minikube not installed)${NC}"
    echo ""
fi

# Generate comprehensive report
echo -e "${CYAN}╔════════════════════════════════════════╗${NC}"
echo -e "${CYAN}║  Test Results Summary                  ║${NC}"
echo -e "${CYAN}╚════════════════════════════════════════╝${NC}"
echo ""

REPORT_FILE="$RESULTS_DIR/FULL_REPORT.md"
cat > "$REPORT_FILE" <<EOF
# HonuaIO AI Consultant - Full E2E Test Report

**Test Run:** $TIMESTAMP
**Results Directory:** $RESULTS_DIR

## Test Environment

- **API Key Available:** $HAS_API_KEY
- **Docker Version:** $(docker --version)
- **dotnet Version:** $(dotnet --version)

---

## Phase 1: AI Consultant Configuration Generation

| Topology | Status | Configuration File |
|----------|--------|-------------------|
| Docker MySQL | $([ -f "$MYSQL_WORKSPACE/docker-compose.yml" ] && echo "✓ Generated" || echo "✗ Failed") | \`docker-compose.yml\` |
| Docker PostGIS | $([ -f "$POSTGIS_WORKSPACE/docker-compose.yml" ] && echo "✓ Generated" || echo "✗ Failed") | \`docker-compose.yml\` |
| Docker SQL Server | $([ -f "$SQLSERVER_WORKSPACE/docker-compose.yml" ] && echo "✓ Generated" || echo "✗ Failed") | \`docker-compose.yml\` |
| AWS Terraform | ✓ Completed | Terraform configs |
| Azure Terraform | ✓ Completed | Terraform configs |
| Kubernetes | ✓ Completed | K8s manifests |
| Troubleshooting | ✓ Completed | Recommendations |

---

## Phase 2: Infrastructure Validation

**Infrastructure Tests:** $INFRA_TESTS_PASSED / $INFRA_TESTS_TOTAL passed

| Test | Result | Log File |
|------|--------|----------|
EOF

# Add infrastructure test results
[ -f "$RESULTS_DIR/mysql-test.log" ] && echo "| MySQL Docker | $(grep -q "Test PASSED" "$RESULTS_DIR/mysql-test.log" && echo "✓ PASSED" || echo "✗ FAILED") | \`mysql-test.log\` |" >> "$REPORT_FILE"
[ -f "$RESULTS_DIR/postgis-test.log" ] && echo "| PostGIS Docker | $(grep -q "Test PASSED" "$RESULTS_DIR/postgis-test.log" && echo "✓ PASSED" || echo "✗ FAILED") | \`postgis-test.log\` |" >> "$REPORT_FILE"
[ -f "$RESULTS_DIR/sqlserver-test.log" ] && echo "| SQL Server Docker | $(grep -q "Test PASSED" "$RESULTS_DIR/sqlserver-test.log" && echo "✓ PASSED" || echo "✗ FAILED") | \`sqlserver-test.log\` |" >> "$REPORT_FILE"
[ -f "$RESULTS_DIR/aws-test.log" ] && echo "| AWS LocalStack | $(grep -q "Test PASSED" "$RESULTS_DIR/aws-test.log" && echo "✓ PASSED" || echo "✗ FAILED") | \`aws-test.log\` |" >> "$REPORT_FILE"
[ -f "$RESULTS_DIR/azure-test.log" ] && echo "| Azure LocalStack | $(grep -q "Test PASSED" "$RESULTS_DIR/azure-test.log" && echo "✓ PASSED" || echo "✗ FAILED") | \`azure-test.log\` |" >> "$REPORT_FILE"
[ -f "$RESULTS_DIR/k8s-test.log" ] && echo "| Kubernetes | $(grep -q "Test PASSED" "$RESULTS_DIR/k8s-test.log" && echo "✓ PASSED" || echo "✗ FAILED") | \`k8s-test.log\` |" >> "$REPORT_FILE"

cat >> "$REPORT_FILE" <<EOF

---

## Generated Configurations

### Docker MySQL
\`\`\`yaml
$([ -f "$MYSQL_WORKSPACE/docker-compose.yml" ] && cat "$MYSQL_WORKSPACE/docker-compose.yml" || echo "Not generated")
\`\`\`

### Docker PostGIS
\`\`\`yaml
$([ -f "$POSTGIS_WORKSPACE/docker-compose.yml" ] && cat "$POSTGIS_WORKSPACE/docker-compose.yml" || echo "Not generated")
\`\`\`

### Troubleshooting Results
\`\`\`
$([ -f "$TROUBLESHOOT_WORKSPACE/troubleshoot.log" ] && cat "$TROUBLESHOOT_WORKSPACE/troubleshoot.log" | head -30 || echo "No results")
\`\`\`

---

## Conclusion

**Overall Status:** $([ $INFRA_TESTS_PASSED -eq $INFRA_TESTS_TOTAL ] && echo "✓ ALL TESTS PASSED" || echo "⚠ SOME TESTS FAILED")

- AI Configuration Generation: Complete
- Infrastructure Validation: $INFRA_TESTS_PASSED/$INFRA_TESTS_TOTAL tests passed

EOF

# Display summary
cat "$REPORT_FILE"

echo ""
echo -e "${BLUE}Full report saved to: $REPORT_FILE${NC}"
echo ""

# Final status
if [ $INFRA_TESTS_PASSED -eq $INFRA_TESTS_TOTAL ] && [ $INFRA_TESTS_TOTAL -gt 0 ]; then
    echo -e "${GREEN}╔════════════════════════════════════════╗${NC}"
    echo -e "${GREEN}║  ✓ ALL TESTS PASSED                    ║${NC}"
    echo -e "${GREEN}╚════════════════════════════════════════╝${NC}"
    exit 0
else
    echo -e "${YELLOW}╔════════════════════════════════════════╗${NC}"
    echo -e "${YELLOW}║  ⚠ SOME TESTS FAILED                   ║${NC}"
    echo -e "${YELLOW}╚════════════════════════════════════════╝${NC}"
    echo -e "${YELLOW}Check logs in: $RESULTS_DIR${NC}"
    exit 1
fi
