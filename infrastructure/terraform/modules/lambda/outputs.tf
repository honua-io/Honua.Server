# Outputs for AWS Lambda + ALB Serverless Module

# ==================== Lambda Function ====================
output "lambda_function_name" {
  description = "Name of the Lambda function"
  value       = aws_lambda_function.honua.function_name
}

output "lambda_function_arn" {
  description = "ARN of the Lambda function"
  value       = aws_lambda_function.honua.arn
}

output "lambda_function_version" {
  description = "Latest version of the Lambda function"
  value       = aws_lambda_function.honua.version
}

output "lambda_function_url" {
  description = "Lambda Function URL (if enabled)"
  value       = var.create_function_url ? aws_lambda_function_url.honua[0].function_url : null
}

output "lambda_role_arn" {
  description = "ARN of the Lambda execution role"
  value       = aws_iam_role.lambda.arn
}

# ==================== Application Load Balancer ====================
output "alb_dns_name" {
  description = "DNS name of the Application Load Balancer"
  value       = var.create_alb ? aws_lb.honua[0].dns_name : null
}

output "alb_arn" {
  description = "ARN of the Application Load Balancer"
  value       = var.create_alb ? aws_lb.honua[0].arn : null
}

output "alb_zone_id" {
  description = "Zone ID of the Application Load Balancer"
  value       = var.create_alb ? aws_lb.honua[0].zone_id : null
}

output "alb_url" {
  description = "URL of the Application Load Balancer"
  value       = var.create_alb ? (var.ssl_certificate_arn != "" ? "https://${aws_lb.honua[0].dns_name}" : "http://${aws_lb.honua[0].dns_name}") : null
}

output "target_group_arn" {
  description = "ARN of the Lambda target group"
  value       = var.create_alb ? aws_lb_target_group.lambda[0].arn : null
}

# ==================== Database ====================
output "database_endpoint" {
  description = "RDS instance endpoint"
  value       = var.create_database ? aws_db_instance.honua[0].endpoint : null
}

output "database_address" {
  description = "RDS instance address"
  value       = var.create_database ? aws_db_instance.honua[0].address : null
  sensitive   = true
}

output "database_name" {
  description = "Name of the database"
  value       = var.create_database ? aws_db_instance.honua[0].db_name : null
}

output "database_username" {
  description = "Database username"
  value       = var.create_database ? var.db_username : null
  sensitive   = true
}

output "database_port" {
  description = "Database port"
  value       = var.create_database ? aws_db_instance.honua[0].port : null
}

# ==================== Secrets ====================
output "jwt_secret_arn" {
  description = "ARN of JWT secret in Secrets Manager"
  value       = aws_secretsmanager_secret.jwt_secret.arn
}

output "jwt_secret_name" {
  description = "Name of JWT secret in Secrets Manager"
  value       = aws_secretsmanager_secret.jwt_secret.name
}

output "db_connection_secret_arn" {
  description = "ARN of database connection secret in Secrets Manager"
  value       = var.create_database ? aws_secretsmanager_secret.db_connection[0].arn : null
}

output "db_connection_secret_name" {
  description = "Name of database connection secret in Secrets Manager"
  value       = var.create_database ? aws_secretsmanager_secret.db_connection[0].name : null
}

# ==================== VPC ====================
output "vpc_id" {
  description = "ID of the VPC"
  value       = var.create_vpc ? aws_vpc.honua[0].id : var.vpc_id
}

output "vpc_cidr" {
  description = "CIDR block of the VPC"
  value       = var.create_vpc ? aws_vpc.honua[0].cidr_block : null
}

output "public_subnet_ids" {
  description = "IDs of public subnets"
  value       = var.create_vpc ? aws_subnet.public[*].id : null
}

output "private_subnet_ids" {
  description = "IDs of private subnets"
  value       = var.create_vpc ? aws_subnet.private[*].id : null
}

output "nat_gateway_ip" {
  description = "Elastic IP of NAT Gateway"
  value       = var.create_vpc && var.enable_nat_gateway ? aws_eip.nat[0].public_ip : null
}

# ==================== Security Groups ====================
output "lambda_security_group_id" {
  description = "ID of Lambda security group"
  value       = var.create_database ? aws_security_group.lambda[0].id : null
}

output "rds_security_group_id" {
  description = "ID of RDS security group"
  value       = var.create_database ? aws_security_group.rds[0].id : null
}

output "alb_security_group_id" {
  description = "ID of ALB security group"
  value       = var.create_alb ? aws_security_group.alb[0].id : null
}

# ==================== ECR ====================
output "ecr_repository_url" {
  description = "URL of ECR repository"
  value       = var.create_ecr_repository ? aws_ecr_repository.honua[0].repository_url : null
}

output "ecr_repository_arn" {
  description = "ARN of ECR repository"
  value       = var.create_ecr_repository ? aws_ecr_repository.honua[0].arn : null
}

# ==================== CloudWatch ====================
output "cloudwatch_log_group_name" {
  description = "Name of CloudWatch log group"
  value       = aws_cloudwatch_log_group.lambda.name
}

output "cloudwatch_log_group_arn" {
  description = "ARN of CloudWatch log group"
  value       = aws_cloudwatch_log_group.lambda.arn
}

# ==================== DNS Configuration ====================
output "dns_records_required" {
  description = "DNS records required for custom domain"
  value = var.create_alb && var.ssl_certificate_arn != "" ? {
    type   = "CNAME"
    name   = "api.example.com"  # Replace with actual domain
    value  = aws_lb.honua[0].dns_name
    note   = "Point your custom domain to this ALB DNS name"
  } : null
}

# ==================== Cost Estimation ====================
output "estimated_monthly_cost" {
  description = "Estimated monthly cost breakdown (USD, approximate)"
  value = {
    lambda = {
      description = "Lambda function (highly variable based on usage)"
      compute     = "$0.0000166667 per GB-second"
      requests    = "$0.20 per 1M requests"
      free_tier   = "1M requests and 400,000 GB-seconds per month"
      note        = "Cost depends heavily on invocations and duration"
    }
    database = var.create_database ? {
      description = "RDS PostgreSQL ${var.db_instance_class}"
      instance    = var.db_instance_class == "db.t4g.small" ? "~$16/month" : "varies by instance type"
      storage     = "~$0.115/GB-month for gp3"
      multi_az    = var.db_multi_az ? "Doubles instance cost" : "Single AZ"
    } : null
    alb = var.create_alb ? {
      description = "Application Load Balancer"
      alb         = "~$16/month (base cost)"
      lcu         = "$0.008 per LCU-hour (~$6/month for low traffic)"
      total       = "~$22/month for low traffic"
    } : null
    nat_gateway = var.create_vpc && var.enable_nat_gateway ? {
      description = "NAT Gateway"
      gateway     = "$32.40/month (per AZ)"
      data        = "$0.045/GB processed"
      note        = "Can be expensive, consider VPC endpoints to reduce data transfer"
    } : null
    vpc_endpoints = {
      description = "VPC Endpoints (optional, reduces NAT costs)"
      interface   = "$7.20/month per endpoint"
      data        = "$0.01/GB processed"
      note        = "Consider for S3, Secrets Manager, CloudWatch"
    }
    secrets = {
      description = "AWS Secrets Manager"
      cost        = "$0.40 per secret per month + $0.05 per 10,000 API calls"
    }
    cloudwatch = {
      description = "CloudWatch Logs"
      ingestion   = "$0.50/GB"
      storage     = "$0.03/GB-month"
      note        = "First 5GB ingestion and 5GB storage free per month"
    }
    total_estimate_dev = var.create_database && var.create_alb ? "$70-100/month for low traffic dev" : "$30-50/month minimal config"
    total_estimate_prod = var.create_database && var.create_alb ? "$200-500/month for moderate traffic" : "$100-200/month"
    note = "Lambda costs scale with usage. High traffic can be $1000+/month"
  }
}

# ==================== Monitoring URLs ====================
output "monitoring_urls" {
  description = "URLs for monitoring and management"
  value = {
    lambda_console  = "https://console.aws.amazon.com/lambda/home?region=${var.aws_region}#/functions/${aws_lambda_function.honua.function_name}"
    rds_console     = var.create_database ? "https://console.aws.amazon.com/rds/home?region=${var.aws_region}#database:id=${aws_db_instance.honua[0].id}" : null
    alb_console     = var.create_alb ? "https://console.aws.amazon.com/ec2/v2/home?region=${var.aws_region}#LoadBalancers:search=${aws_lb.honua[0].name}" : null
    cloudwatch_logs = "https://console.aws.amazon.com/cloudwatch/home?region=${var.aws_region}#logsV2:log-groups/log-group/${replace(aws_cloudwatch_log_group.lambda.name, "/", "$252F")}"
  }
}

# ==================== Deployment Information ====================
output "deployment_info" {
  description = "Information for deployment and integration"
  value = {
    function_name    = aws_lambda_function.honua.function_name
    alb_dns          = var.create_alb ? aws_lb.honua[0].dns_name : null
    function_url     = var.create_function_url ? aws_lambda_function_url.honua[0].function_url : null
    endpoint         = var.create_alb ? (var.ssl_certificate_arn != "" ? "https://${aws_lb.honua[0].dns_name}" : "http://${aws_lb.honua[0].dns_name}") : (var.create_function_url ? aws_lambda_function_url.honua[0].function_url : "Lambda function only (no public endpoint)")
    health_check_url = var.create_alb ? "${var.ssl_certificate_arn != "" ? "https" : "http"}://${aws_lb.honua[0].dns_name}${var.health_check_path}" : null
    container_image  = var.container_image_uri
    database_enabled = var.create_database
    environment      = var.environment
  }
}

# ==================== Connection Strings ====================
output "connection_info" {
  description = "Connection information for applications"
  value = {
    database_connection_secret = var.create_database ? aws_secretsmanager_secret.db_connection[0].arn : null
    jwt_secret                 = aws_secretsmanager_secret.jwt_secret.arn
    retrieve_secrets_command   = "aws secretsmanager get-secret-value --secret-id ${aws_secretsmanager_secret.jwt_secret.name} --region ${var.aws_region}"
  }
  sensitive = true
}
