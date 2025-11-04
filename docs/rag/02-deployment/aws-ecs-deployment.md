# AWS ECS Deployment Guide

**Keywords**: aws, ecs, fargate, ec2, cloudformation, terraform, application-load-balancer, service-discovery, auto-scaling, rds, s3, secrets-manager
**Related**: docker-deployment, kubernetes-deployment, environment-variables, aws-infrastructure

## Overview

Amazon Elastic Container Service (ECS) provides a fully managed container orchestration platform for deploying Honua with deep AWS integration. ECS supports both EC2 and Fargate launch types, offering flexibility between infrastructure management and serverless containers.

**Key Features**:
- AWS Fargate for serverless container execution
- EC2 launch type for custom instance control
- Application Load Balancer (ALB) integration
- Auto Scaling based on metrics
- AWS Secrets Manager integration
- VPC networking and security groups
- RDS for managed PostGIS database
- S3 for tile cache and attachment storage
- CloudWatch logging and monitoring
- Service discovery with AWS Cloud Map

## Architecture Overview

```
Internet
   │
   ↓
Application Load Balancer (ALB)
   │
   ↓
┌─────────────────────────────────────┐
│  ECS Service (Honua Server)         │
│  ┌─────┐  ┌─────┐  ┌─────┐         │
│  │Task │  │Task │  │Task │         │
│  │ 1   │  │ 2   │  │ 3   │         │
│  └─────┘  └─────┘  └─────┘         │
└─────────────────────────────────────┘
   │                    │
   ↓                    ↓
RDS PostGIS          S3 Buckets
(Multi-AZ)           (Tiles, Attachments)
   │
   ↓
AWS Secrets Manager
(Database credentials)
```

## Quick Start with Fargate

### Prerequisites

```bash
# Install AWS CLI
pip install awscli

# Configure AWS credentials
aws configure

# Verify access
aws sts get-caller-identity
```

### 1. Create ECR Repository

```bash
# Create repository
aws ecr create-repository \
  --repository-name honua/server \
  --region us-east-1

# Authenticate Docker to ECR
aws ecr get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin \
  $(aws sts get-caller-identity --query Account --output text).dkr.ecr.us-east-1.amazonaws.com

# Build and push image
docker build -t honua:latest .
docker tag honua:latest \
  $(aws sts get-caller-identity --query Account --output text).dkr.ecr.us-east-1.amazonaws.com/honua/server:latest
docker push \
  $(aws sts get-caller-identity --query Account --output text).dkr.ecr.us-east-1.amazonaws.com/honua/server:latest
```

### 2. Create ECS Cluster

```bash
# Create Fargate cluster
aws ecs create-cluster \
  --cluster-name honua-production \
  --capacity-providers FARGATE FARGATE_SPOT \
  --default-capacity-provider-strategy \
    capacityProvider=FARGATE,weight=1,base=2 \
    capacityProvider=FARGATE_SPOT,weight=4 \
  --region us-east-1
```

### 3. Create Task Definition

```json
{
  "family": "honua-server",
  "networkMode": "awsvpc",
  "requiresCompatibilities": ["FARGATE"],
  "cpu": "1024",
  "memory": "2048",
  "executionRoleArn": "arn:aws:iam::ACCOUNT_ID:role/ecsTaskExecutionRole",
  "taskRoleArn": "arn:aws:iam::ACCOUNT_ID:role/honuaTaskRole",
  "containerDefinitions": [
    {
      "name": "honua-server",
      "image": "ACCOUNT_ID.dkr.ecr.us-east-1.amazonaws.com/honua/server:latest",
      "essential": true,
      "portMappings": [
        {
          "containerPort": 8080,
          "protocol": "tcp"
        }
      ],
      "environment": [
        {
          "name": "ASPNETCORE_ENVIRONMENT",
          "value": "Production"
        },
        {
          "name": "ASPNETCORE_URLS",
          "value": "http://+:8080"
        },
        {
          "name": "HONUA__METADATA__PROVIDER",
          "value": "database"
        },
        {
          "name": "HONUA__METADATA__PATH",
          "value": "/app/config"
        },
        {
          "name": "HONUA__AUTHENTICATION__MODE",
          "value": "OAuth"
        },
        {
          "name": "HONUA__AUTHENTICATION__ENFORCE",
          "value": "true"
        },
        {
          "name": "HONUA__ODATA__ENABLED",
          "value": "true"
        },
        {
          "name": "HONUA__SERVICES__RASTERTILES__PROVIDER",
          "value": "s3"
        },
        {
          "name": "HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME",
          "value": "honua-tiles-production"
        },
        {
          "name": "HONUA__SERVICES__RASTERTILES__S3__REGION",
          "value": "us-east-1"
        },
        {
          "name": "HONUA__ATTACHMENTS__PROVIDER",
          "value": "s3"
        },
        {
          "name": "HONUA__ATTACHMENTS__S3__BUCKETNAME",
          "value": "honua-attachments-production"
        },
        {
          "name": "HONUA__ATTACHMENTS__S3__REGION",
          "value": "us-east-1"
        }
      ],
      "secrets": [
        {
          "name": "HONUA__DATABASE__CONNECTIONSTRING",
          "valueFrom": "arn:aws:secretsmanager:us-east-1:ACCOUNT_ID:secret:honua/db/connection-string"
        }
      ],
      "logConfiguration": {
        "logDriver": "awslogs",
        "options": {
          "awslogs-group": "/ecs/honua-server",
          "awslogs-region": "us-east-1",
          "awslogs-stream-prefix": "honua"
        }
      },
      "healthCheck": {
        "command": [
          "CMD-SHELL",
          "curl -f http://localhost:8080/health || exit 1"
        ],
        "interval": 30,
        "timeout": 5,
        "retries": 3,
        "startPeriod": 60
      }
    }
  ]
}
```

Save as `task-definition.json` and register:

```bash
aws ecs register-task-definition \
  --cli-input-json file://task-definition.json \
  --region us-east-1
```

### 4. Create ECS Service

```bash
aws ecs create-service \
  --cluster honua-production \
  --service-name honua-server \
  --task-definition honua-server:1 \
  --desired-count 3 \
  --launch-type FARGATE \
  --platform-version LATEST \
  --network-configuration "awsvpcConfiguration={
    subnets=[subnet-abc123,subnet-def456],
    securityGroups=[sg-xyz789],
    assignPublicIp=DISABLED
  }" \
  --load-balancers "targetGroupArn=arn:aws:elasticloadbalancing:us-east-1:ACCOUNT_ID:targetgroup/honua-tg/abc123,containerName=honua-server,containerPort=8080" \
  --health-check-grace-period-seconds 60 \
  --deployment-configuration "maximumPercent=200,minimumHealthyPercent=100" \
  --region us-east-1
```

## CloudFormation Template

Complete infrastructure-as-code deployment:

```yaml
AWSTemplateFormatVersion: '2010-09-09'
Description: 'Honua ECS Deployment on Fargate'

Parameters:
  VpcId:
    Type: AWS::EC2::VPC::Id
    Description: VPC for ECS deployment

  PrivateSubnetIds:
    Type: List<AWS::EC2::Subnet::Id>
    Description: Private subnets for ECS tasks

  PublicSubnetIds:
    Type: List<AWS::EC2::Subnet::Id>
    Description: Public subnets for ALB

  ContainerImage:
    Type: String
    Description: ECR image URI
    Default: '123456789012.dkr.ecr.us-east-1.amazonaws.com/honua/server:latest'

  DesiredCount:
    Type: Number
    Description: Desired number of tasks
    Default: 3

  DatabaseEndpoint:
    Type: String
    Description: RDS PostGIS endpoint

  DatabaseSecretArn:
    Type: String
    Description: Secrets Manager ARN for database credentials

Resources:
  # ECS Cluster
  ECSCluster:
    Type: AWS::ECS::Cluster
    Properties:
      ClusterName: honua-production
      CapacityProviders:
        - FARGATE
        - FARGATE_SPOT
      DefaultCapacityProviderStrategy:
        - CapacityProvider: FARGATE
          Weight: 1
          Base: 2
        - CapacityProvider: FARGATE_SPOT
          Weight: 4
      ClusterSettings:
        - Name: containerInsights
          Value: enabled

  # CloudWatch Log Group
  LogGroup:
    Type: AWS::Logs::LogGroup
    Properties:
      LogGroupName: /ecs/honua-server
      RetentionInDays: 30

  # Security Group for ALB
  ALBSecurityGroup:
    Type: AWS::EC2::SecurityGroup
    Properties:
      GroupDescription: Security group for Honua ALB
      VpcId: !Ref VpcId
      SecurityGroupIngress:
        - IpProtocol: tcp
          FromPort: 443
          ToPort: 443
          CidrIp: 0.0.0.0/0
        - IpProtocol: tcp
          FromPort: 80
          ToPort: 80
          CidrIp: 0.0.0.0/0
      Tags:
        - Key: Name
          Value: honua-alb-sg

  # Security Group for ECS Tasks
  TaskSecurityGroup:
    Type: AWS::EC2::SecurityGroup
    Properties:
      GroupDescription: Security group for Honua ECS tasks
      VpcId: !Ref VpcId
      SecurityGroupIngress:
        - IpProtocol: tcp
          FromPort: 8080
          ToPort: 8080
          SourceSecurityGroupId: !Ref ALBSecurityGroup
      Tags:
        - Key: Name
          Value: honua-task-sg

  # Application Load Balancer
  ApplicationLoadBalancer:
    Type: AWS::ElasticLoadBalancingV2::LoadBalancer
    Properties:
      Name: honua-alb
      Type: application
      Scheme: internet-facing
      IpAddressType: ipv4
      Subnets: !Ref PublicSubnetIds
      SecurityGroups:
        - !Ref ALBSecurityGroup
      Tags:
        - Key: Name
          Value: honua-alb

  # Target Group
  TargetGroup:
    Type: AWS::ElasticLoadBalancingV2::TargetGroup
    Properties:
      Name: honua-tg
      Port: 8080
      Protocol: HTTP
      TargetType: ip
      VpcId: !Ref VpcId
      HealthCheckEnabled: true
      HealthCheckPath: /health
      HealthCheckProtocol: HTTP
      HealthCheckIntervalSeconds: 30
      HealthCheckTimeoutSeconds: 5
      HealthyThresholdCount: 2
      UnhealthyThresholdCount: 3
      Matcher:
        HttpCode: 200
      TargetGroupAttributes:
        - Key: deregistration_delay.timeout_seconds
          Value: 30
        - Key: stickiness.enabled
          Value: false

  # HTTPS Listener
  HTTPSListener:
    Type: AWS::ElasticLoadBalancingV2::Listener
    Properties:
      LoadBalancerArn: !Ref ApplicationLoadBalancer
      Port: 443
      Protocol: HTTPS
      Certificates:
        - CertificateArn: !Ref Certificate
      DefaultActions:
        - Type: forward
          TargetGroupArn: !Ref TargetGroup

  # HTTP Listener (redirect to HTTPS)
  HTTPListener:
    Type: AWS::ElasticLoadBalancingV2::Listener
    Properties:
      LoadBalancerArn: !Ref ApplicationLoadBalancer
      Port: 80
      Protocol: HTTP
      DefaultActions:
        - Type: redirect
          RedirectConfig:
            Protocol: HTTPS
            Port: 443
            StatusCode: HTTP_301

  # ACM Certificate
  Certificate:
    Type: AWS::CertificateManager::Certificate
    Properties:
      DomainName: honua.example.com
      ValidationMethod: DNS
      Tags:
        - Key: Name
          Value: honua-cert

  # Task Execution Role
  TaskExecutionRole:
    Type: AWS::IAM::Role
    Properties:
      RoleName: honuaTaskExecutionRole
      AssumeRolePolicyDocument:
        Version: '2012-10-17'
        Statement:
          - Effect: Allow
            Principal:
              Service: ecs-tasks.amazonaws.com
            Action: sts:AssumeRole
      ManagedPolicyArns:
        - arn:aws:iam::aws:policy/service-role/AmazonECSTaskExecutionRolePolicy
      Policies:
        - PolicyName: SecretsManagerAccess
          PolicyDocument:
            Version: '2012-10-17'
            Statement:
              - Effect: Allow
                Action:
                  - secretsmanager:GetSecretValue
                Resource: !Ref DatabaseSecretArn

  # Task Role (for application-level AWS API access)
  TaskRole:
    Type: AWS::IAM::Role
    Properties:
      RoleName: honuaTaskRole
      AssumeRolePolicyDocument:
        Version: '2012-10-17'
        Statement:
          - Effect: Allow
            Principal:
              Service: ecs-tasks.amazonaws.com
            Action: sts:AssumeRole
      Policies:
        - PolicyName: S3Access
          PolicyDocument:
            Version: '2012-10-17'
            Statement:
              - Effect: Allow
                Action:
                  - s3:GetObject
                  - s3:PutObject
                  - s3:DeleteObject
                  - s3:ListBucket
                Resource:
                  - !Sub '${TileBucket.Arn}/*'
                  - !GetAtt TileBucket.Arn
                  - !Sub '${AttachmentBucket.Arn}/*'
                  - !GetAtt AttachmentBucket.Arn

  # S3 Bucket for Tiles
  TileBucket:
    Type: AWS::S3::Bucket
    Properties:
      BucketName: !Sub 'honua-tiles-${AWS::AccountId}'
      BucketEncryption:
        ServerSideEncryptionConfiguration:
          - ServerSideEncryptionByDefault:
              SSEAlgorithm: AES256
      PublicAccessBlockConfiguration:
        BlockPublicAcls: true
        BlockPublicPolicy: true
        IgnorePublicAcls: true
        RestrictPublicBuckets: true
      LifecycleConfiguration:
        Rules:
          - Id: DeleteOldTiles
            Status: Enabled
            ExpirationInDays: 90

  # S3 Bucket for Attachments
  AttachmentBucket:
    Type: AWS::S3::Bucket
    Properties:
      BucketName: !Sub 'honua-attachments-${AWS::AccountId}'
      BucketEncryption:
        ServerSideEncryptionConfiguration:
          - ServerSideEncryptionByDefault:
              SSEAlgorithm: AES256
      PublicAccessBlockConfiguration:
        BlockPublicAcls: true
        BlockPublicPolicy: true
        IgnorePublicAcls: true
        RestrictPublicBuckets: true
      VersioningConfiguration:
        Status: Enabled

  # Task Definition
  TaskDefinition:
    Type: AWS::ECS::TaskDefinition
    Properties:
      Family: honua-server
      NetworkMode: awsvpc
      RequiresCompatibilities:
        - FARGATE
      Cpu: '1024'
      Memory: '2048'
      ExecutionRoleArn: !GetAtt TaskExecutionRole.Arn
      TaskRoleArn: !GetAtt TaskRole.Arn
      ContainerDefinitions:
        - Name: honua-server
          Image: !Ref ContainerImage
          Essential: true
          PortMappings:
            - ContainerPort: 8080
              Protocol: tcp
          Environment:
            - Name: ASPNETCORE_ENVIRONMENT
              Value: Production
            - Name: ASPNETCORE_URLS
              Value: http://+:8080
            - Name: HONUA__METADATA__PROVIDER
              Value: database
            - Name: HONUA__SERVICES__RASTERTILES__PROVIDER
              Value: s3
            - Name: HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME
              Value: !Ref TileBucket
            - Name: HONUA__SERVICES__RASTERTILES__S3__REGION
              Value: !Ref AWS::Region
            - Name: HONUA__ATTACHMENTS__PROVIDER
              Value: s3
            - Name: HONUA__ATTACHMENTS__S3__BUCKETNAME
              Value: !Ref AttachmentBucket
            - Name: HONUA__ATTACHMENTS__S3__REGION
              Value: !Ref AWS::Region
          Secrets:
            - Name: HONUA__DATABASE__CONNECTIONSTRING
              ValueFrom: !Ref DatabaseSecretArn
          LogConfiguration:
            LogDriver: awslogs
            Options:
              awslogs-group: !Ref LogGroup
              awslogs-region: !Ref AWS::Region
              awslogs-stream-prefix: honua
          HealthCheck:
            Command:
              - CMD-SHELL
              - curl -f http://localhost:8080/health || exit 1
            Interval: 30
            Timeout: 5
            Retries: 3
            StartPeriod: 60

  # ECS Service
  Service:
    Type: AWS::ECS::Service
    DependsOn:
      - HTTPSListener
    Properties:
      ServiceName: honua-server
      Cluster: !Ref ECSCluster
      TaskDefinition: !Ref TaskDefinition
      DesiredCount: !Ref DesiredCount
      LaunchType: FARGATE
      PlatformVersion: LATEST
      NetworkConfiguration:
        AwsvpcConfiguration:
          AssignPublicIp: DISABLED
          Subnets: !Ref PrivateSubnetIds
          SecurityGroups:
            - !Ref TaskSecurityGroup
      LoadBalancers:
        - ContainerName: honua-server
          ContainerPort: 8080
          TargetGroupArn: !Ref TargetGroup
      HealthCheckGracePeriodSeconds: 60
      DeploymentConfiguration:
        MaximumPercent: 200
        MinimumHealthyPercent: 100
        DeploymentCircuitBreaker:
          Enable: true
          Rollback: true

  # Auto Scaling Target
  ScalableTarget:
    Type: AWS::ApplicationAutoScaling::ScalableTarget
    Properties:
      ServiceNamespace: ecs
      ResourceId: !Sub 'service/${ECSCluster}/${Service.Name}'
      ScalableDimension: ecs:service:DesiredCount
      MinCapacity: 3
      MaxCapacity: 20
      RoleARN: !Sub 'arn:aws:iam::${AWS::AccountId}:role/aws-service-role/ecs.application-autoscaling.amazonaws.com/AWSServiceRoleForApplicationAutoScaling_ECSService'

  # CPU-based Auto Scaling Policy
  CPUScalingPolicy:
    Type: AWS::ApplicationAutoScaling::ScalingPolicy
    Properties:
      PolicyName: honua-cpu-scaling
      PolicyType: TargetTrackingScaling
      ScalingTargetId: !Ref ScalableTarget
      TargetTrackingScalingPolicyConfiguration:
        PredefinedMetricSpecification:
          PredefinedMetricType: ECSServiceAverageCPUUtilization
        TargetValue: 70.0
        ScaleInCooldown: 300
        ScaleOutCooldown: 60

  # Memory-based Auto Scaling Policy
  MemoryScalingPolicy:
    Type: AWS::ApplicationAutoScaling::ScalingPolicy
    Properties:
      PolicyName: honua-memory-scaling
      PolicyType: TargetTrackingScaling
      ScalingTargetId: !Ref ScalableTarget
      TargetTrackingScalingPolicyConfiguration:
        PredefinedMetricSpecification:
          PredefinedMetricType: ECSServiceAverageMemoryUtilization
        TargetValue: 80.0
        ScaleInCooldown: 300
        ScaleOutCooldown: 60

Outputs:
  LoadBalancerURL:
    Description: URL of the Application Load Balancer
    Value: !GetAtt ApplicationLoadBalancer.DNSName

  ServiceName:
    Description: ECS Service Name
    Value: !GetAtt Service.Name

  ClusterName:
    Description: ECS Cluster Name
    Value: !Ref ECSCluster

  TileBucketName:
    Description: S3 Bucket for Tiles
    Value: !Ref TileBucket

  AttachmentBucketName:
    Description: S3 Bucket for Attachments
    Value: !Ref AttachmentBucket
```

Deploy with:

```bash
aws cloudformation create-stack \
  --stack-name honua-production \
  --template-body file://honua-ecs.yaml \
  --parameters \
    ParameterKey=VpcId,ParameterValue=vpc-abc123 \
    ParameterKey=PrivateSubnetIds,ParameterValue=subnet-abc123\\,subnet-def456 \
    ParameterKey=PublicSubnetIds,ParameterValue=subnet-xyz789\\,subnet-uvw012 \
    ParameterKey=DatabaseEndpoint,ParameterValue=honua-db.abc123.us-east-1.rds.amazonaws.com \
    ParameterKey=DatabaseSecretArn,ParameterValue=arn:aws:secretsmanager:us-east-1:123456789012:secret:honua/db \
  --capabilities CAPABILITY_NAMED_IAM \
  --region us-east-1

# Wait for stack creation
aws cloudformation wait stack-create-complete \
  --stack-name honua-production \
  --region us-east-1
```

## Terraform Deployment

For infrastructure-as-code with Terraform:

```hcl
# main.tf

terraform {
  required_version = ">= 1.0"

  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
  }

  backend "s3" {
    bucket = "honua-terraform-state"
    key    = "ecs/production/terraform.tfstate"
    region = "us-east-1"
  }
}

provider "aws" {
  region = var.aws_region
}

# ECS Cluster
resource "aws_ecs_cluster" "honua" {
  name = "honua-production"

  setting {
    name  = "containerInsights"
    value = "enabled"
  }
}

resource "aws_ecs_cluster_capacity_providers" "honua" {
  cluster_name = aws_ecs_cluster.honua.name

  capacity_providers = ["FARGATE", "FARGATE_SPOT"]

  default_capacity_provider_strategy {
    base              = 2
    weight            = 1
    capacity_provider = "FARGATE"
  }

  default_capacity_provider_strategy {
    weight            = 4
    capacity_provider = "FARGATE_SPOT"
  }
}

# ECS Task Definition
resource "aws_ecs_task_definition" "honua" {
  family                   = "honua-server"
  network_mode             = "awsvpc"
  requires_compatibilities = ["FARGATE"]
  cpu                      = "1024"
  memory                   = "2048"
  execution_role_arn       = aws_iam_role.task_execution.arn
  task_role_arn            = aws_iam_role.task.arn

  container_definitions = jsonencode([
    {
      name      = "honua-server"
      image     = var.container_image
      essential = true

      portMappings = [
        {
          containerPort = 8080
          protocol      = "tcp"
        }
      ]

      environment = [
        {
          name  = "ASPNETCORE_ENVIRONMENT"
          value = "Production"
        },
        {
          name  = "HONUA__SERVICES__RASTERTILES__PROVIDER"
          value = "s3"
        },
        {
          name  = "HONUA__SERVICES__RASTERTILES__S3__BUCKETNAME"
          value = aws_s3_bucket.tiles.id
        }
      ]

      secrets = [
        {
          name      = "HONUA__DATABASE__CONNECTIONSTRING"
          valueFrom = aws_secretsmanager_secret.db_connection.arn
        }
      ]

      logConfiguration = {
        logDriver = "awslogs"
        options = {
          "awslogs-group"         = aws_cloudwatch_log_group.honua.name
          "awslogs-region"        = var.aws_region
          "awslogs-stream-prefix" = "honua"
        }
      }

      healthCheck = {
        command     = ["CMD-SHELL", "curl -f http://localhost:8080/health || exit 1"]
        interval    = 30
        timeout     = 5
        retries     = 3
        startPeriod = 60
      }
    }
  ])
}

# ECS Service
resource "aws_ecs_service" "honua" {
  name            = "honua-server"
  cluster         = aws_ecs_cluster.honua.id
  task_definition = aws_ecs_task_definition.honua.arn
  desired_count   = var.desired_count
  launch_type     = "FARGATE"
  platform_version = "LATEST"

  network_configuration {
    assign_public_ip = false
    subnets          = var.private_subnet_ids
    security_groups  = [aws_security_group.task.id]
  }

  load_balancer {
    target_group_arn = aws_lb_target_group.honua.arn
    container_name   = "honua-server"
    container_port   = 8080
  }

  health_check_grace_period_seconds = 60

  deployment_configuration {
    maximum_percent         = 200
    minimum_healthy_percent = 100

    deployment_circuit_breaker {
      enable   = true
      rollback = true
    }
  }

  depends_on = [
    aws_lb_listener.https
  ]
}

# Auto Scaling
resource "aws_appautoscaling_target" "honua" {
  max_capacity       = 20
  min_capacity       = 3
  resource_id        = "service/${aws_ecs_cluster.honua.name}/${aws_ecs_service.honua.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  service_namespace  = "ecs"
}

resource "aws_appautoscaling_policy" "cpu" {
  name               = "honua-cpu-scaling"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.honua.resource_id
  scalable_dimension = aws_appautoscaling_target.honua.scalable_dimension
  service_namespace  = aws_appautoscaling_target.honua.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
    target_value       = 70.0
    scale_in_cooldown  = 300
    scale_out_cooldown = 60
  }
}
```

Apply with:

```bash
terraform init
terraform plan -out=tfplan
terraform apply tfplan
```

## RDS PostGIS Setup

### Create RDS Instance

```bash
aws rds create-db-instance \
  --db-instance-identifier honua-postgis-prod \
  --db-instance-class db.r6g.xlarge \
  --engine postgres \
  --engine-version 16.1 \
  --master-username honua \
  --master-user-password <SECURE_PASSWORD> \
  --allocated-storage 100 \
  --storage-type gp3 \
  --storage-encrypted \
  --iops 3000 \
  --multi-az \
  --db-subnet-group-name honua-db-subnet-group \
  --vpc-security-group-ids sg-abc123 \
  --backup-retention-period 7 \
  --preferred-backup-window 03:00-04:00 \
  --preferred-maintenance-window sun:04:00-sun:05:00 \
  --enable-cloudwatch-logs-exports '["postgresql","upgrade"]' \
  --deletion-protection \
  --region us-east-1
```

### Install PostGIS Extension

```bash
# Connect to RDS
psql -h honua-postgis-prod.abc123.us-east-1.rds.amazonaws.com \
  -U honua \
  -d postgres

# Create extension
CREATE EXTENSION IF NOT EXISTS postgis;
CREATE EXTENSION IF NOT EXISTS postgis_topology;

# Verify
SELECT PostGIS_Version();
```

## Secrets Management

### Store Database Connection String

```bash
# Create secret
aws secretsmanager create-secret \
  --name honua/db/connection-string \
  --description "Honua database connection string" \
  --secret-string "Host=honua-postgis-prod.abc123.us-east-1.rds.amazonaws.com;Port=5432;Database=honuadb;Username=honua;Password=SecurePassword123!;Pooling=true;MinPoolSize=10;MaxPoolSize=100" \
  --region us-east-1

# Update secret
aws secretsmanager update-secret \
  --secret-id honua/db/connection-string \
  --secret-string "Host=...new-value..." \
  --region us-east-1
```

## Monitoring and Logging

### CloudWatch Logs Insights

Query ECS logs:

```sql
-- Find errors
fields @timestamp, @message
| filter @message like /ERROR/
| sort @timestamp desc
| limit 100

-- Request latency
fields @timestamp, request.duration
| stats avg(request.duration), max(request.duration), pct(@duration, 95)
```

### CloudWatch Alarms

```bash
# CPU utilization alarm
aws cloudwatch put-metric-alarm \
  --alarm-name honua-high-cpu \
  --alarm-description "Honua ECS service high CPU" \
  --metric-name CPUUtilization \
  --namespace AWS/ECS \
  --statistic Average \
  --period 300 \
  --evaluation-periods 2 \
  --threshold 80 \
  --comparison-operator GreaterThanThreshold \
  --dimensions Name=ServiceName,Value=honua-server Name=ClusterName,Value=honua-production \
  --alarm-actions arn:aws:sns:us-east-1:123456789012:honua-alerts
```

## CI/CD with AWS CodePipeline

```yaml
# buildspec.yml
version: 0.2

phases:
  pre_build:
    commands:
      - echo Logging in to Amazon ECR...
      - aws ecr get-login-password --region $AWS_REGION | docker login --username AWS --password-stdin $AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com
      - REPOSITORY_URI=$AWS_ACCOUNT_ID.dkr.ecr.$AWS_REGION.amazonaws.com/honua/server
      - IMAGE_TAG=${CODEBUILD_RESOLVED_SOURCE_VERSION:0:7}

  build:
    commands:
      - echo Build started on `date`
      - docker build -t $REPOSITORY_URI:latest .
      - docker tag $REPOSITORY_URI:latest $REPOSITORY_URI:$IMAGE_TAG

  post_build:
    commands:
      - echo Build completed on `date`
      - docker push $REPOSITORY_URI:latest
      - docker push $REPOSITORY_URI:$IMAGE_TAG
      - printf '[{"name":"honua-server","imageUri":"%s"}]' $REPOSITORY_URI:$IMAGE_TAG > imagedefinitions.json

artifacts:
  files:
    - imagedefinitions.json
```

## Troubleshooting

### Task Fails to Start

```bash
# View stopped tasks
aws ecs list-tasks \
  --cluster honua-production \
  --desired-status STOPPED \
  --service-name honua-server

# Describe stopped task
aws ecs describe-tasks \
  --cluster honua-production \
  --tasks <task-arn>

# Check logs
aws logs tail /ecs/honua-server --follow
```

### Database Connection Issues

```bash
# Test from ECS task
aws ecs execute-command \
  --cluster honua-production \
  --task <task-id> \
  --container honua-server \
  --command "/bin/sh" \
  --interactive

# Inside container
curl http://localhost:8080/health
```

## See Also

- [Docker Deployment](docker-deployment.md) - Container basics
- [Kubernetes Deployment](kubernetes-deployment.md) - K8s alternative
- [Environment Variables](../01-configuration/environment-variables.md) - Configuration reference
- [Monitoring and Observability](../04-operations/monitoring-observability.md) - Production monitoring
