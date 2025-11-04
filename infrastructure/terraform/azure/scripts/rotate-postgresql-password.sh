#!/bin/bash
set -euo pipefail

# ============================================================================
# PostgreSQL Password Rotation Script for Azure Key Vault
# ============================================================================
# Rotates PostgreSQL admin password and updates:
#   1. PostgreSQL server password
#   2. Key Vault PostgreSQL-AdminPassword secret
#   3. Key Vault PostgreSQL-ConnectionString secret
#   4. (Optional) Restarts dependent services
#
# Usage:
#   ./rotate-postgresql-password.sh [--environment dev|staging|prod] [--skip-restart]
#
# Prerequisites:
#   - Azure CLI authenticated (az login)
#   - Terraform state available
#   - PostgreSQL firewall allows connection from current IP
#   - Key Vault Secrets Officer role assigned
# ============================================================================

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

# Default values
ENVIRONMENT="dev"
SKIP_RESTART=false
DRY_RUN=false

# Parse arguments
while [[ $# -gt 0 ]]; do
  case $1 in
    --environment)
      ENVIRONMENT="$2"
      shift 2
      ;;
    --skip-restart)
      SKIP_RESTART=true
      shift
      ;;
    --dry-run)
      DRY_RUN=true
      shift
      ;;
    --help)
      echo "Usage: $0 [--environment dev|staging|prod] [--skip-restart] [--dry-run]"
      echo ""
      echo "Options:"
      echo "  --environment    Target environment (default: dev)"
      echo "  --skip-restart   Skip restarting dependent services"
      echo "  --dry-run        Show what would be done without making changes"
      echo "  --help           Show this help message"
      exit 0
      ;;
    *)
      echo "Unknown option: $1"
      exit 1
      ;;
  esac
done

echo -e "${GREEN}PostgreSQL Password Rotation Script${NC}"
echo "Environment: $ENVIRONMENT"
echo "Dry Run: $DRY_RUN"
echo ""

# ============================================================================
# Step 1: Get current credentials from Terraform outputs
# ============================================================================

echo -e "${YELLOW}[1/7]${NC} Retrieving configuration from Terraform..."

cd "$(dirname "$0")/.."

if ! command -v terraform &> /dev/null; then
    echo -e "${RED}Error: terraform not found${NC}"
    exit 1
fi

if ! command -v az &> /dev/null; then
    echo -e "${RED}Error: Azure CLI (az) not found${NC}"
    exit 1
fi

if ! command -v psql &> /dev/null; then
    echo -e "${RED}Error: psql not found${NC}"
    echo "Install PostgreSQL client: sudo apt-get install postgresql-client"
    exit 1
fi

# Get Terraform outputs
KV_NAME=$(terraform output -raw key_vault_name)
RESOURCE_GROUP=$(terraform output -raw resource_group_name)
FUNCTION_APP_NAME=$(terraform output -raw function_app_name)

echo "  Key Vault: $KV_NAME"
echo "  Resource Group: $RESOURCE_GROUP"
echo ""

# ============================================================================
# Step 2: Retrieve current secrets from Key Vault
# ============================================================================

echo -e "${YELLOW}[2/7]${NC} Retrieving current credentials from Key Vault..."

CURRENT_USERNAME=$(az keyvault secret show \
  --vault-name "$KV_NAME" \
  --name PostgreSQL-AdminUsername \
  --query value -o tsv)

CURRENT_PASSWORD=$(az keyvault secret show \
  --vault-name "$KV_NAME" \
  --name PostgreSQL-AdminPassword \
  --query value -o tsv)

POSTGRES_HOST=$(az keyvault secret show \
  --vault-name "$KV_NAME" \
  --name PostgreSQL-Host \
  --query value -o tsv)

echo "  Username: $CURRENT_USERNAME"
echo "  Host: $POSTGRES_HOST"
echo "  Current password: <redacted>"
echo ""

# ============================================================================
# Step 3: Generate new secure password
# ============================================================================

echo -e "${YELLOW}[3/7]${NC} Generating new secure password..."

# Generate 32-character password with letters, numbers, and safe symbols
# Avoid characters that cause issues in connection strings: ;&|
NEW_PASSWORD=$(openssl rand -base64 48 | tr -d "=+/;&|" | head -c 32)

# Ensure password has required complexity (uppercase, lowercase, number)
if ! echo "$NEW_PASSWORD" | grep -q '[A-Z]' || \
   ! echo "$NEW_PASSWORD" | grep -q '[a-z]' || \
   ! echo "$NEW_PASSWORD" | grep -q '[0-9]'; then
    # Ensure at least one of each
    NEW_PASSWORD="Aa1${NEW_PASSWORD:3}"
fi

echo "  New password: <redacted> (32 characters)"
echo ""

# ============================================================================
# Step 4: Test current connection
# ============================================================================

echo -e "${YELLOW}[4/7]${NC} Testing current database connection..."

if $DRY_RUN; then
    echo "  [DRY RUN] Would test connection to $POSTGRES_HOST"
else
    if PGPASSWORD="$CURRENT_PASSWORD" psql \
        "host=$POSTGRES_HOST port=5432 dbname=postgres user=$CURRENT_USERNAME sslmode=require" \
        -c "SELECT version();" &> /dev/null; then
        echo -e "  ${GREEN}✓${NC} Connection successful"
    else
        echo -e "  ${RED}✗${NC} Connection failed with current credentials"
        echo ""
        echo "Possible causes:"
        echo "  1. Current IP not allowed in PostgreSQL firewall"
        echo "  2. Current password in Key Vault is incorrect"
        echo "  3. PostgreSQL server is down"
        echo ""
        echo "Add your IP to firewall:"
        echo "  MY_IP=\$(curl -s https://api.ipify.org)"
        echo "  az postgres flexible-server firewall-rule create \\"
        echo "    --resource-group $RESOURCE_GROUP \\"
        echo "    --name postgres-honua-* \\"
        echo "    --rule-name AllowMyIP \\"
        echo "    --start-ip-address \$MY_IP \\"
        echo "    --end-ip-address \$MY_IP"
        exit 1
    fi
fi

echo ""

# ============================================================================
# Step 5: Update PostgreSQL password
# ============================================================================

echo -e "${YELLOW}[5/7]${NC} Updating PostgreSQL server password..."

if $DRY_RUN; then
    echo "  [DRY RUN] Would execute: ALTER USER $CURRENT_USERNAME WITH PASSWORD '***';"
else
    if PGPASSWORD="$CURRENT_PASSWORD" psql \
        "host=$POSTGRES_HOST port=5432 dbname=postgres user=$CURRENT_USERNAME sslmode=require" \
        -c "ALTER USER $CURRENT_USERNAME WITH PASSWORD '$NEW_PASSWORD';" &> /dev/null; then
        echo -e "  ${GREEN}✓${NC} PostgreSQL password updated"
    else
        echo -e "  ${RED}✗${NC} Failed to update PostgreSQL password"
        exit 1
    fi
fi

echo ""

# ============================================================================
# Step 6: Update Key Vault secrets
# ============================================================================

echo -e "${YELLOW}[6/7]${NC} Updating Key Vault secrets..."

if $DRY_RUN; then
    echo "  [DRY RUN] Would update Key Vault secrets:"
    echo "    - PostgreSQL-AdminPassword"
    echo "    - PostgreSQL-ConnectionString"
else
    # Update password secret
    az keyvault secret set \
      --vault-name "$KV_NAME" \
      --name PostgreSQL-AdminPassword \
      --value "$NEW_PASSWORD" \
      --content-type "text/plain" \
      --tags "Purpose=PostgreSQL Admin Password" "RotationDays=90" "RotatedAt=$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
      --output none

    echo -e "  ${GREEN}✓${NC} Updated PostgreSQL-AdminPassword"

    # Update connection string secret
    NEW_CONN_STR="Host=$POSTGRES_HOST;Database=honua;Username=$CURRENT_USERNAME;Password=$NEW_PASSWORD;SSL Mode=Require"
    az keyvault secret set \
      --vault-name "$KV_NAME" \
      --name PostgreSQL-ConnectionString \
      --value "$NEW_CONN_STR" \
      --content-type "application/x-connection-string" \
      --tags "Purpose=PostgreSQL Database Access" "RotationDays=90" "RotatedAt=$(date -u +%Y-%m-%dT%H:%M:%SZ)" \
      --output none

    echo -e "  ${GREEN}✓${NC} Updated PostgreSQL-ConnectionString"
fi

echo ""

# ============================================================================
# Step 7: Verify new password and restart services
# ============================================================================

echo -e "${YELLOW}[7/7]${NC} Verifying new password..."

if $DRY_RUN; then
    echo "  [DRY RUN] Would verify connection with new password"
else
    # Wait 5 seconds for Key Vault cache to update
    sleep 5

    if PGPASSWORD="$NEW_PASSWORD" psql \
        "host=$POSTGRES_HOST port=5432 dbname=postgres user=$CURRENT_USERNAME sslmode=require" \
        -c "SELECT 'Connection successful' AS status;" &> /dev/null; then
        echo -e "  ${GREEN}✓${NC} New password verified successfully"
    else
        echo -e "  ${RED}✗${NC} Verification failed with new password"
        echo ""
        echo -e "${RED}CRITICAL: Password was changed in PostgreSQL but verification failed${NC}"
        echo "Manual intervention required. Old password:"
        echo "  $CURRENT_PASSWORD"
        echo ""
        exit 1
    fi
fi

echo ""

# Restart dependent services
if ! $SKIP_RESTART && ! $DRY_RUN; then
    echo "Restarting Function App to pick up new secrets..."
    az functionapp restart \
      --name "$FUNCTION_APP_NAME" \
      --resource-group "$RESOURCE_GROUP" \
      --output none
    echo -e "  ${GREEN}✓${NC} Function App restarted"
    echo ""
elif $SKIP_RESTART; then
    echo -e "${YELLOW}Note: Skipped service restart. Remember to restart manually:${NC}"
    echo "  az functionapp restart --name $FUNCTION_APP_NAME --resource-group $RESOURCE_GROUP"
    echo ""
fi

# ============================================================================
# Summary
# ============================================================================

echo -e "${GREEN}════════════════════════════════════════════════════════${NC}"
echo -e "${GREEN}Password rotation completed successfully!${NC}"
echo -e "${GREEN}════════════════════════════════════════════════════════${NC}"
echo ""
echo "Summary:"
echo "  ✓ New password generated (32 characters)"
echo "  ✓ PostgreSQL server password updated"
echo "  ✓ Key Vault secrets updated (PostgreSQL-AdminPassword, PostgreSQL-ConnectionString)"
echo "  ✓ New password verified"
if ! $SKIP_RESTART && ! $DRY_RUN; then
    echo "  ✓ Function App restarted"
fi
echo ""
echo "Next steps:"
echo "  1. Monitor Function App logs for successful database connections"
echo "  2. Update rotation date in documentation"
echo "  3. Schedule next rotation in 90 days: $(date -d '+90 days' '+%Y-%m-%d')"
echo ""
echo "Verification commands:"
echo "  # Check Function App logs"
echo "  az functionapp log tail --name $FUNCTION_APP_NAME --resource-group $RESOURCE_GROUP"
echo ""
echo "  # Verify Key Vault secret version"
echo "  az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminPassword --query 'attributes.updated'"
echo ""

# Log rotation event
ROTATION_LOG="/tmp/postgres-rotation-$(date +%Y%m%d-%H%M%S).log"
if ! $DRY_RUN; then
    cat > "$ROTATION_LOG" <<EOF
PostgreSQL Password Rotation Log
================================
Timestamp: $(date -u +%Y-%m-%dT%H:%M:%SZ)
Environment: $ENVIRONMENT
Operator: $(whoami)@$(hostname)
Key Vault: $KV_NAME
PostgreSQL Host: $POSTGRES_HOST
Username: $CURRENT_USERNAME
Status: SUCCESS
Next Rotation: $(date -d '+90 days' '+%Y-%m-%d')
EOF
    echo "Rotation logged to: $ROTATION_LOG"
fi
