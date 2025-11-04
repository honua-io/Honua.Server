# Secret Rotation Quick Reference

**Quick commands for common secret rotation tasks**

---

## AWS Commands

### Check Rotation Status

```bash
# List all secrets with rotation enabled
aws secretsmanager list-secrets \
  --filters Key=rotation-enabled,Values=true \
  --query 'SecretList[].[Name,RotationEnabled,LastRotatedDate]' \
  --output table

# Check specific secret
aws secretsmanager describe-secret --secret-id honua/prod/postgres/app
```

### Trigger Manual Rotation

```bash
# Rotate specific secret
aws secretsmanager rotate-secret --secret-id honua/prod/postgres/app

# Rotate all via Lambda
aws lambda invoke \
  --function-name honua-secret-rotation-prod \
  --payload '{"source":"manual"}' \
  response.json
```

### View Logs

```bash
# Tail Lambda logs
aws logs tail /aws/lambda/honua-secret-rotation-prod --follow

# Get last 50 log events
aws logs tail /aws/lambda/honua-secret-rotation-prod --since 1h
```

### Emergency Rollback

```bash
# Get previous version
aws secretsmanager list-secret-version-ids --secret-id honua/prod/postgres/app

# Rollback to previous version
aws secretsmanager update-secret-version-stage \
  --secret-id honua/prod/postgres/app \
  --version-stage AWSCURRENT \
  --move-to-version-id <previous-version-id>
```

---

## Azure Commands

### Check Rotation Status

```bash
# List all secrets with autoRotate tag
az keyvault secret list \
  --vault-name kv-honua-xxxxx \
  --query "[?tags.autoRotate=='true'].[name,attributes.updated]" \
  --output table

# Check specific secret
az keyvault secret show \
  --vault-name kv-honua-xxxxx \
  --name postgres-app
```

### Trigger Manual Rotation

```bash
# Get function key
FUNCTION_KEY=$(az functionapp keys list \
  --name func-rotation-xxxxx \
  --resource-group rg-honua-rotation-prod \
  --query "functionKeys.default" -o tsv)

# Trigger rotation
curl -X POST "https://func-rotation-xxxxx.azurewebsites.net/api/SecretRotation?code=$FUNCTION_KEY"
```

### View Logs

```bash
# Query Application Insights
az monitor app-insights query \
  --app appi-rotation-prod \
  --analytics-query "traces | where message contains 'rotation' | order by timestamp desc | take 50"

# Stream logs
az webapp log tail \
  --name func-rotation-xxxxx \
  --resource-group rg-honua-rotation-prod
```

### Emergency Rollback

```bash
# List secret versions
az keyvault secret list-versions \
  --vault-name kv-honua-xxxxx \
  --name postgres-app

# Restore previous version
az keyvault secret show \
  --vault-name kv-honua-xxxxx \
  --name postgres-app \
  --version <previous-version-id> \
  --query value -o tsv > previous.json

az keyvault secret set \
  --vault-name kv-honua-xxxxx \
  --name postgres-app \
  --file previous.json
```

---

## PostgreSQL Commands

### Verify Password

```bash
# Test connection with current secret
PGPASSWORD=<password> psql -h <host> -U honua_app -d honua -c "SELECT current_user;"
```

### Manual Password Change

```bash
# Connect as master user
PGPASSWORD=<master-pass> psql -h <host> -U postgres -d postgres

# Change password
ALTER USER honua_app WITH PASSWORD 'new-password';
```

---

## Common Scenarios

### Rotation Failed - Manual Fix

```bash
# 1. Check logs for error
aws logs tail /aws/lambda/honua-secret-rotation-prod --since 1h

# 2. Generate new password
NEW_PASS=$(openssl rand -base64 24 | tr -d /=+ | head -c 32)

# 3. Update in database
PGPASSWORD=<master> psql -h <host> -U postgres -c "ALTER USER honua_app WITH PASSWORD '$NEW_PASS';"

# 4. Test connection
PGPASSWORD=$NEW_PASS psql -h <host> -U honua_app -d honua -c "SELECT 1;"

# 5. Update secret
aws secretsmanager put-secret-value \
  --secret-id honua/prod/postgres/app \
  --secret-string "{\"password\":\"$NEW_PASS\",\"username\":\"honua_app\",\"host\":\"<host>\"}"

# 6. Restart app
kubectl rollout restart deployment/honua-api
```

### Secret Compromised

```bash
# 1. IMMEDIATELY rotate
aws secretsmanager rotate-secret --secret-id honua/prod/postgres/app

# 2. Force app restart
kubectl rollout restart deployment/honua-api

# 3. Audit access
aws cloudtrail lookup-events \
  --lookup-attributes AttributeKey=ResourceName,AttributeValue=honua/prod/postgres/app

# 4. Notify security team
# Send email to security@honua.io
```

### Check Last Rotation Date

```bash
# AWS
aws secretsmanager describe-secret \
  --secret-id honua/prod/postgres/app \
  --query 'LastRotatedDate' \
  --output text

# Azure
az keyvault secret show \
  --vault-name kv-honua-xxxxx \
  --name postgres-app \
  --query 'attributes.updated' \
  --output tsv
```

---

## Health Checks

### Verify Rotation Schedule

```bash
# AWS - Check EventBridge rule
aws events describe-rule --name honua-secret-rotation-prod-schedule

# Azure - Check Function schedule
az functionapp config appsettings list \
  --name func-rotation-xxxxx \
  --resource-group rg-honua-rotation-prod \
  --query "[?name=='WEBSITE_TIME_ZONE']"
```

### Test Rotation Function

```bash
# AWS
aws lambda invoke \
  --function-name honua-secret-rotation-prod \
  --payload '{"secretType":"postgresql","secretId":"honua/prod/postgres/app"}' \
  test-response.json

# Azure
curl -X POST "https://func-rotation-xxxxx.azurewebsites.net/api/SecretRotation?code=$FUNCTION_KEY" \
  -H "Content-Type: application/json" \
  -d '{"secretName":"postgres-app"}'
```

---

## Monitoring

### Check for Failed Rotations

```bash
# AWS CloudWatch
aws cloudwatch get-metric-statistics \
  --namespace AWS/Lambda \
  --metric-name Errors \
  --dimensions Name=FunctionName,Value=honua-secret-rotation-prod \
  --start-time $(date -u -d '7 days ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
  --period 86400 \
  --statistics Sum

# Azure Monitor
az monitor metrics list \
  --resource <function-resource-id> \
  --metric FunctionExecutionCount \
  --aggregation Count \
  --filter "Status eq 'Failed'"
```

### Get Rotation History

```bash
# AWS
aws secretsmanager list-secret-version-ids \
  --secret-id honua/prod/postgres/app \
  --max-results 10

# Azure
az keyvault secret list-versions \
  --vault-name kv-honua-xxxxx \
  --name postgres-app \
  --maxresults 10
```

---

## Emergency Contacts

- **Security Team**: security@honua.io
- **DevOps On-Call**: Slack #honua-oncall
- **PagerDuty**: Escalate via PD app

---

## Related Documentation

- [Full Rotation Runbook](./SECRET_ROTATION_RUNBOOK.md)
- [Production Deployment Checklist](./PRODUCTION_DEPLOYMENT_CHECKLIST.md)
- [Security Architecture](./SECURITY_ARCHITECTURE.md)
