# Azure Backup Automation Scripts

This directory contains automation scripts for Azure PostgreSQL database backups and verification.

## Scripts

### backup-automation.sh

Automated backup script that:
- Creates PostgreSQL dumps with parallel jobs
- Compresses backups for storage efficiency
- Uploads to Azure Blob Storage with lifecycle management
- Sends alerts on failures
- Generates backup reports

**Schedule**: Daily at 2:00 AM UTC

### verify-backup.sh

Backup verification script that:
- Checks backup existence and age
- Verifies geo-replication status
- Tests backup integrity
- Performs monthly restore tests

**Schedule**: Daily at 3:00 AM UTC (verification), 1st Sunday at 4:00 AM UTC (restore test)

## Setup

### Prerequisites

1. Install Azure CLI:
```bash
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash
```

2. Install PostgreSQL client:
```bash
sudo apt-get install postgresql-client-15
```

3. Authenticate with Azure:
```bash
az login
```

4. Set environment variables:
```bash
export RESOURCE_GROUP="rg-honua-prod-eastus"
export SERVER_NAME="postgres-honua-prod"
export STORAGE_ACCOUNT="stbkphonua123456"
export ALERT_EMAIL="ops@honua.io"
```

### Installation

1. Copy scripts to `/usr/local/bin/`:
```bash
sudo cp backup-automation.sh /usr/local/bin/honua-backup
sudo cp verify-backup.sh /usr/local/bin/honua-backup-verify
sudo chmod +x /usr/local/bin/honua-backup*
```

2. Create log directory:
```bash
sudo mkdir -p /var/log/honua
sudo chown $(whoami):$(whoami) /var/log/honua
```

3. Setup cron jobs:
```bash
sudo crontab -e
```

Add:
```cron
# Honua Database Backups
# Daily backup at 2:00 AM UTC
0 2 * * * /usr/local/bin/honua-backup >> /var/log/honua/backup-cron.log 2>&1

# Daily verification at 3:00 AM UTC
0 3 * * * /usr/local/bin/honua-backup-verify >> /var/log/honua/verify-cron.log 2>&1

# Monthly restore test (1st Sunday at 4:00 AM UTC)
0 4 * * 0 [ $(date +\%d) -le 7 ] && RUN_RESTORE_TEST=true /usr/local/bin/honua-backup-verify >> /var/log/honua/restore-test.log 2>&1
```

### Configuration

#### Environment Variables

| Variable | Required | Default | Description |
|----------|----------|---------|-------------|
| `RESOURCE_GROUP` | Yes | - | Azure resource group name |
| `SERVER_NAME` | Yes | - | PostgreSQL server name |
| `DB_NAME` | No | honua | Database name |
| `STORAGE_ACCOUNT` | Yes | - | Backup storage account |
| `BACKUP_CONTAINER` | No | database-backups | Storage container |
| `ALERT_EMAIL` | Yes | - | Email for alerts |
| `SLACK_WEBHOOK` | No | - | Slack webhook URL |
| `PAGERDUTY_KEY` | No | - | PagerDuty integration key |
| `BACKUP_RETENTION_DAYS` | No | 90 | Days to keep daily backups |
| `COMPRESSION_LEVEL` | No | 9 | Gzip compression level (1-9) |
| `PARALLEL_JOBS` | No | 4 | pg_dump parallel jobs |
| `MAX_BACKUP_SIZE_GB` | No | 100 | Maximum backup size alert |

#### Azure Managed Identity (Recommended)

For production, use Managed Identity instead of environment variables:

```bash
# Assign Managed Identity to VM/Container
az vm identity assign --name honua-backup-vm --resource-group rg-honua-prod

# Grant permissions
IDENTITY_ID=$(az vm identity show --name honua-backup-vm --resource-group rg-honua-prod --query principalId -o tsv)

az role assignment create \
  --assignee $IDENTITY_ID \
  --role "Backup Operator" \
  --scope /subscriptions/{subscription-id}/resourceGroups/rg-honua-prod-eastus

az role assignment create \
  --assignee $IDENTITY_ID \
  --role "Storage Blob Data Contributor" \
  --scope /subscriptions/{subscription-id}/resourceGroups/rg-honua-prod-eastus/providers/Microsoft.Storage/storageAccounts/stbkphonua123456
```

## Usage

### Manual Backup

```bash
/usr/local/bin/honua-backup
```

### Manual Verification

```bash
/usr/local/bin/honua-backup-verify
```

### Monthly Restore Test

```bash
RUN_RESTORE_TEST=true /usr/local/bin/honua-backup-verify
```

### Custom Backup Location

```bash
BACKUP_DIR=/mnt/backups /usr/local/bin/honua-backup
```

## Monitoring

### Logs

- Backup logs: `/var/log/honua/backup-*.log`
- Verification logs: `/var/log/honua/backup-verify-*.log`
- Cron logs: `/var/log/honua/backup-cron.log`

### Metrics

Scripts send metrics to:
- Azure Monitor (custom metrics)
- Prometheus Pushgateway (if configured)

**Metrics emitted:**
- `honua_backup_duration_seconds` - Backup duration
- `honua_backup_size_bytes` - Uncompressed size
- `honua_backup_compressed_size_bytes` - Compressed size
- `honua_backup_success` - 1 for success, 0 for failure
- `honua_backup_verification_success` - Verification status
- `honua_backup_last_success_timestamp` - Last successful backup

### Alerts

Scripts send alerts via:
- **Email**: Backup failures, verification failures
- **Slack**: Real-time notifications
- **PagerDuty**: Critical failures only

**Alert Levels:**
- `INFO` - Successful completion (optional)
- `WARNING` - Non-critical issues (small backup size, high age)
- `CRITICAL` - Backup failures, verification failures

## Backup Storage Layout

```
stbkphonua123456/database-backups/
├── daily/                          # Daily backups (90 days retention)
│   ├── postgres-honua-prod-20251018_020000.dump.gz
│   ├── postgres-honua-prod-20251017_020000.dump.gz
│   └── ...
├── weekly/                         # Weekly backups (1 year retention)
│   ├── postgres-honua-prod-20251013_020000.dump.gz
│   ├── postgres-honua-prod-20251006_020000.dump.gz
│   └── ...
├── monthly/                        # Monthly backups (7 years retention)
│   ├── postgres-honua-prod-20251001_020000.dump.gz
│   ├── postgres-honua-prod-20250901_020000.dump.gz
│   └── ...
├── reports/                        # Backup reports
│   ├── backup-report-20251018_020000.txt
│   └── ...
└── restore-tests/                  # Monthly restore test results
    ├── report-202510.txt
    └── ...
```

## Troubleshooting

### Backup Fails with "Insufficient Disk Space"

```bash
# Check available space
df -h /tmp/honua-backups

# Cleanup old local backups
find /tmp/honua-backups -name "*.dump.gz" -mtime +7 -delete
```

### Cannot Upload to Azure Storage

```bash
# Test Azure authentication
az account show

# Test storage account access
az storage account show --name stbkphonua123456

# Re-authenticate if needed
az login
```

### Verification Fails: "Backup Too Old"

```bash
# Check last backup time
az postgres flexible-server backup list \
  --resource-group rg-honua-prod-eastus \
  --server-name postgres-honua-prod \
  --query '[0].{Name:name, Time:backupStartTime}' -o table

# Manually trigger backup
/usr/local/bin/honua-backup
```

### Restore Test Times Out

```bash
# Check test server status
az postgres flexible-server show \
  --name postgres-test-restore-202510 \
  --resource-group rg-honua-prod-eastus \
  --query 'state' -o tsv

# If stuck, delete and retry
az postgres flexible-server delete \
  --name postgres-test-restore-202510 \
  --resource-group rg-honua-prod-eastus \
  --yes
```

## Security

### Credential Management

- **Passwords**: Stored in Azure Key Vault, never in scripts
- **Connection Strings**: Retrieved at runtime via Azure CLI
- **API Keys**: Environment variables or Managed Identity

### Network Security

- Backup server should have:
  - Outbound access to Azure services (443)
  - Access to PostgreSQL server (5432)
  - No inbound access required

### Audit

All backup operations are logged:
- Azure Activity Log (resource operations)
- Script logs (detailed execution)
- Storage access logs (blob operations)

## Compliance

These scripts help meet:
- **GDPR**: 30-day retention minimum (35-day configured)
- **SOC 2**: Regular verification and testing
- **HIPAA**: Encrypted backups with access controls
- **PCI DSS**: Secure credential management

## Support

For issues or questions:
- Platform Engineering: platform@honua.io
- On-Call: PagerDuty escalation
- Documentation: /docs/deployment/AZURE_BACKUP_POLICY.md

## Related Documentation

- [Backup Policy](../../docs/deployment/AZURE_BACKUP_POLICY.md)
- [Restore Procedures](../../docs/deployment/AZURE_RESTORE_PROCEDURES.md)
- [Terraform Configuration](../../infrastructure/terraform/azure/main.tf)
