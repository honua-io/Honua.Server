# Azure Key Vault Secret Management Guide

Complete guide for managing PostgreSQL connection strings and other secrets using Azure Key Vault with Terraform.

## Table of Contents

- [Overview](#overview)
- [Architecture](#architecture)
- [Secret Storage Strategy](#secret-storage-strategy)
- [Managed Identity Configuration](#managed-identity-configuration)
- [Secret Rotation](#secret-rotation)
- [Access Control](#access-control)
- [Monitoring and Alerts](#monitoring-and-alerts)
- [Troubleshooting](#troubleshooting)

## Overview

All sensitive credentials are stored in Azure Key Vault and referenced by Azure App Service and Function Apps using managed identities. **No secrets are stored in plain text in Terraform state or app configurations.**

### Key Benefits

- **Zero hardcoded secrets** - All credentials stored in Key Vault
- **Automatic rotation** - Secret updates propagate without redeployment
- **Audit trail** - Every secret access is logged
- **RBAC enforcement** - Fine-grained access control using Azure AD
- **Compliance ready** - Meets SOC 2, ISO 27001 requirements

## Architecture

```
┌─────────────────────┐
│  Terraform State    │
│  (No secrets)       │
└──────────┬──────────┘
           │
           ▼
┌─────────────────────┐      ┌─────────────────────┐
│  Azure Key Vault    │◄─────│  PostgreSQL Server  │
│  - Connection Str   │      │  (Generated pwd)    │
│  - Username         │      └─────────────────────┘
│  - Password         │
│  - Host FQDN        │
└──────────┬──────────┘
           │
           │ @Microsoft.KeyVault(SecretUri=...)
           ▼
┌─────────────────────┐
│  Function App       │
│  (Managed Identity) │
│  - No secrets       │
│  - KV references    │
└─────────────────────┘
```

## Secret Storage Strategy

### Stored Secrets

The following PostgreSQL-related secrets are stored in Key Vault:

1. **`PostgreSQL-ConnectionString`** (Primary)
   - Complete connection string for immediate use
   - Format: `Host=...;Database=honua;Username=...;Password=...;SSL Mode=Require`
   - Used by: Function App app settings
   - Rotation: Every 90 days

2. **`PostgreSQL-AdminUsername`** (Component)
   - Username only
   - Used for: Manual rotation, troubleshooting
   - Rotation: Infrequent (200+ days)

3. **`PostgreSQL-AdminPassword`** (Component)
   - Password only
   - Used for: Rotation scripts, emergency access
   - Rotation: Every 90 days

4. **`PostgreSQL-Host`** (Reference)
   - FQDN of PostgreSQL server
   - Used for: Application configuration, connection validation
   - Rotation: Never (only on infrastructure change)

### Secret Naming Convention

```
Format: {Service}-{Purpose}
Examples:
  - PostgreSQL-ConnectionString
  - PostgreSQL-AdminPassword
  - AzureOpenAI-ApiKey
  - AzureSearch-ApiKey
```

### Secret Tagging

All secrets are tagged for lifecycle management:

```hcl
tags = {
  Purpose      = "PostgreSQL Database Access"
  RotationDays = "90"
  Critical     = "true"
  Environment  = "prod"
  ManagedBy    = "Terraform"
}
```

## Managed Identity Configuration

### System-Assigned Identity (Recommended)

Terraform automatically configures system-assigned managed identities:

```hcl
resource "azurerm_linux_function_app" "main" {
  # ... other configuration ...

  identity {
    type = "SystemAssigned"
  }
}
```

### RBAC Role Assignment

The Function App is granted `Key Vault Secrets User` role:

```hcl
resource "azurerm_role_assignment" "function_kv_secrets_user" {
  scope                = azurerm_key_vault.main.id
  role_definition_name = "Key Vault Secrets User"
  principal_id         = azurerm_linux_function_app.main.identity[0].principal_id
}
```

### Key Vault Reference Syntax

In App Service/Function App settings:

```json
{
  "ConnectionStrings__PostgreSQL": "@Microsoft.KeyVault(SecretUri=https://kv-honua-abc123.vault.azure.net/secrets/PostgreSQL-ConnectionString)"
}
```

**Note**: No version specified = always use latest version (automatic rotation)

## Secret Rotation

### Rotation Schedule

| Secret | Rotation Period | Method | Priority |
|--------|----------------|--------|----------|
| PostgreSQL Password | 90 days | Automated | High |
| PostgreSQL Username | 365 days | Manual | Low |
| OpenAI API Key | On demand | Manual | Medium |
| Search API Key | On demand | Manual | Medium |

### Automated Rotation (Azure Automation)

#### Step 1: Create Rotation Function

```bash
cd infrastructure/terraform/azure/rotation
func init --python
func new --name RotatePostgreSQLPassword --template "Timer trigger"
```

#### Step 2: Rotation Logic (Python)

```python
import os
import logging
import psycopg2
from azure.identity import DefaultAzureCredential
from azure.keyvault.secrets import SecretClient
import secrets
import string

def generate_password(length=32):
    """Generate a secure random password"""
    alphabet = string.ascii_letters + string.digits + "!@#$%^&*"
    return ''.join(secrets.choice(alphabet) for _ in range(length))

def rotate_postgresql_password(vault_url: str, postgres_host: str):
    """Rotate PostgreSQL admin password"""

    credential = DefaultAzureCredential()
    secret_client = SecretClient(vault_url=vault_url, credential=credential)

    # Get current credentials
    current_username = secret_client.get_secret("PostgreSQL-AdminUsername").value
    current_password = secret_client.get_secret("PostgreSQL-AdminPassword").value

    # Generate new password
    new_password = generate_password()

    # Update PostgreSQL
    conn = psycopg2.connect(
        host=postgres_host,
        database="postgres",
        user=current_username,
        password=current_password,
        sslmode="require"
    )
    cursor = conn.cursor()
    cursor.execute(f"ALTER USER {current_username} WITH PASSWORD %s;", (new_password,))
    conn.commit()
    cursor.close()
    conn.close()

    # Update Key Vault
    secret_client.set_secret("PostgreSQL-AdminPassword", new_password)

    # Update connection string
    new_conn_str = f"Host={postgres_host};Database=honua;Username={current_username};Password={new_password};SSL Mode=Require"
    secret_client.set_secret("PostgreSQL-ConnectionString", new_conn_str)

    logging.info("PostgreSQL password rotated successfully")
```

#### Step 3: Deploy Rotation Function

```bash
# Deploy to Azure Functions
func azure functionapp publish func-honua-rotation-{environment}

# Set schedule (every 90 days = 0 0 0 */90 * *)
# Configured in function.json:
{
  "schedule": "0 0 0 */90 * *",
  "runOnStartup": false
}
```

### Manual Rotation

#### Rotate PostgreSQL Password

```bash
# Step 1: Get current credentials from Key Vault
KV_NAME=$(terraform output -raw key_vault_name)
CURRENT_USERNAME=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminUsername --query value -o tsv)
CURRENT_PASSWORD=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminPassword --query value -o tsv)
POSTGRES_HOST=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-Host --query value -o tsv)

# Step 2: Generate new password (32 chars, alphanumeric + symbols)
NEW_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)

# Step 3: Update PostgreSQL
PGPASSWORD=$CURRENT_PASSWORD psql \
  "host=$POSTGRES_HOST port=5432 dbname=postgres user=$CURRENT_USERNAME sslmode=require" \
  -c "ALTER USER $CURRENT_USERNAME WITH PASSWORD '$NEW_PASSWORD';"

# Step 4: Update Key Vault password
az keyvault secret set \
  --vault-name $KV_NAME \
  --name PostgreSQL-AdminPassword \
  --value "$NEW_PASSWORD"

# Step 5: Update Key Vault connection string
NEW_CONN_STR="Host=$POSTGRES_HOST;Database=honua;Username=$CURRENT_USERNAME;Password=$NEW_PASSWORD;SSL Mode=Require"
az keyvault secret set \
  --vault-name $KV_NAME \
  --name PostgreSQL-ConnectionString \
  --value "$NEW_CONN_STR"

# Step 6: Restart Function App to pick up new secret (if not using latest)
FUNCTION_APP_NAME=$(terraform output -raw function_app_name)
az functionapp restart --name $FUNCTION_APP_NAME --resource-group $(terraform output -raw resource_group_name)

echo "Password rotation completed successfully"
```

#### Verify Rotation

```bash
# Test new connection string
NEW_PASSWORD=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminPassword --query value -o tsv)
PGPASSWORD=$NEW_PASSWORD psql \
  "host=$POSTGRES_HOST port=5432 dbname=honua user=$CURRENT_USERNAME sslmode=require" \
  -c "SELECT version();"

# Check Function App logs for successful connection
az functionapp log tail --name $FUNCTION_APP_NAME --resource-group $(terraform output -raw resource_group_name)
```

## Access Control

### RBAC Roles

| Role | Scope | Assigned To | Purpose |
|------|-------|-------------|---------|
| Key Vault Administrator | Key Vault | Terraform Service Principal | Full management access |
| Key Vault Secrets User | Key Vault | Function App Managed Identity | Read secrets only |
| Key Vault Secrets User | Key Vault | DevOps Engineers | Emergency access |
| Key Vault Secrets Officer | Key Vault | Rotation Automation | Update secrets |

### Grant Access to Additional Services

```bash
# Example: Grant AKS cluster access to Key Vault
AKS_IDENTITY_ID=$(az aks show -g rg-honua-prod-eastus -n aks-honua-prod --query identityProfile.kubeletidentity.objectId -o tsv)

az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $AKS_IDENTITY_ID \
  --scope $(terraform output -raw key_vault_id)
```

### Network Access Control (Production)

For production environments, restrict Key Vault to specific VNets:

```hcl
resource "azurerm_key_vault" "main" {
  # ... other configuration ...

  network_acls {
    default_action = "Deny"
    bypass         = "AzureServices"

    # Allow specific VNets
    virtual_network_subnet_ids = [
      azurerm_subnet.functions.id,
      azurerm_subnet.aks.id
    ]

    # Allow DevOps jump box
    ip_rules = [
      "203.0.113.0/24" # Replace with your IP range
    ]
  }
}
```

## Monitoring and Alerts

### Diagnostic Settings

All Key Vault operations are logged to Log Analytics:

```bash
# Query Key Vault access logs
az monitor log-analytics query \
  --workspace $(terraform output -raw log_analytics_workspace_id) \
  --analytics-query '
    AzureDiagnostics
    | where ResourceProvider == "MICROSOFT.KEYVAULT"
    | where OperationName == "SecretGet"
    | summarize Count=count() by CallerIPAddress, identity_claim_appid_g
    | order by Count desc
  ' \
  --output table
```

### Alerts Configured

1. **Key Vault Availability** (Production only)
   - Trigger: Availability < 99.9%
   - Severity: Critical
   - Action: Email admin, create incident

2. **Suspicious Access Pattern**
   - Trigger: >100 secret reads from single IP in 1 hour
   - Severity: Warning
   - Action: Email security team

### Custom Alert: Secret Near Expiration

```bash
# Create alert rule (if using secret expiration dates)
az monitor metrics alert create \
  --name "keyvault-secret-expiry-warning" \
  --resource-group $(terraform output -raw resource_group_name) \
  --scopes $(terraform output -raw key_vault_id) \
  --condition "avg ServiceApiLatency > 1000" \
  --description "Alert when secrets are nearing expiration" \
  --evaluation-frequency 1h \
  --window-size 6h \
  --severity 2
```

## Troubleshooting

### Issue: "Access Denied" when Function App tries to read secret

**Cause**: Managed identity not granted Key Vault Secrets User role

**Solution**:
```bash
FUNCTION_APP_PRINCIPAL_ID=$(az functionapp show \
  --name $(terraform output -raw function_app_name) \
  --resource-group $(terraform output -raw resource_group_name) \
  --query identity.principalId -o tsv)

az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $FUNCTION_APP_PRINCIPAL_ID \
  --scope $(terraform output -raw key_vault_id)
```

### Issue: Function App uses old password after rotation

**Cause**: App Service caches Key Vault references

**Solution**:
```bash
# Restart Function App to clear cache
az functionapp restart \
  --name $(terraform output -raw function_app_name) \
  --resource-group $(terraform output -raw resource_group_name)

# Or sync secrets manually
az functionapp config appsettings list \
  --name $(terraform output -raw function_app_name) \
  --resource-group $(terraform output -raw resource_group_name)
```

**Prevention**: Always use latest secret version (no version in URI)

### Issue: PostgreSQL connection fails after rotation

**Diagnosis**:
```bash
# Test connection with new password
KV_NAME=$(terraform output -raw key_vault_name)
NEW_PASSWORD=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminPassword --query value -o tsv)
POSTGRES_HOST=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-Host --query value -o tsv)
USERNAME=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminUsername --query value -o tsv)

PGPASSWORD=$NEW_PASSWORD psql "host=$POSTGRES_HOST port=5432 dbname=postgres user=$USERNAME sslmode=require" -c "\conninfo"
```

**Common causes**:
1. PostgreSQL password not actually updated (step skipped)
2. Connection string not updated in Key Vault
3. Special characters in password not properly escaped

### Issue: Terraform shows secrets in plan output

**Cause**: Outputs not marked as sensitive

**Solution**: All secret outputs are marked `sensitive = true`:
```hcl
output "app_insights_connection_string" {
  value       = azurerm_application_insights.main.connection_string
  description = "Application Insights connection string"
  sensitive   = true
}
```

### Emergency Access (Break Glass)

If all automated access fails:

```bash
# 1. Login as Key Vault Administrator
az login

# 2. Retrieve PostgreSQL password
az keyvault secret show \
  --vault-name $(terraform output -raw key_vault_name) \
  --name PostgreSQL-AdminPassword \
  --query value -o tsv

# 3. Manually configure connection
# Use password to connect via psql or update app manually
```

## Best Practices

### DO ✅

- **Always use managed identities** for Azure service-to-service auth
- **Reference secrets without version** to auto-pickup rotations
- **Tag secrets** with rotation schedule and criticality
- **Enable soft delete** and purge protection in production
- **Monitor access logs** for anomalies
- **Test rotation** in dev/staging before production
- **Document emergency procedures** for team

### DON'T ❌

- **Never hardcode secrets** in Terraform or app code
- **Never output secrets** without `sensitive = true`
- **Never commit .tfvars** with passwords to git
- **Never disable RBAC** on Key Vault
- **Never share managed identity** credentials
- **Never skip testing** after rotation

## Compliance Checklist

- [x] Secrets stored in Azure Key Vault (not in code)
- [x] Access via managed identities (no API keys)
- [x] RBAC enforced (least privilege)
- [x] Audit logging enabled (90 day retention)
- [x] Soft delete enabled (90 days for prod, 7 for dev)
- [x] Purge protection enabled (production)
- [x] Rotation schedule defined (90 days)
- [x] Network restrictions configured (production VNet only)
- [x] Monitoring and alerts active
- [x] Break glass procedure documented

## Additional Resources

- [Azure Key Vault Best Practices](https://learn.microsoft.com/en-us/azure/key-vault/general/best-practices)
- [App Service Key Vault References](https://learn.microsoft.com/en-us/azure/app-service/app-service-key-vault-references)
- [Managed Identity Overview](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)
- [PostgreSQL Security Best Practices](https://www.postgresql.org/docs/current/auth-methods.html)

## Support

For issues with this configuration:
1. Check [Troubleshooting](#troubleshooting) section
2. Review Azure Monitor logs for access patterns
3. Verify managed identity role assignments
4. Open issue: https://github.com/your-org/honuaio/issues
