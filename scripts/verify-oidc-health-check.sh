#!/bin/bash
# OIDC Health Check Verification Script
# Verifies that the OidcDiscoveryHealthCheck is properly registered and responding

set -e

echo "======================================"
echo "OIDC Health Check Verification"
echo "======================================"
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Check if jq is available
if ! command -v jq &> /dev/null; then
    echo -e "${YELLOW}Warning: jq not installed. JSON output will not be formatted.${NC}"
    JQ_AVAILABLE=false
else
    JQ_AVAILABLE=true
fi

# Configuration
HONUA_HOST="${HONUA_HOST:-localhost}"
HONUA_PORT="${HONUA_PORT:-5000}"
BASE_URL="http://${HONUA_HOST}:${HONUA_PORT}"

echo "Target: ${BASE_URL}"
echo ""

# Function to check endpoint
check_endpoint() {
    local endpoint=$1
    local description=$2

    echo "----------------------------------------"
    echo "Checking: ${description}"
    echo "Endpoint: ${endpoint}"
    echo ""

    # Make the request
    response=$(curl -s -w "\nHTTP_STATUS:%{http_code}" "${BASE_URL}${endpoint}" 2>&1 || echo "CURL_ERROR")

    if [[ "$response" == "CURL_ERROR" ]]; then
        echo -e "${RED}✗ Failed to connect to ${BASE_URL}${NC}"
        echo "  Make sure Honua server is running on ${HONUA_HOST}:${HONUA_PORT}"
        return 1
    fi

    # Extract status code and body
    http_status=$(echo "$response" | grep "HTTP_STATUS:" | cut -d':' -f2)
    body=$(echo "$response" | sed '/HTTP_STATUS:/d')

    echo "HTTP Status: ${http_status}"
    echo ""

    # Display formatted output
    if [ "$JQ_AVAILABLE" = true ]; then
        echo "Response:"
        echo "$body" | jq '.'
    else
        echo "Response (unformatted):"
        echo "$body"
    fi

    # Check for OIDC entry
    if [ "$JQ_AVAILABLE" = true ]; then
        oidc_status=$(echo "$body" | jq -r '.entries.oidc.status // "NOT_FOUND"')

        if [ "$oidc_status" != "NOT_FOUND" ]; then
            echo ""
            echo -e "${GREEN}✓ OIDC health check found in response${NC}"
            echo "  Status: ${oidc_status}"

            # Extract additional details
            oidc_description=$(echo "$body" | jq -r '.entries.oidc.description // "N/A"')
            oidc_tags=$(echo "$body" | jq -r '.entries.oidc.tags | join(", ") // "N/A"')

            echo "  Description: ${oidc_description}"
            echo "  Tags: ${oidc_tags}"

            # Check data section
            if echo "$body" | jq -e '.entries.oidc.data' > /dev/null 2>&1; then
                echo ""
                echo "  Diagnostic Data:"
                echo "$body" | jq -r '.entries.oidc.data | to_entries | .[] | "    \(.key): \(.value)"'
            fi
        else
            echo ""
            echo -e "${RED}✗ OIDC health check NOT found in response${NC}"
            return 1
        fi
    else
        # Basic check without jq
        if echo "$body" | grep -q '"oidc"'; then
            echo ""
            echo -e "${GREEN}✓ OIDC health check found in response${NC}"
        else
            echo ""
            echo -e "${RED}✗ OIDC health check NOT found in response${NC}"
            return 1
        fi
    fi

    echo ""
}

# Check readiness endpoint (should include OIDC)
echo "======================================"
echo "1. Readiness Probe Check"
echo "======================================"
echo ""
check_endpoint "/healthz/ready" "Readiness Probe (should include OIDC)"
READY_RESULT=$?

# Check liveness endpoint (should NOT include OIDC)
echo ""
echo "======================================"
echo "2. Liveness Probe Check"
echo "======================================"
echo ""
check_endpoint "/healthz/live" "Liveness Probe (should NOT include OIDC)"
LIVE_RESULT=$?

# Verify OIDC is NOT in liveness
if [ "$JQ_AVAILABLE" = true ] && [ $LIVE_RESULT -eq 0 ]; then
    response=$(curl -s "${BASE_URL}/healthz/live")
    oidc_in_live=$(echo "$response" | jq -r '.entries.oidc.status // "NOT_FOUND"')

    if [ "$oidc_in_live" == "NOT_FOUND" ]; then
        echo -e "${GREEN}✓ Correct: OIDC health check NOT in liveness probe${NC}"
    else
        echo -e "${YELLOW}⚠ Warning: OIDC health check found in liveness probe${NC}"
        echo "  This may cause unnecessary pod restarts"
    fi
fi

# Check startup endpoint (should NOT include OIDC)
echo ""
echo "======================================"
echo "3. Startup Probe Check"
echo "======================================"
echo ""
check_endpoint "/healthz/startup" "Startup Probe (should NOT include OIDC)"
STARTUP_RESULT=$?

# Summary
echo ""
echo "======================================"
echo "Verification Summary"
echo "======================================"
echo ""

if [ $READY_RESULT -eq 0 ]; then
    echo -e "${GREEN}✓ OIDC health check is properly registered${NC}"
    echo -e "${GREEN}✓ Available in readiness probe (/healthz/ready)${NC}"
else
    echo -e "${RED}✗ OIDC health check registration verification failed${NC}"
fi

echo ""
echo "Next Steps:"
echo "1. Check the OIDC health check status in your monitoring dashboard"
echo "2. Verify that OIDC endpoint is accessible from your application"
echo "3. Review logs for any OIDC health check warnings"
echo ""

# Return appropriate exit code
exit $READY_RESULT
