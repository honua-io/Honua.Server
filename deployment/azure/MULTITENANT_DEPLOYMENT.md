# Honua Multitenant SaaS Deployment Guide

## 🎯 Architecture Overview

This deployment creates a **fully serverless, multi-tenant SaaS platform** on Azure with:

- **YARP Gateway** (Container App) - SSL termination, subdomain routing, tenant extraction
- **Dapr Service Mesh** - mTLS between services, automatic discovery
- **Backend Services** (Container Apps) - OData, Raster, Vector, STAC APIs
- **Azure Functions** - Demo signup, trial cleanup
- **Azure DNS** - Wildcard `*.honua.io` routing
- **PostgreSQL** - Tenant database
- **Redis** - Rate limiting, caching

### Request Flow

```
User: https://acme.honua.io/api/collections
    ↓
Azure DNS: *.honua.io → YARP Container App IP
    ↓
YARP Container App:
  - Terminates SSL (wildcard cert *.honua.io)
  - Extracts tenant "acme" from subdomain
  - Routes /api/** to core-api
  - Adds X-Tenant-Id: acme header
    ↓ (Dapr mTLS service invocation)
Core API Container App:
  - Receives X-Tenant-Id: acme
  - TenantMiddleware validates tenant
  - Filters data by tenant
    ↓
Returns data for tenant "acme"
```

## 📋 Prerequisites

### Azure Resources

1. **Azure Subscription** with permissions to create:
   - Resource Groups
   - Container Apps
   - DNS Zones
   - Azure Functions
   - PostgreSQL Flexible Server
   - Managed Identities

2. **Azure CLI** installed and authenticated:
   ```bash
   az login
   az account set --subscription <subscription-id>
   ```

3. **DNS Zone** for your domain (e.g., `honua.io`):
   ```bash
   az network dns zone create \
     --resource-group honua-dns \
     --name honua.io
   ```

4. **Container Registry** (ACR):
   ```bash
   az acr create \
     --resource-group honua-shared \
     --name honuaregistry \
     --sku Standard
   ```

### Local Tools

- **.NET 9.0 SDK**
- **Docker** (for building images)
- **Azure Functions Core Tools** v4

## 🚀 Deployment Steps

### Step 1: Build Container Images

```bash
# Navigate to repository root
cd /home/mike/projects/HonuaIO

# Build YARP Gateway
docker build -t honuaregistry.azurecr.io/honua/gateway:latest \
  -f src/Honua.Server.Gateway/Dockerfile .

# Build backend services (example)
docker build -t honuaregistry.azurecr.io/honua/core-api:latest \
  -f deployment/docker/Dockerfile.host .

# Push to ACR
az acr login --name honuaregistry
docker push honuaregistry.azurecr.io/honua/gateway:latest
docker push honuaregistry.azurecr.io/honua/core-api:latest
```

### Step 2: Configure Parameters

Create `deployment/azure/bicep/parameters.prod.json`:

```json
{
  "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentParameters.json#",
  "contentVersion": "1.0.0.0",
  "parameters": {
    "environment": {
      "value": "prod"
    },
    "location": {
      "value": "eastus2"
    },
    "dnsZoneName": {
      "value": "honua.io"
    },
    "postgresPassword": {
      "value": "YOUR_SECURE_PASSWORD_HERE"
    },
    "imageTag": {
      "value": "latest"
    }
  }
}
```

### Step 3: Deploy Infrastructure

```bash
cd deployment/azure/bicep

# Deploy
az deployment sub create \
  --name honua-multitenant-$(date +%Y%m%d%H%M%S) \
  --location eastus2 \
  --template-file main.bicep \
  --parameters parameters.prod.json
```

### Step 4: Configure DNS

Get the YARP Gateway public IP:

```bash
az containerapp show \
  --resource-group honua-prod \
  --name honua-gateway \
  --query "properties.configuration.ingress.fqdn" \
  -o tsv
```

Create wildcard DNS record:

```bash
# Get gateway IP
GATEWAY_IP=$(az containerapp show \
  --resource-group honua-prod \
  --name honua-gateway \
  --query "properties.outboundIpAddresses[0]" \
  -o tsv)

# Create A record
az network dns record-set a add-record \
  --resource-group honua-dns \
  --zone-name honua.io \
  --record-set-name "*" \
  --ipv4-address $GATEWAY_IP
```

### Step 5: Configure SSL Certificate

Azure Container Apps manages SSL certificates automatically. Add custom domain:

```bash
# Add wildcard domain to Container App
az containerapp hostname add \
  --resource-group honua-prod \
  --name honua-gateway \
  --hostname "*.honua.io"

# Azure will automatically provision Let's Encrypt certificate
```

### Step 6: Initialize Database

Run migrations:

```bash
# Get PostgreSQL connection string
POSTGRES_HOST=$(az postgres flexible-server show \
  --resource-group honua-prod \
  --name honua-db-prod \
  --query "fullyQualifiedDomainName" \
  -o tsv)

# Run migrations
psql "host=$POSTGRES_HOST port=5432 dbname=honua user=honuaadmin password=YOUR_PASSWORD sslmode=require" \
  < src/Honua.Server.Core/Data/Migrations/001_InitialSchema.sql
```

### Step 7: Deploy Azure Functions

```bash
cd src/Honua.Server.Enterprise.Functions

# Publish
func azure functionapp publish honua-demo-prod

# Configure environment variables
az functionapp config appsettings set \
  --resource-group honua-prod \
  --name honua-demo-prod \
  --settings \
    PostgresConnectionString="Host=$POSTGRES_HOST;Database=honua;Username=honuaadmin;Password=YOUR_PASSWORD;SslMode=Require" \
    AzureSubscriptionId="<subscription-id>" \
    DnsResourceGroupName="honua-dns" \
    DnsZoneName="honua.io" \
    GatewayPublicIp="$GATEWAY_IP"
```

## 🧪 Testing

### Test Demo Signup

```bash
curl -X POST https://honua-demo-prod.azurewebsites.net/demo/signup \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "organizationName": "Acme Corp",
    "name": "John Doe"
  }'
```

Expected response:

```json
{
  "tenantId": "acme-corp",
  "organizationName": "Acme Corp",
  "email": "test@example.com",
  "url": "https://acme-corp.honua.io",
  "trialExpiresAt": "2025-11-12T10:30:00Z",
  "message": "Demo environment created successfully! Your 14-day trial has started."
}
```

### Test Tenant Access

```bash
# Access tenant-specific API
curl https://acme-corp.honua.io/api/collections \
  -H "Accept: application/json"

# Should return collections for tenant "acme-corp" only
```

## 🔒 Security Configuration

### Enable Dapr mTLS

Already enabled by default in Container Apps environment. Verify:

```bash
az containerapp env dapr-component list \
  --resource-group honua-prod \
  --name honua-prod
```

### Configure Managed Identity

Grant DNS permissions to Functions:

```bash
# Get Function App identity
FUNCTION_IDENTITY=$(az functionapp identity show \
  --resource-group honua-prod \
  --name honua-demo-prod \
  --query "principalId" \
  -o tsv)

# Grant DNS Zone Contributor role
az role assignment create \
  --assignee $FUNCTION_IDENTITY \
  --role "DNS Zone Contributor" \
  --scope "/subscriptions/<subscription-id>/resourceGroups/honua-dns/providers/Microsoft.Network/dnszones/honua.io"
```

## 📊 Monitoring

### Application Insights

View logs:

```bash
az monitor app-insights query \
  --app honua-prod-insights \
  --analytics-query "traces | where message contains 'tenant' | take 50"
```

### Container App Logs

```bash
az containerapp logs show \
  --resource-group honua-prod \
  --name honua-gateway \
  --follow
```

## 🔄 Updates and Scaling

### Update Container Image

```bash
# Build and push new image
docker build -t honuaregistry.azurecr.io/honua/gateway:v2.0 .
docker push honuaregistry.azurecr.io/honua/gateway:v2.0

# Update Container App
az containerapp update \
  --resource-group honua-prod \
  --name honua-gateway \
  --image honuaregistry.azurecr.io/honua/gateway:v2.0
```

### Manual Scaling

```bash
az containerapp update \
  --resource-group honua-prod \
  --name honua-gateway \
  --min-replicas 3 \
  --max-replicas 10
```

## 🧹 Maintenance

### Trial Cleanup

Automatic cleanup runs daily at 2 AM UTC via Azure Function. Manual trigger:

```bash
az functionapp function trigger \
  --resource-group honua-prod \
  --name honua-demo-prod \
  --function-name TrialCleanup
```

### Database Backup

```bash
az postgres flexible-server backup create \
  --resource-group honua-prod \
  --name honua-db-prod \
  --backup-name manual-backup-$(date +%Y%m%d)
```

## 🐛 Troubleshooting

### Tenant Not Found

Check database:

```bash
psql "host=$POSTGRES_HOST..." -c "SELECT customer_id, organization_name, subscription_status FROM customers WHERE deleted_at IS NULL;"
```

### DNS Not Resolving

```bash
# Check DNS record
nslookup acme-corp.honua.io

# Verify wildcard record
az network dns record-set a show \
  --resource-group honua-dns \
  --zone-name honua.io \
  --name "*"
```

### SSL Certificate Issues

```bash
# Check certificate binding
az containerapp hostname list \
  --resource-group honua-prod \
  --name honua-gateway
```

## 💰 Cost Optimization

- **Container Apps**: ~$50/month (3 apps, minimal traffic)
- **Azure Functions**: ~$5/month (consumption plan)
- **PostgreSQL**: ~$80/month (Burstable B2s)
- **DNS**: ~$0.50/month (per zone)
- **Total**: ~$135/month for demo/dev environment

Production scaling adjustments needed based on load.

## 📚 Additional Resources

- [Azure Container Apps Documentation](https://learn.microsoft.com/azure/container-apps/)
- [Dapr Documentation](https://docs.dapr.io/)
- [YARP Documentation](https://microsoft.github.io/reverse-proxy/)
- [Azure DNS Documentation](https://learn.microsoft.com/azure/dns/)

## 🆘 Support

For issues:
1. Check logs in Application Insights
2. Review Container App logs
3. Verify DNS configuration
4. Check database connectivity
