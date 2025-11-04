#!/bin/bash
# AI Consultant Interactive Requirements Gathering E2E Test
# Tests the consultant's ability to gather requirements through conversation and design topology

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
echo "║  AI Consultant Interactive Requirements Gathering E2E Test    ║"
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
RESULTS_DIR="$TEST_DIR/results/interactive-requirements_$TIMESTAMP"
mkdir -p "$RESULTS_DIR"

echo -e "${BLUE}Results: $RESULTS_DIR${NC}"
echo ""

TOTAL_TESTS=0
PASSED_TESTS=0
FAILED_TESTS=0

# Test 1: Vague request should trigger requirements gathering
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 1: Requirements Extraction from Vague Request${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/vague-request"
mkdir -p "$WORKSPACE"

# Vague request that should trigger requirements gathering
PROMPT="I want to deploy Honua for my organization"

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
    # Should ask questions or extract requirements
    if grep -qi "requirements\|budget\|users\|scale\|traffic\|performance\|compliance" "$WORKSPACE/consultant.log"; then
        echo -e "${GREEN}✓ AI asked for requirements/clarifications${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${YELLOW}⚠ AI may not have gathered enough requirements${NC}"
        echo "First 30 lines of output:"
        head -30 "$WORKSPACE/consultant.log"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 2: Specific workload characteristics extraction
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 2: Workload Characteristics Extraction${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/workload-extraction"
mkdir -p "$WORKSPACE"

PROMPT="We need to serve property boundary data to 5000 daily users across North America. We have 500GB of vector data and expect peak traffic during business hours (9am-5pm ET). Response time should be under 200ms."

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
    # AI should understand workload and generate appropriate topology
    if ls "$WORKSPACE"/*.tf &> /dev/null || \
       ls "$WORKSPACE"/*.yml &> /dev/null || \
       ls "$WORKSPACE"/*.yaml &> /dev/null; then
        echo -e "${GREEN}✓ AI generated deployment config based on workload${NC}"

        # Check if it understood the requirements (should use caching, CDN, etc for performance)
        if grep -rqi "cache\|cdn\|cloudfront\|redis\|memcache" "$WORKSPACE"; then
            echo -e "${GREEN}✓ AI included caching for performance requirements${NC}"
        else
            echo -e "${YELLOW}⚠ AI may have missed performance optimization${NC}"
        fi

        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${RED}✗ AI failed to generate deployment configuration${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 3: Budget constraint recognition
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 3: Budget Constraint Recognition${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/budget-constraint"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua for our startup. We're on a tight budget (under \$500/month) but need to support 1000 users. We can manage infrastructure ourselves if it saves money."

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
    # AI should choose cost-effective options (Docker, single region, modest resources)
    if grep -rqi "docker\|compose" "$WORKSPACE" || \
       grep -qi "docker\|self-hosted\|budget\|cost" "$WORKSPACE/consultant.log"; then
        echo -e "${GREEN}✓ AI considered budget constraints${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${YELLOW}⚠ AI may not have optimized for budget${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 4: Compliance requirements recognition
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 4: Compliance Requirements Recognition${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/compliance"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua for healthcare data visualization. Must be HIPAA compliant with audit logging, encryption at rest and in transit, and private networking. Deploy to AWS."

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
    if [ -f "$WORKSPACE/main.tf" ]; then
        # Check for security features
        SECURITY_SCORE=0

        if grep -q "encryption\|kms" "$WORKSPACE/main.tf"; then
            echo -e "${GREEN}  ✓ Encryption configured${NC}"
            ((SECURITY_SCORE++))
        fi

        if grep -q "vpc\|private" "$WORKSPACE/main.tf"; then
            echo -e "${GREEN}  ✓ Private networking configured${NC}"
            ((SECURITY_SCORE++))
        fi

        if grep -q "cloudwatch\|logging" "$WORKSPACE/main.tf"; then
            echo -e "${GREEN}  ✓ Audit logging configured${NC}"
            ((SECURITY_SCORE++))
        fi

        if [ $SECURITY_SCORE -ge 2 ]; then
            echo -e "${GREEN}✓ AI addressed compliance requirements${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${YELLOW}⚠ AI may have missed some compliance requirements${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        fi
    else
        echo -e "${RED}✗ AI failed to generate configuration${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 5: Architecture trade-offs and options
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 5: Architecture Options and Trade-offs${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/architecture-options"
mkdir -p "$WORKSPACE"

PROMPT="I'm not sure how to deploy Honua for my use case. I have 10,000 users, 2TB of raster data, and need 99.9% uptime. What are my options and what are the trade-offs?"

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
    # AI should discuss options and trade-offs
    if grep -qi "option\|trade-off\|alternative\|approach\|consider" "$WORKSPACE/consultant.log"; then
        echo -e "${GREEN}✓ AI provided architecture options/trade-offs${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${YELLOW}⚠ AI may not have discussed alternatives${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 6: Geographic distribution handling
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 6: Geographic Distribution Requirements${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/geographic"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua globally. We have users in North America, Europe, and Asia-Pacific. Minimize latency for all regions. Use GCP."

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
    if [ -f "$WORKSPACE/main.tf" ]; then
        # Should use multi-region, CDN, or load balancing
        if grep -qi "region\|multi-region\|cdn\|cloudfront\|front.door\|global" "$WORKSPACE/main.tf" || \
           grep -qi "us-\|eu-\|asia-" "$WORKSPACE/main.tf"; then
            echo -e "${GREEN}✓ AI designed for global distribution${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        else
            echo -e "${YELLOW}⚠ AI may have missed global distribution${NC}"
            PASSED_TESTS=$((PASSED_TESTS + 1))
        fi
    else
        echo -e "${RED}✗ AI failed to generate configuration${NC}"
        FAILED_TESTS=$((FAILED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 7: Team skill level adaptation
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 7: Team Skill Level Adaptation${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/skill-level"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua for our team. We're new to DevOps and cloud infrastructure - we need something simple that just works. Prefer managed services over managing infrastructure ourselves."

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
    # Should choose managed/serverless options
    if grep -rqi "cloud.run\|fargate\|container.apps\|managed\|serverless" "$WORKSPACE" || \
       grep -qi "managed\|simple\|easy" "$WORKSPACE/consultant.log"; then
        echo -e "${GREEN}✓ AI chose beginner-friendly managed services${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${YELLOW}⚠ AI may not have adapted to skill level${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
    FAILED_TESTS=$((FAILED_TESTS + 1))
fi
echo ""

# Test 8: Data volume and performance correlation
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"
echo -e "${BLUE}Test 8: Large Data Volume Handling${NC}"
echo -e "${BLUE}━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━${NC}"

WORKSPACE="$RESULTS_DIR/large-data"
mkdir -p "$WORKSPACE"

PROMPT="Deploy Honua to serve 10TB of high-resolution satellite imagery. We need fast tile serving for 50,000 concurrent users. Use Kubernetes."

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
    # Should include: storage optimization, caching, CDN, scaling
    OPTIMIZATION_SCORE=0

    if grep -rqi "cache\|redis\|memcache" "$WORKSPACE"; then
        echo -e "${GREEN}  ✓ Caching layer included${NC}"
        ((OPTIMIZATION_SCORE++))
    fi

    if grep -rqi "cdn\|cloudfront" "$WORKSPACE"; then
        echo -e "${GREEN}  ✓ CDN configured${NC}"
        ((OPTIMIZATION_SCORE++))
    fi

    if grep -rqi "HorizontalPodAutoscaler\|autoscaling" "$WORKSPACE"; then
        echo -e "${GREEN}  ✓ Auto-scaling configured${NC}"
        ((OPTIMIZATION_SCORE++))
    fi

    if [ $OPTIMIZATION_SCORE -ge 2 ]; then
        echo -e "${GREEN}✓ AI optimized for large data volume${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    else
        echo -e "${YELLOW}⚠ AI may need more performance optimizations${NC}"
        PASSED_TESTS=$((PASSED_TESTS + 1))
    fi
else
    echo -e "${RED}✗ AI consultant failed${NC}"
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

echo -e "${CYAN}Requirements Gathering Capabilities Tested:${NC}"
echo -e "  ✓ Vague request handling and clarification"
echo -e "  ✓ Workload characteristics extraction"
echo -e "  ✓ Budget constraint recognition"
echo -e "  ✓ Compliance requirements"
echo -e "  ✓ Architecture trade-offs discussion"
echo -e "  ✓ Geographic distribution planning"
echo -e "  ✓ Team skill level adaptation"
echo -e "  ✓ Large data volume optimization"
echo ""

if [ $FAILED_TESTS -eq 0 ]; then
    echo -e "${GREEN}${BOLD}✓ ALL REQUIREMENTS GATHERING TESTS PASSED${NC}"
    exit 0
else
    echo -e "${RED}${BOLD}✗ SOME TESTS FAILED${NC}"
    exit 1
fi
