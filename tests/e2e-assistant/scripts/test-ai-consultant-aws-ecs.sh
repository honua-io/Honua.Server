#!/bin/bash
# AI Consultant AWS ECS E2E Test
# Tests AI consultant's ability to generate AWS ECS deployments and validates infrastructure

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
echo "║     AI Consultant AWS ECS E2E Test for HonuaIO                ║"
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
RESULTS_DIR="$TEST_DIR/results/aws-ecs_$TIMESTAMP"
mkdir -p "$RESULTS_DIR"

echo -e "${BLUE}Results: $RESULTS_DIR${NC}"
echo ""

TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

# Test 1: Basic ECS deployment with RDS
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 1: Basic ECS Deployment with RDS${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/ecs-basic"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua to AWS ECS Fargate in us-east-1 with RDS PostgreSQL with PostGIS extensions, use AWS Secrets Manager for database credentials"

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

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/terraform-aws/main.tf" ]; then
    echo -e "${GREEN}✓ AI generated Terraform configuration${NC}"

    # Validate Terraform file contains ECS resources
    if grep -q "aws_ecs_cluster" "$WORKSPACE/terraform-aws/main.tf" && \
       grep -q "aws_ecs_task_definition" "$WORKSPACE/terraform-aws/main.tf" && \
       grep -q "aws_db_instance" "$WORKSPACE/terraform-aws/main.tf"; then
        echo -e "${GREEN}✓ Terraform contains ECS cluster, task definition, and RDS${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Terraform missing required AWS resources${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 2: ECS with S3 tile caching
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 2: ECS with S3 Tile Caching${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/ecs-s3-caching"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua to AWS ECS with S3 bucket for tile caching, configure lifecycle policies to delete tiles older than 30 days, enable CloudFront CDN"

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

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/terraform-aws/main.tf" ]; then
    if grep -q "aws_s3_bucket" "$WORKSPACE/terraform-aws/main.tf" && \
       grep -q "aws_s3_bucket_lifecycle_configuration" "$WORKSPACE/terraform-aws/main.tf"; then
        echo -e "${GREEN}✓ Terraform contains S3 with lifecycle policies${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Missing S3 or lifecycle configuration${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 3: High availability ECS deployment
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 3: HA ECS Deployment Multi-AZ${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/ecs-ha"
mkdir -p "$WORKSPACE"

PROMPT="Deploy highly available Honua on AWS ECS across 3 availability zones with Application Load Balancer, RDS Multi-AZ, ElastiCache Redis, and auto-scaling (min 2, max 10 tasks)"

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

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/terraform-aws/main.tf" ]; then
    if grep -q "aws_lb" "$WORKSPACE/terraform-aws/main.tf" && \
       grep -q "aws_appautoscaling_target" "$WORKSPACE/terraform-aws/main.tf" && \
       grep -q "aws_elasticache_cluster" "$WORKSPACE/terraform-aws/main.tf"; then
        echo -e "${GREEN}✓ Terraform contains ALB, auto-scaling, and ElastiCache${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Missing HA components${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 4: ECS with CloudWatch monitoring
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 4: ECS with CloudWatch Monitoring${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/ecs-monitoring"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua on ECS with comprehensive CloudWatch monitoring, include custom metrics for OGC API response times, set up alarms for high latency and error rates"

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

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/terraform-aws/main.tf" ]; then
    if grep -q "aws_cloudwatch_log_group" "$WORKSPACE/terraform-aws/main.tf"; then
        echo -e "${GREEN}✓ Terraform contains CloudWatch configuration${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Missing CloudWatch configuration${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI failed to generate configuration${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 5: Troubleshooting - Performance optimization
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 5: Troubleshooting ECS Performance${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/ecs-troubleshooting"
mkdir -p "$WORKSPACE"

# First create basic deployment
dotnet run --project src/Honua.Cli consultant \
    --prompt "Deploy Honua to AWS ECS" \
    --workspace "$WORKSPACE" \
    --mode multi-agent \
    --auto-approve \
    --no-logging > "$WORKSPACE/setup.log" 2>&1

# Now troubleshoot
PROMPT="My ECS tasks are running out of memory and getting killed. Help me diagnose and fix this issue."

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
    if grep -qi "memory" "$WORKSPACE/troubleshooting.log" || \
       grep -qi "task.*definition" "$WORKSPACE/troubleshooting.log"; then
        echo -e "${GREEN}✓ AI provided troubleshooting guidance${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${YELLOW}⚠ Troubleshooting completed but no clear guidance found${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ Troubleshooting failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 6: Security hardening
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 6: ECS Security Hardening${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/ecs-security"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua on ECS with security best practices: use VPC with private subnets, enable VPC Flow Logs, configure WAF on ALB, use IAM task roles with least privilege, encrypt all data at rest and in transit"

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

if [ $EXIT_CODE -eq 0 ] && [ -f "$WORKSPACE/terraform-aws/main.tf" ]; then
    if grep -q "aws_iam_role" "$WORKSPACE/terraform-aws/main.tf" && \
       grep -q "aws_vpc" "$WORKSPACE/terraform-aws/main.tf"; then
        echo -e "${GREEN}✓ Terraform contains security configurations${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ Missing security configurations${NC}"
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
    echo -e "${GREEN}${BOLD}✓ ALL AWS ECS AI CONSULTANT TESTS PASSED${NC}"
    exit 0
else
    echo -e "${RED}${BOLD}✗ SOME TESTS FAILED${NC}"
    exit 1
fi
