# Two-Tier IAM Architecture for Honua Deployments

## Overview

The Honua AI Consultant generates **TWO separate IAM policy sets** to implement the principle of least privilege at both deployment and runtime:

1. **Deployment IAM** - Permissions for Terraform execution
2. **Runtime IAM** - Permissions for Honua application operation

This separation ensures that:
- The Honua application cannot modify infrastructure
- Terraform deployment accounts have time-limited, controlled access
- Each component has only the minimum required permissions

---

## 1. Deployment IAM (Terraform Execution)

### Purpose
Permissions for a **user or service account** to execute Terraform and provision cloud infrastructure.

### Use Case
- User runs `terraform apply` with credentials from this account
- CI/CD pipeline executes deployments
- Honua AI can be delegated this account for automated deployments

### Permissions Include
| Category | Actions | Rationale |
|----------|---------|-----------|
| **Compute** | Create, Update, Delete ECS/AKS/GKE/Cloud Run resources | Provision container orchestration |
| **Networking** | Create VPCs, subnets, security groups, load balancers | Set up network topology |
| **Database** | Create RDS/Azure PostgreSQL/Cloud SQL instances | Provision PostGIS database |
| **Storage** | Create S3/Blob/GCS buckets | Provision object storage |
| **IAM** | Create roles, policies, service accounts | Create runtime IAM for application |
| **Monitoring** | Create CloudWatch/Log Analytics/Cloud Logging resources | Set up observability |
| **Secrets** | Create secrets for database credentials | Store sensitive configuration |
| **Terraform State** | Read/Write to state backend (S3/Blob/GCS) | Manage Terraform state |

### Restrictions
- Scoped to specific region/resource group/project
- Optional: IP allowlist, MFA requirements
- Optional: Time-based conditions for temporary access

### Generated Files
- **`iam-deployment.tf`** - Terraform configuration to create the deployment user/service principal
- Outputs access keys/credentials (marked sensitive)
- README header explaining purpose

### Example (AWS)
```hcl
# =============================================================================
# DEPLOYMENT IAM: Terraform Execution Account
# This IAM user can execute 'terraform apply' to provision infrastructure
# =============================================================================

resource "aws_iam_user" "terraform_deployer" {
  name = "honua-terraform-deployer-${var.environment}"

  tags = {
    Environment = var.environment
    Purpose     = "TerraformDeployment"
    ManagedBy   = "HonuaAI"
  }
}

resource "aws_iam_access_key" "terraform_deployer" {
  user = aws_iam_user.terraform_deployer.name
}

resource "aws_iam_user_policy" "terraform_deployment" {
  name   = "honua-terraform-deployment"
  user   = aws_iam_user.terraform_deployer.name
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "ec2:*",                  # VPC, subnets, security groups
          "ecs:*",                  # ECS clusters, services, tasks
          "rds:*",                  # RDS instances
          "s3:*",                   # S3 buckets
          "iam:CreateRole",         # Create runtime IAM roles
          "iam:AttachRolePolicy",   # Attach policies to roles
          "logs:CreateLogGroup",    # CloudWatch logs
          "secretsmanager:CreateSecret"  # Secrets for DB credentials
        ]
        Resource = "*"
        Condition = {
          StringEquals = {
            "aws:RequestedRegion" = var.region
          }
        }
      },
      {
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:PutObject"
        ]
        Resource = "arn:aws:s3:::terraform-state-bucket/*"  # Terraform state
      }
    ]
  })
}

output "deployment_access_key_id" {
  value     = aws_iam_access_key.terraform_deployer.id
  sensitive = false
}

output "deployment_secret_access_key" {
  value     = aws_iam_access_key.terraform_deployer.secret
  sensitive = true
}
```

---

## 2. Runtime IAM (Application Permissions)

### Purpose
Permissions for the **Honua application** at runtime, attached to containers/instances.

### Use Case
- ECS task role (AWS)
- Managed identity (Azure)
- Service account (GCP)
- Workload identity (Kubernetes)

### Permissions Include
| Category | Actions | Rationale |
|----------|---------|-----------|
| **Storage** | Read/Write to specific buckets | Access raster data, attachments |
| **Database** | Connect to database | Access PostGIS data (NO admin) |
| **Secrets** | Read database credentials | Get connection strings |
| **Logging** | Write logs | Send application logs |
| **Metrics** | Publish metrics | Send performance data |

### Restrictions
- **NO infrastructure creation/deletion** (no EC2, VPC, RDS admin)
- **NO IAM permissions** (cannot create roles or policies)
- Resource-level permissions with specific ARNs (buckets, databases)
- Read-only secrets access (cannot create/delete secrets)

### Generated Files
- **`iam-runtime.tf`** - Terraform configuration for application role
- Integrated into main deployment Terraform
- Referenced by compute resources (ECS task definition, etc.)

### Example (AWS)
```hcl
# =============================================================================
# RUNTIME IAM: Honua Application Permissions
# This IAM role is attached to ECS tasks running the Honua container
# =============================================================================

resource "aws_iam_role" "honua_app" {
  name = "honua-app-${var.environment}"

  # Trust policy: Allow ECS tasks to assume this role
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Principal = {
        Service = "ecs-tasks.amazonaws.com"
      }
      Action = "sts:AssumeRole"
    }]
  })

  tags = {
    Environment = var.environment
    Purpose     = "HonuaRuntime"
    ManagedBy   = "HonuaAI"
  }
}

resource "aws_iam_role_policy" "honua_runtime" {
  name   = "honua-runtime-permissions"
  role   = aws_iam_role.honua_app.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:PutObject",
          "s3:ListBucket"
        ]
        Resource = [
          aws_s3_bucket.raster_data.arn,
          "${aws_s3_bucket.raster_data.arn}/*",
          aws_s3_bucket.attachments.arn,
          "${aws_s3_bucket.attachments.arn}/*"
        ]
      },
      {
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue"
        ]
        Resource = aws_secretsmanager_secret.db_credentials.arn
      },
      {
        Effect = "Allow"
        Action = [
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = "${aws_cloudwatch_log_group.honua.arn}:*"
      },
      {
        Effect = "Allow"
        Action = [
          "cloudwatch:PutMetricData"
        ]
        Resource = "*"
        Condition = {
          StringEquals = {
            "cloudwatch:namespace" = "Honua/${var.environment}"
          }
        }
      }
    ]
  })
}

output "honua_app_role_arn" {
  value       = aws_iam_role.honua_app.arn
  description = "IAM role ARN for Honua application (attach to ECS task definition)"
}

# Reference in ECS task definition
resource "aws_ecs_task_definition" "honua" {
  family             = "honua-${var.environment}"
  task_role_arn      = aws_iam_role.honua_app.arn  # <-- Runtime IAM role
  execution_role_arn = aws_iam_role.ecs_execution.arn
  # ... rest of task definition
}
```

---

## Comparison Table

| Aspect | Deployment IAM | Runtime IAM |
|--------|---------------|-------------|
| **Principal Type** | IAM User / Service Principal / Service Account | IAM Role / Managed Identity / Workload Identity |
| **Credentials** | Access keys / Client secrets | Assumed by compute resources automatically |
| **Scope** | Infrastructure management | Application operation |
| **Permissions** | Create, Update, Delete resources | Read/Write data only |
| **IAM Powers** | ✅ Can create roles and policies | ❌ Cannot manage IAM |
| **Compute** | ✅ Create ECS/EC2/AKS/GKE | ❌ No compute permissions |
| **Database** | ✅ Create RDS/PostgreSQL | ✅ Connect (NO admin) |
| **Storage** | ✅ Create buckets | ✅ Read/Write to buckets |
| **Secrets** | ✅ Create secrets | ✅ Read secrets (NO write) |
| **Lifetime** | Long-lived (rotate periodically) | Short-lived tokens (hours) |
| **Usage** | Manual or CI/CD deployment | Automatic at runtime |
| **Delegation** | ✅ Can delegate to Honua AI | N/A |

---

## User Workflow

### Step 1: Generate IAM Policies
```bash
honua consultant "deploy to AWS ECS in us-east-1"
```

**Output includes:**
1. Plan table with deployment steps
2. ASCII architecture diagram
3. Architecture documentation
4. **`iam-deployment.tf`** - Terraform deployer account
5. **`iam-runtime.tf`** - Application role

### Step 2: Create Deployment Account
```bash
# Apply ONLY the deployment IAM
terraform apply -target=aws_iam_user.terraform_deployer
```

**Result:**
- IAM user: `honua-terraform-deployer-prod`
- Access keys output (save securely)

### Step 3: Delegate to Honua AI (Optional)
```bash
# Configure Honua AI with deployment credentials
export AWS_ACCESS_KEY_ID=<deployment_access_key>
export AWS_SECRET_ACCESS_KEY=<deployment_secret_key>

# Now Honua AI can execute terraform for you
honua consultant deploy --auto-approve
```

### Step 4: Deploy Infrastructure
```bash
# Use deployment account credentials to provision everything
terraform apply

# This creates:
# - VPC, subnets, security groups
# - ECS cluster, service, task definition
# - RDS PostgreSQL database
# - S3 buckets
# - Runtime IAM role (honua-app-prod)
# - CloudWatch resources
```

### Step 5: Application Runs with Runtime IAM
- ECS task automatically assumes `honua-app-prod` role
- Application has permissions to:
  - Read/Write S3 buckets
  - Connect to RDS
  - Write CloudWatch logs
- Application **cannot** modify infrastructure

---

## Security Best Practices

### Deployment IAM
✅ **DO:**
- Rotate access keys every 90 days
- Use MFA for human access
- Restrict by IP allowlist for CI/CD
- Use temporary credentials (STS AssumeRole) when possible
- Audit Terraform operations with CloudTrail

❌ **DON'T:**
- Share deployment credentials widely
- Embed credentials in code
- Use for runtime operations

### Runtime IAM
✅ **DO:**
- Use resource-level permissions (specific bucket ARNs)
- Scope to single environment (prod role ≠ dev role)
- Monitor with CloudTrail/Azure Monitor/Cloud Audit Logs
- Use conditions (namespace restrictions for metrics)

❌ **DON'T:**
- Grant `*` permissions
- Allow infrastructure modifications
- Allow IAM management

---

## Cloud-Specific Implementations

### AWS
- **Deployment:** IAM User with access keys
- **Runtime:** IAM Role with ECS task role assumption

### Azure
- **Deployment:** Service Principal with client secret
- **Runtime:** Managed Identity assigned to Container Apps

### GCP
- **Deployment:** Service Account with key file
- **Runtime:** Workload Identity bound to GKE/Cloud Run

### Kubernetes
- **Deployment:** ServiceAccount with cloud credentials secret
- **Runtime:** Workload Identity or IRSA (IAM Roles for Service Accounts)

---

## Integration with Honua Consultant

The `CloudPermissionGeneratorAgent` automatically:
1. Analyzes deployment topology
2. Determines required cloud services
3. Generates **TWO** separate IAM policy sets
4. Creates **TWO** Terraform files:
   - `iam-deployment.tf` (create this first, manually)
   - `iam-runtime.tf` (included in main deployment)
5. Documents both in architecture documentation

Users receive clear guidance:
- **Deployment IAM:** For Terraform execution (can delegate to Honua AI)
- **Runtime IAM:** For application operation (automatic)

---

## FAQ

**Q: Can I use the deployment account for runtime?**
A: No. This violates least privilege. Deployment accounts have excessive permissions for runtime needs.

**Q: Can the runtime role modify infrastructure?**
A: No. Runtime IAM explicitly excludes infrastructure creation/deletion permissions.

**Q: How do I delegate deployments to Honua AI?**
A: Configure Honua AI with the **deployment account credentials** (from `iam-deployment.tf`). Keep runtime IAM separate.

**Q: Do I need both IAM sets for local development?**
A: For local dev with Docker, you may only need runtime-equivalent permissions via environment variables or local credentials. Deployment IAM is for provisioning cloud infrastructure.

**Q: Can runtime IAM access Terraform state?**
A: No. Only deployment IAM can access Terraform state buckets.

---

## Summary

The **Two-Tier IAM Architecture** ensures:
✅ Deployment accounts can provision infrastructure (time-limited, audited)
✅ Application accounts can only access runtime resources (scoped, monitored)
✅ Clear separation of concerns
✅ Least privilege at every layer
✅ Safe delegation to Honua AI for automated deployments
