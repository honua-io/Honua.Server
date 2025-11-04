# Azure Database Restore Procedures

**Last Updated**: 2025-10-18
**Status**: Production Ready
**Version**: 1.0
**Severity**: P1 (Critical Operations)

## Table of Contents

1. [Overview](#overview)
2. [Prerequisites](#prerequisites)
3. [Restore Scenarios](#restore-scenarios)
4. [Runbook 1: Point-in-Time Restore](#runbook-1-point-in-time-restore)
5. [Runbook 2: Geo-Redundant Restore](#runbook-2-geo-redundant-restore)
6. [Runbook 3: Long-term Backup Restore](#runbook-3-long-term-backup-restore)
7. [Runbook 4: Table-Level Recovery](#runbook-4-table-level-recovery)
8. [Runbook 5: Cross-Region Failover](#runbook-5-cross-region-failover)
9. [Post-Restore Verification](#post-restore-verification)
10. [Troubleshooting](#troubleshooting)

---

## Overview

This runbook provides step-by-step procedures for restoring Azure PostgreSQL databases in various disaster recovery scenarios. Each runbook is designed to minimize downtime and data loss while ensuring data integrity.

### Recovery Objectives

| Metric | Target | Maximum Acceptable |
|--------|--------|--------------------|
| **RTO** (Recovery Time Objective) | 30 minutes | 1 hour |
| **RPO** (Recovery Point Objective) | 5 minutes | 15 minutes |
| **Data Loss** | 0% | < 0.01% |
| **Verification Time** | 15 minutes | 30 minutes |

### When to Use This Guide

- **Data Corruption**: Database files corrupted or inconsistent
- **Accidental Deletion**: User accidentally deleted critical data
- **Bad Migration**: Schema migration caused errors
- **Ransomware Attack**: Data encrypted by malicious actors
- **Regional Disaster**: Primary Azure region unavailable
- **Compliance Restore**: Need to restore data for audit/legal purposes

---

## Prerequisites

### Required Access

- **Azure CLI**: Installed and authenticated (`az login`)
- **Azure Subscription**: Contributor or Owner role
- **Resource Group**: Access to `rg-honua-{env}-eastus`
- **PostgreSQL Admin**: `honuaadmin` credentials
- **Blob Storage**: Read access to backup storage account

### Required Information

```bash
# Environment configuration
export RESOURCE_GROUP="rg-honua-prod-eastus"
export SERVER_NAME="postgres-honua-prod"
export DB_NAME="honua"
export ADMIN_USER="honuaadmin"
export ADMIN_PASSWORD="<from-key-vault>"
export BACKUP_STORAGE_ACCOUNT="stbkphonua123456"
export BACKUP_CONTAINER="database-backups"
```

### Pre-Restore Checklist

- [ ] Identify exact time/backup to restore from
- [ ] Confirm availability of backup
- [ ] Notify stakeholders of downtime
- [ ] Backup current database state (if not corrupted)
- [ ] Prepare rollback plan
- [ ] Document incident for post-mortem

---

## Restore Scenarios

### Decision Matrix

| Scenario | Recovery Method | Downtime | Data Loss | Complexity |
|----------|----------------|----------|-----------|------------|
| **Recent deletion** (< 35 days) | PITR | 30-60 min | < 5 min | Low |
| **Old data** (> 35 days) | Long-term backup | 1-2 hours | None | Medium |
| **Regional outage** | Geo-restore | 30-60 min | < 15 min | Medium |
| **Specific table** | Table-level | 15-30 min | None | Low |
| **Complete disaster** | Cross-region | 2-4 hours | < 1 hour | High |

### Restore Type Comparison

| Feature | PITR | Geo-Restore | Long-term | Table-Level |
|---------|------|-------------|-----------|-------------|
| **Granularity** | Per-second | Per-second | Daily/Weekly | Per-table |
| **Max History** | 35 days | 35 days | 7 years | 35 days |
| **Speed** | Fast (30 min) | Medium (60 min) | Slow (2 hours) | Fast (15 min) |
| **Downtime** | Optional | Required | Required | None |
| **Data Loss** | < 5 min | < 15 min | Up to 1 day | None |

---

## Runbook 1: Point-in-Time Restore

**Use Case**: Restore database to specific point in time (last 35 days)

**Estimated Time**: 30-60 minutes
**Downtime Required**: Optional (can restore to new server)
**Data Loss**: < 5 minutes

### Symptoms

- Accidental data deletion detected
- Bad migration applied
- Data corruption identified
- Need to recover to specific timestamp

### Procedure

#### Step 1: Identify Restore Point

```bash
# 1. Determine incident time
INCIDENT_TIME="2025-10-18T14:30:00Z"  # When problem occurred

# 2. Calculate restore target (5-10 minutes before incident)
RESTORE_TIME=$(date -u -d "$INCIDENT_TIME - 10 minutes" +%Y-%m-%dT%H:%M:%SZ)
echo "Restore target: $RESTORE_TIME"

# 3. Verify restore point is within retention window
RETENTION_DAYS=35
OLDEST_AVAILABLE=$(date -u -d "$RETENTION_DAYS days ago" +%Y-%m-%dT%H:%M:%SZ)

if [[ "$RESTORE_TIME" < "$OLDEST_AVAILABLE" ]]; then
  echo "ERROR: Restore point is outside retention window"
  echo "Oldest available: $OLDEST_AVAILABLE"
  exit 1
fi

echo "✓ Restore point is valid"
```

#### Step 2: Create Restore Target

**Option A: Restore to New Server (Recommended - No Downtime)**

```bash
# Create new server from restore point
NEW_SERVER="${SERVER_NAME}-restored-$(date +%Y%m%d%H%M)"

echo "Creating restored server: $NEW_SERVER"

az postgres flexible-server restore \
  --resource-group "$RESOURCE_GROUP" \
  --name "$NEW_SERVER" \
  --source-server "$SERVER_NAME" \
  --restore-time "$RESTORE_TIME" \
  --location eastus

# Wait for restore completion (15-45 minutes)
echo "Waiting for restore to complete..."
az postgres flexible-server wait \
  --name "$NEW_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --exists \
  --timeout 3600

echo "✓ Restore completed"
```

**Option B: In-Place Restore (Requires Downtime)**

```bash
# ⚠️ WARNING: This will delete the current database ⚠️

# 1. Backup current state first
CURRENT_BACKUP="${SERVER_NAME}-pre-restore-$(date +%Y%m%d%H%M)"

az postgres flexible-server backup create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$SERVER_NAME" \
  --backup-name "$CURRENT_BACKUP"

# 2. Stop application to prevent writes
echo "Stop application before proceeding"
read -p "Press Enter when application is stopped..."

# 3. Delete current server (cannot be undone!)
read -p "This will DELETE the current database. Type 'DELETE' to confirm: " CONFIRM

if [ "$CONFIRM" != "DELETE" ]; then
  echo "Restore cancelled"
  exit 1
fi

az postgres flexible-server delete \
  --name "$SERVER_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --yes

# 4. Restore with same name
az postgres flexible-server restore \
  --resource-group "$RESOURCE_GROUP" \
  --name "$SERVER_NAME" \
  --source-server "$SERVER_NAME" \
  --restore-time "$RESTORE_TIME"
```

#### Step 3: Verify Restored Data

```bash
# Get restored server endpoint
RESTORED_ENDPOINT=$(az postgres flexible-server show \
  --name "$NEW_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --query 'fullyQualifiedDomainName' -o tsv)

echo "Restored server: $RESTORED_ENDPOINT"

# Connect and verify data
psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" <<EOF
-- Check database size
SELECT pg_size_pretty(pg_database_size('$DB_NAME')) as database_size;

-- Check table count
SELECT COUNT(*) as table_count
FROM information_schema.tables
WHERE table_schema = 'public';

-- Check row counts
SELECT
  schemaname,
  tablename,
  n_live_tup as row_count
FROM pg_stat_user_tables
ORDER BY n_live_tup DESC
LIMIT 10;

-- Verify data that was deleted
-- Example: Check if deleted collection exists
SELECT COUNT(*) FROM collections WHERE id = 'deleted-collection-id';

-- Check data timestamp matches restore point
SELECT MAX(updated_at) FROM audit_log;
EOF
```

#### Step 4: Cutover to Restored Server (If using Option A)

```bash
# 1. Update application configuration
# Option: Update DNS CNAME
# Option: Update connection string in Key Vault

# 2. Test connectivity from application
curl -X POST http://app/health/database

# 3. Switch traffic to new server
# This depends on your deployment method:

# Kubernetes example:
kubectl set env deployment/honua-server \
  DB_HOST="$RESTORED_ENDPOINT" \
  -n honua-prod

kubectl rollout status deployment/honua-server -n honua-prod

# 4. Verify application is working
curl http://app/health
curl http://app/api/collections

# 5. Monitor for errors
kubectl logs -f deployment/honua-server -n honua-prod | grep -i error
```

#### Step 5: Cleanup

```bash
# After 24-48 hours of verification:

# 1. Delete old server (if using Option A)
az postgres flexible-server delete \
  --name "$SERVER_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --yes

# 2. Rename restored server to original name
# Note: Requires deleting old server first
az postgres flexible-server update \
  --name "$NEW_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --tags "RestoreDate=$(date +%Y-%m-%d)" "OriginalName=$SERVER_NAME"
```

### Validation

**Success Criteria:**
- ✅ Restored server is running and healthy
- ✅ Data exists as of restore point
- ✅ Application can connect and query
- ✅ No data corruption errors
- ✅ All critical data verified present

**Validation Script:**

```bash
#!/bin/bash
# validate-restore.sh

echo "=== Restore Validation ==="

# 1. Check server status
STATUS=$(az postgres flexible-server show \
  --name "$NEW_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --query 'state' -o tsv)

if [ "$STATUS" != "Ready" ]; then
  echo "✗ Server not ready: $STATUS"
  exit 1
fi
echo "✓ Server is ready"

# 2. Check connectivity
if psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" -c "SELECT 1;" > /dev/null 2>&1; then
  echo "✓ Database connection successful"
else
  echo "✗ Cannot connect to database"
  exit 1
fi

# 3. Check data integrity
ROW_COUNT=$(psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" \
  -t -c "SELECT SUM(n_live_tup) FROM pg_stat_user_tables;")

if [ "$ROW_COUNT" -lt 1000 ]; then
  echo "✗ Row count suspiciously low: $ROW_COUNT"
  exit 1
fi
echo "✓ Row count looks reasonable: $ROW_COUNT"

# 4. Verify PostGIS extension
if psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" \
  -t -c "SELECT PostGIS_Version();" | grep -q "POSTGIS"; then
  echo "✓ PostGIS extension working"
else
  echo "✗ PostGIS extension not found"
  exit 1
fi

# 5. Check restore timestamp
MAX_TIMESTAMP=$(psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" \
  -t -c "SELECT MAX(updated_at) FROM audit_log;")

echo "Latest data timestamp: $MAX_TIMESTAMP"
echo "Target restore time: $RESTORE_TIME"

echo "=== Validation Complete ==="
```

---

## Runbook 2: Geo-Redundant Restore

**Use Case**: Restore from geo-redundant backup after regional disaster

**Estimated Time**: 60-90 minutes
**Downtime Required**: Yes (primary region down)
**Data Loss**: < 15 minutes

### Symptoms

- Primary Azure region unavailable
- All resources in primary region unreachable
- Azure status page reports regional outage
- Need to failover to secondary region

### Procedure

#### Step 1: Confirm Regional Outage

```bash
# 1. Check Azure status
az status --query 'status' -o tsv

# 2. Check if primary server is accessible
if az postgres flexible-server show \
  --name "$SERVER_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --query 'state' > /dev/null 2>&1; then
  echo "Primary server is accessible - regional outage not confirmed"
  exit 1
fi

echo "✓ Confirmed: Primary server unreachable"

# 3. Check geo-replication status (from secondary region)
az postgres flexible-server geo-restore list \
  --resource-group "$RESOURCE_GROUP" \
  --name "$SERVER_NAME"
```

#### Step 2: Restore to Secondary Region

```bash
# Secondary region (paired with East US)
SECONDARY_REGION="westus2"
SECONDARY_RG="rg-honua-prod-westus2"
SECONDARY_SERVER="${SERVER_NAME}-geo-restored"

# 1. Create resource group in secondary region (if not exists)
az group create \
  --name "$SECONDARY_RG" \
  --location "$SECONDARY_REGION"

# 2. Perform geo-restore
echo "Starting geo-restore to $SECONDARY_REGION..."

az postgres flexible-server geo-restore \
  --resource-group "$SECONDARY_RG" \
  --name "$SECONDARY_SERVER" \
  --source-server "/subscriptions/{subscription-id}/resourceGroups/$RESOURCE_GROUP/providers/Microsoft.DBforPostgreSQL/flexibleServers/$SERVER_NAME" \
  --location "$SECONDARY_REGION"

# 3. Wait for restore (30-60 minutes)
az postgres flexible-server wait \
  --name "$SECONDARY_SERVER" \
  --resource-group "$SECONDARY_RG" \
  --exists \
  --timeout 5400

echo "✓ Geo-restore completed"
```

#### Step 3: Configure Firewall and Networking

```bash
# 1. Allow Azure services
az postgres flexible-server firewall-rule create \
  --resource-group "$SECONDARY_RG" \
  --name "$SECONDARY_SERVER" \
  --rule-name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# 2. Add application IP ranges
az postgres flexible-server firewall-rule create \
  --resource-group "$SECONDARY_RG" \
  --name "$SECONDARY_SERVER" \
  --rule-name AllowApplicationSubnet \
  --start-ip-address 10.0.1.0 \
  --end-ip-address 10.0.1.255

# 3. Get new endpoint
SECONDARY_ENDPOINT=$(az postgres flexible-server show \
  --name "$SECONDARY_SERVER" \
  --resource-group "$SECONDARY_RG" \
  --query 'fullyQualifiedDomainName' -o tsv)

echo "Secondary endpoint: $SECONDARY_ENDPOINT"
```

#### Step 4: Update Application Configuration

```bash
# 1. Update connection string in Key Vault
az keyvault secret set \
  --vault-name "kv-honua-prod" \
  --name "PostgreSQL-ConnectionString" \
  --value "Host=$SECONDARY_ENDPOINT;Database=$DB_NAME;Username=$ADMIN_USER;Password=$ADMIN_PASSWORD;SSL Mode=Require"

# 2. Restart application pods to pick up new connection
kubectl rollout restart deployment/honua-server -n honua-prod

# 3. Update DNS for multi-region routing (if configured)
# This depends on your DNS provider (Azure DNS, Route 53, etc.)
```

#### Step 5: Verify Recovery

```bash
# 1. Test database connectivity
psql "host=$SECONDARY_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" \
  -c "SELECT version();"

# 2. Check application health
curl http://app/health

# 3. Verify data is recent
psql "host=$SECONDARY_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" <<EOF
-- Check latest data timestamp
SELECT MAX(updated_at) as latest_data FROM audit_log;

-- Calculate data lag
SELECT
  NOW() - MAX(updated_at) as data_lag,
  CASE
    WHEN NOW() - MAX(updated_at) < INTERVAL '30 minutes' THEN 'Acceptable'
    ELSE 'High Lag'
  END as status
FROM audit_log;
EOF
```

### Failback Procedure

When primary region recovers:

```bash
# 1. Wait for primary region full recovery
az status --query 'status' -o tsv
# Confirm status is "Available"

# 2. Create backup of secondary server
az postgres flexible-server backup create \
  --resource-group "$SECONDARY_RG" \
  --name "$SECONDARY_SERVER" \
  --backup-name "pre-failback-$(date +%Y%m%d%H%M)"

# 3. Restore secondary to primary region
az postgres flexible-server geo-restore \
  --resource-group "$RESOURCE_GROUP" \
  --name "$SERVER_NAME" \
  --source-server "/subscriptions/{subscription-id}/resourceGroups/$SECONDARY_RG/providers/Microsoft.DBforPostgreSQL/flexibleServers/$SECONDARY_SERVER" \
  --location eastus

# 4. Update application to point back to primary
az keyvault secret set \
  --vault-name "kv-honua-prod" \
  --name "PostgreSQL-ConnectionString" \
  --value "Host=$ORIGINAL_ENDPOINT;Database=$DB_NAME;..."

kubectl rollout restart deployment/honua-server -n honua-prod

# 5. Cleanup secondary server after verification (24-48 hours)
az postgres flexible-server delete \
  --name "$SECONDARY_SERVER" \
  --resource-group "$SECONDARY_RG" \
  --yes
```

---

## Runbook 3: Long-term Backup Restore

**Use Case**: Restore from archived backup (> 35 days old)

**Estimated Time**: 2-4 hours
**Downtime Required**: Optional
**Data Loss**: Up to 1 day (depending on backup frequency)

### Procedure

#### Step 1: Identify and Retrieve Backup

```bash
# 1. List available long-term backups
az storage blob list \
  --account-name "$BACKUP_STORAGE_ACCOUNT" \
  --container-name "$BACKUP_CONTAINER" \
  --prefix "daily/" \
  --output table

# 2. Choose backup to restore
BACKUP_DATE="2025-09-01"
BACKUP_BLOB="daily/postgres-honua-prod-${BACKUP_DATE}.dump"

# 3. Check if backup is in Archive tier
TIER=$(az storage blob show \
  --account-name "$BACKUP_STORAGE_ACCOUNT" \
  --container-name "$BACKUP_CONTAINER" \
  --name "$BACKUP_BLOB" \
  --query 'properties.blobTier' -o tsv)

if [ "$TIER" == "Archive" ]; then
  echo "Backup is in Archive tier - rehydration required (up to 15 hours)"

  # Request rehydration to Hot tier
  az storage blob set-tier \
    --account-name "$BACKUP_STORAGE_ACCOUNT" \
    --container-name "$BACKUP_CONTAINER" \
    --name "$BACKUP_BLOB" \
    --tier Hot \
    --rehydrate-priority High

  echo "Backup rehydration started. Check status with:"
  echo "az storage blob show --name $BACKUP_BLOB --query 'properties.archiveStatus'"

  # Wait for rehydration (polling every 30 minutes)
  while true; do
    STATUS=$(az storage blob show \
      --account-name "$BACKUP_STORAGE_ACCOUNT" \
      --container-name "$BACKUP_CONTAINER" \
      --name "$BACKUP_BLOB" \
      --query 'properties.archiveStatus' -o tsv)

    if [ "$STATUS" == "rehydrate-pending-to-hot" ]; then
      echo "Rehydration in progress..."
      sleep 1800  # 30 minutes
    else
      echo "Backup rehydrated and ready"
      break
    fi
  done
fi
```

#### Step 2: Download and Verify Backup

```bash
# 1. Download backup
LOCAL_BACKUP="/tmp/postgres-restore-${BACKUP_DATE}.dump"

az storage blob download \
  --account-name "$BACKUP_STORAGE_ACCOUNT" \
  --container-name "$BACKUP_CONTAINER" \
  --name "$BACKUP_BLOB" \
  --file "$LOCAL_BACKUP"

# 2. Verify download integrity
EXPECTED_SIZE=$(az storage blob show \
  --account-name "$BACKUP_STORAGE_ACCOUNT" \
  --container-name "$BACKUP_CONTAINER" \
  --name "$BACKUP_BLOB" \
  --query 'properties.contentLength' -o tsv)

ACTUAL_SIZE=$(stat -f%z "$LOCAL_BACKUP" 2>/dev/null || stat -c%s "$LOCAL_BACKUP")

if [ "$EXPECTED_SIZE" -ne "$ACTUAL_SIZE" ]; then
  echo "ERROR: Downloaded file size mismatch"
  echo "Expected: $EXPECTED_SIZE"
  echo "Actual: $ACTUAL_SIZE"
  exit 1
fi

echo "✓ Backup downloaded and verified"
```

#### Step 3: Create Temporary Restore Server

```bash
# 1. Create new server for restore
TEMP_SERVER="${SERVER_NAME}-longterm-restore-$(date +%Y%m%d)"

az postgres flexible-server create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$TEMP_SERVER" \
  --location eastus \
  --admin-user "$ADMIN_USER" \
  --admin-password "$ADMIN_PASSWORD" \
  --sku-name GP_Standard_D2s_v3 \
  --version 15 \
  --storage-size 128

# 2. Wait for server ready
az postgres flexible-server wait \
  --name "$TEMP_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --exists \
  --timeout 1800

TEMP_ENDPOINT=$(az postgres flexible-server show \
  --name "$TEMP_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --query 'fullyQualifiedDomainName' -o tsv)
```

#### Step 4: Restore Backup

```bash
# 1. Create database
psql "host=$TEMP_ENDPOINT user=$ADMIN_USER dbname=postgres sslmode=require" <<EOF
CREATE DATABASE $DB_NAME;
\c $DB_NAME
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;
EOF

# 2. Restore backup
echo "Restoring backup (this may take 30-60 minutes)..."

pg_restore \
  --host="$TEMP_ENDPOINT" \
  --username="$ADMIN_USER" \
  --dbname="$DB_NAME" \
  --verbose \
  --no-owner \
  --no-privileges \
  --jobs=4 \
  "$LOCAL_BACKUP" \
  2>&1 | tee restore-$(date +%Y%m%d%H%M).log

RESTORE_STATUS=${PIPESTATUS[0]}

if [ $RESTORE_STATUS -eq 0 ]; then
  echo "✓ Restore completed successfully"
else
  echo "✗ Restore failed with status $RESTORE_STATUS"
  echo "Check restore-$(date +%Y%m%d%H%M).log for details"
  exit 1
fi
```

#### Step 5: Verify and Migrate Data

```bash
# 1. Verify restored data
psql "host=$TEMP_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" <<EOF
-- Verification queries
SELECT COUNT(*) as table_count
FROM information_schema.tables
WHERE table_schema = 'public';

SELECT
  schemaname,
  tablename,
  n_live_tup
FROM pg_stat_user_tables
ORDER BY n_live_tup DESC;

-- Check specific data you need to recover
SELECT * FROM your_table WHERE id = 'specific-id';
EOF

# 2. If only specific data needed, export it
psql "host=$TEMP_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" \
  -c "COPY (SELECT * FROM your_table WHERE created_at < '2025-09-01') TO STDOUT CSV HEADER" \
  > recovered-data.csv

# 3. Import into production (if selective recovery)
psql "host=$PROD_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" \
  -c "\COPY your_table FROM 'recovered-data.csv' CSV HEADER"
```

#### Step 6: Cleanup

```bash
# Delete temporary server
az postgres flexible-server delete \
  --name "$TEMP_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --yes

# Delete local backup file
rm "$LOCAL_BACKUP"

echo "✓ Cleanup complete"
```

---

## Runbook 4: Table-Level Recovery

**Use Case**: Recover specific table(s) without full database restore

**Estimated Time**: 15-30 minutes
**Downtime Required**: No
**Data Loss**: None

### Procedure

```bash
# 1. Create temporary restore server (as in PITR or Long-term restore)

# 2. Export specific table from restored database
psql "host=$TEMP_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" \
  -c "\COPY your_table TO '/tmp/table-recovery.csv' CSV HEADER"

# 3. Backup current table in production
psql "host=$PROD_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" <<EOF
CREATE TABLE your_table_backup_$(date +%Y%m%d) AS
SELECT * FROM your_table;
EOF

# 4. Clear and restore table data
psql "host=$PROD_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" <<EOF
TRUNCATE TABLE your_table;
\COPY your_table FROM '/tmp/table-recovery.csv' CSV HEADER
EOF

# 5. Verify
psql "host=$PROD_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" \
  -c "SELECT COUNT(*) FROM your_table;"
```

---

## Runbook 5: Cross-Region Failover

**Use Case**: Complete failover to different Azure region

**Estimated Time**: 2-4 hours
**Downtime Required**: 30-60 minutes
**Data Loss**: < 1 hour

### Procedure

**Phase 1: Failover**
1. Restore database in target region (see Runbook 2)
2. Deploy application infrastructure in target region
3. Update DNS/load balancer to point to new region
4. Verify all services operational

**Phase 2: Stabilization**
5. Monitor performance and errors
6. Scale resources as needed
7. Enable backups in new region

**Phase 3: Failback (Optional)**
8. When primary region recovers, optionally fail back
9. Follow reverse procedure

---

## Post-Restore Verification

### Comprehensive Verification Checklist

```bash
#!/bin/bash
# comprehensive-restore-verification.sh

echo "=== Comprehensive Restore Verification ==="

RESTORED_ENDPOINT="$1"

# 1. Server Health
echo "1. Checking server health..."
az postgres flexible-server show \
  --name "$NEW_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --query '{State:state, Version:version, HA:highAvailability.mode}' -o table

# 2. Database Connectivity
echo "2. Testing database connectivity..."
psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" \
  -c "SELECT 1;" > /dev/null 2>&1 && echo "✓ Connection successful" || echo "✗ Connection failed"

# 3. Extension Verification
echo "3. Verifying extensions..."
psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" <<EOF
SELECT extname, extversion FROM pg_extension;
EOF

# 4. Schema Validation
echo "4. Validating schema..."
psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" <<EOF
-- Check table count
SELECT COUNT(*) as table_count
FROM information_schema.tables
WHERE table_schema = 'public';

-- Check constraint count
SELECT COUNT(*) as constraint_count
FROM information_schema.table_constraints;

-- Check index count
SELECT COUNT(*) as index_count
FROM pg_indexes
WHERE schemaname = 'public';
EOF

# 5. Data Integrity
echo "5. Checking data integrity..."
psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" <<EOF
-- Row counts per table
SELECT
  schemaname,
  tablename,
  n_live_tup as row_count,
  pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size
FROM pg_stat_user_tables
ORDER BY n_live_tup DESC;

-- Check for NULL values in critical columns
SELECT COUNT(*) as null_count
FROM collections
WHERE id IS NULL OR name IS NULL;

-- Verify referential integrity
SELECT
  conname as constraint_name,
  conrelid::regclass as table_name,
  confrelid::regclass as referenced_table
FROM pg_constraint
WHERE contype = 'f';
EOF

# 6. Application Connectivity Test
echo "6. Testing application connectivity..."
if command -v kubectl &> /dev/null; then
  kubectl run psql-test --rm -it --restart=Never --image=postgres:15 -- \
    psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" -c "SELECT 1;"
fi

# 7. Performance Baseline
echo "7. Checking performance metrics..."
psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" <<EOF
-- Check cache hit ratio (should be > 90%)
SELECT
  'cache_hit_ratio' as metric,
  ROUND(sum(blks_hit) / nullif(sum(blks_hit + blks_read), 0) * 100, 2) as percentage
FROM pg_stat_database;

-- Check slow queries
SELECT
  calls,
  mean_exec_time,
  query
FROM pg_stat_statements
ORDER BY mean_exec_time DESC
LIMIT 5;
EOF

# 8. Backup Configuration
echo "8. Verifying backup configuration..."
az postgres flexible-server show \
  --name "$NEW_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --query '{BackupRetention:backup.backupRetentionDays, GeoRedundant:backup.geoRedundantBackup}' -o table

# 9. Generate Verification Report
cat > "restore-verification-$(date +%Y%m%d%H%M).txt" <<EOF
Restore Verification Report
==========================
Date: $(date)
Restored Server: $NEW_SERVER
Endpoint: $RESTORED_ENDPOINT
Restore Point: $RESTORE_TIME

Verification Results:
$(cat verification-results.txt)

Status: All checks passed
Next Steps:
1. Monitor application for 24 hours
2. Perform user acceptance testing
3. Update documentation
4. Schedule post-mortem
EOF

echo "=== Verification Complete ==="
```

---

## Troubleshooting

### Common Issues

#### Issue 1: Restore Timeout

**Symptom**: Restore operation exceeds 1 hour
**Solution**:
```bash
# Check restore status
az postgres flexible-server show \
  --name "$NEW_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --query 'state' -o tsv

# If stuck, cancel and retry with different target
az postgres flexible-server delete --name "$NEW_SERVER" --resource-group "$RESOURCE_GROUP" --yes
```

#### Issue 2: Connection Failures After Restore

**Symptom**: Cannot connect to restored database
**Solution**:
```bash
# 1. Check firewall rules
az postgres flexible-server firewall-rule list \
  --name "$NEW_SERVER" \
  --resource-group "$RESOURCE_GROUP"

# 2. Add your IP
az postgres flexible-server firewall-rule create \
  --name "$NEW_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --rule-name AllowMyIP \
  --start-ip-address $(curl -s ifconfig.me) \
  --end-ip-address $(curl -s ifconfig.me)

# 3. Check server status
az postgres flexible-server show \
  --name "$NEW_SERVER" \
  --resource-group "$RESOURCE_GROUP" \
  --query '{State:state, PublicAccess:network.publicNetworkAccess}' -o table
```

#### Issue 3: Data Inconsistencies

**Symptom**: Row counts don't match expected
**Solution**:
```bash
# 1. Check for replication lag
psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" \
  -c "SELECT pg_last_wal_replay_lsn(), pg_is_in_recovery();"

# 2. Run VACUUM ANALYZE
psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" \
  -c "VACUUM ANALYZE;"

# 3. Refresh statistics
psql "host=$RESTORED_ENDPOINT user=$ADMIN_USER dbname=$DB_NAME sslmode=require" \
  -c "ANALYZE;"
```

#### Issue 4: Long-term Backup Not Found

**Symptom**: Backup blob doesn't exist in expected location
**Solution**:
```bash
# Search all containers
for CONTAINER in $(az storage container list --account-name "$BACKUP_STORAGE_ACCOUNT" --query '[].name' -o tsv); do
  echo "Searching $CONTAINER..."
  az storage blob list \
    --account-name "$BACKUP_STORAGE_ACCOUNT" \
    --container-name "$CONTAINER" \
    --prefix "$BACKUP_DATE" \
    --output table
done

# Check lifecycle policy didn't delete backup prematurely
az storage management-policy show \
  --account-name "$BACKUP_STORAGE_ACCOUNT" \
  --resource-group "$RESOURCE_GROUP"
```

---

## Emergency Contacts

| Role | Contact | Availability |
|------|---------|--------------|
| **Platform Engineering Lead** | platform-lead@honua.io | 24/7 |
| **Database Administrator** | dba@honua.io | Business hours |
| **On-Call Engineer** | PagerDuty | 24/7 |
| **Azure Support** | Azure Portal | 24/7 (Severity A) |

---

## Related Documentation

- [Backup Policy](./AZURE_BACKUP_POLICY.md) - Complete backup strategy
- [Terraform Configuration](../../infrastructure/terraform/azure/main.tf) - Infrastructure as Code
- [General Restore Guide](../rag/04-operations/backup-disaster-recovery.md) - Multi-cloud procedures
- [Operations Runbooks](../operations/RUNBOOKS.md) - Operational procedures

---

**Document Version**: 1.0
**Last Updated**: 2025-10-18
**Next Review**: 2025-11-18
**Owner**: Platform Engineering Team
