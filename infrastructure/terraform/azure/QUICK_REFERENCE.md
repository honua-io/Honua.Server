# Azure Key Vault Quick Reference

Quick reference for common Key Vault operations with HonuaIO infrastructure.

## Quick Commands

### Get All Secrets

```bash
# Set Key Vault name
export KV_NAME=$(cd infrastructure/terraform/azure && terraform output -raw key_vault_name)

# List all secrets
az keyvault secret list --vault-name $KV_NAME --query '[].name' -o table

# Get specific secret value
az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-ConnectionString --query value -o tsv
```

### Get PostgreSQL Connection String

```bash
# Full connection string (ready to use)
az keyvault secret show \
  --vault-name $KV_NAME \
  --name PostgreSQL-ConnectionString \
  --query value -o tsv

# Or construct from components
POSTGRES_HOST=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-Host --query value -o tsv)
POSTGRES_USER=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminUsername --query value -o tsv)
POSTGRES_PASSWORD=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminPassword --query value -o tsv)

echo "Host=$POSTGRES_HOST;Database=honua;Username=$POSTGRES_USER;Password=$POSTGRES_PASSWORD;SSL Mode=Require"
```

### Connect to PostgreSQL

```bash
# Using connection string from Key Vault
KV_NAME=$(cd infrastructure/terraform/azure && terraform output -raw key_vault_name)
POSTGRES_HOST=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-Host --query value -o tsv)
POSTGRES_USER=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminUsername --query value -o tsv)
POSTGRES_PASSWORD=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminPassword --query value -o tsv)

PGPASSWORD=$POSTGRES_PASSWORD psql "host=$POSTGRES_HOST port=5432 dbname=honua user=$POSTGRES_USER sslmode=require"
```

### Rotate Password

```bash
# Recommended: Use automated script
cd infrastructure/terraform/azure
./scripts/rotate-postgresql-password.sh --environment prod

# Preview changes without executing
./scripts/rotate-postgresql-password.sh --environment prod --dry-run
```

### Get Key Vault Reference for App Settings

```bash
# Get formatted reference for all secrets
cd infrastructure/terraform/azure
terraform output -json keyvault_reference_format | jq

# Use in App Service app settings like:
# "ConnectionStrings__PostgreSQL": "@Microsoft.KeyVault(SecretUri=https://kv-honua-abc123.vault.azure.net/secrets/PostgreSQL-ConnectionString)"
```

## Common Scenarios

### Scenario 1: Application Can't Connect to Database

**Diagnosis:**
```bash
# 1. Verify secret exists
az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-ConnectionString

# 2. Verify managed identity has access
FUNCTION_APP_NAME=$(cd infrastructure/terraform/azure && terraform output -raw function_app_name)
RESOURCE_GROUP=$(cd infrastructure/terraform/azure && terraform output -raw resource_group_name)

PRINCIPAL_ID=$(az functionapp show \
  --name $FUNCTION_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query identity.principalId -o tsv)

az role assignment list \
  --assignee $PRINCIPAL_ID \
  --scope $(az keyvault show --name $KV_NAME --query id -o tsv)

# 3. Test connection manually
POSTGRES_PASSWORD=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminPassword --query value -o tsv)
POSTGRES_HOST=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-Host --query value -o tsv)
POSTGRES_USER=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminUsername --query value -o tsv)

PGPASSWORD=$POSTGRES_PASSWORD psql "host=$POSTGRES_HOST port=5432 dbname=honua user=$POSTGRES_USER sslmode=require" -c "SELECT version();"
```

**Fix:**
```bash
# Grant managed identity Key Vault access
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $PRINCIPAL_ID \
  --scope $(az keyvault show --name $KV_NAME --query id -o tsv)

# Restart app to pick up new permissions
az functionapp restart --name $FUNCTION_APP_NAME --resource-group $RESOURCE_GROUP
```

### Scenario 2: Need to Access Secrets Locally for Development

**Option A: Azure CLI (Recommended)**
```bash
# Ensure you have Key Vault Secrets User role
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $(az ad signed-in-user show --query id -o tsv) \
  --scope $(az keyvault show --name $KV_NAME --query id -o tsv)

# Get secrets
az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-ConnectionString --query value -o tsv
```

**Option B: Local appsettings.Development.json (Not in Git!)**
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "<get from Key Vault using command above>"
  }
}
```

**Add to .gitignore:**
```
appsettings.Development.json
appsettings.*.local.json
```

### Scenario 3: Emergency Password Reset

If password is lost or compromised:

```bash
# 1. Generate new password
NEW_PASSWORD=$(openssl rand -base64 32 | tr -d "=+/" | cut -c1-32)Aa1

# 2. Update PostgreSQL (using admin account or Azure Portal)
# Via Azure Portal: PostgreSQL > Settings > Authentication > Reset password

# 3. Update Key Vault
az keyvault secret set \
  --vault-name $KV_NAME \
  --name PostgreSQL-AdminPassword \
  --value "$NEW_PASSWORD"

# 4. Update connection string
POSTGRES_HOST=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-Host --query value -o tsv)
POSTGRES_USER=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminUsername --query value -o tsv)

az keyvault secret set \
  --vault-name $KV_NAME \
  --name PostgreSQL-ConnectionString \
  --value "Host=$POSTGRES_HOST;Database=honua;Username=$POSTGRES_USER;Password=$NEW_PASSWORD;SSL Mode=Require"

# 5. Restart dependent services
FUNCTION_APP_NAME=$(cd infrastructure/terraform/azure && terraform output -raw function_app_name)
RESOURCE_GROUP=$(cd infrastructure/terraform/azure && terraform output -raw resource_group_name)

az functionapp restart --name $FUNCTION_APP_NAME --resource-group $RESOURCE_GROUP
```

### Scenario 4: Add New Secret to Key Vault

```bash
# Using Terraform (Recommended)
# Add to main.tf:
resource "azurerm_key_vault_secret" "my_new_secret" {
  name         = "MyNewSecret"
  value        = var.my_secret_value
  key_vault_id = azurerm_key_vault.main.id

  depends_on = [azurerm_role_assignment.kv_admin]
}

# Apply changes
terraform apply

# Using Azure CLI (For temporary/one-off secrets)
az keyvault secret set \
  --vault-name $KV_NAME \
  --name MyNewSecret \
  --value "secret-value-here"
```

### Scenario 5: Monitor Secret Access

```bash
# Get workspace ID
WORKSPACE_ID=$(cd infrastructure/terraform/azure && terraform output -raw log_analytics_workspace_id)

# Query last 24 hours of secret access
az monitor log-analytics query \
  --workspace $WORKSPACE_ID \
  --analytics-query '
    AzureDiagnostics
    | where ResourceProvider == "MICROSOFT.KEYVAULT"
    | where OperationName == "SecretGet"
    | where TimeGenerated > ago(24h)
    | project TimeGenerated, CallerIPAddress, identity_claim_appid_g, ResultSignature, properties_s
    | order by TimeGenerated desc
  ' \
  --output table

# Check for failed access attempts
az monitor log-analytics query \
  --workspace $WORKSPACE_ID \
  --analytics-query '
    AzureDiagnostics
    | where ResourceProvider == "MICROSOFT.KEYVAULT"
    | where ResultSignature != "OK"
    | where TimeGenerated > ago(7d)
    | summarize FailedAttempts=count() by CallerIPAddress, OperationName, ResultSignature
    | order by FailedAttempts desc
  ' \
  --output table
```

## Environment-Specific Patterns

### Development Environment

```bash
# Use less restrictive policies
# Key Vault: Allow all Azure services
# Soft delete: 7 days
# Purge protection: Disabled

# Access secrets directly via Azure CLI
az keyvault secret show --vault-name kv-honua-dev-abc123 --name PostgreSQL-ConnectionString --query value -o tsv
```

### Production Environment

```bash
# Use strict policies
# Key Vault: VNet-restricted access only
# Soft delete: 90 days
# Purge protection: Enabled

# Access via managed identities only (no CLI access from local machines)
# Use jump box within VNet for emergency access
```

## Security Best Practices

### ✅ DO

- Always use managed identities for service-to-service authentication
- Reference secrets without version (auto-pickup rotations)
- Rotate PostgreSQL password every 90 days
- Monitor access logs for anomalies
- Use RBAC (never access policies)
- Enable soft delete and purge protection in production
- Tag secrets with rotation schedule

### ❌ DON'T

- Never hardcode secrets in code or Terraform state
- Never commit secrets to Git
- Never share managed identity credentials
- Never disable audit logging
- Never use same password across environments
- Never skip testing after rotation

## Helpful Aliases

Add to your `~/.bashrc` or `~/.zshrc`:

```bash
# HonuaIO Key Vault helpers
alias honua-kv='az keyvault secret show --vault-name $(cd ~/projects/HonuaIO/infrastructure/terraform/azure && terraform output -raw key_vault_name)'
alias honua-pg-conn='honua-kv --name PostgreSQL-ConnectionString --query value -o tsv'
alias honua-pg-password='honua-kv --name PostgreSQL-AdminPassword --query value -o tsv'
alias honua-pg-connect='PGPASSWORD=$(honua-pg-password) psql "host=$(honua-kv --name PostgreSQL-Host --query value -o tsv) port=5432 dbname=honua user=$(honua-kv --name PostgreSQL-AdminUsername --query value -o tsv) sslmode=require"'

# Usage:
# honua-pg-connect  # Directly connect to PostgreSQL
# honua-pg-conn     # Get connection string
```

## Terraform Outputs Reference

```bash
# All available outputs
cd infrastructure/terraform/azure
terraform output

# Specific outputs
terraform output -raw key_vault_name
terraform output -raw key_vault_uri
terraform output -json keyvault_secret_uris
terraform output -json keyvault_reference_format
```

## Further Reading

- [Full Key Vault Management Guide](./KEYVAULT_SECRET_MANAGEMENT.md)
- [Azure Key Vault Best Practices](https://learn.microsoft.com/en-us/azure/key-vault/general/best-practices)
- [Managed Identities Overview](https://learn.microsoft.com/en-us/azure/active-directory/managed-identities-azure-resources/overview)
