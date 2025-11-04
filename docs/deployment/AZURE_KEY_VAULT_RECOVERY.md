# Azure Key Vault Recovery Procedures

**Last Updated**: 2025-10-18
**Status**: Production Ready
**Version**: 1.0

## Table of Contents

1. [Overview](#overview)
2. [Soft Delete and Purge Protection](#soft-delete-and-purge-protection)
3. [Recovery Scenarios](#recovery-scenarios)
4. [Step-by-Step Recovery Procedures](#step-by-step-recovery-procedures)
5. [Secret Backup and Restore](#secret-backup-and-restore)
6. [Monitoring and Alerts](#monitoring-and-alerts)
7. [Best Practices](#best-practices)

---

## Overview

Azure Key Vault provides critical secret management for Honua deployments. This document outlines recovery procedures for Key Vault and its contents (secrets, keys, certificates).

### Key Vault Configuration by Environment

| Feature | Development | Staging | Production |
|---------|-------------|---------|------------|
| **Soft Delete** | Enabled (7 days) | Enabled (7 days) | Enabled (90 days) |
| **Purge Protection** | Disabled | Disabled | **Enabled** |
| **Network Access** | Allow All | Allow All | Restricted VNets |
| **RBAC** | Enabled | Enabled | Enabled |
| **Backup Frequency** | Weekly | Daily | Daily |

### Recovery Objectives

| Environment | RTO (Recovery Time) | RPO (Recovery Point) |
|-------------|---------------------|----------------------|
| **Production** | < 15 minutes | < 1 hour |
| **Staging** | < 30 minutes | < 4 hours |
| **Development** | < 1 hour | < 24 hours |

---

## Soft Delete and Purge Protection

### Soft Delete

**What it does**: When a Key Vault or secret is deleted, it enters a "soft deleted" state for a retention period (7-90 days). During this time, it can be recovered.

**Configuration**:
```hcl
# Terraform: infrastructure/terraform/azure/main.tf
soft_delete_retention_days = var.environment == "prod" ? 90 : 7
```

**Key Points**:
- Enabled by default in Azure (cannot be disabled)
- Retention period: 7 days (minimum) to 90 days (maximum)
- Soft-deleted resources don't count against quota
- Deleted resources are hidden but still billable

### Purge Protection

**What it does**: When enabled, prevents permanent deletion of Key Vaults and secrets until the retention period expires. This is **irreversible** once enabled.

**Configuration**:
```hcl
# Terraform: infrastructure/terraform/azure/main.tf
purge_protection_enabled = var.environment == "prod" ? true : false
```

**Key Points**:
- **Production**: Enabled (compliance requirement)
- **Non-production**: Disabled (allows quick cleanup)
- **Warning**: Once enabled, purge protection cannot be disabled
- Protects against accidental or malicious purging

### When Purge Protection is Enabled

```
Deletion Flow (Production):
┌─────────────────┐
│  Delete Request │
└────────┬────────┘
         │
         ▼
┌─────────────────────────┐
│  Soft Deleted State     │
│  (90 days retention)    │
│  - Can be recovered     │
│  - Cannot be purged     │
│  - Still billable       │
└────────┬────────────────┘
         │ (after 90 days)
         ▼
┌─────────────────────────┐
│  Permanently Deleted    │
│  (automatic)            │
└─────────────────────────┘
```

### When Purge Protection is Disabled

```
Deletion Flow (Development/Staging):
┌─────────────────┐
│  Delete Request │
└────────┬────────┘
         │
         ▼
┌─────────────────────────┐
│  Soft Deleted State     │
│  (7 days retention)     │
│  - Can be recovered     │
│  - Can be purged        │◄──── Manual purge possible
│  - Still billable       │
└────────┬────────────────┘
         │ (after 7 days OR manual purge)
         ▼
┌─────────────────────────┐
│  Permanently Deleted    │
└─────────────────────────┘
```

---

## Recovery Scenarios

### Scenario 1: Accidentally Deleted Secret

**Problem**: Developer accidentally deleted a critical secret (e.g., database password)

**Impact**: Application may fail to authenticate

**Recovery**: Restore soft-deleted secret

**Time to Recover**: < 5 minutes

---

### Scenario 2: Key Vault Deleted

**Problem**: Entire Key Vault was deleted

**Impact**: All secrets, keys, and certificates unavailable

**Recovery**: Recover soft-deleted Key Vault

**Time to Recover**: < 15 minutes

---

### Scenario 3: Secret Overwritten with Wrong Value

**Problem**: Secret updated with incorrect value

**Impact**: Application using wrong configuration

**Recovery**: Restore previous secret version

**Time to Recover**: < 5 minutes

---

### Scenario 4: Key Vault Purged (Non-Production)

**Problem**: Key Vault was purged before retention expired

**Impact**: Permanent data loss

**Recovery**: Recreate Key Vault and restore secrets from backup

**Time to Recover**: < 1 hour

---

## Step-by-Step Recovery Procedures

### Procedure 1: Recover Deleted Secret

**When to use**: Secret deleted within retention period

**Prerequisites**:
- User has `Key Vault Administrator` or `Key Vault Secrets Officer` role
- Secret was deleted within retention period (7-90 days)

**Steps**:

1. **List soft-deleted secrets**:
   ```bash
   # Using Azure CLI
   az keyvault secret list-deleted \
     --vault-name kv-honua-abc123 \
     --query "[].{Name:name, DeletedDate:deletedDate, ScheduledPurgeDate:scheduledPurgeDate}" \
     --output table
   ```

2. **Verify the secret to recover**:
   ```bash
   # Get details of deleted secret
   az keyvault secret show-deleted \
     --vault-name kv-honua-abc123 \
     --name PostgreSQL-ConnectionString
   ```

3. **Recover the secret**:
   ```bash
   # Recover deleted secret
   az keyvault secret recover \
     --vault-name kv-honua-abc123 \
     --name PostgreSQL-ConnectionString

   # Verify recovery
   az keyvault secret show \
     --vault-name kv-honua-abc123 \
     --name PostgreSQL-ConnectionString
   ```

4. **Test application**:
   ```bash
   # Restart application to pick up recovered secret
   az webapp restart \
     --name honua-app-prod \
     --resource-group rg-honua-prod-eastus

   # Verify health
   curl -f https://honua-app-prod.azurewebsites.net/health
   ```

**Recovery Time**: < 5 minutes

---

### Procedure 2: Recover Deleted Key Vault

**When to use**: Entire Key Vault deleted within retention period

**Prerequisites**:
- User has `Owner` role on subscription
- Key Vault was deleted within retention period

**Steps**:

1. **List soft-deleted Key Vaults**:
   ```bash
   # List deleted Key Vaults in subscription
   az keyvault list-deleted \
     --query "[].{Name:name, Location:properties.location, DeletionDate:properties.deletionDate, ScheduledPurgeDate:properties.scheduledPurgeDate}" \
     --output table
   ```

2. **Get details of deleted Key Vault**:
   ```bash
   az keyvault show-deleted \
     --name kv-honua-abc123 \
     --location eastus
   ```

3. **Recover the Key Vault**:
   ```bash
   # Recover deleted Key Vault
   az keyvault recover \
     --name kv-honua-abc123 \
     --location eastus \
     --resource-group rg-honua-prod-eastus

   # Wait for recovery to complete (usually 1-2 minutes)
   az keyvault wait \
     --name kv-honua-abc123 \
     --exists
   ```

4. **Verify recovery**:
   ```bash
   # Check Key Vault status
   az keyvault show \
     --name kv-honua-abc123 \
     --resource-group rg-honua-prod-eastus

   # List secrets to verify contents
   az keyvault secret list \
     --vault-name kv-honua-abc123 \
     --query "[].name" \
     --output table
   ```

5. **Update application configuration** (if needed):
   ```bash
   # If Key Vault URI changed, update app settings
   az webapp config appsettings set \
     --name honua-app-prod \
     --resource-group rg-honua-prod-eastus \
     --settings KeyVault__VaultUri="https://kv-honua-abc123.vault.azure.net/"
   ```

**Recovery Time**: < 15 minutes

---

### Procedure 3: Restore Previous Secret Version

**When to use**: Secret was updated with wrong value

**Prerequisites**:
- Secret versioning enabled (default in Azure Key Vault)
- Previous version exists

**Steps**:

1. **List secret versions**:
   ```bash
   # List all versions of a secret
   az keyvault secret list-versions \
     --vault-name kv-honua-abc123 \
     --name PostgreSQL-ConnectionString \
     --query "[].{Version:id, Enabled:attributes.enabled, Created:attributes.created, Updated:attributes.updated}" \
     --output table
   ```

2. **Get previous version value**:
   ```bash
   # Get specific version
   PREVIOUS_VERSION="abc123def456"

   az keyvault secret show \
     --vault-name kv-honua-abc123 \
     --name PostgreSQL-ConnectionString \
     --version "$PREVIOUS_VERSION"
   ```

3. **Restore previous version** (two options):

   **Option A: Set previous version as current**:
   ```bash
   # Get previous version value
   PREVIOUS_VALUE=$(az keyvault secret show \
     --vault-name kv-honua-abc123 \
     --name PostgreSQL-ConnectionString \
     --version "$PREVIOUS_VERSION" \
     --query "value" \
     --output tsv)

   # Set as current version
   az keyvault secret set \
     --vault-name kv-honua-abc123 \
     --name PostgreSQL-ConnectionString \
     --value "$PREVIOUS_VALUE"
   ```

   **Option B: Use previous version directly**:
   ```bash
   # Update application to use specific version
   # Format: https://{vault}.vault.azure.net/secrets/{name}/{version}
   SECRET_URI="https://kv-honua-abc123.vault.azure.net/secrets/PostgreSQL-ConnectionString/$PREVIOUS_VERSION"

   az webapp config appsettings set \
     --name honua-app-prod \
     --resource-group rg-honua-prod-eastus \
     --settings ConnectionStrings__PostgreSQL="@Microsoft.KeyVault(SecretUri=$SECRET_URI)"
   ```

4. **Verify application**:
   ```bash
   # Restart application
   az webapp restart \
     --name honua-app-prod \
     --resource-group rg-honua-prod-eastus

   # Check health
   curl -f https://honua-app-prod.azurewebsites.net/health
   ```

**Recovery Time**: < 5 minutes

---

### Procedure 4: Restore Key Vault from Backup

**When to use**: Key Vault purged or corrupted beyond recovery

**Prerequisites**:
- Backup exists in Azure Storage
- User has permissions to create Key Vault and Storage access

**Steps**:

1. **Create new Key Vault**:
   ```bash
   # Create new Key Vault (if purged)
   az keyvault create \
     --name kv-honua-restored-$(date +%s) \
     --resource-group rg-honua-prod-eastus \
     --location eastus \
     --enable-rbac-authorization true \
     --enable-soft-delete true \
     --soft-delete-retention-days 90 \
     --enable-purge-protection true  # For production
   ```

2. **Download backup**:
   ```bash
   # Find latest backup
   LATEST_BACKUP=$(az storage blob list \
     --container config-backups \
     --account-name stbkphonua123456 \
     --query "sort_by([?contains(name, 'keyvault-secrets')], &properties.lastModified)[-1].name" \
     --output tsv)

   # Download backup
   az storage blob download \
     --container config-backups \
     --name "$LATEST_BACKUP" \
     --file /tmp/keyvault-backup.json \
     --account-name stbkphonua123456
   ```

3. **Restore secrets**:
   ```bash
   #!/bin/bash
   # restore-keyvault-secrets.sh

   VAULT_NAME="kv-honua-restored-1234567890"
   BACKUP_FILE="/tmp/keyvault-backup.json"

   # Parse backup and restore each secret
   jq -r '.secrets[] | @base64' "$BACKUP_FILE" | while read -r secret; do
     _jq() {
       echo "$secret" | base64 --decode | jq -r "$1"
     }

     SECRET_NAME=$(_jq '.name')
     SECRET_VALUE=$(_jq '.value')
     SECRET_CONTENT_TYPE=$(_jq '.contentType')
     SECRET_TAGS=$(_jq '.tags')

     echo "Restoring secret: $SECRET_NAME"

     az keyvault secret set \
       --vault-name "$VAULT_NAME" \
       --name "$SECRET_NAME" \
       --value "$SECRET_VALUE" \
       --content-type "$SECRET_CONTENT_TYPE" \
       --tags "$SECRET_TAGS"
   done

   echo "All secrets restored successfully"
   ```

4. **Update application to use new Key Vault**:
   ```bash
   # Update app settings
   NEW_VAULT_URI=$(az keyvault show \
     --name kv-honua-restored-1234567890 \
     --query "properties.vaultUri" \
     --output tsv)

   az webapp config appsettings set \
     --name honua-app-prod \
     --resource-group rg-honua-prod-eastus \
     --settings KeyVault__VaultUri="$NEW_VAULT_URI"
   ```

5. **Verify restoration**:
   ```bash
   # List restored secrets
   az keyvault secret list \
     --vault-name kv-honua-restored-1234567890 \
     --query "[].name" \
     --output table

   # Test application
   az webapp restart \
     --name honua-app-prod \
     --resource-group rg-honua-prod-eastus

   curl -f https://honua-app-prod.azurewebsites.net/health
   ```

**Recovery Time**: < 1 hour

---

## Secret Backup and Restore

### Automated Secret Backup

**Backup Script** (runs daily at 3:00 AM UTC):

```bash
#!/bin/bash
# backup-keyvault-secrets.sh

set -e

VAULT_NAME="kv-honua-abc123"
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="/tmp/keyvault-secrets-${BACKUP_DATE}.json"
STORAGE_ACCOUNT="stbkphonua123456"
STORAGE_CONTAINER="config-backups"

echo "=== Key Vault Secret Backup ==="
echo "Vault: ${VAULT_NAME}"
echo "Date: ${BACKUP_DATE}"

# Initialize backup JSON
echo '{"vault":"'${VAULT_NAME}'","backupDate":"'${BACKUP_DATE}'","secrets":[]}' > "$BACKUP_FILE"

# Get list of secrets
SECRETS=$(az keyvault secret list \
  --vault-name "$VAULT_NAME" \
  --query "[].name" \
  --output tsv)

# Backup each secret
for SECRET_NAME in $SECRETS; do
  echo "Backing up secret: $SECRET_NAME"

  # Get secret details (without value)
  SECRET_INFO=$(az keyvault secret show \
    --vault-name "$VAULT_NAME" \
    --name "$SECRET_NAME" \
    --query "{name:name, contentType:contentType, tags:tags, enabled:attributes.enabled, created:attributes.created}" \
    --output json)

  # Note: We do NOT backup the actual secret value for security
  # Only backup metadata to facilitate recreation

  # Add to backup file
  echo "$SECRET_INFO" | jq --arg name "$SECRET_NAME" \
    '. + {name: $name, hasValue: true}' >> "$BACKUP_FILE.tmp"
done

# Consolidate backup file
jq -s '{vault: "'${VAULT_NAME}'", backupDate: "'${BACKUP_DATE}'", secrets: .}' \
  "$BACKUP_FILE.tmp" > "$BACKUP_FILE"

rm -f "$BACKUP_FILE.tmp"

# Encrypt backup
echo "Encrypting backup..."
gpg --symmetric --cipher-algo AES256 \
  --output "${BACKUP_FILE}.gpg" \
  "$BACKUP_FILE"

rm "$BACKUP_FILE"

# Upload to Azure Storage
echo "Uploading to Azure Storage..."
az storage blob upload \
  --container "$STORAGE_CONTAINER" \
  --file "${BACKUP_FILE}.gpg" \
  --name "keyvault-secrets/keyvault-secrets-${BACKUP_DATE}.json.gpg" \
  --account-name "$STORAGE_ACCOUNT" \
  --overwrite

# Cleanup local backup
rm -f "${BACKUP_FILE}.gpg"

echo "✓ Backup completed successfully"
echo "Backup location: ${STORAGE_CONTAINER}/keyvault-secrets/keyvault-secrets-${BACKUP_DATE}.json.gpg"
```

### Manual Secret Export

For disaster recovery purposes, export all secrets to a secure location:

```bash
#!/bin/bash
# export-keyvault-secrets.sh
# WARNING: This exports actual secret values - handle with extreme care!

VAULT_NAME="kv-honua-abc123"
OUTPUT_FILE="/secure/location/secrets-export-$(date +%Y%m%d).txt"

# Ensure output directory is secure
mkdir -p "$(dirname "$OUTPUT_FILE")"
chmod 700 "$(dirname "$OUTPUT_FILE")"

# Export secrets
az keyvault secret list --vault-name "$VAULT_NAME" --query "[].name" -o tsv | while read -r SECRET_NAME; do
  echo "=== $SECRET_NAME ===" >> "$OUTPUT_FILE"
  az keyvault secret show \
    --vault-name "$VAULT_NAME" \
    --name "$SECRET_NAME" \
    --query "value" \
    --output tsv >> "$OUTPUT_FILE"
  echo "" >> "$OUTPUT_FILE"
done

# Encrypt export
gpg --encrypt --recipient backup@honua.io "$OUTPUT_FILE"
rm "$OUTPUT_FILE"

echo "Secrets exported to ${OUTPUT_FILE}.gpg"
```

---

## Monitoring and Alerts

### Key Vault Metrics to Monitor

| Metric | Threshold | Alert Severity | Action |
|--------|-----------|----------------|--------|
| `ServiceApiResult` (failures) | > 5 failures/min | Warning | Check access policies |
| `Availability` | < 99% | Critical | Azure support ticket |
| `ServiceApiLatency` | > 1000ms | Warning | Performance investigation |
| `SaturateShoebox` | > 80% | Warning | Scale up tier |

### Alert Configuration

```hcl
# Terraform: infrastructure/terraform/azure/main.tf

resource "azurerm_monitor_metric_alert" "keyvault_access_failures" {
  name                = "keyvault-access-failures-${var.environment}"
  resource_group_name = azurerm_resource_group.main.name
  scopes              = [azurerm_key_vault.main.id]
  description         = "Alert when Key Vault access failures exceed threshold"
  severity            = 2

  criteria {
    metric_namespace = "Microsoft.KeyVault/vaults"
    metric_name      = "ServiceApiResult"
    aggregation      = "Count"
    operator         = "GreaterThan"
    threshold        = 5

    dimension {
      name     = "ActivityType"
      operator = "Include"
      values   = ["SecretGet", "SecretList", "SecretSet"]
    }
  }

  frequency   = "PT5M"
  window_size = "PT15M"

  action {
    action_group_id = azurerm_monitor_action_group.backup_alerts.id
  }
}
```

### Monitoring Script

```bash
#!/bin/bash
# monitor-keyvault-health.sh

VAULT_NAME="kv-honua-abc123"

echo "=== Key Vault Health Check ==="

# Check vault exists
if ! az keyvault show --name "$VAULT_NAME" &>/dev/null; then
  echo "✗ CRITICAL: Key Vault not found!"
  exit 1
fi

# Check soft delete status
SOFT_DELETE=$(az keyvault show \
  --name "$VAULT_NAME" \
  --query "properties.enableSoftDelete" \
  --output tsv)

if [ "$SOFT_DELETE" != "true" ]; then
  echo "✗ WARNING: Soft delete not enabled"
fi

# Check purge protection (production only)
PURGE_PROTECTION=$(az keyvault show \
  --name "$VAULT_NAME" \
  --query "properties.enablePurgeProtection" \
  --output tsv)

echo "Purge Protection: $PURGE_PROTECTION"

# Check secret count
SECRET_COUNT=$(az keyvault secret list \
  --vault-name "$VAULT_NAME" \
  --query "length([])" \
  --output tsv)

echo "Secret Count: $SECRET_COUNT"

if [ "$SECRET_COUNT" -lt 5 ]; then
  echo "✗ WARNING: Unexpectedly low secret count"
fi

# Check recent access logs
echo "Recent Access Logs:"
az monitor activity-log list \
  --resource-id "/subscriptions/$(az account show --query id -o tsv)/resourceGroups/rg-honua-prod-eastus/providers/Microsoft.KeyVault/vaults/$VAULT_NAME" \
  --start-time "$(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%SZ)" \
  --query "[].{Time:eventTimestamp, Operation:operationName.value, Status:status.value}" \
  --output table

echo "✓ Health check completed"
```

---

## Best Practices

### 1. Prevention

- **Enable purge protection in production**: Prevents accidental permanent deletion
- **Use RBAC**: Limit who can delete secrets or Key Vault
- **Tag resources**: Include owner, environment, and purpose tags
- **Document secret purposes**: Use content-type and tags to document each secret

### 2. Backup Strategy

- **Daily backups**: Automated backups to Azure Storage
- **Encrypted backups**: Use GPG or Azure encryption
- **Test restoration**: Monthly restoration tests
- **Off-site backups**: Store backups in different region

### 3. Access Control

```bash
# Principle of least privilege

# Read-only access for applications
az role assignment create \
  --assignee honua-app-identity \
  --role "Key Vault Secrets User" \
  --scope /subscriptions/{sub-id}/resourceGroups/rg-honua-prod/providers/Microsoft.KeyVault/vaults/kv-honua-abc123

# Admin access for ops team
az role assignment create \
  --assignee ops-team@honua.io \
  --role "Key Vault Administrator" \
  --scope /subscriptions/{sub-id}/resourceGroups/rg-honua-prod/providers/Microsoft.KeyVault/vaults/kv-honua-abc123
```

### 4. Secret Rotation

```bash
# Regular secret rotation schedule
# - Database passwords: Every 90 days
# - API keys: Every 180 days
# - Certificates: 30 days before expiration
# - OAuth secrets: Every 365 days

# Automate rotation with Azure Functions
# See: /infrastructure/azure/functions/secret-rotation/
```

### 5. Disaster Recovery Planning

**Quarterly DR Test**:
1. Delete non-production Key Vault
2. Restore from backup
3. Verify all secrets restored
4. Test application connectivity
5. Document findings and improvements

### 6. Compliance and Auditing

- **Enable diagnostic logs**: Forward to Log Analytics
- **Audit access**: Review access logs weekly
- **Secret inventory**: Maintain inventory of all secrets
- **Compliance tags**: Tag secrets with compliance requirements (PCI, HIPAA, etc.)

---

## Emergency Contacts

| Role | Contact | Responsibility |
|------|---------|---------------|
| **Platform Lead** | platform-team@honua.io | Key Vault administration |
| **Security Team** | security@honua.io | Secret rotation and compliance |
| **On-Call Engineer** | oncall@honua.io | Emergency recovery |
| **Azure Support** | +1-800-xxx-xxxx | Microsoft support |

---

## Related Documentation

- [Azure Backup Policy](./AZURE_BACKUP_POLICY.md) - Complete backup strategy
- [Disaster Recovery Plan](./DISASTER_RECOVERY_PLAN.md) - Full DR procedures
- [Terraform Configuration](../../infrastructure/terraform/azure/main.tf) - Infrastructure as Code
- [Secret Management Guide](../security/SECRET_MANAGEMENT.md) - Secret handling best practices

---

## Appendix: Azure CLI Quick Reference

### List Deleted Resources

```bash
# List deleted Key Vaults
az keyvault list-deleted

# List deleted secrets in a vault
az keyvault secret list-deleted --vault-name kv-honua-abc123

# List deleted keys
az keyvault key list-deleted --vault-name kv-honua-abc123

# List deleted certificates
az keyvault certificate list-deleted --vault-name kv-honua-abc123
```

### Recovery Commands

```bash
# Recover Key Vault
az keyvault recover --name kv-honua-abc123

# Recover secret
az keyvault secret recover --vault-name kv-honua-abc123 --name SecretName

# Recover key
az keyvault key recover --vault-name kv-honua-abc123 --name KeyName

# Recover certificate
az keyvault certificate recover --vault-name kv-honua-abc123 --name CertName
```

### Purge Commands (Non-Production Only)

```bash
# WARNING: Purge is permanent and irreversible!

# Purge Key Vault (requires purge protection disabled)
az keyvault purge --name kv-honua-abc123 --location eastus

# Purge secret (requires purge protection disabled)
az keyvault secret purge --vault-name kv-honua-abc123 --name SecretName
```

---

**Document Version**: 1.0
**Last Updated**: 2025-10-18
**Next Review**: 2025-11-18
**Owner**: Platform Engineering Team
**Approved By**: CTO, Security Team
