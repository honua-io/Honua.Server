# HonuaIO Azure AI Infrastructure - Terraform

Complete Azure infrastructure for the AI-powered deployment consultant.

## Architecture

This Terraform configuration deploys:

- **Azure OpenAI Service** - GPT-4 Turbo (chat) + text-embedding-3-large (vectors)
- **Azure AI Search** - Vector database with hybrid search (Basic tier)
- **PostgreSQL Flexible Server** - Deployment telemetry and pattern storage
- **Application Insights** - Monitoring, distributed tracing, cost tracking
- **Azure Functions** - Nightly pattern analysis job (consumption plan)
- **Key Vault** - Secure secret storage with RBAC
- **Storage Account** - Function App runtime storage

## Cost Estimate

| Service | SKU | Monthly Cost |
|---------|-----|--------------|
| Azure OpenAI | S0 (10K TPM) | $100* |
| Azure AI Search | Basic | $75 |
| PostgreSQL | B1ms Burstable | $14 |
| Application Insights | Pay-as-you-go | $5 |
| Azure Functions | Consumption | $0** |
| Key Vault | Standard | $0*** |
| Storage Account | LRS | $1 |
| **Total** | | **$195/month** |

\* Usage-based: $10 per 1M input tokens, $30 per 1M output tokens
\** Pay per execution (~$0.20 per 1M executions)
\*** First 10,000 operations free

**With Azure for Startups credits:**
- Tier 1: $200/month → Your cost: $0 (Year 1)
- Tier 2: $1,000/month → Your cost: $0 (covers AI + customer POCs)
- Tier 3: $2,083/month → Your cost: $0 (covers AI + multiple customers)

## Prerequisites

1. **Azure subscription** with OpenAI access approved
   - Apply: https://aka.ms/oai/access
   - Approval usually takes 1-2 business days

2. **Azure CLI** installed and authenticated
   ```bash
   az login
   az account set --subscription "Your Subscription Name"
   ```

3. **Terraform** >= 1.5.0 installed
   ```bash
   terraform -v
   ```

## Quick Start

1. **Clone and navigate to infrastructure directory**
   ```bash
   cd infrastructure/terraform/azure
   ```

2. **Copy and configure variables**
   ```bash
   cp terraform.tfvars.example terraform.tfvars
   # Edit terraform.tfvars with your values
   ```

3. **Initialize Terraform**
   ```bash
   terraform init
   ```

4. **Review deployment plan**
   ```bash
   terraform plan
   ```

5. **Deploy infrastructure**
   ```bash
   terraform apply
   ```

   This will take ~10-15 minutes to complete.

6. **Save outputs**
   ```bash
   terraform output -json > ../../../deployment-outputs.json
   ```

## Configuration

### Required Variables

Edit `terraform.tfvars`:

```hcl
location = "eastus"
environment = "dev"
admin_email = "your-email@example.com"
postgres_admin_username = "honuaadmin"
postgres_admin_password = "YourSecurePassword123!"
```

### Optional Customization

Edit `main.tf` to customize:

- **Azure OpenAI TPM**: Change `capacity` in `azurerm_cognitive_deployment` (10K default)
- **AI Search tier**: Change `sku` in `azurerm_search_service` (basic/standard/standard2)
- **PostgreSQL tier**: Change `sku_name` in `azurerm_postgresql_flexible_server`
- **High availability**: Automatically enabled in `prod` environment

## Post-Deployment Setup

### 1. Verify Key Vault Secret Storage

All secrets are automatically stored in Key Vault during deployment:

```bash
# List all secrets in Key Vault
KV_NAME=$(terraform output -raw key_vault_name)
az keyvault secret list --vault-name $KV_NAME --query '[].name' -o table

# Expected secrets:
#   - PostgreSQL-ConnectionString
#   - PostgreSQL-AdminUsername
#   - PostgreSQL-AdminPassword
#   - PostgreSQL-Host
#   - AzureOpenAI-ApiKey
#   - AzureSearch-ApiKey
#   - ApplicationInsights-ConnectionString
```

### 2. Configure PostgreSQL Schema

SSH into Azure Cloud Shell or use local psql:

```bash
# Get connection details from Key Vault (not hardcoded!)
POSTGRES_HOST=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-Host --query value -o tsv)
POSTGRES_USER=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminUsername --query value -o tsv)
POSTGRES_PASSWORD=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminPassword --query value -o tsv)

# Connect to PostgreSQL
PGPASSWORD=$POSTGRES_PASSWORD psql "host=$POSTGRES_HOST port=5432 dbname=honua user=$POSTGRES_USER sslmode=require"

# Run schema migration
\i ../../../src/Honua.Cli.AI/Database/schema.sql
```

### 3. Create Azure AI Search Index

```bash
# Get endpoints from Terraform output
SEARCH_ENDPOINT=$(terraform output -raw search_endpoint)
SEARCH_KEY=$(az keyvault secret show \
  --vault-name $(terraform output -raw key_vault_name) \
  --name AzureSearch-ApiKey \
  --query value -o tsv)

# Create index (using Azure CLI or REST API)
az search index create \
  --service-name $(terraform output -raw search_name) \
  --name deployment-knowledge \
  --fields @../../../src/Honua.Cli.AI/Database/search-index-schema.json
```

### 4. Configure Application Settings

**IMPORTANT**: Application settings now use Key Vault references, not hardcoded secrets.

Update `appsettings.Azure.json` in your CLI project:

```json
{
  "AzureOpenAI": {
    "Endpoint": "<from terraform output: openai_endpoint>",
    "DeploymentName": "gpt-4-turbo",
    "EmbeddingDeploymentName": "text-embedding-3-large"
  },
  "AzureAISearch": {
    "Endpoint": "<from terraform output: search_endpoint>",
    "IndexName": "deployment-knowledge"
  },
  "ApplicationInsights": {
    "ConnectionString": "<from Key Vault reference - see below>"
  }
}
```

**Get Key Vault references from Terraform:**

```bash
# Display all formatted Key Vault references
terraform output -json keyvault_reference_format | jq

# Example output:
# {
#   "postgres_connection_string": "@Microsoft.KeyVault(SecretUri=https://kv-honua-abc123.vault.azure.net/secrets/PostgreSQL-ConnectionString)",
#   "postgres_host": "@Microsoft.KeyVault(SecretUri=https://kv-honua-abc123.vault.azure.net/secrets/PostgreSQL-Host)",
#   ...
# }
```

### 5. Deploy Function App Code

```bash
cd ../../../src/Honua.Cli.AI
func azure functionapp publish $(terraform output -raw function_app_name)
```

## Azure for Startups Application

### Tier 2 Application ($1,000/month)

**Elevator Pitch:**
> "HonuaIO replaces $50-150K geospatial deployment consultants with an AI agent that designs, deploys, and optimizes infrastructure in hours instead of weeks. We're disrupting the $1B+ geospatial consulting market by democratizing enterprise GIS infrastructure for mid-market organizations. Built on Azure AI (OpenAI, AI Search), targeting $1M ARR in 18 months."

**Key Points:**
1. **Direct Azure consumption**: $195/month (AI services)
2. **Customer infrastructure driven**: $1.25M/month (VMs, DBs, Storage, Networking)
3. **Microsoft's ROI**: Invest $12K/year → Drive $15M/year consumption = **1,250x return**
4. **Competitive advantage**: Moving Esri customers from on-premises/AWS to Azure
5. **Market opportunity**: 350K+ Esri customers, 80% still on-premises

**Traction to highlight:**
- Open-source geospatial server (competitive with Esri's $100K product)
- AI consultant MVP launching Q2 2025
- Target: 10 paying customers by end of Year 1

## Remote State (Production)

For production, use remote state storage:

1. **Create state storage**
   ```bash
   az group create --name rg-honua-tfstate --location eastus
   az storage account create --name sthonuatfstate --resource-group rg-honua-tfstate --sku Standard_LRS
   az storage container create --name tfstate --account-name sthonuatfstate
   ```

2. **Uncomment backend configuration** in `main.tf`:
   ```hcl
   backend "azurerm" {
     resource_group_name  = "rg-honua-tfstate"
     storage_account_name = "sthonuatfstate"
     container_name       = "tfstate"
     key                  = "honua-ai-consultant.tfstate"
   }
   ```

3. **Re-initialize Terraform**
   ```bash
   terraform init -migrate-state
   ```

## Secret Management and Rotation

### Key Vault Integration

All secrets are stored in Azure Key Vault and accessed via managed identities:

- **No hardcoded secrets** in Terraform state or application code
- **Automatic rotation** support with zero-downtime updates
- **RBAC-enforced access** using Azure AD identities
- **Audit logging** for compliance and security monitoring

See [KEYVAULT_SECRET_MANAGEMENT.md](./KEYVAULT_SECRET_MANAGEMENT.md) for complete documentation.

### Rotate PostgreSQL Password

Recommended rotation: Every 90 days

```bash
# Automated rotation script
cd infrastructure/terraform/azure
./scripts/rotate-postgresql-password.sh --environment prod

# Manual rotation (if needed)
./scripts/rotate-postgresql-password.sh --environment prod --dry-run  # Preview changes
./scripts/rotate-postgresql-password.sh --environment prod            # Execute rotation
```

The rotation script:
1. Generates a secure 32-character password
2. Updates PostgreSQL server password
3. Updates Key Vault secrets (password + connection string)
4. Verifies new password works
5. Restarts dependent services

**No application code changes required** - services automatically pickup new secrets.

### Monitoring Secret Access

```bash
# View Key Vault access logs
az monitor log-analytics query \
  --workspace $(terraform output -raw log_analytics_workspace_id) \
  --analytics-query '
    AzureDiagnostics
    | where ResourceProvider == "MICROSOFT.KEYVAULT"
    | where OperationName == "SecretGet"
    | summarize Count=count() by identity_claim_appid_g, ResultSignature
    | order by Count desc
  ' \
  --output table

# Check last rotation date
az keyvault secret show \
  --vault-name $(terraform output -raw key_vault_name) \
  --name PostgreSQL-AdminPassword \
  --query 'attributes.updated' -o tsv
```

## Troubleshooting

### "Azure OpenAI access not approved"
Apply for access: https://aka.ms/oai/access (1-2 days approval time)

### "Key Vault access denied"
Ensure you're logged in with correct account:
```bash
az account show
az login --tenant <your-tenant-id>

# Grant yourself Key Vault Secrets User role
az role assignment create \
  --role "Key Vault Secrets User" \
  --assignee $(az ad signed-in-user show --query id -o tsv) \
  --scope $(terraform output -raw key_vault_id)
```

### "Function App can't read secrets"
Verify managed identity has correct role:
```bash
FUNCTION_PRINCIPAL=$(az functionapp show \
  --name $(terraform output -raw function_app_name) \
  --resource-group $(terraform output -raw resource_group_name) \
  --query identity.principalId -o tsv)

az role assignment list \
  --assignee $FUNCTION_PRINCIPAL \
  --scope $(terraform output -raw key_vault_id) \
  --query '[].roleDefinitionName' -o tsv

# Should show: "Key Vault Secrets User"
```

### "PostgreSQL firewall blocking connection"
Temporarily allow your IP:
```bash
MY_IP=$(curl -s https://api.ipify.org)
az postgres flexible-server firewall-rule create \
  --resource-group $(terraform output -raw resource_group_name) \
  --name postgres-honua-* \
  --rule-name AllowMyIP \
  --start-ip-address $MY_IP \
  --end-ip-address $MY_IP
```

### "Password rotation failed"
Check the rotation script logs:
```bash
# Test current connection
KV_NAME=$(terraform output -raw key_vault_name)
POSTGRES_PASSWORD=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminPassword --query value -o tsv)
POSTGRES_HOST=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-Host --query value -o tsv)
POSTGRES_USER=$(az keyvault secret show --vault-name $KV_NAME --name PostgreSQL-AdminUsername --query value -o tsv)

PGPASSWORD=$POSTGRES_PASSWORD psql "host=$POSTGRES_HOST port=5432 dbname=postgres user=$POSTGRES_USER sslmode=require" -c "\conninfo"
```

## Clean Up

To destroy all infrastructure:

```bash
terraform destroy
```

**Warning**: This will delete all data including PostgreSQL databases and Key Vault secrets.

## Support

- **Terraform Issues**: https://github.com/hashicorp/terraform/issues
- **Azure Provider**: https://github.com/hashicorp/terraform-provider-azurerm/issues
- **HonuaIO**: https://github.com/your-org/honuaio/issues
