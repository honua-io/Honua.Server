# Honua Infrastructure Cost Estimation

Detailed cost breakdown for Honua infrastructure across AWS, Azure, and GCP.

## Cost Summary by Environment

| Environment | AWS (Monthly) | Azure (Monthly) | GCP (Monthly) |
|------------|---------------|-----------------|---------------|
| Development | $500-800 | $450-750 | $480-780 |
| Staging | $1,500-2,500 | $1,400-2,300 | $1,450-2,400 |
| Production | $5,000-10,000 | $4,800-9,500 | $5,200-10,500 |

## Detailed Cost Breakdown

### Development Environment

#### AWS Costs

| Service | Configuration | Monthly Cost |
|---------|--------------|--------------|
| **EKS Control Plane** | 1 cluster | $73 |
| **EC2 Instances (Graviton)** | 2x t4g.xlarge spot | $60-90 |
| **RDS PostgreSQL** | db.t4g.medium, 50GB | $60 |
| **ElastiCache Redis** | cache.r7g.large | $80 |
| **ECR Storage** | 100GB | $10 |
| **NAT Gateway** | 2 gateways | $70 |
| **Data Transfer** | 500GB out | $45 |
| **CloudWatch Logs** | 50GB | $25 |
| **KMS** | 1 key | $1 |
| **S3 (Terraform State)** | 10GB | $0.23 |
| **DynamoDB (State Lock)** | On-demand | $2 |
| **EBS Volumes** | 200GB gp3 | $20 |
| **Backup Storage** | 100GB | $10 |
| **Total** | | **$456-486** |

#### Azure Costs

| Service | Configuration | Monthly Cost |
|---------|--------------|--------------|
| **AKS Control Plane** | Free | $0 |
| **VM Instances (Ampere)** | 2x Standard_D4ps_v5 spot | $140-180 |
| **PostgreSQL Flexible** | Standard_B2s, 128GB | $75 |
| **Azure Cache Redis** | Standard C2 | $100 |
| **ACR** | Standard, 100GB | $20 |
| **NAT Gateway** | 2 gateways | $90 |
| **Data Transfer** | 500GB out | $40 |
| **Log Analytics** | 50GB | $15 |
| **Key Vault** | Standard | $3 |
| **Storage (State)** | 10GB LRS | $0.20 |
| **Managed Disks** | 200GB Premium SSD | $40 |
| **Total** | | **$523-573** |

#### GCP Costs

| Service | Configuration | Monthly Cost |
|---------|--------------|--------------|
| **GKE Control Plane** | Zonal cluster | $73 |
| **Compute (Tau T2A)** | 2x t2a-standard-4 preemptible | $80-120 |
| **Cloud SQL PostgreSQL** | db-custom-2-8192, 100GB | $90 |
| **Memorystore Redis** | Basic, 5GB | $85 |
| **Artifact Registry** | 100GB | $10 |
| **Cloud NAT** | 1 NAT gateway | $45 |
| **Data Transfer** | 500GB out | $85 |
| **Cloud Logging** | 50GB | $25 |
| **Cloud KMS** | 1 key | $1 |
| **Cloud Storage (State)** | 10GB | $0.20 |
| **Persistent Disks** | 200GB SSD | $40 |
| **Total** | | **$534-634** |

### Staging Environment

#### AWS Costs

| Service | Configuration | Monthly Cost |
|---------|--------------|--------------|
| **EKS Control Plane** | 1 cluster | $73 |
| **EC2 Instances (Graviton)** | 4x m7g.xlarge | $520 |
| **RDS PostgreSQL** | db.r7g.large, 200GB, Multi-AZ | $420 |
| **RDS Read Replica** | db.r7g.large, 200GB | $280 |
| **ElastiCache Redis** | cache.r7g.xlarge, cluster mode | $310 |
| **ECR Storage** | 300GB | $30 |
| **NAT Gateway** | 3 gateways | $105 |
| **Data Transfer** | 1TB out | $90 |
| **CloudWatch** | 150GB logs, metrics | $80 |
| **KMS** | 2 keys | $2 |
| **Backup Storage** | 500GB | $50 |
| **Load Balancer** | ALB | $25 |
| **Total** | | **~$1,985** |

#### Azure Costs (Staging)

| Service | Configuration | Monthly Cost |
|---------|--------------|--------------|
| **AKS** | Free | $0 |
| **VM Instances** | 4x Standard_D8ps_v5 | $700 |
| **PostgreSQL** | Standard_D4s_v3, HA | $480 |
| **Redis** | Premium P1, geo-replication | $400 |
| **ACR** | Premium, 300GB | $150 |
| **NAT Gateway** | 3 gateways | $135 |
| **Data Transfer** | 1TB | $80 |
| **Monitoring** | 150GB | $50 |
| **Total** | | **~$1,995** |

#### GCP Costs (Staging)

| Service | Configuration | Monthly Cost |
|---------|--------------|--------------|
| **GKE** | Regional cluster | $146 |
| **Compute** | 4x t2a-standard-8 | $520 |
| **Cloud SQL** | HA, db-custom-4-16384 | $460 |
| **Memorystore** | Standard, 10GB | $180 |
| **Artifact Registry** | 300GB | $30 |
| **Cloud NAT** | 2 gateways | $90 |
| **Data Transfer** | 1TB | $170 |
| **Monitoring** | 150GB | $75 |
| **Load Balancer** | Network LB | $20 |
| **Total** | | **~$1,691** |

### Production Environment

#### AWS Costs (Production)

| Service | Configuration | Monthly Cost |
|---------|--------------|--------------|
| **EKS Control Plane** | 1 cluster | $73 |
| **EC2 Instances** | 6x m7g.2xlarge (Reserved) | $1,900 |
| **RDS PostgreSQL** | db.r7g.2xlarge, 500GB, Multi-AZ | $1,100 |
| **RDS Read Replicas** | 2x db.r7g.2xlarge, 500GB | $1,480 |
| **ElastiCache Redis** | 3 shards, 2 replicas, r7g.2xlarge | $1,850 |
| **ECR Storage** | 1TB | $100 |
| **NAT Gateway** | 3 gateways | $105 |
| **Data Transfer** | 5TB out | $450 |
| **CloudWatch** | 500GB logs, enhanced metrics | $280 |
| **KMS** | 3 keys | $3 |
| **Backup Storage** | 2TB | $200 |
| **Cross-Region Backup** | 1TB | $100 |
| **Load Balancer** | 2 ALBs | $50 |
| **Route53** | Hosted zone + queries | $25 |
| **AWS Shield Standard** | Included | $0 |
| **AWS WAF** | Optional | $50-200 |
| **Total (without WAF)** | | **~$6,716** |
| **Total (with WAF)** | | **~$6,766-6,916** |

#### Azure Costs (Production)

| Service | Configuration | Monthly Cost |
|---------|--------------|--------------|
| **AKS** | Free | $0 |
| **VM Instances** | 6x Standard_D16ps_v5 (Reserved) | $2,850 |
| **PostgreSQL** | Standard_D16s_v3, HA, geo-backup | $1,920 |
| **Redis** | Premium P4, HA, geo-replication | $1,600 |
| **ACR** | Premium, 1TB, geo-replication | $500 |
| **NAT Gateway** | 3 gateways | $135 |
| **Data Transfer** | 5TB | $400 |
| **Monitoring** | 500GB + App Insights | $200 |
| **Key Vault** | Premium HSM | $100 |
| **Azure Firewall** | Optional | $500 |
| **Load Balancer** | Standard | $40 |
| **Total (without Firewall)** | | **~$7,745** |
| **Total (with Firewall)** | | **~$8,245** |

#### GCP Costs (Production)

| Service | Configuration | Monthly Cost |
|---------|--------------|--------------|
| **GKE** | Regional cluster (99.95% SLA) | $146 |
| **Compute** | 6x t2a-standard-16 (Committed) | $2,100 |
| **Cloud SQL** | HA, db-custom-16-65536 | $1,840 |
| **Cloud SQL Replicas** | 2 read replicas | $2,450 |
| **Memorystore** | Standard HA, 50GB | $900 |
| **Artifact Registry** | 1TB, geo-replication | $100 |
| **Cloud NAT** | 2 gateways | $90 |
| **Data Transfer** | 5TB | $850 |
| **Monitoring** | 500GB + APM | $250 |
| **Cloud Armor** | Optional | $200-500 |
| **Load Balancer** | Network + HTTP(S) | $60 |
| **Total (without Armor)** | | **~$8,786** |
| **Total (with Armor)** | | **~$8,986-9,286** |

## Cost Optimization Strategies

### 1. Reserved Instances / Committed Use Discounts

**AWS Reserved Instances**:
- 1-year term: 40% savings
- 3-year term: 60% savings
- Apply to: EC2, RDS, ElastiCache

**Azure Reserved Instances**:
- 1-year: 20-40% savings
- 3-year: 40-60% savings
- Apply to: VMs, SQL Database, Redis

**GCP Committed Use Discounts**:
- 1-year: 25-37% savings
- 3-year: 52-57% savings
- Apply to: Compute, SQL, Memorystore

**Potential Production Savings**: $1,500-3,000/month

### 2. Spot/Preemptible Instances

- Use for non-production environments
- Potential savings: 60-90%
- Dev environment savings: $200-300/month

### 3. Right-Sizing

Regularly review instance utilization:

```bash
# AWS CloudWatch metrics
aws cloudwatch get-metric-statistics \
  --namespace AWS/EC2 \
  --metric-name CPUUtilization \
  --dimensions Name=InstanceId,Value=<instance-id> \
  --start-time 2024-01-01T00:00:00Z \
  --end-time 2024-01-31T23:59:59Z \
  --period 3600 \
  --statistics Average

# Consider downsizing if consistently <40% utilization
```

**Potential Savings**: 20-30% on compute

### 4. Storage Optimization

- Lifecycle policies for container images (configured)
- Delete old snapshots: `$0.05/GB-month` savings
- Use appropriate storage tiers
  - AWS: gp3 vs io2
  - Azure: Standard vs Premium SSD
  - GCP: Standard vs SSD persistent disks

**Potential Savings**: $100-200/month

### 5. Data Transfer Optimization

- Use CloudFront/CDN for static assets
- Enable compression
- Optimize API payloads
- Use VPC endpoints (AWS) / Private Links (Azure/GCP)

**Potential Savings**: 30-50% on egress ($135-225/month in prod)

### 6. Scheduled Scaling

For non-production:

```bash
# Scale down nights and weekends
# Monday-Friday 9am-6pm only
# Savings: 75% of compute costs
# Dev environment: ~$300/month savings
```

### 7. Multi-Region vs Single-Region

- Consider actual DR requirements
- Single region can save 40-50% on replication/backup costs
- Evaluate if RPO/RTO allows for backup-based recovery

### 8. Monitoring Cost Management

- Set retention policies
- Use sampling for high-volume logs
- Archive to S3/Blob/GCS after 30 days

**Savings**: $50-100/month

## Budget Alerts Configuration

Budget alerts are automatically configured in Terraform:

### Alert Thresholds

- **80% of budget**: Warning email
- **100% of budget**: Critical email
- **120% of budget**: (Manual intervention required)

### Budget Recommendations

| Environment | AWS Budget | Azure Budget | GCP Budget |
|------------|------------|--------------|------------|
| Development | $600/month | $600/month | $650/month |
| Staging | $2,500/month | $2,500/month | $2,500/month |
| Production | $8,000/month | $8,500/month | $9,500/month |

## Annual Cost Projections

### With No Optimization

| Environment | AWS (Annual) | Azure (Annual) | GCP (Annual) |
|------------|--------------|----------------|--------------|
| Dev + Staging + Prod | $90,000 | $95,000 | $98,000 |

### With Optimization

| Environment | AWS (Annual) | Azure (Annual) | GCP (Annual) |
|------------|--------------|----------------|--------------|
| Dev + Staging + Prod | $55,000-65,000 | $58,000-68,000 | $60,000-70,000 |

**Total Potential Savings**: $25,000-35,000/year (38-42%)

## Cost Monitoring Tools

### AWS
```bash
# AWS Cost Explorer
aws ce get-cost-and-usage \
  --time-period Start=2024-01-01,End=2024-01-31 \
  --granularity DAILY \
  --metrics "UnblendedCost"

# Cost allocation tags
aws ce get-tags --time-period Start=2024-01-01,End=2024-01-31
```

### Azure
```bash
# Azure Cost Management
az consumption usage list \
  --start-date 2024-01-01 \
  --end-date 2024-01-31

# Cost by resource group
az consumption budget list --resource-group honua-production
```

### GCP
```bash
# BigQuery cost analysis
bq query --use_legacy_sql=false '
SELECT
  service.description,
  SUM(cost) as total_cost
FROM `project-id.billing_export.gcp_billing_export_v1_XXXXX`
WHERE DATE(usage_start_time) >= "2024-01-01"
GROUP BY service.description
ORDER BY total_cost DESC'
```

## Detailed Line Items

### Hidden Costs to Watch

1. **Data Transfer Between AZs**: $0.01-0.02/GB
2. **VPC Endpoint Charges**: $0.01/hour per endpoint
3. **CloudWatch Detailed Monitoring**: $2.10/metric/month
4. **Secrets Manager**: $0.40/secret/month
5. **KMS API Calls**: $0.03/10,000 requests
6. **Load Balancer Data Processing**: $0.008/GB
7. **DNS Queries**: $0.40/million queries (Route53)

### Cost by Compliance Level

| Compliance | Additional Cost | Notes |
|-----------|----------------|-------|
| SOC2 Type 2 | +15-20% | Enhanced logging, audit trails |
| ISO 27001 | +10-15% | Security controls, monitoring |
| HIPAA | +25-35% | Encryption, dedicated tenancy |
| PCI DSS | +20-30% | Network isolation, scanning |

## Free Tier Usage (First Year)

### AWS Free Tier
- 750 hours t2.micro/t3.micro EC2
- 20GB general purpose SSD storage
- 750 hours RDS db.t2.micro
- 25GB DynamoDB storage
- 5GB S3 storage

### Azure Free Tier
- 750 hours B1S VM
- 64GB managed disks
- 5GB blob storage
- Azure Functions (1M requests)

### GCP Free Tier
- 1 f1-micro instance
- 30GB storage
- 1GB network egress (North America)

**Note**: Free tier benefits apply only to specific resources and regions.

## Cost Comparison by Workload Type

### For 100 Builds/Day

| Cloud | Compute | Storage | Transfer | Monitoring | **Total** |
|-------|---------|---------|----------|------------|-----------|
| AWS | $150 | $30 | $20 | $15 | **$215** |
| Azure | $160 | $35 | $18 | $12 | **$225** |
| GCP | $165 | $32 | $35 | $18 | **$250** |

### For 1000 Builds/Day

| Cloud | Compute | Storage | Transfer | Monitoring | **Total** |
|-------|---------|---------|----------|------------|-----------|
| AWS | $850 | $120 | $180 | $80 | **$1,230** |
| Azure | $920 | $140 | $160 | $70 | **$1,290** |
| GCP | $900 | $130 | $280 | $90 | **$1,400** |

## Conclusion

- **Lowest Cost (Raw)**: AWS typically 5-10% cheaper
- **Best Value**: Azure (free AKS control plane)
- **Best for Egress**: Azure (lower egress costs)
- **Most Predictable**: GCP (sustained use discounts automatic)

### Recommendation by Use Case

- **Startups/Small Teams**: Start with dev on AWS spot instances
- **Enterprise**: Multi-cloud with production on reserved instances
- **Cost-Sensitive**: Azure for Kubernetes workloads
- **Data-Heavy**: Azure or AWS (avoid GCP egress costs)

---

**Last Updated**: 2024
**Pricing as of**: January 2024
**Note**: Prices subject to change. Always verify current pricing on provider websites.
