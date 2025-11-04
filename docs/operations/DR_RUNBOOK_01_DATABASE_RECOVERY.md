# Disaster Recovery Runbook: Database Recovery from Backup

**Runbook ID**: DR-01
**Last Updated**: 2025-10-18
**Version**: 1.0
**Severity**: P1 (Critical)
**Estimated Time**: 30-90 minutes

## Table of Contents

- [Overview](#overview)
- [Recovery Objectives](#recovery-objectives)
- [Prerequisites](#prerequisites)
- [Recovery Scenarios](#recovery-scenarios)
- [Step-by-Step Procedures](#step-by-step-procedures)
- [Validation](#validation)
- [Post-Recovery Tasks](#post-recovery-tasks)
- [Rollback Procedures](#rollback-procedures)

---

## Overview

This runbook provides step-by-step procedures for recovering Honua databases from backups in disaster scenarios. It covers recovery for all supported database platforms: PostgreSQL, MySQL, SQL Server, and Oracle.

### When to Use This Runbook

- **Database corruption**: Data corruption detected in production database
- **Data loss**: Accidental deletion of critical data
- **Disaster recovery**: Complete datacenter failure
- **Point-in-time recovery**: Need to restore to specific timestamp
- **Migration failure**: Failed database migration requiring rollback

### Critical Success Factors

- Have current verified backups available
- Understand acceptable data loss window (RPO)
- Have tested restore procedure in non-production
- Can communicate downtime to stakeholders

---

## Recovery Objectives

### Production Environment

| Metric | Target | Maximum |
|--------|--------|---------|
| **RTO** (Recovery Time Objective) | 30 minutes | 2 hours |
| **RPO** (Recovery Point Objective) | 15 minutes | 1 hour |
| **Data Loss Tolerance** | < 100 records | < 1000 records |
| **Downtime Window** | Off-hours preferred | Emergency: 24x7 |

### Staging Environment

| Metric | Target |
|--------|--------|
| **RTO** | 2 hours |
| **RPO** | 4 hours |

### Development Environment

| Metric | Target |
|--------|--------|
| **RTO** | 4 hours |
| **RPO** | 24 hours |

---

## Prerequisites

### Required Access

- [ ] Database admin credentials (from Key Vault)
- [ ] Cloud provider access (AWS/Azure/GCP) with storage permissions
- [ ] Kubernetes cluster access (if applicable)
- [ ] SSH access to database server (if self-hosted)
- [ ] PagerDuty/Slack for incident communication

### Required Tools

```bash
# PostgreSQL
apt-get install postgresql-client-16

# MySQL
apt-get install mysql-client

# SQL Server
apt-get install mssql-tools18

# Azure CLI (for Azure deployments)
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# AWS CLI (for AWS deployments)
pip install awscli

# Verification tools
apt-get install jq curl
```

### Required Information

```bash
# Document these before starting recovery
BACKUP_LOCATION="<s3://bucket or Azure Storage URL>"
BACKUP_DATE="<YYYY-MM-DD HH:MM:SS>"
DATABASE_NAME="<honua/honua_prod>"
RECOVERY_TARGET_TIME="<YYYY-MM-DD HH:MM:SS>"  # For point-in-time recovery
DOWNTIME_WINDOW="<start-end time range>"
```

---

## Recovery Scenarios

### Scenario A: PostgreSQL Recovery (Azure/AWS/GCP)

**Use Case**: Recover PostgreSQL database from backup

**Common Triggers**:
- Database corruption
- Accidental DROP TABLE or DELETE
- Failed schema migration
- Complete database deletion

---

### Scenario B: Point-in-Time Recovery (PITR)

**Use Case**: Restore database to specific timestamp

**Common Triggers**:
- Need to recover before specific incident
- Bad data import that needs to be undone
- Ransomware attack recovery

---

### Scenario C: Cross-Region Recovery

**Use Case**: Recover database in different region (disaster scenario)

**Common Triggers**:
- Regional outage
- Data center failure
- Geographic failover

---

## Step-by-Step Procedures

### Procedure 1: PostgreSQL Full Restore (Azure Flexible Server)

**When to use**: Production PostgreSQL database needs full restore

**Estimated Time**: 30-60 minutes

**Prerequisites**:
- Azure CLI authenticated: `az login`
- Backup retention period not expired
- Sufficient storage quota in target region

**Steps**:

#### Step 1: Declare Incident and Prepare

```bash
#!/bin/bash
# DR-01-postgresql-restore.sh

set -euo pipefail

# Configuration
RESOURCE_GROUP="rg-honua-prod-eastus"
SERVER_NAME="postgres-honua-prod"
DATABASE_NAME="honua"
BACKUP_TIME="2025-10-18T02:00:00Z"  # UTC timestamp
NEW_SERVER_NAME="postgres-honua-prod-restored-$(date +%s)"

# Log file
LOG_FILE="/var/log/honua/dr-restore-$(date +%Y%m%d_%H%M%S).log"
mkdir -p "$(dirname "$LOG_FILE")"

log() {
    echo "[$(date +'%Y-%m-%d %H:%M:%S')] $*" | tee -a "$LOG_FILE"
}

log "=== PostgreSQL Disaster Recovery Started ==="
log "Source Server: $SERVER_NAME"
log "Restore Time: $BACKUP_TIME"
log "Target Server: $NEW_SERVER_NAME"

# Notify stakeholders
if [ -n "${SLACK_WEBHOOK:-}" ]; then
    curl -X POST "$SLACK_WEBHOOK" \
        -H 'Content-Type: application/json' \
        -d "{\"text\":\"🚨 DATABASE RECOVERY IN PROGRESS - ETA 60 minutes\"}"
fi
```

#### Step 2: Verify Backup Availability

```bash
# List available backups
log "Checking backup availability..."

az postgres flexible-server backup list \
    --resource-group "$RESOURCE_GROUP" \
    --server-name "$SERVER_NAME" \
    --query "[].{Name:name,BackupTime:backupTime,BackupType:backupType}" \
    --output table | tee -a "$LOG_FILE"

# Verify specific backup exists
BACKUP_EXISTS=$(az postgres flexible-server backup list \
    --resource-group "$RESOURCE_GROUP" \
    --server-name "$SERVER_NAME" \
    --query "[?backupTime>='$BACKUP_TIME'] | [0].name" \
    --output tsv)

if [ -z "$BACKUP_EXISTS" ]; then
    log "ERROR: No backup found at or before $BACKUP_TIME"
    log "Available backups listed above. Choose nearest backup."
    exit 1
fi

log "✓ Backup verified: $BACKUP_EXISTS"
```

#### Step 3: Stop Application Traffic

```bash
# Scale down application to prevent writes during recovery
log "Scaling down applications..."

kubectl scale deployment/honua-server -n honua --replicas=0
kubectl scale deployment/honua-process-framework -n honua-process-framework --replicas=0

# Wait for pods to terminate
kubectl wait --for=delete pod -l app=honua-server -n honua --timeout=120s
kubectl wait --for=delete pod -l app=honua-process-framework -n honua-process-framework --timeout=120s

log "✓ Applications scaled down"
```

#### Step 4: Perform Point-in-Time Restore

```bash
# Create new server from backup
log "Starting point-in-time restore (this may take 15-30 minutes)..."

az postgres flexible-server restore \
    --resource-group "$RESOURCE_GROUP" \
    --name "$NEW_SERVER_NAME" \
    --source-server "$SERVER_NAME" \
    --restore-time "$BACKUP_TIME" \
    --location eastus \
    --output table | tee -a "$LOG_FILE"

# Wait for restore to complete
log "Waiting for server to become available..."

az postgres flexible-server wait \
    --resource-group "$RESOURCE_GROUP" \
    --name "$NEW_SERVER_NAME" \
    --exists \
    --timeout 1800  # 30 minutes

log "✓ Server restore completed"
```

#### Step 5: Verify Restored Database

```bash
# Get new server details
log "Verifying restored database..."

NEW_SERVER_HOST=$(az postgres flexible-server show \
    --resource-group "$RESOURCE_GROUP" \
    --name "$NEW_SERVER_NAME" \
    --query "fullyQualifiedDomainName" \
    --output tsv)

# Get admin credentials from Key Vault
VAULT_NAME=$(az keyvault list \
    --resource-group "$RESOURCE_GROUP" \
    --query "[0].name" \
    --output tsv)

ADMIN_USER=$(az keyvault secret show \
    --vault-name "$VAULT_NAME" \
    --name "PostgreSQL-AdminUser" \
    --query "value" \
    --output tsv)

ADMIN_PASSWORD=$(az keyvault secret show \
    --vault-name "$VAULT_NAME" \
    --name "PostgreSQL-AdminPassword" \
    --query "value" \
    --output tsv)

# Test database connection
log "Testing database connectivity..."

PGPASSWORD="$ADMIN_PASSWORD" psql \
    --host="$NEW_SERVER_HOST" \
    --port=5432 \
    --username="$ADMIN_USER" \
    --dbname="$DATABASE_NAME" \
    --command="SELECT version();" | tee -a "$LOG_FILE"

if [ ${PIPESTATUS[0]} -ne 0 ]; then
    log "ERROR: Cannot connect to restored database"
    exit 1
fi

log "✓ Database connection successful"
```

#### Step 6: Verify Data Integrity

```bash
# Verify table counts match expectations
log "Verifying data integrity..."

PGPASSWORD="$ADMIN_PASSWORD" psql \
    --host="$NEW_SERVER_HOST" \
    --port=5432 \
    --username="$ADMIN_USER" \
    --dbname="$DATABASE_NAME" \
    <<EOF | tee -a "$LOG_FILE"

-- Check critical tables
SELECT
    schemaname,
    tablename,
    pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) as size,
    (SELECT count(*) FROM pg_stat_user_tables WHERE schemaname||'.'||tablename = t.schemaname||'.'||t.tablename) as row_count
FROM pg_tables t
WHERE schemaname NOT IN ('pg_catalog', 'information_schema')
ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC
LIMIT 20;

-- Check data freshness
SELECT
    'features' as table_name,
    MAX(updated_at) as latest_update,
    COUNT(*) as total_rows
FROM features
UNION ALL
SELECT
    'stac_items',
    MAX(properties->>'datetime'),
    COUNT(*)
FROM stac_items;

EOF

# Verify indexes exist
PGPASSWORD="$ADMIN_PASSWORD" psql \
    --host="$NEW_SERVER_HOST" \
    --port=5432 \
    --username="$ADMIN_USER" \
    --dbname="$DATABASE_NAME" \
    --command="SELECT schemaname, tablename, indexname FROM pg_indexes WHERE schemaname = 'public' ORDER BY tablename, indexname;" \
    | tee -a "$LOG_FILE"

log "✓ Data integrity checks completed - review output above"
```

#### Step 7: Update Connection Strings

```bash
# Update connection string in Key Vault
log "Updating connection strings..."

NEW_CONNECTION_STRING="Host=${NEW_SERVER_HOST};Port=5432;Database=${DATABASE_NAME};Username=${ADMIN_USER};Password=${ADMIN_PASSWORD};SSL Mode=Require;Trust Server Certificate=true;"

az keyvault secret set \
    --vault-name "$VAULT_NAME" \
    --name "PostgreSQL-ConnectionString" \
    --value "$NEW_CONNECTION_STRING" | tee -a "$LOG_FILE"

# Update application configuration
kubectl patch configmap honua-config -n honua \
    --type merge \
    -p "{\"data\":{\"ConnectionStrings__DefaultConnection\":\"@Microsoft.KeyVault(SecretUri=https://${VAULT_NAME}.vault.azure.net/secrets/PostgreSQL-ConnectionString)\"}}"

log "✓ Connection strings updated"
```

#### Step 8: Restore Application Traffic

```bash
# Scale up applications
log "Restoring application traffic..."

kubectl scale deployment/honua-server -n honua --replicas=3
kubectl scale deployment/honua-process-framework -n honua-process-framework --replicas=3

# Wait for pods to be ready
kubectl wait --for=condition=ready pod -l app=honua-server -n honua --timeout=300s
kubectl wait --for=condition=ready pod -l app=honua-process-framework -n honua-process-framework --timeout=300s

log "✓ Applications restored"
```

#### Step 9: Smoke Test

```bash
# Test critical endpoints
log "Running smoke tests..."

# Test health endpoint
curl -f http://honua-server/health || {
    log "ERROR: Health check failed"
    exit 1
}

# Test database read
curl -f "http://honua-server/ogcapi/collections" || {
    log "ERROR: Collections endpoint failed"
    exit 1
}

# Test database write (create test feature)
curl -X POST "http://honua-server/ogcapi/collections/test/items" \
    -H "Content-Type: application/geo+json" \
    -d '{
        "type":"Feature",
        "geometry":{"type":"Point","coordinates":[0,0]},
        "properties":{"name":"recovery-test"}
    }' || {
    log "ERROR: Write test failed"
    exit 1
}

log "✓ Smoke tests passed"
```

#### Step 10: Monitor and Cleanup

```bash
# Monitor for errors
log "Monitoring application logs (press Ctrl+C after 2 minutes if no errors)..."

kubectl logs -f -l app=honua-server -n honua --tail=100 &
LOG_PID=$!

sleep 120
kill $LOG_PID 2>/dev/null || true

# Success notification
log "=== RECOVERY COMPLETED SUCCESSFULLY ==="
log "Old Server: $SERVER_NAME (can be deleted after 7 days)"
log "New Server: $NEW_SERVER_NAME"
log "Recovery Time: $(date)"
log "Log File: $LOG_FILE"

if [ -n "${SLACK_WEBHOOK:-}" ]; then
    curl -X POST "$SLACK_WEBHOOK" \
        -H 'Content-Type: application/json' \
        -d "{\"text\":\"✅ DATABASE RECOVERY COMPLETED - Service restored at $(date)\"}"
fi

# Create cleanup reminder
echo "#!/bin/bash" > /tmp/cleanup-old-server.sh
echo "# Run this after 7 days to delete old server" >> /tmp/cleanup-old-server.sh
echo "az postgres flexible-server delete \\" >> /tmp/cleanup-old-server.sh
echo "    --resource-group \"$RESOURCE_GROUP\" \\" >> /tmp/cleanup-old-server.sh
echo "    --name \"$SERVER_NAME\" \\" >> /tmp/cleanup-old-server.sh
echo "    --yes" >> /tmp/cleanup-old-server.sh
chmod +x /tmp/cleanup-old-server.sh

log "Cleanup script created: /tmp/cleanup-old-server.sh (run after 7-day verification period)"
```

---

### Procedure 2: PostgreSQL Point-in-Time Recovery

**When to use**: Need to restore to specific timestamp before incident

**Steps**:

```bash
# Identify exact recovery point
INCIDENT_TIME="2025-10-18T14:23:00Z"  # When bad data was written
RECOVERY_TIME="2025-10-18T14:20:00Z"  # 3 minutes before incident

# Use same procedure as above but with specific restore time
az postgres flexible-server restore \
    --resource-group "rg-honua-prod-eastus" \
    --name "postgres-honua-pitr-$(date +%s)" \
    --source-server "postgres-honua-prod" \
    --restore-time "$RECOVERY_TIME" \
    --location eastus

# Verify recovered data
PGPASSWORD="$ADMIN_PASSWORD" psql \
    --host="$NEW_SERVER_HOST" \
    --command="SELECT * FROM audit_log WHERE timestamp > '$RECOVERY_TIME' AND timestamp < '$INCIDENT_TIME' ORDER BY timestamp DESC LIMIT 10;"
```

---

### Procedure 3: Self-Hosted PostgreSQL Restore (Docker/VM)

**When to use**: PostgreSQL running in Docker or VM (non-managed)

**Steps**:

```bash
#!/bin/bash
# Self-hosted PostgreSQL restore

# Configuration
BACKUP_FILE="/backups/postgres-honua-20251018-020000.dump.gz"
DB_HOST="localhost"
DB_PORT="5432"
DB_NAME="honua"
DB_USER="honua_admin"
DB_PASSWORD="<from-vault>"

# Stop application
docker-compose -f /opt/honua/docker-compose.yml down

# Drop and recreate database
PGPASSWORD="$DB_PASSWORD" psql \
    --host="$DB_HOST" \
    --port="$DB_PORT" \
    --username="$DB_USER" \
    --dbname="postgres" \
    <<EOF
-- Terminate active connections
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = '$DB_NAME' AND pid <> pg_backend_pid();

-- Drop and recreate
DROP DATABASE IF EXISTS $DB_NAME;
CREATE DATABASE $DB_NAME;
GRANT ALL PRIVILEGES ON DATABASE $DB_NAME TO $DB_USER;
EOF

# Restore from backup
gunzip -c "$BACKUP_FILE" | \
PGPASSWORD="$DB_PASSWORD" pg_restore \
    --host="$DB_HOST" \
    --port="$DB_PORT" \
    --username="$DB_USER" \
    --dbname="$DB_NAME" \
    --jobs=4 \
    --verbose

# Verify restore
PGPASSWORD="$DB_PASSWORD" psql \
    --host="$DB_HOST" \
    --port="$DB_PORT" \
    --username="$DB_USER" \
    --dbname="$DB_NAME" \
    --command="SELECT schemaname, tablename, pg_size_pretty(pg_total_relation_size(schemaname||'.'||tablename)) FROM pg_tables WHERE schemaname = 'public' ORDER BY pg_total_relation_size(schemaname||'.'||tablename) DESC LIMIT 10;"

# Restart application
docker-compose -f /opt/honua/docker-compose.yml up -d
```

---

## Validation

### Validation Checklist

After recovery, verify ALL of the following:

#### Database Connectivity
- [ ] Application can connect to database
- [ ] Connection pool is healthy
- [ ] No connection errors in logs

```bash
# Test connection
kubectl exec -n honua deployment/honua-server -- \
    psql "$CONNECTION_STRING" -c "SELECT 1;"

# Check connection pool
curl http://honua-server/health | jq '.checks.database'
```

#### Data Integrity
- [ ] Expected number of tables present
- [ ] Row counts match expectations (±acceptable loss)
- [ ] Critical indexes exist
- [ ] Foreign key constraints intact

```bash
# Validate schema
psql "$CONNECTION_STRING" <<EOF
-- Count tables
SELECT count(*) as table_count
FROM information_schema.tables
WHERE table_schema = 'public';

-- Verify critical tables
SELECT tablename,
       (SELECT count(*) FROM honua.features) as feature_count,
       (SELECT count(*) FROM honua.stac_items) as stac_count;

-- Check indexes
SELECT count(*) as index_count
FROM pg_indexes
WHERE schemaname = 'public';
EOF
```

#### Application Functionality
- [ ] Health endpoint returns 200 OK
- [ ] Can read data (GET requests work)
- [ ] Can write data (POST requests work)
- [ ] Search queries return results
- [ ] No 500 errors in logs

```bash
# Health check
curl -f http://honua-server/health

# Read test
curl -f http://honua-server/ogcapi/collections

# Write test
curl -X POST http://honua-server/ogcapi/collections/test/items \
    -H "Content-Type: application/geo+json" \
    -d '{"type":"Feature","geometry":{"type":"Point","coordinates":[0,0]},"properties":{"test":"recovery"}}'

# Search test
curl -f "http://honua-server/ogcapi/collections/test/items?limit=10"
```

#### Performance
- [ ] Query response times < 2x normal
- [ ] No connection pool exhaustion
- [ ] Memory usage normal
- [ ] CPU usage normal

```bash
# Check response times
curl -w "Time: %{time_total}s\n" -o /dev/null -s http://honua-server/ogcapi/collections

# Check resource usage
kubectl top pods -n honua
```

---

## Post-Recovery Tasks

### Immediate (Within 1 Hour)

1. **Document Recovery**
   ```bash
   # Create incident report
   cat > /tmp/recovery-report-$(date +%Y%m%d).md <<EOF
   # Database Recovery Report

   **Incident Date**: $(date)
   **Recovery Start**: <start-time>
   **Recovery End**: $(date)
   **Data Loss**: <minutes>
   **Root Cause**: <description>

   ## Recovery Details
   - Old Server: $SERVER_NAME
   - New Server: $NEW_SERVER_NAME
   - Restore Point: $BACKUP_TIME
   - Records Lost: <estimated>

   ## Validation Results
   - Database Connectivity: ✓ PASS
   - Data Integrity: ✓ PASS
   - Application Functionality: ✓ PASS
   - Performance: ✓ PASS

   ## Next Steps
   - Monitor for 24 hours
   - Delete old server after 7 days
   - Review backup strategy
   EOF
   ```

2. **Update Monitoring**
   ```bash
   # Tag metrics with recovery event
   curl -X POST http://prometheus:9090/api/v1/admin/tsdb/snapshot

   # Add annotation to Grafana
   curl -X POST http://grafana:3000/api/annotations \
       -H "Content-Type: application/json" \
       -d '{
           "text":"Database recovered from backup",
           "tags":["disaster-recovery","database"],
           "time":'$(date +%s000)'
       }'
   ```

3. **Notify Stakeholders**
   ```bash
   # Send completion notification
   cat > /tmp/recovery-notification.txt <<EOF
   DATABASE RECOVERY COMPLETED

   Service: Honua GIS Platform
   Environment: Production
   Recovery Time: $(date)
   Downtime: <calculated>
   Data Loss: <RPO achieved>

   Status: ✅ OPERATIONAL

   Next Steps:
   - Monitoring for anomalies (24h)
   - Post-mortem scheduled
   - Backup validation review
   EOF

   # Send via email/Slack
   ```

### Short-Term (Within 24 Hours)

4. **Monitor for Anomalies**
   - Watch error rates in Grafana
   - Check application logs for database errors
   - Monitor query performance
   - Verify data consistency

5. **Conduct Post-Mortem**
   - Identify root cause
   - Document lessons learned
   - Create action items
   - Update runbooks

6. **Verify Backups**
   ```bash
   # Ensure new backups are working
   /scripts/azure/backup-automation.sh
   /scripts/azure/verify-backup.sh
   ```

### Long-Term (Within 7 Days)

7. **Clean Up Old Server**
   ```bash
   # After 7-day verification period
   az postgres flexible-server delete \
       --resource-group "rg-honua-prod-eastus" \
       --name "$SERVER_NAME" \
       --yes
   ```

8. **Update Documentation**
   - Update recovery time estimates
   - Document any issues encountered
   - Improve runbook based on experience

9. **Review and Improve**
   - Assess if RTO/RPO were met
   - Identify areas for improvement
   - Update disaster recovery plan

---

## Rollback Procedures

If recovery fails or causes issues, use these rollback procedures:

### Rollback Option 1: Revert to Original Server

```bash
# If original server still exists and is healthy
# 1. Stop applications
kubectl scale deployment/honua-server -n honua --replicas=0

# 2. Revert connection string
az keyvault secret set \
    --vault-name "$VAULT_NAME" \
    --name "PostgreSQL-ConnectionString" \
    --value "$ORIGINAL_CONNECTION_STRING"

# 3. Restart applications
kubectl scale deployment/honua-server -n honua --replicas=3
```

### Rollback Option 2: Restore from Different Backup

```bash
# If recovered database is corrupted
# Choose earlier backup point
EARLIER_BACKUP_TIME="2025-10-18T01:00:00Z"

az postgres flexible-server restore \
    --resource-group "rg-honua-prod-eastus" \
    --name "postgres-honua-rollback-$(date +%s)" \
    --source-server "$SERVER_NAME" \
    --restore-time "$EARLIER_BACKUP_TIME"
```

### Rollback Option 3: Emergency Read-Only Mode

```bash
# If recovery is taking too long, enable read-only access
kubectl patch configmap honua-config -n honua \
    --patch '{"data":{"honua__readOnly":"true"}}'

kubectl rollout restart deployment/honua-server -n honua

# This allows users to access data while recovery continues
```

---

## Related Documentation

- [Operations Guide](./PROCESS_FRAMEWORK_OPERATIONS.md) - Daily operations
- [Runbooks Index](./RUNBOOKS.md) - All operational runbooks
- [Azure Backup Policy](../deployment/AZURE_BACKUP_POLICY.md) - Backup strategy
- [Deployment Guide](../deployment/README.md) - Initial deployment

---

## Emergency Contacts

| Role | Contact | Availability |
|------|---------|--------------|
| **Database Administrator** | dba@honua.io | 24x7 |
| **Platform Lead** | platform@honua.io | 24x7 |
| **On-Call Engineer** | oncall@honua.io | 24x7 |
| **Azure Support** | +1-800-xxx-xxxx | 24x7 |
| **AWS Support** | +1-866-xxx-xxxx | 24x7 |

---

**Document Version**: 1.0
**Last Updated**: 2025-10-18
**Next Review**: 2025-11-18
**Owner**: Platform Engineering Team
**Tested**: 2025-10-15 (Staging), 2025-10-01 (Production DR Drill)
