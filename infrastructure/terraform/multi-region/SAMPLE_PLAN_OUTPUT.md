# Sample Terraform Plan Output

This document shows a sample output from running `terraform plan` on the multi-region deployment configuration.

## Configuration Summary

```
Cloud Provider:        AWS
Primary Region:        us-east-1 (N. Virginia)
DR Region:             us-west-2 (Oregon)
Environment:           prod
Project Name:          honua

Features:
  - Multi-region deployment: ✓
  - Database replication:    ✓
  - Storage replication:     ✓
  - Redis replication:       ✓
  - Global load balancer:    ✓
  - Auto-scaling:           ✓
  - Monitoring:             ✓
  - WAF:                    ✓
  - CDN:                    ✓
```

## Terraform Plan Output

```hcl
Terraform used the selected providers to generate the following execution plan. Resource actions are indicated with the following symbols:
  + create
  ~ update in-place
  - destroy

Terraform will perform the following actions:

  # module.aws_primary[0].aws_vpc.main will be created
  + resource "aws_vpc" "main" {
      + arn                              = (known after apply)
      + cidr_block                       = "10.0.0.0/16"
      + default_network_acl_id          = (known after apply)
      + default_route_table_id          = (known after apply)
      + default_security_group_id       = (known after apply)
      + dhcp_options_id                 = (known after apply)
      + enable_dns_hostnames            = true
      + enable_dns_support              = true
      + id                              = (known after apply)
      + instance_tenancy                = "default"
      + ipv6_association_id             = (known after apply)
      + ipv6_cidr_block                 = (known after apply)
      + main_route_table_id             = (known after apply)
      + owner_id                        = (known after apply)
      + tags                            = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-vpc"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
        }
      + tags_all                        = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-vpc"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
        }
    }

  # module.aws_primary[0].aws_subnet.public[0] will be created
  + resource "aws_subnet" "public" {
      + arn                             = (known after apply)
      + availability_zone               = "us-east-1a"
      + availability_zone_id            = (known after apply)
      + cidr_block                      = "10.0.0.0/24"
      + id                              = (known after apply)
      + ipv6_cidr_block_association_id  = (known after apply)
      + map_public_ip_on_launch         = true
      + owner_id                        = (known after apply)
      + tags                            = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-public-1"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
          + "Tier"        = "Public"
        }
      + tags_all                        = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-public-1"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
          + "Tier"        = "Public"
        }
      + vpc_id                          = (known after apply)
    }

  # module.aws_primary[0].aws_subnet.public[1] will be created
  + resource "aws_subnet" "public" {
      + arn                             = (known after apply)
      + availability_zone               = "us-east-1b"
      + availability_zone_id            = (known after apply)
      + cidr_block                      = "10.0.1.0/24"
      + id                              = (known after apply)
      + ipv6_cidr_block_association_id  = (known after apply)
      + map_public_ip_on_launch         = true
      + owner_id                        = (known after apply)
      + tags                            = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-public-2"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
          + "Tier"        = "Public"
        }
      + tags_all                        = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-public-2"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
          + "Tier"        = "Public"
        }
      + vpc_id                          = (known after apply)
    }

  # module.aws_primary[0].aws_subnet.public[2] will be created
  + resource "aws_subnet" "public" {
      + arn                             = (known after apply)
      + availability_zone               = "us-east-1c"
      + availability_zone_id            = (known after apply)
      + cidr_block                      = "10.0.2.0/24"
      + id                              = (known after apply)
      + ipv6_cidr_block_association_id  = (known after apply)
      + map_public_ip_on_launch         = true
      + owner_id                        = (known after apply)
      + tags                            = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-public-3"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
          + "Tier"        = "Public"
        }
      + tags_all                        = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-public-3"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
          + "Tier"        = "Public"
        }
      + vpc_id                          = (known after apply)
    }

  # module.aws_primary[0].aws_subnet.private[0] will be created
  + resource "aws_subnet" "private" {
      + arn                             = (known after apply)
      + availability_zone               = "us-east-1a"
      + availability_zone_id            = (known after apply)
      + cidr_block                      = "10.0.10.0/24"
      + id                              = (known after apply)
      + ipv6_cidr_block_association_id  = (known after apply)
      + owner_id                        = (known after apply)
      + tags                            = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-private-1"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
          + "Tier"        = "Private"
        }
      + tags_all                        = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-private-1"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
          + "Tier"        = "Private"
        }
      + vpc_id                          = (known after apply)
    }

  # [... similar output for private[1], private[2] ...]

  # module.aws_primary[0].aws_internet_gateway.main will be created
  + resource "aws_internet_gateway" "main" {
      + arn      = (known after apply)
      + id       = (known after apply)
      + owner_id = (known after apply)
      + tags     = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-igw"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
        }
      + tags_all = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-igw"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
        }
      + vpc_id   = (known after apply)
    }

  # module.aws_primary[0].aws_nat_gateway.main[0] will be created
  + resource "aws_nat_gateway" "main" {
      + allocation_id        = (known after apply)
      + connectivity_type    = "public"
      + id                   = (known after apply)
      + network_interface_id = (known after apply)
      + private_ip           = (known after apply)
      + public_ip            = (known after apply)
      + subnet_id            = (known after apply)
      + tags                 = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-nat-1"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
        }
      + tags_all             = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-nat-1"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
        }
    }

  # [... similar output for nat[1], nat[2] ...]

  # module.aws_primary[0].aws_security_group.alb will be created
  + resource "aws_security_group" "alb" {
      + arn                    = (known after apply)
      + description            = "Security group for ALB"
      + egress                 = [
          + {
              + cidr_blocks      = [
                  + "0.0.0.0/0",
                ]
              + description      = ""
              + from_port        = 0
              + ipv6_cidr_blocks = []
              + prefix_list_ids  = []
              + protocol         = "-1"
              + security_groups  = []
              + self             = false
              + to_port          = 0
            },
        ]
      + id                     = (known after apply)
      + ingress                = [
          + {
              + cidr_blocks      = [
                  + "0.0.0.0/0",
                ]
              + description      = ""
              + from_port        = 80
              + ipv6_cidr_blocks = []
              + prefix_list_ids  = []
              + protocol         = "tcp"
              + security_groups  = []
              + self             = false
              + to_port          = 80
            },
          + {
              + cidr_blocks      = [
                  + "0.0.0.0/0",
                ]
              + description      = ""
              + from_port        = 443
              + ipv6_cidr_blocks = []
              + prefix_list_ids  = []
              + protocol         = "tcp"
              + security_groups  = []
              + self             = false
              + to_port          = 443
            },
        ]
      + name                   = (known after apply)
      + name_prefix            = "honua-prod-us-east-1-alb-"
      + owner_id               = (known after apply)
      + revoke_rules_on_delete = false
      + tags                   = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-alb-sg"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
        }
      + tags_all               = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-alb-sg"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
        }
      + vpc_id                 = (known after apply)
    }

  # module.aws_primary[0].aws_security_group.ecs_tasks will be created
  + resource "aws_security_group" "ecs_tasks" {
      + arn                    = (known after apply)
      + description            = "Security group for ECS tasks"
      + egress                 = [
          + {
              + cidr_blocks      = [
                  + "0.0.0.0/0",
                ]
              + description      = ""
              + from_port        = 0
              + ipv6_cidr_blocks = []
              + prefix_list_ids  = []
              + protocol         = "-1"
              + security_groups  = []
              + self             = false
              + to_port          = 0
            },
        ]
      + id                     = (known after apply)
      + ingress                = [
          + {
              + cidr_blocks      = []
              + description      = ""
              + from_port        = 8080
              + ipv6_cidr_blocks = []
              + prefix_list_ids  = []
              + protocol         = "tcp"
              + security_groups  = (known after apply)
              + self             = false
              + to_port          = 8080
            },
        ]
      + name                   = (known after apply)
      + name_prefix            = "honua-prod-us-east-1-ecs-tasks-"
      + owner_id               = (known after apply)
      + revoke_rules_on_delete = false
      + tags                   = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-ecs-tasks-sg"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
        }
      + tags_all               = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-ecs-tasks-sg"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
        }
      + vpc_id                 = (known after apply)
    }

  # module.aws_primary[0].aws_security_group.rds will be created
  + resource "aws_security_group" "rds" {
      + arn                    = (known after apply)
      + description            = "Security group for RDS"
      + egress                 = [
          + {
              + cidr_blocks      = [
                  + "0.0.0.0/0",
                ]
              + description      = ""
              + from_port        = 0
              + ipv6_cidr_blocks = []
              + prefix_list_ids  = []
              + protocol         = "-1"
              + security_groups  = []
              + self             = false
              + to_port          = 0
            },
        ]
      + id                     = (known after apply)
      + ingress                = [
          + {
              + cidr_blocks      = []
              + description      = ""
              + from_port        = 5432
              + ipv6_cidr_blocks = []
              + prefix_list_ids  = []
              + protocol         = "tcp"
              + security_groups  = (known after apply)
              + self             = false
              + to_port          = 5432
            },
        ]
      + name                   = (known after apply)
      + name_prefix            = "honua-prod-us-east-1-rds-"
      + owner_id               = (known after apply)
      + revoke_rules_on_delete = false
      + tags                   = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-rds-sg"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
        }
      + tags_all               = {
          + "Environment" = "prod"
          + "ManagedBy"   = "Terraform"
          + "Name"        = "honua-prod-us-east-1-rds-sg"
          + "Project"     = "HonuaIO"
          + "Region"      = "us-east-1"
          + "Role"        = "primary"
        }
      + vpc_id                 = (known after apply)
    }

  # [... similar resources for DR region (module.aws_dr[0]) ...]

  # random_id.suffix will be created
  + resource "random_id" "suffix" {
      + b64_std     = (known after apply)
      + b64_url     = (known after apply)
      + byte_length = 4
      + dec         = (known after apply)
      + hex         = (known after apply)
      + id          = (known after apply)
    }

Plan: 45 to add, 0 to change, 0 to destroy.

Changes to Outputs:
  + connection_info       = (sensitive value)
  + deployment_summary    = {
      + capacity = {
          + dr_instances      = 1
          + max_instances     = 10
          + min_instances     = 1
          + primary_instances = 3
        }
      + cloud_provider = "aws"
      + environment    = "prod"
      + features       = {
          + auto_scaling         = true
          + cdn                  = true
          + db_replication       = true
          + global_load_balancer = true
          + monitoring           = true
          + redis_replication    = true
          + storage_replication  = true
          + waf                  = true
        }
      + project_name   = "honua"
      + regions        = {
          + dr      = "us-west-2"
          + primary = "us-east-1"
        }
      + sla            = {
          + rpo_seconds         = 60
          + rto_minutes         = 15
          + target_availability = "99.95%"
        }
    }
  + dr_db_endpoint        = (sensitive value)
  + dr_db_identifier      = "honua-prod-us-west-2-postgres"
  + dr_endpoint           = "honua-prod-us-west-2-lb.amazonaws.com"
  + dr_health_check_id    = "N/A"
  + dr_region             = "us-west-2"
  + dr_storage_bucket     = "honua-prod-us-west-2-storage-a1b2c3d4"
  + dr_storage_endpoint   = "honua-prod-us-west-2-storage-a1b2c3d4.s3.us-west-2.amazonaws.com"
  + estimated_monthly_cost = {
      + dr_region = {
          + compute       = "$150"
          + data_transfer = "$50-150"
          + database      = "$250-400"
          + redis         = "$50-80"
          + storage       = "$5-25"
        }
      + global    = {
          + cdn           = "$20-100"
          + dns           = "$10-20"
          + load_balancer = "$30-50"
          + waf           = "$20-50"
        }
      + note      = "Costs are estimates and may vary based on usage"
      + primary_region = {
          + compute       = "$450"
          + data_transfer = "$50-150"
          + database      = "$400-600"
          + redis         = "$80-120"
          + storage       = "$10-50"
        }
    }
  + failover_info         = {
      + automated_failover      = "Enabled via health checks"
      + documentation           = "See FAILOVER.md for detailed procedures"
      + dr_health_check         = "N/A"
      + estimated_data_loss     = "60 seconds"
      + estimated_failover_time = "15 minutes"
      + health_check_interval   = "30 seconds"
      + health_check_threshold  = "3 failures"
      + primary_health_check    = "N/A"
    }
  + global_endpoint       = "N/A"
  + global_load_balancer_name = "N/A"
  + monitoring_dashboard_url = "https://console.aws.amazon.com/cloudwatch/home?region=us-east-1#dashboards:name=honua-prod"
  + primary_db_endpoint   = (sensitive value)
  + primary_db_identifier = "honua-prod-us-east-1-postgres"
  + primary_endpoint      = "honua-prod-us-east-1-lb.amazonaws.com"
  + primary_health_check_id = "N/A"
  + primary_redis_endpoint = (sensitive value)
  + primary_region        = "us-east-1"
  + primary_storage_bucket = "honua-prod-us-east-1-storage-a1b2c3d4"
  + primary_storage_endpoint = "honua-prod-us-east-1-storage-a1b2c3d4.s3.us-east-1.amazonaws.com"
  + validation_commands   = {
      + check_replication       = "aws rds describe-db-instances --db-instance-identifier honua-prod-us-west-2-postgres --query 'DBInstances[0].StatusInfos'"
      + test_dr_endpoint        = "curl -I honua-prod-us-west-2-lb.amazonaws.com/health"
      + test_global_endpoint    = "N/A"
      + test_primary_endpoint   = "curl -I honua-prod-us-east-1-lb.amazonaws.com/health"
    }
```

## Plan Summary

```
Resource Summary:
  create: 45 resources
  update: 0 resources
  destroy: 0 resources

Primary Region (us-east-1):
  ✓ VPC with 3 public and 3 private subnets
  ✓ Internet Gateway
  ✓ 3 NAT Gateways (one per AZ)
  ✓ Route tables and associations
  ✓ Security groups for ALB, ECS, and RDS
  ✓ [Placeholder: ECS Cluster, Tasks, Services]
  ✓ [Placeholder: Application Load Balancer]
  ✓ [Placeholder: RDS PostgreSQL Multi-AZ]
  ✓ [Placeholder: S3 buckets]
  ✓ [Placeholder: ElastiCache Redis]

DR Region (us-west-2):
  ✓ VPC with 3 public and 3 private subnets
  ✓ Internet Gateway
  ✓ 3 NAT Gateways (one per AZ)
  ✓ Route tables and associations
  ✓ Security groups for ALB, ECS, and RDS
  ✓ [Placeholder: ECS Cluster, Tasks, Services]
  ✓ [Placeholder: Application Load Balancer]
  ✓ [Placeholder: RDS Read Replica]
  ✓ [Placeholder: S3 buckets with replication]
  ✓ [Placeholder: ElastiCache Redis replica]

Global:
  ✓ [Placeholder: Route53 hosted zone]
  ✓ [Placeholder: Route53 health checks]
  ✓ [Placeholder: CloudFront distribution]
  ✓ [Placeholder: WAF rules]
```

## Estimated Costs

```
Monthly Cost Breakdown:

Primary Region (us-east-1):
  Compute (ECS Fargate):     $450   (3 instances)
  Database (RDS):           $400-600
  Storage (S3):             $10-50
  Cache (Redis):            $80-120
  Data Transfer:            $50-150
  Subtotal:                 ~$1,000-1,370

DR Region (us-west-2):
  Compute (ECS Fargate):     $150   (1 instance)
  Database (Read Replica):  $250-400
  Storage (S3):             $5-25
  Cache (Redis):            $50-80
  Data Transfer:            $50-150
  Subtotal:                 ~$505-805

Global Services:
  Load Balancer:            $30-50
  CDN (CloudFront):         $20-100
  DNS (Route53):            $10-20
  WAF:                      $20-50
  Subtotal:                 ~$80-220

Total Estimated Cost:       $1,585-2,395 per month
```

## Next Steps

1. Review the plan output carefully
2. Verify all resource configurations
3. Ensure secrets are stored securely (not in terraform.tfvars)
4. Run `terraform apply tfplan` to create resources
5. Configure application environment variables
6. Deploy application containers
7. Run smoke tests
8. Configure monitoring and alerts
9. Schedule DR drills

## Validation Commands

After deployment, run these commands to validate:

```bash
# Test primary endpoint
curl -I honua-prod-us-east-1-lb.amazonaws.com/health

# Test DR endpoint
curl -I honua-prod-us-west-2-lb.amazonaws.com/health

# Check database replication status
aws rds describe-db-instances \
  --db-instance-identifier honua-prod-us-west-2-postgres \
  --query 'DBInstances[0].StatusInfos'

# View CloudWatch dashboard
open https://console.aws.amazon.com/cloudwatch/home?region=us-east-1#dashboards:name=honua-prod
```

## Notes

- This is a sample output based on the current module structure
- The actual plan would include more resources for a complete deployment
- Placeholder comments indicate where additional resources would be defined
- Cost estimates are approximate and may vary based on actual usage
- Always review security group rules and network ACLs before applying in production
