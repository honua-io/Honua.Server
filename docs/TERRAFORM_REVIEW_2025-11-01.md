# Comprehensive Terraform Infrastructure Review for HonuaIO

**Date**: November 1, 2025
**Project**: HonuaIO - Cloud-Native Geospatial Server
**Review Scope**: Terraform coverage vs actual deployment options
**Status**: SIGNIFICANT GAPS IDENTIFIED

---

## Executive Summary

The HonuaIO project has undergone significant architectural changes with the introduction of modular deployments (Full, Lite, Cloud-specific), but the Terraform infrastructure has **NOT been updated to reflect these new deployment options**. Critical gaps exist between what the codebase now supports and what Terraform provisions.

**Key Findings:**
- ✅ **Good**: Terraform has solid multi-region, multi-cloud foundation (AWS/Azure/GCP)
- ⚠️ **Critical**: No Terraform for serverless deployments (Cloud Run, Lambda, Container Apps)
- ⚠️ **Critical**: No Terraform for Lite deployment variant
- ⚠️ **Major**: Kubernetes/Helm configurations exist but limited Terraform coverage
- ⚠️ **Major**: Docker Compose deployments widely used but no Terraform support
- ❌ **Missing**: Enterprise/Functions serverless deployments not provisioned by Terraform

---

## Part 1: Current Terraform Coverage

### 1.1 What Terraform Currently Provisions

#### Cloud Providers Supported
- ✅ **AWS** (Primary support with most modules)
- ✅ **Azure** (Comprehensive support with alerts, Key Vault)
- ✅ **GCP** (Uptime monitoring only, limited VM/container support)

#### Infrastructure Components

**Database Layer** (ALL CLOUDS)
- ✅ PostgreSQL managed databases (AWS RDS, Azure PostgreSQL, GCP Cloud SQL)
- ✅ Connection pooling (PgBouncer via Secrets Manager)
- ✅ Automated backups and read replicas
- ✅ High availability configurations
- ✅ Secret storage (AWS Secrets Manager, Azure Key Vault, GCP Secret Manager)

**Compute Layer** (KUBERNETES-FOCUSED)
- ✅ Kubernetes clusters (AWS EKS, Azure AKS, GCP GKE)
- ✅ Node groups with auto-scaling
- ✅ Network policies, RBAC, security contexts
- ⚠️ Container orchestration (Kubernetes only - no ECS, Cloud Run, Container Apps)

**Networking**
- ✅ VPCs/VNets with public/private subnets
- ✅ Security groups/NSGs
- ✅ Load balancers (regional)
- ⚠️ Global load balancer (Route53 for AWS, Front Door for Azure, LB for GCP)

**Caching**
- ✅ Redis clusters (AWS ElastiCache, Azure Cache, GCP Memorystore)
- ✅ High availability and replication

**Container Registry**
- ✅ AWS ECR repositories
- ⚠️ Azure Container Registry (referenced but limited detail)
- ⚠️ GCP Artifact Registry (not found)

**Monitoring & Logging**
- ✅ Application Insights (Azure)
- ✅ CloudWatch (AWS)
- ✅ Prometheus/Grafana via Kubernetes
- ✅ Alert rules for all clouds
- ✅ Uptime/synthetic monitoring (Azure, AWS, GCP)

**Security & IAM**
- ✅ KMS encryption keys
- ✅ IAM roles/policies (AWS)
- ✅ RBAC (Azure, GCP)
- ✅ GitHub OIDC for deployments
- ✅ Secret rotation automation

**State Management**
- ✅ Remote state backends (AWS S3+DynamoDB, Azure Blob+Leasing)
- ✅ State locking mechanisms

#### Environments Supported
- ✅ Development (AWS only - partial)
- ✅ Staging (mentioned but minimal Terraform)
- ✅ Production (multi-region with DR)
- ❌ Single-region serverless (Cloud Run, Lambda)

### 1.2 Architecture Patterns

**Development Environment (AWS)**
```
├─ Networking (VPC, subnets, security groups)
├─ Kubernetes (EKS with 2-4 nodes)
├─ PostgreSQL (t4g.medium, 50-100GB)
├─ Redis (single node, no clustering)
└─ Monitoring (Prometheus, Grafana, CloudWatch)
```

**Multi-Region Production**
```
Primary Region (AWS/Azure/GCP)
├─ Kubernetes cluster (full-featured)
├─ PostgreSQL (multi-AZ, high availability)
├─ Redis (clustered, HA)
├─ Global load balancer
└─ Monitoring & alerting

DR Region (AWS/Azure/GCP)
├─ Kubernetes cluster (scaled-down)
├─ Database read replica
├─ Storage replication
├─ Auto-failover via Route53/Front Door
└─ Same monitoring setup
```

---

## Part 2: Actual Deployment Options in Codebase

### 2.1 Source Code Structure (Post-Modular Refactoring)

```
src/
├─ Honua.Server.Core/                      # Base (both Full & Lite)
│  ├─ Vector data sources (PostGIS, MySQL, SQLite, SQL Server)
│  ├─ NetTopologySuite (geometry operations)
│  ├─ Authentication & Authorization
│  ├─ Caching, metrics, core utilities
│  └─ NOTE: SkiaSharp for rendering STAYS HERE (both variants use it)
│
├─ Honua.Server.Core.Raster/               # Full only (NEW)
│  ├─ GDAL support (GeoTIFF, COG, satellite imagery)
│  ├─ SkiaSharp raster rendering
│  ├─ Parquet/Arrow (columnar data)
│  ├─ LibGit2Sharp (Git operations)
│  └─ LibTIFF (TIFF support)
│
├─ Honua.Server.Core.OData/                # Both Full & Lite (NEW)
│  └─ Microsoft.OData.Core & related packages
│
├─ Honua.Server.Core.Cloud/                # Full only (NEW)
│  ├─ AWS SDK (S3, KMS)
│  ├─ Azure SDK (Blob Storage, Resource Manager)
│  └─ Google Cloud SDK (GCS, KMS)
│
├─ Honua.Server.Host/                      # Full deployment entry point
│  ├─ References: Core + Raster + OData + Cloud
│  ├─ Image size: ~150MB (chiseled)
│  ├─ Startup: 3-5 seconds (ReadyToRun)
│  └─ Base: .NET 9 (aspnet:9.0-noble-chiseled)
│
├─ Honua.Server.Host.Lite/                 # Lite deployment entry point (NEW)
│  ├─ References: Core + OData only
│  ├─ Image size: ~50-60MB (Alpine)
│  ├─ Startup: <2 seconds (ReadyToRun)
│  └─ Base: .NET 9 (aspnet:9.0-alpine)
│  └─ Features: Vector-only, NO GDAL, NO Cloud SDKs
│
├─ Honua.Server.Enterprise/                # Enterprise features
│  └─ Dashboard, additional features
│
├─ Honua.Server.Enterprise.Functions/      # Serverless functions (existing)
│  ├─ References: Core only (minimal)
│  ├─ Target: Background jobs, timers
│  └─ Size: ~35MB
│
├─ Honua.Server.Intake/                    # Data ingestion service
│  └─ Processes geographic data imports
│
├─ Honua.Server.Gateway/                   # API gateway
│  └─ Routing, request processing
│
├─ Honua.Server.Observability/             # Observability features
│  └─ Telemetry, metrics, tracing
│
└─ Honua.Server.AlertReceiver/             # Alert receiver service
   └─ Processes incoming alerts
```

### 2.2 Deployment Options Available

#### Option 1: Full Deployment (Honua.Server.Host)
**Image**: `Dockerfile` (chiseled, ~150MB)
**Startup**: 3-5 seconds
**Best For**: Traditional deployments

**Features**:
- ✅ Vector data (PostGIS, MySQL, SQLite, SQL Server)
- ✅ Raster data via GDAL (GeoTIFF, COG)
- ✅ Cloud storage (AWS S3, Azure Blob, GCP GCS)
- ✅ Map rendering (SkiaSharp)
- ✅ All OGC APIs (WFS, WMS, WCS, WMTS)
- ✅ OData protocol
- ✅ STAC catalog

**Supported Platforms**:
- Docker Compose
- Kubernetes (via Helm or manual YAML)
- VMs with Docker
- Cloud containers (but no serverless)

#### Option 2: Lite Deployment (Honua.Server.Host.Lite) - NEW
**Image**: `Dockerfile.lite` (Alpine, ~50-60MB)
**Startup**: <2 seconds
**Best For**: Serverless platforms

**Features**:
- ✅ Vector data (PostGIS, MySQL, SQLite, SQL Server)
- ✅ Map rendering (SkiaSharp) ← Still available!
- ✅ OData protocol
- ✅ Vector OGC APIs (WFS, OGC API Features, vector WMTS)
- ✅ Vector tiles (MVT)
- ❌ NO raster data (no GDAL)
- ❌ NO cloud storage SDKs

**Supported Platforms**:
- Google Cloud Run ← **NOT IN TERRAFORM**
- AWS Lambda (Container) ← **NOT IN TERRAFORM**
- Azure Container Apps ← **NOT IN TERRAFORM**
- Docker (for testing)

#### Option 3: Enterprise Functions (Honua.Server.Enterprise.Functions)
**Target**: Serverless background jobs
**References**: Core only (minimal)
**Size**: ~35MB
**Platforms**:
- Azure Functions ← **NOT IN TERRAFORM**
- AWS Lambda ← **NOT IN TERRAFORM**

#### Option 4: Docker Compose (Development/Testing)
**Configurations**:
- `docker-compose.yml` - Basic (Honua + PostgreSQL)
- `docker-compose.full.yml` - Full stack (+ Jaeger + Prometheus + Grafana)

**Services**:
- Honua Server
- PostgreSQL with PostGIS
- Jaeger (distributed tracing)
- Prometheus (metrics)
- Grafana (visualization)
- AlertManager

#### Option 5: Kubernetes (Production)
**Configurations**:
- Helm chart: `deployment/helm/honua/`
- Kustomize base: `deployment/k8s/base/`
- Cloud-specific overlays: `deployment/cloud/{aws,azure,gcp}/`

**Services**:
- Honua API deployment
- PostgreSQL StatefulSet
- Intake service
- Gateway service
- Orchestrator
- Monitoring (ServiceMonitors for Prometheus)

#### Option 6: Cloud-Specific Configurations
**AWS**:
- Gateway config for ALB/NLB
- EKS-specific network policies
- EC2 security groups

**Azure**:
- AKS-specific configurations
- Azure DNS cluster issuer
- Application Gateway support

**GCP**:
- GKE-specific configurations
- Cloud Load Balancing
- Workload Identity integration

---

## Part 3: The Gap Analysis

### 3.1 CRITICAL GAPS: Serverless/Lite Deployment

**Problem**: Honua now has Lite deployment option for serverless platforms, but Terraform cannot provision it.

**Missing Terraform**:
- ❌ Google Cloud Run (complete stack)
- ❌ AWS Lambda Container (complete stack)
- ❌ Azure Container Apps (complete stack)
- ❌ Database provisioning for serverless (with connection pooling)
- ❌ Secret management for serverless functions
- ❌ Auto-scaling for serverless (handled by platform)

**What's Needed**:
```
infrastructure/terraform/serverless/
├─ google-cloud-run/
│  ├─ main.tf (Cloud Run service, Cloud SQL, Cloud Storage, KMS)
│  ├─ variables.tf
│  └─ outputs.tf
├─ aws-lambda/
│  ├─ main.tf (Lambda, RDS, S3, IAM)
│  ├─ variables.tf
│  └─ outputs.tf
├─ azure-container-apps/
│  ├─ main.tf (Container Apps, PostgreSQL, Blob, Key Vault)
│  ├─ variables.tf
│  └─ outputs.tf
└─ shared/
   ├─ database/
   ├─ networking/
   └─ monitoring/
```

**Impact**: Organizations using serverless must manually provision infrastructure outside Terraform.

### 3.2 MAJOR GAPS: Docker Compose

**Problem**: Docker Compose configurations exist and are commonly used, but no Terraform provisions the underlying infrastructure.

**Missing Terraform**:
- ❌ Docker host provisioning (EC2, VM, etc.)
- ❌ Docker networking setup
- ❌ Volume management
- ❌ PostgreSQL container orchestration
- ❌ Monitoring stack infrastructure

**Impact**: Teams running docker-compose locally or on self-managed hosts cannot use Terraform for infrastructure.

### 3.3 MAJOR GAPS: Container Registry Support

**Missing**:
- ❌ GCP Artifact Registry (Terraform exists for modules, no registry)
- ⚠️ Azure Container Registry (mentioned in modules but minimal Terraform)
- ⚠️ AWS ECR (only Dev environment has it, not Production)

**Impact**: Multi-cloud deployments need manual registry configuration.

### 3.4 MAJOR GAPS: Lite Variant Not in Kubernetes Terraform

**Problem**: Kubernetes Terraform provisions Honua.Server.Host (Full) but cannot deploy Host.Lite

**What's Missing**:
- ❌ Separate deployment definitions for Lite variant
- ❌ Different image tag/registry references
- ❌ Resource limits optimized for Lite (smaller footprint)
- ❌ Service account/RBAC for Lite-specific features

**Kubernetes manifests exist**:
- ✅ `deployment/k8s/base/deployment-host.yaml` (Full)
- ❌ `deployment/k8s/base/deployment-host-lite.yaml` (MISSING)

### 3.5 MAJOR GAPS: Other Services Not in Terraform

**Services exist but no Terraform**:
- ❌ `Honua.Server.Intake` (data ingestion) - Kubernetes YAML only
- ❌ `Honua.Server.Gateway` (API gateway) - Kubernetes YAML only
- ❌ `Honua.Server.AlertReceiver` (alert processing) - Kubernetes YAML only
- ❌ `Honua.Server.Observability` (observability features) - Not provisioned
- ❌ `Honua.Server.Enterprise` (enterprise features) - Kubernetes YAML only
- ❌ `Honua.Server.Enterprise.Dashboard` (dashboard) - No Terraform or K8s

**Impact**: Only Honua.Server.Host and PostgreSQL are fully managed by Terraform.

### 3.6 MODERATE GAPS: GCP Support

**Current GCP Terraform**:
- ✅ Uptime checks only
- ❌ GCP regional stack (AWS & Azure have this)
- ❌ GCP global load balancer (AWS & Azure have this)
- ❌ GCP Cloud Run (serverless)
- ❌ GCP native services (Cloud Storage, BigQuery connectors)

### 3.7 ISSUE: Cloud Run Dockerfile Outdated

**File**: `deploy/gcp/Dockerfile.cloudrun`

**Problems**:
- Uses .NET 8 (codebase is .NET 9)
- References Full deployment (Honua.Server.Host) instead of Lite
- Includes GDAL (unnecessary for serverless, adds 40MB)
- No trimming/optimization for serverless
- Doesn't exist in build - no modern Cloud Run optimization

---

## Part 4: What's Being Used in Practice

### 4.1 Docker Compose (MOST USED)
```
docker/
├─ docker-compose.yml (basic: Honua + PostgreSQL)
├─ docker-compose.full.yml (+ observability stack)
├─ docker-compose.prometheus.yml (+ Prometheus only)
├─ grafana/ (dashboards)
├─ prometheus/ (config)
└─ alertmanager/ (config)
```

**Status**: Widely used for development, no Terraform support.

### 4.2 Kubernetes (PRODUCTION)
```
deployment/
├─ k8s/base/ (base deployments, manifests)
├─ k8s/overlays/ (environment overrides - NOT FOUND)
├─ helm/honua/ (Helm chart with values-dev/staging/prod)
└─ cloud/
   ├─ aws/eks-config.yaml
   ├─ azure/aks-config.yaml
   └─ gcp/gke-config.yaml
```

**Status**: Full Kubernetes deployments exist, but Terraform only provisions cluster infrastructure (not application deployments).

### 4.3 Terraform's Actual Use

**Current Terraform is for**:
1. Infrastructure foundation (networking, compute, databases)
2. Multi-region deployments with automatic failover
3. State backend setup
4. Monitoring infrastructure
5. Secret rotation automation

**NOT for**:
1. Application deployment specifics (uses Kubernetes manifests instead)
2. Serverless deployments
3. Docker Compose infrastructure
4. Other services (Intake, Gateway, AlertReceiver)

---

## Part 5: Detailed Recommendations

### PRIORITY 1: CRITICAL - Serverless Deployment Support

**Create**: `infrastructure/terraform/serverless/`

#### A. Google Cloud Run Module
**File**: `infrastructure/terraform/serverless/google-cloud-run/main.tf`

```hcl
# Cloud Run service
resource "google_cloud_run_service" "honua_lite" {
  name     = "honua-api"
  location = var.region
  project  = var.project_id

  template {
    spec {
      containers {
        image = var.container_image  # honua:lite
        env {
          name  = "ConnectionStrings__DefaultConnection"
          value = google_sql_database_instance.db.connection_name
        }
      }
      service_account_email = google_service_account.honua.email
    }
  }
}

# Cloud SQL for PostgreSQL
resource "google_sql_database_instance" "db" {
  name             = "honua-postgres"
  database_version = "POSTGRES_15"
  region           = var.region
  
  settings {
    tier              = "db-f1-micro"  # Serverless-friendly
    availability_type = "REGIONAL"     # HA
  }
}

# Cloud Storage for backups
resource "google_storage_bucket" "backups" {
  name          = "honua-backups-${random_string.suffix.result}"
  location      = var.region
  force_destroy = false
  
  versioning {
    enabled = true
  }
}

# Cloud KMS for encryption
resource "google_kms_key_ring" "honua" {
  name     = "honua-keyring"
  location = var.region
}
```

#### B. AWS Lambda Container Module
**File**: `infrastructure/terraform/serverless/aws-lambda/main.tf`

```hcl
# Lambda function (container image)
resource "aws_lambda_function" "honua_lite" {
  filename      = "honua-lite-lambda.zip"  # or via ECR
  function_name = "honua-api"
  role          = aws_iam_role.honua.arn
  handler       = "index.handler"  # Not used for container, but required
  
  container_image_uri = "${aws_ecr_repository.honua.repository_url}:lite"
  
  environment {
    variables = {
      ConnectionStrings__DefaultConnection = var.db_connection_string
    }
  }
  
  timeout       = 60
  memory_size   = 1024
}

# API Gateway for HTTP access
resource "aws_apigatewayv2_api" "honua" {
  name          = "honua-api"
  protocol_type = "HTTP"
  
  cors_configuration {
    allow_origins = ["*"]
    allow_methods = ["*"]
    allow_headers = ["*"]
  }
}

# RDS for PostgreSQL
resource "aws_db_instance" "honua" {
  identifier     = "honua-serverless"
  engine         = "postgres"
  engine_version = "15"
  instance_class = "db.t4g.small"
  
  # Aurora Serverless v2 alternative
  # db_cluster_identifier = aws_rds_cluster.honua.id
}

# RDS Proxy for connection pooling
resource "aws_db_proxy" "honua" {
  name                   = "honua-proxy"
  engine_family          = "POSTGRESQL"
  auth {
    secret_arn = aws_secretsmanager_secret.db_password.arn
  }
  role_arn               = aws_iam_role.proxy.arn
  max_connections        = 100
}
```

#### C. Azure Container Apps Module
**File**: `infrastructure/terraform/serverless/azure-container-apps/main.tf`

```hcl
# Container Apps environment
resource "azurerm_container_app_environment" "honua" {
  name                = "honua-env"
  location            = var.location
  resource_group_name = var.resource_group_name
  
  infrastructure_subnet_id = var.subnet_id
}

# Container App (Lite)
resource "azurerm_container_app" "honua" {
  name                = "honua-api"
  container_app_environment_id = azurerm_container_app_environment.honua.id
  resource_group_name = var.resource_group_name
  
  template {
    container {
      name   = "honua-lite"
      image  = var.container_image  # honua:lite
      cpu    = 0.5
      memory = "1Gi"
      
      env {
        name  = "ConnectionStrings__DefaultConnection"
        value = "@connectionString"  # From secret
      }
    }
    scale {
      min_replicas = 0     # Scale to zero for cost
      max_replicas = 10
    }
  }
  
  secret {
    name  = "connectionString"
    value = azurerm_postgresql_flexible_server_database_connection.honua.server_fqdn
  }
}

# PostgreSQL Flexible Server
resource "azurerm_postgresql_flexible_server" "honua" {
  name                   = "honua-postgres"
  location               = var.location
  resource_group_name    = var.resource_group_name
  version                = "15"
  
  sku_name = "B_Standard_B1s"  # Small for serverless
}
```

### PRIORITY 2: HIGH - Lite Variant Support in Kubernetes

**Update**: `infrastructure/terraform/modules/kubernetes-cluster/main.tf` and related

**Changes Needed**:
1. Add deployment template for Honua.Server.Host.Lite
2. Add Helm chart values for lite variant
3. Add service definitions for both Full and Lite
4. Add network policies for both
5. Add horizontal pod autoscaling specific to Lite

**Example**:
```hcl
# New deployment for Lite variant
resource "kubernetes_deployment" "honua_lite" {
  count = var.deploy_lite_variant ? 1 : 0
  
  metadata {
    name      = "honua-lite"
    namespace = "honua"
  }
  
  spec {
    replicas = var.lite_replicas
    
    template {
      spec {
        container {
          name  = "honua-lite"
          image = "${var.container_registry}/honua:lite"
          
          # Smaller resource limits for Lite
          resources {
            requests = {
              cpu    = "100m"
              memory = "256Mi"
            }
            limits = {
              cpu    = "500m"
              memory = "512Mi"
            }
          }
        }
      }
    }
  }
}
```

### PRIORITY 3: HIGH - Container Registry Terraform

**Create**: `infrastructure/terraform/modules/container-registry/main.tf`

```hcl
# AWS ECR
resource "aws_ecr_repository" "honua" {
  count = var.cloud_provider == "aws" ? 1 : 0
  
  name                 = "honua"
  image_tag_mutability = "IMMUTABLE"
  
  image_scanning_configuration {
    scan_on_push = true
  }
  
  encryption_configuration {
    encryption_type = "KMS"
    kms_key        = var.kms_key_arn
  }
}

# Azure Container Registry
resource "azurerm_container_registry" "honua" {
  count = var.cloud_provider == "azure" ? 1 : 0
  
  name                = "honua${random_string.suffix.result}"
  resource_group_name = var.resource_group_name
  location            = var.location
  
  sku      = "Standard"
  admin_enabled = false
  
  encryption {
    enabled            = true
    key_vault_key_id   = var.key_vault_key_id
  }
}

# GCP Artifact Registry
resource "google_artifact_registry_repository" "honua" {
  count    = var.cloud_provider == "gcp" ? 1 : 0
  
  location = var.region
  repository_id = "honua"
  format = "DOCKER"
  
  kms_key_name = var.kms_key_name
}
```

### PRIORITY 4: MEDIUM - Docker Host Infrastructure

**Create**: `infrastructure/terraform/docker-host/`

```hcl
# EC2 instance for Docker Compose
resource "aws_instance" "docker_host" {
  ami           = data.aws_ami.ubuntu.id
  instance_type = "t3.medium"
  
  vpc_security_group_ids = [aws_security_group.docker_host.id]
  
  # Install Docker
  user_data = base64encode(<<-EOF
    #!/bin/bash
    apt-get update
    apt-get install -y docker.io docker-compose
  EOF
  )
  
  root_block_device {
    volume_size = 100
    volume_type = "gp3"
    encrypted   = true
  }
}

# Persistent storage
resource "aws_ebs_volume" "postgres_data" {
  availability_zone = aws_instance.docker_host.availability_zone
  size              = 100
  encrypted         = true
  kms_key_id       = var.kms_key_arn
}
```

### PRIORITY 5: MEDIUM - Other Services Terraform

**Create**: `infrastructure/terraform/modules/honua-services/`

Modules for:
- `intake-service` (data ingestion)
- `gateway-service` (API gateway)
- `alert-receiver` (alert processing)
- `observability-service` (telemetry)
- `enterprise-dashboard` (web dashboard)

Each should include:
- Kubernetes deployment definition (or Terraform kubernetes provider)
- Service definition
- ConfigMaps and Secrets
- RBAC (ServiceAccount, ClusterRole, ClusterRoleBinding)
- Resource limits and requests
- Probes (liveness, readiness, startup)

### PRIORITY 6: MEDIUM - GCP Enhancements

**Create/Enhance**:
1. `infrastructure/terraform/gcp/regional-stack/` (similar to AWS/Azure)
2. `infrastructure/terraform/gcp/global-lb/` (Cloud Load Balancing)
3. `infrastructure/terraform/gcp/cloud-run/` (serverless)
4. `infrastructure/terraform/gcp/artifact-registry/` (container registry)

### PRIORITY 7: LOW - Dockerfile Updates

**Update**: `deploy/gcp/Dockerfile.cloudrun`

```dockerfile
# Use Lite image, not Full
FROM honua:lite as base

# Optimize for Cloud Run
ENV PORT=8080
ENV K_SERVICE=honua-api
ENV ASPNETCORE_ENVIRONMENT=Production

# Use cloud-native settings
RUN echo "Optimized for Cloud Run"
```

Or better: **DELETE** `Dockerfile.cloudrun` and use `Dockerfile.lite` instead with:
```bash
docker build -t honua:lite -f Dockerfile.lite .
```

---

## Part 6: Updated File Organization Recommendation

```
infrastructure/terraform/
├─ README.md (update with new structure)
├─
├─ # Core Infrastructure (existing)
├─ state-backend/
├─ secret-rotation/
├─ modules/
│  ├─ networking/
│  ├─ database/
│  ├─ redis/
│  ├─ kubernetes-cluster/ (update for Lite)
│  ├─ iam/
│  ├─ monitoring/
│  ├─ registry/ (NEW)
│  └─ honua-services/ (NEW for Intake, Gateway, etc.)
│
├─ # Environment-Specific (existing)
├─ environments/
│  ├─ dev/
│  ├─ staging/
│  └─ production/
│
├─ # Cloud-Specific (existing)
├─ aws/
├─ azure/
├─ gcp/ (expand)
├─ cloudflare/
│
├─ # Deployment Patterns (existing + NEW)
├─ multi-region/ (existing)
│
├─ # NEW: Serverless Deployments
├─ serverless/
│  ├─ google-cloud-run/
│  │  ├─ main.tf
│  │  ├─ variables.tf
│  │  ├─ outputs.tf
│  │  └─ terraform.tfvars.example
│  │
│  ├─ aws-lambda/
│  │  ├─ main.tf
│  │  ├─ variables.tf
│  │  ├─ outputs.tf
│  │  └─ terraform.tfvars.example
│  │
│  └─ azure-container-apps/
│     ├─ main.tf
│     ├─ variables.tf
│     ├─ outputs.tf
│     └─ terraform.tfvars.example
│
├─ # NEW: Self-Hosted Deployments
├─ self-hosted/
│  ├─ docker-host/
│  │  ├─ main.tf
│  │  └─ terraform.tfvars.example
│  │
│  └─ vm-host/
│     ├─ aws/
│     ├─ azure/
│     └─ gcp/
│
└─ # NEW: Application Deployments
└─ application/ (optional: for deploying via Terraform)
   ├─ kubernetes/
   │  ├─ full-deployment.tf
   │  └─ lite-deployment.tf
   │
   └─ helm/
      └─ honua-server/
```

---

## Part 7: Implementation Priority Matrix

| Component | Priority | Effort | Impact | Owner |
|-----------|----------|--------|--------|-------|
| Google Cloud Run | P1 | 2-3 days | Organizations can't deploy serverless | DevOps |
| AWS Lambda Container | P1 | 2-3 days | AWS users stuck with Kubernetes | DevOps |
| Azure Container Apps | P1 | 2-3 days | Azure users stuck with AKS | DevOps |
| Lite variant in K8s | P2 | 1-2 days | Can't optimize K8s costs | DevOps |
| Container Registries | P2 | 1 day | Manual registry setup needed | DevOps |
| GCP Regional Stack | P2 | 2-3 days | GCP parity with AWS/Azure | DevOps |
| Other Services (Intake, Gateway) | P3 | 3-5 days | Services not IaC provisioned | DevOps |
| Docker Host Infrastructure | P3 | 1-2 days | Docker Compose users unserved | DevOps |
| Enterprise Dashboard | P3 | 1-2 days | No IaC for dashboard | DevOps |

---

## Part 8: Documentation Updates Needed

### Create New Documentation

1. **`docs/deployment/serverless.md`**
   - Cloud Run, Lambda, Container Apps setup
   - Lite variant deployment specifics
   - Cost comparison
   - Auto-scaling considerations

2. **`docs/deployment/docker-compose-terraform.md`**
   - How to use Terraform to provision Docker hosts
   - Integration with docker-compose files
   - State management considerations

3. **`docs/deployment/multi-variant.md`**
   - Full vs Lite deployment decision matrix
   - Feature comparison at deployment time
   - Migration paths between variants

4. **`infrastructure/terraform/DEPLOYMENT_OPTIONS.md`**
   - Complete matrix of supported deployments
   - What Terraform provisions for each
   - What manual steps remain

5. **`infrastructure/terraform/serverless/README.md`**
   - Serverless-specific setup
   - Platform-specific considerations
   - Cost analysis

### Update Existing Documentation

1. **`infrastructure/terraform/README.md`** (missing - create!)
   - Link to all subdirectories
   - High-level architecture decision tree
   - Platform selection guide

2. **`docs/DEPLOYMENT.md`** (update)
   - Add Terraform references for each option
   - Document which components use what
   - Link to Infrastructure-as-Code docs

3. **`docs/MODULAR_ARCHITECTURE_PLAN.md`** (update)
   - Add Terraform deployment details
   - Link to serverless modules
   - Update on code migration status

---

## Part 9: Testing Recommendations

### Validation Checklist

For each new Terraform module:

```hcl
# 1. Syntax validation
terraform fmt -check
terraform validate

# 2. Security scanning
tfsec .
checkov -d .

# 3. Cost estimation
terraform plan -out=tfplan
terraform show tfplan | grep "# aws_"

# 4. Variables documentation
terraform-docs markdown .

# 5. Example deployment
terraform apply -auto-approve
# Run tests
terraform destroy -auto-approve
```

### Integration Testing

1. Deploy Lite variant to serverless platform
2. Verify cold start < 2 seconds
3. Test database connectivity
4. Verify all OData endpoints work
5. Test scaling to zero

---

## Summary: Key Decisions Needed

**Questions for the team**:

1. **Terraform Scope**: Should all deployments (Docker, serverless, K8s) be Terraform-managed, or just cloud infrastructure?

2. **Lite Adoption**: How quickly should Lite variant be available in all deployment patterns?

3. **Kubernetes Deployment**: Should application manifests move into Terraform (kubernetes provider) or stay separate (Helm)?

4. **Other Services**: Should Intake, Gateway, AlertReceiver be moved into Terraform or remain Kubernetes-only?

5. **Deprecated Configs**: Should old deployment/docker files be removed after Terraform is in place?

---

## Final Recommendations

1. **START**: Serverless modules (P1) - highest value, unblocks major use case
2. **PARALLEL**: Update Kubernetes for Lite variant 
3. **FOLLOW**: Container registry modules for all clouds
4. **NEXT**: GCP parity and other services
5. **LATER**: Optional Docker host and self-hosted modules

**Timeline**: 4-6 weeks for P1+P2 items with 2-3 person team

