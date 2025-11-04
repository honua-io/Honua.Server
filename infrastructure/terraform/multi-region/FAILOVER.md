# Multi-Region Failover Procedures

This document provides step-by-step procedures for failover scenarios in the HonuaIO multi-region deployment.

## Table of Contents

1. [Overview](#overview)
2. [Automated Failover](#automated-failover)
3. [Manual Failover](#manual-failover)
4. [Database Promotion](#database-promotion)
5. [DNS Cutover](#dns-cutover)
6. [Rollback Procedures](#rollback-procedures)
7. [Testing Failover](#testing-failover)
8. [Post-Failover Checklist](#post-failover-checklist)

## Overview

### Failover Scenarios

| Scenario | Type | RTO | RPO | Trigger |
|----------|------|-----|-----|---------|
| Health Check Failure | Automated | ~5 min | ~10 sec | Route53/Front Door |
| Region Outage | Automated | ~10 min | ~30 sec | Health checks |
| Planned Maintenance | Manual | ~15 min | 0 | Operations team |
| Database Failure | Manual | ~20 min | ~60 sec | DBA team |
| Complete DR Test | Manual | ~30 min | 0 | Quarterly drill |

### Decision Matrix

```
┌─────────────────────────────────────────────────────────────┐
│  Is the primary region completely unavailable?              │
│  ├─ YES → Automated failover initiated                      │
│  └─ NO → Investigate before failing over                    │
│                                                              │
│  Is this a planned maintenance?                             │
│  ├─ YES → Use Manual Failover procedure                     │
│  └─ NO → Use Emergency Failover procedure                   │
│                                                              │
│  Is the database affected?                                  │
│  ├─ YES → Promote replica, then failover                    │
│  └─ NO → Failover application layer only                    │
└─────────────────────────────────────────────────────────────┘
```

## Automated Failover

The system automatically fails over when health checks fail in the primary region.

### How It Works

1. **Health Check Failure**: Route53/Front Door detects 3 consecutive failures (90s)
2. **Traffic Shift**: DNS automatically routes traffic to DR region
3. **Alert Notification**: On-call team receives PagerDuty/Slack alert
4. **Monitoring**: Dashboard updates to show DR region as active

### AWS Route53 Automated Failover

```bash
# Check current health status
aws route53 get-health-check-status \
  --health-check-id $(terraform output -raw primary_health_check_id)

# View failover events
aws cloudwatch get-metric-statistics \
  --namespace AWS/Route53 \
  --metric-name HealthCheckStatus \
  --dimensions Name=HealthCheckId,Value=$(terraform output -raw primary_health_check_id) \
  --start-time $(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
  --period 60 \
  --statistics Average
```

### Azure Front Door Automated Failover

```bash
# Check backend health
az network front-door backend-pool show \
  --front-door-name $(terraform output -raw front_door_name) \
  --resource-group $(terraform output -raw resource_group_name) \
  --name default-backend-pool

# View health probe results
az monitor metrics list \
  --resource $(terraform output -raw front_door_id) \
  --metric BackendHealthPercentage \
  --start-time $(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S)
```

### Verification

```bash
# Test global endpoint
GLOBAL_URL=$(terraform output -raw global_endpoint)
curl -I $GLOBAL_URL/health

# Check which region is serving traffic
curl -s $GLOBAL_URL/health | jq '.region'
# Expected output after failover: "us-west-2" (or DR region)
```

## Manual Failover

Use this procedure for planned maintenance or when you need controlled failover.

### Pre-Failover Checklist

- [ ] Verify DR region health
- [ ] Check replication lag (should be < 10s)
- [ ] Notify stakeholders (maintenance window)
- [ ] Create database backup snapshot
- [ ] Scale up DR resources if needed
- [ ] Test DR application endpoints

### Step 1: Scale Up DR Resources

```bash
# AWS: Scale up ECS tasks in DR region
cd infrastructure/terraform/multi-region
terraform apply \
  -var="dr_instance_count=3" \
  -var="dr_instance_type=c6i.2xlarge"

# Azure: Scale up Container Apps
terraform apply \
  -var="dr_instance_count=3" \
  -var="dr_instance_size=Standard_D4s_v5"

# Wait for resources to be ready (2-3 minutes)
./scripts/wait-for-dr-scale.sh
```

### Step 2: Verify DR Application Health

```bash
# Test DR endpoint directly
DR_ENDPOINT=$(terraform output -raw dr_endpoint)
curl -f $DR_ENDPOINT/health || { echo "DR not healthy!"; exit 1; }

# Test database connectivity from DR
./scripts/test-dr-database.sh

# Test storage access from DR
./scripts/test-dr-storage.sh
```

### Step 3: Enable Maintenance Mode (Optional)

```bash
# Put primary region in maintenance mode
./scripts/enable-maintenance-mode.sh --region primary

# This returns HTTP 503 from primary, triggering automatic failover
```

### Step 4: Promote Database Replica (If Needed)

**AWS RDS:**

```bash
# Check replication lag before promoting
aws rds describe-db-instances \
  --db-instance-identifier $(terraform output -raw dr_db_identifier) \
  --query 'DBInstances[0].ReadReplicaSourceDBInstanceIdentifier'

# Promote replica to standalone instance
aws rds promote-read-replica \
  --db-instance-identifier $(terraform output -raw dr_db_identifier)

# Wait for promotion to complete (5-10 minutes)
aws rds wait db-instance-available \
  --db-instance-identifier $(terraform output -raw dr_db_identifier)
```

**Azure PostgreSQL:**

```bash
# Check replication status
az postgres flexible-server replica list \
  --resource-group $(terraform output -raw dr_resource_group) \
  --server-name $(terraform output -raw primary_db_server)

# Promote replica
az postgres flexible-server replica stop-replication \
  --resource-group $(terraform output -raw dr_resource_group) \
  --name $(terraform output -raw dr_db_server)

# Verify promotion
az postgres flexible-server show \
  --resource-group $(terraform output -raw dr_resource_group) \
  --name $(terraform output -raw dr_db_server) \
  --query "replicationRole"
# Expected: "None" (was "Secondary")
```

### Step 5: Update DNS to Point to DR Region

**AWS Route53:**

```bash
# Update Route53 weighted routing to favor DR
./scripts/failover-dns-aws.sh --target dr --weight 100

# Or manually update the record
aws route53 change-resource-record-sets \
  --hosted-zone-id $(terraform output -raw hosted_zone_id) \
  --change-batch file://dns-failover-dr.json
```

**Azure Front Door:**

```bash
# Disable primary backend
az network front-door backend-pool backend update \
  --front-door-name $(terraform output -raw front_door_name) \
  --resource-group $(terraform output -raw resource_group_name) \
  --pool-name default-backend-pool \
  --address $(terraform output -raw primary_endpoint) \
  --disabled true

# Verify DR backend is active
az network front-door backend-pool backend update \
  --front-door-name $(terraform output -raw front_door_name) \
  --resource-group $(terraform output -raw resource_group_name) \
  --pool-name default-backend-pool \
  --address $(terraform output -raw dr_endpoint) \
  --enabled true
```

### Step 6: Verify Failover

```bash
# Test global endpoint (should now route to DR)
GLOBAL_URL=$(terraform output -raw global_endpoint)
for i in {1..10}; do
  curl -s $GLOBAL_URL/health | jq '.region'
  sleep 2
done
# All requests should show DR region

# Check application functionality
./scripts/smoke-test.sh --endpoint $GLOBAL_URL

# Monitor application logs
./scripts/tail-logs.sh --region dr
```

### Step 7: Monitor Metrics

```bash
# Watch key metrics in DR region
./scripts/watch-metrics.sh --region dr --duration 30m

# Key metrics to watch:
# - CPU utilization (should be < 70%)
# - Memory utilization (should be < 80%)
# - Request latency (should be < 500ms)
# - Error rate (should be < 1%)
# - Database connections (should be stable)
```

## Database Promotion

### AWS RDS Read Replica Promotion

```bash
#!/bin/bash
# scripts/promote-rds-replica.sh

set -e

DR_INSTANCE_ID=$(terraform output -raw dr_db_identifier)
PRIMARY_INSTANCE_ID=$(terraform output -raw primary_db_identifier)

echo "Checking replication lag..."
LAG=$(aws rds describe-db-instances \
  --db-instance-identifier $DR_INSTANCE_ID \
  --query 'DBInstances[0].StatusInfos[?StatusType==`replica lag`].Status' \
  --output text)

echo "Current replication lag: $LAG"

if [[ "$LAG" -gt 60 ]]; then
  echo "ERROR: Replication lag too high ($LAG seconds). Aborting."
  exit 1
fi

echo "Creating final snapshot of primary..."
aws rds create-db-snapshot \
  --db-instance-identifier $PRIMARY_INSTANCE_ID \
  --db-snapshot-identifier "${PRIMARY_INSTANCE_ID}-final-$(date +%Y%m%d-%H%M%S)"

echo "Promoting read replica..."
aws rds promote-read-replica \
  --db-instance-identifier $DR_INSTANCE_ID

echo "Waiting for promotion to complete..."
aws rds wait db-instance-available \
  --db-instance-identifier $DR_INSTANCE_ID

echo "Database promoted successfully!"
echo "New primary: $DR_INSTANCE_ID"
```

### Azure PostgreSQL Replica Promotion

```bash
#!/bin/bash
# scripts/promote-azure-replica.sh

set -e

DR_SERVER=$(terraform output -raw dr_db_server)
DR_RG=$(terraform output -raw dr_resource_group)

echo "Checking replication status..."
ROLE=$(az postgres flexible-server show \
  --resource-group $DR_RG \
  --name $DR_SERVER \
  --query "replicationRole" -o tsv)

if [[ "$ROLE" != "Replica" ]]; then
  echo "ERROR: Server is not a replica. Current role: $ROLE"
  exit 1
fi

echo "Stopping replication and promoting replica..."
az postgres flexible-server replica stop-replication \
  --resource-group $DR_RG \
  --name $DR_SERVER

echo "Waiting for promotion to complete..."
sleep 30

echo "Verifying promotion..."
NEW_ROLE=$(az postgres flexible-server show \
  --resource-group $DR_RG \
  --name $DR_SERVER \
  --query "replicationRole" -o tsv)

if [[ "$NEW_ROLE" == "None" ]]; then
  echo "Database promoted successfully!"
  echo "New primary: $DR_SERVER"
else
  echo "ERROR: Promotion failed. Current role: $NEW_ROLE"
  exit 1
fi
```

## DNS Cutover

### Route53 Weighted Routing Update

```json
// dns-failover-dr.json
{
  "Changes": [
    {
      "Action": "UPSERT",
      "ResourceRecordSet": {
        "Name": "api.honua.io",
        "Type": "A",
        "SetIdentifier": "Primary",
        "Weight": 0,
        "AliasTarget": {
          "HostedZoneId": "Z12345EXAMPLE",
          "DNSName": "primary-lb.us-east-1.elb.amazonaws.com",
          "EvaluateTargetHealth": true
        }
      }
    },
    {
      "Action": "UPSERT",
      "ResourceRecordSet": {
        "Name": "api.honua.io",
        "Type": "A",
        "SetIdentifier": "DR",
        "Weight": 100,
        "AliasTarget": {
          "HostedZoneId": "Z67890EXAMPLE",
          "DNSName": "dr-lb.us-west-2.elb.amazonaws.com",
          "EvaluateTargetHealth": true
        }
      }
    }
  ]
}
```

### Front Door Backend Pool Update

```bash
# Disable primary backend
az network front-door backend-pool backend update \
  --front-door-name honua-prod-fd \
  --resource-group rg-honua-prod \
  --pool-name default-backend-pool \
  --address primary-app.eastus.azurecontainerapps.io \
  --disabled true \
  --backend-host-header primary-app.eastus.azurecontainerapps.io

# Ensure DR backend is enabled
az network front-door backend-pool backend update \
  --front-door-name honua-prod-fd \
  --resource-group rg-honua-prod \
  --pool-name default-backend-pool \
  --address dr-app.westus2.azurecontainerapps.io \
  --enabled true \
  --backend-host-header dr-app.westus2.azurecontainerapps.io
```

## Rollback Procedures

### Rollback from DR to Primary

```bash
#!/bin/bash
# scripts/rollback-to-primary.sh

set -e

echo "=== Rolling back to Primary Region ==="

# Step 1: Verify primary region health
echo "1. Checking primary region health..."
PRIMARY_ENDPOINT=$(terraform output -raw primary_endpoint)
curl -f $PRIMARY_ENDPOINT/health || { echo "Primary not healthy!"; exit 1; }

# Step 2: Scale up primary resources
echo "2. Scaling up primary resources..."
terraform apply -auto-approve \
  -var="primary_instance_count=3"

# Step 3: Update DNS to point back to primary
echo "3. Updating DNS to primary region..."
./scripts/failover-dns-aws.sh --target primary --weight 100

# Step 4: Wait for DNS propagation
echo "4. Waiting for DNS propagation (60s)..."
sleep 60

# Step 5: Verify traffic routing
echo "5. Verifying traffic routing..."
GLOBAL_URL=$(terraform output -raw global_endpoint)
REGION=$(curl -s $GLOBAL_URL/health | jq -r '.region')

if [[ "$REGION" == "us-east-1" ]]; then
  echo "✓ Successfully rolled back to primary region"
else
  echo "✗ Rollback failed. Still routing to: $REGION"
  exit 1
fi

# Step 6: Scale down DR resources
echo "6. Scaling down DR resources..."
terraform apply -auto-approve \
  -var="dr_instance_count=1"

echo "=== Rollback Complete ==="
```

## Testing Failover

### DR Drill Procedure (Quarterly)

```bash
#!/bin/bash
# scripts/dr-drill.sh

set -e

echo "=== Starting DR Drill ==="
echo "This will test failover without affecting production traffic"

# Step 1: Create test DNS entry
echo "1. Creating test DNS entry (test.api.honua.io)..."
./scripts/create-test-dns.sh

# Step 2: Scale up DR resources
echo "2. Scaling up DR resources..."
terraform apply -auto-approve \
  -var="dr_instance_count=3" \
  -var="dr_instance_type=c6i.2xlarge"

# Step 3: Test DR endpoint directly
echo "3. Testing DR endpoint..."
DR_ENDPOINT=$(terraform output -raw dr_endpoint)
./scripts/smoke-test.sh --endpoint $DR_ENDPOINT

# Step 4: Promote read replica to test database promotion
echo "4. Creating test read replica..."
./scripts/create-test-replica.sh
./scripts/test-replica-promotion.sh

# Step 5: Simulate DNS cutover to test DNS
echo "5. Updating test DNS to DR..."
./scripts/failover-test-dns.sh --target dr

# Step 6: Run full test suite against test endpoint
echo "6. Running test suite..."
./scripts/run-integration-tests.sh --endpoint test.api.honua.io

# Step 7: Generate drill report
echo "7. Generating drill report..."
./scripts/generate-dr-report.sh

# Step 8: Cleanup
echo "8. Cleaning up test resources..."
./scripts/cleanup-dr-drill.sh

echo "=== DR Drill Complete ==="
echo "Report saved to: dr-drill-report-$(date +%Y%m%d).pdf"
```

## Post-Failover Checklist

After failover is complete, verify the following:

### Immediate (Within 15 Minutes)

- [ ] Global endpoint responding successfully
- [ ] Application health checks passing
- [ ] Database connections established
- [ ] Storage accessible (S3/Blob)
- [ ] Cache connections working (Redis)
- [ ] Monitoring dashboards updated
- [ ] Alert notifications sent
- [ ] On-call team acknowledged

### Short-term (Within 1 Hour)

- [ ] Error rates within normal range (< 1%)
- [ ] Latency within acceptable limits (< 500ms p99)
- [ ] Background jobs processing
- [ ] Scheduled tasks running
- [ ] Audit logs capturing events
- [ ] Backup jobs executing
- [ ] Replication lag monitoring (if applicable)

### Medium-term (Within 24 Hours)

- [ ] Incident post-mortem scheduled
- [ ] Root cause identified
- [ ] Stakeholder communication sent
- [ ] Cost impact assessed
- [ ] Recovery plan documented
- [ ] Primary region restoration started
- [ ] DR drill lessons learned documented

### Long-term (Within 1 Week)

- [ ] Primary region fully restored
- [ ] Replication re-established
- [ ] Rollback plan tested
- [ ] Runbooks updated
- [ ] Failover time metrics recorded
- [ ] DR capacity planning reviewed
- [ ] Next DR drill scheduled

## Emergency Contacts

| Role | Primary | Secondary | Escalation |
|------|---------|-----------|------------|
| On-Call Engineer | PagerDuty | Slack #oncall | CTO |
| Database Admin | DBA rotation | Senior DBA | VP Engineering |
| Network Engineer | NetOps team | Cloud Architect | CTO |
| Security | Security lead | CISO | CEO |

## Monitoring Dashboards

- **AWS**: https://console.aws.amazon.com/cloudwatch/home?region=us-east-1#dashboards:name=HonuaMultiRegion
- **Azure**: https://portal.azure.com/#@/dashboard/HonuaMultiRegion
- **PagerDuty**: https://honua.pagerduty.com/incidents
- **Grafana**: https://grafana.honua.io/d/multi-region

## Automation

All failover procedures can be automated using:

```bash
# Automated failover with approval
./scripts/failover.sh --cloud aws --approve

# Automated rollback
./scripts/rollback.sh --cloud aws --approve

# DR drill automation
./scripts/dr-drill.sh --scheduled
```

## Audit Trail

All failover events are logged to:

- **AWS**: CloudTrail + CloudWatch Logs
- **Azure**: Activity Log + Log Analytics
- **Application**: PostgreSQL audit tables
- **External**: PagerDuty + Slack #incidents

## Lessons Learned

After each failover, update this section with lessons learned:

### 2025-01-15: Planned Maintenance Failover
- **Duration**: 12 minutes (RTO target: 15 min)
- **Data Loss**: None (RPO: 0)
- **Lessons**:
  - Pre-scaling DR resources reduced cutover time
  - DNS propagation was faster than expected (45s vs 60s)
  - Database promotion script needs timeout handling

### 2025-02-20: Automated Failover (Region Outage)
- **Duration**: 8 minutes
- **Data Loss**: ~5 seconds (RPO target: 60 sec)
- **Lessons**:
  - Automated failover worked flawlessly
  - Need better alerting for replication lag spikes
  - DR resources auto-scaled correctly

## Next Review

This document should be reviewed:
- After each failover event
- During quarterly DR drills
- When infrastructure changes are made
- Annually (minimum)

**Last Updated**: 2025-10-18
**Next Review**: 2025-11-18
**Owner**: Platform Engineering Team
