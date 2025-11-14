# Honua IO Cloud Marketplace - Deployment Options

This document outlines all available deployment options for Honua IO across AWS, Azure, and Google Cloud marketplaces.

## Deployment Matrix

| Platform | Option | Management | Cost Model | Best For |
|----------|--------|------------|------------|----------|
| **AWS** | EKS | Managed K8s | Pay for nodes | Production, predictable load |
| **AWS** | ECS Fargate | Serverless containers | Pay per task | Variable load, no ops overhead |
| **Azure** | AKS | Managed K8s | Pay for nodes | Production, predictable load |
| **Azure** | Container Apps | Serverless containers | Pay per use | Variable load, auto scale-to-zero |
| **GCP** | GKE | Managed K8s | Pay for nodes | Production, predictable load |
| **GCP** | Cloud Run | Serverless containers | Pay per request | Bursty traffic, low ops |

---

## AWS Marketplace Deployment Options

### Option 1: Amazon EKS (Kubernetes)
**File**: `aws/templates/eks-deployment.yaml`

**Architecture**:
```
Internet → NLB → YARP Gateway (2-20 pods) → Honua Server (2-10 pods)
                          ↓
            RDS PostgreSQL (Multi-AZ) + ElastiCache Redis (HA)
```

**Features**:
- ✅ **YARP API Gateway** - Rate limiting, security headers, load balancing
- ✅ **Kubernetes HPA** - Auto-scaling 2-20 replicas for gateway, 2-10 for backend
- ✅ **High Availability** - Multi-AZ deployment across 2+ availability zones
- ✅ **Redis HA** - 2-node cluster with automatic failover
- ✅ **Database** - RDS PostgreSQL 16 with Multi-AZ, automated backups
- ✅ **Storage** - S3 bucket for attachments and COG data
- ✅ **IAM** - IRSA for secure AWS service access
- ✅ **Metering** - AWS Marketplace usage tracking

**When to Use**:
- Production workloads with predictable traffic
- Need for advanced Kubernetes features
- Multi-service architectures
- Custom networking requirements

**Cost**:
- EKS cluster: $0.10/hour (~$73/month)
- Worker nodes: EC2 pricing (e.g., 2x t3.large = ~$150/month)
- RDS, Redis, data transfer

### Option 2: Amazon ECS Fargate (Serverless)
**File**: `aws/templates/ecs-fargate-deployment.yaml`

**Architecture**:
```
Internet → ALB → ECS Tasks (2-20, Fargate/Spot mix)
                      ↓
      RDS PostgreSQL + ElastiCache Redis + S3
```

**Features**:
- ✅ **No EC2 Management** - AWS manages infrastructure
- ✅ **Auto-scaling** - 2-20 tasks based on CPU/memory
- ✅ **Cost Optimization** - 80% Fargate Spot for non-critical workloads
- ✅ **Circuit Breaker** - Automatic rollback on failed deployments
- ✅ **Container Insights** - Built-in monitoring
- ✅ **Same database/cache** - RDS PostgreSQL Multi-AZ + Redis HA

**When to Use**:
- Variable or unpredictable traffic
- Don't want to manage servers/clusters
- Development/staging environments
- Cost-sensitive deployments

**Cost**:
- Fargate: ~$0.04/vCPU/hour + $0.004/GB/hour
- Example: 2 tasks (1 vCPU, 2GB each) = ~$60/month
- Plus RDS, Redis, data transfer
- **~40% cheaper than EKS** for small deployments

**Processor Optimizations**:
```yaml
# Support for ARM64 Graviton processors (~20% cost savings)
RuntimePlatform:
  CpuArchitecture: ARM64  # or X86_64
  OperatingSystemFamily: LINUX

# Use AOT-compiled images for faster cold starts
Image: ghcr.io/honua-io/honua-server:latest-aot-arm64
# Available variants:
# - latest (JIT, x64)
# - latest-aot (ReadyToRun AOT, x64, 50% faster startup)
# - latest-arm64 (JIT, ARM Graviton)
# - latest-aot-arm64 (AOT + Graviton, best price/performance)
# - lite (minimal, vector-only)
# - lite-aot-arm64 (minimal + AOT + Graviton, fastest cold start)
```

---

## Azure Marketplace Deployment Options

### Option 1: Azure Kubernetes Service (AKS)
**File**: `azure/templates/aks-deployment.json`

**Features**:
- ✅ **YARP Gateway** - Same as AWS deployment
- ✅ **Zone Redundant** - Across 3 availability zones
- ✅ **PostgreSQL Flexible Server** - Zone-redundant HA
- ✅ **Azure Cache for Redis** - Premium tier with replication (1-3 replicas)
- ✅ **Workload Identity** - Secure Azure service access
- ✅ **Auto-scaling** - HPA for gateway and backend

**Redis HA Configuration**:
```json
{
  "sku": "Premium",  // Required for HA
  "replicasPerMaster": 1,  // 1-3 replicas
  "shardCount": 1,  // For cluster mode
  "zones": ["1", "2", "3"]  // Zone redundancy
}
```

**Cost**:
- AKS: Free control plane
- Node pools: VM pricing (e.g., 2x Standard_D4s_v3 = ~$280/month)
- PostgreSQL, Redis Premium, storage

### Option 2: Azure Container Apps (Serverless)
**File**: `azure/templates/container-apps-deployment.json`

**Architecture**:
```
Internet → Container App (HTTPS, auto-scale 1-10)
                    ↓
  PostgreSQL Flexible + Redis Premium + Blob Storage
```

**Features**:
- ✅ **Fully Managed** - No cluster, no nodes to manage
- ✅ **Scale to Zero** - Pay nothing when idle (great for dev/test)
- ✅ **Auto TLS** - Automatic HTTPS certificates
- ✅ **Dapr Integration** - Built-in service-to-service communication
- ✅ **Zone Redundant** - Automatic HA across zones
- ✅ **Managed Identity** - Secure Azure access

**Scaling Rules**:
```json
{
  "scale": {
    "minReplicas": 1,  // Can be 0 for scale-to-zero
    "maxReplicas": 10,
    "rules": [
      {
        "name": "http-rule",
        "http": {"concurrentRequests": "50"}
      },
      {
        "name": "cpu-rule",
        "custom": {"type": "cpu", "value": "70"}
      },
      {
        "name": "memory-rule",
        "custom": {"type": "memory", "value": "80"}
      }
    ]
  }
}
```

**When to Use**:
- Development and testing (scale to zero when not in use)
- Variable traffic patterns
- Microservices architecture
- Simplified operations

**Cost**:
- Container Apps: $0.000012/vCPU-second + $0.000002/GiB-second
- Example: 1 replica running 24/7 (1 vCPU, 2GB) = ~$50/month
- Plus database, cache, storage
- **Scale to zero** = $0 when idle

---

## Google Cloud Marketplace Deployment Options

### Option 1: Google Kubernetes Engine (GKE)
**File**: `gcp/templates/gke-deployment.yaml`

**Features**:
- ✅ **Autopilot Mode** - Google manages nodes, OS, scaling
- ✅ **Cloud SQL PostgreSQL** - Regional HA with automatic failover
- ✅ **Memorystore Redis** - HA tier with replicas
- ✅ **Workload Identity** - Secure GCP service access
- ✅ **Auto-scaling** - Both cluster and application level

**Cost**:
- GKE Autopilot: $0.10/vCPU/hour + management fee
- Cloud SQL: Instance pricing + storage
- Memorystore: Redis pricing based on tier

### Option 2: Cloud Run (Serverless)
**Recommended for**: Variable traffic, lowest ops overhead

**Features**:
- ✅ **Fully Managed** - Zero infrastructure management
- ✅ **Scale to Zero** - No charges when idle
- ✅ **Request-based Scaling** - 0 to thousands of instances
- ✅ **Built-in CDN** - Global edge caching
- ✅ **VPC Connector** - Private access to Cloud SQL/Memorystore

**Cost**:
- Free tier: 2M requests/month, 360K vCPU-seconds, 180K GiB-seconds
- Beyond free tier: $0.00002400/vCPU-second + $0.00000250/GiB-second
- Extremely cost-effective for bursty workloads

---

## Database Options

All deployments support flexible database choices:

### AWS Database Options
```yaml
DatabaseOption:
  - create-postgresql      # RDS PostgreSQL (recommended)
  - create-mysql          # RDS MySQL
  - create-aurora-postgresql  # Aurora Serverless v2 PostgreSQL
  - create-aurora-mysql      # Aurora Serverless v2 MySQL
  - bring-your-own          # Existing database (provide connection string)
```

**Instance Classes**:
- `db.t3.*` - General purpose (Intel)
- `db.t4g.*` - General purpose (ARM Graviton, ~20% cheaper)
- `db.r5.*` - Memory optimized (Intel)
- `db.r6g.*` - Memory optimized (ARM Graviton, ~20% cheaper)

**Aurora Serverless v2**:
- Auto-scales from 0.5 to 4 ACUs (Aurora Capacity Units)
- Scales in 0.5 ACU increments
- Sub-second scaling
- Only pay for capacity used
- **Best for**: Variable workloads, dev/test

### Azure Database Options
```json
{
  "databaseOption": {
    "allowedValues": [
      "create-postgresql",  // PostgreSQL Flexible Server (default)
      "create-mysql",       // MySQL Flexible Server
      "bring-your-own"      // Existing database
    ]
  }
}
```

**PostgreSQL Flexible Server Tiers**:
- **Burstable**: B-series (cost-effective for dev/test)
- **General Purpose**: D-series (balanced)
- **Memory Optimized**: E-series (high memory)

### GCP Database Options
- **Cloud SQL PostgreSQL** - Managed PostgreSQL
- **Cloud SQL MySQL** - Managed MySQL
- **AlloyDB** - PostgreSQL-compatible, 4x faster
- **Spanner** - Globally distributed SQL
- **Firestore** - NoSQL document database

---

## Image Variants and Processor Optimizations

Honua IO provides multiple container image variants optimized for different scenarios:

### Image Variants

| Variant | Size | Startup Time | Use Case |
|---------|------|--------------|----------|
| `full` | ~180MB | ~15s | Production with all features |
| `full-aot` | ~200MB | **~8s** | Production, AOT compiled |
| `lite` | ~80MB | ~10s | Vector-only workloads |
| `lite-aot` | ~90MB | **~5s** | Vector-only, fastest startup |

### Architecture Support

| Architecture | Cost Savings | Performance | Availability |
|--------------|--------------|-------------|--------------|
| **x86_64 (Intel/AMD)** | Baseline | Baseline | All clouds |
| **ARM64 (Graviton/Ampere)** | **~20%** | 10-15% better | AWS, Azure, GCP |

**Recommended Configurations**:

1. **Production (Kubernetes)**: `full-aot` on x86_64
   - Best compatibility
   - Fast startup for pod scheduling
   - All features enabled

2. **Production (Cost-Optimized)**: `full-aot-arm64` on ARM
   - 20% cost savings
   - Better price/performance
   - All features enabled

3. **Serverless (ECS/Container Apps/Cloud Run)**: `lite-aot-arm64`
   - 50% faster cold starts
   - Lower memory footprint
   - 20% cost savings
   - Ideal for vector-only workloads

4. **Development/Testing**: `lite` on x86_64
   - Small image size
   - Fast pull times
   - Sufficient for testing

### ReadyToRun (AOT) Benefits

ReadyToRun is .NET's ahead-of-time compilation technology:

- **50% faster cold starts** - Pre-compiled code
- **Lower initial CPU** - No JIT compilation overhead
- **Consistent performance** - No JIT warmup period
- **Slightly larger images** - ~10-15% size increase

**When to use AOT**:
- ✅ Serverless deployments (ECS Fargate, Container Apps, Cloud Run)
- ✅ Auto-scaling workloads (frequent pod/container creation)
- ✅ Consistent latency requirements
- ✅ Cost-sensitive deployments (faster startup = less billable time)

**When JIT is fine**:
- Long-running pods in Kubernetes
- Single-instance deployments
- Development environments

---

## Deployment Decision Tree

```
Start
  ├─ Need Kubernetes features (custom controllers, operators)?
  │   ├─ Yes → EKS/AKS/GKE
  │   └─ No → Continue
  │
  ├─ Traffic pattern?
  │   ├─ Predictable/constant → EKS/AKS/GKE (lower per-request cost)
  │   ├─ Variable/bursty → ECS Fargate/Container Apps/Cloud Run
  │   └─ Mostly idle → Container Apps (scale to zero) or Cloud Run
  │
  ├─ Operations complexity tolerance?
  │   ├─ Can manage K8s → EKS/AKS/GKE
  │   ├─ Want simplicity → ECS Fargate/Container Apps
  │   └─ Zero ops → Cloud Run
  │
  └─ Cost optimization priority?
      ├─ High → Use ARM64 + AOT images + Serverless
      ├─ Medium → Standard x86_64 + Serverless
      └─ Low → Standard K8s deployment
```

## Cost Comparison Examples

**Scenario**: Small production deployment (2 replicas, 1 vCPU, 2GB RAM each, 24/7)

| Platform | Option | Monthly Cost* |
|----------|--------|---------------|
| AWS | EKS | ~$300 (cluster + nodes + RDS + Redis) |
| AWS | ECS Fargate (x86) | ~$180 (tasks + RDS + Redis) |
| AWS | ECS Fargate (ARM) | ~$145 (20% savings) |
| Azure | AKS | ~$350 (nodes + DB + Redis Premium) |
| Azure | Container Apps (x86) | ~$200 (apps + DB + Redis) |
| Azure | Container Apps (ARM) | ~$160 (20% savings) |
| GCP | GKE Autopilot | ~$280 (compute + DB + Redis) |
| GCP | Cloud Run | ~$50** (often in free tier) |

*Estimates exclude data transfer, storage, and marketplace fees
**Cloud Run with low traffic may stay in free tier

## Recommendations by Use Case

### 1. Enterprise Production
- **Choice**: Kubernetes (EKS/AKS/GKE)
- **Image**: `full-aot`
- **Arch**: x86_64 (maximum compatibility)
- **Database**: RDS/Aurora with Multi-AZ
- **Why**: Predictable costs, enterprise support, advanced features

### 2. Startup/SMB Production
- **Choice**: ECS Fargate or Container Apps
- **Image**: `full-aot-arm64`
- **Arch**: ARM64 (cost savings)
- **Database**: Aurora Serverless v2 or Azure Flexible Server
- **Why**: Lower ops overhead, cost-effective, scales with growth

### 3. Development/Staging
- **Choice**: Container Apps or Cloud Run
- **Image**: `lite`
- **Arch**: x86_64
- **Database**: Smallest tier or Aurora Serverless
- **Why**: Scale to zero when not in use, minimal cost

### 4. High-Traffic Public API
- **Choice**: Kubernetes with HPA
- **Image**: `full-aot-arm64`
- **Arch**: ARM64
- **Database**: Aurora or Cloud SQL HA
- **Why**: Consistent performance, cost optimization at scale

### 5. Bursty Workloads
- **Choice**: Cloud Run or Container Apps
- **Image**: `lite-aot-arm64`
- **Arch**: ARM64
- **Database**: Serverless database
- **Why**: Pay only for actual usage, instant scaling

## Next Steps

1. Choose your deployment option from the table above
2. Review the corresponding template file
3. Customize parameters (instance sizes, image variant, architecture)
4. Deploy using the provided scripts or CloudFormation/ARM/GCP console
5. Configure monitoring and alerts
6. Set up CI/CD for updates

For detailed deployment instructions, see:
- AWS EKS: `aws/docs/README.md`
- AWS ECS: `aws/docs/ECS_DEPLOYMENT.md` (create this)
- Azure AKS: `azure/docs/README.md` (create this)
- Azure Container Apps: `azure/docs/CONTAINER_APPS.md` (create this)
- GCP: `gcp/docs/README.md` (create this)
