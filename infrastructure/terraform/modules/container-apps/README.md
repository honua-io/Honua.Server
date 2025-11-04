# Azure Container Apps Serverless Module

Comprehensive Terraform module for deploying Honua GIS Platform on Azure Container Apps with Azure Database for PostgreSQL, Azure Front Door CDN, and managed identity.

## Overview

Deploy a serverless Honua GIS stack on Azure featuring:

- **Azure Container Apps**: Fully managed serverless containers with Dapr
- **Azure Database for PostgreSQL**: Managed PostgreSQL with PostGIS
- **Azure Front Door**: Global CDN and load balancing
- **Azure Key Vault**: Secure secrets management
- **Managed Identity**: Passwordless authentication
- **Log Analytics**: Centralized logging and monitoring

## Features

- **True Serverless**: Scale to zero when not in use
- **Auto-scaling**: 0-30 replicas based on HTTP traffic
- **Global CDN**: Azure Front Door for worldwide performance
- **High Availability**: Zone-redundant deployment available
- **Managed Identity**: No credential management needed
- **VNET Integration**: Private networking for security

## Prerequisites

1. **Azure Subscription**: Active Azure subscription
2. **Azure CLI**: For deployment (`az login`)
3. **Container Registry**: Azure Container Registry with image
4. **Terraform**: Version >= 1.5.0

## Usage

### Basic Deployment

```hcl
module "honua_azure" {
  source = "../../modules/container-apps"

  environment     = "production"
  location        = "eastus"
  container_image = "myregistry.azurecr.io/honua:latest"

  # Auto-scaling
  min_replicas = 0
  max_replicas = 30

  # Database
  create_database = true
  db_sku_name     = "B_Standard_B1ms"
}
```

### Development Environment

```hcl
module "honua_dev" {
  source = "../../modules/container-apps"

  environment     = "dev"
  location        = "eastus"
  container_image = "myregistry.azurecr.io/honua:dev"

  # Minimal resources
  container_cpu    = 0.5
  container_memory = "1Gi"
  min_replicas     = 0
  max_replicas     = 5

  # Smaller database
  db_sku_name              = "B_Standard_B1ms"
  db_storage_mb            = 32768
  db_high_availability_mode = "Disabled"

  # No Front Door for dev
  create_front_door = false
}
```

### Production with High Availability

```hcl
module "honua_prod" {
  source = "../../modules/container-apps"

  environment     = "production"
  location        = "eastus"
  container_image = "myregistry.azurecr.io/honua:v1.2.3"

  # Production scaling
  container_cpu    = 2
  container_memory = "4Gi"
  min_replicas     = 2  # Always-on
  max_replicas     = 30

  # High availability database
  db_sku_name               = "GP_Standard_D2s_v3"
  db_storage_mb             = 131072  # 128 GB
  db_high_availability_mode = "ZoneRedundant"
  db_geo_redundant_backup   = true
  db_backup_retention_days  = 30

  # Front Door Premium for WAF
  create_front_door = true
  front_door_sku    = "Premium_AzureFrontDoor"
  custom_domains    = ["api.honua.io"]

  log_retention_days = 90

  tags = {
    Team       = "Platform"
    CostCenter = "Engineering"
  }
}
```

## Key Variables

| Name | Description | Default |
|------|-------------|---------|
| `environment` | Environment (dev/staging/production) | required |
| `container_image` | Full container image path | required |
| `min_replicas` | Minimum replicas (0 for serverless) | `0` |
| `max_replicas` | Maximum replicas | `30` |
| `db_sku_name` | PostgreSQL SKU | `"B_Standard_B1ms"` |
| `create_front_door` | Enable Azure Front Door | `true` |
| `front_door_sku` | Front Door SKU | `"Standard_AzureFrontDoor"` |

## Outputs

- `container_app_url` - HTTPS URL of Container App
- `front_door_endpoint_url` - Front Door CDN URL
- `database_fqdn` - PostgreSQL server FQDN
- `key_vault_uri` - Key Vault URI
- `monitoring_urls` - Azure Portal monitoring links

## Post-Deployment

### 1. Push Container Image

```bash
az acr login --name myregistry
docker build -t honua:latest .
docker tag honua:latest myregistry.azurecr.io/honua:latest
docker push myregistry.azurecr.io/honua:latest
```

### 2. Install PostGIS

```bash
az postgres flexible-server execute \
  --name honua-production \
  --database-name honua \
  --query "CREATE EXTENSION IF NOT EXISTS postgis;"
```

### 3. Configure Custom Domain

```bash
# Get Front Door endpoint
terraform output front_door_endpoint_url

# Add CNAME record:
# api.honua.io -> honua-production-endpoint.azurefd.net
```

### 4. Test Deployment

```bash
curl https://honua-production.azurecontainerapps.io/health
curl https://api.honua.io/api/v1/layers
```

## Cost Estimation

**Development**: ~$15-25/month
- Container Apps: ~$5 (free tier covers most)
- PostgreSQL B1ms: ~$12
- No Front Door

**Production (Low Traffic)**: ~$150-250/month
- Container Apps: ~$30-50
- PostgreSQL GP_Standard_D2s_v3: ~$100
- Front Door Standard: ~$35
- Log Analytics: ~$10

**Production (High Traffic)**: ~$500-1000/month
- Container Apps: ~$200-400
- PostgreSQL HA: ~$200-400
- Front Door Premium: ~$330+
- Additional data transfer and requests

## Troubleshooting

### Container App Not Starting

```bash
# View logs
az containerapp logs show --name honua-production --resource-group honua-production-rg

# Check revisions
az containerapp revision list --name honua-production --resource-group honua-production-rg
```

### Database Connection Issues

```bash
# Test connectivity
az postgres flexible-server connect --name honua-production

# Check firewall rules (should use VNet integration)
az postgres flexible-server firewall-rule list --resource-group honua-production-rg --name honua-production
```

### Scaling Issues

```bash
# View metrics
az monitor metrics list --resource <container-app-id> --metric Requests

# Check revision status
az containerapp revision show --name honua-production --revision <revision-name>
```

## Security Best Practices

1. **Use Managed Identity**: No passwords needed
2. **VNet Integration**: Private database access
3. **Key Vault**: Store all secrets
4. **Front Door WAF**: Use Premium tier for WAF
5. **Enable diagnostic logs**: Monitor all activities
6. **Private endpoints**: For storage and Key Vault

## Integration with Existing Resources

```hcl
# Use existing VNet
create_vnet            = false
container_apps_subnet_id = "/subscriptions/.../subnets/ca-subnet"
postgresql_subnet_id   = "/subscriptions/.../subnets/db-subnet"

# Use existing resource group
create_resource_group = false
resource_group_name   = "existing-rg"

# Use existing Log Analytics
create_log_analytics      = false
log_analytics_workspace_id = "/subscriptions/.../workspaces/my-law"
```

## Support

- [Azure Container Apps Docs](https://learn.microsoft.com/en-us/azure/container-apps/)
- [Azure Database for PostgreSQL Docs](https://learn.microsoft.com/en-us/azure/postgresql/)
- [Honua Platform](https://github.com/HonuaIO/honua)

## License

Part of Honua platform, licensed under Elastic License 2.0.
