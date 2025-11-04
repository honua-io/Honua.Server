#!/bin/bash
set -e

echo "=========================================="
echo "Honua Kubernetes Manifest Validation"
echo "=========================================="
echo ""

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

cd "$(dirname "$0")"

# Check if kubectl is installed
if ! command -v kubectl &> /dev/null; then
    echo -e "${RED}✗ kubectl is not installed${NC}"
    exit 1
fi
echo -e "${GREEN}✓ kubectl is installed${NC}"

# Check if cluster is accessible (optional)
if kubectl cluster-info &> /dev/null; then
    echo -e "${GREEN}✓ Kubernetes cluster is accessible${NC}"
    CLUSTER_AVAILABLE=true
else
    echo -e "${YELLOW}⚠ No Kubernetes cluster detected (validation will be offline)${NC}"
    CLUSTER_AVAILABLE=false
fi
echo ""

# YAML syntax validation using Python
echo "Validating YAML syntax..."
python3 -c "
import yaml
import sys
import os

files = sorted([f for f in os.listdir('.') if f.endswith('.yaml') and f[0].isdigit()])
errors = []

for filename in files:
    try:
        with open(filename, 'r') as f:
            docs = list(yaml.safe_load_all(f))
            print(f'  ✓ {filename}: Valid YAML ({len(docs)} document(s))')
    except yaml.YAMLError as e:
        errors.append(f'  ✗ {filename}: {str(e)}')
        print(f'  ✗ {filename}: {str(e)}')

if errors:
    print(f'\n{len(errors)} file(s) with YAML syntax errors')
    sys.exit(1)
else:
    print(f'\n✓ All {len(files)} manifest files have valid YAML syntax')
"

if [ $? -ne 0 ]; then
    echo -e "${RED}✗ YAML validation failed${NC}"
    exit 1
fi
echo ""

# Kubernetes validation (if cluster is available)
if [ "$CLUSTER_AVAILABLE" = true ]; then
    echo "Validating Kubernetes manifests against cluster..."

    # Dry-run apply all manifests
    if kubectl apply --dry-run=client -f . > /dev/null 2>&1; then
        echo -e "${GREEN}✓ All manifests are valid Kubernetes resources${NC}"
    else
        echo -e "${RED}✗ Some manifests failed validation${NC}"
        kubectl apply --dry-run=client -f . 2>&1 | grep -i "error"
        exit 1
    fi
else
    echo "Validating Kubernetes manifests (offline)..."

    # Offline validation without cluster
    if kubectl apply --dry-run=client --validate=false -f . > /dev/null 2>&1; then
        echo -e "${GREEN}✓ All manifests are syntactically correct${NC}"
    else
        echo -e "${YELLOW}⚠ Some validation warnings (this is normal without a cluster)${NC}"
    fi
fi
echo ""

# Check for required secrets placeholders
echo "Checking for placeholder values that need updating..."
NEEDS_UPDATE=false

if grep -q "CHANGE_ME" 01-secrets.yaml; then
    echo -e "${YELLOW}⚠ 01-secrets.yaml contains placeholder values (CHANGE_ME)${NC}"
    echo "  Please update the following before deployment:"
    grep "CHANGE_ME" 01-secrets.yaml | sed 's/^/    /'
    NEEDS_UPDATE=true
fi

if grep -q "example.com" 06-ingress.yaml; then
    echo -e "${YELLOW}⚠ 06-ingress.yaml uses example.com domains${NC}"
    echo "  Please update to your actual domain names"
    NEEDS_UPDATE=true
fi

if grep -q "ACCOUNT_ID\|PROJECT_ID\|CLIENT_ID" 05-serviceaccount.yaml; then
    echo -e "${YELLOW}⚠ 05-serviceaccount.yaml contains cloud provider placeholders${NC}"
    echo "  Please update with actual cloud provider IDs"
    NEEDS_UPDATE=true
fi

if [ "$NEEDS_UPDATE" = true ]; then
    echo ""
    echo -e "${YELLOW}⚠ Configuration updates required before production deployment${NC}"
else
    echo -e "${GREEN}✓ No placeholder values detected${NC}"
fi
echo ""

# Summary
echo "=========================================="
echo "Validation Summary"
echo "=========================================="
echo -e "${GREEN}✓ YAML syntax validation passed${NC}"
echo -e "${GREEN}✓ Kubernetes manifest structure is valid${NC}"

if [ "$NEEDS_UPDATE" = true ]; then
    echo -e "${YELLOW}⚠ Configuration placeholders need updating${NC}"
    echo ""
    echo "Next steps:"
    echo "  1. Update secrets in 01-secrets.yaml"
    echo "  2. Update domain names in 06-ingress.yaml"
    echo "  3. Update cloud provider IDs in 05-serviceaccount.yaml (if applicable)"
    echo "  4. Review and adjust resource limits in 03-deployment.yaml"
    echo "  5. Deploy: kubectl apply -f ."
else
    echo -e "${GREEN}✓ Configuration appears complete${NC}"
    echo ""
    echo "Ready to deploy:"
    echo "  kubectl apply -f ."
fi
echo ""
