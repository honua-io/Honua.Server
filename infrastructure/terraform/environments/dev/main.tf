# Development Environment Configuration

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
    bucket         = "honua-terraform-state-dev"
    key            = "dev/terraform.tfstate"
    region         = "us-east-1"
    encrypt        = true
    dynamodb_table = "honua-terraform-locks"
  }
}

provider "aws" {
  region = var.aws_region

  default_tags {
    tags = {
      Environment = "dev"
      Project     = "Honua"
      ManagedBy   = "Terraform"
      CostCenter  = "Engineering"
      Owner       = "DevOps"
    }
  }
}

# Data sources
data "aws_availability_zones" "available" {
  state = "available"
}

locals {
  environment        = "dev"
  cluster_name       = "honua"
  availability_zones = slice(data.aws_availability_zones.available.names, 0, 2) # Use 2 AZs for dev

  common_tags = {
    Environment = local.environment
    Project     = "Honua"
    ManagedBy   = "Terraform"
    CostCenter  = "Engineering"
  }
}

# ==================== Networking ====================
module "networking" {
  source = "../../modules/networking"

  cloud_provider     = "aws"
  environment        = local.environment
  network_name       = "honua-network"
  cluster_name       = "${local.cluster_name}-${local.environment}"
  vpc_cidr           = "10.0.0.0/16"
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

  # Small instances for dev
  node_group_desired_size = 2
  node_group_min_size     = 2
  node_group_max_size     = 4
  use_spot_instances      = true # Use spot for cost savings in dev

  vpc_id              = module.networking.vpc_id
  subnet_ids          = module.networking.public_subnet_ids
  private_subnet_ids  = module.networking.private_subnet_ids
  allowed_public_cidrs = ["0.0.0.0/0"] # Open in dev
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
  database_name       = "honua_dev"
  master_username     = "honua_admin"
  postgres_version    = "15"

  # Small instance for dev
  db_instance_class       = "db.t4g.medium"
  replica_instance_class  = "db.t4g.medium"
  allocated_storage       = 50
  max_allocated_storage   = 100
  backup_retention_days   = 3  # Shorter retention for dev
  read_replica_count      = 0  # No replicas in dev

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

  # Small instance for dev
  redis_node_type         = "cache.r7g.large"
  enable_cluster_mode     = false  # Disable cluster mode in dev
  num_cache_nodes         = 1      # Single node for dev
  backup_retention_days   = 3

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

  image_retention_count     = 10  # Keep fewer images in dev
  untagged_retention_days   = 3
  enable_replication        = false  # No replication in dev
  enable_customer_registries = false # No customer repos in dev

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
              "ecr:*",
              "secretsmanager:GetSecretValue"
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

  enable_customer_iam = false # Disable in dev

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
  prometheus_retention    = "15d"  # Shorter retention for dev
  prometheus_storage_size = "20Gi"
  storage_class           = "gp3"
  grafana_admin_password  = var.grafana_admin_password

  # Logging
  log_retention_days = 7  # Shorter retention for dev

  # Alerting
  alarm_emails       = var.alarm_emails
  create_sns_topic   = true

  # Budget alerts
  enable_budget_alerts  = true
  monthly_budget_limit  = 500  # Lower budget for dev

  aws_region = var.aws_region

  tags = local.common_tags

  depends_on = [module.kubernetes]
}

# ==================== KMS Key ====================
resource "aws_kms_key" "honua" {
  description             = "KMS key for Honua ${local.environment}"
  deletion_window_in_days = 7  # Shorter window for dev
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
