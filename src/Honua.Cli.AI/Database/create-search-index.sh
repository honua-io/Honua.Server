#!/bin/bash
# ============================================================================
# Azure AI Search Index Creation Script
# ============================================================================
# Creates the deployment-knowledge index in Azure AI Search with vector
# search, semantic ranking, and scoring profiles.
#
# Prerequisites:
#   1. Azure CLI installed and authenticated: az login
#   2. Terraform outputs available (or manual env vars set)
#   3. jq installed: sudo apt-get install jq
#
# Usage:
#   ./create-search-index.sh
#
# Or with manual configuration:
#   SEARCH_SERVICE_NAME=search-honua-abc123 \
#   SEARCH_ADMIN_KEY=your-admin-key \
#   ./create-search-index.sh
# ============================================================================

set -e  # Exit on error

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Azure AI Search Index Creation${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""

# ============================================================================
# Get configuration from Terraform outputs or environment variables
# ============================================================================

if [ -z "$SEARCH_SERVICE_NAME" ]; then
    echo -e "${YELLOW}Fetching search service name from Terraform...${NC}"
    SEARCH_SERVICE_NAME=$(cd ../../../infrastructure/terraform/azure && terraform output -raw search_name 2>/dev/null || echo "")
fi

if [ -z "$SEARCH_SERVICE_NAME" ]; then
    echo -e "${RED}Error: SEARCH_SERVICE_NAME not set${NC}"
    echo "Set it manually or run Terraform first:"
    echo "  export SEARCH_SERVICE_NAME=search-honua-abc123"
    exit 1
fi

if [ -z "$SEARCH_ADMIN_KEY" ]; then
    echo -e "${YELLOW}Fetching search admin key from Azure Key Vault...${NC}"

    # Get Key Vault name from Terraform
    KEY_VAULT_NAME=$(cd ../../../infrastructure/terraform/azure && terraform output -raw key_vault_name 2>/dev/null || echo "")

    if [ -n "$KEY_VAULT_NAME" ]; then
        SEARCH_ADMIN_KEY=$(az keyvault secret show \
            --vault-name "$KEY_VAULT_NAME" \
            --name AzureSearch-ApiKey \
            --query value -o tsv 2>/dev/null || echo "")
    fi
fi

if [ -z "$SEARCH_ADMIN_KEY" ]; then
    echo -e "${RED}Error: SEARCH_ADMIN_KEY not set${NC}"
    echo "Set it manually:"
    echo "  export SEARCH_ADMIN_KEY=your-admin-key"
    exit 1
fi

SEARCH_ENDPOINT="https://${SEARCH_SERVICE_NAME}.search.windows.net"
INDEX_NAME="deployment-knowledge"
API_VERSION="2023-11-01"

echo -e "${GREEN}Configuration:${NC}"
echo "  Search Service: $SEARCH_SERVICE_NAME"
echo "  Endpoint: $SEARCH_ENDPOINT"
echo "  Index Name: $INDEX_NAME"
echo ""

# ============================================================================
# Check if index already exists
# ============================================================================

echo -e "${YELLOW}Checking if index exists...${NC}"

HTTP_CODE=$(curl -s -o /dev/null -w "%{http_code}" \
    -X GET \
    "${SEARCH_ENDPOINT}/indexes/${INDEX_NAME}?api-version=${API_VERSION}" \
    -H "api-key: ${SEARCH_ADMIN_KEY}")

if [ "$HTTP_CODE" == "200" ]; then
    echo -e "${YELLOW}Index '${INDEX_NAME}' already exists${NC}"
    read -p "Do you want to delete and recreate it? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo -e "${YELLOW}Deleting existing index...${NC}"
        curl -X DELETE \
            "${SEARCH_ENDPOINT}/indexes/${INDEX_NAME}?api-version=${API_VERSION}" \
            -H "api-key: ${SEARCH_ADMIN_KEY}"
        echo -e "${GREEN}Index deleted${NC}"
        sleep 2
    else
        echo -e "${YELLOW}Skipping index creation${NC}"
        exit 0
    fi
fi

# ============================================================================
# Create index
# ============================================================================

echo -e "${YELLOW}Creating index '${INDEX_NAME}'...${NC}"

RESPONSE=$(curl -s -w "\n%{http_code}" \
    -X POST \
    "${SEARCH_ENDPOINT}/indexes?api-version=${API_VERSION}" \
    -H "Content-Type: application/json" \
    -H "api-key: ${SEARCH_ADMIN_KEY}" \
    -d @search-index-schema.json)

HTTP_CODE=$(echo "$RESPONSE" | tail -n1)
BODY=$(echo "$RESPONSE" | head -n-1)

if [ "$HTTP_CODE" == "201" ]; then
    echo -e "${GREEN}✓ Index created successfully!${NC}"
    echo ""
    echo -e "${GREEN}Index Details:${NC}"
    echo "$BODY" | jq '{name, fields: (.fields | length), vectorSearch, semantic}'
    echo ""
    echo -e "${GREEN}Next Steps:${NC}"
    echo "1. Run PostgreSQL schema migration: psql < schema.sql"
    echo "2. Deploy Azure Function: func azure functionapp publish <function-app-name>"
    echo "3. Test pattern indexing with sample data"
    echo ""
else
    echo -e "${RED}✗ Failed to create index${NC}"
    echo "HTTP Status: $HTTP_CODE"
    echo "Response:"
    echo "$BODY" | jq '.'
    exit 1
fi

# ============================================================================
# Verify index configuration
# ============================================================================

echo -e "${YELLOW}Verifying index configuration...${NC}"

curl -s \
    "${SEARCH_ENDPOINT}/indexes/${INDEX_NAME}?api-version=${API_VERSION}" \
    -H "api-key: ${SEARCH_ADMIN_KEY}" | jq '{
        name,
        fieldCount: (.fields | length),
        vectorSearchProfiles: (.vectorSearch.profiles | length),
        semanticConfigurations: (.semantic.configurations | length),
        scoringProfiles: (.scoringProfiles | length)
    }'

echo ""
echo -e "${GREEN}========================================${NC}"
echo -e "${GREEN}Index creation complete!${NC}"
echo -e "${GREEN}========================================${NC}"
