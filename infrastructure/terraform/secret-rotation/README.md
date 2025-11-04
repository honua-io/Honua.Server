# Secret Rotation Infrastructure

Automated secret rotation infrastructure for HonuaIO using Terraform.

## Overview

This infrastructure automatically rotates:
- **PostgreSQL passwords** - Application database credentials
- **API keys** - Service API authentication keys
- **JWT signing keys** - Token signing keys for authentication

**Rotation Schedule**: Every 90 days (configurable)

## Architecture

### AWS Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Secret Rotation Flow                     │
└─────────────────────────────────────────────────────────────┘

EventBridge Rule          Secrets Manager
(Every 90 days)          (Rotation Schedule)
      │                         │
      └────────┬────────────────┘
               │
               ▼
         Lambda Function
         ┌──────────────┐
         │ 1. Get Secret │
         │ 2. Generate   │
         │ 3. Set New    │
         │ 4. Test       │
         │ 5. Finalize   │
         └──────┬───────┘
                │
         ┌──────┴──────┬────────────┐
         │             │            │
         ▼             ▼            ▼
    PostgreSQL    API Database   SNS Topic
    (Update)     (Update Hash)  (Notify)
```

### Azure Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                     Secret Rotation Flow                     │
└─────────────────────────────────────────────────────────────┘

Timer Trigger            HTTP Trigger
(Cron: 0 0 0 */90 * *)  (Manual)
      │                         │
      └────────┬────────────────┘
               │
               ▼
       Azure Function
       ┌──────────────┐
       │ 1. Get Secret │
       │ 2. Generate   │
       │ 3. Update DB  │
       │ 4. Test       │
       │ 5. Store KV   │
       └──────┬───────┘
                │
         ┌──────┴──────┬────────────┐
         │             │            │
         ▼             ▼            ▼
    PostgreSQL    Key Vault    Action Group
    (Update)      (Store)      (Notify)
```

## Prerequisites

### AWS
- AWS CLI configured with appropriate credentials
- Terraform >= 1.5.0
- Access to:
  - AWS Secrets Manager
  - AWS Lambda
  - Amazon EventBridge
  - Amazon SNS
  - RDS/PostgreSQL instance

### Azure
- Azure CLI with active subscription
- Terraform >= 1.5.0
- Access to:
  - Azure Key Vault
  - Azure Functions
  - Azure PostgreSQL
  - Azure Monitor

## Quick Start

### 1. Clone and Navigate

```bash
cd infrastructure/terraform/secret-rotation
```

### 2. Choose Your Cloud Provider

#### AWS Deployment

```bash
cd aws

# Copy example variables
cp terraform.tfvars.example terraform.tfvars

# Edit variables
nano terraform.tfvars

# Initialize Terraform
terraform init

# Review plan
terraform plan

# Apply configuration
terraform apply
```

#### Azure Deployment

```bash
cd azure

# Login to Azure
az login

# Copy example variables
cp terraform.tfvars.example terraform.tfvars

# Edit variables
nano terraform.tfvars

# Initialize Terraform
terraform init

# Review plan
terraform plan

# Apply configuration
terraform apply
```

## Configuration

### Required Variables

| Variable | Description | Example |
|----------|-------------|---------|
| `environment` | Environment name | `prod` |
| `project_name` | Project name | `honua` |
| `rotation_days` | Days between rotations | `90` |
| `notification_email` | Email for alerts | `security@company.com` |
| `postgres_host` | Database hostname | `db.example.com` |
| `postgres_master_username` | Master DB username | `postgres` |
| `postgres_master_password` | Master DB password | `SecurePass123!` |
| `postgres_app_username` | App DB username | `honua_app` |
| `api_endpoint` | API endpoint for testing | `api.honua.io` |

### Optional Variables

| Variable | Description | Default |
|----------|-------------|---------|
| `postgres_port` | Database port | `5432` |
| `postgres_database` | Database name | `honua` |
| `tags` | Resource tags | `{}` |

## Post-Deployment

### 1. Verify Infrastructure

```bash
# AWS: Check Lambda function
aws lambda get-function --function-name honua-secret-rotation-prod

# Azure: Check Function App
az functionapp show --name func-rotation-xxxxx --resource-group rg-honua-rotation-prod
```

### 2. Deploy Function Code

#### AWS

```bash
cd ../../functions/secret-rotation/aws

# Install dependencies
npm install

# Package function
zip -r function.zip index.js package.json node_modules/

# Deploy
aws lambda update-function-code \
  --function-name honua-secret-rotation-prod \
  --zip-file fileb://function.zip
```

#### Azure

```bash
cd ../../functions/secret-rotation/azure

# Install dependencies
npm install

# Install Azure Functions Core Tools (if not installed)
npm install -g azure-functions-core-tools@4 --unsafe-perm true

# Deploy
func azure functionapp publish func-rotation-xxxxx
```

### 3. Test Rotation

#### AWS

```bash
# Trigger test rotation
aws lambda invoke \
  --function-name honua-secret-rotation-prod \
  --payload '{"source":"manual"}' \
  response.json

# Check logs
aws logs tail /aws/lambda/honua-secret-rotation-prod --follow
```

#### Azure

```bash
# Get function URL and key
FUNCTION_KEY=$(az functionapp keys list \
  --name func-rotation-xxxxx \
  --resource-group rg-honua-rotation-prod \
  --query "functionKeys.default" -o tsv)

# Trigger test rotation
curl -X POST "https://func-rotation-xxxxx.azurewebsites.net/api/SecretRotation?code=$FUNCTION_KEY"

# Check logs
az monitor app-insights query \
  --app appi-rotation-prod \
  --analytics-query "traces | order by timestamp desc | take 50"
```

### 4. Confirm Email Subscription

Check your email for SNS/Action Group subscription confirmation and confirm it.

## Monitoring

### CloudWatch/Azure Monitor Dashboards

The infrastructure creates:
- **Error rate alarms** - Triggered on rotation failures
- **Duration alarms** - Triggered on slow rotations
- **Log groups** - Centralized rotation logs

### View Metrics

#### AWS

```bash
# View Lambda metrics
aws cloudwatch get-metric-statistics \
  --namespace AWS/Lambda \
  --metric-name Errors \
  --dimensions Name=FunctionName,Value=honua-secret-rotation-prod \
  --start-time $(date -u -d '1 day ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
  --period 3600 \
  --statistics Sum
```

#### Azure

```bash
# View Function metrics
az monitor metrics list \
  --resource /subscriptions/.../resourceGroups/rg-honua-rotation-prod/providers/Microsoft.Web/sites/func-rotation-xxxxx \
  --metric "FunctionExecutionCount" \
  --start-time $(date -u -d '1 day ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S)
```

## Security Considerations

### Encryption

- **AWS**: All secrets encrypted with KMS (key rotation enabled)
- **Azure**: All secrets encrypted with Key Vault (managed keys)

### Access Control

- **AWS**: Lambda uses least-privilege IAM role
- **Azure**: Function uses Managed Identity with RBAC

### Network Security

- Lambda can be deployed in VPC for private database access
- Function can use VNet integration for Azure

### Audit Logging

- **AWS**: CloudTrail logs all secret access
- **Azure**: Key Vault audit logs enabled

## Troubleshooting

### Rotation Fails

1. **Check function logs**
   ```bash
   # AWS
   aws logs tail /aws/lambda/honua-secret-rotation-prod

   # Azure
   az monitor app-insights query --app appi-rotation-prod --analytics-query "traces | where severityLevel >= 3"
   ```

2. **Verify database connectivity**
   ```bash
   nc -zv your-database-host 5432
   ```

3. **Test master credentials**
   ```bash
   PGPASSWORD=your-master-password psql -h db-host -U postgres -c "SELECT 1"
   ```

### Application Can't Connect

1. **Verify secret value**
   ```bash
   # AWS
   aws secretsmanager get-secret-value --secret-id honua/prod/postgres/app

   # Azure
   az keyvault secret show --vault-name kv-honua-xxxxx --name postgres-app
   ```

2. **Restart application**
   ```bash
   # Force new deployment to pick up updated secrets
   ```

See [SECRET_ROTATION_RUNBOOK.md](../../../docs/security/SECRET_ROTATION_RUNBOOK.md) for detailed troubleshooting.

## Cost Estimates

### AWS Monthly Costs

| Service | Usage | Cost |
|---------|-------|------|
| Lambda | ~100 invocations/month (5 min ea) | $0.02 |
| Secrets Manager | 3 secrets | $1.20 |
| KMS | 3 keys | $3.00 |
| SNS | ~100 emails/month | $0.01 |
| **Total** | | **~$4.23/month** |

### Azure Monthly Costs

| Service | Usage | Cost |
|---------|-------|------|
| Functions | ~100 executions/month | $0.00 (free tier) |
| Key Vault | 3 secrets | $0.15 |
| Application Insights | Basic monitoring | $0.00 (free tier) |
| **Total** | | **~$0.15/month** |

## Cleanup

To destroy all infrastructure:

```bash
# AWS
cd aws
terraform destroy

# Azure
cd azure
terraform destroy
```

**Warning**: This will:
- Delete rotation function
- Remove rotation schedules
- Delete secrets (after recovery window)

## Support

For issues or questions:
- **Documentation**: [Secret Rotation Runbook](../../../docs/security/SECRET_ROTATION_RUNBOOK.md)
- **Security**: security@honua.io
- **Issues**: GitHub Issues

## License

MIT License - See LICENSE file for details
