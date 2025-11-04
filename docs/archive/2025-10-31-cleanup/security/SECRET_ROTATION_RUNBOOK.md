# Secret Rotation Runbook

**Version**: 1.0
**Last Updated**: 2025-10-18
**Owners**: Security Team, DevOps Team

---

## Table of Contents

1. [Overview](#overview)
2. [Automated Rotation](#automated-rotation)
3. [Manual Rotation Procedures](#manual-rotation-procedures)
4. [Emergency Rotation](#emergency-rotation)
5. [Troubleshooting](#troubleshooting)
6. [Verification](#verification)
7. [Rollback Procedures](#rollback-procedures)

---

## Overview

HonuaIO implements automated secret rotation for:
- PostgreSQL database passwords
- API keys
- JWT signing keys

**Rotation Schedule**: Every 90 days (configurable)

**Notification Channels**:
- Email: security@honua.io
- SNS/Teams webhook for real-time alerts

---

## Automated Rotation

### AWS Setup

The automated rotation runs via AWS Lambda triggered by:
1. **Secrets Manager Rotation Schedule** (every 90 days)
2. **EventBridge Scheduled Event** (backup trigger)
3. **Manual Invocation** (on-demand)

#### Check Rotation Status (AWS)

```bash
# List all secrets and their rotation status
aws secretsmanager list-secrets \
  --query 'SecretList[?RotationEnabled==`true`].[Name,RotationEnabled,LastRotatedDate]' \
  --output table

# Check specific secret rotation
aws secretsmanager describe-secret \
  --secret-id honua/prod/postgres/app

# View rotation Lambda logs
aws logs tail /aws/lambda/honua-secret-rotation-prod --follow
```

#### Trigger Manual Rotation (AWS)

```bash
# Rotate specific secret
aws secretsmanager rotate-secret \
  --secret-id honua/prod/postgres/app

# Rotate all tagged secrets via Lambda
aws lambda invoke \
  --function-name honua-secret-rotation-prod \
  --payload '{"source":"manual"}' \
  response.json

cat response.json
```

### Azure Setup

The automated rotation runs via Azure Function triggered by:
1. **Timer Trigger** (every 90 days via cron: `0 0 0 */90 * *`)
2. **HTTP Trigger** (manual invocation)

#### Check Rotation Status (Azure)

```bash
# List all secrets
az keyvault secret list \
  --vault-name kv-honua-xxxxx \
  --query "[?tags.autoRotate=='true'].[name,attributes.updated]" \
  --output table

# Check specific secret
az keyvault secret show \
  --vault-name kv-honua-xxxxx \
  --name postgres-app \
  --query "{name:name,updated:attributes.updated,tags:tags}"

# View function logs
az monitor app-insights query \
  --app appi-rotation-prod \
  --analytics-query "traces | where message contains 'rotation' | order by timestamp desc | take 50"
```

#### Trigger Manual Rotation (Azure)

```bash
# Get function key
FUNCTION_KEY=$(az functionapp keys list \
  --name func-rotation-xxxxx \
  --resource-group rg-honua-rotation-prod \
  --query "functionKeys.default" -o tsv)

# Trigger rotation for all secrets
curl -X POST "https://func-rotation-xxxxx.azurewebsites.net/api/SecretRotation?code=$FUNCTION_KEY" \
  -H "Content-Type: application/json"

# Trigger rotation for specific secret
curl -X POST "https://func-rotation-xxxxx.azurewebsites.net/api/SecretRotation?code=$FUNCTION_KEY" \
  -H "Content-Type: application/json" \
  -d '{"secretName": "postgres-app"}'
```

---

## Manual Rotation Procedures

Use manual rotation when:
- Automated rotation fails
- Secret is compromised (emergency)
- Testing rotation process
- Rotating master credentials

### 1. PostgreSQL Password Rotation

#### Prerequisites
- Access to PostgreSQL server
- Master/admin credentials
- Access to Secrets Manager/Key Vault

#### Steps (AWS)

```bash
# 1. Generate new password
NEW_PASSWORD=$(openssl rand -base64 24 | tr -d /=+ | head -c 32)

# 2. Connect to PostgreSQL with master credentials
MASTER_SECRET=$(aws secretsmanager get-secret-value \
  --secret-id honua/prod/postgres/master \
  --query SecretString --output text)

MASTER_USER=$(echo $MASTER_SECRET | jq -r '.username')
MASTER_PASS=$(echo $MASTER_SECRET | jq -r '.password')
DB_HOST=$(echo $MASTER_SECRET | jq -r '.host')

# 3. Update password in PostgreSQL
PGPASSWORD=$MASTER_PASS psql -h $DB_HOST -U $MASTER_USER -d postgres -c \
  "ALTER USER honua_app WITH PASSWORD '$NEW_PASSWORD';"

# 4. Test new credentials
PGPASSWORD=$NEW_PASSWORD psql -h $DB_HOST -U honua_app -d honua -c "SELECT 1;"

# 5. Update secret in Secrets Manager
SECRET_VALUE=$(cat <<EOF
{
  "type": "postgresql",
  "host": "$DB_HOST",
  "port": 5432,
  "database": "honua",
  "username": "honua_app",
  "password": "$NEW_PASSWORD",
  "lastRotated": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
EOF
)

aws secretsmanager put-secret-value \
  --secret-id honua/prod/postgres/app \
  --secret-string "$SECRET_VALUE"

# 6. Restart application to pick up new credentials
# (Depends on your deployment - ECS, Kubernetes, etc.)

# 7. Verify application connectivity
curl https://your-api.honua.io/healthz/ready
```

#### Steps (Azure)

```bash
# 1. Generate new password
NEW_PASSWORD=$(openssl rand -base64 24 | tr -d /=+ | head -c 32)

# 2. Get master credentials from Key Vault
VAULT_NAME="kv-honua-xxxxx"

MASTER_SECRET=$(az keyvault secret show \
  --vault-name $VAULT_NAME \
  --name postgres-master \
  --query value -o tsv)

MASTER_USER=$(echo $MASTER_SECRET | jq -r '.username')
MASTER_PASS=$(echo $MASTER_SECRET | jq -r '.password')
DB_HOST=$(echo $MASTER_SECRET | jq -r '.host')

# 3. Update password in PostgreSQL
PGPASSWORD=$MASTER_PASS psql -h $DB_HOST -U $MASTER_USER -d postgres -c \
  "ALTER USER honua_app WITH PASSWORD '$NEW_PASSWORD';"

# 4. Test new credentials
PGPASSWORD=$NEW_PASSWORD psql -h $DB_HOST -U honua_app -d honua -c "SELECT 1;"

# 5. Update secret in Key Vault
SECRET_VALUE=$(cat <<EOF
{
  "type": "postgresql",
  "host": "$DB_HOST",
  "port": 5432,
  "database": "honua",
  "username": "honua_app",
  "password": "$NEW_PASSWORD",
  "lastRotated": "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
}
EOF
)

az keyvault secret set \
  --vault-name $VAULT_NAME \
  --name postgres-app \
  --value "$SECRET_VALUE"

# 6. Restart application
az containerapp revision restart \
  --name honua-app \
  --resource-group rg-honua-prod

# 7. Verify
curl https://your-api.honua.io/healthz/ready
```

### 2. API Key Rotation

#### Steps

```bash
# 1. Generate new API key
PREFIX="honua"
RANDOM_PART=$(openssl rand -hex 32)
NEW_API_KEY="${PREFIX}_${RANDOM_PART}"

# 2. Hash the API key for database storage
# (This step is done by the application, but for manual process:)

# 3. Update in database
PGPASSWORD=$DB_PASSWORD psql -h $DB_HOST -U $DB_USER -d honua <<EOF
UPDATE api_keys
SET
  key_hash = crypt('$NEW_API_KEY', gen_salt('bf')),
  updated_at = NOW(),
  rotated_at = NOW()
WHERE key_id = 'your-key-id';
EOF

# 4. Test new API key
curl -H "X-API-Key: $NEW_API_KEY" https://your-api.honua.io/api/collections

# 5. Store in secrets manager (AWS)
aws secretsmanager put-secret-value \
  --secret-id honua/prod/api-keys/main \
  --secret-string "{\"apiKey\":\"$NEW_API_KEY\",\"keyId\":\"your-key-id\"}"

# 5. Store in Key Vault (Azure)
az keyvault secret set \
  --vault-name $VAULT_NAME \
  --name api-key-main \
  --value "{\"apiKey\":\"$NEW_API_KEY\",\"keyId\":\"your-key-id\"}"

# 6. Notify API key consumers
# Send email/notification with new key
```

### 3. JWT Signing Key Rotation

**WARNING**: JWT signing key rotation requires careful coordination to avoid service disruption.

#### Steps

```bash
# 1. Generate new signing key (256-bit)
NEW_KEY=$(openssl rand -base64 32)

# 2. Update in secrets storage WITHOUT removing old key
# AWS:
aws secretsmanager put-secret-value \
  --secret-id honua/prod/jwt/signing-key \
  --secret-string "{\"signingKey\":\"$NEW_KEY\",\"lastRotated\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}"

# Azure:
az keyvault secret set \
  --vault-name $VAULT_NAME \
  --name jwt-signing-key \
  --value "{\"signingKey\":\"$NEW_KEY\",\"lastRotated\":\"$(date -u +%Y-%m-%dT%H:%M:%SZ)\"}"

# 3. Deploy application update with BOTH old and new keys
# This allows validation of existing tokens while issuing new ones

# 4. Wait for token expiration period (default: 60 minutes)
sleep 3600

# 5. Remove old key from configuration

# 6. Verify all tokens are using new key
# Check application logs for JWT validation errors
```

---

## Emergency Rotation

**When to use**: Secret compromise, security breach, unauthorized access

### Immediate Actions

```bash
# 1. IMMEDIATELY disable compromised credentials

# AWS - Disable secret
aws secretsmanager update-secret \
  --secret-id honua/prod/postgres/app \
  --description "COMPROMISED - Rotation in progress"

# Azure - Disable secret
az keyvault secret set-attributes \
  --vault-name $VAULT_NAME \
  --name postgres-app \
  --enabled false

# 2. Rotate to new credentials (follow manual rotation steps above)

# 3. Force application restart to clear any cached credentials
# AWS ECS:
aws ecs update-service \
  --cluster honua-prod \
  --service honua-api \
  --force-new-deployment

# Azure Container Apps:
az containerapp revision restart \
  --name honua-app \
  --resource-group rg-honua-prod

# Kubernetes:
kubectl rollout restart deployment/honua-api -n honua

# 4. Audit access logs
# AWS CloudTrail:
aws cloudtrail lookup-events \
  --lookup-attributes AttributeKey=ResourceName,AttributeValue=honua/prod/postgres/app \
  --max-items 100

# Azure Activity Log:
az monitor activity-log list \
  --resource-group rg-honua-prod \
  --start-time $(date -u -d '24 hours ago' +%Y-%m-%dT%H:%M:%SZ) \
  --query "[?contains(resourceId, 'postgres-app')]"

# 5. Notify security team
# Send incident report via email/Slack/Teams

# 6. Document incident
# Create incident report in security documentation
```

---

## Troubleshooting

### Rotation Failed Error

#### Symptoms
- Rotation Lambda/Function fails
- Email notification with "Rotation Failed"
- Application can't connect to database

#### Diagnosis

```bash
# Check rotation function logs (AWS)
aws logs tail /aws/lambda/honua-secret-rotation-prod --since 1h

# Check rotation function logs (Azure)
az monitor app-insights query \
  --app appi-rotation-prod \
  --analytics-query "traces | where timestamp > ago(1h) and severityLevel >= 3"

# Test database connectivity
nc -zv your-db-host.rds.amazonaws.com 5432

# Verify master credentials work
PGPASSWORD=$MASTER_PASS psql -h $DB_HOST -U $MASTER_USER -d postgres -c "SELECT version();"
```

#### Resolution

```bash
# 1. Check if rotation is in AWSPENDING state
aws secretsmanager describe-secret --secret-id honua/prod/postgres/app

# 2. If stuck in AWSPENDING, force completion or rollback
aws secretsmanager update-secret-version-stage \
  --secret-id honua/prod/postgres/app \
  --version-stage AWSPENDING \
  --remove-from-version-id <pending-version-id>

# 3. Retry rotation
aws secretsmanager rotate-secret --secret-id honua/prod/postgres/app

# 4. If all else fails, perform manual rotation
```

### Application Can't Connect After Rotation

#### Diagnosis
- Check application logs for authentication errors
- Verify secret value is correct
- Check application is using latest secret version

#### Resolution

```bash
# 1. Verify secret value
aws secretsmanager get-secret-value --secret-id honua/prod/postgres/app

# 2. Test credentials manually
SECRET_VALUE=$(aws secretsmanager get-secret-value --secret-id honua/prod/postgres/app --query SecretString --output text)
DB_PASS=$(echo $SECRET_VALUE | jq -r '.password')
PGPASSWORD=$DB_PASS psql -h $DB_HOST -U honua_app -d honua -c "SELECT 1;"

# 3. If credentials work, restart application
aws ecs update-service --cluster honua-prod --service honua-api --force-new-deployment

# 4. If credentials don't work, rollback to previous version
aws secretsmanager update-secret-version-stage \
  --secret-id honua/prod/postgres/app \
  --version-stage AWSCURRENT \
  --move-to-version-id <previous-version-id>
```

### Rotation Takes Too Long

#### Symptoms
- Rotation timeout errors
- CloudWatch alarm triggered

#### Resolution

```bash
# 1. Increase Lambda timeout (currently 5 minutes)
aws lambda update-function-configuration \
  --function-name honua-secret-rotation-prod \
  --timeout 600

# 2. Check for network issues
# Verify VPC configuration, security groups, NACLs

# 3. Optimize rotation logic
# Consider rotating secrets in batches
```

---

## Verification

### Post-Rotation Checks

```bash
# 1. Verify secret was updated
aws secretsmanager get-secret-value \
  --secret-id honua/prod/postgres/app \
  --query "{Name:Name,Version:VersionId,UpdatedDate:UpdatedDate}"

# 2. Test database connectivity
SECRET=$(aws secretsmanager get-secret-value --secret-id honua/prod/postgres/app --query SecretString --output text)
DB_PASS=$(echo $SECRET | jq -r '.password')
PGPASSWORD=$DB_PASS psql -h $DB_HOST -U honua_app -d honua -c "SELECT current_database(), current_user, version();"

# 3. Verify application health
curl https://your-api.honua.io/healthz/ready

# 4. Check application logs for errors
kubectl logs -n honua deployment/honua-api --tail=100 | grep -i "error\|fail\|auth"

# 5. Run smoke tests
curl https://your-api.honua.io/api/collections
curl https://your-api.honua.io/api/features

# 6. Verify rotation notification was sent
# Check email inbox for "Secret Rotation SUCCESS"
```

### Rotation Audit

```bash
# List all rotation events (last 30 days)
aws secretsmanager list-secret-version-ids \
  --secret-id honua/prod/postgres/app \
  --max-results 10

# Check CloudTrail for rotation events
aws cloudtrail lookup-events \
  --lookup-attributes AttributeKey=EventName,AttributeValue=RotateSecret \
  --max-items 20

# Azure audit logs
az monitor activity-log list \
  --resource-group rg-honua-rotation-prod \
  --start-time $(date -u -d '30 days ago' +%Y-%m-%dT%H:%M:%SZ) \
  --query "[?operationName.value contains 'Microsoft.KeyVault']"
```

---

## Rollback Procedures

### Rollback to Previous Secret Version

#### AWS

```bash
# 1. List secret versions
aws secretsmanager list-secret-version-ids \
  --secret-id honua/prod/postgres/app

# 2. Identify previous AWSCURRENT version
PREVIOUS_VERSION="<version-id-before-rotation>"

# 3. Move AWSCURRENT stage back to previous version
aws secretsmanager update-secret-version-stage \
  --secret-id honua/prod/postgres/app \
  --version-stage AWSCURRENT \
  --move-to-version-id $PREVIOUS_VERSION

# 4. Restart application
aws ecs update-service --cluster honua-prod --service honua-api --force-new-deployment

# 5. Verify
curl https://your-api.honua.io/healthz/ready
```

#### Azure

```bash
# 1. List secret versions
az keyvault secret list-versions \
  --vault-name $VAULT_NAME \
  --name postgres-app

# 2. Restore previous version
PREVIOUS_VERSION="<version-id>"
az keyvault secret show \
  --vault-name $VAULT_NAME \
  --name postgres-app \
  --version $PREVIOUS_VERSION \
  --query value -o tsv > previous-secret.json

az keyvault secret set \
  --vault-name $VAULT_NAME \
  --name postgres-app \
  --file previous-secret.json

# 3. Restart application
az containerapp revision restart --name honua-app --resource-group rg-honua-prod

# 4. Verify
curl https://your-api.honua.io/healthz/ready
```

---

## Maintenance

### Monthly Tasks

- [ ] Review rotation logs for failures
- [ ] Verify all secrets have been rotated in last 90 days
- [ ] Test manual rotation procedure in staging
- [ ] Review and update rotation documentation
- [ ] Audit secret access logs

### Quarterly Tasks

- [ ] Test emergency rotation procedure
- [ ] Review rotation IAM policies and permissions
- [ ] Update rotation function dependencies
- [ ] Conduct rotation drill with team
- [ ] Review rotation schedule (adjust if needed)

---

## Contacts

**Security Team**: security@honua.io
**DevOps On-Call**: Slack #honua-oncall
**Emergency**: PagerDuty escalation

---

## Change Log

| Date       | Version | Changes                                    | Author         |
|------------|---------|-------------------------------------------|----------------|
| 2025-10-18 | 1.0     | Initial rotation runbook                  | Security Team  |

---

## References

- [AWS Secrets Manager Rotation](https://docs.aws.amazon.com/secretsmanager/latest/userguide/rotating-secrets.html)
- [Azure Key Vault Secret Rotation](https://learn.microsoft.com/en-us/azure/key-vault/secrets/overview-storage-keys-rotation)
- [NIST Secret Management Guidelines](https://csrc.nist.gov/publications/detail/sp/800-57-part-1/rev-5/final)
