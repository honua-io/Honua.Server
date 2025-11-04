#!/bin/bash

# Honua Security Test Script
# Automated security testing for production deployments
# Usage: ./security-test.sh https://your-domain.com

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Counters
PASS=0
FAIL=0
WARN=0

# Target URL
TARGET_URL="${1:-http://localhost:5000}"

echo "========================================"
echo "Honua Security Test Suite"
echo "========================================"
echo "Target: $TARGET_URL"
echo "Date: $(date)"
echo "========================================"
echo ""

# Helper functions
pass() {
    echo -e "${GREEN}[PASS]${NC} $1"
    ((PASS++))
}

fail() {
    echo -e "${RED}[FAIL]${NC} $1"
    ((FAIL++))
}

warn() {
    echo -e "${YELLOW}[WARN]${NC} $1"
    ((WARN++))
}

# Test 1: HTTPS Redirect
echo "Test 1: HTTPS Redirect"
if [[ $TARGET_URL == http://* ]]; then
    HTTP_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" -L "$TARGET_URL")
    if [[ $HTTP_RESPONSE == 301 ]] || [[ $HTTP_RESPONSE == 302 ]]; then
        pass "HTTP redirects to HTTPS"
    else
        fail "HTTP does not redirect (got $HTTP_RESPONSE)"
    fi
else
    warn "Target is HTTPS, cannot test redirect"
fi

# Test 2: Security Headers
echo ""
echo "Test 2: Security Headers"

HEADERS=$(curl -s -I "$TARGET_URL")

# Test HSTS
if echo "$HEADERS" | grep -qi "Strict-Transport-Security"; then
    pass "HSTS header present"
else
    fail "HSTS header missing"
fi

# Test X-Frame-Options
if echo "$HEADERS" | grep -qi "X-Frame-Options: DENY"; then
    pass "X-Frame-Options: DENY"
elif echo "$HEADERS" | grep -qi "X-Frame-Options"; then
    warn "X-Frame-Options present but not DENY"
else
    fail "X-Frame-Options missing"
fi

# Test X-Content-Type-Options
if echo "$HEADERS" | grep -qi "X-Content-Type-Options: nosniff"; then
    pass "X-Content-Type-Options: nosniff"
else
    fail "X-Content-Type-Options missing"
fi

# Test Content-Security-Policy
if echo "$HEADERS" | grep -qi "Content-Security-Policy"; then
    pass "Content-Security-Policy present"
else
    fail "Content-Security-Policy missing"
fi

# Test Server header removal
if echo "$HEADERS" | grep -qi "Server:"; then
    warn "Server header present (information disclosure)"
else
    pass "Server header removed"
fi

# Test X-Powered-By removal
if echo "$HEADERS" | grep -qi "X-Powered-By"; then
    warn "X-Powered-By header present (information disclosure)"
else
    pass "X-Powered-By header removed"
fi

# Test 3: Authentication
echo ""
echo "Test 3: Authentication"

# Test unauthenticated admin access
ADMIN_RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" "$TARGET_URL/admin/metadata")
if [[ $ADMIN_RESPONSE == 401 ]] || [[ $ADMIN_RESPONSE == 403 ]]; then
    pass "Admin endpoints require authentication"
else
    fail "Admin endpoints accessible without auth (got $ADMIN_RESPONSE)"
fi

# Test 4: Rate Limiting
echo ""
echo "Test 4: Rate Limiting"

# Make rapid requests
RATE_LIMIT_TRIGGERED=false
for i in {1..120}; do
    RESPONSE=$(curl -s -o /dev/null -w "%{http_code}" "$TARGET_URL/healthz/live")
    if [[ $RESPONSE == 429 ]]; then
        RATE_LIMIT_TRIGGERED=true
        break
    fi
done

if [ "$RATE_LIMIT_TRIGGERED" = true ]; then
    pass "Rate limiting active (triggered after $i requests)"
else
    warn "Rate limiting not triggered within 120 requests"
fi

# Test 5: security.txt
echo ""
echo "Test 5: Responsible Disclosure"

SECURITY_TXT=$(curl -s "$TARGET_URL/.well-known/security.txt")
if echo "$SECURITY_TXT" | grep -qi "Contact:"; then
    pass "security.txt is accessible"
else
    fail "security.txt missing or malformed"
fi

# Test 6: Information Disclosure
echo ""
echo "Test 6: Information Disclosure"

# Test 404 error
ERROR_RESPONSE=$(curl -s "$TARGET_URL/nonexistent-path-12345")
if echo "$ERROR_RESPONSE" | grep -qi "stack trace\|exception\|kestrel\|asp.net"; then
    fail "Stack traces or framework info exposed in errors"
else
    pass "No detailed error information exposed"
fi

# Test 7: TLS Configuration (if HTTPS)
echo ""
echo "Test 7: TLS Configuration"

if [[ $TARGET_URL == https://* ]]; then
    DOMAIN=$(echo "$TARGET_URL" | sed -e 's|https://||' -e 's|/.*||')
    
    # Check TLS version
    TLS_VERSION=$(echo | openssl s_client -connect "$DOMAIN:443" 2>/dev/null | grep "Protocol" | awk '{print $3}')
    if [[ $TLS_VERSION == TLSv1.2 ]] || [[ $TLS_VERSION == TLSv1.3 ]]; then
        pass "TLS version: $TLS_VERSION"
    else
        fail "Weak TLS version: $TLS_VERSION"
    fi
else
    warn "Cannot test TLS (target is HTTP)"
fi

# Test 8: SQL Injection (basic)
echo ""
echo "Test 8: Basic Injection Tests"

# Test SQL injection in query parameters
SQL_TEST=$(curl -s -o /dev/null -w "%{http_code}" "$TARGET_URL/api/collections?id=1'OR'1'='1")
if [[ $SQL_TEST == 400 ]] || [[ $SQL_TEST == 404 ]]; then
    pass "SQL injection attempt rejected"
elif [[ $SQL_TEST == 500 ]]; then
    fail "SQL injection may be possible (500 error)"
else
    warn "Unexpected response to SQL injection test: $SQL_TEST"
fi

# Test 9: Path Traversal
echo ""
echo "Test 9: Path Traversal"

PATH_TRAVERSAL=$(curl -s -o /dev/null -w "%{http_code}" "$TARGET_URL/api/../../etc/passwd")
if [[ $PATH_TRAVERSAL == 400 ]] || [[ $PATH_TRAVERSAL == 404 ]]; then
    pass "Path traversal attempt rejected"
elif [[ $PATH_TRAVERSAL == 200 ]]; then
    fail "Path traversal may be possible"
else
    warn "Unexpected response to path traversal test: $PATH_TRAVERSAL"
fi

# Test 10: CORS
echo ""
echo "Test 10: CORS Configuration"

CORS_RESPONSE=$(curl -s -H "Origin: https://evil.com" \
    -H "Access-Control-Request-Method: GET" \
    -X OPTIONS "$TARGET_URL/api/collections" -i)

if echo "$CORS_RESPONSE" | grep -qi "Access-Control-Allow-Origin: \*"; then
    fail "CORS allows all origins (*)"
elif echo "$CORS_RESPONSE" | grep -qi "Access-Control-Allow-Origin: https://evil.com"; then
    fail "CORS allows unauthorized origin"
else
    pass "CORS properly restricted"
fi

# Results Summary
echo ""
echo "========================================"
echo "Test Results Summary"
echo "========================================"
echo -e "${GREEN}PASS:${NC} $PASS"
echo -e "${YELLOW}WARN:${NC} $WARN"
echo -e "${RED}FAIL:${NC} $FAIL"
echo "========================================"

# Exit code based on failures
if [ $FAIL -gt 0 ]; then
    echo -e "${RED}Security tests FAILED${NC}"
    exit 1
elif [ $WARN -gt 5 ]; then
    echo -e "${YELLOW}Security tests PASSED with warnings${NC}"
    exit 0
else
    echo -e "${GREEN}Security tests PASSED${NC}"
    exit 0
fi
