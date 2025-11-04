# Azure Key Vault Configuration Summary

**Last Updated**: 2025-10-18
**Status**: Implemented
**Version**: 1.0

## Overview

This document summarizes the environment-conditional Key Vault configuration implemented for the Honua Azure infrastructure.

## Implementation Summary

### Changes Made

1. **Terraform Configuration** (`main.tf` line 159-190):
   - Implemented environment-conditional purge protection
   - Configured environment-based soft delete retention
   - Added comprehensive inline documentation
   - Enhanced resource tagging for tracking

2. **Recovery Documentation** (`docs/deployment/AZURE_KEY_VAULT_RECOVERY.md`):
   - Complete recovery procedures for all scenarios
   - Step-by-step guides for soft delete and purge operations
   - Automated backup and monitoring scripts
   - Emergency contacts and best practices

3. **Configuration Examples** (`terraform.tfvars.example`):
   - Detailed environment-specific configurations
   - Cost estimates by environment
   - Security best practices and warnings
   - Comprehensive deployment guidance

## Configuration by Environment

### Production

```hcl
soft_delete_retention_days  = 90    # Maximum protection
purge_protection_enabled    = true  # Irreversible - prevents deletion
```

**Characteristics**:
- Maximum security and compliance
- 90-day recovery window
- Cannot purge during retention period
- Protects against accidental/malicious deletion
- Suitable for: Production workloads, compliance requirements

**Cost**: $0 (first 10,000 operations free)

### Staging

```hcl
soft_delete_retention_days  = 7     # Minimum retention
purge_protection_enabled    = false # Allows cleanup
```

**Characteristics**:
- Balanced protection and flexibility
- 7-day recovery window
- Can purge for immediate cleanup
- Suitable for: Pre-production testing, staging environments

**Cost**: $0 (first 10,000 operations free)

### Development

```hcl
soft_delete_retention_days  = 7     # Minimum retention
purge_protection_enabled    = false # Allows rapid iteration
```

**Characteristics**:
- Rapid iteration and cleanup
- 7-day recovery window
- Immediate name reuse after purge
- Suitable for: Development, testing, experimentation

**Cost**: $0 (first 10,000 operations free)

## Key Differences

| Feature | Development | Staging | Production |
|---------|-------------|---------|------------|
| **Soft Delete Retention** | 7 days | 7 days | 90 days |
| **Purge Protection** | Disabled | Disabled | **Enabled** |
| **Recovery Window** | 7 days | 7 days | 90 days |
| **Can Purge** | Yes | Yes | **No** |
| **Name Reuse** | Immediate | Immediate | After 90 days |
| **Network Access** | Allow All | Allow All | Restricted* |

*Network restriction requires manual configuration after Terraform provisioning.

## Terraform Code Snippet

```hcl
resource "azurerm_key_vault" "main" {
  name                        = local.key_vault_name
  location                    = azurerm_resource_group.main.location
  resource_group_name         = azurerm_resource_group.main.name
  tenant_id                   = data.azurerm_client_config.current.tenant_id
  sku_name                    = "standard"

  # Soft delete configuration
  # - Production: 90 days (maximum protection)
  # - Non-production: 7 days (minimum required)
  soft_delete_retention_days  = var.environment == "prod" ? 90 : 7

  # Purge protection (prevents permanent deletion)
  # - Production: Enabled (compliance requirement)
  # - Non-production: Disabled (allows quick cleanup)
  purge_protection_enabled    = var.environment == "prod" ? true : false

  enable_rbac_authorization   = true

  network_acls {
    default_action = "Allow" # Restrict in prod to specific VNets
    bypass         = "AzureServices"
  }

  tags = merge(
    local.tags,
    {
      PurgeProtection = var.environment == "prod" ? "Enabled" : "Disabled"
      SoftDeleteDays  = var.environment == "prod" ? "90" : "7"
    }
  )
}
```

## Critical Warnings

### Purge Protection is Irreversible

Once enabled on a Key Vault (in production), purge protection **CANNOT be disabled**. This is by design for security and compliance.

**Implications**:
1. Deleted Key Vaults enter soft-deleted state for 90 days
2. Cannot purge (immediately delete) during this period
3. Cannot reuse the same name for a new Key Vault
4. Still billed for the Key Vault during soft delete
5. Automatic permanent deletion after 90 days

**Before Enabling in Production**:
- [ ] Test thoroughly in dev/staging
- [ ] Finalize Key Vault naming convention
- [ ] Document recovery procedures
- [ ] Train team on implications
- [ ] Budget for 90-day retention period

## Deployment Commands

### Initialize and Preview

```bash
# Initialize Terraform
cd infrastructure/terraform/azure
terraform init

# Validate configuration
terraform validate
terraform fmt -check

# Preview changes
terraform plan -var="environment=prod"
```

### Apply Configuration

```bash
# For Production (enables purge protection)
terraform apply -var="environment=prod" \
  -var="admin_email=ops@example.com"

# For Development (purge protection disabled)
terraform apply -var="environment=dev" \
  -var="admin_email=dev@example.com"
```

### Verify Deployment

```bash
# Check Key Vault configuration
az keyvault show \
  --name kv-honua-abc123 \
  --query "{name:name, softDelete:properties.enableSoftDelete, purgeProtection:properties.enablePurgeProtection, retention:properties.softDeleteRetentionInDays}" \
  --output table

# Expected output for Production:
# Name              SoftDelete    PurgeProtection    Retention
# kv-honua-abc123   True          True               90

# Expected output for Development:
# Name              SoftDelete    PurgeProtection    Retention
# kv-honua-abc123   True          False              7
```

## Recovery Procedures

### Scenario: Accidentally Deleted Secret

**Recovery Time**: < 5 minutes

```bash
# List soft-deleted secrets
az keyvault secret list-deleted --vault-name kv-honua-abc123

# Recover secret
az keyvault secret recover \
  --vault-name kv-honua-abc123 \
  --name PostgreSQL-ConnectionString
```

### Scenario: Key Vault Deleted

**Recovery Time**: < 15 minutes

```bash
# List soft-deleted Key Vaults
az keyvault list-deleted

# Recover Key Vault
az keyvault recover \
  --name kv-honua-abc123 \
  --location eastus \
  --resource-group rg-honua-prod-eastus
```

### Scenario: Key Vault Purged (Non-Production Only)

**Recovery Time**: < 1 hour

```bash
# Create new Key Vault
az keyvault create \
  --name kv-honua-restored-$(date +%s) \
  --resource-group rg-honua-dev-eastus \
  --location eastus

# Restore secrets from backup
# See: docs/deployment/AZURE_KEY_VAULT_RECOVERY.md
```

## Monitoring and Alerts

### Key Vault Health Monitoring

```bash
#!/bin/bash
# Quick health check script

VAULT_NAME="kv-honua-abc123"

# Check vault exists
az keyvault show --name "$VAULT_NAME" &>/dev/null || {
  echo "ERROR: Key Vault not found!"
  exit 1
}

# Check purge protection status
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
```

### Automated Alerts

Configured in Terraform (main.tf lines 585-610):
- Key Vault availability < 99.9%
- Secret access failures > 5/minute
- Unusual access patterns
- Backup failures

## Backup Strategy

### Automated Backups

**Schedule**: Daily at 3:00 AM UTC

**Retention**:
- Daily backups: 90 days
- Weekly backups: 1 year
- Monthly backups: 7 years

**Storage**: Geo-redundant Azure Blob Storage (production)

### Manual Backup

```bash
# Backup Key Vault secrets (metadata only)
az keyvault secret list --vault-name kv-honua-abc123 \
  --query "[].{name:name, enabled:attributes.enabled}" \
  --output json > keyvault-inventory.json

# Upload to backup storage
az storage blob upload \
  --container config-backups \
  --name "keyvault-inventory-$(date +%Y%m%d).json" \
  --file keyvault-inventory.json \
  --account-name stbkphonua123456
```

## Compliance

### Meets Requirements

- ✓ **GDPR**: Data protection and recovery capabilities
- ✓ **SOC 2**: Access control and audit logging
- ✓ **HIPAA**: Encryption at rest and in transit
- ✓ **PCI DSS**: Secure secret management

### Audit Trail

All Key Vault operations logged to:
- Azure Monitor (90 days retention)
- Log Analytics Workspace (30 days retention)
- Long-term storage (7 years for compliance)

## Best Practices

### 1. Secret Management

- [ ] Regular secret rotation (every 90 days)
- [ ] Use managed identities where possible
- [ ] Document purpose of each secret
- [ ] Tag secrets with compliance requirements

### 2. Access Control

- [ ] Principle of least privilege
- [ ] Use RBAC instead of access policies
- [ ] Review access permissions quarterly
- [ ] Enable MFA for admin operations

### 3. Disaster Recovery

- [ ] Monthly restore test
- [ ] Maintain offline backups
- [ ] Document recovery procedures
- [ ] Train team on DR process

### 4. Monitoring

- [ ] Review access logs weekly
- [ ] Monitor secret expiration
- [ ] Alert on unusual access patterns
- [ ] Track secret versioning

## Related Documentation

- **Recovery Procedures**: [docs/deployment/AZURE_KEY_VAULT_RECOVERY.md](../../../docs/deployment/AZURE_KEY_VAULT_RECOVERY.md)
- **Backup Policy**: [docs/deployment/AZURE_BACKUP_POLICY.md](../../../docs/deployment/AZURE_BACKUP_POLICY.md)
- **Configuration Examples**: [terraform.tfvars.example](./terraform.tfvars.example)
- **Main Terraform**: [main.tf](./main.tf)

## Support and Contacts

| Role | Contact | Responsibility |
|------|---------|---------------|
| **Platform Team** | platform@honua.io | Key Vault administration |
| **Security Team** | security@honua.io | Secret rotation and compliance |
| **On-Call** | oncall@honua.io | Emergency recovery |

## Troubleshooting

### Issue: Cannot Delete Key Vault

**Cause**: Purge protection enabled (production)

**Solution**: Key Vault will enter soft-deleted state and auto-delete after 90 days. To recover:
```bash
az keyvault recover --name kv-honua-abc123
```

### Issue: Cannot Reuse Key Vault Name

**Cause**: Key Vault in soft-deleted state

**Solution**:
- Production: Wait 90 days for automatic cleanup
- Dev/Staging: Purge manually
```bash
az keyvault purge --name kv-honua-abc123 --location eastus
```

### Issue: Secret Access Denied

**Cause**: Missing RBAC permissions

**Solution**: Grant appropriate role
```bash
az role assignment create \
  --assignee user@example.com \
  --role "Key Vault Secrets User" \
  --scope /subscriptions/{sub}/resourceGroups/{rg}/providers/Microsoft.KeyVault/vaults/{vault}
```

## Changelog

### Version 1.0 (2025-10-18)

**Initial Implementation**:
- Environment-conditional purge protection
- 90-day retention for production
- 7-day retention for dev/staging
- Comprehensive recovery documentation
- Configuration examples and warnings

---

**Document Owner**: Platform Engineering Team
**Last Review**: 2025-10-18
**Next Review**: 2025-11-18
