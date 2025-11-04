# Backup and Disaster Recovery Guide

**Keywords**: backup, disaster-recovery, pitr, point-in-time-recovery, rto, rpo, restore, postgresql-backup, snapshots, wal-archiving, database-backup, recovery-procedures, failover, high-availability, backup-automation, backup-testing

**Related Topics**: [Performance Tuning](performance-tuning.md), [Kubernetes Deployment](../02-deployment/kubernetes-deployment.md), [AWS ECS Deployment](../02-deployment/aws-ecs-deployment.md), [Docker Deployment](../02-deployment/docker-deployment.md)

---

## Overview

This guide provides comprehensive backup and disaster recovery strategies for production Honua deployments. A robust backup strategy protects against data loss, corruption, human error, and infrastructure failures while meeting your Recovery Time Objective (RTO) and Recovery Point Objective (RPO) requirements.

**Key Components**:
- PostgreSQL/PostGIS database backup and recovery
- Metadata configuration backup (JSON/YAML)
- Raster tile cache backup (S3, Azure, filesystem)
- Attachment storage backup
- Configuration and secrets backup
- Point-in-Time Recovery (PITR) with WAL archiving
- Multi-region disaster recovery
- Automated backup validation and testing

**Critical Metrics**:
- **RTO (Recovery Time Objective)**: Target time to restore service after failure
- **RPO (Recovery Point Objective)**: Acceptable data loss window
- **Backup Frequency**: How often backups are created
- **Retention Period**: How long backups are retained

---

## Table of Contents

1. [Backup Strategy Overview](#backup-strategy-overview)
2. [PostgreSQL Database Backup](#postgresql-database-backup)
3. [Point-in-Time Recovery (PITR)](#point-in-time-recovery-pitr)
4. [Metadata Backup](#metadata-backup)
5. [Tile Cache and Storage Backup](#tile-cache-and-storage-backup)
6. [Configuration and Secrets Backup](#configuration-and-secrets-backup)
7. [Cloud-Specific Backup Solutions](#cloud-specific-backup-solutions)
8. [Backup Automation](#backup-automation)
9. [Disaster Recovery Procedures](#disaster-recovery-procedures)
10. [Recovery Scenarios](#recovery-scenarios)
11. [Testing and Validation](#testing-and-validation)
12. [Monitoring and Alerting](#monitoring-and-alerting)

---

## Backup Strategy Overview

### Recovery Requirements

Define your backup strategy based on business requirements:

| Scenario | RTO Target | RPO Target | Strategy |
|----------|------------|------------|----------|
| Production (Critical) | < 1 hour | < 15 minutes | Continuous WAL archiving + hourly snapshots |
| Production (Standard) | < 4 hours | < 1 hour | Daily snapshots + transaction logs |
| Staging | < 8 hours | < 24 hours | Daily snapshots |
| Development | Best effort | < 7 days | Weekly snapshots |

### Backup Components

Complete Honua backup includes:

1. **Database (PostgreSQL/PostGIS)**
   - Schema and data (tables, indexes, constraints)
   - Spatial data and geometries
   - User accounts and permissions
   - Extensions (PostGIS, pg_stat_statements)

2. **Metadata Configuration**
   - Collection definitions (JSON/YAML)
   - Service configuration
   - Version control (Git)

3. **Tile Cache**
   - Pre-generated raster tiles
   - Tile metadata and indexes

4. **Attachments**
   - User-uploaded files
   - Related documents and media

5. **Application Configuration**
   - Environment variables
   - appsettings.json files
   - Secrets (OAuth keys, API tokens)

### Backup Storage Locations

**3-2-1 Backup Rule**:
- 3 copies of data
- 2 different storage media
- 1 copy off-site

**Example Implementation**:
- Primary: Production database (live data)
- Secondary: Local snapshots on same cloud region
- Tertiary: Cross-region snapshots in different availability zone
- Off-site: S3/Azure Blob in different geographic region

---

## PostgreSQL Database Backup

### Backup Methods Comparison

| Method | RTO | RPO | Size | Use Case |
|--------|-----|-----|------|----------|
| `pg_dump` | Slow (hours for TB) | Good (daily) | Compressed | Full logical backup |
| `pg_basebackup` | Fast (minutes) | Excellent (PITR) | Large | Physical backup + WAL |
| Cloud Snapshots | Very fast (seconds) | Excellent (PITR) | Incremental | Managed databases |
| Streaming Replication | Instant failover | Near-zero | N/A | High availability |

### Method 1: Logical Backup with pg_dump

**Best for**: Development, staging, cross-version migrations, selective restore

#### Basic pg_dump Backup

```bash
#!/bin/bash
# backup-postgres-logical.sh

# Configuration
BACKUP_DIR="/backups/postgresql"
DB_HOST="postgis"
DB_PORT="5432"
DB_NAME="honuadb"
DB_USER="honua"
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/honua_${BACKUP_DATE}.sql.gz"
RETENTION_DAYS=30

# Create backup directory
mkdir -p "${BACKUP_DIR}"

# Perform backup with compression
echo "Starting logical backup at $(date)"
pg_dump \
  --host="${DB_HOST}" \
  --port="${DB_PORT}" \
  --username="${DB_USER}" \
  --dbname="${DB_NAME}" \
  --format=custom \
  --compress=9 \
  --verbose \
  --file="${BACKUP_FILE}" \
  2>&1 | tee "${BACKUP_DIR}/backup_${BACKUP_DATE}.log"

# Check backup success
if [ $? -eq 0 ]; then
  echo "Backup completed successfully: ${BACKUP_FILE}"

  # Calculate backup size
  BACKUP_SIZE=$(du -h "${BACKUP_FILE}" | cut -f1)
  echo "Backup size: ${BACKUP_SIZE}"

  # Verify backup integrity
  pg_restore --list "${BACKUP_FILE}" > /dev/null 2>&1
  if [ $? -eq 0 ]; then
    echo "Backup verification passed"
  else
    echo "ERROR: Backup verification failed!"
    exit 1
  fi
else
  echo "ERROR: Backup failed!"
  exit 1
fi

# Remove backups older than retention period
echo "Cleaning up old backups (older than ${RETENTION_DAYS} days)..."
find "${BACKUP_DIR}" -name "honua_*.sql.gz" -type f -mtime +${RETENTION_DAYS} -delete

echo "Backup completed at $(date)"
```

#### Advanced pg_dump with Parallel Jobs

For large databases (>100GB), use parallel dumps:

```bash
#!/bin/bash
# backup-postgres-parallel.sh

BACKUP_DIR="/backups/postgresql"
DB_HOST="postgis"
DB_NAME="honuadb"
DB_USER="honua"
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/honua_${BACKUP_DATE}.backup"
JOBS=4  # Number of parallel jobs

mkdir -p "${BACKUP_DIR}"

# Parallel directory-format dump
pg_dump \
  --host="${DB_HOST}" \
  --username="${DB_USER}" \
  --dbname="${DB_NAME}" \
  --format=directory \
  --jobs=${JOBS} \
  --compress=9 \
  --verbose \
  --file="${BACKUP_FILE}" \
  2>&1 | tee "${BACKUP_DIR}/backup_${BACKUP_DATE}.log"

if [ $? -eq 0 ]; then
  # Create tarball for transport
  tar -czf "${BACKUP_FILE}.tar.gz" -C "${BACKUP_DIR}" "$(basename ${BACKUP_FILE})"

  # Verify backup
  pg_restore --list "${BACKUP_FILE}" > /dev/null 2>&1

  echo "Parallel backup completed: ${BACKUP_FILE}.tar.gz"
else
  echo "ERROR: Parallel backup failed!"
  exit 1
fi
```

#### Schema-Only and Data-Only Backups

```bash
# Schema-only backup (for version control)
pg_dump \
  --host=postgis \
  --username=honua \
  --dbname=honuadb \
  --schema-only \
  --file=/backups/schema_$(date +%Y%m%d).sql

# Data-only backup (for data migration)
pg_dump \
  --host=postgis \
  --username=honua \
  --dbname=honuadb \
  --data-only \
  --format=custom \
  --file=/backups/data_$(date +%Y%m%d).backup

# Backup specific tables
pg_dump \
  --host=postgis \
  --username=honua \
  --dbname=honuadb \
  --table=parcels \
  --table=buildings \
  --format=custom \
  --file=/backups/spatial_tables_$(date +%Y%m%d).backup
```

### Method 2: Physical Backup with pg_basebackup

**Best for**: Production, PITR, fast recovery

#### Base Backup Creation

```bash
#!/bin/bash
# backup-postgres-physical.sh

BACKUP_DIR="/backups/postgresql/base"
DB_HOST="postgis"
DB_PORT="5432"
DB_USER="replication_user"  # User with REPLICATION privilege
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_PATH="${BACKUP_DIR}/${BACKUP_DATE}"

mkdir -p "${BACKUP_PATH}"

# Create base backup with WAL files
pg_basebackup \
  --host="${DB_HOST}" \
  --port="${DB_PORT}" \
  --username="${DB_USER}" \
  --pgdata="${BACKUP_PATH}" \
  --format=tar \
  --wal-method=fetch \
  --gzip \
  --compress=9 \
  --progress \
  --verbose \
  --checkpoint=fast \
  --label="honua_base_backup_${BACKUP_DATE}" \
  2>&1 | tee "${BACKUP_DIR}/basebackup_${BACKUP_DATE}.log"

if [ $? -eq 0 ]; then
  echo "Base backup completed: ${BACKUP_PATH}"

  # Create backup manifest
  cat > "${BACKUP_PATH}/backup_info.txt" <<EOF
Backup Date: ${BACKUP_DATE}
Database Host: ${DB_HOST}
Backup Type: Base Backup
Format: tar + gzip
WAL Method: fetch
EOF

  # Upload to S3 (optional)
  # aws s3 sync "${BACKUP_PATH}" "s3://honua-backups/postgresql/${BACKUP_DATE}/"
else
  echo "ERROR: Base backup failed!"
  exit 1
fi
```

#### Setup Replication User

```sql
-- Create replication user (run as superuser)
CREATE ROLE replication_user WITH REPLICATION LOGIN PASSWORD 'secure_password';

-- Grant necessary permissions
GRANT CONNECT ON DATABASE honuadb TO replication_user;

-- Configure pg_hba.conf to allow replication connections
-- Add to postgresql/data/pg_hba.conf:
-- host replication replication_user 0.0.0.0/0 md5
```

### Method 3: Docker Volume Backup

**Best for**: Docker/Docker Compose deployments

```bash
#!/bin/bash
# backup-docker-volume.sh

VOLUME_NAME="honua_postgis-data"
BACKUP_DIR="/backups/volumes"
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/postgis-volume_${BACKUP_DATE}.tar.gz"

mkdir -p "${BACKUP_DIR}"

# Stop Honua server (optional, for consistency)
# docker-compose stop honua

# Backup PostgreSQL volume
docker run --rm \
  --volume "${VOLUME_NAME}:/data:ro" \
  --volume "${BACKUP_DIR}:/backup" \
  alpine \
  tar -czf "/backup/$(basename ${BACKUP_FILE})" -C /data .

# Restart Honua server
# docker-compose start honua

if [ $? -eq 0 ]; then
  echo "Volume backup completed: ${BACKUP_FILE}"

  # Verify backup
  tar -tzf "${BACKUP_FILE}" > /dev/null 2>&1
  if [ $? -eq 0 ]; then
    echo "Backup verification passed"
  else
    echo "ERROR: Backup verification failed!"
  fi
else
  echo "ERROR: Volume backup failed!"
  exit 1
fi
```

### Restore from pg_dump

```bash
#!/bin/bash
# restore-postgres-logical.sh

BACKUP_FILE="/backups/postgresql/honua_20251004_120000.sql.gz"
DB_HOST="postgis"
DB_PORT="5432"
DB_NAME="honuadb"
DB_USER="postgres"  # Superuser for restore

# Stop application to prevent connections
docker-compose stop honua

# Drop and recreate database
psql --host="${DB_HOST}" --username="${DB_USER}" --dbname=postgres <<EOF
-- Terminate existing connections
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = '${DB_NAME}' AND pid <> pg_backend_pid();

-- Drop and recreate database
DROP DATABASE IF EXISTS ${DB_NAME};
CREATE DATABASE ${DB_NAME};

-- Install extensions
\c ${DB_NAME}
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
EOF

# Restore from backup
echo "Restoring database from ${BACKUP_FILE}..."
pg_restore \
  --host="${DB_HOST}" \
  --username="${DB_USER}" \
  --dbname="${DB_NAME}" \
  --verbose \
  --no-owner \
  --no-privileges \
  "${BACKUP_FILE}" \
  2>&1 | tee restore_$(date +%Y%m%d_%H%M%S).log

# Verify restore
psql --host="${DB_HOST}" --username="${DB_USER}" --dbname="${DB_NAME}" <<EOF
-- Check table count
SELECT COUNT(*) as table_count FROM information_schema.tables WHERE table_schema = 'public';

-- Verify PostGIS
SELECT PostGIS_Version();

-- Check spatial data
SELECT COUNT(*) FROM geometry_columns;
EOF

# Restart application
docker-compose start honua

echo "Restore completed successfully"
```

---

## Point-in-Time Recovery (PITR)

Point-in-Time Recovery allows restoring the database to any specific moment using base backups and WAL (Write-Ahead Log) archives.

**Benefits**:
- Recover from data corruption or accidental deletion
- Restore to exact point before incident
- Minimal data loss (RPO < 1 minute with continuous archiving)

### WAL Archiving Setup

#### Configure PostgreSQL for WAL Archiving

Edit `postgresql.conf`:

```ini
# WAL Configuration for PITR
# -------------------------

# Enable WAL archiving
wal_level = replica  # or 'logical' for logical replication
archive_mode = on
archive_command = 'test ! -f /archive/wal/%f && cp %p /archive/wal/%f'  # Local archive
# OR for S3:
# archive_command = 'aws s3 cp %p s3://honua-backups/wal/%f'

# WAL retention
wal_keep_size = 1GB  # Retain 1GB of WAL files
max_wal_senders = 5  # For replication/backup connections

# Checkpoint configuration (affects PITR granularity)
checkpoint_timeout = 15min
checkpoint_completion_target = 0.9

# Archive timeout (force WAL archive even if not full)
archive_timeout = 300  # Archive every 5 minutes

# Recovery configuration
restore_command = 'cp /archive/wal/%f %p'  # For restore
# OR for S3:
# restore_command = 'aws s3 cp s3://honua-backups/wal/%f %p'
```

#### Create Archive Directory

```bash
# Create WAL archive directory
sudo mkdir -p /archive/wal
sudo chown postgres:postgres /archive/wal
sudo chmod 700 /archive/wal

# For Docker deployment
docker exec honua-postgis mkdir -p /archive/wal
docker exec honua-postgis chown postgres:postgres /archive/wal
```

#### WAL Archive to S3 Script

```bash
#!/bin/bash
# wal-archive-s3.sh

WAL_FILE="$1"
WAL_PATH="$2"
S3_BUCKET="s3://honua-backups/wal"
MAX_RETRIES=3

for i in $(seq 1 $MAX_RETRIES); do
  aws s3 cp "${WAL_PATH}" "${S3_BUCKET}/${WAL_FILE}" \
    --storage-class STANDARD_IA \
    --metadata "archived=$(date -u +%Y-%m-%dT%H:%M:%SZ)"

  if [ $? -eq 0 ]; then
    exit 0
  fi

  echo "Retry ${i}/${MAX_RETRIES} failed, waiting..."
  sleep 5
done

echo "ERROR: Failed to archive WAL file ${WAL_FILE}"
exit 1
```

Update `archive_command` in `postgresql.conf`:

```ini
archive_command = '/usr/local/bin/wal-archive-s3.sh %f %p'
```

### Create Base Backup for PITR

```bash
#!/bin/bash
# create-pitr-base-backup.sh

BACKUP_DIR="/backups/pitr"
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_LABEL="pitr_base_${BACKUP_DATE}"

mkdir -p "${BACKUP_DIR}/${BACKUP_DATE}"

# Create base backup
pg_basebackup \
  --host=postgis \
  --username=replication_user \
  --pgdata="${BACKUP_DIR}/${BACKUP_DATE}" \
  --format=plain \
  --wal-method=stream \
  --checkpoint=fast \
  --progress \
  --verbose \
  --label="${BACKUP_LABEL}"

# Create backup info file
cat > "${BACKUP_DIR}/${BACKUP_DATE}/backup_label.txt" <<EOF
Backup Label: ${BACKUP_LABEL}
Backup Date: ${BACKUP_DATE}
Backup Type: PITR Base Backup
WAL Method: stream
Restore Instructions: Use with WAL archives for point-in-time recovery
EOF

echo "PITR base backup created: ${BACKUP_DIR}/${BACKUP_DATE}"
```

### Perform Point-in-Time Recovery

```bash
#!/bin/bash
# restore-pitr.sh

BASE_BACKUP="/backups/pitr/20251004_120000"
RECOVERY_TARGET_TIME="2025-10-04 14:30:00"  # Target recovery time
PGDATA="/var/lib/postgresql/data"
WAL_ARCHIVE="/archive/wal"

# Stop PostgreSQL
sudo systemctl stop postgresql

# Clear data directory
sudo rm -rf "${PGDATA}"/*

# Restore base backup
sudo cp -r "${BASE_BACKUP}"/* "${PGDATA}/"
sudo chown -R postgres:postgres "${PGDATA}"

# Create recovery configuration
sudo tee "${PGDATA}/postgresql.auto.conf" > /dev/null <<EOF
# Point-in-Time Recovery Configuration
restore_command = 'cp ${WAL_ARCHIVE}/%f %p'
recovery_target_time = '${RECOVERY_TARGET_TIME}'
recovery_target_action = promote
EOF

# Create recovery signal file (PostgreSQL 12+)
sudo touch "${PGDATA}/recovery.signal"

# Start PostgreSQL (will enter recovery mode)
sudo systemctl start postgresql

# Monitor recovery progress
echo "Monitoring recovery progress..."
while sudo -u postgres psql -c "SELECT pg_is_in_recovery();" | grep -q "t"; do
  echo "Still in recovery mode..."
  sleep 5
done

echo "Recovery completed. Database promoted to production."

# Verify recovery
sudo -u postgres psql -d honuadb <<EOF
-- Check recovery target
SELECT pg_last_wal_replay_lsn();

-- Verify data integrity
SELECT COUNT(*) FROM parcels;

-- Check latest transaction time
SELECT MAX(created_at) FROM audit_log;
EOF
```

### Automated PITR Recovery Script

```bash
#!/bin/bash
# automated-pitr-recovery.sh

set -e

# Configuration
BASE_BACKUP_DIR="/backups/pitr"
WAL_ARCHIVE_DIR="/archive/wal"
PGDATA="/var/lib/postgresql/data"
RECOVERY_TARGET="$1"  # Can be timestamp, XID, or LSN

if [ -z "$RECOVERY_TARGET" ]; then
  echo "Usage: $0 <recovery_target_time>"
  echo "Example: $0 '2025-10-04 14:30:00'"
  exit 1
fi

# Find latest base backup
LATEST_BACKUP=$(ls -td "${BASE_BACKUP_DIR}"/* | head -1)

echo "=== Point-in-Time Recovery ==="
echo "Base Backup: ${LATEST_BACKUP}"
echo "Recovery Target: ${RECOVERY_TARGET}"
echo "WAL Archive: ${WAL_ARCHIVE_DIR}"
echo ""

read -p "Proceed with recovery? (yes/no): " CONFIRM
if [ "$CONFIRM" != "yes" ]; then
  echo "Recovery cancelled"
  exit 0
fi

# Stop application
echo "Stopping application..."
docker-compose -f /opt/honua/docker-compose.yml stop honua

# Stop PostgreSQL
echo "Stopping PostgreSQL..."
sudo systemctl stop postgresql

# Backup current data (just in case)
echo "Backing up current data directory..."
sudo mv "${PGDATA}" "${PGDATA}.pre-recovery.$(date +%s)"

# Restore base backup
echo "Restoring base backup..."
sudo mkdir -p "${PGDATA}"
sudo cp -r "${LATEST_BACKUP}"/* "${PGDATA}/"
sudo chown -R postgres:postgres "${PGDATA}"

# Configure recovery
echo "Configuring PITR recovery..."
sudo tee "${PGDATA}/postgresql.auto.conf" > /dev/null <<EOF
restore_command = 'cp ${WAL_ARCHIVE_DIR}/%f %p'
recovery_target_time = '${RECOVERY_TARGET}'
recovery_target_action = promote
EOF

sudo touch "${PGDATA}/recovery.signal"

# Start PostgreSQL
echo "Starting PostgreSQL in recovery mode..."
sudo systemctl start postgresql

# Wait for recovery
echo "Waiting for recovery to complete..."
TIMEOUT=300  # 5 minutes
ELAPSED=0

while sudo -u postgres psql -c "SELECT pg_is_in_recovery();" 2>/dev/null | grep -q "t"; do
  if [ $ELAPSED -ge $TIMEOUT ]; then
    echo "ERROR: Recovery timeout after ${TIMEOUT} seconds"
    exit 1
  fi

  echo "Recovery in progress (${ELAPSED}s elapsed)..."
  sleep 5
  ELAPSED=$((ELAPSED + 5))
done

echo "Recovery completed successfully"

# Verify recovery
echo "Verifying recovery..."
sudo -u postgres psql -d honuadb <<EOF
SELECT 'Recovery LSN: ' || pg_last_wal_replay_lsn();
SELECT 'Database Size: ' || pg_size_pretty(pg_database_size('honuadb'));
SELECT 'Table Count: ' || COUNT(*) FROM information_schema.tables WHERE table_schema = 'public';
EOF

# Restart application
echo "Starting application..."
docker-compose -f /opt/honua/docker-compose.yml start honua

echo "=== PITR Recovery Complete ==="
```

---

## Metadata Backup

Honua metadata (collection definitions, service configurations) should be version-controlled and backed up separately.

### JSON/YAML Metadata Backup

```bash
#!/bin/bash
# backup-metadata.sh

METADATA_DIR="/app/config"
BACKUP_DIR="/backups/metadata"
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/metadata_${BACKUP_DATE}.tar.gz"

mkdir -p "${BACKUP_DIR}"

# Create metadata backup
tar -czf "${BACKUP_FILE}" \
  -C "$(dirname ${METADATA_DIR})" \
  "$(basename ${METADATA_DIR})"

if [ $? -eq 0 ]; then
  echo "Metadata backup created: ${BACKUP_FILE}"

  # Upload to S3
  aws s3 cp "${BACKUP_FILE}" "s3://honua-backups/metadata/"

  # Calculate checksum
  sha256sum "${BACKUP_FILE}" > "${BACKUP_FILE}.sha256"
else
  echo "ERROR: Metadata backup failed"
  exit 1
fi

# Cleanup old backups (keep 90 days)
find "${BACKUP_DIR}" -name "metadata_*.tar.gz" -mtime +90 -delete
```

### Git-Based Metadata Backup

**Recommended**: Store metadata in Git for version control

```bash
#!/bin/bash
# backup-metadata-git.sh

METADATA_DIR="/app/config"
GIT_REPO="git@github.com:your-org/honua-metadata.git"
BACKUP_BRANCH="backups/$(date +%Y%m%d_%H%M%S)"

cd "${METADATA_DIR}" || exit 1

# Initialize Git if not already
if [ ! -d ".git" ]; then
  git init
  git remote add origin "${GIT_REPO}"
fi

# Commit current state
git add .
git commit -m "Automated backup: $(date -u +%Y-%m-%dT%H:%M:%SZ)"

# Push to backup branch
git push origin "HEAD:${BACKUP_BRANCH}"

# Also update main branch
git push origin HEAD:main

echo "Metadata backed up to Git: ${BACKUP_BRANCH}"
```

### Database Metadata Backup

For database-stored metadata:

```bash
#!/bin/bash
# backup-database-metadata.sh

DB_HOST="postgis"
DB_NAME="honuadb"
DB_USER="honua"
BACKUP_DIR="/backups/metadata"
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p "${BACKUP_DIR}"

# Export metadata tables
pg_dump \
  --host="${DB_HOST}" \
  --username="${DB_USER}" \
  --dbname="${DB_NAME}" \
  --table=collections \
  --table=service_config \
  --table=authentication_providers \
  --format=custom \
  --file="${BACKUP_DIR}/metadata_${BACKUP_DATE}.backup"

# Export as JSON (human-readable)
psql --host="${DB_HOST}" --username="${DB_USER}" --dbname="${DB_NAME}" \
  --tuples-only --no-align --field-separator=',' \
  --command="COPY (SELECT row_to_json(t) FROM collections t) TO STDOUT" \
  > "${BACKUP_DIR}/collections_${BACKUP_DATE}.json"

echo "Database metadata backed up: ${BACKUP_DIR}"
```

---

## Tile Cache and Storage Backup

### S3 Tile Cache Backup

```bash
#!/bin/bash
# backup-s3-tiles.sh

SOURCE_BUCKET="honua-tiles-production"
BACKUP_BUCKET="honua-backups"
BACKUP_PREFIX="tiles/$(date +%Y%m%d)"

# Sync tiles to backup bucket
aws s3 sync \
  "s3://${SOURCE_BUCKET}/" \
  "s3://${BACKUP_BUCKET}/${BACKUP_PREFIX}/" \
  --storage-class GLACIER_IR \
  --metadata "backup-date=$(date -u +%Y-%m-%dT%H:%M:%SZ)"

echo "S3 tile cache backed up to s3://${BACKUP_BUCKET}/${BACKUP_PREFIX}/"

# Enable S3 versioning on source bucket (recommended)
aws s3api put-bucket-versioning \
  --bucket "${SOURCE_BUCKET}" \
  --versioning-configuration Status=Enabled

# Configure lifecycle policy for old versions
cat > lifecycle-policy.json <<EOF
{
  "Rules": [
    {
      "Id": "TransitionOldVersions",
      "Status": "Enabled",
      "NoncurrentVersionTransitions": [
        {
          "NoncurrentDays": 30,
          "StorageClass": "GLACIER_IR"
        },
        {
          "NoncurrentDays": 90,
          "StorageClass": "DEEP_ARCHIVE"
        }
      ],
      "NoncurrentVersionExpiration": {
        "NoncurrentDays": 365
      }
    }
  ]
}
EOF

aws s3api put-bucket-lifecycle-configuration \
  --bucket "${SOURCE_BUCKET}" \
  --lifecycle-configuration file://lifecycle-policy.json
```

### Azure Blob Tile Cache Backup

```bash
#!/bin/bash
# backup-azure-tiles.sh

SOURCE_CONTAINER="honua-tiles"
BACKUP_CONTAINER="honua-backups"
STORAGE_ACCOUNT="honuastorage"
BACKUP_PREFIX="tiles/$(date +%Y%m%d)"

# Copy tiles to backup container
az storage blob copy start-batch \
  --account-name "${STORAGE_ACCOUNT}" \
  --source-container "${SOURCE_CONTAINER}" \
  --destination-container "${BACKUP_CONTAINER}" \
  --destination-path "${BACKUP_PREFIX}/" \
  --tier Cool

echo "Azure tile cache backed up to ${BACKUP_CONTAINER}/${BACKUP_PREFIX}/"

# Enable soft delete (recommended)
az storage blob service-properties delete-policy update \
  --account-name "${STORAGE_ACCOUNT}" \
  --enable true \
  --days-retained 30

# Enable versioning
az storage account blob-service-properties update \
  --account-name "${STORAGE_ACCOUNT}" \
  --enable-versioning true
```

### Filesystem Tile Cache Backup

```bash
#!/bin/bash
# backup-filesystem-tiles.sh

TILES_DIR="/app/tiles"
BACKUP_DIR="/backups/tiles"
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/tiles_${BACKUP_DATE}.tar.gz"

mkdir -p "${BACKUP_DIR}"

# Create incremental backup using rsync
rsync -avz \
  --delete \
  --link-dest="${BACKUP_DIR}/latest" \
  "${TILES_DIR}/" \
  "${BACKUP_DIR}/${BACKUP_DATE}/"

# Update latest symlink
ln -snf "${BACKUP_DIR}/${BACKUP_DATE}" "${BACKUP_DIR}/latest"

echo "Filesystem tile cache backed up: ${BACKUP_DIR}/${BACKUP_DATE}"

# Optional: Create compressed archive
tar -czf "${BACKUP_FILE}" -C "${TILES_DIR}" .

# Upload to remote storage
# scp "${BACKUP_FILE}" backup-server:/backups/honua/tiles/
```

### Attachment Storage Backup

```bash
#!/bin/bash
# backup-attachments.sh

ATTACHMENTS_DIR="/app/attachments"
BACKUP_DIR="/backups/attachments"
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)
S3_BUCKET="s3://honua-backups/attachments"

mkdir -p "${BACKUP_DIR}"

# Incremental backup to local storage
rsync -avz \
  --delete \
  --link-dest="${BACKUP_DIR}/latest" \
  "${ATTACHMENTS_DIR}/" \
  "${BACKUP_DIR}/${BACKUP_DATE}/"

ln -snf "${BACKUP_DIR}/${BACKUP_DATE}" "${BACKUP_DIR}/latest"

# Sync to S3 with encryption
aws s3 sync \
  "${ATTACHMENTS_DIR}/" \
  "${S3_BUCKET}/$(date +%Y%m%d)/" \
  --storage-class STANDARD_IA \
  --server-side-encryption AES256

echo "Attachments backed up to ${S3_BUCKET}/$(date +%Y%m%d)/"
```

---

## Configuration and Secrets Backup

### Environment Variables Backup

```bash
#!/bin/bash
# backup-environment.sh

BACKUP_DIR="/backups/config"
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)
BACKUP_FILE="${BACKUP_DIR}/environment_${BACKUP_DATE}.enc"

mkdir -p "${BACKUP_DIR}"

# Export environment variables (excluding secrets)
docker-compose -f /opt/honua/docker-compose.yml config \
  | grep -E "HONUA__|ASPNETCORE__|Serilog__" \
  > "${BACKUP_DIR}/environment_${BACKUP_DATE}.txt"

# Encrypt backup
gpg --symmetric --cipher-algo AES256 \
  --output "${BACKUP_FILE}" \
  "${BACKUP_DIR}/environment_${BACKUP_DATE}.txt"

# Remove plaintext
rm "${BACKUP_DIR}/environment_${BACKUP_DATE}.txt"

echo "Environment variables backed up (encrypted): ${BACKUP_FILE}"
```

### Secrets Backup (Encrypted)

```bash
#!/bin/bash
# backup-secrets.sh

SECRETS_DIR="/opt/honua/secrets"
BACKUP_DIR="/backups/secrets"
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)
GPG_RECIPIENT="backup@honua.io"

mkdir -p "${BACKUP_DIR}"

# Create encrypted backup of secrets
tar -czf - -C "${SECRETS_DIR}" . \
  | gpg --encrypt --recipient "${GPG_RECIPIENT}" \
  > "${BACKUP_DIR}/secrets_${BACKUP_DATE}.tar.gz.gpg"

if [ $? -eq 0 ]; then
  echo "Secrets backed up (encrypted): ${BACKUP_DIR}/secrets_${BACKUP_DATE}.tar.gz.gpg"

  # Upload to secure storage
  aws s3 cp "${BACKUP_DIR}/secrets_${BACKUP_DATE}.tar.gz.gpg" \
    "s3://honua-secrets-backup/" \
    --server-side-encryption aws:kms \
    --ssekms-key-id "arn:aws:kms:us-east-1:123456789012:key/abc123"
else
  echo "ERROR: Secrets backup failed"
  exit 1
fi
```

### Kubernetes Secrets Backup

```bash
#!/bin/bash
# backup-k8s-secrets.sh

NAMESPACE="honua-prod"
BACKUP_DIR="/backups/kubernetes"
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)

mkdir -p "${BACKUP_DIR}"

# Export all secrets in namespace
kubectl get secrets -n "${NAMESPACE}" -o yaml \
  > "${BACKUP_DIR}/secrets_${BACKUP_DATE}.yaml"

# Encrypt backup
gpg --symmetric --cipher-algo AES256 \
  --output "${BACKUP_DIR}/secrets_${BACKUP_DATE}.yaml.gpg" \
  "${BACKUP_DIR}/secrets_${BACKUP_DATE}.yaml"

rm "${BACKUP_DIR}/secrets_${BACKUP_DATE}.yaml"

echo "Kubernetes secrets backed up: ${BACKUP_DIR}/secrets_${BACKUP_DATE}.yaml.gpg"
```

---

## Cloud-Specific Backup Solutions

### AWS RDS Automated Backups

#### Configure RDS Backups

```bash
#!/bin/bash
# configure-rds-backups.sh

DB_INSTANCE="honua-postgis-prod"
BACKUP_RETENTION_DAYS=7
BACKUP_WINDOW="03:00-04:00"  # UTC
MAINTENANCE_WINDOW="sun:04:00-sun:05:00"  # UTC

# Modify RDS instance for automated backups
aws rds modify-db-instance \
  --db-instance-identifier "${DB_INSTANCE}" \
  --backup-retention-period ${BACKUP_RETENTION_DAYS} \
  --preferred-backup-window "${BACKUP_WINDOW}" \
  --preferred-maintenance-window "${MAINTENANCE_WINDOW}" \
  --enable-cloudwatch-logs-exports postgresql upgrade \
  --apply-immediately

echo "RDS automated backups configured for ${DB_INSTANCE}"
```

#### Create Manual RDS Snapshot

```bash
#!/bin/bash
# create-rds-snapshot.sh

DB_INSTANCE="honua-postgis-prod"
SNAPSHOT_ID="honua-manual-$(date +%Y%m%d-%H%M%S)"

# Create snapshot
aws rds create-db-snapshot \
  --db-instance-identifier "${DB_INSTANCE}" \
  --db-snapshot-identifier "${SNAPSHOT_ID}" \
  --tags Key=Type,Value=Manual Key=Purpose,Value=Backup

echo "Creating RDS snapshot: ${SNAPSHOT_ID}"

# Wait for snapshot completion
aws rds wait db-snapshot-completed \
  --db-snapshot-identifier "${SNAPSHOT_ID}"

echo "Snapshot completed: ${SNAPSHOT_ID}"

# Copy snapshot to another region
aws rds copy-db-snapshot \
  --source-db-snapshot-identifier "arn:aws:rds:us-east-1:123456789012:snapshot:${SNAPSHOT_ID}" \
  --target-db-snapshot-identifier "${SNAPSHOT_ID}" \
  --region us-west-2 \
  --kms-key-id "arn:aws:kms:us-west-2:123456789012:key/xyz789"

echo "Snapshot copied to us-west-2"
```

#### Restore from RDS Snapshot

```bash
#!/bin/bash
# restore-rds-snapshot.sh

SNAPSHOT_ID="honua-manual-20251004-120000"
NEW_INSTANCE="honua-postgis-restored"
DB_SUBNET_GROUP="honua-db-subnet-group"
SECURITY_GROUPS="sg-abc123"

# Restore RDS instance from snapshot
aws rds restore-db-instance-from-db-snapshot \
  --db-instance-identifier "${NEW_INSTANCE}" \
  --db-snapshot-identifier "${SNAPSHOT_ID}" \
  --db-instance-class db.r6g.xlarge \
  --db-subnet-group-name "${DB_SUBNET_GROUP}" \
  --vpc-security-group-ids "${SECURITY_GROUPS}" \
  --publicly-accessible false \
  --multi-az true \
  --storage-type gp3 \
  --iops 3000

echo "Restoring RDS instance from snapshot: ${SNAPSHOT_ID}"

# Wait for instance availability
aws rds wait db-instance-available \
  --db-instance-identifier "${NEW_INSTANCE}"

# Get endpoint
ENDPOINT=$(aws rds describe-db-instances \
  --db-instance-identifier "${NEW_INSTANCE}" \
  --query "DBInstances[0].Endpoint.Address" \
  --output text)

echo "RDS instance restored: ${ENDPOINT}"
```

### Azure Database for PostgreSQL Backup

#### Configure Azure Backup

```bash
#!/bin/bash
# configure-azure-backup.sh

RESOURCE_GROUP="honua-rg"
SERVER_NAME="honua-postgis"
BACKUP_RETENTION_DAYS=35
GEO_REDUNDANT="Enabled"

# Configure backup settings
az postgres server update \
  --resource-group "${RESOURCE_GROUP}" \
  --name "${SERVER_NAME}" \
  --backup-retention ${BACKUP_RETENTION_DAYS} \
  --geo-redundant-backup "${GEO_REDUNDANT}"

echo "Azure PostgreSQL backup configured for ${SERVER_NAME}"
```

#### Create Azure Database Snapshot

```bash
#!/bin/bash
# create-azure-snapshot.sh

RESOURCE_GROUP="honua-rg"
SERVER_NAME="honua-postgis"
BACKUP_NAME="honua-manual-$(date +%Y%m%d-%H%M%S)"

# Trigger manual backup (not directly available, use point-in-time restore)
# Azure provides automatic backups only

# Alternative: Export database
az postgres db export \
  --resource-group "${RESOURCE_GROUP}" \
  --server-name "${SERVER_NAME}" \
  --name honuadb \
  --admin-user honua \
  --admin-password "${DB_PASSWORD}" \
  --output-directory /backups/azure/

echo "Azure database exported to /backups/azure/"
```

### S3 Versioning and Lifecycle Policies

```bash
#!/bin/bash
# configure-s3-backup-lifecycle.sh

BACKUP_BUCKET="honua-backups"

# Enable versioning
aws s3api put-bucket-versioning \
  --bucket "${BACKUP_BUCKET}" \
  --versioning-configuration Status=Enabled

# Configure lifecycle policy
cat > s3-lifecycle-policy.json <<EOF
{
  "Rules": [
    {
      "Id": "TransitionToIA",
      "Status": "Enabled",
      "Transitions": [
        {
          "Days": 30,
          "StorageClass": "STANDARD_IA"
        },
        {
          "Days": 90,
          "StorageClass": "GLACIER_IR"
        },
        {
          "Days": 180,
          "StorageClass": "DEEP_ARCHIVE"
        }
      ],
      "Expiration": {
        "Days": 2555
      },
      "NoncurrentVersionTransitions": [
        {
          "NoncurrentDays": 30,
          "StorageClass": "GLACIER_IR"
        }
      ],
      "NoncurrentVersionExpiration": {
        "NoncurrentDays": 90
      }
    }
  ]
}
EOF

aws s3api put-bucket-lifecycle-configuration \
  --bucket "${BACKUP_BUCKET}" \
  --lifecycle-configuration file://s3-lifecycle-policy.json

echo "S3 backup lifecycle policy configured for ${BACKUP_BUCKET}"
```

---

## Backup Automation

### Complete Backup Script (All Components)

```bash
#!/bin/bash
# backup-all.sh - Complete Honua backup

set -e

# Configuration
BACKUP_ROOT="/backups"
BACKUP_DATE=$(date +%Y%m%d_%H%M%S)
LOG_FILE="${BACKUP_ROOT}/logs/backup_${BACKUP_DATE}.log"

mkdir -p "${BACKUP_ROOT}"/{postgresql,metadata,tiles,attachments,config,logs}

# Logging function
log() {
  echo "[$(date +'%Y-%m-%d %H:%M:%S')] $1" | tee -a "${LOG_FILE}"
}

log "=== Starting Honua Complete Backup ==="

# 1. PostgreSQL Database Backup
log "Step 1/5: Backing up PostgreSQL database..."
pg_dump \
  --host=postgis \
  --username=honua \
  --dbname=honuadb \
  --format=custom \
  --compress=9 \
  --file="${BACKUP_ROOT}/postgresql/honua_${BACKUP_DATE}.backup" \
  2>&1 | tee -a "${LOG_FILE}"

if [ ${PIPESTATUS[0]} -eq 0 ]; then
  log "✓ Database backup completed"
else
  log "✗ Database backup failed"
  exit 1
fi

# 2. Metadata Backup
log "Step 2/5: Backing up metadata..."
tar -czf "${BACKUP_ROOT}/metadata/metadata_${BACKUP_DATE}.tar.gz" \
  -C /app/config . \
  2>&1 | tee -a "${LOG_FILE}"

if [ ${PIPESTATUS[0]} -eq 0 ]; then
  log "✓ Metadata backup completed"
else
  log "✗ Metadata backup failed"
  exit 1
fi

# 3. Tile Cache Backup
log "Step 3/5: Backing up tile cache..."
rsync -avz --delete \
  --link-dest="${BACKUP_ROOT}/tiles/latest" \
  /app/tiles/ \
  "${BACKUP_ROOT}/tiles/${BACKUP_DATE}/" \
  2>&1 | tee -a "${LOG_FILE}"

ln -snf "${BACKUP_ROOT}/tiles/${BACKUP_DATE}" "${BACKUP_ROOT}/tiles/latest"

if [ ${PIPESTATUS[0]} -eq 0 ]; then
  log "✓ Tile cache backup completed"
else
  log "✗ Tile cache backup failed"
  exit 1
fi

# 4. Attachments Backup
log "Step 4/5: Backing up attachments..."
rsync -avz --delete \
  --link-dest="${BACKUP_ROOT}/attachments/latest" \
  /app/attachments/ \
  "${BACKUP_ROOT}/attachments/${BACKUP_DATE}/" \
  2>&1 | tee -a "${LOG_FILE}"

ln -snf "${BACKUP_ROOT}/attachments/${BACKUP_DATE}" "${BACKUP_ROOT}/attachments/latest"

if [ ${PIPESTATUS[0]} -eq 0 ]; then
  log "✓ Attachments backup completed"
else
  log "✗ Attachments backup failed"
  exit 1
fi

# 5. Configuration Backup
log "Step 5/5: Backing up configuration..."
docker-compose -f /opt/honua/docker-compose.yml config \
  > "${BACKUP_ROOT}/config/docker-compose_${BACKUP_DATE}.yml"

if [ ${PIPESTATUS[0]} -eq 0 ]; then
  log "✓ Configuration backup completed"
else
  log "✗ Configuration backup failed"
  exit 1
fi

# Upload to S3 (optional)
if command -v aws &> /dev/null; then
  log "Uploading backups to S3..."
  aws s3 sync "${BACKUP_ROOT}" "s3://honua-backups/complete/${BACKUP_DATE}/" \
    --exclude "logs/*" \
    --exclude "tiles/*" \
    --storage-class STANDARD_IA \
    2>&1 | tee -a "${LOG_FILE}"

  if [ ${PIPESTATUS[0]} -eq 0 ]; then
    log "✓ S3 upload completed"
  else
    log "✗ S3 upload failed (continuing anyway)"
  fi
fi

# Cleanup old backups (keep 30 days locally)
log "Cleaning up old backups..."
find "${BACKUP_ROOT}/postgresql" -name "honua_*.backup" -mtime +30 -delete
find "${BACKUP_ROOT}/metadata" -name "metadata_*.tar.gz" -mtime +30 -delete
find "${BACKUP_ROOT}/config" -name "docker-compose_*.yml" -mtime +30 -delete

# Generate backup manifest
cat > "${BACKUP_ROOT}/manifest_${BACKUP_DATE}.txt" <<EOF
Honua Backup Manifest
Date: ${BACKUP_DATE}
Backup Components:
- Database: ${BACKUP_ROOT}/postgresql/honua_${BACKUP_DATE}.backup
- Metadata: ${BACKUP_ROOT}/metadata/metadata_${BACKUP_DATE}.tar.gz
- Tiles: ${BACKUP_ROOT}/tiles/${BACKUP_DATE}/
- Attachments: ${BACKUP_ROOT}/attachments/${BACKUP_DATE}/
- Config: ${BACKUP_ROOT}/config/docker-compose_${BACKUP_DATE}.yml

Backup Size:
$(du -sh "${BACKUP_ROOT}/postgresql/honua_${BACKUP_DATE}.backup" | cut -f1) - Database
$(du -sh "${BACKUP_ROOT}/metadata/metadata_${BACKUP_DATE}.tar.gz" | cut -f1) - Metadata
$(du -sh "${BACKUP_ROOT}/tiles/${BACKUP_DATE}" | cut -f1) - Tiles
$(du -sh "${BACKUP_ROOT}/attachments/${BACKUP_DATE}" | cut -f1) - Attachments

Total: $(du -sh "${BACKUP_ROOT}" | cut -f1)
EOF

log "=== Backup Completed Successfully ==="
log "Manifest: ${BACKUP_ROOT}/manifest_${BACKUP_DATE}.txt"
```

### Cron Job Scheduling

```bash
# crontab -e

# Daily full backup at 2 AM
0 2 * * * /usr/local/bin/backup-all.sh >> /var/log/honua-backup.log 2>&1

# Hourly database backup (incremental with PITR)
0 * * * * /usr/local/bin/backup-postgres-physical.sh >> /var/log/honua-backup-hourly.log 2>&1

# Weekly metadata backup at Sunday 3 AM
0 3 * * 0 /usr/local/bin/backup-metadata.sh >> /var/log/honua-metadata-backup.log 2>&1

# Monthly full system backup (all components)
0 1 1 * * /usr/local/bin/backup-all.sh >> /var/log/honua-full-backup.log 2>&1
```

### Systemd Timer (Alternative to Cron)

Create `/etc/systemd/system/honua-backup.service`:

```ini
[Unit]
Description=Honua Complete Backup
After=network.target postgresql.service

[Service]
Type=oneshot
User=root
ExecStart=/usr/local/bin/backup-all.sh
StandardOutput=append:/var/log/honua-backup.log
StandardError=append:/var/log/honua-backup.log
```

Create `/etc/systemd/system/honua-backup.timer`:

```ini
[Unit]
Description=Honua Backup Timer
Requires=honua-backup.service

[Timer]
OnCalendar=daily
OnCalendar=02:00
Persistent=true

[Install]
WantedBy=timers.target
```

Enable and start timer:

```bash
sudo systemctl daemon-reload
sudo systemctl enable honua-backup.timer
sudo systemctl start honua-backup.timer

# Check timer status
sudo systemctl list-timers honua-backup.timer
```

---

## Disaster Recovery Procedures

### Full System Recovery

**Scenario**: Complete infrastructure loss (data center failure, region outage)

```bash
#!/bin/bash
# disaster-recovery-full.sh

set -e

echo "=== Honua Disaster Recovery - Full System Restore ==="

# Step 1: Provision Infrastructure
echo "Step 1: Provisioning infrastructure..."

# AWS Example
terraform apply \
  -var="environment=production" \
  -var="region=us-west-2" \
  -auto-approve

# Wait for infrastructure
sleep 60

# Step 2: Restore Database
echo "Step 2: Restoring PostgreSQL database..."

# Find latest RDS snapshot
LATEST_SNAPSHOT=$(aws rds describe-db-snapshots \
  --db-instance-identifier honua-postgis-prod \
  --query "DBSnapshots | sort_by(@, &SnapshotCreateTime) | [-1].DBSnapshotIdentifier" \
  --output text)

# Restore from snapshot
aws rds restore-db-instance-from-db-snapshot \
  --db-instance-identifier honua-postgis-dr \
  --db-snapshot-identifier "${LATEST_SNAPSHOT}" \
  --db-instance-class db.r6g.xlarge \
  --multi-az true

# Wait for database availability
aws rds wait db-instance-available \
  --db-instance-identifier honua-postgis-dr

DB_ENDPOINT=$(aws rds describe-db-instances \
  --db-instance-identifier honua-postgis-dr \
  --query "DBInstances[0].Endpoint.Address" \
  --output text)

echo "Database restored: ${DB_ENDPOINT}"

# Step 3: Restore Metadata
echo "Step 3: Restoring metadata..."

# Download latest metadata backup from S3
aws s3 cp "s3://honua-backups/metadata/" \
  /tmp/metadata-restore/ \
  --recursive \
  --exclude "*" \
  --include "metadata_*.tar.gz" \
  --region us-west-2

LATEST_METADATA=$(ls -t /tmp/metadata-restore/metadata_*.tar.gz | head -1)
tar -xzf "${LATEST_METADATA}" -C /opt/honua/config/

echo "Metadata restored from ${LATEST_METADATA}"

# Step 4: Restore Tile Cache
echo "Step 4: Restoring tile cache..."

# Restore from S3
aws s3 sync \
  "s3://honua-backups/tiles/latest/" \
  /opt/honua/tiles/ \
  --region us-west-2

echo "Tile cache restored"

# Step 5: Restore Attachments
echo "Step 5: Restoring attachments..."

aws s3 sync \
  "s3://honua-backups/attachments/latest/" \
  /opt/honua/attachments/ \
  --region us-west-2

echo "Attachments restored"

# Step 6: Deploy Application
echo "Step 6: Deploying Honua application..."

# Update database endpoint in environment
export HONUA__DATABASE__CONNECTIONSTRING="Host=${DB_ENDPOINT};Database=honuadb;Username=honua;Password=${DB_PASSWORD};Pooling=true"

# Deploy with Docker Compose
docker-compose -f /opt/honua/docker-compose.yml up -d

# Wait for health check
sleep 30

# Verify deployment
curl -f http://localhost:8080/health

if [ $? -eq 0 ]; then
  echo "✓ Honua application deployed successfully"
else
  echo "✗ Honua deployment failed"
  exit 1
fi

# Step 7: Update DNS (manual step)
echo "Step 7: Update DNS to point to new infrastructure"
echo "New endpoint: ${DB_ENDPOINT}"
echo "New application URL: $(terraform output application_url)"

echo "=== Disaster Recovery Completed ==="
```

### Database-Only Recovery

**Scenario**: Database corruption or data loss

```bash
#!/bin/bash
# recover-database-only.sh

set -e

BACKUP_FILE="$1"
DB_HOST="postgis"
DB_NAME="honuadb"
DB_USER="postgres"

if [ -z "$BACKUP_FILE" ]; then
  echo "Usage: $0 <backup_file>"
  exit 1
fi

echo "=== Database Recovery ==="
echo "Backup file: ${BACKUP_FILE}"

read -p "This will DESTROY current database. Continue? (yes/no): " CONFIRM
if [ "$CONFIRM" != "yes" ]; then
  echo "Recovery cancelled"
  exit 0
fi

# Stop application
echo "Stopping application..."
docker-compose -f /opt/honua/docker-compose.yml stop honua

# Terminate existing connections
psql --host="${DB_HOST}" --username="${DB_USER}" --dbname=postgres <<EOF
SELECT pg_terminate_backend(pid)
FROM pg_stat_activity
WHERE datname = '${DB_NAME}' AND pid <> pg_backend_pid();
EOF

# Drop and recreate database
echo "Dropping and recreating database..."
psql --host="${DB_HOST}" --username="${DB_USER}" --dbname=postgres <<EOF
DROP DATABASE IF EXISTS ${DB_NAME};
CREATE DATABASE ${DB_NAME};

\c ${DB_NAME}
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;
CREATE EXTENSION IF NOT EXISTS pg_stat_statements;
EOF

# Restore from backup
echo "Restoring from backup..."
pg_restore \
  --host="${DB_HOST}" \
  --username="${DB_USER}" \
  --dbname="${DB_NAME}" \
  --verbose \
  --no-owner \
  --no-privileges \
  "${BACKUP_FILE}"

if [ $? -eq 0 ]; then
  echo "✓ Database restored successfully"
else
  echo "✗ Database restore failed"
  exit 1
fi

# Verify restore
echo "Verifying restore..."
psql --host="${DB_HOST}" --username="${DB_USER}" --dbname="${DB_NAME}" <<EOF
-- Table count
SELECT COUNT(*) as table_count FROM information_schema.tables WHERE table_schema = 'public';

-- Spatial data
SELECT COUNT(*) as spatial_tables FROM geometry_columns;

-- PostGIS version
SELECT PostGIS_Version();
EOF

# Restart application
echo "Starting application..."
docker-compose -f /opt/honua/docker-compose.yml start honua

# Wait for health check
sleep 30
curl -f http://localhost:8080/health

echo "=== Database Recovery Completed ==="
```

### Partial Recovery (Single Table)

```bash
#!/bin/bash
# recover-single-table.sh

BACKUP_FILE="$1"
TABLE_NAME="$2"
DB_HOST="postgis"
DB_NAME="honuadb"
DB_USER="honua"

if [ -z "$TABLE_NAME" ]; then
  echo "Usage: $0 <backup_file> <table_name>"
  exit 1
fi

# Create temporary database
TEMP_DB="restore_temp_$$"

psql --host="${DB_HOST}" --username="${DB_USER}" --dbname=postgres <<EOF
CREATE DATABASE ${TEMP_DB};
\c ${TEMP_DB}
CREATE EXTENSION IF NOT EXISTS postgis;
EOF

# Restore full backup to temp database
pg_restore \
  --host="${DB_HOST}" \
  --username="${DB_USER}" \
  --dbname="${TEMP_DB}" \
  --table="${TABLE_NAME}" \
  "${BACKUP_FILE}"

# Copy table to production
psql --host="${DB_HOST}" --username="${DB_USER}" --dbname="${DB_NAME}" <<EOF
-- Backup current table
CREATE TABLE ${TABLE_NAME}_backup_$(date +%s) AS SELECT * FROM ${TABLE_NAME};

-- Truncate target table
TRUNCATE TABLE ${TABLE_NAME} CASCADE;

-- Copy data from temp database
INSERT INTO ${TABLE_NAME}
SELECT * FROM dblink(
  'dbname=${TEMP_DB} host=${DB_HOST} user=${DB_USER}',
  'SELECT * FROM ${TABLE_NAME}'
) AS t(...);  -- Specify column list

-- Verify row count
SELECT COUNT(*) FROM ${TABLE_NAME};
EOF

# Cleanup temp database
psql --host="${DB_HOST}" --username="${DB_USER}" --dbname=postgres <<EOF
DROP DATABASE ${TEMP_DB};
EOF

echo "Table ${TABLE_NAME} restored successfully"
```

### Multi-Region Failover

```bash
#!/bin/bash
# failover-to-secondary-region.sh

PRIMARY_REGION="us-east-1"
SECONDARY_REGION="us-west-2"
ROUTE53_HOSTED_ZONE="Z1234567890ABC"
DNS_NAME="api.honua.io"

echo "=== Multi-Region Failover ==="

# Promote read replica to primary
echo "Promoting secondary database to primary..."
aws rds promote-read-replica \
  --db-instance-identifier honua-postgis-west \
  --region "${SECONDARY_REGION}"

# Wait for promotion
aws rds wait db-instance-available \
  --db-instance-identifier honua-postgis-west \
  --region "${SECONDARY_REGION}"

# Get new endpoint
NEW_ENDPOINT=$(aws rds describe-db-instances \
  --db-instance-identifier honua-postgis-west \
  --region "${SECONDARY_REGION}" \
  --query "DBInstances[0].Endpoint.Address" \
  --output text)

echo "New database endpoint: ${NEW_ENDPOINT}"

# Update Route53 DNS
echo "Updating DNS to point to secondary region..."
cat > /tmp/dns-change.json <<EOF
{
  "Changes": [
    {
      "Action": "UPSERT",
      "ResourceRecordSet": {
        "Name": "${DNS_NAME}",
        "Type": "CNAME",
        "TTL": 60,
        "ResourceRecords": [
          {
            "Value": "honua-west-alb-123456.us-west-2.elb.amazonaws.com"
          }
        ]
      }
    }
  ]
}
EOF

aws route53 change-resource-record-sets \
  --hosted-zone-id "${ROUTE53_HOSTED_ZONE}" \
  --change-batch file:///tmp/dns-change.json

echo "DNS updated to secondary region"

# Scale up secondary region capacity
echo "Scaling secondary region..."
aws ecs update-service \
  --cluster honua-west \
  --service honua-server \
  --desired-count 6 \
  --region "${SECONDARY_REGION}"

echo "=== Failover Completed ==="
echo "Primary region: ${SECONDARY_REGION}"
echo "Database: ${NEW_ENDPOINT}"
```

---

## Recovery Scenarios

### Scenario 1: Accidental Data Deletion

**Problem**: User accidentally deleted critical records

**Solution**: Point-in-Time Recovery

```bash
# Identify deletion time
DELETION_TIME="2025-10-04 14:25:00"

# Restore to point before deletion
/usr/local/bin/automated-pitr-recovery.sh "${DELETION_TIME}"

# Verify data is restored
psql -h postgis -U honua -d honuadb -c "SELECT COUNT(*) FROM deleted_table;"
```

### Scenario 2: Database Corruption

**Problem**: PostgreSQL data corruption detected

**Solution**: Restore from latest backup

```bash
# Find latest backup
LATEST_BACKUP=$(ls -t /backups/postgresql/honua_*.backup | head -1)

# Perform recovery
/usr/local/bin/recover-database-only.sh "${LATEST_BACKUP}"

# Run integrity checks
psql -h postgis -U honua -d honuadb <<EOF
-- Check for corruption
SELECT * FROM pg_stat_database WHERE datname = 'honuadb';

-- Reindex all tables
REINDEX DATABASE honuadb;

-- Vacuum and analyze
VACUUM ANALYZE;
EOF
```

### Scenario 3: Region Failure (AWS)

**Problem**: Entire AWS region unavailable

**Solution**: Failover to secondary region

```bash
# Execute multi-region failover
/usr/local/bin/failover-to-secondary-region.sh

# Monitor failover progress
watch -n 5 'aws ecs describe-services \
  --cluster honua-west \
  --services honua-server \
  --region us-west-2 \
  --query "services[0].runningCount"'

# Verify application health
curl -f https://api.honua.io/health
```

### Scenario 4: Ransomware Attack

**Problem**: Files encrypted by ransomware

**Solution**: Restore from offline backups

```bash
# Restore from S3 Glacier (offline backup)
BACKUP_DATE="20251001"  # Date before attack

# Request Glacier restore
aws s3api restore-object \
  --bucket honua-backups \
  --key "postgresql/honua_${BACKUP_DATE}.backup" \
  --restore-request Days=7,GlacierJobParameters={Tier=Expedited}

# Wait for restore (1-5 minutes for expedited)
# Check restore status
aws s3api head-object \
  --bucket honua-backups \
  --key "postgresql/honua_${BACKUP_DATE}.backup"

# Download and restore
aws s3 cp "s3://honua-backups/postgresql/honua_${BACKUP_DATE}.backup" /tmp/
/usr/local/bin/recover-database-only.sh "/tmp/honua_${BACKUP_DATE}.backup"
```

### Scenario 5: Configuration Error

**Problem**: Misconfiguration broke service

**Solution**: Rollback configuration

```bash
# Find last known good configuration
LAST_GOOD_CONFIG=$(ls -t /backups/config/docker-compose_*.yml | head -2 | tail -1)

# Restore configuration
cp "${LAST_GOOD_CONFIG}" /opt/honua/docker-compose.yml

# Restart services
docker-compose -f /opt/honua/docker-compose.yml up -d

# Verify
docker-compose -f /opt/honua/docker-compose.yml ps
curl -f http://localhost:8080/health
```

---

## Testing and Validation

### Backup Validation Script

```bash
#!/bin/bash
# validate-backup.sh

BACKUP_FILE="$1"

if [ -z "$BACKUP_FILE" ]; then
  echo "Usage: $0 <backup_file>"
  exit 1
fi

echo "=== Backup Validation ==="
echo "File: ${BACKUP_FILE}"

# Check file exists
if [ ! -f "$BACKUP_FILE" ]; then
  echo "✗ Backup file not found"
  exit 1
fi
echo "✓ Backup file exists"

# Check file size
FILE_SIZE=$(stat -f%z "$BACKUP_FILE" 2>/dev/null || stat -c%s "$BACKUP_FILE")
if [ "$FILE_SIZE" -lt 1000000 ]; then  # Less than 1MB
  echo "✗ Backup file suspiciously small: ${FILE_SIZE} bytes"
  exit 1
fi
echo "✓ Backup file size: $(numfmt --to=iec-i --suffix=B $FILE_SIZE)"

# Verify backup integrity
echo "Verifying backup integrity..."
pg_restore --list "$BACKUP_FILE" > /tmp/backup_contents.txt 2>&1

if [ $? -eq 0 ]; then
  TABLE_COUNT=$(grep -c "TABLE DATA" /tmp/backup_contents.txt || true)
  echo "✓ Backup integrity verified"
  echo "  Tables: ${TABLE_COUNT}"
else
  echo "✗ Backup integrity check failed"
  exit 1
fi

# Test restore to temporary database
TEST_DB="test_restore_$$"
DB_HOST="postgis"
DB_USER="postgres"

echo "Testing restore to temporary database..."
psql --host="${DB_HOST}" --username="${DB_USER}" --dbname=postgres <<EOF
CREATE DATABASE ${TEST_DB};
\c ${TEST_DB}
CREATE EXTENSION IF NOT EXISTS postgis;
EOF

pg_restore \
  --host="${DB_HOST}" \
  --username="${DB_USER}" \
  --dbname="${TEST_DB}" \
  --verbose \
  "$BACKUP_FILE" \
  > /tmp/restore_test.log 2>&1

if [ $? -eq 0 ]; then
  echo "✓ Test restore successful"

  # Verify data
  ROW_COUNT=$(psql --host="${DB_HOST}" --username="${DB_USER}" --dbname="${TEST_DB}" \
    --tuples-only --no-align \
    --command="SELECT SUM(n_live_tup) FROM pg_stat_user_tables;")

  echo "  Total rows: ${ROW_COUNT}"
else
  echo "✗ Test restore failed"
  cat /tmp/restore_test.log
  exit 1
fi

# Cleanup
psql --host="${DB_HOST}" --username="${DB_USER}" --dbname=postgres <<EOF
DROP DATABASE ${TEST_DB};
EOF

echo "=== Backup Validation Completed Successfully ==="
```

### Automated Recovery Testing

```bash
#!/bin/bash
# automated-recovery-test.sh

set -e

echo "=== Automated Recovery Test ==="

# Configuration
TEST_ENV="staging"
BACKUP_FILE=$(ls -t /backups/postgresql/honua_*.backup | head -1)
TEST_DB_HOST="postgis-test"

# Step 1: Create test environment
echo "Step 1: Creating test environment..."
docker-compose -f docker-compose.test.yml up -d postgis-test

sleep 30

# Step 2: Restore backup
echo "Step 2: Restoring from backup..."
pg_restore \
  --host="${TEST_DB_HOST}" \
  --username=postgres \
  --dbname=honuadb \
  --create \
  --clean \
  --if-exists \
  "${BACKUP_FILE}"

# Step 3: Start application
echo "Step 3: Starting Honua application..."
export HONUA__DATABASE__CONNECTIONSTRING="Host=${TEST_DB_HOST};Database=honuadb;Username=honua;Password=test123"
docker-compose -f docker-compose.test.yml up -d honua-test

sleep 30

# Step 4: Run health checks
echo "Step 4: Running health checks..."
HEALTH_STATUS=$(curl -s -o /dev/null -w "%{http_code}" http://localhost:8081/health)

if [ "$HEALTH_STATUS" == "200" ]; then
  echo "✓ Health check passed"
else
  echo "✗ Health check failed: HTTP ${HEALTH_STATUS}"
  exit 1
fi

# Step 5: Verify data
echo "Step 5: Verifying data integrity..."
curl -s http://localhost:8081/ogc/collections | jq -e '.collections | length > 0'

if [ $? -eq 0 ]; then
  echo "✓ Data verification passed"
else
  echo "✗ Data verification failed"
  exit 1
fi

# Step 6: Cleanup
echo "Step 6: Cleaning up test environment..."
docker-compose -f docker-compose.test.yml down -v

echo "=== Recovery Test Completed Successfully ==="
```

---

## Monitoring and Alerting

### Backup Monitoring Script

```bash
#!/bin/bash
# monitor-backups.sh

BACKUP_DIR="/backups"
MAX_AGE_HOURS=25  # Alert if no backup in 25 hours
ALERT_EMAIL="ops@honua.io"
SLACK_WEBHOOK="https://hooks.slack.com/services/YOUR/WEBHOOK/URL"

# Check last backup time
LAST_BACKUP=$(find "${BACKUP_DIR}/postgresql" -name "honua_*.backup" -type f -printf '%T@ %p\n' | sort -n | tail -1)
LAST_BACKUP_TIME=$(echo "$LAST_BACKUP" | cut -d' ' -f1)
LAST_BACKUP_FILE=$(echo "$LAST_BACKUP" | cut -d' ' -f2)
CURRENT_TIME=$(date +%s)
AGE_SECONDS=$((CURRENT_TIME - ${LAST_BACKUP_TIME%.*}))
AGE_HOURS=$((AGE_SECONDS / 3600))

echo "Last backup: ${LAST_BACKUP_FILE}"
echo "Age: ${AGE_HOURS} hours"

if [ "$AGE_HOURS" -gt "$MAX_AGE_HOURS" ]; then
  MESSAGE="⚠️ ALERT: No Honua backup in ${AGE_HOURS} hours! Last backup: $(basename ${LAST_BACKUP_FILE})"

  # Send email alert
  echo "$MESSAGE" | mail -s "Honua Backup Alert" "${ALERT_EMAIL}"

  # Send Slack alert
  curl -X POST -H 'Content-type: application/json' \
    --data "{\"text\":\"${MESSAGE}\"}" \
    "${SLACK_WEBHOOK}"

  echo "ALERT SENT: ${MESSAGE}"
  exit 1
else
  echo "✓ Backup is current (${AGE_HOURS} hours old)"
fi

# Check backup size
BACKUP_SIZE=$(stat -c%s "$LAST_BACKUP_FILE")
MIN_SIZE=10000000  # 10MB minimum

if [ "$BACKUP_SIZE" -lt "$MIN_SIZE" ]; then
  MESSAGE="⚠️ ALERT: Backup file suspiciously small: $(numfmt --to=iec-i --suffix=B $BACKUP_SIZE)"
  echo "$MESSAGE" | mail -s "Honua Backup Size Alert" "${ALERT_EMAIL}"
  echo "ALERT SENT: ${MESSAGE}"
  exit 1
else
  echo "✓ Backup size is healthy: $(numfmt --to=iec-i --suffix=B $BACKUP_SIZE)"
fi

# Verify backup integrity
pg_restore --list "$LAST_BACKUP_FILE" > /dev/null 2>&1

if [ $? -eq 0 ]; then
  echo "✓ Backup integrity verified"
else
  MESSAGE="⚠️ ALERT: Backup integrity check failed: $(basename ${LAST_BACKUP_FILE})"
  echo "$MESSAGE" | mail -s "Honua Backup Integrity Alert" "${ALERT_EMAIL}"
  echo "ALERT SENT: ${MESSAGE}"
  exit 1
fi

echo "=== All backup checks passed ==="
```

### CloudWatch/Prometheus Metrics

```yaml
# prometheus-backup-exporter.yml
global:
  scrape_interval: 60s

scrape_configs:
  - job_name: 'honua-backup-metrics'
    static_configs:
      - targets: ['localhost:9100']

# Custom metrics to track:
# - backup_last_success_timestamp
# - backup_duration_seconds
# - backup_size_bytes
# - backup_validation_status
# - wal_archive_count
```

### Backup Dashboard (Grafana)

Create a Grafana dashboard tracking:
- Last successful backup timestamp
- Backup frequency
- Backup size trends
- Failed backup count
- Recovery test results
- Storage utilization

---

## Best Practices and Checklist

### Pre-Production Checklist

- [ ] Define RTO and RPO requirements
- [ ] Implement 3-2-1 backup strategy
- [ ] Configure PostgreSQL WAL archiving for PITR
- [ ] Set up automated daily backups
- [ ] Enable cloud snapshot automation (RDS/Azure)
- [ ] Configure backup retention policies
- [ ] Encrypt backups at rest and in transit
- [ ] Store backup encryption keys securely
- [ ] Set up backup monitoring and alerting
- [ ] Document recovery procedures
- [ ] Test restore procedures monthly
- [ ] Train team on disaster recovery

### Monthly Recovery Test

```bash
#!/bin/bash
# monthly-recovery-test.sh

# Run on 1st of every month
# 0 2 1 * * /usr/local/bin/monthly-recovery-test.sh

echo "=== Monthly Recovery Test - $(date) ==="

# 1. Validate latest backup
/usr/local/bin/validate-backup.sh $(ls -t /backups/postgresql/honua_*.backup | head -1)

# 2. Test PITR recovery
YESTERDAY=$(date -d "yesterday" +"%Y-%m-%d 23:59:59")
/usr/local/bin/automated-pitr-recovery.sh "${YESTERDAY}"

# 3. Document results
cat > /var/log/recovery-test-$(date +%Y%m).txt <<EOF
Recovery Test Results
Date: $(date)
Backup Validated: $(ls -t /backups/postgresql/honua_*.backup | head -1)
PITR Target: ${YESTERDAY}
Status: SUCCESS
Duration: ${SECONDS} seconds
EOF

# 4. Send report
mail -s "Monthly Recovery Test Report" ops@honua.io < /var/log/recovery-test-$(date +%Y%m).txt

echo "=== Recovery Test Completed ==="
```

---

**Last Updated**: 2025-10-04
**Honua Version**: 1.0+
**Related Documentation**: [Performance Tuning](performance-tuning.md), [Troubleshooting](troubleshooting.md), [AWS ECS Deployment](../02-deployment/aws-ecs-deployment.md), [Kubernetes Deployment](../02-deployment/kubernetes-deployment.md)
