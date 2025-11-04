# Variables for AWS Lambda + ALB Serverless Module

# ==================== General Configuration ====================
variable "environment" {
  description = "Environment name (dev, staging, production)"
  type        = string
  validation {
    condition     = contains(["dev", "staging", "production"], var.environment)
    error_message = "Environment must be dev, staging, or production."
  }
}

variable "service_name" {
  description = "Name of the service"
  type        = string
  default     = "honua"
}

variable "aws_region" {
  description = "AWS region"
  type        = string
  default     = "us-east-1"
}

# ==================== Container Configuration ====================
variable "container_image_uri" {
  description = "URI of container image in ECR (e.g., 123456789012.dkr.ecr.us-east-1.amazonaws.com/honua:latest)"
  type        = string
}

variable "aspnetcore_environment" {
  description = "ASP.NET Core environment (Development, Staging, Production)"
  type        = string
  default     = "Production"
}

variable "additional_env_vars" {
  description = "Additional environment variables for the container"
  type        = map(string)
  default     = {}
}

variable "cors_origins" {
  description = "Allowed CORS origins"
  type        = list(string)
  default     = ["*"]
}

variable "gdal_cache_max" {
  description = "GDAL cache size in MB"
  type        = string
  default     = "512"
}

# ==================== Lambda Configuration ====================
variable "lambda_timeout" {
  description = "Lambda function timeout in seconds (max 900)"
  type        = number
  default     = 300
  validation {
    condition     = var.lambda_timeout >= 1 && var.lambda_timeout <= 900
    error_message = "lambda_timeout must be between 1 and 900 seconds."
  }
}

variable "lambda_memory_size" {
  description = "Lambda memory size in MB (128 to 10240, in 1 MB increments)"
  type        = number
  default     = 2048
  validation {
    condition     = var.lambda_memory_size >= 128 && var.lambda_memory_size <= 10240
    error_message = "lambda_memory_size must be between 128 and 10240 MB."
  }
}

variable "lambda_ephemeral_storage" {
  description = "Lambda ephemeral storage in MB (512 to 10240)"
  type        = number
  default     = 2048
  validation {
    condition     = var.lambda_ephemeral_storage >= 512 && var.lambda_ephemeral_storage <= 10240
    error_message = "lambda_ephemeral_storage must be between 512 and 10240 MB."
  }
}

variable "enable_lambda_autoscaling" {
  description = "Enable Lambda provisioned concurrency autoscaling"
  type        = bool
  default     = false
}

variable "lambda_min_provisioned_concurrency" {
  description = "Minimum provisioned concurrency for Lambda"
  type        = number
  default     = 0
}

variable "lambda_max_provisioned_concurrency" {
  description = "Maximum provisioned concurrency for Lambda"
  type        = number
  default     = 100
}

# ==================== Lambda Function URL ====================
variable "create_function_url" {
  description = "Create Lambda Function URL (simpler alternative to ALB)"
  type        = bool
  default     = false
}

variable "function_url_auth_type" {
  description = "Authorization type for function URL (NONE or AWS_IAM)"
  type        = string
  default     = "NONE"
  validation {
    condition     = contains(["NONE", "AWS_IAM"], var.function_url_auth_type)
    error_message = "function_url_auth_type must be NONE or AWS_IAM."
  }
}

# ==================== VPC Configuration ====================
variable "create_vpc" {
  description = "Create new VPC for Lambda and RDS"
  type        = bool
  default     = true
}

variable "vpc_id" {
  description = "Existing VPC ID (if not creating new VPC)"
  type        = string
  default     = ""
}

variable "vpc_cidr" {
  description = "CIDR block for VPC"
  type        = string
  default     = "10.0.0.0/16"
}

variable "availability_zones" {
  description = "List of availability zones"
  type        = list(string)
  default     = ["us-east-1a", "us-east-1b"]
}

variable "enable_nat_gateway" {
  description = "Enable NAT Gateway for private subnets"
  type        = bool
  default     = true
}

variable "lambda_subnet_ids" {
  description = "Subnet IDs for Lambda (if not creating VPC)"
  type        = list(string)
  default     = []
}

variable "lambda_security_group_ids" {
  description = "Security group IDs for Lambda (if not creating VPC)"
  type        = list(string)
  default     = []
}

# ==================== Database Configuration ====================
variable "create_database" {
  description = "Create RDS PostgreSQL instance"
  type        = bool
  default     = true
}

variable "database_name" {
  description = "Name of the PostgreSQL database"
  type        = string
  default     = "honua"
}

variable "db_username" {
  description = "Database username"
  type        = string
  default     = "honua"
}

variable "postgres_version" {
  description = "PostgreSQL version"
  type        = string
  default     = "15.4"
}

variable "db_instance_class" {
  description = "RDS instance class"
  type        = string
  default     = "db.t4g.small"
}

variable "db_allocated_storage" {
  description = "Allocated storage in GB"
  type        = number
  default     = 20
}

variable "db_max_allocated_storage" {
  description = "Maximum allocated storage for autoscaling in GB"
  type        = number
  default     = 100
}

variable "db_multi_az" {
  description = "Enable Multi-AZ deployment"
  type        = bool
  default     = false
}

variable "db_backup_retention_days" {
  description = "Database backup retention in days"
  type        = number
  default     = 7
}

variable "db_enable_performance_insights" {
  description = "Enable Performance Insights"
  type        = bool
  default     = true
}

variable "db_deletion_protection" {
  description = "Prevent accidental database deletion"
  type        = bool
  default     = true
}

variable "database_subnet_ids" {
  description = "Subnet IDs for database (if not creating VPC)"
  type        = list(string)
  default     = []
}

# ==================== ALB Configuration ====================
variable "create_alb" {
  description = "Create Application Load Balancer"
  type        = bool
  default     = true
}

variable "alb_subnet_ids" {
  description = "Subnet IDs for ALB (if not creating VPC)"
  type        = list(string)
  default     = []
}

variable "alb_deletion_protection" {
  description = "Enable ALB deletion protection"
  type        = bool
  default     = false
}

variable "alb_enable_access_logs" {
  description = "Enable ALB access logs"
  type        = bool
  default     = false
}

variable "alb_access_logs_bucket" {
  description = "S3 bucket for ALB access logs"
  type        = string
  default     = ""
}

variable "ssl_certificate_arn" {
  description = "ARN of ACM SSL certificate"
  type        = string
  default     = ""
}

# ==================== ECR Configuration ====================
variable "create_ecr_repository" {
  description = "Create ECR repository for container images"
  type        = bool
  default     = false
}

# ==================== Security Configuration ====================
variable "kms_key_arn" {
  description = "KMS key ARN for encryption"
  type        = string
  default     = ""
}

# ==================== S3 Access ====================
variable "enable_s3_access" {
  description = "Grant Lambda access to S3"
  type        = bool
  default     = true
}

variable "s3_bucket_arns" {
  description = "S3 bucket ARNs for Lambda access"
  type        = list(string)
  default     = ["*"]
}

# ==================== Logging ====================
variable "log_retention_days" {
  description = "CloudWatch log retention in days"
  type        = number
  default     = 7
}

# ==================== Health Check ====================
variable "health_check_path" {
  description = "HTTP path for health checks"
  type        = string
  default     = "/health"
}

# ==================== Tags ====================
variable "tags" {
  description = "Additional tags for all resources"
  type        = map(string)
  default     = {}
}
