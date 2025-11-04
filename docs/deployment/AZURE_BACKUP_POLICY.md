# Azure Database Backup Policy

**Last Updated**: 2025-10-18
**Status**: Production Ready
**Version**: 1.0

## Table of Contents

1. [Overview](#overview)
2. [Backup Strategy](#backup-strategy)
3. [Backup Retention Policy](#backup-retention-policy)
4. [Geo-Redundant Backup Configuration](#geo-redundant-backup-configuration)
5. [Point-in-Time Recovery (PITR)](#point-in-time-recovery-pitr)
6. [Backup Verification](#backup-verification)
7. [Monitoring and Alerts](#monitoring-and-alerts)
8. [Compliance and Security](#compliance-and-security)

---

## Overview

This document defines the backup and disaster recovery policy for Honua's Azure PostgreSQL databases. The policy ensures:

- **Data Protection**: Multiple backup copies in different geographic regions
- **Business Continuity**: Rapid recovery from any data loss scenario
- **Compliance**: Meet regulatory requirements for data retention
- **Cost Optimization**: Balance protection with storage costs

### Key Metrics

| Metric | Development | Staging | Production |
|--------|-------------|---------|------------|
| **RPO** (Recovery Point Objective) | < 1 hour | < 15 minutes | < 5 minutes |
| **RTO** (Recovery Time Objective) | < 4 hours | < 2 hours | < 1 hour |
| **Backup Retention** | 7 days | 14 days | 35 days |
| **Geo-Redundancy** | No | No | Yes |
| **PITR Window** | 7 days | 14 days | 35 days |
| **Long-term Retention** | 1 year | 1 year | 7 years |

---

## Backup Strategy

### Automatic Backups

Azure Database for PostgreSQL Flexible Server provides automated backups:

#### Backup Schedule

| Component | Frequency | Type | Storage Location |
|-----------|-----------|------|------------------|
| **Full Backup** | Weekly | Physical | Azure Backup Storage (ZRS) |
| **Differential Backup** | Daily | Physical | Azure Backup Storage (ZRS) |
| **Transaction Logs** | Every 5 minutes | Logical | Azure Backup Storage (ZRS) |
| **Application Snapshots** | Daily | Logical | Geo-redundant Storage (GRS) |

#### Backup Components

1. **Database Files**
   - PostgreSQL data files (`base/`)
   - Write-Ahead Log (WAL) files
   - Configuration files (`postgresql.conf`, `pg_hba.conf`)

2. **Application Data**
   - Collection metadata (JSON/YAML)
   - User attachments
   - Configuration secrets (encrypted)
   - Application logs (retention: 90 days)

### Manual Backups

Manual backups should be taken:

- **Before Major Deployments**: Create snapshot before infrastructure changes
- **Before Schema Migrations**: Backup before altering database schema
- **On Demand**: For compliance, audits, or special requirements
- **Before Restore Operations**: Backup current state before restoring

**Command to trigger manual backup:**

```bash
# Create on-demand backup
az postgres flexible-server backup create \
  --resource-group rg-honua-prod-eastus \
  --name postgres-honua-prod \
  --backup-name manual-$(date +%Y%m%d-%H%M%S)
```

---

## Backup Retention Policy

### Short-term Retention (Automated)

Azure PostgreSQL Flexible Server automatically retains backups:

```hcl
# From Terraform configuration
backup_retention_days = {
  production = 35  # Maximum allowed
  staging    = 14
  development = 7  # Minimum recommended
}
```

**Retention Schedule:**

| Environment | Retention Period | Recovery Window | Cost Impact |
|-------------|------------------|-----------------|-------------|
| **Production** | 35 days | Any point in last 35 days | ~$50/month |
| **Staging** | 14 days | Any point in last 14 days | ~$20/month |
| **Development** | 7 days | Any point in last 7 days | ~$10/month |

### Long-term Retention (Manual)

For compliance and disaster recovery, backups are exported to Azure Blob Storage:

```hcl
# Lifecycle policy from Terraform
storage_lifecycle = {
  hot_tier    = "0-30 days"     # Immediate access
  cool_tier   = "30-90 days"    # Lower cost, still accessible
  archive_tier = "90-2555 days" # Lowest cost, restore delay
}
```

**Long-term Backup Process:**

1. **Daily Export** (2:00 AM UTC):
   ```bash
   # Automated via Azure Function
   - Export database snapshot to Blob Storage
   - Compress and encrypt backup
   - Verify backup integrity
   - Update backup catalog
   ```

2. **Storage Tiers**:
   - **Hot**: First 30 days (instant access)
   - **Cool**: 30-90 days (access within minutes)
   - **Archive**: 90 days+ (access within 15 hours)

3. **Retention Rules**:
   - **Daily backups**: Keep for 90 days
   - **Weekly backups** (Sunday): Keep for 1 year
   - **Monthly backups** (1st of month): Keep for 7 years
   - **Yearly backups** (January 1st): Keep for 10 years

### Retention Cost Analysis

| Backup Type | Frequency | Retention | Storage (GB/year) | Cost/year |
|-------------|-----------|-----------|-------------------|-----------|
| Daily | 365 | 90 days | 90 × 10 GB = 900 GB | $18 (Cool) |
| Weekly | 52 | 1 year | 52 × 10 GB = 520 GB | $4 (Archive) |
| Monthly | 12 | 7 years | 84 × 10 GB = 840 GB | $2 (Archive) |
| Yearly | 1 | 10 years | 10 × 10 GB = 100 GB | $0.20 (Archive) |
| **Total** | - | - | **~2.4 TB** | **~$24/year** |

---

## Geo-Redundant Backup Configuration

### Geographic Distribution

For production environments, backups are replicated across Azure regions:

```
Primary Region: East US
├── Local Backups: Zone-Redundant Storage (ZRS)
│   ├── Available in: East US Zone 1, 2, 3
│   └── Protection: Datacenter failure
└── Geo-Redundant Backups: Geo-Redundant Storage (GRS)
    ├── Replicated to: West US 2 (paired region)
    ├── Replication lag: < 15 minutes
    └── Protection: Regional disaster
```

### Terraform Configuration

```hcl
resource "azurerm_postgresql_flexible_server" "main" {
  # Geo-redundant backups for production only
  geo_redundant_backup_enabled = var.environment == "prod" ? true : false

  # High availability configuration
  high_availability {
    mode                      = "ZoneRedundant"
    standby_availability_zone = "2"
  }
}

resource "azurerm_storage_account" "backups" {
  # Geo-redundant storage for long-term backups
  account_replication_type = var.environment == "prod" ? "GRS" : "LRS"

  # Read access to geo-replicated data
  # Enables reads from secondary region during outage
  enable_https_traffic_only = true
}
```

### Failover Scenarios

| Scenario | Recovery Method | RTO | RPO | Data Loss |
|----------|----------------|-----|-----|-----------|
| **Database Corruption** | Restore from local backup | < 30 min | < 5 min | Minimal |
| **Zone Failure** | Automatic HA failover | < 2 min | 0 | None |
| **Region Failure** | Restore from geo-backup | < 1 hour | < 15 min | Minimal |
| **Complete Azure Outage** | Restore to alternative cloud | < 4 hours | < 1 hour | Acceptable |

---

## Point-in-Time Recovery (PITR)

### PITR Capabilities

Azure PostgreSQL Flexible Server provides continuous PITR:

```
Backup Timeline:
├── Full Backup (Sunday 2:00 AM)
├── Differential Backup (Monday 2:00 AM)
├── Differential Backup (Tuesday 2:00 AM)
│   ...
└── Transaction Logs (continuous, every 5 minutes)
    └── Allows restore to ANY second within retention window
```

**Example Recovery Points:**

- **Specific timestamp**: `2025-10-18 14:30:45`
- **Before incident**: `2025-10-18 14:00:00` (30 mins before issue)
- **Latest possible**: Current time minus 5 minutes

### PITR Use Cases

1. **Accidental Data Deletion**
   ```bash
   # User deleted critical records at 2:30 PM
   # Restore database to 2:25 PM (before deletion)
   az postgres flexible-server restore \
     --resource-group rg-honua-prod-eastus \
     --name postgres-honua-prod \
     --source-server postgres-honua-prod \
     --restore-time "2025-10-18T14:25:00Z" \
     --target-server postgres-honua-restored
   ```

2. **Bad Migration Rollback**
   ```bash
   # Migration applied at 10:00 AM caused issues
   # Restore to 9:55 AM (before migration)
   az postgres flexible-server restore \
     --restore-time "2025-10-18T09:55:00Z"
   ```

3. **Ransomware Recovery**
   ```bash
   # Encryption detected at 8:00 AM
   # Restore to last known good time (7:45 AM)
   az postgres flexible-server restore \
     --restore-time "2025-10-18T07:45:00Z"
   ```

### PITR Limitations

| Limitation | Impact | Mitigation |
|------------|--------|------------|
| **Retention Window** | Can only restore within 35 days | Export older backups to Blob Storage |
| **Recovery Time** | Restore takes 15-60 minutes | Use HA for immediate failover |
| **No Selective Restore** | Must restore entire database | Export specific tables post-restore |
| **Lag During Restore** | 5-15 minute lag on transaction logs | Plan for acceptable data loss |

---

## Backup Verification

### Automated Verification

**Daily Verification Process** (3:00 AM UTC):

```bash
#!/bin/bash
# Automated backup verification script
# Runs daily via Azure Function

# 1. Check backup existence
LATEST_BACKUP=$(az postgres flexible-server backup list \
  --resource-group rg-honua-prod-eastus \
  --server-name postgres-honua-prod \
  --query '[0].name' -o tsv)

if [ -z "$LATEST_BACKUP" ]; then
  echo "ERROR: No backups found!"
  send_alert "backup-failure" "No backups available"
  exit 1
fi

# 2. Check backup age (should be < 24 hours)
BACKUP_TIME=$(az postgres flexible-server backup show \
  --name "$LATEST_BACKUP" \
  --resource-group rg-honua-prod-eastus \
  --server-name postgres-honua-prod \
  --query 'backupStartTime' -o tsv)

BACKUP_AGE_HOURS=$(( ($(date +%s) - $(date -d "$BACKUP_TIME" +%s)) / 3600 ))

if [ "$BACKUP_AGE_HOURS" -gt 24 ]; then
  echo "ERROR: Latest backup is $BACKUP_AGE_HOURS hours old!"
  send_alert "backup-stale" "Backup is $BACKUP_AGE_HOURS hours old"
  exit 1
fi

# 3. Verify backup size (should be > 100 MB)
BACKUP_SIZE=$(az storage blob show \
  --container database-backups \
  --name "postgres-$LATEST_BACKUP.dump" \
  --account-name stbkphonua123456 \
  --query 'properties.contentLength' -o tsv)

MIN_SIZE=$((100 * 1024 * 1024))  # 100 MB
if [ "$BACKUP_SIZE" -lt "$MIN_SIZE" ]; then
  echo "ERROR: Backup size is suspiciously small: $BACKUP_SIZE bytes"
  send_alert "backup-size-anomaly" "Backup size: $BACKUP_SIZE bytes"
  exit 1
fi

# 4. Verify geo-replication (for production)
if [ "$ENVIRONMENT" == "prod" ]; then
  GEO_STATUS=$(az postgres flexible-server show \
    --resource-group rg-honua-prod-eastus \
    --name postgres-honua-prod \
    --query 'backup.geoRedundantBackup' -o tsv)

  if [ "$GEO_STATUS" != "Enabled" ]; then
    echo "ERROR: Geo-redundant backup not enabled!"
    send_alert "geo-backup-disabled" "Geo-redundancy is not enabled"
    exit 1
  fi
fi

echo "✓ Backup verification passed"
send_metric "backup_verification_success" 1
```

### Monthly Restore Test

**Test Restore Procedure** (1st Sunday of month, 4:00 AM UTC):

```bash
#!/bin/bash
# Monthly restore test
# Verifies backups can be successfully restored

TEST_SERVER="postgres-honua-test-restore-$(date +%Y%m)"

echo "=== Monthly Backup Restore Test ==="
echo "Test Server: $TEST_SERVER"
echo "Source Server: postgres-honua-prod"
echo "Test Date: $(date)"

# 1. Create test restore from latest backup
echo "Creating test restore..."
az postgres flexible-server restore \
  --resource-group rg-honua-prod-eastus \
  --name postgres-honua-prod \
  --source-server postgres-honua-prod \
  --restore-time "$(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%SZ)" \
  --target-server "$TEST_SERVER"

# 2. Wait for restore completion
echo "Waiting for restore to complete..."
az postgres flexible-server wait \
  --name "$TEST_SERVER" \
  --resource-group rg-honua-prod-eastus \
  --exists \
  --timeout 1800

# 3. Verify restored database
echo "Verifying restored database..."
RESTORED_ENDPOINT=$(az postgres flexible-server show \
  --name "$TEST_SERVER" \
  --resource-group rg-honua-prod-eastus \
  --query 'fullyQualifiedDomainName' -o tsv)

# Connect and verify data
psql "host=$RESTORED_ENDPOINT user=honuaadmin dbname=honua sslmode=require" <<EOF
-- Check table count
SELECT COUNT(*) as table_count FROM information_schema.tables
WHERE table_schema = 'public';

-- Check row count
SELECT
  schemaname,
  tablename,
  n_live_tup as row_count
FROM pg_stat_user_tables
ORDER BY n_live_tup DESC
LIMIT 10;

-- Check PostGIS extension
SELECT PostGIS_Version();

-- Sample data verification
SELECT COUNT(*) as collection_count FROM collections;
SELECT COUNT(*) as feature_count FROM features;
EOF

VERIFY_STATUS=$?

# 4. Generate test report
cat > "/tmp/restore-test-$(date +%Y%m).txt" <<EOF
Backup Restore Test Report
==========================
Date: $(date)
Test Server: $TEST_SERVER
Source Server: postgres-honua-prod
Restore Point: $(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%SZ)

Test Results:
- Restore Time: $RESTORE_DURATION seconds
- Verification: $([ $VERIFY_STATUS -eq 0 ] && echo "PASSED" || echo "FAILED")
- Database Size: $(psql "host=$RESTORED_ENDPOINT user=honuaadmin dbname=honua sslmode=require" -t -c "SELECT pg_size_pretty(pg_database_size('honua'));" | xargs)
- Tables Restored: $(psql "host=$RESTORED_ENDPOINT user=honuaadmin dbname=honua sslmode=require" -t -c "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';" | xargs)

Status: $([ $VERIFY_STATUS -eq 0 ] && echo "✓ SUCCESS" || echo "✗ FAILED")
EOF

# 5. Upload report to storage
az storage blob upload \
  --container database-backups \
  --file "/tmp/restore-test-$(date +%Y%m).txt" \
  --name "restore-tests/report-$(date +%Y%m).txt" \
  --account-name stbkphonua123456

# 6. Cleanup test server (after 24 hours)
echo "Scheduling cleanup of test server..."
az postgres flexible-server delete \
  --name "$TEST_SERVER" \
  --resource-group rg-honua-prod-eastus \
  --yes

echo "=== Restore Test Complete ==="
[ $VERIFY_STATUS -eq 0 ] && exit 0 || exit 1
```

---

## Monitoring and Alerts

### Azure Monitor Metrics

**Key Metrics to Monitor:**

| Metric | Threshold | Alert Severity | Action |
|--------|-----------|----------------|--------|
| `backup_storage_used` | < 1 MB | Critical | Investigate backup failure |
| `backup_storage_used` | > 400 GB | Warning | Plan storage expansion |
| `oldest_backup_age` | > 25 hours | Critical | Check backup job status |
| `geo_replication_lag` | > 30 minutes | Warning | Review replication health |
| `storage_percent` | > 80% | Warning | Scale up database storage |
| `failed_connections` | > 10/min | Warning | Check firewall/networking |

### Alert Configuration

Configured via Terraform in `/infrastructure/terraform/azure/main.tf`:

```hcl
# Email alerts for backup issues
resource "azurerm_monitor_action_group" "backup_alerts" {
  name                = "backup-alerts-prod"
  resource_group_name = azurerm_resource_group.main.name
  short_name          = "bkpalerts"

  email_receiver {
    name          = "admin"
    email_address = var.admin_email
  }

  # Optional: Add SMS or webhook receivers
  # sms_receiver {
  #   name         = "oncall"
  #   country_code = "1"
  #   phone_number = "5555555555"
  # }
}
```

### Monitoring Dashboard

**Grafana Dashboard**: `honua-backup-monitoring`

**Panels:**
1. **Backup Health**
   - Last successful backup time
   - Backup size trend (30 days)
   - Backup duration
   - Geo-replication status

2. **Storage Utilization**
   - Database storage usage
   - Backup storage usage
   - Storage growth rate
   - Projected capacity

3. **Recovery Metrics**
   - PITR window availability
   - Last restore test date
   - Restore test success rate
   - Average restore time

4. **Alerts**
   - Active alerts
   - Alert history (7 days)
   - Mean time to resolution

---

## Compliance and Security

### Regulatory Compliance

| Requirement | Implementation | Evidence |
|-------------|----------------|----------|
| **GDPR** | 30-day retention minimum | Automated backups with 35-day retention |
| **SOC 2** | Backup verification | Daily verification + monthly restore tests |
| **HIPAA** | Encrypted backups | Azure Storage encryption at rest (AES-256) |
| **PCI DSS** | Backup access control | RBAC + Key Vault for credentials |

### Backup Encryption

**Encryption at Rest:**
- Azure Database: Transparent Data Encryption (TDE) enabled
- Blob Storage: Azure Storage Service Encryption (SSE) with Microsoft-managed keys
- Key Management: Azure Key Vault (purge protection enabled in prod)

**Encryption in Transit:**
- Database connections: SSL/TLS 1.2+ enforced
- Azure replication: Encrypted by default
- Backup exports: HTTPS only

### Access Control

**Role-Based Access Control (RBAC):**

```bash
# Backup Operator Role (can create/restore backups)
az role assignment create \
  --assignee backup-operator@honua.io \
  --role "Backup Operator" \
  --scope /subscriptions/{subscription-id}/resourceGroups/rg-honua-prod-eastus

# Storage Blob Data Contributor (can read/write backup blobs)
az role assignment create \
  --assignee backup-operator@honua.io \
  --role "Storage Blob Data Contributor" \
  --scope /subscriptions/{subscription-id}/resourceGroups/rg-honua-prod-eastus/providers/Microsoft.Storage/storageAccounts/stbkphonua123456
```

**Access Audit:**
- All backup access logged to Azure Monitor
- Retention: 90 days (standard), 2 years (compliance logs)
- Alerts on unauthorized access attempts

### Data Retention Policy

**Production Environment:**

| Data Type | Retention | Justification |
|-----------|-----------|---------------|
| **Transaction Logs** | 35 days | PITR window |
| **Daily Backups** | 90 days | Operational needs |
| **Weekly Backups** | 1 year | Compliance requirement |
| **Monthly Backups** | 7 years | Legal/audit requirement |
| **Yearly Backups** | 10 years | Regulatory requirement |

**Data Deletion:**

When retention period expires:
1. Backup marked for deletion (soft delete: 30 days)
2. Notification sent to DPO (Data Protection Officer)
3. After soft delete period: Permanent deletion
4. Deletion audit log retained for 7 years

---

## Backup Schedule Summary

### Daily Operations

```
00:00 - Transaction log backup (continuous)
02:00 - Full database backup (automated)
02:30 - Export backup to Blob Storage (long-term)
03:00 - Backup verification script
03:30 - Generate backup report
04:00 - Cleanup old transaction logs (> 35 days)
```

### Weekly Operations

```
Sunday 02:00 - Full backup tagged as "weekly"
Sunday 03:00 - Verify geo-replication status
Sunday 04:00 - Backup catalog update
Sunday 05:00 - Generate weekly backup report
```

### Monthly Operations

```
1st Sunday 02:00 - Full backup tagged as "monthly"
1st Sunday 04:00 - Restore test (create test server)
1st Sunday 06:00 - Validate restored data
1st Sunday 07:00 - Generate restore test report
1st Sunday 08:00 - Cleanup test server
```

### Yearly Operations

```
January 1st 02:00 - Full backup tagged as "yearly"
January 1st 03:00 - Compliance audit report
January 1st 04:00 - Review backup policy
January 1st 05:00 - Capacity planning review
```

---

## Related Documentation

- [Restore Procedures Runbook](./AZURE_RESTORE_PROCEDURES.md) - Step-by-step restore instructions
- [Backup Automation Scripts](../../scripts/azure/backups/) - Automation scripts
- [Disaster Recovery Plan](./DISASTER_RECOVERY_PLAN.md) - Complete DR procedures
- [Terraform Configuration](../../infrastructure/terraform/azure/main.tf) - Infrastructure as Code

---

**Document Version**: 1.0
**Last Updated**: 2025-10-18
**Next Review**: 2025-11-18
**Owner**: Platform Engineering Team
**Approved By**: CTO, DPO
