# Deployment Documentation and Architecture Tracking Guide

**Keywords**: deployment-documentation, architecture-documentation, infrastructure-documentation, runbooks, change-management, documentation-automation, IaC-documentation, system-architecture, deployment-tracking, configuration-management

## Table of Contents

1. [Documentation Standards](#documentation-standards)
2. [Deployment Documentation Templates](#deployment-documentation-templates)
3. [Architecture Documentation](#architecture-documentation)
4. [Work Tracking and Runbooks](#work-tracking-and-runbooks)
5. [Auto-Documentation Tools](#auto-documentation-tools)
6. [Documentation as Code](#documentation-as-code)
7. [Post-Deployment Documentation](#post-deployment-documentation)
8. [Best Practices](#best-practices)

## Documentation Standards

### What to Document During Deployment

#### Pre-Deployment Documentation
```markdown
# Deployment Plan: [Project Name] - [Version]

## Deployment Metadata
- **Date**: YYYY-MM-DD
- **Environment**: Production/Staging/Development
- **Deployed By**: [Name/Team]
- **Deployment Type**: Initial/Update/Rollback/Hotfix
- **Risk Level**: Low/Medium/High/Critical

## Scope
### Components Being Deployed
- Service A: v1.2.3 -> v1.3.0
- Database Schema: v5 -> v6
- Infrastructure: [Changes]

### Dependencies
- External Services: [List]
- Third-party APIs: [List]
- Infrastructure Dependencies: [List]

## Impact Analysis
### Services Affected
- [Service Name]: [Impact Description]
- Expected Downtime: [Duration]
- Affected Users: [Estimate]

### Data Migration
- Migration Scripts: [List]
- Data Volume: [Size]
- Estimated Duration: [Time]
- Rollback Strategy: [Description]

## Pre-Deployment Checklist
- [ ] Code reviewed and approved
- [ ] Tests passing (unit, integration, e2e)
- [ ] Security scan completed
- [ ] Performance testing completed
- [ ] Backup verified
- [ ] Rollback plan documented
- [ ] Monitoring alerts configured
- [ ] Stakeholders notified
- [ ] Maintenance window scheduled
- [ ] Change approval obtained
```

#### During-Deployment Tracking
```markdown
# Deployment Execution Log

## Timeline
| Time | Step | Status | Notes |
|------|------|--------|-------|
| 14:00 | Backup initiated | ✓ | Backup ID: backup-20250104-1400 |
| 14:15 | Traffic drained | ✓ | Load balancer updated |
| 14:20 | Service stopped | ✓ | All instances gracefully stopped |
| 14:25 | Database migration started | In Progress | Running script v6-migration.sql |

## Issues Encountered
| Issue | Time | Severity | Resolution | Resolved By |
|-------|------|----------|------------|-------------|
| Connection timeout | 14:22 | Medium | Increased timeout setting | John Doe |

## Commands Executed
```bash
# Backup database
pg_dump -h prod-db.example.com -U admin honua_db > backup-20250104.sql

# Apply migration
psql -h prod-db.example.com -U admin honua_db -f migrations/v6-migration.sql

# Deploy new version
kubectl apply -f k8s/deployment-v1.3.0.yaml

# Verify deployment
kubectl rollout status deployment/honua-api -n production
```

## Verification Steps
- [ ] Health checks passing
- [ ] Smoke tests completed
- [ ] Performance metrics within acceptable range
- [ ] No error spikes in logs
- [ ] User acceptance testing completed
```

#### Post-Deployment Documentation
```markdown
# Post-Deployment Report

## Deployment Summary
- **Status**: Successful/Failed/Partial
- **Start Time**: 14:00 UTC
- **End Time**: 15:30 UTC
- **Total Duration**: 1h 30m
- **Downtime**: 15 minutes (planned: 20 minutes)

## What Was Deployed
### Version Information
- Application: v1.3.0
- Database Schema: v6
- Infrastructure: Updated autoscaling policies

### Changes Implemented
1. New authentication service
2. Database schema updates for user preferences
3. Performance optimization for API endpoints
4. Security patches for dependency vulnerabilities

## Metrics
### Performance
- API Response Time: 95th percentile 150ms (target: <200ms)
- Database Query Time: avg 25ms (baseline: 30ms)
- Error Rate: 0.02% (target: <0.1%)

### Resource Utilization
- CPU Usage: 45% (baseline: 50%)
- Memory Usage: 60% (baseline: 55%)
- Network I/O: Normal

## Issues and Resolutions
### Issue 1: Database Connection Pool Exhaustion
- **Severity**: Medium
- **Impact**: Brief increase in response times
- **Root Cause**: New service connections not configured correctly
- **Resolution**: Updated connection pool settings
- **Time to Resolve**: 10 minutes

## Rollback Information
- **Rollback Performed**: No
- **Rollback Procedure**: [Link to runbook]
- **Estimated Rollback Time**: 20 minutes

## Lessons Learned
1. Connection pool configuration should be verified in staging
2. Monitoring alerts caught the issue quickly
3. Automated rollback scripts worked as expected

## Follow-up Actions
- [ ] Update staging environment configuration
- [ ] Add connection pool monitoring alerts
- [ ] Document new authentication flow
- [ ] Update API documentation
```

### Architecture Diagram Standards

#### C4 Model Documentation
```markdown
# System Architecture - C4 Model

## Level 1: System Context
```
┌─────────────────────────────────────────────────────────────┐
│                                                             │
│                    External Systems                         │
│                                                             │
└─────────────────────────────────────────────────────────────┘

[Users] ---> [Honua Platform] ---> [AWS Services]
                    |
                    +---> [Third-party APIs]
                    |
                    +---> [External Databases]

### System Context Diagram
Shows how the system fits in the overall IT landscape.

**Actors:**
- End Users: Web and mobile application users
- Administrators: System administrators and operators
- External Systems: Third-party integrations

**External Dependencies:**
- AWS S3: Object storage
- Auth0: Authentication service
- Stripe: Payment processing
- SendGrid: Email delivery
```

## Level 2: Container Diagram
```
┌─────────────────────────────────────────────────────────────┐
│                    Honua Platform                           │
│                                                             │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐   │
│  │   Web    │  │   API    │  │  Worker  │  │   Admin  │   │
│  │   App    │  │  Server  │  │  Service │  │  Portal  │   │
│  └──────────┘  └──────────┘  └──────────┘  └──────────┘   │
│                                                             │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐                 │
│  │PostgreSQL│  │  Redis   │  │   S3     │                 │
│  │   DB     │  │  Cache   │  │  Storage │                 │
│  └──────────┘  └──────────┘  └──────────┘                 │
└─────────────────────────────────────────────────────────────┘

### Container Details

**Web Application**
- Technology: React, TypeScript
- Hosting: CloudFront + S3
- Purpose: User interface

**API Server**
- Technology: .NET 8, C#
- Hosting: ECS Fargate
- Purpose: Business logic and data access

**Worker Service**
- Technology: .NET 8, C#
- Hosting: ECS Fargate
- Purpose: Background job processing

**PostgreSQL Database**
- Technology: PostgreSQL 15
- Hosting: RDS
- Purpose: Primary data store

**Redis Cache**
- Technology: Redis 7
- Hosting: ElastiCache
- Purpose: Session storage and caching
```

## Level 3: Component Diagram
```
┌─────────────────────────────────────────────────────────────┐
│                    API Server                               │
│                                                             │
│  ┌──────────────────┐  ┌──────────────────┐               │
│  │  Authentication  │  │  Authorization   │               │
│  │    Component     │  │    Component     │               │
│  └──────────────────┘  └──────────────────┘               │
│                                                             │
│  ┌──────────────────┐  ┌──────────────────┐               │
│  │   OGC Services   │  │   Geoservices    │               │
│  │    Component     │  │    Component     │               │
│  └──────────────────┘  └──────────────────┘               │
│                                                             │
│  ┌──────────────────┐  ┌──────────────────┐               │
│  │  Data Access     │  │   Caching        │               │
│  │    Component     │  │   Component      │               │
│  └──────────────────┘  └──────────────────┘               │
└─────────────────────────────────────────────────────────────┘
```

#### Network Topology Documentation
```markdown
# Network Architecture

## VPC Configuration
```
VPC: honua-production-vpc (10.0.0.0/16)

├── Public Subnets (DMZ)
│   ├── 10.0.1.0/24 (us-east-1a) - Load Balancers
│   ├── 10.0.2.0/24 (us-east-1b) - Load Balancers
│   └── 10.0.3.0/24 (us-east-1c) - NAT Gateways
│
├── Private Subnets (Application Tier)
│   ├── 10.0.11.0/24 (us-east-1a) - ECS Tasks
│   ├── 10.0.12.0/24 (us-east-1b) - ECS Tasks
│   └── 10.0.13.0/24 (us-east-1c) - ECS Tasks
│
└── Private Subnets (Data Tier)
    ├── 10.0.21.0/24 (us-east-1a) - RDS, ElastiCache
    ├── 10.0.22.0/24 (us-east-1b) - RDS, ElastiCache
    └── 10.0.23.0/24 (us-east-1c) - RDS, ElastiCache
```

## Security Groups

### ALB-SG (Application Load Balancer)
```hcl
# Inbound Rules
443/tcp from 0.0.0.0/0         # HTTPS from internet
80/tcp from 0.0.0.0/0          # HTTP from internet (redirect)

# Outbound Rules
ALL to ECS-SG                  # Forward to ECS tasks
```

### ECS-SG (ECS Tasks)
```hcl
# Inbound Rules
8080/tcp from ALB-SG           # HTTP from load balancer
8080/tcp from VPN-SG           # HTTP from VPN (debugging)

# Outbound Rules
5432/tcp to RDS-SG             # PostgreSQL
6379/tcp to REDIS-SG           # Redis
443/tcp to 0.0.0.0/0           # HTTPS to internet
```

### RDS-SG (Database)
```hcl
# Inbound Rules
5432/tcp from ECS-SG           # PostgreSQL from application
5432/tcp from BASTION-SG       # PostgreSQL from bastion

# Outbound Rules
NONE                           # No outbound traffic
```

## Network Flow
```
Internet
   |
   v
CloudFront CDN
   |
   +---> S3 (Static Assets)
   |
   v
Route 53 DNS
   |
   v
Application Load Balancer (Public Subnet)
   |
   v
ECS Tasks (Private Subnet)
   |
   +---> RDS PostgreSQL (Private Subnet)
   +---> ElastiCache Redis (Private Subnet)
   +---> S3 (Object Storage)
   +---> NAT Gateway --> Internet (API calls)
```

## DNS Configuration
| Record | Type | Value | Purpose |
|--------|------|-------|---------|
| api.honua.io | A | ALB-123456.us-east-1.elb.amazonaws.com | API endpoint |
| app.honua.io | CNAME | d111111abcdef8.cloudfront.net | Web application |
| admin.honua.io | CNAME | d222222abcdef8.cloudfront.net | Admin portal |

## VPN Configuration
- Type: AWS Client VPN
- CIDR: 172.16.0.0/22
- Authentication: Certificate-based
- Access: Private subnets only
```

### Configuration Management Documentation

#### Infrastructure Configuration
```markdown
# Infrastructure Configuration Baseline

## Environment: Production

### Compute Resources
| Resource | Type | Size | Count | Auto-scaling |
|----------|------|------|-------|--------------|
| API Server | ECS Fargate | 2 vCPU, 4GB RAM | 3-10 | Yes |
| Worker Service | ECS Fargate | 1 vCPU, 2GB RAM | 2-5 | Yes |
| Bastion Host | EC2 t3.small | 2 vCPU, 2GB RAM | 1 | No |

### Database Resources
| Resource | Type | Size | Multi-AZ | Backup Retention |
|----------|------|------|----------|------------------|
| Primary DB | RDS PostgreSQL 15 | db.r6g.xlarge | Yes | 30 days |
| Read Replica | RDS PostgreSQL 15 | db.r6g.large | Yes | N/A |
| Redis Cache | ElastiCache | cache.r6g.large | Yes | No backups |

### Storage Resources
| Resource | Type | Size | Versioning | Lifecycle Policy |
|----------|------|------|------------|------------------|
| Application Assets | S3 Standard | ~500 GB | Enabled | 90 days -> Glacier |
| User Uploads | S3 Intelligent-Tiering | ~2 TB | Enabled | Auto-tier |
| Backups | S3 Glacier | ~5 TB | Disabled | Delete after 1 year |
| Logs | S3 Standard-IA | ~200 GB | Disabled | Delete after 90 days |

## Application Configuration

### Environment Variables
```bash
# Database
DATABASE_HOST=prod-db.us-east-1.rds.amazonaws.com
DATABASE_PORT=5432
DATABASE_NAME=honua_production
DATABASE_SSL_MODE=require
DATABASE_MAX_CONNECTIONS=100
DATABASE_CONNECTION_TIMEOUT=30

# Redis
REDIS_HOST=prod-cache.us-east-1.cache.amazonaws.com
REDIS_PORT=6379
REDIS_SSL_ENABLED=true
REDIS_MAX_CONNECTIONS=50

# Application
ASPNETCORE_ENVIRONMENT=Production
LOG_LEVEL=Information
API_TIMEOUT=60
MAX_REQUEST_SIZE=104857600  # 100MB
RATE_LIMIT_REQUESTS=1000
RATE_LIMIT_WINDOW=60

# External Services
AUTH0_DOMAIN=honua.auth0.com
AUTH0_AUDIENCE=https://api.honua.io
S3_BUCKET=honua-prod-assets
AWS_REGION=us-east-1
```

### Feature Flags
```yaml
features:
  ogc_wfs_3_0:
    enabled: true
    rollout_percentage: 100

  new_authentication:
    enabled: true
    rollout_percentage: 50
    user_segments:
      - beta_testers
      - internal_users

  experimental_caching:
    enabled: false
    rollout_percentage: 0
```

### Security Configuration
```yaml
security:
  tls:
    minimum_version: "1.2"
    cipher_suites:
      - TLS_ECDHE_RSA_WITH_AES_256_GCM_SHA384
      - TLS_ECDHE_RSA_WITH_AES_128_GCM_SHA256

  cors:
    allowed_origins:
      - https://app.honua.io
      - https://admin.honua.io
    allowed_methods:
      - GET
      - POST
      - PUT
      - DELETE
    allowed_headers:
      - Authorization
      - Content-Type
    max_age: 3600

  rate_limiting:
    anonymous:
      requests_per_minute: 60
      burst: 100
    authenticated:
      requests_per_minute: 600
      burst: 1000

  authentication:
    jwt:
      issuer: https://honua.auth0.com/
      audience: https://api.honua.io
      token_expiry: 3600
      refresh_expiry: 604800
```

## Configuration Change Log

### 2025-01-04: Database Connection Pool Update
- **Changed By**: John Doe
- **Reason**: Address connection exhaustion during peak load
- **Changes**:
  - `DATABASE_MAX_CONNECTIONS`: 50 -> 100
  - `DATABASE_CONNECTION_TIMEOUT`: 15 -> 30
- **Rollback Command**:
  ```bash
  aws ssm put-parameter --name /honua/prod/database/max_connections --value "50" --overwrite
  ```

### 2025-01-03: Auto-scaling Policy Update
- **Changed By**: Jane Smith
- **Reason**: Optimize cost during low-traffic hours
- **Changes**:
  - Minimum instances: 5 -> 3
  - Scale-in cooldown: 300s -> 600s
- **Terraform Commit**: abc123def
```

### Change Log and Version Tracking

#### Application Version Log
```markdown
# Version History

## v1.3.0 - 2025-01-04
**Type**: Minor Release
**Deployment Date**: 2025-01-04 14:00 UTC
**Deployed By**: DevOps Team

### New Features
- OGC WFS 3.0 compliance improvements
- New authentication service integration
- User preference management

### Improvements
- API response time reduced by 15%
- Database query optimization
- Improved error handling and logging

### Bug Fixes
- Fixed memory leak in background worker (#234)
- Corrected timezone handling in date fields (#245)
- Fixed pagination bug in search results (#256)

### Security
- Updated dependencies to address CVE-2025-1234
- Implemented rate limiting on authentication endpoints
- Added security headers to all responses

### Database Changes
- Migration: v6-user-preferences.sql
- New tables: user_preferences
- Modified tables: users (added preference_id column)

### Infrastructure Changes
- Updated ECS task definition (2 vCPU -> 2 vCPU, 4GB -> 4GB RAM)
- Modified auto-scaling policy (min: 5 -> 3, max: 10 -> 10)
- Added CloudWatch alarms for new metrics

### Breaking Changes
- None

### Deprecations
- Old authentication endpoint deprecated, will be removed in v2.0.0
- Legacy search API marked for deprecation in v1.4.0

### Dependencies
- .NET 8.0.1 -> 8.0.2
- Npgsql 8.0.0 -> 8.0.1
- StackExchange.Redis 2.7.0 -> 2.7.4

### Rollback Information
- Rollback Runbook: [Link]
- Database Rollback: v6-user-preferences-rollback.sql
- Estimated Rollback Time: 20 minutes

### Related Documents
- Deployment Plan: [Link]
- Post-Deployment Report: [Link]
- Test Results: [Link]

## v1.2.5 - 2024-12-20
**Type**: Patch Release
**Deployment Date**: 2024-12-20 10:00 UTC
**Deployed By**: DevOps Team

### Bug Fixes
- Critical: Fixed authentication bypass vulnerability (#200)
- Fixed incorrect coordinate transformation (#201)
- Corrected memory leak in tile cache (#203)

### Security
- Emergency patch for CVE-2024-9999

### Dependencies
- Auth0 SDK 7.20.0 -> 7.20.1 (security fix)
```

## Deployment Documentation Templates

### Infrastructure as Code Documentation Template

#### Terraform Module Documentation
```markdown
# Terraform Module: ECS Service

## Overview
This module creates an ECS Fargate service with associated load balancer, auto-scaling, and monitoring.

## Architecture
```
[Application Load Balancer]
        |
        v
[Target Group] --> [ECS Service]
                       |
                       +-> [Task Definition]
                       +-> [Auto-scaling Policy]
                       +-> [CloudWatch Alarms]
```

## Usage

```hcl
module "api_service" {
  source = "./modules/ecs-service"

  # Service Configuration
  service_name = "honua-api"
  cluster_id   = aws_ecs_cluster.main.id

  # Task Configuration
  task_cpu    = 2048
  task_memory = 4096
  container_image = "123456789.dkr.ecr.us-east-1.amazonaws.com/honua-api:v1.3.0"
  container_port  = 8080

  # Networking
  vpc_id          = aws_vpc.main.id
  private_subnets = aws_subnet.private[*].id
  security_groups = [aws_security_group.ecs.id]

  # Load Balancing
  alb_listener_arn = aws_lb_listener.https.arn
  health_check_path = "/health"

  # Auto-scaling
  min_capacity = 3
  max_capacity = 10
  cpu_target   = 70
  memory_target = 80

  # Environment Variables
  environment_variables = {
    ASPNETCORE_ENVIRONMENT = "Production"
    LOG_LEVEL = "Information"
  }

  # Secrets
  secrets = {
    DATABASE_PASSWORD = "arn:aws:secretsmanager:us-east-1:123456789:secret:db-password"
    API_KEY = "arn:aws:secretsmanager:us-east-1:123456789:secret:api-key"
  }

  tags = {
    Environment = "production"
    ManagedBy   = "terraform"
  }
}
```

## Inputs

| Name | Description | Type | Default | Required |
|------|-------------|------|---------|----------|
| service_name | Name of the ECS service | string | n/a | yes |
| cluster_id | ECS cluster ID | string | n/a | yes |
| task_cpu | CPU units for task | number | 1024 | no |
| task_memory | Memory (MB) for task | number | 2048 | no |
| container_image | Docker image URL | string | n/a | yes |
| container_port | Container port | number | 8080 | no |
| min_capacity | Minimum number of tasks | number | 2 | no |
| max_capacity | Maximum number of tasks | number | 10 | no |
| environment_variables | Environment variables | map(string) | {} | no |
| secrets | Secrets from Secrets Manager | map(string) | {} | no |

## Outputs

| Name | Description |
|------|-------------|
| service_id | ECS service ID |
| service_name | ECS service name |
| task_definition_arn | Task definition ARN |
| target_group_arn | Target group ARN |
| security_group_id | Security group ID |

## Resources Created

- ECS Service
- ECS Task Definition
- Application Auto-scaling Target
- Auto-scaling Policies (CPU and Memory)
- CloudWatch Log Group
- IAM Role for Task Execution
- IAM Role for Task
- Target Group
- Security Group Rules
- CloudWatch Alarms

## Dependencies

- VPC with private subnets
- ECS Cluster
- Application Load Balancer
- Secrets in AWS Secrets Manager

## Monitoring

### CloudWatch Alarms
- High CPU utilization (>80% for 5 minutes)
- High memory utilization (>80% for 5 minutes)
- Task failures (>2 in 5 minutes)
- Unhealthy targets (>0 for 5 minutes)

### Metrics
- `AWS/ECS/CPUUtilization`
- `AWS/ECS/MemoryUtilization`
- `AWS/ApplicationELB/TargetResponseTime`
- `AWS/ApplicationELB/HTTPCode_Target_5XX_Count`

## Auto-scaling Behavior

### Scale-out Triggers
- CPU utilization > 70% for 2 consecutive periods (60s each)
- Memory utilization > 80% for 2 consecutive periods (60s each)

### Scale-in Triggers
- CPU utilization < 30% for 5 consecutive periods (60s each)
- Memory utilization < 40% for 5 consecutive periods (60s each)

### Scaling Limits
- Minimum: 3 tasks
- Maximum: 10 tasks
- Scale-out cooldown: 60 seconds
- Scale-in cooldown: 300 seconds

## Cost Estimation

Based on us-east-1 pricing (as of 2025-01-01):

```
Minimum Configuration (3 tasks):
- Fargate: 3 tasks × $0.04048/hour = $87.45/month
- ALB: $16.20/month + $0.008/LCU-hour
- Data Transfer: Variable
Estimated minimum: ~$110/month

Maximum Configuration (10 tasks):
- Fargate: 10 tasks × $0.04048/hour = $291.50/month
- ALB: $16.20/month + $0.008/LCU-hour
- Data Transfer: Variable
Estimated maximum: ~$320/month
```

## Disaster Recovery

### Backup
- Container images stored in ECR with lifecycle policies
- Configuration stored in Git
- Secrets stored in AWS Secrets Manager with automatic rotation

### Recovery Time Objective (RTO)
- Service: 5 minutes (automatic ECS task replacement)
- Complete redeployment: 15 minutes

### Recovery Point Objective (RPO)
- Configuration: 0 (Git-versioned)
- Application state: Depends on database backup (15 minutes)

## Maintenance

### Updates
1. Build new container image
2. Push to ECR
3. Update task definition
4. Deploy using blue-green deployment
5. Monitor for issues
6. Complete deployment or rollback

### Rollback Procedure
```bash
# Get previous task definition
aws ecs describe-task-definition --task-definition honua-api --query 'taskDefinition.revision'

# Update service to previous revision
aws ecs update-service \
  --cluster honua-production \
  --service honua-api \
  --task-definition honua-api:[previous-revision]
```

## Security Considerations

### Network Security
- Tasks run in private subnets
- No direct internet access
- Internet access through NAT Gateway
- Security groups restrict inbound traffic to ALB only

### IAM Security
- Task execution role has minimal permissions
- Task role follows principle of least privilege
- Secrets accessed via IAM permissions

### Secrets Management
- No secrets in environment variables
- All secrets stored in AWS Secrets Manager
- Secrets rotated automatically

## Examples

### Production Configuration
```hcl
module "production_api" {
  source = "./modules/ecs-service"

  service_name = "honua-api"
  cluster_id   = aws_ecs_cluster.production.id

  task_cpu    = 2048
  task_memory = 4096
  container_image = "123456789.dkr.ecr.us-east-1.amazonaws.com/honua-api:v1.3.0"

  min_capacity = 5
  max_capacity = 20

  environment_variables = {
    ASPNETCORE_ENVIRONMENT = "Production"
    LOG_LEVEL = "Warning"
  }
}
```

### Staging Configuration
```hcl
module "staging_api" {
  source = "./modules/ecs-service"

  service_name = "honua-api-staging"
  cluster_id   = aws_ecs_cluster.staging.id

  task_cpu    = 1024
  task_memory = 2048
  container_image = "123456789.dkr.ecr.us-east-1.amazonaws.com/honua-api:latest"

  min_capacity = 1
  max_capacity = 3

  environment_variables = {
    ASPNETCORE_ENVIRONMENT = "Staging"
    LOG_LEVEL = "Debug"
  }
}
```
```

#### Kubernetes Resource Documentation Template
```yaml
# Deployment: honua-api

## Overview
Production deployment for Honua API service with horizontal pod autoscaling and rolling updates.

## Resource Specification

apiVersion: apps/v1
kind: Deployment
metadata:
  name: honua-api
  namespace: production
  labels:
    app: honua-api
    version: v1.3.0
    tier: backend
  annotations:
    deployment.kubernetes.io/revision: "12"
    kubernetes.io/change-cause: "Update to v1.3.0 with new authentication"
spec:
  replicas: 3
  strategy:
    type: RollingUpdate
    rollingUpdate:
      maxSurge: 1
      maxUnavailable: 0
  selector:
    matchLabels:
      app: honua-api
  template:
    metadata:
      labels:
        app: honua-api
        version: v1.3.0
      annotations:
        prometheus.io/scrape: "true"
        prometheus.io/port: "8080"
        prometheus.io/path: "/metrics"
    spec:
      serviceAccountName: honua-api-sa

      # Pod Security
      securityContext:
        runAsNonRoot: true
        runAsUser: 1000
        fsGroup: 1000

      # Init Containers
      initContainers:
      - name: migration
        image: 123456789.dkr.ecr.us-east-1.amazonaws.com/honua-migrations:v6
        env:
        - name: DATABASE_HOST
          valueFrom:
            configMapKeyRef:
              name: honua-config
              key: database.host
        - name: DATABASE_PASSWORD
          valueFrom:
            secretKeyRef:
              name: honua-secrets
              key: database.password

      # Application Container
      containers:
      - name: api
        image: 123456789.dkr.ecr.us-east-1.amazonaws.com/honua-api:v1.3.0
        imagePullPolicy: IfNotPresent

        ports:
        - name: http
          containerPort: 8080
          protocol: TCP
        - name: metrics
          containerPort: 9090
          protocol: TCP

        # Resource Limits
        resources:
          requests:
            cpu: 500m
            memory: 512Mi
          limits:
            cpu: 2000m
            memory: 2Gi

        # Health Checks
        livenessProbe:
          httpGet:
            path: /health/live
            port: http
          initialDelaySeconds: 30
          periodSeconds: 10
          timeoutSeconds: 5
          failureThreshold: 3

        readinessProbe:
          httpGet:
            path: /health/ready
            port: http
          initialDelaySeconds: 10
          periodSeconds: 5
          timeoutSeconds: 3
          failureThreshold: 3

        # Startup Probe (for slow-starting applications)
        startupProbe:
          httpGet:
            path: /health/startup
            port: http
          initialDelaySeconds: 0
          periodSeconds: 10
          timeoutSeconds: 3
          failureThreshold: 30

        # Environment Variables
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: LOG_LEVEL
          value: "Information"
        - name: POD_NAME
          valueFrom:
            fieldRef:
              fieldPath: metadata.name
        - name: POD_NAMESPACE
          valueFrom:
            fieldRef:
              fieldPath: metadata.namespace
        - name: POD_IP
          valueFrom:
            fieldRef:
              fieldPath: status.podIP

        # Configuration from ConfigMap
        envFrom:
        - configMapRef:
            name: honua-config

        # Secrets
        - secretRef:
            name: honua-secrets

        # Volume Mounts
        volumeMounts:
        - name: config
          mountPath: /app/config
          readOnly: true
        - name: tmp
          mountPath: /tmp
        - name: cache
          mountPath: /app/cache

      # Volumes
      volumes:
      - name: config
        configMap:
          name: honua-config-files
      - name: tmp
        emptyDir: {}
      - name: cache
        emptyDir:
          sizeLimit: 1Gi

      # Pod Disruption Budget
      # See: honua-api-pdb.yaml

      # Affinity Rules
      affinity:
        podAntiAffinity:
          preferredDuringSchedulingIgnoredDuringExecution:
          - weight: 100
            podAffinityTerm:
              labelSelector:
                matchExpressions:
                - key: app
                  operator: In
                  values:
                  - honua-api
              topologyKey: kubernetes.io/hostname

## Horizontal Pod Autoscaler

apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: honua-api-hpa
  namespace: production
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: honua-api
  minReplicas: 3
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 0
      policies:
      - type: Percent
        value: 100
        periodSeconds: 30
      - type: Pods
        value: 2
        periodSeconds: 30
      selectPolicy: Max

## Service

apiVersion: v1
kind: Service
metadata:
  name: honua-api
  namespace: production
  annotations:
    service.beta.kubernetes.io/aws-load-balancer-type: "nlb"
spec:
  type: LoadBalancer
  selector:
    app: honua-api
  ports:
  - name: http
    port: 80
    targetPort: http
    protocol: TCP

## Documentation

### Deployment Strategy
- **Type**: Rolling Update
- **Max Surge**: 1 additional pod during update
- **Max Unavailable**: 0 pods (ensures zero-downtime)
- **Update Process**: Blue-green deployment with gradual traffic shift

### Resource Requirements
- **Minimum**: 500m CPU, 512Mi memory per pod
- **Maximum**: 2000m CPU, 2Gi memory per pod
- **Recommended**: Start with 3 replicas for production

### Auto-scaling Behavior
- **Scale-out**: When CPU > 70% or Memory > 80%
- **Scale-in**: After 5 minutes below threshold
- **Limits**: Min 3 pods, Max 10 pods

### Health Checks
- **Liveness**: Checks if application is running (restart if failed)
- **Readiness**: Checks if application can handle traffic (remove from service if failed)
- **Startup**: Gives slow-starting apps time to initialize (up to 5 minutes)

### Configuration Management
- **ConfigMap**: `honua-config` for non-sensitive configuration
- **Secret**: `honua-secrets` for sensitive data
- **Files**: Mounted from `honua-config-files` ConfigMap

### Monitoring
- **Metrics**: Prometheus scrapes `/metrics` on port 8080
- **Logs**: Streamed to CloudWatch/ELK
- **Traces**: Sent to distributed tracing system

### Security
- **Non-root**: Runs as user 1000
- **ServiceAccount**: Uses dedicated service account with minimal permissions
- **Network Policies**: Restricts ingress/egress (see network-policy.yaml)

### Deployment Commands

```bash
# Apply deployment
kubectl apply -f deployment.yaml

# Check rollout status
kubectl rollout status deployment/honua-api -n production

# View deployment history
kubectl rollout history deployment/honua-api -n production

# Rollback to previous version
kubectl rollout undo deployment/honua-api -n production

# Rollback to specific revision
kubectl rollout undo deployment/honua-api -n production --to-revision=11

# Scale manually
kubectl scale deployment/honua-api -n production --replicas=5

# Update image
kubectl set image deployment/honua-api api=123456789.dkr.ecr.us-east-1.amazonaws.com/honua-api:v1.3.1 -n production

# View pods
kubectl get pods -n production -l app=honua-api

# View logs
kubectl logs -n production -l app=honua-api --tail=100 -f

# Execute command in pod
kubectl exec -it -n production deployment/honua-api -- /bin/bash
```

### Troubleshooting

#### Pods Not Starting
```bash
# Check pod status
kubectl describe pod <pod-name> -n production

# Check events
kubectl get events -n production --sort-by='.lastTimestamp'

# Check logs
kubectl logs <pod-name> -n production --previous
```

#### High Memory Usage
```bash
# Check resource usage
kubectl top pods -n production -l app=honua-api

# Check HPA status
kubectl get hpa -n production

# Temporarily increase resources
kubectl set resources deployment/honua-api -n production --limits=cpu=3000m,memory=3Gi
```

#### Connection Issues
```bash
# Test service connectivity
kubectl run -it --rm debug --image=nicolaka/netshoot --restart=Never -n production
# Inside the pod:
curl http://honua-api/health

# Check service endpoints
kubectl get endpoints honua-api -n production
```
```

### Service Catalog Template

```markdown
# Service Catalog

## Overview
Complete inventory of all services running in production environment.

## Services

### API Services

#### Honua API
- **Name**: honua-api
- **Version**: v1.3.0
- **Type**: REST API
- **Technology**: .NET 8, C#
- **Hosting**: AWS ECS Fargate
- **Endpoint**: https://api.honua.io
- **Documentation**: https://docs.honua.io/api
- **Team**: Platform Team
- **On-call**: platform-oncall@honua.io
- **Slack Channel**: #platform-api
- **Repository**: https://github.com/honua/api
- **Dependencies**:
  - PostgreSQL Database
  - Redis Cache
  - Auth0 Authentication
  - S3 Object Storage
- **SLA**: 99.9% uptime
- **Health Check**: https://api.honua.io/health
- **Metrics Dashboard**: https://grafana.honua.io/d/api-metrics
- **Logs**: CloudWatch Logs `/aws/ecs/honua-api`

#### OGC Services
- **Name**: ogc-services
- **Version**: v2.1.0
- **Type**: OGC-compliant geospatial services
- **Technology**: .NET 8, C#
- **Hosting**: AWS ECS Fargate
- **Endpoints**:
  - WFS 3.0: https://api.honua.io/ogc/wfs
  - WMS: https://api.honua.io/ogc/wms
  - WMTS: https://api.honua.io/ogc/wmts
- **Standards**: OGC WFS 3.0, WMS 1.3.0, WMTS 1.0.0
- **Documentation**: https://docs.honua.io/ogc
- **Team**: Geospatial Team
- **Dependencies**:
  - PostgreSQL/PostGIS Database
  - Tile Cache (Redis)
  - S3 for raster storage

### Frontend Services

#### Web Application
- **Name**: web-app
- **Version**: v1.5.2
- **Type**: Single Page Application
- **Technology**: React 18, TypeScript
- **Hosting**: CloudFront + S3
- **Endpoint**: https://app.honua.io
- **Repository**: https://github.com/honua/web-app
- **Team**: Frontend Team
- **Dependencies**:
  - Honua API
  - Auth0 Authentication
  - Mapbox GL JS

#### Admin Portal
- **Name**: admin-portal
- **Version**: v1.2.0
- **Type**: Admin Interface
- **Technology**: React 18, TypeScript
- **Hosting**: CloudFront + S3
- **Endpoint**: https://admin.honua.io
- **Access**: Internal only (VPN required)
- **Team**: Platform Team

### Background Services

#### Worker Service
- **Name**: worker-service
- **Version**: v1.3.0
- **Type**: Background job processor
- **Technology**: .NET 8, C#
- **Hosting**: AWS ECS Fargate
- **Job Queue**: Redis
- **Team**: Platform Team
- **Processes**:
  - Data imports
  - Report generation
  - Email sending
  - Cache warming

### Data Services

#### PostgreSQL Database
- **Name**: honua-prod-db
- **Version**: PostgreSQL 15.4
- **Type**: Relational Database
- **Hosting**: AWS RDS
- **Endpoint**: prod-db.cluster-xyz.us-east-1.rds.amazonaws.com
- **Port**: 5432
- **Multi-AZ**: Yes
- **Read Replicas**: 1
- **Backup Retention**: 30 days
- **Monitoring**: Enhanced Monitoring enabled
- **Team**: Platform Team
- **Access**: VPN required

#### Redis Cache
- **Name**: honua-prod-cache
- **Version**: Redis 7.0
- **Type**: In-memory cache
- **Hosting**: AWS ElastiCache
- **Endpoint**: prod-cache.xyz.use1.cache.amazonaws.com
- **Port**: 6379
- **Cluster Mode**: Yes (3 shards)
- **Replication**: Yes
- **Team**: Platform Team

## Service Matrix

| Service | Environment | Status | Health | Last Deploy | Version |
|---------|-------------|--------|--------|-------------|---------|
| honua-api | Production | Active | Healthy | 2025-01-04 | v1.3.0 |
| ogc-services | Production | Active | Healthy | 2024-12-15 | v2.1.0 |
| web-app | Production | Active | Healthy | 2025-01-03 | v1.5.2 |
| admin-portal | Production | Active | Healthy | 2024-12-20 | v1.2.0 |
| worker-service | Production | Active | Healthy | 2025-01-04 | v1.3.0 |

## Service Dependencies

```
Web App --> API --> Database
            |
            +--> Redis
            |
            +--> Auth0
            |
            +--> S3

Admin Portal --> API

Worker Service --> Database
                   |
                   +--> Redis
                   |
                   +--> S3

OGC Services --> Database (PostGIS)
                 |
                 +--> Redis (Tile Cache)
                 |
                 +--> S3 (Rasters)
```

## Service Ownership

| Team | Services | Contact |
|------|----------|---------|
| Platform Team | honua-api, worker-service, admin-portal, databases | platform@honua.io |
| Geospatial Team | ogc-services | geospatial@honua.io |
| Frontend Team | web-app | frontend@honua.io |
| DevOps Team | Infrastructure, monitoring | devops@honua.io |

## SLA Summary

| Service | Availability SLA | Response Time SLA | Support Hours |
|---------|------------------|-------------------|---------------|
| API | 99.9% | p95 < 200ms | 24/7 |
| OGC Services | 99.5% | p95 < 500ms | Business hours |
| Web App | 99.9% | Load < 3s | 24/7 |
| Database | 99.99% | N/A | 24/7 |
```

### API Endpoints Inventory

```markdown
# API Endpoints Inventory

## Base URL
- Production: `https://api.honua.io`
- Staging: `https://api-staging.honua.io`
- Development: `http://localhost:5000`

## Authentication
All endpoints except `/health` require JWT authentication.

```http
Authorization: Bearer <token>
```

## Core API Endpoints

### Health & Status

#### GET /health
Check API health status.

**Response**: 200 OK
```json
{
  "status": "healthy",
  "version": "1.3.0",
  "timestamp": "2025-01-04T10:00:00Z"
}
```

#### GET /health/ready
Check if API is ready to serve traffic.

#### GET /version
Get API version information.

**Response**: 200 OK
```json
{
  "version": "1.3.0",
  "commit": "abc123def",
  "buildDate": "2025-01-04T08:00:00Z"
}
```

### Authentication

#### POST /api/v1/auth/login
User login.

**Request Body**:
```json
{
  "email": "user@example.com",
  "password": "password123"
}
```

**Response**: 200 OK
```json
{
  "token": "eyJ...",
  "refreshToken": "abc...",
  "expiresIn": 3600
}
```

**Rate Limit**: 10 requests/minute
**Errors**: 400, 401, 429

#### POST /api/v1/auth/refresh
Refresh access token.

**Request Body**:
```json
{
  "refreshToken": "abc..."
}
```

#### POST /api/v1/auth/logout
User logout.

### Users

#### GET /api/v1/users/me
Get current user profile.

**Response**: 200 OK
```json
{
  "id": "user-123",
  "email": "user@example.com",
  "name": "John Doe",
  "createdAt": "2024-01-01T00:00:00Z"
}
```

#### PUT /api/v1/users/me
Update current user profile.

#### DELETE /api/v1/users/me
Delete current user account.

### Collections

#### GET /api/v1/collections
List all collections.

**Query Parameters**:
- `page`: Page number (default: 1)
- `limit`: Items per page (default: 20, max: 100)
- `sort`: Sort field (default: createdAt)
- `order`: Sort order (asc/desc, default: desc)
- `search`: Search term

**Response**: 200 OK
```json
{
  "items": [
    {
      "id": "collection-1",
      "name": "My Collection",
      "description": "Description",
      "itemCount": 150,
      "createdAt": "2024-01-01T00:00:00Z"
    }
  ],
  "pagination": {
    "page": 1,
    "limit": 20,
    "total": 45,
    "totalPages": 3
  }
}
```

**Rate Limit**: 100 requests/minute

#### POST /api/v1/collections
Create a new collection.

**Request Body**:
```json
{
  "name": "My Collection",
  "description": "Description"
}
```

**Response**: 201 Created

#### GET /api/v1/collections/{id}
Get collection by ID.

**Path Parameters**:
- `id`: Collection ID

**Response**: 200 OK

#### PUT /api/v1/collections/{id}
Update collection.

#### DELETE /api/v1/collections/{id}
Delete collection.

### Features

#### GET /api/v1/collections/{collectionId}/features
Get features in a collection.

**Query Parameters**:
- `bbox`: Bounding box filter (minX,minY,maxX,maxY)
- `limit`: Max features to return
- `offset`: Offset for pagination
- `properties`: Comma-separated list of properties to include

**Response**: 200 OK (GeoJSON)
```json
{
  "type": "FeatureCollection",
  "features": [
    {
      "type": "Feature",
      "id": "feature-1",
      "geometry": {
        "type": "Point",
        "coordinates": [-122.4194, 37.7749]
      },
      "properties": {
        "name": "San Francisco",
        "population": 874961
      }
    }
  ]
}
```

#### POST /api/v1/collections/{collectionId}/features
Create a new feature.

#### GET /api/v1/collections/{collectionId}/features/{featureId}
Get feature by ID.

#### PUT /api/v1/collections/{collectionId}/features/{featureId}
Update feature.

#### DELETE /api/v1/collections/{collectionId}/features/{featureId}
Delete feature.

## OGC API Endpoints

### OGC API - Features (WFS 3.0)

#### GET /ogc/wfs
Landing page.

#### GET /ogc/wfs/conformance
Conformance classes.

#### GET /ogc/wfs/collections
List feature collections.

#### GET /ogc/wfs/collections/{collectionId}
Collection metadata.

#### GET /ogc/wfs/collections/{collectionId}/items
Get features from collection.

**Query Parameters**:
- `bbox`: Bounding box
- `datetime`: Temporal filter
- `limit`: Max items (default: 10, max: 10000)
- `offset`: Offset
- `properties`: Properties to include
- `crs`: Coordinate reference system

### OGC WMS

#### GET /ogc/wms?SERVICE=WMS&REQUEST=GetCapabilities
Get WMS capabilities.

#### GET /ogc/wms?SERVICE=WMS&REQUEST=GetMap
Get map image.

**Query Parameters**:
- `LAYERS`: Layers to render
- `BBOX`: Bounding box
- `WIDTH`: Image width
- `HEIGHT`: Image height
- `FORMAT`: Output format (image/png, image/jpeg)
- `CRS`: Coordinate reference system
- `STYLES`: Styles to apply

### OGC WMTS

#### GET /ogc/wmts?SERVICE=WMTS&REQUEST=GetCapabilities
Get WMTS capabilities.

#### GET /ogc/wmts?SERVICE=WMTS&REQUEST=GetTile
Get tile.

**Query Parameters**:
- `LAYER`: Layer name
- `TILEMATRIXSET`: Tile matrix set
- `TILEMATRIX`: Tile matrix (zoom level)
- `TILEROW`: Tile row
- `TILECOL`: Tile column
- `FORMAT`: Output format

## Rate Limits

| Endpoint Category | Anonymous | Authenticated | Premium |
|-------------------|-----------|---------------|---------|
| Authentication | 10/min | N/A | N/A |
| Read Operations | 60/min | 600/min | 6000/min |
| Write Operations | 10/min | 100/min | 1000/min |
| Bulk Operations | 5/min | 50/min | 500/min |
| OGC Services | 100/min | 1000/min | Unlimited |

## Error Codes

| Code | Description |
|------|-------------|
| 400 | Bad Request - Invalid input |
| 401 | Unauthorized - Missing or invalid authentication |
| 403 | Forbidden - Insufficient permissions |
| 404 | Not Found - Resource does not exist |
| 409 | Conflict - Resource already exists |
| 422 | Unprocessable Entity - Validation error |
| 429 | Too Many Requests - Rate limit exceeded |
| 500 | Internal Server Error |
| 503 | Service Unavailable |

## Deprecation Policy

### Deprecated Endpoints

| Endpoint | Deprecated Date | Removal Date | Replacement |
|----------|----------------|--------------|-------------|
| POST /api/v1/login | 2025-01-04 | 2025-07-04 | POST /api/v1/auth/login |
| GET /api/v1/search | 2024-12-01 | 2025-06-01 | GET /api/v1/collections?search= |

Deprecated endpoints return a `Deprecation` header:
```http
Deprecation: Sun, 04 Jul 2025 00:00:00 GMT
Link: <https://docs.honua.io/migration/auth>; rel="deprecation"
```
```

## Architecture Documentation

### System Architecture Diagrams

#### PlantUML Example
```plantuml
@startuml Honua System Architecture

!define AWSPuml https://raw.githubusercontent.com/awslabs/aws-icons-for-plantuml/v14.0/dist
!includeurl AWSPuml/AWSCommon.puml
!includeurl AWSPuml/Compute/ECSContainer.puml
!includeurl AWSPuml/Database/RDSPostgreSQLInstance.puml
!includeurl AWSPuml/Database/ElastiCache.puml
!includeurl AWSPuml/Storage/SimpleStorageService.puml
!includeurl AWSPuml/NetworkingContentDelivery/CloudFront.puml
!includeurl AWSPuml/NetworkingContentDelivery/ElasticLoadBalancingApplicationLoadBalancer.puml

' System Context
actor User
actor Admin

' AWS Services
CloudFront(cdn, "CloudFront CDN", "Static Assets")
ElasticLoadBalancingApplicationLoadBalancer(alb, "Application LB", "HTTPS")
ECSContainer(api, "API Service", "ECS Fargate")
ECSContainer(worker, "Worker Service", "ECS Fargate")
RDSPostgreSQLInstance(db, "PostgreSQL", "RDS")
ElastiCache(cache, "Redis Cache", "ElastiCache")
SimpleStorageService(s3, "Object Storage", "S3")

' External Services
cloud "External Services" {
  [Auth0] as auth
  [SendGrid] as email
  [Stripe] as payment
}

' Connections
User --> cdn : Browse
User --> alb : API Requests
Admin --> alb : Admin Requests

cdn --> s3 : Static Files

alb --> api : Forward Requests

api --> db : Read/Write
api --> cache : Cache Operations
api --> s3 : Object Storage
api --> auth : Authenticate
api --> payment : Process Payments

worker --> db : Read/Write
worker --> cache : Queue Operations
worker --> s3 : Object Storage
worker --> email : Send Emails

@enduml
```

#### Mermaid Example
```mermaid
graph TB
    subgraph "Client Layer"
        A[Web Browser]
        B[Mobile App]
    end

    subgraph "CDN Layer"
        C[CloudFront CDN]
    end

    subgraph "Load Balancing"
        D[Application Load Balancer]
    end

    subgraph "Application Layer"
        E[API Server 1]
        F[API Server 2]
        G[API Server 3]
        H[Worker Service]
    end

    subgraph "Caching Layer"
        I[Redis Cluster]
    end

    subgraph "Data Layer"
        J[(PostgreSQL Primary)]
        K[(PostgreSQL Replica)]
    end

    subgraph "Storage Layer"
        L[S3 Bucket]
    end

    subgraph "External Services"
        M[Auth0]
        N[SendGrid]
        O[Stripe]
    end

    A --> C
    B --> C
    C --> D
    D --> E
    D --> F
    D --> G

    E --> I
    F --> I
    G --> I
    H --> I

    E --> J
    F --> J
    G --> J
    H --> J

    J --> K

    E --> L
    F --> L
    G --> L
    H --> L

    E --> M
    F --> M
    G --> M

    H --> N
    H --> O

    style A fill:#f9f,stroke:#333
    style B fill:#f9f,stroke:#333
    style J fill:#bbf,stroke:#333
    style K fill:#bbf,stroke:#333
```

### Component Diagrams

```markdown
# Component Architecture

## API Server Components

```mermaid
graph TB
    subgraph "API Server"
        A[HTTP Server]
        B[Authentication Middleware]
        C[Rate Limiting Middleware]
        D[Logging Middleware]

        E[User Controller]
        F[Collection Controller]
        G[Feature Controller]
        H[OGC Controller]

        I[User Service]
        J[Collection Service]
        K[Feature Service]
        L[OGC Service]

        M[User Repository]
        N[Collection Repository]
        O[Feature Repository]
        P[Spatial Repository]

        Q[Database Context]
        R[Cache Manager]
        S[Storage Manager]
    end

    A --> B
    B --> C
    C --> D
    D --> E
    D --> F
    D --> G
    D --> H

    E --> I
    F --> J
    G --> K
    H --> L

    I --> M
    J --> N
    K --> O
    L --> P

    M --> Q
    N --> Q
    O --> Q
    P --> Q

    I --> R
    J --> R
    K --> R

    K --> S
    L --> S
```

## Component Responsibilities

### Controllers
- **User Controller**: User authentication and profile management
- **Collection Controller**: Collection CRUD operations
- **Feature Controller**: Feature CRUD operations
- **OGC Controller**: OGC-compliant service endpoints

### Services (Business Logic)
- **User Service**: User business logic, validation
- **Collection Service**: Collection management, permissions
- **Feature Service**: Feature operations, spatial queries
- **OGC Service**: OGC protocol implementation

### Repositories (Data Access)
- **User Repository**: User data persistence
- **Collection Repository**: Collection data persistence
- **Feature Repository**: Feature data persistence
- **Spatial Repository**: Spatial queries and operations

### Infrastructure
- **Database Context**: EF Core database context
- **Cache Manager**: Redis cache operations
- **Storage Manager**: S3 file operations
```

### Sequence Diagrams

```mermaid
sequenceDiagram
    participant U as User
    participant A as API Server
    participant Auth as Auth0
    participant DB as PostgreSQL
    participant Cache as Redis

    U->>A: POST /api/v1/auth/login
    A->>Auth: Validate credentials
    Auth-->>A: User info + token
    A->>DB: Get user profile
    DB-->>A: User data
    A->>Cache: Store session
    Cache-->>A: OK
    A-->>U: 200 OK + token

    U->>A: GET /api/v1/collections
    Note over A: Validate JWT token
    A->>Cache: Check cache
    Cache-->>A: Cache miss
    A->>DB: Query collections
    DB-->>A: Collection data
    A->>Cache: Store in cache
    A-->>U: 200 OK + collections

    U->>A: POST /api/v1/collections/{id}/features
    Note over A: Validate permissions
    A->>DB: Insert feature
    DB-->>A: Feature created
    A->>Cache: Invalidate collection cache
    A-->>U: 201 Created
```

### Data Flow Diagrams

```markdown
# Data Flow Diagram - Feature Creation

## Level 0: Context Diagram
```
[User] ---> [Honua Platform] ---> [Database]
```

## Level 1: High-Level Process Flow
```
┌──────────┐         ┌──────────────┐         ┌──────────┐
│          │  HTTP   │              │  SQL    │          │
│  User    ├────────>│  API Server  ├────────>│ Database │
│          │<────────┤              │<────────┤          │
└──────────┘  JSON   └──────┬───────┘  Result └──────────┘
                             │
                             │ Cache
                             v
                      ┌──────────┐
                      │  Redis   │
                      └──────────┘
```

## Level 2: Detailed Process Flow

### Create Feature Flow
```
1. User submits GeoJSON feature
   |
   v
2. API validates request
   ├─> Check authentication (JWT)
   ├─> Check authorization (permissions)
   ├─> Validate GeoJSON schema
   └─> Check collection exists
   |
   v
3. Process geometry
   ├─> Parse GeoJSON
   ├─> Validate geometry
   ├─> Transform coordinates (if needed)
   └─> Calculate bbox
   |
   v
4. Store in database
   ├─> Begin transaction
   ├─> Insert feature
   ├─> Update collection metadata
   └─> Commit transaction
   |
   v
5. Update cache
   ├─> Invalidate collection cache
   └─> Invalidate bbox cache
   |
   v
6. Return response
   └─> 201 Created + feature
```

### Data Transformations

#### Input (GeoJSON)
```json
{
  "type": "Feature",
  "geometry": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749]
  },
  "properties": {
    "name": "San Francisco"
  }
}
```

#### Validation
- Check geometry type is supported
- Validate coordinates are valid
- Check properties match schema
- Verify user has write permission

#### Transformation
- Convert GeoJSON to PostGIS geometry
- Add system fields (id, created_at, updated_at)
- Calculate bounding box
- Index for spatial queries

#### Storage (PostgreSQL)
```sql
INSERT INTO features (
  id,
  collection_id,
  geometry,
  properties,
  created_at,
  updated_at
) VALUES (
  'feature-123',
  'collection-1',
  ST_GeomFromGeoJSON('{"type":"Point","coordinates":[-122.4194,37.7749]}'),
  '{"name":"San Francisco"}',
  NOW(),
  NOW()
)
```

#### Output (Response)
```json
{
  "id": "feature-123",
  "type": "Feature",
  "geometry": {
    "type": "Point",
    "coordinates": [-122.4194, 37.7749]
  },
  "properties": {
    "name": "San Francisco"
  },
  "created_at": "2025-01-04T10:00:00Z",
  "updated_at": "2025-01-04T10:00:00Z"
}
```
```

## Work Tracking and Runbooks

### Deployment Runbooks

#### Standard Deployment Runbook
```markdown
# Standard Deployment Runbook

## Pre-Deployment (T-24 hours)

### Checklist
- [ ] Code freeze in effect
- [ ] All tests passing (unit, integration, e2e)
- [ ] Security scan completed, no critical issues
- [ ] Performance testing completed
- [ ] Database migration tested in staging
- [ ] Rollback plan documented
- [ ] Stakeholders notified
- [ ] Change approval obtained (ticket #)
- [ ] Maintenance window scheduled
- [ ] On-call engineer identified

### Verification
```bash
# Run full test suite
dotnet test --configuration Release

# Run security scan
dotnet tool run security-scan

# Verify Docker image
docker pull 123456789.dkr.ecr.us-east-1.amazonaws.com/honua-api:v1.3.0
docker run --rm honua-api:v1.3.0 --version
```

## Pre-Deployment (T-1 hour)

### Backups
```bash
# Backup database
aws rds create-db-snapshot \
  --db-instance-identifier honua-prod-db \
  --db-snapshot-identifier honua-prod-db-backup-20250104

# Verify backup
aws rds describe-db-snapshots \
  --db-snapshot-identifier honua-prod-db-backup-20250104

# Backup current ECS task definition
aws ecs describe-task-definition \
  --task-definition honua-api \
  --query 'taskDefinition' > task-def-backup.json
```

### System Health Check
```bash
# Check current health
curl https://api.honua.io/health

# Check error rates
aws cloudwatch get-metric-statistics \
  --namespace AWS/ApplicationELB \
  --metric-name HTTPCode_Target_5XX_Count \
  --dimensions Name=LoadBalancer,Value=app/honua-prod \
  --start-time $(date -u -d '1 hour ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
  --period 300 \
  --statistics Sum

# Check current resource usage
aws ecs describe-services \
  --cluster honua-production \
  --services honua-api \
  --query 'services[0].runningCount'
```

## Deployment (T-0)

### Step 1: Enable Maintenance Mode (if needed)
```bash
# Update ALB rule to show maintenance page
aws elbv2 modify-rule \
  --rule-arn arn:aws:elasticloadbalancing:... \
  --conditions Field=path-pattern,Values='*' \
  --actions Type=fixed-response,FixedResponseConfig='{StatusCode=503,ContentType=text/html,MessageBody="Maintenance in progress"}'
```

### Step 2: Database Migration
```bash
# Connect to database via bastion
ssh -i bastion-key.pem ec2-user@bastion.honua.io

# Run migration
psql -h prod-db.cluster-xyz.us-east-1.rds.amazonaws.com \
     -U admin \
     -d honua_production \
     -f migrations/v6-migration.sql

# Verify migration
psql -h prod-db.cluster-xyz.us-east-1.rds.amazonaws.com \
     -U admin \
     -d honua_production \
     -c "SELECT version FROM schema_migrations ORDER BY version DESC LIMIT 1;"
```

**Expected Output**: `6`

### Step 3: Update ECS Service
```bash
# Update task definition
aws ecs register-task-definition \
  --cli-input-json file://task-definition-v1.3.0.json

# Update service
aws ecs update-service \
  --cluster honua-production \
  --service honua-api \
  --task-definition honua-api:123 \  # New revision number
  --force-new-deployment

# Monitor deployment
aws ecs wait services-stable \
  --cluster honua-production \
  --services honua-api
```

### Step 4: Smoke Tests
```bash
# Wait for deployment to stabilize
sleep 60

# Health check
curl https://api.honua.io/health
# Expected: {"status":"healthy","version":"1.3.0"}

# Version check
curl https://api.honua.io/version
# Expected: {"version":"1.3.0"}

# Functional test - Login
curl -X POST https://api.honua.io/api/v1/auth/login \
  -H "Content-Type: application/json" \
  -d '{"email":"test@honua.io","password":"test123"}'
# Expected: 200 OK with token

# Functional test - Get collections
curl https://api.honua.io/api/v1/collections \
  -H "Authorization: Bearer $TOKEN"
# Expected: 200 OK with collections

# Functional test - Create feature
curl -X POST https://api.honua.io/api/v1/collections/test/features \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{"type":"Feature","geometry":{"type":"Point","coordinates":[-122,37]},"properties":{"name":"Test"}}'
# Expected: 201 Created
```

### Step 5: Disable Maintenance Mode
```bash
# Restore normal ALB rule
aws elbv2 modify-rule \
  --rule-arn arn:aws:elasticloadbalancing:... \
  --conditions Field=path-pattern,Values='*' \
  --actions Type=forward,TargetGroupArn=arn:aws:elasticloadbalancing:...
```

## Post-Deployment (T+30 minutes)

### Monitoring
```bash
# Check error rates
aws cloudwatch get-metric-statistics \
  --namespace AWS/ApplicationELB \
  --metric-name HTTPCode_Target_5XX_Count \
  --dimensions Name=LoadBalancer,Value=app/honua-prod \
  --start-time $(date -u -d '30 minutes ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
  --period 300 \
  --statistics Sum

# Check response times
aws cloudwatch get-metric-statistics \
  --namespace AWS/ApplicationELB \
  --metric-name TargetResponseTime \
  --dimensions Name=LoadBalancer,Value=app/honua-prod \
  --start-time $(date -u -d '30 minutes ago' +%Y-%m-%dT%H:%M:%S) \
  --end-time $(date -u +%Y-%m-%dT%H:%M:%S) \
  --period 300 \
  --statistics Average

# Check CPU/Memory
aws ecs describe-services \
  --cluster honua-production \
  --services honua-api
```

### Verification Checklist
- [ ] No error spikes in CloudWatch
- [ ] Response times within acceptable range (p95 < 200ms)
- [ ] CPU usage within limits (< 80%)
- [ ] Memory usage within limits (< 80%)
- [ ] All instances healthy
- [ ] No critical logs/alerts
- [ ] User-facing smoke tests completed
- [ ] Stakeholders notified of completion

## Rollback Procedure

### When to Rollback
- Critical bugs discovered
- Error rate > 1%
- Response time degradation > 50%
- Data corruption detected
- Security vulnerability introduced

### Rollback Steps
```bash
# 1. Revert ECS service to previous task definition
aws ecs update-service \
  --cluster honua-production \
  --service honua-api \
  --task-definition honua-api:122  # Previous revision

# 2. Wait for rollback to complete
aws ecs wait services-stable \
  --cluster honua-production \
  --services honua-api

# 3. Rollback database migration (if needed)
psql -h prod-db.cluster-xyz.us-east-1.rds.amazonaws.com \
     -U admin \
     -d honua_production \
     -f migrations/v6-migration-rollback.sql

# 4. Clear cache
redis-cli -h prod-cache.xyz.use1.cache.amazonaws.com FLUSHALL

# 5. Verify rollback
curl https://api.honua.io/version
# Expected: {"version":"1.2.5"}
```

**Estimated Rollback Time**: 10-15 minutes

## Post-Mortem (If Issues Occurred)

### Incident Report Template
```markdown
# Incident Report: [Date] Deployment

## Summary
Brief description of what went wrong.

## Timeline
- T-0: Deployment started
- T+5: Issue discovered
- T+10: Rollback initiated
- T+15: Service restored

## Root Cause
Detailed explanation of what caused the issue.

## Impact
- Duration: X minutes
- Affected users: Y
- Error rate: Z%

## Resolution
How the issue was resolved.

## Action Items
- [ ] Fix identified bug
- [ ] Update deployment process
- [ ] Add additional monitoring
```

## Contact Information

| Role | Name | Phone | Slack |
|------|------|-------|-------|
| On-call Engineer | Rotating | +1-555-0100 | #oncall |
| DevOps Lead | Jane Smith | +1-555-0101 | @jane |
| Platform Lead | John Doe | +1-555-0102 | @john |
| Engineering Manager | Bob Johnson | +1-555-0103 | @bob |

## Resources

- Monitoring Dashboard: https://grafana.honua.io/d/deployments
- Logs: https://console.aws.amazon.com/cloudwatch/
- Runbook Repository: https://github.com/honua/runbooks
- Incident Slack Channel: #incidents
```

#### Emergency Rollback Runbook
```markdown
# Emergency Rollback Runbook

## When to Use This Runbook
Use this runbook for immediate rollback when:
- Production is down or severely degraded
- Data corruption is occurring
- Security incident is active
- Error rate exceeds 5%

## Prerequisites
- Access to AWS console or AWS CLI
- Database credentials (stored in 1Password)
- VPN connection active

## Quick Rollback (5 minutes)

### Step 1: Identify Previous Version (30 seconds)
```bash
# Get current task definition revision
aws ecs describe-services \
  --cluster honua-production \
  --services honua-api \
  --query 'services[0].taskDefinition'

# Output: honua-api:123
# Previous version: honua-api:122
```

### Step 2: Rollback ECS Service (2 minutes)
```bash
# Rollback to previous revision
aws ecs update-service \
  --cluster honua-production \
  --service honua-api \
  --task-definition honua-api:122 \
  --force-new-deployment

# Monitor rollback
watch -n 5 'aws ecs describe-services \
  --cluster honua-production \
  --services honua-api \
  --query "services[0].deployments"'
```

**Wait for**: `runningCount` matches `desiredCount` for old revision

### Step 3: Verify Service (1 minute)
```bash
# Health check
curl https://api.honua.io/health

# Version check
curl https://api.honua.io/version

# Quick functional test
curl https://api.honua.io/api/v1/collections \
  -H "Authorization: Bearer $TEST_TOKEN"
```

### Step 4: Database Rollback (if needed) (2 minutes)
```bash
# Only if database migration was part of deployment
ssh bastion.honua.io

psql -h prod-db.cluster-xyz.us-east-1.rds.amazonaws.com \
     -U admin \
     -d honua_production \
     -f /home/ec2-user/migrations/rollback-v6.sql
```

### Step 5: Clear Cache (30 seconds)
```bash
# Clear Redis cache
redis-cli -h prod-cache.xyz.use1.cache.amazonaws.com FLUSHALL
```

## Verification Checklist
- [ ] Health endpoint returns 200
- [ ] Version endpoint shows previous version
- [ ] No 5XX errors in CloudWatch
- [ ] Response times back to normal
- [ ] User-facing functionality works

## Post-Rollback Actions

### Immediate (Next 15 minutes)
1. Notify stakeholders
   ```
   Slack: #incidents
   Subject: Production rollback completed
   Message: Deployment v1.3.0 rolled back to v1.2.5 due to [reason].
            Service is stable. Post-mortem to follow.
   ```

2. Create incident ticket
   - Use template: https://github.com/honua/runbooks/templates/incident.md
   - Assign to engineering lead

3. Monitor for 15 minutes
   - Watch CloudWatch dashboards
   - Check error logs
   - Monitor user reports

### Short-term (Next 2 hours)
1. Incident review meeting
   - What happened?
   - Why did it happen?
   - Why didn't we catch it?

2. Document timeline
   - All actions taken
   - All commands executed
   - All decisions made

3. Identify root cause

### Long-term (Next 24 hours)
1. Create post-mortem document
2. Identify action items
3. Update deployment process
4. Add additional safeguards

## Alternative Rollback Methods

### Method 2: Using AWS Console (No CLI Access)

1. Navigate to ECS console
2. Select cluster: honua-production
3. Select service: honua-api
4. Click "Update"
5. Select previous task definition revision
6. Click "Update service"
7. Wait for deployment to complete

### Method 3: Using Blue-Green Deployment

If blue-green deployment is configured:
```bash
# Switch traffic back to blue environment
aws elbv2 modify-listener \
  --listener-arn $LISTENER_ARN \
  --default-actions Type=forward,TargetGroupArn=$BLUE_TARGET_GROUP_ARN
```

## Rollback Decision Matrix

| Issue | Rollback? | Method |
|-------|-----------|--------|
| Error rate > 5% | Yes | Immediate |
| Error rate 1-5% | Evaluate for 5 min | If not improving, rollback |
| Response time +50% | Evaluate for 5 min | If not improving, rollback |
| Data corruption | Yes | Immediate + DB rollback |
| Security issue | Yes | Immediate |
| Feature bug (non-critical) | No | Fix forward |
| UI issue (non-blocking) | No | Fix forward |

## Troubleshooting

### Rollback Not Completing
```bash
# Check service events
aws ecs describe-services \
  --cluster honua-production \
  --services honua-api \
  --query 'services[0].events[0:10]'

# Force stop current tasks
aws ecs list-tasks \
  --cluster honua-production \
  --service honua-api \
  --query 'taskArns' \
  | jq -r '.[]' \
  | xargs -I {} aws ecs stop-task --cluster honua-production --task {}
```

### Database Rollback Failed
```bash
# Check migration status
psql -h prod-db... -U admin -d honua_production \
  -c "SELECT * FROM schema_migrations ORDER BY version DESC LIMIT 5;"

# Manual fix might be needed - escalate to DBA
```

## Emergency Contacts

| Priority | Contact | Method |
|----------|---------|--------|
| 1 | On-call Engineer | PagerDuty |
| 2 | DevOps Lead | +1-555-0101 |
| 3 | Engineering Manager | +1-555-0103 |
| 4 | CTO | +1-555-0199 |

## Related Documentation
- Standard Deployment Runbook
- Database Migration Runbook
- Disaster Recovery Plan
- Post-Mortem Template
```

### Work-in-Progress Tracking

```markdown
# Work-in-Progress Tracking

## Active Deployments

### v1.3.0 Deployment
**Status**: In Progress
**Started**: 2025-01-04 14:00 UTC
**Owner**: DevOps Team
**Risk**: Medium

#### Tasks
- [x] Pre-deployment verification
- [x] Database migration
- [x] ECS service update
- [ ] Smoke tests
- [ ] Post-deployment monitoring
- [ ] Stakeholder notification

#### Issues
- None

---

### Database Schema Migration v6
**Status**: Blocked
**Started**: 2025-01-04 13:00 UTC
**Owner**: Database Team
**Risk**: Low

#### Tasks
- [x] Write migration script
- [x] Test in development
- [x] Test in staging
- [ ] Peer review (waiting for Jane)
- [ ] Execute in production

#### Blockers
- Waiting for Jane Smith's review (requested 2025-01-04 13:30)

---

## Deployment Pipeline Status

| Environment | Version | Status | Last Deployed | Next Deployment |
|-------------|---------|--------|---------------|-----------------|
| Development | v1.4.0-dev | Healthy | 2025-01-04 10:00 | Continuous |
| Staging | v1.3.0 | Healthy | 2025-01-03 15:00 | v1.3.1 (Jan 8) |
| Production | v1.2.5 | Healthy | 2024-12-20 10:00 | v1.3.0 (Jan 4) |

## Infrastructure Changes

### AWS Auto-scaling Policy Update
**Status**: Pending Approval
**Created**: 2025-01-03
**Owner**: John Doe
**Change Ticket**: CHG-12345

#### Description
Update auto-scaling policy to reduce minimum instances during off-peak hours.

#### Changes
- Minimum instances: 5 → 3
- Schedule: 02:00-08:00 UTC (off-peak)
- Estimated savings: $200/month

#### Approval Status
- [x] Technical review (Jane Smith)
- [ ] Security review (Pending)
- [ ] Management approval (Pending)

---

### Redis Cluster Upgrade
**Status**: Scheduled
**Scheduled**: 2025-01-10 03:00 UTC
**Owner**: DevOps Team
**Maintenance Window**: 4 hours

#### Description
Upgrade Redis from 6.2 to 7.0 for performance improvements.

#### Tasks
- [x] Review upgrade documentation
- [x] Test upgrade in staging
- [x] Create rollback plan
- [ ] Schedule maintenance window
- [ ] Notify stakeholders
- [ ] Execute upgrade
- [ ] Verify functionality
- [ ] Update documentation

---

## TODO Items

### High Priority
- [ ] Implement database connection pooling optimization (John - Due: Jan 5)
- [ ] Add monitoring for new authentication service (Jane - Due: Jan 6)
- [ ] Update API documentation for v1.3.0 (Mike - Due: Jan 7)

### Medium Priority
- [ ] Optimize Docker image size (Sarah - Due: Jan 10)
- [ ] Implement automated security scanning in CI/CD (Tom - Due: Jan 15)
- [ ] Update disaster recovery runbook (Jane - Due: Jan 20)

### Low Priority
- [ ] Research serverless options for worker service (John - Due: Jan 31)
- [ ] Evaluate alternative caching strategies (Team - Due: Feb 15)

## Issue Tracking Integration

### Open Issues Related to Deployment

#### PROD-234: Memory leak in worker service
- **Priority**: High
- **Status**: In Progress
- **Assigned**: John Doe
- **Created**: 2024-12-15
- **Target**: v1.3.1
- **Impact**: Worker service requires restart every 24 hours
- **Work**: Root cause identified, fix in testing

#### PROD-256: Intermittent 504 errors on /api/v1/search
- **Priority**: Medium
- **Status**: Open
- **Assigned**: Jane Smith
- **Created**: 2025-01-02
- **Target**: v1.3.1
- **Impact**: 0.1% of search requests timeout
- **Work**: Investigating database query performance

## Deployment Checklist Templates

### Pre-Deployment Checklist
```markdown
## Pre-Deployment Checklist: v[VERSION]

### Code Quality
- [ ] All tests passing (unit, integration, e2e)
- [ ] Code review completed
- [ ] No critical SonarQube issues
- [ ] Security scan passed
- [ ] Performance testing completed

### Documentation
- [ ] CHANGELOG updated
- [ ] API documentation updated
- [ ] Migration guide created (if needed)
- [ ] Runbook updated

### Infrastructure
- [ ] Database migration tested
- [ ] Rollback plan documented
- [ ] Backup verified
- [ ] Monitoring alerts configured

### Approvals
- [ ] Technical review approved
- [ ] Security review approved
- [ ] Change management ticket approved
- [ ] Stakeholders notified

### Preparation
- [ ] Maintenance window scheduled
- [ ] On-call engineer identified
- [ ] Communication plan ready
```

### Deployment Checklist
```markdown
## Deployment Checklist: v[VERSION]

### Pre-Deployment
- [ ] Verify all pre-deployment checks passed
- [ ] Create database backup
- [ ] Verify backup integrity
- [ ] Check system health

### Deployment
- [ ] Enable maintenance mode (if needed)
- [ ] Run database migration
- [ ] Verify migration success
- [ ] Deploy application
- [ ] Run smoke tests
- [ ] Disable maintenance mode

### Post-Deployment
- [ ] Monitor error rates (30 min)
- [ ] Monitor response times (30 min)
- [ ] Monitor resource usage (30 min)
- [ ] Run full integration tests
- [ ] Notify stakeholders
- [ ] Update deployment log

### Rollback (If Needed)
- [ ] Initiate rollback procedure
- [ ] Rollback database migration
- [ ] Verify rollback success
- [ ] Create incident report
```
```

## Auto-Documentation Tools

### Terraform Documentation Generation

#### terraform-docs
```bash
# Install
brew install terraform-docs

# Generate documentation
terraform-docs markdown table ./terraform > README.md

# Generate with custom template
terraform-docs markdown document \
  --config .terraform-docs.yml \
  ./terraform > TERRAFORM.md
```

#### .terraform-docs.yml
```yaml
formatter: "markdown table"
version: ""
header-from: main.tf
footer-from: ""
sections:
  show:
    - header
    - inputs
    - outputs
    - providers
    - requirements
    - resources
    - modules
output:
  file: "README.md"
  mode: inject
  template: |-
    <!-- BEGIN_TF_DOCS -->
    {{ .Content }}
    <!-- END_TF_DOCS -->
sort:
  enabled: true
  by: name
```

#### Example Output
```markdown
## Requirements

| Name | Version |
|------|---------|
| terraform | >= 1.0 |
| aws | ~> 5.0 |

## Providers

| Name | Version |
|------|---------|
| aws | 5.31.0 |

## Modules

| Name | Source | Version |
|------|--------|---------|
| ecs_service | ./modules/ecs-service | n/a |
| vpc | terraform-aws-modules/vpc/aws | 5.1.2 |

## Resources

| Name | Type |
|------|------|
| aws_ecs_cluster.main | resource |
| aws_ecs_service.api | resource |
| aws_ecs_task_definition.api | resource |

## Inputs

| Name | Description | Type | Default | Required |
|------|-------------|------|---------|:--------:|
| cluster_name | Name of ECS cluster | `string` | n/a | yes |
| task_cpu | CPU units for task | `number` | `1024` | no |

## Outputs

| Name | Description |
|------|-------------|
| cluster_id | ECS cluster ID |
| service_name | ECS service name |
```

### Kubernetes Documentation

#### kubectl-doc
```bash
# Install
kubectl krew install doc

# Generate documentation for deployment
kubectl doc deployment honua-api -n production > deployment-docs.md

# Generate full namespace documentation
kubectl doc namespace production > production-namespace.md
```

#### kubewall
```bash
# Install
curl -sL https://install.kubewall.io | sh

# Generate visual documentation
kubewall generate --namespace production --output html > k8s-docs.html
```

#### Example K8s Auto-Doc Script
```bash
#!/bin/bash

# Generate Kubernetes resource documentation

NAMESPACE="production"
OUTPUT_DIR="./docs/kubernetes"

mkdir -p $OUTPUT_DIR

# Get all resource types
RESOURCES=$(kubectl api-resources --verbs=list --namespaced -o name)

for RESOURCE in $RESOURCES; do
    echo "Documenting $RESOURCE..."

    # Get resources
    kubectl get $RESOURCE -n $NAMESPACE -o json > "$OUTPUT_DIR/${RESOURCE}.json"

    # Generate markdown
    cat > "$OUTPUT_DIR/${RESOURCE}.md" << EOF
# $RESOURCE

## Resources

\`\`\`bash
kubectl get $RESOURCE -n $NAMESPACE
\`\`\`

## Detailed Information

\`\`\`yaml
$(kubectl get $RESOURCE -n $NAMESPACE -o yaml)
\`\`\`
EOF
done

echo "Documentation generated in $OUTPUT_DIR"
```

### API Documentation Generation

#### Swagger/OpenAPI
```csharp
// Startup.cs or Program.cs
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Honua API",
        Version = "v1.3.0",
        Description = "Honua Platform API",
        Contact = new OpenApiContact
        {
            Name = "Honua Team",
            Email = "api@honua.io",
            Url = new Uri("https://honua.io"),
        },
        License = new OpenApiLicense
        {
            Name = "MIT License",
            Url = new Uri("https://opensource.org/licenses/MIT"),
        }
    });

    // Include XML comments
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    c.IncludeXmlComments(xmlPath);
});

// Enable Swagger middleware
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "Honua API v1");
    c.RoutePrefix = "api-docs";
});
```

#### Generate Static Documentation
```bash
# Install swagger-cli
npm install -g @apidevtools/swagger-cli

# Validate OpenAPI spec
swagger-cli validate openapi.yaml

# Bundle OpenAPI spec
swagger-cli bundle openapi.yaml -o openapi-bundled.yaml

# Generate HTML documentation
npx redoc-cli bundle openapi.yaml -o api-docs.html

# Generate Markdown documentation
npx widdershins openapi.yaml -o api-docs.md
```

### Database Schema Documentation

#### SchemaSpy
```bash
# Run SchemaSpy
docker run -v "$PWD/output:/output" schemaspy/schemaspy:latest \
  -t pgsql \
  -host prod-db.cluster-xyz.us-east-1.rds.amazonaws.com \
  -port 5432 \
  -db honua_production \
  -u admin \
  -p $DB_PASSWORD \
  -s public

# Open output/index.html for visual schema documentation
```

#### pg_dump with documentation
```bash
# Generate schema documentation
pg_dump -h prod-db... -U admin -d honua_production \
  --schema-only \
  --no-owner \
  --no-privileges \
  > schema-documentation.sql

# Generate ER diagram using ERAlchemy
pip install eralchemy2
eralchemy2 -i postgresql://admin:$DB_PASSWORD@prod-db.../honua_production \
  -o schema-diagram.pdf
```

#### Database Documentation Script
```sql
-- Generate table documentation
SELECT
    t.table_schema,
    t.table_name,
    obj_description((t.table_schema||'.'||t.table_name)::regclass) as table_comment,
    c.column_name,
    c.data_type,
    c.character_maximum_length,
    c.is_nullable,
    col_description((t.table_schema||'.'||t.table_name)::regclass, c.ordinal_position) as column_comment
FROM information_schema.tables t
JOIN information_schema.columns c
    ON t.table_schema = c.table_schema
    AND t.table_name = c.table_name
WHERE t.table_schema = 'public'
    AND t.table_type = 'BASE TABLE'
ORDER BY t.table_name, c.ordinal_position;
```

### Architecture Diagram Tools

#### PlantUML
```bash
# Install
brew install plantuml

# Generate diagram
plantuml architecture.puml

# Generate PNG
plantuml -tpng architecture.puml

# Generate SVG
plantuml -tsvg architecture.puml

# Watch mode (auto-regenerate on change)
plantuml -tpng -o ./output -r ./diagrams
```

#### Mermaid CLI
```bash
# Install
npm install -g @mermaid-js/mermaid-cli

# Generate diagram
mmdc -i diagram.mmd -o diagram.png

# Generate SVG
mmdc -i diagram.mmd -o diagram.svg

# Generate PDF
mmdc -i diagram.mmd -o diagram.pdf

# With custom theme
mmdc -i diagram.mmd -o diagram.png -t forest
```

#### Diagrams as Code (Python)
```python
# diagrams library
from diagrams import Diagram, Cluster
from diagrams.aws.compute import ECS
from diagrams.aws.database import RDS, ElastiCache
from diagrams.aws.network import ELB, CloudFront
from diagrams.aws.storage import S3

with Diagram("Honua Architecture", show=False, direction="TB"):
    cdn = CloudFront("CDN")

    with Cluster("Application"):
        alb = ELB("Load Balancer")
        api = [ECS("API-1"),
               ECS("API-2"),
               ECS("API-3")]

    with Cluster("Data"):
        db = RDS("PostgreSQL")
        cache = ElastiCache("Redis")
        storage = S3("S3")

    cdn >> alb >> api
    api >> db
    api >> cache
    api >> storage
```

#### Structurizr
```java
// Define architecture using code
Workspace workspace = new Workspace("Honua", "Honua Platform Architecture");
Model model = workspace.getModel();

// Define people and systems
Person user = model.addPerson("User", "Platform user");
SoftwareSystem honua = model.addSoftwareSystem("Honua Platform", "Geospatial data platform");
SoftwareSystem auth0 = model.addSoftwareSystem("Auth0", "Authentication service");

user.uses(honua, "Uses");
honua.uses(auth0, "Authenticates users using");

// Define containers
Container webApp = honua.addContainer("Web Application", "React SPA", "React");
Container api = honua.addContainer("API", "REST API", ".NET");
Container database = honua.addContainer("Database", "Primary data store", "PostgreSQL");

user.uses(webApp, "Uses", "HTTPS");
webApp.uses(api, "Makes API calls to", "JSON/HTTPS");
api.uses(database, "Reads from and writes to", "SQL/TCP");
api.uses(auth0, "Authenticates users using", "OAuth2/HTTPS");

// Generate diagrams
ViewSet views = workspace.getViews();
SystemContextView contextView = views.createSystemContextView(honua, "SystemContext", "System Context");
contextView.addAllSoftwareSystems();
contextView.addAllPeople();

ContainerView containerView = views.createContainerView(honua, "Containers", "Container diagram");
containerView.addAllContainers();
containerView.addAllPeople();
containerView.addAllSoftwareSystems();
```

## Documentation as Code

### Markdown-based Documentation

#### Documentation Structure
```
docs/
├── README.md
├── architecture/
│   ├── README.md
│   ├── system-context.md
│   ├── containers.md
│   ├── components.md
│   └── diagrams/
│       ├── system-context.png
│       ├── containers.png
│       └── components.png
├── deployment/
│   ├── README.md
│   ├── aws-infrastructure.md
│   ├── kubernetes.md
│   └── runbooks/
│       ├── standard-deployment.md
│       ├── emergency-rollback.md
│       └── database-migration.md
├── api/
│   ├── README.md
│   ├── authentication.md
│   ├── collections.md
│   ├── features.md
│   └── ogc-services.md
└── operations/
    ├── README.md
    ├── monitoring.md
    ├── alerting.md
    └── troubleshooting.md
```

### Git-versioned Documentation

#### Documentation Workflow
```bash
# Create documentation branch
git checkout -b docs/deployment-update

# Update documentation
vim docs/deployment/aws-infrastructure.md

# Commit changes
git add docs/
git commit -m "Update AWS infrastructure documentation

- Add new ECS auto-scaling configuration
- Document recent security group changes
- Update resource limits"

# Push and create PR
git push origin docs/deployment-update
gh pr create --title "Update deployment documentation" \
  --body "Updates AWS infrastructure documentation with recent changes"
```

#### Documentation Review Process
```yaml
# .github/workflows/docs-review.yml
name: Documentation Review

on:
  pull_request:
    paths:
      - 'docs/**'
      - '**.md'

jobs:
  lint:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Lint Markdown
        uses: DavidAnson/markdownlint-cli2-action@v14
        with:
          globs: '**/*.md'

      - name: Check links
        uses: gaurav-nelson/github-action-markdown-link-check@v1
        with:
          use-quiet-mode: 'yes'
          use-verbose-mode: 'yes'

      - name: Spell check
        uses: streetsidesoftware/cspell-action@v5
        with:
          files: 'docs/**/*.md'
```

### CI/CD for Documentation

#### Documentation Build Pipeline
```yaml
# .github/workflows/docs-deploy.yml
name: Deploy Documentation

on:
  push:
    branches:
      - main
    paths:
      - 'docs/**'
      - 'mkdocs.yml'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup Python
        uses: actions/setup-python@v4
        with:
          python-version: '3.11'

      - name: Install dependencies
        run: |
          pip install mkdocs mkdocs-material
          pip install mkdocs-mermaid2-plugin
          pip install mkdocs-swagger-ui-tag

      - name: Build documentation
        run: mkdocs build

      - name: Deploy to S3
        run: |
          aws s3 sync ./site s3://docs.honua.io/ --delete
          aws cloudfront create-invalidation \
            --distribution-id ${{ secrets.CLOUDFRONT_DIST_ID }} \
            --paths "/*"
        env:
          AWS_ACCESS_KEY_ID: ${{ secrets.AWS_ACCESS_KEY_ID }}
          AWS_SECRET_ACCESS_KEY: ${{ secrets.AWS_SECRET_ACCESS_KEY }}

      - name: Notify team
        uses: slackapi/slack-github-action@v1
        with:
          payload: |
            {
              "text": "Documentation updated: https://docs.honua.io"
            }
        env:
          SLACK_WEBHOOK_URL: ${{ secrets.SLACK_WEBHOOK_URL }}
```

#### MkDocs Configuration
```yaml
# mkdocs.yml
site_name: Honua Documentation
site_url: https://docs.honua.io
repo_url: https://github.com/honua/honua
repo_name: honua/honua

theme:
  name: material
  palette:
    scheme: slate
    primary: indigo
    accent: indigo
  features:
    - navigation.tabs
    - navigation.sections
    - navigation.expand
    - search.suggest
    - search.highlight

plugins:
  - search
  - mermaid2
  - swagger-ui-tag

markdown_extensions:
  - pymdownx.highlight
  - pymdownx.superfences:
      custom_fences:
        - name: mermaid
          class: mermaid
          format: !!python/name:mermaid2.fence_mermaid
  - pymdownx.tabbed
  - pymdownx.details
  - admonition
  - toc:
      permalink: true

nav:
  - Home: index.md
  - Architecture:
    - Overview: architecture/README.md
    - System Context: architecture/system-context.md
    - Containers: architecture/containers.md
    - Components: architecture/components.md
  - Deployment:
    - Overview: deployment/README.md
    - AWS Infrastructure: deployment/aws-infrastructure.md
    - Kubernetes: deployment/kubernetes.md
    - Runbooks:
      - Standard Deployment: deployment/runbooks/standard-deployment.md
      - Emergency Rollback: deployment/runbooks/emergency-rollback.md
  - API:
    - Overview: api/README.md
    - Authentication: api/authentication.md
    - Collections: api/collections.md
    - Features: api/features.md
  - Operations:
    - Monitoring: operations/monitoring.md
    - Alerting: operations/alerting.md
    - Troubleshooting: operations/troubleshooting.md
```

### Documentation Validation

#### Linting Rules
```json
// .markdownlint.json
{
  "default": true,
  "MD001": true,
  "MD003": { "style": "atx" },
  "MD007": { "indent": 2 },
  "MD013": { "line_length": 120 },
  "MD024": { "allow_different_nesting": true },
  "MD025": true,
  "MD033": false,
  "MD034": false
}
```

#### Pre-commit Hook
```bash
#!/bin/bash
# .git/hooks/pre-commit

# Lint markdown files
markdownlint docs/**/*.md

# Check for broken links
markdown-link-check docs/**/*.md

# Spell check
cspell "docs/**/*.md"

# Check for TODO/FIXME in docs
if git diff --cached --name-only | grep -E '\.md$' | xargs grep -E 'TODO|FIXME' ; then
    echo "Found TODO/FIXME in documentation. Please resolve before committing."
    exit 1
fi
```

## Post-Deployment Documentation

### As-Built Documentation Template

```markdown
# As-Built Documentation: Production Environment

## Document Information
- **Document Date**: 2025-01-04
- **Environment**: Production
- **Version**: v1.3.0
- **Documented By**: DevOps Team
- **Review Date**: 2025-04-04 (quarterly review)

## Executive Summary
This document describes the production environment as it is currently configured, including all infrastructure, network topology, security controls, and operational procedures.

## Infrastructure Inventory

### Compute Resources

#### ECS Cluster: honua-production
- **Type**: ECS Fargate
- **Services**: 2 (API, Worker)
- **Total Tasks**: 8-15 (varies with auto-scaling)
- **Region**: us-east-1
- **VPC**: vpc-abc123

#### ECS Service: honua-api
- **Task Definition**: honua-api:123
- **Desired Count**: 3-10 (auto-scaled)
- **Current Count**: 5
- **Task CPU**: 2048 (2 vCPU)
- **Task Memory**: 4096 MB (4 GB)
- **Image**: 123456789.dkr.ecr.us-east-1.amazonaws.com/honua-api:v1.3.0
- **Load Balancer**: app/honua-prod-alb
- **Target Group**: honua-api-tg
- **Health Check**: /health (HTTP 200)
- **Auto-scaling**:
  - CPU Target: 70%
  - Memory Target: 80%
  - Scale-out Cooldown: 60s
  - Scale-in Cooldown: 300s

#### ECS Service: honua-worker
- **Task Definition**: honua-worker:45
- **Desired Count**: 2-5 (auto-scaled)
- **Current Count**: 3
- **Task CPU**: 1024 (1 vCPU)
- **Task Memory**: 2048 MB (2 GB)
- **Image**: 123456789.dkr.ecr.us-east-1.amazonaws.com/honua-worker:v1.3.0

### Database Resources

#### RDS Instance: honua-prod-db
- **Engine**: PostgreSQL 15.4
- **Instance Class**: db.r6g.xlarge
- **vCPU**: 4
- **Memory**: 32 GB
- **Storage**: 500 GB gp3 (16,000 IOPS)
- **Multi-AZ**: Yes
- **Backup Retention**: 30 days
- **Backup Window**: 03:00-04:00 UTC
- **Maintenance Window**: Sunday 04:00-05:00 UTC
- **Encryption**: AES-256 (KMS key: alias/honua-prod-db)
- **Enhanced Monitoring**: Enabled (60s granularity)
- **Performance Insights**: Enabled (7 days retention)
- **Parameter Group**: honua-prod-pg-15
- **Subnet Group**: honua-prod-db-subnet-group
- **Security Group**: sg-db123456

#### RDS Read Replica: honua-prod-db-replica
- **Instance Class**: db.r6g.large
- **vCPU**: 2
- **Memory**: 16 GB
- **Purpose**: Read-only queries, reporting

#### ElastiCache: honua-prod-cache
- **Engine**: Redis 7.0.7
- **Node Type**: cache.r6g.large
- **Nodes**: 3 (1 primary, 2 replicas)
- **Total Memory**: 40.37 GB
- **Cluster Mode**: Enabled
- **Shards**: 3
- **Replicas per Shard**: 1
- **Multi-AZ**: Yes
- **Encryption at Rest**: Enabled
- **Encryption in Transit**: Enabled
- **Subnet Group**: honua-prod-cache-subnet-group
- **Security Group**: sg-cache123456

### Storage Resources

#### S3 Bucket: honua-prod-assets
- **Region**: us-east-1
- **Storage Class**: Standard
- **Versioning**: Enabled
- **Encryption**: AES-256 (SSE-S3)
- **Public Access**: Blocked
- **Current Size**: ~500 GB
- **Object Count**: ~250,000
- **Lifecycle Policy**:
  - Transition to Standard-IA after 90 days
  - Transition to Glacier after 365 days
  - Delete non-current versions after 90 days

#### S3 Bucket: honua-prod-backups
- **Region**: us-east-1
- **Storage Class**: Glacier Instant Retrieval
- **Versioning**: Disabled
- **Encryption**: AES-256 (SSE-KMS)
- **Current Size**: ~5 TB
- **Lifecycle Policy**:
  - Delete after 365 days

### Network Resources

#### VPC: honua-production-vpc
- **CIDR**: 10.0.0.0/16
- **Region**: us-east-1
- **Availability Zones**: us-east-1a, us-east-1b, us-east-1c
- **DNS Hostnames**: Enabled
- **DNS Resolution**: Enabled

#### Subnets
```
Public Subnets:
- honua-prod-public-1a: 10.0.1.0/24 (us-east-1a)
- honua-prod-public-1b: 10.0.2.0/24 (us-east-1b)
- honua-prod-public-1c: 10.0.3.0/24 (us-east-1c)

Private Subnets (Application):
- honua-prod-private-app-1a: 10.0.11.0/24 (us-east-1a)
- honua-prod-private-app-1b: 10.0.12.0/24 (us-east-1b)
- honua-prod-private-app-1c: 10.0.13.0/24 (us-east-1c)

Private Subnets (Data):
- honua-prod-private-data-1a: 10.0.21.0/24 (us-east-1a)
- honua-prod-private-data-1b: 10.0.22.0/24 (us-east-1b)
- honua-prod-private-data-1c: 10.0.23.0/24 (us-east-1c)
```

#### Application Load Balancer: honua-prod-alb
- **Type**: Application Load Balancer
- **Scheme**: Internet-facing
- **IP Address Type**: IPv4
- **Subnets**: Public subnets (all AZs)
- **Security Group**: sg-alb123456
- **Listeners**:
  - HTTP (80): Redirect to HTTPS
  - HTTPS (443): Forward to honua-api-tg
- **SSL Certificate**: *.honua.io (ACM)
- **Deletion Protection**: Enabled
- **Access Logs**: s3://honua-prod-alb-logs/

#### NAT Gateways
- **NAT Gateway 1a**: nat-abc123 (10.0.1.0/24, us-east-1a)
- **NAT Gateway 1b**: nat-def456 (10.0.2.0/24, us-east-1b)
- **NAT Gateway 1c**: nat-ghi789 (10.0.3.0/24, us-east-1c)

#### Route Tables
```
Public Route Table:
- 10.0.0.0/16 -> local
- 0.0.0.0/0 -> Internet Gateway

Private Route Table (us-east-1a):
- 10.0.0.0/16 -> local
- 0.0.0.0/0 -> NAT Gateway 1a

Private Route Table (us-east-1b):
- 10.0.0.0/16 -> local
- 0.0.0.0/0 -> NAT Gateway 1b

Private Route Table (us-east-1c):
- 10.0.0.0/16 -> local
- 0.0.0.0/0 -> NAT Gateway 1c
```

## Configuration Baseline

### Application Configuration
See: [Configuration Management Documentation](#configuration-management-documentation)

### Infrastructure Configuration
All infrastructure is managed via Terraform.

- **Terraform Version**: 1.6.5
- **AWS Provider Version**: 5.31.0
- **State Backend**: S3 (s3://honua-terraform-state/production/terraform.tfstate)
- **State Locking**: DynamoDB (honua-terraform-locks)
- **Repository**: https://github.com/honua/infrastructure
- **Branch**: main
- **Last Applied**: 2025-01-04 12:00 UTC
- **Last Applied By**: John Doe

### Security Configuration

#### IAM Roles
- **ECS Task Execution Role**: honua-prod-ecs-execution-role
- **ECS Task Role**: honua-prod-ecs-task-role
- **Lambda Execution Role**: honua-prod-lambda-execution-role

#### Security Groups
See: [Network Topology Documentation](#network-topology-documentation)

#### Secrets
All secrets stored in AWS Secrets Manager:
- /honua/prod/database/password
- /honua/prod/database/connection-string
- /honua/prod/redis/connection-string
- /honua/prod/auth0/client-secret
- /honua/prod/stripe/api-key
- /honua/prod/sendgrid/api-key

Rotation: Automatic (90 days)

## Performance Baseline

### API Performance (p95)
- **Response Time**: 150ms
- **Throughput**: 500 req/s
- **Error Rate**: 0.02%

### Database Performance
- **Connection Count**: 80-120
- **Query Time (avg)**: 25ms
- **Query Time (p95)**: 80ms
- **CPU Utilization**: 40-50%
- **Memory Utilization**: 60-70%

### Cache Performance
- **Hit Rate**: 85%
- **Latency**: <1ms
- **Memory Usage**: 60%

### Resource Utilization
- **ECS API Tasks**: 45% CPU, 60% Memory
- **ECS Worker Tasks**: 30% CPU, 40% Memory
- **RDS**: 45% CPU, 65% Memory
- **ElastiCache**: 35% CPU, 60% Memory

## Security Audit

### Last Security Audit: 2024-12-15

#### Findings
- **Critical**: 0
- **High**: 0
- **Medium**: 2
- **Low**: 5

#### Medium Severity Issues
1. CloudWatch log retention should be increased from 30 to 90 days
   - Status: Remediated 2024-12-20
2. Enable GuardDuty for threat detection
   - Status: Remediated 2024-12-22

#### Low Severity Issues
1. Add additional CloudWatch alarms for database metrics
2. Implement automated security patching
3. Enable AWS Config for compliance tracking
4. Add budget alerts
5. Implement automated backup testing

### Compliance
- **SOC 2**: In progress (target: Q2 2025)
- **GDPR**: Compliant
- **HIPAA**: Not applicable

## Capacity Planning

### Current Capacity
- **API**: 10 tasks × 2 vCPU = 20 vCPU max
- **Worker**: 5 tasks × 1 vCPU = 5 vCPU max
- **Database**: 500 GB storage, 16,000 IOPS
- **Cache**: 40 GB memory
- **Storage**: 500 GB (assets), 5 TB (backups)

### Growth Projections (Next 12 Months)
- **Users**: 10,000 -> 50,000 (5x)
- **Data**: 500 GB -> 2 TB (4x)
- **API Traffic**: 500 req/s -> 2,000 req/s (4x)

### Capacity Recommendations
1. Increase RDS instance to db.r6g.2xlarge (Q2 2025)
2. Increase storage to 1 TB (Q2 2025)
3. Add additional read replicas (Q3 2025)
4. Consider database sharding strategy (Q4 2025)

## Change Log

### 2025-01-04: v1.3.0 Deployment
- Updated API to v1.3.0
- Updated worker to v1.3.0
- Applied database migration v6
- Updated ECS task definitions

### 2024-12-20: Security Updates
- Enabled GuardDuty
- Increased CloudWatch log retention to 90 days
- Rotated all secrets

### 2024-12-15: Auto-scaling Update
- Updated auto-scaling policies
- Changed minimum instances: 5 -> 3
- Added scheduled scaling for off-peak hours

## Disaster Recovery

### RTO (Recovery Time Objective): 1 hour
### RPO (Recovery Point Objective): 15 minutes

### Backup Schedule
- **Database**: Automated daily backups (03:00 UTC)
- **Database**: Manual snapshot before deployments
- **Application**: Container images versioned in ECR
- **Infrastructure**: Terraform state in S3 (versioned)
- **Configuration**: Git repository (versioned)

### Recovery Procedures
See: [Disaster Recovery Plan](./disaster-recovery.md)

## Contacts

| Role | Name | Email | Phone | Slack |
|------|------|-------|-------|-------|
| On-call Engineer | Rotating | oncall@honua.io | +1-555-0100 | #oncall |
| DevOps Lead | Jane Smith | jane@honua.io | +1-555-0101 | @jane |
| Platform Lead | John Doe | john@honua.io | +1-555-0102 | @john |
| Engineering Manager | Bob Johnson | bob@honua.io | +1-555-0103 | @bob |
| CTO | Alice Williams | alice@honua.io | +1-555-0199 | @alice |

## Related Documentation
- [AWS Infrastructure Documentation](./aws-infrastructure.md)
- [Network Topology Documentation](./network-topology.md)
- [Security Configuration Documentation](./security-configuration.md)
- [Disaster Recovery Plan](./disaster-recovery.md)
- [Incident Response Plan](./incident-response.md)
```

## Best Practices

### Documentation Best Practices

1. **Keep Documentation Close to Code**
   - Store docs in same repository
   - Version docs with code
   - Review docs in code reviews

2. **Automate Documentation Generation**
   - Use tools to generate from code
   - Auto-generate API docs from OpenAPI specs
   - Auto-generate infrastructure docs from Terraform

3. **Make Documentation Searchable**
   - Use clear, descriptive headings
   - Add keywords and tags
   - Implement full-text search

4. **Keep Documentation Up-to-Date**
   - Update docs with every deployment
   - Enforce docs updates in PR process
   - Regular documentation reviews

5. **Use Templates**
   - Standardize documentation format
   - Reduce cognitive load
   - Ensure completeness

6. **Include Runnable Examples**
   - Provide copy-paste commands
   - Include expected outputs
   - Test examples regularly

7. **Document the "Why", Not Just the "What"**
   - Explain decisions and trade-offs
   - Provide context
   - Link to related discussions

8. **Make Documentation Accessible**
   - Clear navigation
   - Mobile-friendly
   - Fast load times

### Deployment Documentation Checklist

- [ ] Pre-deployment checklist completed
- [ ] Infrastructure changes documented
- [ ] Configuration changes documented
- [ ] Database migrations documented
- [ ] Runbook updated
- [ ] Architecture diagrams updated
- [ ] API documentation updated
- [ ] Change log updated
- [ ] Post-deployment report created
- [ ] Lessons learned documented

---

**Document Version**: 1.0
**Last Updated**: 2025-01-04
**Next Review**: 2025-04-04
