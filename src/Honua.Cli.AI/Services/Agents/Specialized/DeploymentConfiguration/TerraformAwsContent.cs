// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Linq;
using System.Text;

namespace Honua.Cli.AI.Services.Agents.Specialized.DeploymentConfiguration;

/// <summary>
/// Helper class containing AWS Terraform generation logic.
/// Extracted from DeploymentConfigurationAgent to separate infrastructure code generation.
/// </summary>
internal static class TerraformAwsContent
{
    public static string GenerateEcs(DeploymentAnalysis analysis)
    {
        var hasDatabase = analysis.InfrastructureNeeds.NeedsDatabase ||
                         analysis.RequiredServices.Any(s => s.Contains("postgis", StringComparison.OrdinalIgnoreCase));
        var hasCache = analysis.InfrastructureNeeds.NeedsCache ||
                      analysis.RequiredServices.Any(s => s.Contains("redis", StringComparison.OrdinalIgnoreCase));

        var tf = new System.Text.StringBuilder();
        tf.AppendLine(@"terraform {
  required_providers {
    aws = {
      source  = ""hashicorp/aws""
      version = ""~> 5.0""
    }
  }
}

provider ""aws"" {
  region = var.aws_region
}

variable ""aws_region"" {
  description = ""AWS region""
  default     = ""us-east-1""
}

variable ""environment"" {
  description = ""Environment name""
  default     = """ + analysis.TargetEnvironment + @"""
}

# VPC Configuration
resource ""aws_vpc"" ""honua"" {
  cidr_block           = ""10.0.0.0/16""
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = {
    Name        = ""honua-vpc-${var.environment}""
    Environment = var.environment
  }
}

# Subnets
resource ""aws_subnet"" ""public"" {
  count                   = 2
  vpc_id                  = aws_vpc.honua.id
  cidr_block              = ""10.0.${count.index + 1}.0/24""
  availability_zone       = data.aws_availability_zones.available.names[count.index]
  map_public_ip_on_launch = true

  tags = {
    Name        = ""honua-public-subnet-${count.index + 1}""
    Environment = var.environment
  }
}

resource ""aws_subnet"" ""private"" {
  count             = 2
  vpc_id            = aws_vpc.honua.id
  cidr_block        = ""10.0.${count.index + 10}.0/24""
  availability_zone = data.aws_availability_zones.available.names[count.index]

  tags = {
    Name        = ""honua-private-subnet-${count.index + 1}""
    Environment = var.environment
  }
}

# Internet Gateway
resource ""aws_internet_gateway"" ""honua"" {
  vpc_id = aws_vpc.honua.id

  tags = {
    Name        = ""honua-igw-${var.environment}""
    Environment = var.environment
  }
}

# ECS Cluster for Honua Server
resource ""aws_ecs_cluster"" ""honua"" {
  name = ""honua-cluster-${var.environment}""

  setting {
    name  = ""containerInsights""
    value = ""enabled""
  }
}

# Fargate Task Definition for Honua Server
resource ""aws_ecs_task_definition"" ""honua_server"" {
  family                   = ""honua-server""
  network_mode             = ""awsvpc""
  requires_compatibilities = [""FARGATE""]
  cpu                      = ""1024""
  memory                   = ""2048""
  execution_role_arn       = aws_iam_role.ecs_execution.arn
  task_role_arn           = aws_iam_role.ecs_task.arn

  container_definitions = jsonencode([{
    name  = ""honua-server""
    image = ""honuaio/honua-server:latest""

    portMappings = [{
      containerPort = 8080
      hostPort      = 8080
    }]

    environment = [
      {
        name  = ""ASPNETCORE_ENVIRONMENT""
        value = var.environment
      }");

        if (hasDatabase)
        {
            tf.AppendLine(@",
      {
        name  = ""HONUA__DATABASE__HOST""
        value = aws_db_instance.postgis.address
      },
      {
        name  = ""HONUA__DATABASE__PORT""
        value = ""5432""
      },
      {
        name  = ""HONUA__DATABASE__DATABASE""
        value = ""honua""
      }");
        }

        if (hasCache)
        {
            tf.AppendLine(@",
      {
        name  = ""HONUA__CACHE__PROVIDER""
        value = ""redis""
      },
      {
        name  = ""HONUA__CACHE__REDIS__HOST""
        value = aws_elasticache_cluster.redis.cache_nodes[0].address
      }");
        }

        tf.AppendLine(@"    ]

    logConfiguration = {
      logDriver = ""awslogs""
      options = {
        ""awslogs-group""         = ""/ecs/honua-server""
        ""awslogs-region""        = var.aws_region
        ""awslogs-stream-prefix"" = ""ecs""
      }
    }
  }])
}

# IAM Roles
resource ""aws_iam_role"" ""ecs_execution"" {
  name = ""honua-ecs-execution-role-${var.environment}""

  assume_role_policy = jsonencode({
    Version = ""2012-10-17""
    Statement = [{
      Action = ""sts:AssumeRole""
      Effect = ""Allow""
      Principal = {
        Service = ""ecs-tasks.amazonaws.com""
      }
    }]
  })
}

resource ""aws_iam_role"" ""ecs_task"" {
  name = ""honua-ecs-task-role-${var.environment}""

  assume_role_policy = jsonencode({
    Version = ""2012-10-17""
    Statement = [{
      Action = ""sts:AssumeRole""
      Effect = ""Allow""
      Principal = {
        Service = ""ecs-tasks.amazonaws.com""
      }
    }]
  })
}

resource ""aws_iam_role_policy_attachment"" ""ecs_execution"" {
  role       = aws_iam_role.ecs_execution.name
  policy_arn = ""arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy""
}");

        if (hasDatabase)
        {
            tf.AppendLine(@"
# RDS PostgreSQL with PostGIS
resource ""aws_db_subnet_group"" ""honua"" {
  name       = ""honua-db-subnet-${var.environment}""
  subnet_ids = aws_subnet.private[*].id

  tags = {
    Name        = ""Honua DB subnet group""
    Environment = var.environment
  }
}

resource ""aws_db_instance"" ""postgis"" {
  identifier     = ""honua-postgis-${var.environment}""
  engine         = ""postgres""
  engine_version = ""15.4""
  instance_class = ""db.t3.micro""

  allocated_storage     = 20
  max_allocated_storage = 100
  storage_type          = ""gp3""
  storage_encrypted     = true

  db_name  = ""honua""
  username = ""postgres""
  password = random_password.db_password.result

  vpc_security_group_ids = [aws_security_group.database.id]
  db_subnet_group_name   = aws_db_subnet_group.honua.name

  backup_retention_period = 7
  backup_window          = ""03:00-04:00""
  maintenance_window     = ""sun:04:00-sun:05:00""

  skip_final_snapshot = var.environment != ""production""
  deletion_protection = var.environment == ""production""

  tags = {
    Name        = ""honua-postgis-${var.environment}""
    Environment = var.environment
  }
}

resource ""random_password"" ""db_password"" {
  length  = 32
  special = true
}

resource ""aws_security_group"" ""database"" {
  name        = ""honua-db-sg-${var.environment}""
  description = ""Security group for Honua RDS database""
  vpc_id      = aws_vpc.honua.id

  ingress {
    from_port       = 5432
    to_port         = 5432
    protocol        = ""tcp""
    security_groups = [aws_security_group.ecs_service.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = ""-1""
    cidr_blocks = [""0.0.0.0/0""]
  }

  tags = {
    Name        = ""honua-db-sg-${var.environment}""
    Environment = var.environment
  }
}");
        }

        if (hasCache)
        {
            tf.AppendLine(@"
# ElastiCache Redis
resource ""aws_elasticache_subnet_group"" ""redis"" {
  name       = ""honua-redis-subnet-${var.environment}""
  subnet_ids = aws_subnet.private[*].id
}

resource ""aws_elasticache_cluster"" ""redis"" {
  cluster_id           = ""honua-redis-${var.environment}""
  engine              = ""redis""
  engine_version      = ""7.0""
  node_type           = ""cache.t3.micro""
  num_cache_nodes     = 1
  parameter_group_name = ""default.redis7""
  port                = 6379

  subnet_group_name = aws_elasticache_subnet_group.redis.name
  security_group_ids = [aws_security_group.redis.id]

  tags = {
    Name        = ""honua-redis-${var.environment}""
    Environment = var.environment
  }
}

resource ""aws_security_group"" ""redis"" {
  name        = ""honua-redis-sg-${var.environment}""
  description = ""Security group for Honua Redis cache""
  vpc_id      = aws_vpc.honua.id

  ingress {
    from_port       = 6379
    to_port         = 6379
    protocol        = ""tcp""
    security_groups = [aws_security_group.ecs_service.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = ""-1""
    cidr_blocks = [""0.0.0.0/0""]
  }

  tags = {
    Name        = ""honua-redis-sg-${var.environment}""
    Environment = var.environment
  }
}");
        }

        tf.AppendLine(@"
# S3 Bucket for tile caching and static assets
resource ""aws_s3_bucket"" ""honua_cache"" {
  bucket = ""honua-cache-${var.environment}-${data.aws_caller_identity.current.account_id}""

  tags = {
    Name        = ""honua-cache-${var.environment}""
    Environment = var.environment
  }
}

resource ""aws_s3_bucket_lifecycle_configuration"" ""honua_cache"" {
  bucket = aws_s3_bucket.honua_cache.id

  rule {
    id     = ""expire-old-tiles""
    status = ""Enabled""

    expiration {
      days = 30
    }

    filter {
      prefix = ""tiles/""
    }
  }

  rule {
    id     = ""transition-to-ia""
    status = ""Enabled""

    transition {
      days          = 90
      storage_class = ""STANDARD_IA""
    }

    filter {
      prefix = ""archive/""
    }
  }
}

resource ""aws_s3_bucket_public_access_block"" ""honua_cache"" {
  bucket = aws_s3_bucket.honua_cache.id

  block_public_acls       = true
  block_public_policy     = true
  ignore_public_acls      = true
  restrict_public_buckets = true
}

# CloudWatch Log Group
resource ""aws_cloudwatch_log_group"" ""honua"" {
  name              = ""/ecs/honua-server""
  retention_in_days = 30

  tags = {
    Name        = ""honua-logs-${var.environment}""
    Environment = var.environment
  }
}

# Application Load Balancer
resource ""aws_lb"" ""honua"" {
  name               = ""honua-alb-${var.environment}""
  internal           = false
  load_balancer_type = ""application""
  security_groups    = [aws_security_group.alb.id]
  subnets           = aws_subnet.public[*].id

  enable_deletion_protection = var.environment == ""production""
  enable_http2              = true

  tags = {
    Name        = ""honua-alb-${var.environment}""
    Environment = var.environment
  }
}

resource ""aws_lb_target_group"" ""honua"" {
  name        = ""honua-tg-${var.environment}""
  port        = 8080
  protocol    = ""HTTP""
  vpc_id      = aws_vpc.honua.id
  target_type = ""ip""

  health_check {
    enabled             = true
    healthy_threshold   = 2
    interval            = 30
    matcher            = ""200""
    path               = ""/health""
    port               = ""traffic-port""
    protocol           = ""HTTP""
    timeout            = 5
    unhealthy_threshold = 3
  }

  deregistration_delay = 30

  tags = {
    Name        = ""honua-tg-${var.environment}""
    Environment = var.environment
  }
}

resource ""aws_lb_listener"" ""honua"" {
  load_balancer_arn = aws_lb.honua.arn
  port              = ""80""
  protocol          = ""HTTP""

  default_action {
    type             = ""forward""
    target_group_arn = aws_lb_target_group.honua.arn
  }
}

resource ""aws_security_group"" ""alb"" {
  name        = ""honua-alb-sg-${var.environment}""
  description = ""Security group for Honua ALB""
  vpc_id      = aws_vpc.honua.id

  ingress {
    from_port   = 80
    to_port     = 80
    protocol    = ""tcp""
    cidr_blocks = [""0.0.0.0/0""]
  }

  ingress {
    from_port   = 443
    to_port     = 443
    protocol    = ""tcp""
    cidr_blocks = [""0.0.0.0/0""]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = ""-1""
    cidr_blocks = [""0.0.0.0/0""]
  }

  tags = {
    Name        = ""honua-alb-sg-${var.environment}""
    Environment = var.environment
  }
}

# ECS Service
resource ""aws_ecs_service"" ""honua"" {
  name            = ""honua-service-${var.environment}""
  cluster         = aws_ecs_cluster.honua.id
  task_definition = aws_ecs_task_definition.honua.arn
  desired_count   = var.environment == ""production"" ? 2 : 1
  launch_type     = ""FARGATE""

  network_configuration {
    subnets          = aws_subnet.private[*].id
    security_groups  = [aws_security_group.ecs_service.id]
    assign_public_ip = false
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.honua.arn
    container_name   = ""honua-server""
    container_port   = 8080
  }

  depends_on = [aws_lb_listener.honua]

  tags = {
    Name        = ""honua-service-${var.environment}""
    Environment = var.environment
  }
}

# Auto-scaling for ECS Service
resource ""aws_appautoscaling_target"" ""honua"" {
  max_capacity       = 10
  min_capacity       = 1
  resource_id        = ""service/${aws_ecs_cluster.honua.name}/${aws_ecs_service.honua.name}""
  scalable_dimension = ""ecs:service:DesiredCount""
  service_namespace  = ""ecs""
}

resource ""aws_appautoscaling_policy"" ""honua_cpu"" {
  name               = ""honua-cpu-autoscaling-${var.environment}""
  policy_type        = ""TargetTrackingScaling""
  resource_id        = aws_appautoscaling_target.honua.resource_id
  scalable_dimension = aws_appautoscaling_target.honua.scalable_dimension
  service_namespace  = aws_appautoscaling_target.honua.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = ""ECSServiceAverageCPUUtilization""
    }
    target_value = 70.0
  }
}

resource ""aws_appautoscaling_policy"" ""honua_memory"" {
  name               = ""honua-memory-autoscaling-${var.environment}""
  policy_type        = ""TargetTrackingScaling""
  resource_id        = aws_appautoscaling_target.honua.resource_id
  scalable_dimension = aws_appautoscaling_target.honua.scalable_dimension
  service_namespace  = aws_appautoscaling_target.honua.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = ""ECSServiceAverageMemoryUtilization""
    }
    target_value = 80.0
  }
}

# Data source for current AWS account
data ""aws_caller_identity"" ""current"" {}
");

        tf.AppendLine(@"
# Security Group for ECS Service
resource ""aws_security_group"" ""ecs_service"" {
  name        = ""honua-ecs-sg-${var.environment}""
  description = ""Security group for Honua ECS service""
  vpc_id      = aws_vpc.honua.id

  ingress {
    from_port       = 8080
    to_port         = 8080
    protocol        = ""tcp""
    security_groups = [aws_security_group.alb.id]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = ""-1""
    cidr_blocks = [""0.0.0.0/0""]
  }

  tags = {
    Name        = ""honua-ecs-sg-${var.environment}""
    Environment = var.environment
  }
}

# Data sources
data ""aws_availability_zones"" ""available"" {
  state = ""available""
}

# Outputs
output ""honua_server_url"" {
  value = ""http://${aws_lb.honua.dns_name}""
}");

        if (hasDatabase)
        {
            tf.AppendLine(@"
output ""database_endpoint"" {
  value = aws_db_instance.postgis.endpoint
}

output ""database_password"" {
  value     = random_password.db_password.result
  sensitive = true
}");
        }

        return tf.ToString();
    }


    public static string GenerateLambda(DeploymentAnalysis analysis)
    {
        var tf = new System.Text.StringBuilder();
        tf.AppendLine(@"terraform {
  required_providers {
    aws = {
      source  = ""hashicorp/aws""
      version = ""~> 5.0""
    }
  }
}

provider ""aws"" {
  region = var.aws_region
}

variable ""aws_region"" {
  description = ""AWS region""
  default     = ""us-east-1""
}

variable ""environment"" {
  description = ""Environment name""
  default     = """ + analysis.TargetEnvironment + @"""
}

# Lambda Execution Role
resource ""aws_iam_role"" ""lambda_exec"" {
  name = ""honua-lambda-exec-${var.environment}""

  assume_role_policy = jsonencode({
    Version = ""2012-10-17""
    Statement = [{
      Action = ""sts:AssumeRole""
      Effect = ""Allow""
      Principal = {
        Service = ""lambda.amazonaws.com""
      }
    }]
  })

  tags = {
    Name        = ""honua-lambda-exec-${var.environment}""
    Environment = var.environment
  }
}

# Lambda Policy for CloudWatch Logs
resource ""aws_iam_role_policy_attachment"" ""lambda_logs"" {
  role       = aws_iam_role.lambda_exec.name
  policy_arn = ""arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole""
}

# Lambda Policy for DynamoDB Access
resource ""aws_iam_role_policy"" ""lambda_dynamodb"" {
  name = ""honua-lambda-dynamodb-${var.environment}""
  role = aws_iam_role.lambda_exec.id

  policy = jsonencode({
    Version = ""2012-10-17""
    Statement = [{
      Effect = ""Allow""
      Action = [
        ""dynamodb:GetItem"",
        ""dynamodb:PutItem"",
        ""dynamodb:UpdateItem"",
        ""dynamodb:DeleteItem"",
        ""dynamodb:Query"",
        ""dynamodb:Scan""
      ]
      Resource = aws_dynamodb_table.honua.arn
    }]
  })
}

# Lambda Policy for S3 Access
resource ""aws_iam_role_policy"" ""lambda_s3"" {
  name = ""honua-lambda-s3-${var.environment}""
  role = aws_iam_role.lambda_exec.id

  policy = jsonencode({
    Version = ""2012-10-17""
    Statement = [{
      Effect = ""Allow""
      Action = [
        ""s3:GetObject"",
        ""s3:PutObject"",
        ""s3:DeleteObject"",
        ""s3:ListBucket""
      ]
      Resource = [
        aws_s3_bucket.honua_tiles.arn,
        ""${aws_s3_bucket.honua_tiles.arn}/*""
      ]
    }]
  })
}

# ECR Repository for Lambda Container Image
resource ""aws_ecr_repository"" ""honua_lambda"" {
  name                 = ""honua-lambda-${var.environment}""
  image_tag_mutability = ""MUTABLE""

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = {
    Name        = ""honua-lambda-${var.environment}""
    Environment = var.environment
  }
}

# Lambda Function with Container Image
resource ""aws_lambda_function"" ""honua"" {
  function_name = ""honua-server-${var.environment}""
  role          = aws_iam_role.lambda_exec.arn
  package_type  = ""Image""
  image_uri     = ""${aws_ecr_repository.honua_lambda.repository_url}:latest""
  timeout       = 30
  memory_size   = 2048

  environment {
    variables = {
      ASPNETCORE_ENVIRONMENT = var.environment
      HONUA__DATABASE__TABLE = aws_dynamodb_table.honua.name
      HONUA__STORAGE__BUCKET = aws_s3_bucket.honua_tiles.id
      HONUA__STORAGE__REGION = var.aws_region
    }
  }

  tags = {
    Name        = ""honua-server-${var.environment}""
    Environment = var.environment
  }

  depends_on = [
    aws_iam_role_policy_attachment.lambda_logs,
    aws_cloudwatch_log_group.lambda
  ]
}

# CloudWatch Log Group
resource ""aws_cloudwatch_log_group"" ""lambda"" {
  name              = ""/aws/lambda/honua-server-${var.environment}""
  retention_in_days = 14

  tags = {
    Name        = ""honua-lambda-logs-${var.environment}""
    Environment = var.environment
  }
}

# API Gateway HTTP API
resource ""aws_apigatewayv2_api"" ""honua"" {
  name          = ""honua-api-${var.environment}""
  protocol_type = ""HTTP""

  cors_configuration {
    allow_origins = [""*""]
    allow_methods = [""GET"", ""POST"", ""PUT"", ""DELETE"", ""OPTIONS""]
    allow_headers = [""*""]
    max_age_seconds = 3600
  }

  tags = {
    Name        = ""honua-api-${var.environment}""
    Environment = var.environment
  }
}

# API Gateway Integration
resource ""aws_apigatewayv2_integration"" ""honua"" {
  api_id           = aws_apigatewayv2_api.honua.id
  integration_type = ""AWS_PROXY""
  integration_uri  = aws_lambda_function.honua.invoke_arn
  payload_format_version = ""2.0""
}

# API Gateway Routes
resource ""aws_apigatewayv2_route"" ""default"" {
  api_id    = aws_apigatewayv2_api.honua.id
  route_key = ""$default""
  target    = ""integrations/${aws_apigatewayv2_integration.honua.id}""
}

# API Gateway Stage
resource ""aws_apigatewayv2_stage"" ""default"" {
  api_id      = aws_apigatewayv2_api.honua.id
  name        = ""$default""
  auto_deploy = true

  access_log_settings {
    destination_arn = aws_cloudwatch_log_group.api_gateway.arn
    format = jsonencode({
      requestId      = ""$context.requestId""
      ip             = ""$context.identity.sourceIp""
      requestTime    = ""$context.requestTime""
      httpMethod     = ""$context.httpMethod""
      routeKey       = ""$context.routeKey""
      status         = ""$context.status""
      protocol       = ""$context.protocol""
      responseLength = ""$context.responseLength""
    })
  }

  tags = {
    Name        = ""honua-api-stage-${var.environment}""
    Environment = var.environment
  }
}

# CloudWatch Log Group for API Gateway
resource ""aws_cloudwatch_log_group"" ""api_gateway"" {
  name              = ""/aws/apigateway/honua-${var.environment}""
  retention_in_days = 14

  tags = {
    Name        = ""honua-api-gateway-logs-${var.environment}""
    Environment = var.environment
  }
}

# Lambda Permission for API Gateway
resource ""aws_lambda_permission"" ""api_gateway"" {
  statement_id  = ""AllowAPIGatewayInvoke""
  action        = ""lambda:InvokeFunction""
  function_name = aws_lambda_function.honua.function_name
  principal     = ""apigateway.amazonaws.com""
  source_arn    = ""${aws_apigatewayv2_api.honua.execution_arn}/*/*""
}

# DynamoDB Table for Geospatial Data
resource ""aws_dynamodb_table"" ""honua"" {
  name           = ""honua-geodata-${var.environment}""
  billing_mode   = ""PAY_PER_REQUEST""
  hash_key       = ""id""
  range_key      = ""timestamp""

  attribute {
    name = ""id""
    type = ""S""
  }

  attribute {
    name = ""timestamp""
    type = ""N""
  }

  attribute {
    name = ""layer""
    type = ""S""
  }

  global_secondary_index {
    name            = ""LayerIndex""
    hash_key        = ""layer""
    range_key       = ""timestamp""
    projection_type = ""ALL""
  }

  ttl {
    enabled        = true
    attribute_name = ""ttl""
  }

  point_in_time_recovery {
    enabled = var.environment == ""production"" ? true : false
  }

  server_side_encryption {
    enabled = true
  }

  tags = {
    Name        = ""honua-geodata-${var.environment}""
    Environment = var.environment
  }
}

# S3 Bucket for Tile Storage
resource ""aws_s3_bucket"" ""honua_tiles"" {
  bucket = ""honua-tiles-${var.environment}-${data.aws_caller_identity.current.account_id}""

  tags = {
    Name        = ""honua-tiles-${var.environment}""
    Environment = var.environment
  }
}

# S3 Bucket Versioning
resource ""aws_s3_bucket_versioning"" ""honua_tiles"" {
  bucket = aws_s3_bucket.honua_tiles.id

  versioning_configuration {
    status = var.environment == ""production"" ? ""Enabled"" : ""Suspended""
  }
}

# S3 Bucket Server-Side Encryption
resource ""aws_s3_bucket_server_side_encryption_configuration"" ""honua_tiles"" {
  bucket = aws_s3_bucket.honua_tiles.id

  rule {
    apply_server_side_encryption_by_default {
      sse_algorithm = ""AES256""
    }
  }
}

# S3 Bucket Lifecycle Rule
resource ""aws_s3_bucket_lifecycle_configuration"" ""honua_tiles"" {
  bucket = aws_s3_bucket.honua_tiles.id

  rule {
    id     = ""expire-old-tiles""
    status = ""Enabled""

    expiration {
      days = 90
    }

    transition {
      days          = 30
      storage_class = ""STANDARD_IA""
    }

    transition {
      days          = 60
      storage_class = ""GLACIER""
    }
  }
}

# S3 Bucket Public Access Block
resource ""aws_s3_bucket_public_access_block"" ""honua_tiles"" {
  bucket = aws_s3_bucket.honua_tiles.id

  block_public_acls       = false
  block_public_policy     = false
  ignore_public_acls      = false
  restrict_public_buckets = false
}

# S3 Bucket CORS Configuration
resource ""aws_s3_bucket_cors_configuration"" ""honua_tiles"" {
  bucket = aws_s3_bucket.honua_tiles.id

  cors_rule {
    allowed_headers = [""*""]
    allowed_methods = [""GET"", ""HEAD""]
    allowed_origins = [""*""]
    max_age_seconds = 3600
  }
}

# Data Source for Account ID
data ""aws_caller_identity"" ""current"" {}

# Outputs
output ""api_endpoint"" {
  description = ""API Gateway endpoint URL""
  value       = aws_apigatewayv2_api.honua.api_endpoint
}

output ""lambda_function_name"" {
  description = ""Lambda function name""
  value       = aws_lambda_function.honua.function_name
}

output ""dynamodb_table_name"" {
  description = ""DynamoDB table name""
  value       = aws_dynamodb_table.honua.name
}

output ""s3_bucket_name"" {
  description = ""S3 bucket name for tiles""
  value       = aws_s3_bucket.honua_tiles.id
}

output ""ecr_repository_url"" {
  description = ""ECR repository URL for Lambda container image""
  value       = aws_ecr_repository.honua_lambda.repository_url
}

output ""cloudwatch_log_group"" {
  description = ""CloudWatch log group for Lambda""
  value       = aws_cloudwatch_log_group.lambda.name
}");

        return tf.ToString();
    }

}
