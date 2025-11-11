// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Honua.Cli.AI.Services.Guardrails;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// AWS-specific Terraform infrastructure code generation.
/// Contains methods for generating AWS resources including ECS, RDS, VPC, S3, etc.
/// </summary>
public partial class GenerateInfrastructureCodeStep
{
    private string GenerateAwsTerraform(ResourceEnvelope envelope, string? securityFeedback = null)
    {
        var (taskCpu, taskMemory) = GetFargateTaskShape(envelope);
        var minTasks = Math.Max(envelope.MinInstances, 1);
        var maxTasks = Math.Max(minTasks * 4, 10);
        var ephemeralGb = Math.Max((int)Math.Ceiling(envelope.MinEphemeralGb), 20);
        var sanitizedName = SanitizeName(_state.DeploymentName);

        // If security feedback provided, log it for debugging
        if (!string.IsNullOrEmpty(securityFeedback))
        {
            _logger.LogInformation("Regenerating AWS Terraform with security feedback:\n{Feedback}", securityFeedback);
        }

        var feedbackComment = string.IsNullOrEmpty(securityFeedback)
            ? ""
            : $@"
# SECURITY FEEDBACK FROM PREVIOUS ATTEMPT:
# {securityFeedback.Replace("\n", "\n# ")}
";

        return $@"{feedbackComment}
terraform {{
  required_providers {{
    aws = {{
      source  = ""hashicorp/aws""
      version = ""~> 5.0""
    }}
  }}
}}

provider ""aws"" {{
  region = ""{_state.Region}""
}}

# Guardrail envelope {envelope.Id} ({envelope.WorkloadProfile})
locals {{
  honua_guardrail_envelope = ""{envelope.Id}""
  honua_guardrail_min_vcpu = {envelope.MinVCpu}
  honua_guardrail_min_memory = {envelope.MinMemoryGb}
  honua_guardrail_min_instances = {envelope.MinInstances}
  honua_task_cpu           = {taskCpu}
  honua_task_memory        = {taskMemory}
  honua_min_tasks          = {minTasks}
  honua_max_tasks          = {maxTasks}
  honua_ephemeral_gb       = {ephemeralGb}
  sanitized_name           = ""{sanitizedName}""
}}

check ""honua_guardrail_cpu"" {{
  assert {{
    condition     = local.honua_task_cpu / 1024.0 >= local.honua_guardrail_min_vcpu
    error_message = ""Task CPU must satisfy guardrail envelope ${{local.honua_guardrail_envelope}}""
  }}
}}

check ""honua_guardrail_instances"" {{
  assert {{
    condition     = local.honua_min_tasks >= local.honua_guardrail_min_instances
    error_message = ""Service must run at least ${{local.honua_guardrail_min_instances}} tasks to satisfy envelope ${{local.honua_guardrail_envelope}}""
  }}
}}

# Data sources
data ""aws_availability_zones"" ""available"" {{
  state = ""available""
}}

data ""aws_caller_identity"" ""current"" {{}}

# VPC Configuration
resource ""aws_vpc"" ""honua"" {{
  cidr_block           = ""10.0.0.0/16""
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = {{
    Name = ""${{local.sanitized_name}}-vpc""
  }}
}}

resource ""aws_internet_gateway"" ""honua"" {{
  vpc_id = aws_vpc.honua.id

  tags = {{
    Name = ""${{local.sanitized_name}}-igw""
  }}
}}

# Public subnets for ALB and NAT gateways
resource ""aws_subnet"" ""honua_public_a"" {{
  vpc_id                  = aws_vpc.honua.id
  cidr_block              = ""10.0.1.0/24""
  availability_zone       = data.aws_availability_zones.available.names[0]
  map_public_ip_on_launch = true

  tags = {{
    Name = ""${{local.sanitized_name}}-public-a""
  }}
}}

resource ""aws_subnet"" ""honua_public_b"" {{
  vpc_id                  = aws_vpc.honua.id
  cidr_block              = ""10.0.2.0/24""
  availability_zone       = data.aws_availability_zones.available.names[1]
  map_public_ip_on_launch = true

  tags = {{
    Name = ""${{local.sanitized_name}}-public-b""
  }}
}}

# Private subnets for ECS tasks and databases
resource ""aws_subnet"" ""honua_private_a"" {{
  vpc_id            = aws_vpc.honua.id
  cidr_block        = ""10.0.11.0/24""
  availability_zone = data.aws_availability_zones.available.names[0]

  tags = {{
    Name = ""${{local.sanitized_name}}-private-a""
  }}
}}

resource ""aws_subnet"" ""honua_private_b"" {{
  vpc_id            = aws_vpc.honua.id
  cidr_block        = ""10.0.12.0/24""
  availability_zone = data.aws_availability_zones.available.names[1]

  tags = {{
    Name = ""${{local.sanitized_name}}-private-b""
  }}
}}

# NAT Gateway for private subnet internet access
resource ""aws_eip"" ""nat"" {{
  domain = ""vpc""

  tags = {{
    Name = ""${{local.sanitized_name}}-nat-eip""
  }}
}}

resource ""aws_nat_gateway"" ""honua"" {{
  allocation_id = aws_eip.nat.id
  subnet_id     = aws_subnet.honua_public_a.id

  tags = {{
    Name = ""${{local.sanitized_name}}-nat""
  }}

  depends_on = [aws_internet_gateway.honua]
}}

# Route tables
resource ""aws_route_table"" ""honua_public"" {{
  vpc_id = aws_vpc.honua.id

  route {{
    cidr_block = ""0.0.0.0/0""
    gateway_id = aws_internet_gateway.honua.id
  }}

  tags = {{
    Name = ""${{local.sanitized_name}}-public-rt""
  }}
}}

resource ""aws_route_table"" ""honua_private"" {{
  vpc_id = aws_vpc.honua.id

  route {{
    cidr_block     = ""0.0.0.0/0""
    nat_gateway_id = aws_nat_gateway.honua.id
  }}

  tags = {{
    Name = ""${{local.sanitized_name}}-private-rt""
  }}
}}

resource ""aws_route_table_association"" ""honua_public_a"" {{
  subnet_id      = aws_subnet.honua_public_a.id
  route_table_id = aws_route_table.honua_public.id
}}

resource ""aws_route_table_association"" ""honua_public_b"" {{
  subnet_id      = aws_subnet.honua_public_b.id
  route_table_id = aws_route_table.honua_public.id
}}

resource ""aws_route_table_association"" ""honua_private_a"" {{
  subnet_id      = aws_subnet.honua_private_a.id
  route_table_id = aws_route_table.honua_private.id
}}

resource ""aws_route_table_association"" ""honua_private_b"" {{
  subnet_id      = aws_subnet.honua_private_b.id
  route_table_id = aws_route_table.honua_private.id
}}

# Security Groups
resource ""aws_security_group"" ""honua_alb"" {{
  name        = ""${{local.sanitized_name}}-alb-sg""
  description = ""Security group for Honua ALB""
  vpc_id      = aws_vpc.honua.id

  ingress {{
    from_port   = 80
    to_port     = 80
    protocol    = ""tcp""
    cidr_blocks = [""0.0.0.0/0""]
    description = ""HTTP from anywhere""
  }}

  ingress {{
    from_port   = 443
    to_port     = 443
    protocol    = ""tcp""
    cidr_blocks = [""0.0.0.0/0""]
    description = ""HTTPS from anywhere""
  }}

  egress {{
    from_port   = 0
    to_port     = 0
    protocol    = ""-1""
    cidr_blocks = [""0.0.0.0/0""]
    description = ""All outbound traffic""
  }}

  tags = {{
    Name = ""${{local.sanitized_name}}-alb-sg""
  }}
}}

resource ""aws_security_group"" ""honua_ecs"" {{
  name        = ""${{local.sanitized_name}}-ecs-sg""
  description = ""Security group for Honua ECS tasks""
  vpc_id      = aws_vpc.honua.id

  ingress {{
    from_port       = 8080
    to_port         = 8080
    protocol        = ""tcp""
    security_groups = [aws_security_group.honua_alb.id]
    description     = ""HTTP from ALB""
  }}

  egress {{
    from_port   = 0
    to_port     = 0
    protocol    = ""-1""
    cidr_blocks = [""0.0.0.0/0""]
    description = ""All outbound traffic""
  }}

  tags = {{
    Name = ""${{local.sanitized_name}}-ecs-sg""
  }}
}}

resource ""aws_security_group"" ""honua_db"" {{
  name        = ""${{local.sanitized_name}}-db-sg""
  description = ""Security group for Honua database""
  vpc_id      = aws_vpc.honua.id

  ingress {{
    from_port       = 5432
    to_port         = 5432
    protocol        = ""tcp""
    security_groups = [aws_security_group.honua_ecs.id]
    description     = ""PostgreSQL from ECS tasks""
  }}

  egress {{
    from_port   = 0
    to_port     = 0
    protocol    = ""-1""
    cidr_blocks = [""0.0.0.0/0""]
    description = ""All outbound traffic""
  }}

  tags = {{
    Name = ""${{local.sanitized_name}}-db-sg""
  }}
}}

# DB Subnet Group
resource ""aws_db_subnet_group"" ""honua"" {{
  name       = ""${{local.sanitized_name}}-db-subnet-group""
  subnet_ids = [aws_subnet.honua_private_a.id, aws_subnet.honua_private_b.id]

  tags = {{
    Name = ""${{local.sanitized_name}}-db-subnet-group""
  }}
}}

# PostGIS Database
resource ""aws_db_instance"" ""honua_db"" {{
  identifier              = ""${{local.sanitized_name}}-db""
  engine                  = ""postgres""
  engine_version          = ""16.3""
  instance_class          = ""{GetAwsInstanceClass(_state.Tier)}""
  allocated_storage       = {GetStorageSize(_state.Tier)}
  db_name                 = ""honua""
  username                = ""honua_admin""
  password                = var.db_password
  vpc_security_group_ids  = [aws_security_group.honua_db.id]
  db_subnet_group_name    = aws_db_subnet_group.honua.name

  # Enable final snapshot for production to prevent data loss
  skip_final_snapshot       = {(_state.Tier.ToLower() == "production" ? "false" : "true")}
  final_snapshot_identifier = {(_state.Tier.ToLower() == "production" ? "\"${{local.sanitized_name}}-final-snapshot-${{formatdate(\"YYYY-MM-DD-hhmm\", timestamp())}}\"" : "null")}

  backup_retention_period = {(_state.Tier.ToLower() == "production" ? "7" : "1")}
  multi_az                = {(_state.Tier.ToLower() == "production" ? "true" : "false")}

  tags = {{
    Name = ""${{local.sanitized_name}}-db""
  }}
}}

# S3 Bucket for raster storage with unique name
resource ""aws_s3_bucket"" ""honua_rasters"" {{
  bucket = ""${{local.sanitized_name}}-rasters-${{data.aws_caller_identity.current.account_id}}""

  tags = {{
    Name = ""${{local.sanitized_name}}-rasters""
  }}
}}

resource ""aws_s3_bucket_cors_configuration"" ""honua_rasters"" {{
  bucket = aws_s3_bucket.honua_rasters.id

  cors_rule {{
    allowed_headers = [""*""]
    allowed_methods = [""GET"", ""HEAD""]
    allowed_origins = var.cors_allowed_origins
    expose_headers  = [""ETag""]
    max_age_seconds = 3600
  }}
}}

# CloudWatch Log Group
resource ""aws_cloudwatch_log_group"" ""honua"" {{
  name              = ""/ecs/${{local.sanitized_name}}""
  retention_in_days = {(_state.Tier.ToLower() == "production" ? "30" : "7")}

  tags = {{
    Name = ""${{local.sanitized_name}}-logs""
  }}
}}

# IAM Role for ECS Task Execution
resource ""aws_iam_role"" ""ecs_task_execution"" {{
  name = ""${{local.sanitized_name}}-ecs-task-execution-role""

  assume_role_policy = jsonencode({{
    Version = ""2012-10-17""
    Statement = [
      {{
        Action = ""sts:AssumeRole""
        Effect = ""Allow""
        Principal = {{
          Service = ""ecs-tasks.amazonaws.com""
        }}
      }}
    ]
  }})

  tags = {{
    Name = ""${{local.sanitized_name}}-ecs-task-execution-role""
  }}
}}

resource ""aws_iam_role_policy_attachment"" ""ecs_task_execution"" {{
  role       = aws_iam_role.ecs_task_execution.name
  policy_arn = ""arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy""
}}

# IAM Role for ECS Task
resource ""aws_iam_role"" ""ecs_task"" {{
  name = ""${{local.sanitized_name}}-ecs-task-role""

  assume_role_policy = jsonencode({{
    Version = ""2012-10-17""
    Statement = [
      {{
        Action = ""sts:AssumeRole""
        Effect = ""Allow""
        Principal = {{
          Service = ""ecs-tasks.amazonaws.com""
        }}
      }}
    ]
  }})

  tags = {{
    Name = ""${{local.sanitized_name}}-ecs-task-role""
  }}
}}

resource ""aws_iam_role_policy"" ""ecs_task_s3"" {{
  name = ""${{local.sanitized_name}}-ecs-task-s3-policy""
  role = aws_iam_role.ecs_task.id

  policy = jsonencode({{
    Version = ""2012-10-17""
    Statement = [
      {{
        Effect = ""Allow""
        Action = [
          ""s3:GetObject"",
          ""s3:PutObject"",
          ""s3:DeleteObject"",
          ""s3:ListBucket""
        ]
        Resource = [
          aws_s3_bucket.honua_rasters.arn,
          ""${{aws_s3_bucket.honua_rasters.arn}}/*""
        ]
      }}
    ]
  }})
}}

# Secrets Manager for DB Password
resource ""aws_secretsmanager_secret"" ""db_password"" {{
  name = ""${{local.sanitized_name}}-db-password""

  tags = {{
    Name = ""${{local.sanitized_name}}-db-password""
  }}
}}

resource ""aws_secretsmanager_secret_version"" ""db_password"" {{
  secret_id     = aws_secretsmanager_secret.db_password.id
  secret_string = var.db_password
}}

resource ""aws_iam_role_policy"" ""ecs_task_execution_secrets"" {{
  name = ""${{local.sanitized_name}}-ecs-task-execution-secrets-policy""
  role = aws_iam_role.ecs_task_execution.id

  policy = jsonencode({{
    Version = ""2012-10-17""
    Statement = [
      {{
        Effect = ""Allow""
        Action = [
          ""secretsmanager:GetSecretValue""
        ]
        Resource = [
          aws_secretsmanager_secret.db_password.arn
        ]
      }}
    ]
  }})
}}

# ECS Cluster
resource ""aws_ecs_cluster"" ""honua"" {{
  name = ""${{local.sanitized_name}}-cluster""

  tags = {{
    Name = ""${{local.sanitized_name}}-cluster""
  }}
}}

# ECS Task Definition
resource ""aws_ecs_task_definition"" ""honua"" {{
  family                   = ""${{local.sanitized_name}}-task""
  cpu                      = local.honua_task_cpu
  memory                   = local.honua_task_memory
  requires_compatibilities = [""FARGATE""]
  network_mode             = ""awsvpc""
  execution_role_arn       = aws_iam_role.ecs_task_execution.arn
  task_role_arn            = aws_iam_role.ecs_task.arn

  ephemeral_storage {{
    size_in_gib = local.honua_ephemeral_gb
  }}

  container_definitions = jsonencode([
    {{
      name      = ""honua-api""
      image     = var.app_version != ""latest"" ? ""public.ecr.aws/honua/api:${{var.app_version}}"" : ""public.ecr.aws/honua/api:v1.0.0""
      essential = true

      portMappings = [{{
        containerPort = 8080
        hostPort      = 8080
        protocol      = ""tcp""
      }}]

      environment = [
        {{
          name  = ""DATABASE_HOST""
          value = aws_db_instance.honua_db.endpoint
        }},
        {{
          name  = ""DATABASE_NAME""
          value = ""honua""
        }},
        {{
          name  = ""DATABASE_USER""
          value = ""honua_admin""
        }},
        {{
          name  = ""S3_BUCKET""
          value = aws_s3_bucket.honua_rasters.id
        }},
        {{
          name  = ""AWS_REGION""
          value = ""{_state.Region}""
        }}
      ]

      secrets = [
        {{
          name      = ""DATABASE_PASSWORD""
          valueFrom = aws_secretsmanager_secret.db_password.arn
        }}
      ]

      logConfiguration = {{
        logDriver = ""awslogs""
        options = {{
          ""awslogs-group""         = aws_cloudwatch_log_group.honua.name
          ""awslogs-region""        = ""{_state.Region}""
          ""awslogs-stream-prefix"" = ""ecs""
        }}
      }}

      healthCheck = {{
        command     = [""CMD-SHELL"", ""curl -f http://localhost:8080/health || exit 1""]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60
      }}
    }}
  ])

  tags = {{
    Name = ""${{local.sanitized_name}}-task""
  }}
}}

# Application Load Balancer
resource ""aws_lb"" ""honua"" {{
  name               = ""${{local.sanitized_name}}-alb""
  internal           = false
  load_balancer_type = ""application""
  security_groups    = [aws_security_group.honua_alb.id]
  subnets            = [aws_subnet.honua_public_a.id, aws_subnet.honua_public_b.id]

  enable_deletion_protection = {(_state.Tier.ToLower() == "production" ? "true" : "false")}

  tags = {{
    Name = ""${{local.sanitized_name}}-alb""
  }}
}}

resource ""aws_lb_target_group"" ""honua"" {{
  name        = ""${{local.sanitized_name}}-tg""
  port        = 8080
  protocol    = ""HTTP""
  vpc_id      = aws_vpc.honua.id
  target_type = ""ip""

  health_check {{
    enabled             = true
    healthy_threshold   = 2
    unhealthy_threshold = 3
    timeout             = 5
    interval            = 30
    path                = ""/health""
    matcher             = ""200""
  }}

  deregistration_delay = 30

  tags = {{
    Name = ""${{local.sanitized_name}}-tg""
  }}
}}

resource ""aws_lb_listener"" ""honua_http"" {{
  load_balancer_arn = aws_lb.honua.arn
  port              = ""80""
  protocol          = ""HTTP""

  default_action {{
    type             = ""forward""
    target_group_arn = aws_lb_target_group.honua.arn
  }}
}}

# ECS Service
resource ""aws_ecs_service"" ""honua"" {{
  name            = ""${{local.sanitized_name}}-service""
  cluster         = aws_ecs_cluster.honua.id
  task_definition = aws_ecs_task_definition.honua.arn
  desired_count   = local.honua_min_tasks
  launch_type     = ""FARGATE""

  network_configuration {{
    subnets          = [aws_subnet.honua_private_a.id, aws_subnet.honua_private_b.id]
    security_groups  = [aws_security_group.honua_ecs.id]
    assign_public_ip = false
  }}

  load_balancer {{
    target_group_arn = aws_lb_target_group.honua.arn
    container_name   = ""honua-api""
    container_port   = 8080
  }}

  depends_on = [
    aws_lb_listener.honua_http,
    aws_nat_gateway.honua
  ]

  tags = {{
    Name = ""${{local.sanitized_name}}-service""
  }}
}}

# Auto Scaling
resource ""aws_appautoscaling_target"" ""honua_ecs"" {{
  max_capacity       = local.honua_max_tasks
  min_capacity       = local.honua_min_tasks
  resource_id        = ""service/${{aws_ecs_cluster.honua.name}}/${{aws_ecs_service.honua.name}}""
  scalable_dimension = ""ecs:service:DesiredCount""
  service_namespace  = ""ecs""
}}

resource ""aws_appautoscaling_policy"" ""honua_ecs_cpu"" {{
  name               = ""${{local.sanitized_name}}-ecs-cpu-scaling""
  policy_type        = ""TargetTrackingScaling""
  resource_id        = aws_appautoscaling_target.honua_ecs.resource_id
  scalable_dimension = aws_appautoscaling_target.honua_ecs.scalable_dimension
  service_namespace  = aws_appautoscaling_target.honua_ecs.service_namespace

  target_tracking_scaling_policy_configuration {{
    predefined_metric_specification {{
      predefined_metric_type = ""ECSServiceAverageCPUUtilization""
    }}
    target_value = 70.0
  }}
}}

output ""honua_guardrail_envelope"" {{
  value = local.honua_guardrail_envelope
}}

output ""honua_guardrail_policy"" {{
  value = {{
    envelope_id   = local.honua_guardrail_envelope
    min_vcpu      = local.honua_guardrail_min_vcpu
    min_memory_gb = local.honua_guardrail_min_memory
    min_instances = local.honua_guardrail_min_instances
  }}
}}

output ""alb_dns_name"" {{
  description = ""DNS name of the Application Load Balancer""
  value       = aws_lb.honua.dns_name
}}

output ""load_balancer_endpoint"" {{
  description = ""Load balancer endpoint (alias for alb_dns_name)""
  value       = aws_lb.honua.dns_name
}}

output ""alb_url"" {{
  description = ""URL to access the Honua API""
  value       = ""http://${{aws_lb.honua.dns_name}}""
}}

output ""database_endpoint"" {{
  description = ""PostgreSQL database endpoint""
  value       = aws_db_instance.honua_db.endpoint
  sensitive   = true
}}

output ""database_password"" {{
  description = ""PostgreSQL database password""
  value       = var.db_password
  sensitive   = true
}}

output ""s3_bucket_name"" {{
  description = ""S3 bucket name for raster storage""
  value       = aws_s3_bucket.honua_rasters.id
}}
";
    }

    private static (int CpuUnits, int MemoryMb) GetFargateTaskShape(ResourceEnvelope envelope)
    {
        if (envelope.MinVCpu >= 4 || envelope.MinMemoryGb >= 16)
        {
            return (4096, 16384);
        }

        if (envelope.MinVCpu >= 2 || envelope.MinMemoryGb >= 4)
        {
            return (2048, 4096);
        }

        return (1024, 2048);
    }

    private string GetAwsInstanceClass(string tier) => tier.ToLower() switch
    {
        "development" => "db.t3.micro",
        "staging" => "db.t3.small",
        "production" => "db.r6g.large",
        _ => "db.t3.micro"
    };
}
