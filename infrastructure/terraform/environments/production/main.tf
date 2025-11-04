# Production Environment Configuration

terraform {
  required_version = ">= 1.5.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.23"
    }
    helm = {
      source  = "hashicorp/helm"
      version = "~> 2.11"
    }
  }

  backend "s3" {
    bucket         = "honua-terraform-state-prod"
    key            = "prod/terraform.tfstate"
    region         = "us-east-1"
    encrypt        = true
    dynamodb_table = "honua-terraform-locks-prod"
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Environment = "production"
      Project     = "Honua"
      ManagedBy   = "Terraform"
      CostCenter  = "Production"
      Owner       = "Platform"
      Compliance  = "SOC2,ISO27001"
    }
  }
}

# Data sources
data "aws_availability_zones" "available" {
  state = "available"
}

locals {
  environment        = "production"
  cluster_name       = "honua"
  availability_zones = slice(data.aws_availability_zones.available.names, 0, 3) # Use 3 AZs for HA

  common_tags = {
    Environment = local.environment
    Project     = "Honua"
    ManagedBy   = "Terraform"
    CostCenter  = "Production"
    Compliance  = "SOC2,ISO27001"
  }
}

# ==================== Networking ====================
module "networking" {
  source = "../../modules/networking"

  cloud_provider     = "aws"
  environment        = local.environment
  network_name       = "honua-network"
  cluster_name       = "${local.cluster_name}-${local.environment}"
  vpc_cidr           = "10.1.0.0/16"  # Different CIDR for prod
  availability_zones = local.availability_zones

  tags = local.common_tags
}

# ==================== Kubernetes Cluster ====================
module "kubernetes" {
  source = "../../modules/kubernetes-cluster"

  cloud_provider          = "aws"
  environment             = local.environment
  cluster_name            = local.cluster_name
  kubernetes_version      = "1.28"

  # Larger, on-demand instances for production
  node_group_desired_size = 6
  node_group_min_size     = 3
  node_group_max_size     = 20
  use_spot_instances      = false # On-demand for reliability

  vpc_id              = module.networking.vpc_id
  subnet_ids          = module.networking.public_subnet_ids
  private_subnet_ids  = module.networking.private_subnet_ids
  allowed_public_cidrs = var.allowed_public_cidrs # Restricted in prod
  kms_key_arn         = aws_kms_key.honua.arn

  enable_cluster_autoscaler = true
  enable_network_policies   = true

  tags = local.common_tags

  depends_on = [module.networking]
}

# ==================== Database ====================
module "database" {
  source = "../../modules/database"

  cloud_provider      = "aws"
  environment         = local.environment
  db_name             = "honua"
  database_name       = "honua_prod"
  master_username     = "honua_admin"
  postgres_version    = "15"

  # Production-grade instances
  db_instance_class       = "db.r7g.2xlarge"
  replica_instance_class  = "db.r7g.2xlarge"
  allocated_storage       = 500
  max_allocated_storage   = 2000
  backup_retention_days   = 30  # Longer retention for prod
  read_replica_count      = 2   # High availability

  vpc_id     = module.networking.vpc_id
  vpc_cidr   = module.networking.vpc_cidr
  subnet_ids = module.networking.database_subnet_ids
  kms_key_arn = aws_kms_key.honua.arn

  enable_pgbouncer = true

  tags = local.common_tags

  depends_on = [module.networking]
}

# ==================== Redis ====================
module "redis" {
  source = "../../modules/redis"

  cloud_provider      = "aws"
  environment         = local.environment
  redis_name          = "honua-redis"
  redis_version       = "7.0"

  # Production cluster mode
  redis_node_type         = "cache.r7g.2xlarge"
  enable_cluster_mode     = true
  num_node_groups         = 3  # Shards
  replicas_per_node_group = 2  # Replicas per shard
  backup_retention_days   = 7

  vpc_id     = module.networking.vpc_id
  vpc_cidr   = module.networking.vpc_cidr
  subnet_ids = module.networking.private_subnet_ids
  kms_key_arn = aws_kms_key.honua.arn

  tags = local.common_tags

  depends_on = [module.networking]
}

# ==================== Container Registry ====================
module "registry" {
  source = "../../modules/registry"

  cloud_provider = "aws"
  environment    = local.environment
  registry_prefix = "honua-"

  repository_names = [
    "orchestrator",
    "agent",
    "api-server",
    "web-ui",
    "build-worker",
    "monitor"
  ]

  image_retention_count     = 100  # Keep more images in prod
  untagged_retention_days   = 7
  enable_replication        = true
  replication_regions       = ["us-west-2", "eu-west-1"] # Multi-region
  enable_customer_registries = true
  customer_repositories     = var.customer_repositories

  kms_key_arn = aws_kms_key.honua.arn

  tags = local.common_tags
}

# ==================== IAM ====================
module "iam" {
  source = "../../modules/iam"

  cloud_provider = "aws"
  environment    = local.environment
  service_name   = "honua"
  cluster_name   = "${local.cluster_name}-${local.environment}"

  k8s_service_accounts = {
    orchestrator = {
      namespace       = "honua"
      service_account = "orchestrator"
      policy = jsonencode({
        Version = "2012-10-17"
        Statement = [
          {
            Effect = "Allow"
            Action = [
              "ecr:GetAuthorizationToken",
              "ecr:BatchCheckLayerAvailability",
              "ecr:GetDownloadUrlForLayer",
              "ecr:BatchGetImage",
              "ecr:PutImage",
              "ecr:InitiateLayerUpload",
              "ecr:UploadLayerPart",
              "ecr:CompleteLayerUpload"
            ]
            Resource = "*"
          },
          {
            Effect = "Allow"
            Action = [
              "secretsmanager:GetSecretValue"
            ]
            Resource = "arn:aws:secretsmanager:*:*:secret:honua-*"
          }
        ]
      })
    }
    build-worker = {
      namespace       = "honua"
      service_account = "build-worker"
      policy = jsonencode({
        Version = "2012-10-17"
        Statement = [
          {
            Effect = "Allow"
            Action = [
              "ecr:GetAuthorizationToken",
              "ecr:BatchCheckLayerAvailability",
              "ecr:GetDownloadUrlForLayer",
              "ecr:BatchGetImage",
              "ecr:PutImage",
              "ecr:InitiateLayerUpload",
              "ecr:UploadLayerPart",
              "ecr:CompleteLayerUpload"
            ]
            Resource = "*"
          }
        ]
      })
    }
  }

  kms_key_arn        = aws_kms_key.honua.arn
  oidc_provider_arn  = module.kubernetes.eks_cluster_id # Placeholder
  enable_github_oidc = true
  github_org         = var.github_org
  github_repo        = var.github_repo

  enable_customer_iam = true
  customer_users      = var.customer_users

  tags = local.common_tags

  depends_on = [module.kubernetes]
}

# ==================== Monitoring ====================
module "monitoring" {
  source = "../../modules/monitoring"

  cloud_provider = "aws"
  environment    = local.environment
  service_name   = "honua"

  # Prometheus configuration
  prometheus_retention    = "90d"  # Longer retention for prod
  prometheus_storage_size = "200Gi"
  storage_class           = "gp3"
  grafana_admin_password  = var.grafana_admin_password

  # Logging
  log_retention_days = 90  # Compliance requirement

  # Alerting
  alarm_emails       = var.alarm_emails
  create_sns_topic   = true

  # Budget alerts
  enable_budget_alerts  = true
  monthly_budget_limit  = 10000  # Production budget

  aws_region = var.aws_region

  tags = local.common_tags

  depends_on = [module.kubernetes]
}

# ==================== KMS Key ====================
resource "aws_kms_key" "honua" {
  description             = "KMS key for Honua ${local.environment}"
  deletion_window_in_days = 30  # Longer window for prod
  enable_key_rotation     = true

  tags = merge(
    local.common_tags,
    {
      Name = "honua-${local.environment}"
    }
  )
}

resource "aws_kms_alias" "honua" {
  name          = "alias/honua-${local.environment}"
  target_key_id = aws_kms_key.honua.key_id
}

# ==================== Disaster Recovery - Cross-Region Backup ====================
resource "aws_backup_vault" "honua" {
  name        = "honua-backup-vault-${local.environment}"
  kms_key_arn = aws_kms_key.honua.arn

  tags = local.common_tags
}

resource "aws_backup_plan" "honua" {
  name = "honua-backup-plan-${local.environment}"

  rule {
    rule_name         = "daily_backup"
    target_vault_name = aws_backup_vault.honua.name
    schedule          = "cron(0 2 * * ? *)"

    lifecycle {
      delete_after = 30
    }

    copy_action {
      destination_vault_arn = aws_backup_vault.dr.arn

      lifecycle {
        delete_after = 90
      }
    }
  }

  tags = local.common_tags
}

# DR Vault in different region
resource "aws_backup_vault" "dr" {
  provider    = aws.dr_region
  name        = "honua-dr-vault-${local.environment}"
  kms_key_arn = aws_kms_key.dr.arn

  tags = local.common_tags
}

resource "aws_kms_key" "dr" {
  provider                = aws.dr_region
  description             = "DR KMS key for Honua ${local.environment}"
  deletion_window_in_days = 30
  enable_key_rotation     = true

  tags = local.common_tags
}

# Additional provider for DR region
provider "aws" {
  alias  = "dr_region"
  region = var.dr_region
}
