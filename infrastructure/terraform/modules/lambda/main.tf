# AWS Lambda + ALB Serverless Module for Honua GIS Platform
# Deploys Honua as Lambda function behind Application Load Balancer

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    random = {
      source  = "hashicorp/random"
      version = "~> 3.5"
    }
  }
}

# ==================== Local Variables ====================
locals {
  name_prefix = "${var.service_name}-${var.environment}"

  # Environment variables for Lambda
  env_vars = merge(
    {
      ASPNETCORE_ENVIRONMENT      = var.aspnetcore_environment
      ASPNETCORE_URLS             = "http://+:8080"
      DOTNET_RUNNING_IN_CONTAINER = "true"
      DOTNET_TieredPGO            = "1"
      DOTNET_ReadyToRun           = "1"
      AWS_LWA_ENABLE_COMPRESSION  = "true"
      GDAL_CACHEMAX               = var.gdal_cache_max
      CORS_ORIGINS                = join(",", var.cors_origins)
    },
    var.additional_env_vars
  )

  common_tags = merge(
    {
      Environment = var.environment
      ManagedBy   = "Terraform"
      Application = "Honua"
      Tier        = "Serverless"
    },
    var.tags
  )
}

# ==================== VPC Configuration ====================
# VPC for Lambda and RDS
resource "aws_vpc" "honua" {
  count                = var.create_vpc ? 1 : 0
  cidr_block           = var.vpc_cidr
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-vpc"
    }
  )
}

# Internet Gateway
resource "aws_internet_gateway" "honua" {
  count  = var.create_vpc ? 1 : 0
  vpc_id = aws_vpc.honua[0].id

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-igw"
    }
  )
}

# Public Subnets (for ALB)
resource "aws_subnet" "public" {
  count                   = var.create_vpc ? length(var.availability_zones) : 0
  vpc_id                  = aws_vpc.honua[0].id
  cidr_block              = cidrsubnet(var.vpc_cidr, 4, count.index)
  availability_zone       = var.availability_zones[count.index]
  map_public_ip_on_launch = true

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-public-${var.availability_zones[count.index]}"
      Type = "Public"
    }
  )
}

# Private Subnets (for Lambda and RDS)
resource "aws_subnet" "private" {
  count             = var.create_vpc ? length(var.availability_zones) : 0
  vpc_id            = aws_vpc.honua[0].id
  cidr_block        = cidrsubnet(var.vpc_cidr, 4, count.index + length(var.availability_zones))
  availability_zone = var.availability_zones[count.index]

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-private-${var.availability_zones[count.index]}"
      Type = "Private"
    }
  )
}

# NAT Gateway for private subnet internet access
resource "aws_eip" "nat" {
  count  = var.create_vpc && var.enable_nat_gateway ? 1 : 0
  domain = "vpc"

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-nat-eip"
    }
  )
}

resource "aws_nat_gateway" "honua" {
  count         = var.create_vpc && var.enable_nat_gateway ? 1 : 0
  allocation_id = aws_eip.nat[0].id
  subnet_id     = aws_subnet.public[0].id

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-nat"
    }
  )

  depends_on = [aws_internet_gateway.honua]
}

# Route Tables
resource "aws_route_table" "public" {
  count  = var.create_vpc ? 1 : 0
  vpc_id = aws_vpc.honua[0].id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.honua[0].id
  }

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-public-rt"
    }
  )
}

resource "aws_route_table" "private" {
  count  = var.create_vpc ? 1 : 0
  vpc_id = aws_vpc.honua[0].id

  dynamic "route" {
    for_each = var.enable_nat_gateway ? [1] : []
    content {
      cidr_block     = "0.0.0.0/0"
      nat_gateway_id = aws_nat_gateway.honua[0].id
    }
  }

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-private-rt"
    }
  )
}

resource "aws_route_table_association" "public" {
  count          = var.create_vpc ? length(aws_subnet.public) : 0
  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public[0].id
}

resource "aws_route_table_association" "private" {
  count          = var.create_vpc ? length(aws_subnet.private) : 0
  subnet_id      = aws_subnet.private[count.index].id
  route_table_id = aws_route_table.private[0].id
}

# ==================== RDS PostgreSQL with PostGIS ====================
resource "aws_db_subnet_group" "honua" {
  count      = var.create_database ? 1 : 0
  name       = local.name_prefix
  subnet_ids = var.create_vpc ? aws_subnet.private[*].id : var.database_subnet_ids

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-db-subnet-group"
    }
  )
}

# Security Group for RDS
resource "aws_security_group" "rds" {
  count       = var.create_database ? 1 : 0
  name        = "${local.name_prefix}-rds"
  description = "Security group for RDS PostgreSQL"
  vpc_id      = var.create_vpc ? aws_vpc.honua[0].id : var.vpc_id

  ingress {
    description     = "PostgreSQL from Lambda"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    security_groups = [aws_security_group.lambda[0].id]
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
}

# Generate database password
resource "random_password" "db_password" {
  count   = var.create_database ? 1 : 0
  length  = 32
  special = true
}

# RDS Instance
resource "aws_db_instance" "honua" {
  count                  = var.create_database ? 1 : 0
  identifier             = local.name_prefix
  engine                 = "postgres"
  engine_version         = var.postgres_version
  instance_class         = var.db_instance_class
  allocated_storage      = var.db_allocated_storage
  max_allocated_storage  = var.db_max_allocated_storage
  storage_type           = "gp3"
  storage_encrypted      = true
  kms_key_id             = var.kms_key_arn != "" ? var.kms_key_arn : null

  db_name  = var.database_name
  username = var.db_username
  password = random_password.db_password[0].result
  port     = 5432

  multi_az               = var.db_multi_az
  db_subnet_group_name   = aws_db_subnet_group.honua[0].name
  vpc_security_group_ids = [aws_security_group.rds[0].id]

  backup_retention_period = var.db_backup_retention_days
  backup_window           = "03:00-04:00"
  maintenance_window      = "mon:04:00-mon:05:00"

  enabled_cloudwatch_logs_exports = ["postgresql", "upgrade"]

  performance_insights_enabled    = var.db_enable_performance_insights
  performance_insights_kms_key_id = var.kms_key_arn != "" ? var.kms_key_arn : null

  deletion_protection       = var.db_deletion_protection
  skip_final_snapshot       = !var.db_deletion_protection
  final_snapshot_identifier = var.db_deletion_protection ? "${local.name_prefix}-final-${formatdate("YYYY-MM-DD-hhmm", timestamp())}" : null

  copy_tags_to_snapshot = true

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-db"
    }
  )
}

# ==================== Secrets Manager ====================
# JWT Secret
resource "random_password" "jwt_secret" {
  length  = 64
  special = true
}

resource "aws_secretsmanager_secret" "jwt_secret" {
  name        = "${local.name_prefix}-jwt-secret"
  description = "JWT secret for Honua authentication"
  kms_key_id  = var.kms_key_arn != "" ? var.kms_key_arn : null

  tags = local.common_tags
}

resource "aws_secretsmanager_secret_version" "jwt_secret" {
  secret_id     = aws_secretsmanager_secret.jwt_secret.id
  secret_string = random_password.jwt_secret.result
}

# Database Connection String
resource "aws_secretsmanager_secret" "db_connection" {
  count       = var.create_database ? 1 : 0
  name        = "${local.name_prefix}-db-connection"
  description = "Database connection string for Honua"
  kms_key_id  = var.kms_key_arn != "" ? var.kms_key_arn : null

  tags = local.common_tags
}

resource "aws_secretsmanager_secret_version" "db_connection" {
  count     = var.create_database ? 1 : 0
  secret_id = aws_secretsmanager_secret.db_connection[0].id
  secret_string = jsonencode({
    username          = var.db_username
    password          = random_password.db_password[0].result
    host              = aws_db_instance.honua[0].address
    port              = 5432
    dbname            = var.database_name
    connection_string = "Host=${aws_db_instance.honua[0].address};Database=${var.database_name};Username=${var.db_username};Password=${random_password.db_password[0].result}"
  })
}

# ==================== ECR Repository ====================
resource "aws_ecr_repository" "honua" {
  count = var.create_ecr_repository ? 1 : 0
  name  = local.name_prefix

  image_scanning_configuration {
    scan_on_push = true
  }

  encryption_configuration {
    encryption_type = var.kms_key_arn != "" ? "KMS" : "AES256"
    kms_key         = var.kms_key_arn != "" ? var.kms_key_arn : null
  }

  tags = local.common_tags
}

# ==================== Lambda Function ====================
# Security Group for Lambda
resource "aws_security_group" "lambda" {
  count       = var.create_database ? 1 : 0
  name        = "${local.name_prefix}-lambda"
  description = "Security group for Lambda function"
  vpc_id      = var.create_vpc ? aws_vpc.honua[0].id : var.vpc_id

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-lambda-sg"
    }
  )
}

# IAM Role for Lambda
resource "aws_iam_role" "lambda" {
  name = "${local.name_prefix}-lambda-role"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Action = "sts:AssumeRole"
        Effect = "Allow"
        Principal = {
          Service = "lambda.amazonaws.com"
        }
      }
    ]
  })

  tags = local.common_tags
}

# Lambda VPC Execution Policy
resource "aws_iam_role_policy_attachment" "lambda_vpc" {
  role       = aws_iam_role.lambda.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaVPCAccessExecutionRole"
}

# Lambda Basic Execution Policy
resource "aws_iam_role_policy_attachment" "lambda_basic" {
  role       = aws_iam_role.lambda.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole"
}

# Secrets Manager Access Policy
resource "aws_iam_role_policy" "lambda_secrets" {
  name = "${local.name_prefix}-secrets-policy"
  role = aws_iam_role.lambda.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue"
        ]
        Resource = concat(
          [aws_secretsmanager_secret.jwt_secret.arn],
          var.create_database ? [aws_secretsmanager_secret.db_connection[0].arn] : []
        )
      }
    ]
  })
}

# S3 Access Policy (for GIS data)
resource "aws_iam_role_policy" "lambda_s3" {
  count = var.enable_s3_access ? 1 : 0
  name  = "${local.name_prefix}-s3-policy"
  role  = aws_iam_role.lambda.id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "s3:GetObject",
          "s3:ListBucket"
        ]
        Resource = var.s3_bucket_arns
      }
    ]
  })
}

# Lambda Function
resource "aws_lambda_function" "honua" {
  function_name = local.name_prefix
  role          = aws_iam_role.lambda.arn
  package_type  = "Image"
  image_uri     = var.container_image_uri
  timeout       = var.lambda_timeout
  memory_size   = var.lambda_memory_size

  ephemeral_storage {
    size = var.lambda_ephemeral_storage
  }

  vpc_config {
    subnet_ids         = var.create_vpc ? aws_subnet.private[*].id : var.lambda_subnet_ids
    security_group_ids = var.create_database ? [aws_security_group.lambda[0].id] : var.lambda_security_group_ids
  }

  environment {
    variables = merge(
      local.env_vars,
      {
        JWT_SECRET_ARN              = aws_secretsmanager_secret.jwt_secret.arn
        DATABASE_CONNECTION_SECRET  = var.create_database ? aws_secretsmanager_secret.db_connection[0].arn : ""
      }
    )
  }

  tags = local.common_tags

  depends_on = [
    aws_iam_role_policy_attachment.lambda_vpc,
    aws_iam_role_policy_attachment.lambda_basic
  ]
}

# Lambda Function URL (alternative to ALB for simple deployments)
resource "aws_lambda_function_url" "honua" {
  count              = var.create_function_url ? 1 : 0
  function_name      = aws_lambda_function.honua.function_name
  authorization_type = var.function_url_auth_type

  cors {
    allow_origins     = var.cors_origins
    allow_methods     = ["*"]
    allow_headers     = ["*"]
    expose_headers    = ["*"]
    max_age           = 86400
    allow_credentials = true
  }
}

# Lambda Autoscaling
resource "aws_appautoscaling_target" "lambda" {
  count              = var.enable_lambda_autoscaling ? 1 : 0
  max_capacity       = var.lambda_max_provisioned_concurrency
  min_capacity       = var.lambda_min_provisioned_concurrency
  resource_id        = "function:${aws_lambda_function.honua.function_name}:provisioned-concurrency:prod"
  scalable_dimension = "lambda:function:ProvisionedConcurrentExecutions"
  service_namespace  = "lambda"
}

# ==================== Application Load Balancer ====================
# Security Group for ALB
resource "aws_security_group" "alb" {
  count       = var.create_alb ? 1 : 0
  name        = "${local.name_prefix}-alb"
  description = "Security group for Application Load Balancer"
  vpc_id      = var.create_vpc ? aws_vpc.honua[0].id : var.vpc_id

  ingress {
    description = "HTTPS from internet"
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    description = "HTTP from internet"
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
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
}

# Application Load Balancer
resource "aws_lb" "honua" {
  count              = var.create_alb ? 1 : 0
  name               = local.name_prefix
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb[0].id]
  subnets            = var.create_vpc ? aws_subnet.public[*].id : var.alb_subnet_ids

  enable_deletion_protection = var.alb_deletion_protection
  enable_http2               = true
  enable_cross_zone_load_balancing = true

  access_logs {
    enabled = var.alb_enable_access_logs
    bucket  = var.alb_access_logs_bucket
    prefix  = local.name_prefix
  }

  tags = merge(
    local.common_tags,
    {
      Name = "${local.name_prefix}-alb"
    }
  )
}

# Target Group for Lambda
resource "aws_lb_target_group" "lambda" {
  count       = var.create_alb ? 1 : 0
  name        = "${local.name_prefix}-lambda-tg"
  target_type = "lambda"

  health_check {
    enabled             = true
    path                = var.health_check_path
    interval            = 30
    timeout             = 5
    healthy_threshold   = 2
    unhealthy_threshold = 2
    matcher             = "200"
  }

  tags = local.common_tags
}

# Attach Lambda to Target Group
resource "aws_lb_target_group_attachment" "lambda" {
  count            = var.create_alb ? 1 : 0
  target_group_arn = aws_lb_target_group.lambda[0].arn
  target_id        = aws_lambda_function.honua.arn
  depends_on       = [aws_lambda_permission.alb]
}

# Lambda permission for ALB
resource "aws_lambda_permission" "alb" {
  count         = var.create_alb ? 1 : 0
  statement_id  = "AllowExecutionFromALB"
  action        = "lambda:InvokeFunction"
  function_name = aws_lambda_function.honua.function_name
  principal     = "elasticloadbalancing.amazonaws.com"
  source_arn    = aws_lb_target_group.lambda[0].arn
}

# HTTPS Listener (with SSL)
resource "aws_lb_listener" "https" {
  count             = var.create_alb && var.ssl_certificate_arn != "" ? 1 : 0
  load_balancer_arn = aws_lb.honua[0].arn
  port              = 443
  protocol          = "HTTPS"
  ssl_policy        = "ELBSecurityPolicy-TLS-1-2-2017-01"
  certificate_arn   = var.ssl_certificate_arn

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.lambda[0].arn
  }
}

# HTTP Listener (redirect to HTTPS)
resource "aws_lb_listener" "http" {
  count             = var.create_alb ? 1 : 0
  load_balancer_arn = aws_lb.honua[0].arn
  port              = 80
  protocol          = "HTTP"

  default_action {
    type = var.ssl_certificate_arn != "" ? "redirect" : "forward"

    dynamic "redirect" {
      for_each = var.ssl_certificate_arn != "" ? [1] : []
      content {
        port        = "443"
        protocol    = "HTTPS"
        status_code = "HTTP_301"
      }
    }

    target_group_arn = var.ssl_certificate_arn == "" ? aws_lb_target_group.lambda[0].arn : null
  }
}

# ==================== CloudWatch Logs ====================
resource "aws_cloudwatch_log_group" "lambda" {
  name              = "/aws/lambda/${local.name_prefix}"
  retention_in_days = var.log_retention_days
  kms_key_id        = var.kms_key_arn != "" ? var.kms_key_arn : null

  tags = local.common_tags
}
