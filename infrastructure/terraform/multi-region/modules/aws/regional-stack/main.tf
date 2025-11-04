# ============================================================================
# AWS Regional Stack Module
# ============================================================================
# This module deploys a complete HonuaIO stack in a single AWS region.
# Can be deployed as primary (read-write) or DR (read replica).
# ============================================================================

terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

# ============================================================================
# Variables
# ============================================================================

variable "region" {
  description = "AWS region"
  type        = string
}

variable "environment" {
  description = "Environment name"
  type        = string
}

variable "project_name" {
  description = "Project name"
  type        = string
}

variable "unique_suffix" {
  description = "Unique suffix for resource naming"
  type        = string
}

variable "is_primary" {
  description = "Whether this is the primary region"
  type        = bool
}

# Compute variables
variable "instance_count" {
  description = "Number of ECS tasks"
  type        = number
}

variable "instance_type" {
  description = "Fargate instance type (CPU/Memory)"
  type        = string
}

variable "container_image" {
  description = "Container image"
  type        = string
}

variable "container_port" {
  description = "Container port"
  type        = number
}

variable "cpu_limit" {
  description = "CPU limit (vCPUs)"
  type        = string
}

variable "memory_limit" {
  description = "Memory limit (GB)"
  type        = string
}

# Database variables
variable "db_instance_class" {
  description = "RDS instance class"
  type        = string
}

variable "db_engine_version" {
  description = "PostgreSQL version"
  type        = string
}

variable "db_storage_size" {
  description = "Storage size in GB"
  type        = number
}

variable "db_admin_username" {
  description = "Database admin username"
  type        = string
  default     = "honuaadmin"
}

variable "db_admin_password" {
  description = "Database admin password"
  type        = string
  sensitive   = true
  default     = null
}

variable "db_backup_retention" {
  description = "Backup retention days"
  type        = number
  default     = 30
}

variable "enable_multi_az" {
  description = "Enable Multi-AZ for RDS"
  type        = bool
  default     = false
}

variable "enable_read_replica" {
  description = "Create as read replica"
  type        = bool
  default     = false
}

variable "source_db_identifier" {
  description = "Source DB identifier for read replica"
  type        = string
  default     = null
}

# Storage variables
variable "enable_storage_encryption" {
  description = "Enable storage encryption"
  type        = bool
  default     = true
}

variable "storage_tier" {
  description = "Storage tier"
  type        = string
  default     = "standard"
}

variable "source_bucket" {
  description = "Source bucket for replication"
  type        = string
  default     = null
}

# Redis variables
variable "enable_redis" {
  description = "Enable Redis cache"
  type        = bool
  default     = true
}

variable "redis_instance_type" {
  description = "ElastiCache instance type"
  type        = string
  default     = "cache.r6g.large"
}

variable "enable_redis_replica" {
  description = "Enable Redis replication"
  type        = bool
  default     = false
}

variable "primary_redis_endpoint" {
  description = "Primary Redis endpoint for replication"
  type        = string
  default     = null
}

# Network variables
variable "allowed_cidr_blocks" {
  description = "Allowed CIDR blocks"
  type        = list(string)
  default     = ["0.0.0.0/0"]
}

variable "enable_vpc_endpoints" {
  description = "Enable VPC endpoints"
  type        = bool
  default     = true
}

# Monitoring variables
variable "enable_monitoring" {
  description = "Enable monitoring"
  type        = bool
  default     = true
}

variable "enable_logging" {
  description = "Enable logging"
  type        = bool
  default     = true
}

variable "log_retention_days" {
  description = "Log retention days"
  type        = number
  default     = 30
}

variable "alert_email" {
  description = "Alert email"
  type        = string
  default     = null
}

# Security variables
variable "enable_waf" {
  description = "Enable WAF"
  type        = bool
  default     = true
}

variable "enable_encryption_in_transit" {
  description = "Enable encryption in transit"
  type        = bool
  default     = true
}

# Auto-scaling variables
variable "enable_auto_scaling" {
  description = "Enable auto-scaling"
  type        = bool
  default     = true
}

variable "min_instances" {
  description = "Minimum instances"
  type        = number
  default     = 1
}

variable "max_instances" {
  description = "Maximum instances"
  type        = number
  default     = 10
}

variable "target_cpu_utilization" {
  description = "Target CPU utilization"
  type        = number
  default     = 70
}

variable "tags" {
  description = "Tags"
  type        = map(string)
  default     = {}
}

# ============================================================================
# Locals
# ============================================================================

locals {
  name_prefix = "${var.project_name}-${var.environment}-${var.region}"
  role_suffix = var.is_primary ? "primary" : "dr"

  common_tags = merge(
    var.tags,
    {
      Region    = var.region
      Role      = local.role_suffix
      ManagedBy = "Terraform"
    }
  )
}

# ============================================================================
# VPC and Networking
# ============================================================================

resource "aws_vpc" "main" {
  cidr_block           = var.is_primary ? "10.0.0.0/16" : "10.1.0.0/16"
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-vpc"
    }
  )
}

resource "aws_subnet" "public" {
  count             = 3
  vpc_id            = aws_vpc.main.id
  cidr_block        = var.is_primary ? "10.0.${count.index}.0/24" : "10.1.${count.index}.0/24"
  availability_zone = data.aws_availability_zones.available.names[count.index]

  map_public_ip_on_launch = true

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-public-${count.index + 1}"
      Tier = "Public"
    }
  )
}

resource "aws_subnet" "private" {
  count             = 3
  vpc_id            = aws_vpc.main.id
  cidr_block        = var.is_primary ? "10.0.${count.index + 10}.0/24" : "10.1.${count.index + 10}.0/24"
  availability_zone = data.aws_availability_zones.available.names[count.index]

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-private-${count.index + 1}"
      Tier = "Private"
    }
  )
}

resource "aws_internet_gateway" "main" {
  vpc_id = aws_vpc.main.id

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-igw"
    }
  )
}

resource "aws_eip" "nat" {
  count  = 3
  domain = "vpc"

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-nat-eip-${count.index + 1}"
    }
  )
}

resource "aws_nat_gateway" "main" {
  count         = 3
  allocation_id = aws_eip.nat[count.index].id
  subnet_id     = aws_subnet.public[count.index].id

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-nat-${count.index + 1}"
    }
  )

  depends_on = [aws_internet_gateway.main]
}

resource "aws_route_table" "public" {
  vpc_id = aws_vpc.main.id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.main.id
  }

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-public-rt"
    }
  )
}

resource "aws_route_table" "private" {
  count  = 3
  vpc_id = aws_vpc.main.id

  route {
    cidr_block     = "0.0.0.0/0"
    nat_gateway_id = aws_nat_gateway.main[count.index].id
  }

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-private-rt-${count.index + 1}"
    }
  )
}

resource "aws_route_table_association" "public" {
  count          = 3
  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public.id
}

resource "aws_route_table_association" "private" {
  count          = 3
  subnet_id      = aws_subnet.private[count.index].id
  route_table_id = aws_route_table.private[count.index].id
}

# ============================================================================
# Data Sources
# ============================================================================

data "aws_availability_zones" "available" {
  state = "available"
}

# ============================================================================
# Security Groups
# ============================================================================

resource "aws_security_group" "alb" {
  name_prefix = "${local.name_prefix}-alb-"
  description = "Security group for ALB"
  vpc_id      = aws_vpc.main.id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = var.allowed_cidr_blocks
  }

  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = var.allowed_cidr_blocks
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-alb-sg"
    }
  )

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_security_group" "ecs_tasks" {
  name_prefix = "${local.name_prefix}-ecs-tasks-"
  description = "Security group for ECS tasks"
  vpc_id      = aws_vpc.main.id

  ingress {
    from_port       = var.container_port
    to_port         = var.container_port
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-ecs-tasks-sg"
    }
  )

  lifecycle {
    create_before_destroy = true
  }
}

resource "aws_security_group" "rds" {
  name_prefix = "${local.name_prefix}-rds-"
  description = "Security group for RDS"
  vpc_id      = aws_vpc.main.id

  ingress {
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.ecs_tasks.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-rds-sg"
    }
  )

  lifecycle {
    create_before_destroy = true
  }
}

# ============================================================================
# Placeholder Outputs (module would be much longer in production)
# ============================================================================
# This is a simplified example showing the module structure.
# A complete implementation would include:
# - ECS Cluster, Task Definitions, Services
# - Application Load Balancer
# - RDS PostgreSQL (primary or replica)
# - S3 buckets with replication
# - ElastiCache Redis
# - CloudWatch Logs and Dashboards
# - IAM Roles and Policies
# - Secrets Manager
# - Auto-scaling policies
# ============================================================================

output "vpc_id" {
  description = "VPC ID"
  value       = aws_vpc.main.id
}

output "public_subnet_ids" {
  description = "Public subnet IDs"
  value       = aws_subnet.public[*].id
}

output "private_subnet_ids" {
  description = "Private subnet IDs"
  value       = aws_subnet.private[*].id
}

output "load_balancer_dns" {
  description = "Load balancer DNS (placeholder)"
  value       = "honua-${var.environment}-${var.region}-lb.amazonaws.com"
}

output "load_balancer_zone_id" {
  description = "Load balancer zone ID (placeholder)"
  value       = "Z1234567890ABC"
}

output "load_balancer_arn_suffix" {
  description = "Load balancer ARN suffix (placeholder)"
  value       = "app/honua-${var.environment}-${var.region}/1234567890"
}

output "ecs_cluster_name" {
  description = "ECS cluster name (placeholder)"
  value       = "${local.name_prefix}-cluster"
}

output "db_identifier" {
  description = "Database identifier (placeholder)"
  value       = "${local.name_prefix}-postgres"
}

output "db_endpoint" {
  description = "Database endpoint (placeholder)"
  value       = "${local.name_prefix}-postgres.${var.unique_suffix}.${var.region}.rds.amazonaws.com:5432"
  sensitive   = true
}

output "storage_bucket_id" {
  description = "Storage bucket ID (placeholder)"
  value       = "${local.name_prefix}-storage-${var.unique_suffix}"
}

output "storage_bucket_regional_domain" {
  description = "Storage bucket regional domain (placeholder)"
  value       = "${local.name_prefix}-storage-${var.unique_suffix}.s3.${var.region}.amazonaws.com"
}

output "redis_endpoint" {
  description = "Redis endpoint (placeholder)"
  value       = var.enable_redis ? "${local.name_prefix}-redis.${var.unique_suffix}.0001.${var.region}.cache.amazonaws.com:6379" : null
  sensitive   = true
}
