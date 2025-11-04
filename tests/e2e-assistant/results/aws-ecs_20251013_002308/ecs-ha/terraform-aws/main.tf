terraform {
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }
}

provider "aws" {
  region = var.aws_region
}

variable "aws_region" {
  description = "AWS region"
  default     = "us-east-1"
}

variable "environment" {
  description = "Environment name"
  default     = "development"
}

# VPC Configuration
resource "aws_vpc" "honua" {
  cidr_block           = "10.0.0.0/16"
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = {
    Name        = "honua-vpc-${var.environment}"
    Environment = var.environment
  }
}

# Subnets
resource "aws_subnet" "public" {
  count                   = 2
  vpc_id                  = aws_vpc.honua.id
  cidr_block              = "10.0.${count.index + 1}.0/24"
  availability_zone       = data.aws_availability_zones.available.names[count.index]
  map_public_ip_on_launch = true

  tags = {
    Name        = "honua-public-subnet-${count.index + 1}"
    Environment = var.environment
  }
}

resource "aws_subnet" "private" {
  count             = 2
  vpc_id            = aws_vpc.honua.id
  cidr_block        = "10.0.${count.index + 10}.0/24"
  availability_zone = data.aws_availability_zones.available.names[count.index]

  tags = {
    Name        = "honua-private-subnet-${count.index + 1}"
    Environment = var.environment
  }
}

# Internet Gateway
resource "aws_internet_gateway" "honua" {
  vpc_id = aws_vpc.honua.id

  tags = {
    Name        = "honua-igw-${var.environment}"
    Environment = var.environment
  }
}

# ECS Cluster for Honua Server
resource "aws_ecs_cluster" "honua" {
  name = "honua-cluster-${var.environment}"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

# Fargate Task Definition for Honua Server
resource "aws_ecs_task_definition" "honua_server" {
  family                   = "honua-server"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "1024"
  memory                   = "2048"
  execution_role_arn       = aws_iam_role.ecs_execution.arn
  task_role_arn           = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([{
    name  = "honua-server"
    image = "honuaio/honua-server:latest"

    portMappings = [{
      containerPort = 8080
      hostPort      = 8080
    }]

    environment = [
      {
        name  = "ASPNETCORE_ENVIRONMENT"
        value = var.environment
      }
,
      {
        name  = "HONUA__CACHE__PROVIDER"
        value = "redis"
      },
      {
        name  = "HONUA__CACHE__REDIS__HOST"
        value = aws_elasticache_cluster.redis.cache_nodes[0].address
      }
    ]

    logConfiguration = {
      logDriver = "awslogs"
      options = {
        "awslogs-group"         = "/ecs/honua-server"
        "awslogs-region"        = var.aws_region
        "awslogs-stream-prefix" = "ecs"
      }
    }
  }])
}

# IAM Roles
resource "aws_iam_role" "ecs_execution" {
  name = "honua-ecs-execution-role-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "ecs-tasks.amazonaws.com"
      }
    }]
  })
}

resource "aws_iam_role" "ecs_task" {
  name = "honua-ecs-task-role-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "ecs-tasks.amazonaws.com"
      }
    }]
  })
}

resource "aws_iam_role_policy_attachment" "ecs_execution" {
  role       = aws_iam_role.ecs_execution.name
  policy_arn = "arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy"
}

# ElastiCache Redis
resource "aws_elasticache_subnet_group" "redis" {
  name       = "honua-redis-subnet-${var.environment}"
  subnet_ids = aws_subnet.private[*].id
}

resource "aws_elasticache_cluster" "redis" {
  cluster_id           = "honua-redis-${var.environment}"
  engine              = "redis"
  engine_version      = "7.0"
  node_type           = "cache.t3.micro"
  num_cache_nodes     = 1
  parameter_group_name = "default.redis7"
  port                = 6379

  subnet_group_name = aws_elasticache_subnet_group.redis.name
  security_group_ids = [aws_security_group.redis.id]

  tags = {
    Name        = "honua-redis-${var.environment}"
    Environment = var.environment
  }
}

resource "aws_security_group" "redis" {
  name        = "honua-redis-sg-${var.environment}"
  description = "Security group for Honua Redis cache"
  vpc_id      = aws_vpc.honua.id

  ingress {
    from_port       = 6379
    to_port         = 6379
    protocol        = "tcp"
    security_groups = [aws_security_group.ecs_service.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name        = "honua-redis-sg-${var.environment}"
    Environment = var.environment
  }
}

# S3 Bucket for tile caching and static assets
resource "aws_s3_bucket" "honua_cache" {
  bucket = "honua-cache-${var.environment}-${data.aws_caller_identity.current.account_id}"

  tags = {
    Name        = "honua-cache-${var.environment}"
    Environment = var.environment
  }
}

resource "aws_s3_bucket_lifecycle_configuration" "honua_cache" {
  bucket = aws_s3_bucket.honua_cache.id

  rule {
    id     = "expire-old-tiles"
    status = "Enabled"

    expiration {
      days = 30
    }

    filter {
      prefix = "tiles/"
    }
  }

  rule {
    id     = "transition-to-ia"
    status = "Enabled"

    transition {
      days          = 90
      storage_class = "STANDARD_IA"
    }

    filter {
      prefix = "archive/"
    }
  }
}

resource "aws_s3_bucket_public_access_block" "honua_cache" {
  bucket = aws_s3_bucket.honua_cache.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# CloudWatch Log Group
resource "aws_cloudwatch_log_group" "honua" {
  name              = "/ecs/honua-server"
  retention_in_days = 30

  tags = {
    Name        = "honua-logs-${var.environment}"
    Environment = var.environment
  }
}

# Application Load Balancer
resource "aws_lb" "honua" {
  name               = "honua-alb-${var.environment}"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets           = aws_subnet.public[*].id

  enable_deletion_protection = var.environment == "production"
  enable_http2              = true

  tags = {
    Name        = "honua-alb-${var.environment}"
    Environment = var.environment
  }
}

resource "aws_lb_target_group" "honua" {
  name        = "honua-tg-${var.environment}"
  port        = 8080
  protocol    = "HTTP"
  vpc_id      = aws_vpc.honua.id
  target_type = "ip"

  health_check {
    enabled             = true
    healthy_threshold   = 2
    interval            = 30
    matcher            = "200"
    path               = "/health"
    port               = "traffic-port"
    protocol           = "HTTP"
    timeout            = 5
    unhealthy_threshold = 3
  }

  deregistration_delay = 30

  tags = {
    Name        = "honua-tg-${var.environment}"
    Environment = var.environment
  }
}

resource "aws_lb_listener" "honua" {
  load_balancer_arn = aws_lb.honua.arn
  port              = "80"
  protocol          = "HTTP"

  default_action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.honua.arn
  }
}

resource "aws_security_group" "alb" {
  name        = "honua-alb-sg-${var.environment}"
  description = "Security group for Honua ALB"
  vpc_id      = aws_vpc.honua.id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = "tcp"
    cidr_blocks = ["0.0.0.0/0"]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name        = "honua-alb-sg-${var.environment}"
    Environment = var.environment
  }
}

# ECS Service
resource "aws_ecs_service" "honua" {
  name            = "honua-service-${var.environment}"
  cluster         = aws_ecs_cluster.honua.id
  task_definition = aws_ecs_task_definition.honua.arn
  desired_count   = var.environment == "production" ? 2 : 1
  launch_type     = "FARGATE"

  network_configuration {
    subnets          = aws_subnet.private[*].id
    security_groups  = [aws_security_group.ecs_service.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.honua.arn
    container_name   = "honua-server"
    container_port   = 8080
  }

  depends_on = [aws_lb_listener.honua]

  tags = {
    Name        = "honua-service-${var.environment}"
    Environment = var.environment
  }
}

# Auto-scaling for ECS Service
resource "aws_appautoscaling_target" "honua" {
  max_capacity       = 10
  min_capacity       = 1
  resource_id        = "service/${aws_ecs_cluster.honua.name}/${aws_ecs_service.honua.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  service_namespace  = "ecs"
}

resource "aws_appautoscaling_policy" "honua_cpu" {
  name               = "honua-cpu-autoscaling-${var.environment}"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.honua.resource_id
  scalable_dimension = aws_appautoscaling_target.honua.scalable_dimension
  service_namespace  = aws_appautoscaling_target.honua.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
    target_value = 70.0
  }
}

resource "aws_appautoscaling_policy" "honua_memory" {
  name               = "honua-memory-autoscaling-${var.environment}"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.honua.resource_id
  scalable_dimension = aws_appautoscaling_target.honua.scalable_dimension
  service_namespace  = aws_appautoscaling_target.honua.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageMemoryUtilization"
    }
    target_value = 80.0
  }
}

# Data source for current AWS account
data "aws_caller_identity" "current" {}


# Security Group for ECS Service
resource "aws_security_group" "ecs_service" {
  name        = "honua-ecs-sg-${var.environment}"
  description = "Security group for Honua ECS service"
  vpc_id      = aws_vpc.honua.id

  ingress {
    from_port       = 8080
    to_port         = 8080
    protocol        = "tcp"
    security_groups = [aws_security_group.alb.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = {
    Name        = "honua-ecs-sg-${var.environment}"
    Environment = var.environment
  }
}

# Data sources
data "aws_availability_zones" "available" {
  state = "available"
}

# Outputs
output "honua_server_url" {
  value = "http://${aws_lb.honua.dns_name}"
}