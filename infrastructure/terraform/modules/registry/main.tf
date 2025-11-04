# Container Registry Module - Multi-Cloud Support
# Supports AWS ECR, Azure ACR, and GCP Artifact Registry

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    aws = {
      source  = "hashicorp/aws"
      version = "~> 5.0"
    }
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
    google = {
      source  = "hashicorp/google"
      version = "~> 5.0"
    }
  }
}

# ==================== AWS ECR ====================
resource "aws_ecr_repository" "honua" {
  for_each = var.cloud_provider == "aws" ? toset(var.repository_names) : toset([])
  name     = "${var.registry_prefix}${each.key}"

  image_tag_mutability = "IMMUTABLE"

  encryption_configuration {
    encryption_type = "KMS"
    kms_key         = var.kms_key_arn
  }

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = merge(
    var.tags,
    {
      Name        = "${var.registry_prefix}${each.key}"
      Environment = var.environment
      ManagedBy   = "Terraform"
    }
  )
}

# ECR Lifecycle Policy
resource "aws_ecr_lifecycle_policy" "honua" {
  for_each   = var.cloud_provider == "aws" ? toset(var.repository_names) : toset([])
  repository = aws_ecr_repository.honua[each.key].name

  policy = jsonencode({
    rules = [
      {
        rulePriority = 1
        description  = "Keep last ${var.image_retention_count} images"
        selection = {
          tagStatus     = "tagged"
          tagPrefixList = ["v"]
          countType     = "imageCountMoreThan"
          countNumber   = var.image_retention_count
        }
        action = {
          type = "expire"
        }
      },
      {
        rulePriority = 2
        description  = "Remove untagged images older than ${var.untagged_retention_days} days"
        selection = {
          tagStatus   = "untagged"
          countType   = "sinceImagePushed"
          countUnit   = "days"
          countNumber = var.untagged_retention_days
        }
        action = {
          type = "expire"
        }
      }
    ]
  })
}

# ECR Repository Policy for cross-account access
resource "aws_ecr_repository_policy" "honua" {
  for_each   = var.cloud_provider == "aws" && length(var.customer_account_ids) > 0 ? toset(var.repository_names) : toset([])
  repository = aws_ecr_repository.honua[each.key].name

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowPullFromCustomerAccounts"
        Effect = "Allow"
        Principal = {
          AWS = [for account_id in var.customer_account_ids : "arn:aws:iam::${account_id}:root"]
        }
        Action = [
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchGetImage",
          "ecr:BatchCheckLayerAvailability"
        ]
      }
    ]
  })
}

# ECR Replication Configuration
resource "aws_ecr_replication_configuration" "honua" {
  count = var.cloud_provider == "aws" && var.enable_replication ? 1 : 0

  replication_configuration {
    rule {
      dynamic "destination" {
        for_each = var.replication_regions
        content {
          region      = destination.value
          registry_id = data.aws_caller_identity.current[0].account_id
        }
      }

      repository_filter {
        filter      = "${var.registry_prefix}*"
        filter_type = "PREFIX_MATCH"
      }
    }
  }
}

data "aws_caller_identity" "current" {
  count = var.cloud_provider == "aws" ? 1 : 0
}

# ==================== Azure ACR ====================
resource "azurerm_container_registry" "honua" {
  count               = var.cloud_provider == "azure" ? 1 : 0
  name                = replace("${var.registry_prefix}${var.environment}", "-", "")
  resource_group_name = var.resource_group_name
  location            = var.azure_location
  sku                 = var.azure_acr_sku
  admin_enabled       = false

  identity {
    type = "SystemAssigned"
  }

  encryption {
    enabled            = true
    key_vault_key_id   = var.azure_key_vault_key_id
    identity_client_id = azurerm_user_assigned_identity.acr[0].client_id
  }

  network_rule_set {
    default_action = var.environment == "production" ? "Deny" : "Allow"

    dynamic "ip_rule" {
      for_each = var.allowed_ip_ranges
      content {
        action   = "Allow"
        ip_range = ip_rule.value
      }
    }

    dynamic "virtual_network" {
      for_each = var.subnet_ids
      content {
        action    = "Allow"
        subnet_id = virtual_network.value
      }
    }
  }

  georeplications {
    location                = var.azure_replication_location
    zone_redundancy_enabled = true
  }

  tags = merge(
    var.tags,
    {
      Environment = var.environment
      ManagedBy   = "Terraform"
    }
  )
}

resource "azurerm_user_assigned_identity" "acr" {
  count               = var.cloud_provider == "azure" ? 1 : 0
  name                = "${var.registry_prefix}-acr-identity"
  resource_group_name = var.resource_group_name
  location            = var.azure_location

  tags = var.tags
}

# ACR Retention Policy
resource "azurerm_container_registry_task" "retention" {
  count                 = var.cloud_provider == "azure" ? 1 : 0
  name                  = "retention-policy"
  container_registry_id = azurerm_container_registry.honua[0].id

  platform {
    os = "Linux"
  }

  encoded_step {
    task_content = base64encode(<<-EOT
      version: v1.1.0
      steps:
        - cmd: acr purge --filter '.*:.*' --ago ${var.untagged_retention_days}d --untagged
          disableWorkingDirectoryOverride: true
          timeout: 3600
    EOT
    )
  }

  timer_trigger {
    name     = "daily"
    schedule = "0 2 * * *"
  }
}

# ==================== GCP Artifact Registry ====================
resource "google_artifact_registry_repository" "honua" {
  for_each      = var.cloud_provider == "gcp" ? toset(var.repository_names) : toset([])
  location      = var.gcp_region
  repository_id = "${var.registry_prefix}${each.key}"
  description   = "Docker repository for ${each.key}"
  format        = "DOCKER"
  project       = var.gcp_project_id

  cleanup_policies {
    id     = "delete-untagged"
    action = "DELETE"
    condition {
      tag_state  = "UNTAGGED"
      older_than = "${var.untagged_retention_days * 86400}s"
    }
  }

  cleanup_policies {
    id     = "keep-minimum-versions"
    action = "KEEP"
    most_recent_versions {
      keep_count = var.image_retention_count
    }
  }

  labels = merge(
    var.tags,
    {
      environment = var.environment
      managed_by  = "terraform"
    }
  )
}

# GCP Artifact Registry IAM for customer service accounts
resource "google_artifact_registry_repository_iam_member" "customer_pull" {
  for_each = var.cloud_provider == "gcp" && length(var.customer_service_accounts) > 0 ? {
    for pair in setproduct(var.repository_names, var.customer_service_accounts) :
    "${pair[0]}-${pair[1]}" => {
      repository = pair[0]
      member     = "serviceAccount:${pair[1]}"
    }
  } : {}

  project    = var.gcp_project_id
  location   = var.gcp_region
  repository = google_artifact_registry_repository.honua[each.value.repository].name
  role       = "roles/artifactregistry.reader"
  member     = each.value.member
}

# ==================== Customer-Specific Registries ====================
# AWS ECR for customer isolation
resource "aws_ecr_repository" "customer" {
  for_each = var.cloud_provider == "aws" && var.enable_customer_registries ? var.customer_repositories : {}
  name     = "${var.registry_prefix}customer-${each.key}"

  image_tag_mutability = "IMMUTABLE"

  encryption_configuration {
    encryption_type = "KMS"
    kms_key         = var.kms_key_arn
  }

  image_scanning_configuration {
    scan_on_push = true
  }

  tags = merge(
    var.tags,
    {
      Name        = "${var.registry_prefix}customer-${each.key}"
      Environment = var.environment
      Customer    = each.value
    }
  )
}

resource "aws_ecr_repository_policy" "customer" {
  for_each   = var.cloud_provider == "aws" && var.enable_customer_registries ? var.customer_repositories : {}
  repository = aws_ecr_repository.customer[each.key].name

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "AllowPullFromCustomer"
        Effect = "Allow"
        Principal = {
          AWS = "arn:aws:iam::${each.value}:root"
        }
        Action = [
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchGetImage",
          "ecr:BatchCheckLayerAvailability"
        ]
      }
    ]
  })
}
