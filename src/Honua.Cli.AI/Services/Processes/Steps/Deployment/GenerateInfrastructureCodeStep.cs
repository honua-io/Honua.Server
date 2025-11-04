// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Honua.Cli.AI.Services.Guardrails;
using Honua.Cli.AI.Services.Processes.State;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using DeploymentState = Honua.Cli.AI.Services.Processes.State.DeploymentState;

namespace Honua.Cli.AI.Services.Processes.Steps.Deployment;

/// <summary>
/// Generates Terraform infrastructure code for the target cloud provider.
/// </summary>
public class GenerateInfrastructureCodeStep : KernelProcessStep<DeploymentState>, IProcessStepTimeout
{
    private const int DatabasePasswordLength = 32;
    private readonly ILogger<GenerateInfrastructureCodeStep> _logger;
    private DeploymentState _state = new();

    /// <summary>
    /// Code generation should be fast, but allow time for LLM-based generation if needed.
    /// Default timeout: 5 minutes
    /// </summary>
    public TimeSpan DefaultTimeout => TimeSpan.FromMinutes(5);

    public GenerateInfrastructureCodeStep(ILogger<GenerateInfrastructureCodeStep> logger)
    {
        _logger = logger;
    }

    public override ValueTask ActivateAsync(KernelProcessStepState<DeploymentState> state)
    {
        _state = state.State ?? new DeploymentState();
        return ValueTask.CompletedTask;
    }

    [KernelFunction("GenerateInfrastructure")]
    public async Task GenerateInfrastructureAsync(KernelProcessStepContext context)
    {
        _logger.LogInformation("Generating infrastructure code for {Provider} deployment {DeploymentId}",
            _state.CloudProvider, _state.DeploymentId);

        _state.Status = "GeneratingInfrastructure";

        try
        {
            await ProcessStepRetryHelper.ExecuteWithRetryAsync(
                async () =>
                {
                    var envelope = _state.GuardrailDecision?.Envelope
                        ?? throw new InvalidOperationException("Guardrail decision missing; run validation before code generation.");

                    _logger.LogInformation(
                        "Applying guardrail envelope {EnvelopeId} ({Profile})",
                        envelope.Id,
                        envelope.WorkloadProfile);

                    // Generate Terraform code based on cloud provider
                    var terraformCode = _state.CloudProvider.ToLower() switch
                    {
                        "aws" => GenerateAwsTerraform(envelope),
                        "azure" => GenerateAzureTerraform(envelope),
                        "gcp" => GenerateGcpTerraform(envelope),
                        _ => throw new InvalidOperationException($"Unsupported provider: {_state.CloudProvider}")
                    };

                    _state.InfrastructureCode = terraformCode;
                    _state.EstimatedMonthlyCost = CalculateEstimatedCost(_state.CloudProvider, _state.Tier);

                    // Write Terraform files to secure temporary directory with restricted permissions
                    var workspacePath = CreateSecureTempDirectory(_state.DeploymentId);
                    _state.TerraformWorkspacePath = workspacePath;

                    try
                    {
                        // Write main.tf
                        var mainTfPath = Path.Combine(workspacePath, "main.tf");
                        await File.WriteAllTextAsync(mainTfPath, terraformCode);
                        _logger.LogInformation("Wrote main.tf to {Path}", mainTfPath);

                        // Write variables.tf with required variables
                        var variablesTf = GenerateVariablesTf(_state.CloudProvider);
                        var variablesTfPath = Path.Combine(workspacePath, "variables.tf");
                        await File.WriteAllTextAsync(variablesTfPath, variablesTf);
                        _logger.LogInformation("Wrote variables.tf to {Path}", variablesTfPath);

                        // Generate secure passwords and configuration
                        var tfvars = GenerateTfVars(_state.CloudProvider);
                        if (!string.IsNullOrEmpty(tfvars))
                        {
                            var tfvarsPath = Path.Combine(workspacePath, "terraform.tfvars");
                            await File.WriteAllTextAsync(tfvarsPath, tfvars);
                            // Set restrictive permissions on tfvars file containing sensitive data
                            SetRestrictiveFilePermissions(tfvarsPath);
                            _logger.LogInformation("Wrote terraform.tfvars to {Path} with secure permissions", tfvarsPath);
                        }
                    }
                    catch
                    {
                        // Clean up secure temp directory on failure
                        CleanupSecureTempDirectory(workspacePath);
                        throw;
                    }

                    _logger.LogInformation("Generated infrastructure code for deployment {DeploymentId}. Estimated cost: ${Cost}/month",
                        _state.DeploymentId, _state.EstimatedMonthlyCost);
                    _logger.LogInformation("Terraform workspace: {WorkspacePath}", workspacePath);

                    await context.EmitEventAsync(new KernelProcessEvent
                    {
                        Id = "InfrastructureGenerated",
                        Data = _state
                    });
                },
                _logger,
                "GenerateInfrastructureCode");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate infrastructure code after retries for {DeploymentId}", _state.DeploymentId);
            _state.Status = "InfrastructureGenerationFailed";

            // Clean up workspace on failure
            if (!string.IsNullOrEmpty(_state.TerraformWorkspacePath) && Directory.Exists(_state.TerraformWorkspacePath))
            {
                CleanupSecureTempDirectory(_state.TerraformWorkspacePath);
            }

            await context.EmitEventAsync(new KernelProcessEvent
            {
                Id = "InfrastructureGenerationFailed",
                Data = new { _state.DeploymentId, Error = ex.Message }
            });
        }
    }

    private string GenerateAwsTerraform(ResourceEnvelope envelope)
    {
        var (taskCpu, taskMemory) = GetFargateTaskShape(envelope);
        var minTasks = Math.Max(envelope.MinInstances, 1);
        var maxTasks = Math.Max(minTasks * 4, 10);
        var ephemeralGb = Math.Max((int)Math.Ceiling(envelope.MinEphemeralGb), 20);
        var sanitizedName = SanitizeName(_state.DeploymentName);

        return $@"
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

    private string GenerateAzureTerraform(ResourceEnvelope envelope)
    {
        var planSku = envelope.MinVCpu >= 4 ? "EP3" : "EP1";
        var preWarmed = envelope.MinProvisionedConcurrency ?? Math.Max(1, envelope.MinInstances);
        var sanitizedName = SanitizeName(_state.DeploymentName);
        var storageAccountName = SanitizeStorageAccountName(_state.DeploymentName);

        return $@"
terraform {{
  required_providers {{
    azurerm = {{
      source  = ""hashicorp/azurerm""
      version = ""~> 3.0""
    }}
  }}
}}

provider ""azurerm"" {{
  features {{}}
}}

# Guardrail envelope {envelope.Id} ({envelope.WorkloadProfile})
locals {{
  honua_guardrail_envelope   = ""{envelope.Id}""
  honua_guardrail_min_vcpu    = {envelope.MinVCpu}
  honua_guardrail_min_memory  = {envelope.MinMemoryGb}
  honua_guardrail_min_workers = {envelope.MinProvisionedConcurrency ?? Math.Max(1, envelope.MinInstances)}
  honua_pre_warmed_instances  = {preWarmed}
  sanitized_name              = ""{sanitizedName}""
  storage_account_name        = ""{storageAccountName}""
}}

check ""honua_guardrail_concurrency"" {{
  assert {{
    condition     = local.honua_pre_warmed_instances >= local.honua_guardrail_min_workers
    error_message = ""Provisioned concurrency must be >= ${{local.honua_guardrail_min_workers}} to satisfy envelope ${{local.honua_guardrail_envelope}}""
  }}
}}

resource ""azurerm_resource_group"" ""honua"" {{
  name     = ""${{local.sanitized_name}}-rg""
  location = ""{_state.Region}""
}}

# Storage Account for Functions and rasters
resource ""azurerm_storage_account"" ""honua_functions"" {{
  name                     = ""${{local.storage_account_name}}fn""
  resource_group_name      = azurerm_resource_group.honua.name
  location                 = azurerm_resource_group.honua.location
  account_tier             = ""Standard""
  account_replication_type = ""LRS""

  tags = {{
    purpose = ""functions""
  }}
}}

resource ""azurerm_storage_account"" ""honua_rasters"" {{
  name                     = ""${{local.storage_account_name}}data""
  resource_group_name      = azurerm_resource_group.honua.name
  location                 = azurerm_resource_group.honua.location
  account_tier             = ""Standard""
  account_replication_type = ""LRS""

  blob_properties {{
    cors_rule {{
      allowed_headers    = [""*""]
      allowed_methods    = [""GET"", ""HEAD""]
      allowed_origins    = var.cors_allowed_origins
      exposed_headers    = [""ETag""]
      max_age_in_seconds = 3600
    }}
  }}

  tags = {{
    purpose = ""rasters""
  }}
}}

# PostgreSQL Database with credentials
resource ""azurerm_postgresql_server"" ""honua_db"" {{
  name                = ""${{local.sanitized_name}}-db""
  location            = azurerm_resource_group.honua.location
  resource_group_name = azurerm_resource_group.honua.name
  sku_name            = ""{GetAzureSkuName(_state.Tier)}""
  version             = ""11""

  administrator_login          = var.db_admin_login
  administrator_login_password = var.db_admin_password

  ssl_enforcement_enabled          = true
  ssl_minimal_tls_version_enforced = ""TLS1_2""

  backup_retention_days        = {(_state.Tier.ToLower() == "production" ? "7" : "1")}
  geo_redundant_backup_enabled = {(_state.Tier.ToLower() == "production" ? "true" : "false")}
  auto_grow_enabled            = true

  tags = {{
    Name = ""${{local.sanitized_name}}-db""
  }}
}}

resource ""azurerm_postgresql_database"" ""honua"" {{
  name                = ""honua""
  resource_group_name = azurerm_resource_group.honua.name
  server_name         = azurerm_postgresql_server.honua_db.name
  charset             = ""UTF8""
  collation           = ""English_United States.1252""
}}

resource ""azurerm_postgresql_firewall_rule"" ""allow_azure_services"" {{
  name                = ""allow-azure-services""
  resource_group_name = azurerm_resource_group.honua.name
  server_name         = azurerm_postgresql_server.honua_db.name
  start_ip_address    = ""0.0.0.0""
  end_ip_address      = ""0.0.0.0""
}}

# App Service Plan
resource ""azurerm_service_plan"" ""honua_plan"" {{
  name                = ""${{local.sanitized_name}}-plan""
  resource_group_name = azurerm_resource_group.honua.name
  location            = azurerm_resource_group.honua.location
  os_type             = ""Linux""
  sku_name            = ""{planSku}""

  tags = {{
    Name = ""${{local.sanitized_name}}-plan""
  }}
}}

# Linux Web App for Honua API
resource ""azurerm_linux_web_app"" ""honua"" {{
  name                = ""${{local.sanitized_name}}-api""
  resource_group_name = azurerm_resource_group.honua.name
  location            = azurerm_resource_group.honua.location
  service_plan_id     = azurerm_service_plan.honua_plan.id

  site_config {{
    always_on = {(_state.Tier.ToLower() == "production" ? "true" : "false")}

    application_stack {{
      docker_registry_url      = ""https://index.docker.io""
      docker_registry_username = var.docker_registry_username
      docker_registry_password = var.docker_registry_password
      docker_image_name        = ""honua/api""
      docker_image_tag         = var.app_version != ""latest"" ? var.app_version : ""v1.0.0""
    }}

    health_check_path = ""/health""

    cors {{
      allowed_origins = var.cors_allowed_origins
    }}
  }}

  app_settings = {{
    ""DATABASE_HOST""     = azurerm_postgresql_server.honua_db.fqdn
    ""DATABASE_NAME""     = azurerm_postgresql_database.honua.name
    ""DATABASE_USER""     = ""${{var.db_admin_login}}@${{azurerm_postgresql_server.honua_db.name}}""
    ""DATABASE_PASSWORD"" = var.db_admin_password
    ""STORAGE_ACCOUNT""   = azurerm_storage_account.honua_rasters.name
    ""STORAGE_KEY""       = azurerm_storage_account.honua_rasters.primary_access_key
    ""AZURE_REGION""      = ""{_state.Region}""
  }}

  https_only = true

  tags = {{
    Name = ""${{local.sanitized_name}}-api""
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
    min_workers   = local.honua_guardrail_min_workers
  }}
}}

output ""app_url"" {{
  description = ""URL to access the Honua API""
  value       = ""https://${{azurerm_linux_web_app.honua.default_hostname}}""
}}

output ""database_fqdn"" {{
  description = ""PostgreSQL database FQDN""
  value       = azurerm_postgresql_server.honua_db.fqdn
  sensitive   = true
}}

output ""storage_account_name"" {{
  description = ""Storage account name for raster storage""
  value       = azurerm_storage_account.honua_rasters.name
}}
";
    }

    private string GenerateGcpTerraform(ResourceEnvelope envelope)
    {
        var sanitizedName = SanitizeName(_state.DeploymentName);
        var minInstances = Math.Max(envelope.MinInstances, 1);
        var maxInstances = Math.Max(minInstances * 4, 10);
        var cpuLimit = envelope.MinVCpu >= 4 ? "4" : envelope.MinVCpu >= 2 ? "2" : "1";
        var memoryLimit = envelope.MinMemoryGb >= 16 ? "16Gi" : envelope.MinMemoryGb >= 4 ? "4Gi" : "2Gi";

        return $@"
terraform {{
  required_providers {{
    google = {{
      source  = ""hashicorp/google""
      version = ""~> 5.0""
    }}
  }}
}}

provider ""google"" {{
  project = var.project_id
  region  = ""{_state.Region}""
}}

# Guardrail envelope {envelope.Id} ({envelope.WorkloadProfile})
locals {{
  honua_guardrail_envelope      = ""{envelope.Id}""
  honua_guardrail_min_vcpu      = {envelope.MinVCpu}
  honua_guardrail_min_memory    = {envelope.MinMemoryGb}
  honua_guardrail_min_instances = {envelope.MinInstances}
  sanitized_name                = ""{sanitizedName}""
  min_instances                 = {minInstances}
  max_instances                 = {maxInstances}
}}

check ""honua_guardrail_instances"" {{
  assert {{
    condition     = local.honua_guardrail_min_instances >= 1
    error_message = ""Guardrail min instances must be >= 1 for envelope ${{local.honua_guardrail_envelope}}""
  }}
}}

# VPC Network for private Cloud SQL access
resource ""google_compute_network"" ""honua_vpc"" {{
  name                    = ""${{local.sanitized_name}}-vpc""
  auto_create_subnetworks = true
}}

# Reserve IP range for VPC peering with Cloud SQL
resource ""google_compute_global_address"" ""private_ip_address"" {{
  name          = ""${{local.sanitized_name}}-private-ip""
  purpose       = ""VPC_PEERING""
  address_type  = ""INTERNAL""
  prefix_length = 16
  network       = google_compute_network.honua_vpc.id
}}

# Create VPC peering connection for Cloud SQL
resource ""google_service_networking_connection"" ""private_vpc_connection"" {{
  network                 = google_compute_network.honua_vpc.id
  service                 = ""servicenetworking.googleapis.com""
  reserved_peering_ranges = [google_compute_global_address.private_ip_address.name]
}}

# Cloud SQL Database with root credentials
resource ""google_sql_database_instance"" ""honua_db"" {{
  name             = ""${{local.sanitized_name}}-db""
  database_version = ""POSTGRES_16""
  region           = ""{_state.Region}""

  settings {{
    tier              = ""{GetGcpTier(_state.Tier)}""
    availability_type = ""{(_state.Tier.ToLower() == "production" ? "REGIONAL" : "ZONAL")}""
    disk_size         = {GetStorageSize(_state.Tier)}
    disk_type         = ""PD_SSD""

    backup_configuration {{
      enabled            = true
      point_in_time_recovery_enabled = {(_state.Tier.ToLower() == "production" ? "true" : "false")}
      start_time         = ""02:00""
      backup_retention_settings {{
        retained_backups = {(_state.Tier.ToLower() == "production" ? "7" : "1")}
      }}
    }}

    ip_configuration {{
      # Disable public IPv4 access for security
      # Cloud Run will connect via Cloud SQL Proxy or Private IP
      ipv4_enabled = false

      # Enable private IP for VPC connectivity
      private_network = google_compute_network.honua_vpc.id

      # Only allow authorized networks in non-production for development access
      # Production should use Cloud SQL Proxy or Private Service Connect
      dynamic ""authorized_networks"" {{
        for_each = {(_state.Tier.ToLower() == "production" ? "[]" : "[{ name = \"dev-access\", value = var.dev_authorized_network }]")}
        content {{
          name  = authorized_networks.value.name
          value = authorized_networks.value.value
        }}
      }}
    }}
  }}

  deletion_protection = {(_state.Tier.ToLower() == "production" ? "true" : "false")}

  # Ensure VPC peering is established before creating the instance
  depends_on = [google_service_networking_connection.private_vpc_connection]
}}

resource ""google_sql_user"" ""honua_root"" {{
  name     = ""honua_admin""
  instance = google_sql_database_instance.honua_db.name
  password = var.db_root_password
}}

resource ""google_sql_database"" ""honua"" {{
  name     = ""honua""
  instance = google_sql_database_instance.honua_db.name
}}

# Storage Bucket with sanitized name
resource ""google_storage_bucket"" ""honua_rasters"" {{
  name     = ""${{local.sanitized_name}}-rasters-${{var.project_id}}""
  location = ""{_state.Region}""

  uniform_bucket_level_access = true

  cors {{
    origin          = var.cors_allowed_origins
    method          = [""GET"", ""HEAD""]
    response_header = [""Content-Type""]
    max_age_seconds = 3600
  }}

  labels = {{
    purpose = ""rasters""
  }}
}}

# Secret Manager for Database Password
resource ""google_secret_manager_secret"" ""db_password"" {{
  secret_id = ""${{local.sanitized_name}}-db-password""

  replication {{
    auto {{}}
  }}
}}

resource ""google_secret_manager_secret_version"" ""db_password"" {{
  secret      = google_secret_manager_secret.db_password.id
  secret_data = var.db_root_password
}}

# Grant Cloud Run service account access to the secret
resource ""google_secret_manager_secret_iam_member"" ""cloud_run_secret_accessor"" {{
  secret_id = google_secret_manager_secret.db_password.id
  role      = ""roles/secretmanager.secretAccessor""
  member    = ""serviceAccount:${{google_service_account.cloud_run.email}}""
}}

# Service account for Cloud Run
resource ""google_service_account"" ""cloud_run"" {{
  account_id   = ""${{local.sanitized_name}}-cloud-run""
  display_name = ""Cloud Run Service Account""
}}

# Serverless VPC Access Connector for Cloud Run
resource ""google_vpc_access_connector"" ""honua_connector"" {{
  name          = ""${{local.sanitized_name}}-connector""
  region        = ""{_state.Region}""
  network       = google_compute_network.honua_vpc.name
  ip_cidr_range = ""10.8.0.0/28""

  machine_type   = ""e2-micro""
  min_instances  = 2
  max_instances  = 3
}}

# Cloud Run Service
resource ""google_cloud_run_v2_service"" ""honua"" {{
  name     = ""${{local.sanitized_name}}-api""
  location = ""{_state.Region}""

  template {{
    scaling {{
      min_instance_count = local.min_instances
      max_instance_count = local.max_instances
    }}

    service_account = google_service_account.cloud_run.email

    containers {{
      image = var.app_version != ""latest"" ? ""gcr.io/honua-public/api:${{var.app_version}}"" : ""gcr.io/honua-public/api:v1.0.0""

      ports {{
        container_port = 8080
      }}

      resources {{
        limits = {{
          cpu    = ""{cpuLimit}""
          memory = ""{memoryLimit}""
        }}
      }}

      env {{
        name  = ""DATABASE_HOST""
        value = google_sql_database_instance.honua_db.public_ip_address
      }}

      env {{
        name  = ""DATABASE_NAME""
        value = ""honua""
      }}

      env {{
        name  = ""DATABASE_USER""
        value = ""honua_admin""
      }}

      env {{
        name = ""DATABASE_PASSWORD""
        value_source {{
          secret_key_ref {{
            secret  = google_secret_manager_secret.db_password.secret_id
            version = ""latest""
          }}
        }}
      }}

      env {{
        name  = ""GCS_BUCKET""
        value = google_storage_bucket.honua_rasters.name
      }}

      env {{
        name  = ""GCP_PROJECT_ID""
        value = var.project_id
      }}

      env {{
        name  = ""GCP_REGION""
        value = ""{_state.Region}""
      }}

      startup_probe {{
        http_get {{
          path = ""/health""
          port = 8080
        }}
        initial_delay_seconds = 10
        timeout_seconds       = 3
        period_seconds        = 10
        failure_threshold     = 3
      }}

      liveness_probe {{
        http_get {{
          path = ""/health""
          port = 8080
        }}
        initial_delay_seconds = 30
        timeout_seconds       = 3
        period_seconds        = 30
      }}
    }}

    vpc_access {{
      connector = google_vpc_access_connector.honua_connector.id
      egress    = ""PRIVATE_RANGES_ONLY""
    }}
  }}

  traffic {{
    type    = ""TRAFFIC_TARGET_ALLOCATION_TYPE_LATEST""
    percent = 100
  }}
}}

# Allow public access to Cloud Run service
resource ""google_cloud_run_service_iam_member"" ""public_access"" {{
  service  = google_cloud_run_v2_service.honua.name
  location = google_cloud_run_v2_service.honua.location
  role     = ""roles/run.invoker""
  member   = ""allUsers""
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

output ""service_url"" {{
  description = ""URL to access the Honua API""
  value       = google_cloud_run_v2_service.honua.uri
}}

output ""database_ip"" {{
  description = ""PostgreSQL database IP address""
  value       = google_sql_database_instance.honua_db.public_ip_address
  sensitive   = true
}}

output ""bucket_name"" {{
  description = ""Storage bucket name for raster storage""
  value       = google_storage_bucket.honua_rasters.name
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

    private string GetAzureSkuName(string tier) => tier.ToLower() switch
    {
        "development" => "B_Gen5_1",
        "staging" => "GP_Gen5_2",
        "production" => "MO_Gen5_4",
        _ => "B_Gen5_1"
    };

    private string GetGcpTier(string tier) => tier.ToLower() switch
    {
        "development" => "db-f1-micro",
        "staging" => "db-n1-standard-1",
        "production" => "db-n1-standard-4",
        _ => "db-f1-micro"
    };

    private int GetStorageSize(string tier) => tier.ToLower() switch
    {
        "development" => 20,
        "staging" => 100,
        "production" => 500,
        _ => 20
    };

    private decimal CalculateEstimatedCost(string provider, string tier)
    {
        // Simple cost estimation (placeholder logic)
        return (provider.ToLower(), tier.ToLower()) switch
        {
            ("aws", "development") => 45.00m,  // Increased due to NAT Gateway
            ("aws", "staging") => 200.00m,
            ("aws", "production") => 1000.00m,
            ("azure", "development") => 35.00m,
            ("azure", "staging") => 190.00m,
            ("azure", "production") => 950.00m,
            ("gcp", "development") => 32.00m,
            ("gcp", "staging") => 180.00m,
            ("gcp", "production") => 900.00m,
            _ => 0.00m
        };
    }

    private string GenerateVariablesTf(string provider)
    {
        return provider.ToLower() switch
        {
            "aws" => @"variable ""db_password"" {
  description = ""Database administrator password""
  type        = string
  sensitive   = true
}

variable ""app_version"" {
  description = ""Application container image version/tag to deploy""
  type        = string
  default     = ""latest""
}

variable ""reuse_existing_network"" {
  description = ""Set to true when reusing an existing virtual network""
  type        = bool
  default     = false
}

variable ""existing_vnet_id"" {
  description = ""Resource ID of the existing virtual network""
  type        = string
  default     = """"
}

variable ""reuse_existing_database"" {
  description = ""Set to true when reusing an existing Azure Database for PostgreSQL""
  type        = bool
  default     = false
}

variable ""existing_sql_server_id"" {
  description = ""Resource ID of the existing Azure Database for PostgreSQL server""
  type        = string
  default     = """"
}

variable ""reuse_existing_dns"" {
  description = ""Set to true when reusing an existing Azure DNS zone""
  type        = bool
  default     = false
}

variable ""existing_dns_zone_id"" {
  description = ""Resource ID of the existing Azure DNS zone""
  type        = string
  default     = """"
}

variable ""cors_allowed_origins"" {
  description = ""CORS allowed origins for API access (avoid wildcard in production)""
  type        = list(string)
  default     = []
}

variable ""reuse_existing_network"" {
  description = ""Set to true when reusing an existing VPC network""
  type        = bool
  default     = false
}

variable ""existing_network_self_link"" {
  description = ""Self link of the existing VPC network""
  type        = string
  default     = """"
}

variable ""reuse_existing_database"" {
  description = ""Set to true when reusing an existing Cloud SQL instance""
  type        = bool
  default     = false
}

variable ""existing_sql_instance"" {
  description = ""Name of the existing Cloud SQL instance""
  type        = string
  default     = """"
}

variable ""reuse_existing_dns"" {
  description = ""Set to true when reusing an existing Cloud DNS managed zone""
  type        = bool
  default     = false
}

variable ""existing_dns_zone"" {
  description = ""Name of the existing Cloud DNS managed zone""
  type        = string
  default     = """"
}

variable ""reuse_existing_network"" {
  description = ""Set to true when reusing an existing VPC""
  type        = bool
  default     = false
}

variable ""existing_vpc_id"" {
  description = ""Identifier of the existing VPC when reuse_existing_network is true""
  type        = string
  default     = """"
}

variable ""existing_subnet_ids"" {
  description = ""List of subnet IDs to reuse when reuse_existing_network is true""
  type        = list(string)
  default     = []
}

variable ""reuse_existing_database"" {
  description = ""Set to true when reusing an existing RDS instance""
  type        = bool
  default     = false
}

variable ""existing_database_identifier"" {
  description = ""Identifier of the existing database instance""
  type        = string
  default     = """"
}

variable ""existing_database_endpoint"" {
  description = ""Endpoint of the existing database instance""
  type        = string
  default     = """"
}

variable ""reuse_existing_dns"" {
  description = ""Set to true when reusing an existing Route53 hosted zone""
  type        = bool
  default     = false
}

variable ""existing_dns_zone_id"" {
  description = ""Route53 hosted zone ID to reuse""
  type        = string
  default     = """"
}

variable ""existing_dns_zone_name"" {
  description = ""Route53 hosted zone name to reuse""
  type        = string
  default     = """"
}
",
            "gcp" => @"variable ""project_id"" {
  description = ""GCP project ID""
  type        = string
}

variable ""db_root_password"" {
  description = ""Database root password""
  type        = string
  sensitive   = true
}

variable ""app_version"" {
  description = ""Application container image version/tag to deploy""
  type        = string
  default     = ""latest""
}

variable ""dev_authorized_network"" {
  description = ""CIDR range for development access to Cloud SQL (only used in non-production)""
  type        = string
  default     = ""0.0.0.0/0""
}

variable ""cors_allowed_origins"" {
  description = ""CORS allowed origins for API access (avoid wildcard in production)""
  type        = list(string)
  default     = []
}
",
            "azure" => @"variable ""db_admin_login"" {
  description = ""Database administrator login username""
  type        = string
}

variable ""db_admin_password"" {
  description = ""Database administrator password""
  type        = string
  sensitive   = true
}

variable ""cors_allowed_origins"" {
  description = ""CORS allowed origins for API access (avoid wildcard in production)""
  type        = list(string)
  default     = []
}

variable ""docker_registry_username"" {
  description = ""Docker registry username for pulling container images""
  type        = string
  sensitive   = true
}

variable ""docker_registry_password"" {
  description = ""Docker registry password for pulling container images""
  type        = string
  sensitive   = true
}

variable ""app_version"" {
  description = ""Application container image version/tag to deploy""
  type        = string
  default     = ""latest""
}
",
            _ => ""
        };
    }

    private string GenerateTfVars(string provider)
    {
        return provider.ToLower() switch
        {
            "aws" => GenerateAwsTfVars(),
            "gcp" => GenerateGcpTfVars(),
            "azure" => GenerateAzureTfVars(),
            _ => ""
        };
    }

    private string GenerateAwsTfVars()
    {
        var existing = _state.ExistingInfrastructure ?? ExistingInfrastructurePreference.Default;

        var dbPassword = GenerateSecureDatabasePassword();
        _state.InfrastructureOutputs ??= new Dictionary<string, string>();
        _state.InfrastructureOutputs["db_password"] = dbPassword;
        _logger.LogInformation("Generated secure database password and stored in deployment state");

        var corsOrigins = DeriveCorsOrigins();
        var sb = new StringBuilder();

        sb.AppendLine("# Securely generated database password");
        sb.AppendLine("# DO NOT commit this file to version control");
        sb.AppendLine($@"db_password = ""{dbPassword}""");
        sb.AppendLine();
        sb.AppendLine("# CORS allowed origins - derived from deployment configuration");
        sb.AppendLine("# In production, restrict to specific domains. For development, wildcard may be acceptable.");
        sb.AppendLine($@"cors_allowed_origins = {corsOrigins}");
        sb.AppendLine();
        sb.AppendLine("# Application version - pin to explicit version in production");
        sb.AppendLine("# Example: app_version = \"v1.2.3\"");
        sb.AppendLine(@"app_version = ""latest""");
        sb.AppendLine();
        sb.AppendLine("# Existing infrastructure preferences");
        sb.AppendLine($@"reuse_existing_network = {ToTfBool(existing.ReuseNetwork)}");
        sb.AppendLine($@"reuse_existing_database = {ToTfBool(existing.ReuseDatabase)}");
        sb.AppendLine($@"reuse_existing_dns = {ToTfBool(existing.ReuseDns)}");

        if (existing.ReuseNetwork)
        {
            var vpcId = GetInfrastructureOutput("existing_network_id") ?? existing.ExistingNetworkId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(vpcId))
            {
                sb.AppendLine($@"existing_vpc_id = ""{vpcId}""");
            }

            var subnetIds = GetInfrastructureOutput("existing_subnet_ids");
            if (!string.IsNullOrWhiteSpace(subnetIds))
            {
                var formatted = string.Join(", ",
                    subnetIds.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Select(id => $"\"{id}\""));
                sb.AppendLine($@"existing_subnet_ids = [{formatted}]");
            }

            if (!string.IsNullOrWhiteSpace(existing.NetworkNotes))
            {
                sb.AppendLine($@"network_notes = ""{existing.NetworkNotes}""");
            }
        }

        if (existing.ReuseDatabase)
        {
            var databaseId = GetInfrastructureOutput("existing_database_id") ?? existing.ExistingDatabaseId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(databaseId))
            {
                sb.AppendLine($@"existing_database_identifier = ""{databaseId}""");
            }

            var endpoint = GetInfrastructureOutput("database_endpoint");
            if (!string.IsNullOrWhiteSpace(endpoint))
            {
                sb.AppendLine($@"existing_database_endpoint = ""{endpoint}""");
            }

            if (!string.IsNullOrWhiteSpace(existing.DatabaseNotes))
            {
                sb.AppendLine($@"database_notes = ""{existing.DatabaseNotes}""");
            }
        }

        if (existing.ReuseDns)
        {
            var dnsZoneId = GetInfrastructureOutput("existing_dns_zone_id") ?? existing.ExistingDnsZoneId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(dnsZoneId))
            {
                sb.AppendLine($@"existing_dns_zone_id = ""{dnsZoneId}""");
            }

            var dnsZoneName = GetInfrastructureOutput("existing_dns_zone_name");
            if (!string.IsNullOrWhiteSpace(dnsZoneName))
            {
                sb.AppendLine($@"existing_dns_zone_name = ""{dnsZoneName}""");
            }

            if (!string.IsNullOrWhiteSpace(existing.DnsNotes))
            {
                sb.AppendLine($@"dns_notes = ""{existing.DnsNotes}""");
            }
        }

        return sb.ToString();
    }

    private string GenerateGcpTfVars()
    {
        var gcpProjectId = _state.GcpProjectId
            ?? Environment.GetEnvironmentVariable("GCP_PROJECT_ID")
            ?? Environment.GetEnvironmentVariable("GOOGLE_CLOUD_PROJECT");

        if (string.IsNullOrEmpty(gcpProjectId))
        {
            _logger.LogError("GCP project ID not configured. Set GcpProjectId in deployment state or GCP_PROJECT_ID/GOOGLE_CLOUD_PROJECT environment variable");
            throw new InvalidOperationException(
                "GCP project ID is required for GCP deployments. " +
                "Configure GcpProjectId in deployment state or set GCP_PROJECT_ID/GOOGLE_CLOUD_PROJECT environment variable.");
        }

        var dbPassword = GenerateSecureDatabasePassword();
        _state.InfrastructureOutputs ??= new Dictionary<string, string>();
        _state.InfrastructureOutputs["db_root_password"] = dbPassword;
        _logger.LogInformation("Using GCP project ID: {ProjectId}", gcpProjectId);

        var corsOrigins = DeriveCorsOrigins();
        var existing = _state.ExistingInfrastructure ?? ExistingInfrastructurePreference.Default;

        var sb = new StringBuilder();
        sb.AppendLine("# GCP project ID from configuration");
        sb.AppendLine($@"project_id = ""{gcpProjectId}""");
        sb.AppendLine();
        sb.AppendLine("# Securely generated database password");
        sb.AppendLine("# DO NOT commit this file to version control");
        sb.AppendLine($@"db_root_password = ""{dbPassword}""");
        sb.AppendLine();
        sb.AppendLine("# CORS allowed origins - derived from deployment configuration");
        sb.AppendLine("# In production, restrict to specific domains. For development, wildcard may be acceptable.");
        sb.AppendLine($@"cors_allowed_origins = {corsOrigins}");
        sb.AppendLine();
        sb.AppendLine("# Application version - pin to explicit version in production");
        sb.AppendLine("# Example: app_version = \"v1.2.3\"");
        sb.AppendLine(@"app_version = ""latest""");
        sb.AppendLine();
        sb.AppendLine("# Existing infrastructure preferences");
        sb.AppendLine($@"reuse_existing_network = {ToTfBool(existing.ReuseNetwork)}");
        sb.AppendLine($@"reuse_existing_database = {ToTfBool(existing.ReuseDatabase)}");
        sb.AppendLine($@"reuse_existing_dns = {ToTfBool(existing.ReuseDns)}");

        if (existing.ReuseNetwork)
        {
            var networkId = GetInfrastructureOutput("existing_network_id") ?? existing.ExistingNetworkId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(networkId))
            {
                sb.AppendLine($@"existing_network_self_link = ""{networkId}""");
            }
        }

        if (existing.ReuseDatabase)
        {
            var dbId = GetInfrastructureOutput("existing_database_id") ?? existing.ExistingDatabaseId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(dbId))
            {
                sb.AppendLine($@"existing_sql_instance = ""{dbId}""");
            }
        }

        if (existing.ReuseDns)
        {
            var zoneId = GetInfrastructureOutput("existing_dns_zone_id") ?? existing.ExistingDnsZoneId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(zoneId))
            {
                sb.AppendLine($@"existing_dns_zone = ""{zoneId}""");
            }
        }

        return sb.ToString();
    }

    private string GenerateAzureTfVars()
    {
        var dbPassword = GenerateSecureDatabasePassword();
        _state.InfrastructureOutputs ??= new Dictionary<string, string>();
        _state.InfrastructureOutputs["db_admin_password"] = dbPassword;
        _logger.LogInformation("Generated secure database credentials");

        var corsOrigins = DeriveCorsOrigins();
        var existing = _state.ExistingInfrastructure ?? ExistingInfrastructurePreference.Default;

        var sb = new StringBuilder();
        sb.AppendLine("# Database administrator credentials");
        sb.AppendLine("# DO NOT commit this file to version control");
        sb.AppendLine(@"db_admin_login = ""honuaadmin""");
        sb.AppendLine($@"db_admin_password = ""{dbPassword}""");
        sb.AppendLine();
        sb.AppendLine("# CORS allowed origins - derived from deployment configuration");
        sb.AppendLine("# In production, restrict to specific domains. For development, wildcard may be acceptable.");
        sb.AppendLine($@"cors_allowed_origins = {corsOrigins}");
        sb.AppendLine();
        sb.AppendLine("# Docker registry credentials for pulling container images");
        sb.AppendLine("# Replace with your actual registry credentials");
        sb.AppendLine(@"docker_registry_username = ""SET_YOUR_DOCKER_USERNAME""");
        sb.AppendLine(@"docker_registry_password = ""SET_YOUR_DOCKER_PASSWORD""");
        sb.AppendLine();
        sb.AppendLine("# Application version - pin to explicit version in production");
        sb.AppendLine("# Example: app_version = \"v1.2.3\"");
        sb.AppendLine(@"app_version = ""latest""");
        sb.AppendLine();
        sb.AppendLine("# Existing infrastructure preferences");
        sb.AppendLine($@"reuse_existing_network = {ToTfBool(existing.ReuseNetwork)}");
        sb.AppendLine($@"reuse_existing_database = {ToTfBool(existing.ReuseDatabase)}");
        sb.AppendLine($@"reuse_existing_dns = {ToTfBool(existing.ReuseDns)}");

        if (existing.ReuseNetwork)
        {
            var vnetId = GetInfrastructureOutput("existing_network_id") ?? existing.ExistingNetworkId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(vnetId))
            {
                sb.AppendLine($@"existing_vnet_id = ""{vnetId}""");
            }
        }

        if (existing.ReuseDatabase)
        {
            var serverId = GetInfrastructureOutput("existing_database_id") ?? existing.ExistingDatabaseId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(serverId))
            {
                sb.AppendLine($@"existing_sql_server_id = ""{serverId}""");
            }
        }

        if (existing.ReuseDns)
        {
            var zoneId = GetInfrastructureOutput("existing_dns_zone_id") ?? existing.ExistingDnsZoneId ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(zoneId))
            {
                sb.AppendLine($@"existing_dns_zone_id = ""{zoneId}""");
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Derives CORS allowed origins from deployment configuration.
    /// Returns a JSON array of origins based on the tier and configured domains.
    /// </summary>
    private string DeriveCorsOrigins()
    {
        // For production, never allow wildcard - require explicit configuration
        if (_state.Tier.ToLower() == "production")
        {
            _logger.LogWarning("Production deployment requires explicit CORS origins. Update terraform.tfvars after generation with your allowed domains.");
            _logger.LogInformation("Example: cors_allowed_origins = [\"https://yourdomain.com\", \"https://app.yourdomain.com\"]");
            // Return empty array to force manual configuration
            return "[]";
        }

        // For development/staging, allow wildcard but log a warning
        _logger.LogInformation("Non-production deployment: allowing CORS wildcard. Configure specific domains for production.");
        return "[\"*\"]";
    }

    private string? GetInfrastructureOutput(string key)
    {
        if (_state.InfrastructureOutputs is null)
        {
            return null;
        }

        return _state.InfrastructureOutputs.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static string ToTfBool(bool value) => value ? "true" : "false";

    /// <summary>
    /// Sanitizes deployment name for use in resource identifiers.
    /// Converts to lowercase, removes special characters, and ensures length limits.
    /// </summary>
    private static string SanitizeName(string name, int maxLength = 32)
    {
        if (string.IsNullOrEmpty(name))
        {
            name = "honua";
        }

        // Convert to lowercase
        var sanitized = name.ToLowerInvariant();

        // Replace invalid characters with hyphens
        var sb = new StringBuilder();
        foreach (var c in sanitized)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(c);
            }
            else if (c == '-' || c == '_')
            {
                sb.Append('-');
            }
        }

        sanitized = sb.ToString();

        // Remove consecutive hyphens
        while (sanitized.Contains("--"))
        {
            sanitized = sanitized.Replace("--", "-");
        }

        // Trim hyphens from start and end
        sanitized = sanitized.Trim('-');

        // Ensure minimum length
        if (sanitized.Length == 0)
        {
            sanitized = "honua";
        }

        // Truncate to max length
        if (sanitized.Length > maxLength)
        {
            sanitized = sanitized.Substring(0, maxLength).TrimEnd('-');
        }

        return sanitized;
    }

    /// <summary>
    /// Sanitizes deployment name for Azure storage account names.
    /// Azure storage accounts must be 3-24 chars, lowercase alphanumeric only, globally unique.
    /// </summary>
    private static string SanitizeStorageAccountName(string name)
    {
        if (string.IsNullOrEmpty(name))
        {
            name = "honua";
        }

        // Convert to lowercase and remove all non-alphanumeric characters
        var sanitized = new string(name.ToLowerInvariant()
            .Where(c => char.IsLetterOrDigit(c))
            .ToArray());

        // Ensure minimum length
        if (sanitized.Length == 0)
        {
            sanitized = "honua";
        }

        // Truncate to 18 chars to leave room for suffixes (fn/data) and still be under 24 char limit
        if (sanitized.Length > 18)
        {
            sanitized = sanitized.Substring(0, 18);
        }

        // Ensure it's at least 3 chars (minimum for storage account)
        if (sanitized.Length < 3)
        {
            sanitized = sanitized.PadRight(3, 'x');
        }

        return sanitized;
    }

    /// <summary>
    /// Generates a cryptographically secure random password for database authentication.
    /// </summary>
    private static string GenerateSecureDatabasePassword()
    {
        Span<byte> buffer = stackalloc byte[DatabasePasswordLength];
        RandomNumberGenerator.Fill(buffer);

        // Use a safe alphabet that excludes ambiguous characters and includes required character types
        // Includes: uppercase, lowercase, digits, and special characters safe for shell/config files
        const string alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789!@#$%^&*-_+=";
        var builder = new StringBuilder(DatabasePasswordLength);
        foreach (var b in buffer)
        {
            builder.Append(alphabet[b % alphabet.Length]);
        }

        return builder.ToString();
    }

    /// <summary>
    /// Creates a secure temporary directory with restricted permissions.
    /// </summary>
    private string CreateSecureTempDirectory(string deploymentId)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), "honua-terraform", deploymentId);
        var dirInfo = Directory.CreateDirectory(tempPath);

        // On Unix systems, restrict directory permissions to owner only (700)
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(tempPath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                _logger.LogInformation("Set secure permissions (700) on directory {Path}", tempPath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set Unix file permissions on {Path}", tempPath);
            }
        }

        return tempPath;
    }

    /// <summary>
    /// Sets restrictive permissions on a file containing sensitive data.
    /// </summary>
    private void SetRestrictiveFilePermissions(string filePath)
    {
        // On Unix systems, restrict file permissions to owner read/write only (600)
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                File.SetUnixFileMode(filePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
                _logger.LogDebug("Set secure permissions (600) on file {Path}", filePath);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to set Unix file permissions on {Path}", filePath);
            }
        }
    }

    /// <summary>
    /// Securely cleans up temporary directory by overwriting sensitive files before deletion.
    /// </summary>
    private void CleanupSecureTempDirectory(string directoryPath)
    {
        try
        {
            if (!Directory.Exists(directoryPath))
                return;

            _logger.LogInformation("Cleaning up secure temporary directory: {Path}", directoryPath);

            // Overwrite sensitive files (tfvars) with random data before deletion
            var sensitiveFiles = Directory.GetFiles(directoryPath, "*.tfvars", SearchOption.AllDirectories);
            foreach (var file in sensitiveFiles)
            {
                try
                {
                    var fileInfo = new FileInfo(file);
                    if (fileInfo.Exists && fileInfo.Length > 0)
                    {
                        // Overwrite with random data
                        var randomData = new byte[fileInfo.Length];
                        RandomNumberGenerator.Fill(randomData);
                        File.WriteAllBytes(file, randomData);
                        _logger.LogDebug("Securely overwritten sensitive file: {File}", file);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to securely overwrite file {File}", file);
                }
            }

            // Delete the directory and all contents
            Directory.Delete(directoryPath, recursive: true);
            _logger.LogInformation("Cleaned up temporary directory: {Path}", directoryPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to clean up temporary directory {Path}", directoryPath);
        }
    }
}
