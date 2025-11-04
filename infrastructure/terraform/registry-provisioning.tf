#
# Honua Build Orchestrator - Registry Provisioning Infrastructure
#
# This Terraform configuration provisions the cloud infrastructure required
# for the Honua build orchestrator to create customer-specific container
# registries across AWS ECR, Azure ACR, and GCP Artifact Registry.
#
# Prerequisites:
# - AWS credentials with permissions to create IAM roles and policies
# - Azure credentials with Owner or User Access Administrator role
# - GCP credentials with Project IAM Admin role
# - GitHub organization admin access for creating GitHub Apps
#

terraform {
  required_version = ">= 1.5"

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
    random = {
      source  = "hashicorp/random"
      version = "~> 3.5"
    }
  }
}

# ============================================================================
# Variables
# ============================================================================

variable "aws_region" {
  description = "Primary AWS region for build orchestrator resources"
  type        = string
  default     = "us-east-1"
}

variable "azure_location" {
  description = "Primary Azure location for build orchestrator resources"
  type        = string
  default     = "eastus"
}

variable "gcp_project_id" {
  description = "GCP project ID for build orchestrator resources"
  type        = string
}

variable "gcp_region" {
  description = "Primary GCP region for build orchestrator resources"
  type        = string
  default     = "us-central1"
}

variable "environment" {
  description = "Environment name (dev, staging, production)"
  type        = string
  default     = "production"
}

variable "orchestrator_service_name" {
  description = "Name of the build orchestrator service"
  type        = string
  default     = "honua-build-orchestrator"
}

variable "github_org" {
  description = "GitHub organization name for repository access"
  type        = string
}

variable "allowed_ecr_regions" {
  description = "List of AWS regions where customer ECR repositories can be created"
  type        = list(string)
  default     = ["us-east-1", "us-west-2", "eu-west-1", "ap-southeast-1"]
}

variable "tags" {
  description = "Common tags to apply to all resources"
  type        = map(string)
  default = {
    Project   = "Honua"
    ManagedBy = "Terraform"
    Component = "BuildOrchestrator"
  }
}

# ============================================================================
# AWS Resources - Build Orchestrator IAM Role
# ============================================================================

# IAM policy for the build orchestrator
resource "aws_iam_policy" "build_orchestrator" {
  name        = "${var.orchestrator_service_name}-policy"
  description = "Policy for Honua build orchestrator to manage customer ECR repositories and IAM users"

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Sid    = "ManageCustomerECRRepositories"
        Effect = "Allow"
        Action = [
          "ecr:CreateRepository",
          "ecr:DeleteRepository",
          "ecr:SetRepositoryPolicy",
          "ecr:TagResource",
          "ecr:UntagResource",
          "ecr:DescribeRepositories",
          "ecr:ListTagsForResource"
        ]
        Resource = "arn:aws:ecr:*:*:repository/honua/*"
        Condition = {
          StringEquals = {
            "aws:RequestedRegion" = var.allowed_ecr_regions
          }
        }
      },
      {
        Sid    = "PushImagesToCustomerRepositories"
        Effect = "Allow"
        Action = [
          "ecr:PutImage",
          "ecr:InitiateLayerUpload",
          "ecr:UploadLayerPart",
          "ecr:CompleteLayerUpload",
          "ecr:BatchCheckLayerAvailability",
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchGetImage"
        ]
        Resource = "arn:aws:ecr:*:*:repository/honua/*"
      },
      {
        Sid      = "GetECRAuthorizationToken"
        Effect   = "Allow"
        Action   = ["ecr:GetAuthorizationToken"]
        Resource = "*"
      },
      {
        Sid    = "ManageCustomerIAMUsers"
        Effect = "Allow"
        Action = [
          "iam:CreateUser",
          "iam:DeleteUser",
          "iam:GetUser",
          "iam:ListUserTags",
          "iam:TagUser",
          "iam:UntagUser"
        ]
        Resource = "arn:aws:iam::*:user/honua-customer-*"
        Condition = {
          StringEquals = {
            "iam:ResourceTag/ManagedBy" = "honua-orchestrator"
          }
        }
      },
      {
        Sid    = "ManageCustomerIAMPolicies"
        Effect = "Allow"
        Action = [
          "iam:CreatePolicy",
          "iam:DeletePolicy",
          "iam:GetPolicy",
          "iam:GetPolicyVersion",
          "iam:ListPolicyVersions",
          "iam:CreatePolicyVersion",
          "iam:DeletePolicyVersion",
          "iam:TagPolicy",
          "iam:UntagPolicy"
        ]
        Resource = "arn:aws:iam::*:policy/honua-customer-*"
        Condition = {
          StringEquals = {
            "iam:ResourceTag/ManagedBy" = "honua-orchestrator"
          }
        }
      },
      {
        Sid    = "AttachPolicyToCustomerUsers"
        Effect = "Allow"
        Action = [
          "iam:AttachUserPolicy",
          "iam:DetachUserPolicy",
          "iam:ListAttachedUserPolicies"
        ]
        Resource = "arn:aws:iam::*:user/honua-customer-*"
        Condition = {
          ArnLike = {
            "iam:PolicyARN" = "arn:aws:iam::*:policy/honua-customer-*"
          }
        }
      },
      {
        Sid    = "ManageCustomerAccessKeys"
        Effect = "Allow"
        Action = [
          "iam:CreateAccessKey",
          "iam:DeleteAccessKey",
          "iam:ListAccessKeys",
          "iam:UpdateAccessKey"
        ]
        Resource = "arn:aws:iam::*:user/honua-customer-*"
      },
      {
        Sid    = "AccessSecretsForBuildOrchestration"
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue",
          "secretsmanager:DescribeSecret"
        ]
        Resource = [
          "arn:aws:secretsmanager:*:*:secret:honua/github-pat-*",
          "arn:aws:secretsmanager:*:*:secret:honua/license-keys/*"
        ]
      },
      {
        Sid    = "DecryptSecretsWithKMS"
        Effect = "Allow"
        Action = [
          "kms:Decrypt",
          "kms:DescribeKey"
        ]
        Resource = "arn:aws:kms:*:*:key/*"
        Condition = {
          StringEquals = {
            "kms:ViaService" = [
              for region in var.allowed_ecr_regions :
              "secretsmanager.${region}.amazonaws.com"
            ]
          }
        }
      },
      {
        Sid    = "LogBuildOrchestratorActivity"
        Effect = "Allow"
        Action = [
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = "arn:aws:logs:*:*:log-group:/aws/honua/build-orchestrator:*"
      }
    ]
  })

  tags = merge(var.tags, {
    Name = "${var.orchestrator_service_name}-policy"
  })
}

# IAM role for the build orchestrator service
resource "aws_iam_role" "build_orchestrator" {
  name        = var.orchestrator_service_name
  description = "IAM role for Honua build orchestrator service"
  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Principal = {
          Service = [
            "ecs-tasks.amazonaws.com",
            "lambda.amazonaws.com",
            "ec2.amazonaws.com"
          ]
        }
        Action = "sts:AssumeRole"
      }
    ]
  })

  tags = merge(var.tags, {
    Name = var.orchestrator_service_name
  })
}

# Attach the policy to the role
resource "aws_iam_role_policy_attachment" "build_orchestrator" {
  role       = aws_iam_role.build_orchestrator.name
  policy_arn = aws_iam_policy.build_orchestrator.arn
}

# KMS key for encrypting orchestrator secrets
resource "aws_kms_key" "orchestrator_secrets" {
  description             = "KMS key for encrypting Honua build orchestrator secrets"
  deletion_window_in_days = 30
  enable_key_rotation     = true

  tags = merge(var.tags, {
    Name = "${var.orchestrator_service_name}-secrets"
  })
}

resource "aws_kms_alias" "orchestrator_secrets" {
  name          = "alias/honua/build-orchestrator"
  target_key_id = aws_kms_key.orchestrator_secrets.key_id
}

# Secrets Manager secret for GitHub PAT (placeholder - populate manually)
resource "aws_secretsmanager_secret" "github_pat" {
  name        = "honua/github-pat-build-orchestrator"
  description = "GitHub Personal Access Token for Honua build orchestrator"
  kms_key_id  = aws_kms_key.orchestrator_secrets.arn

  tags = merge(var.tags, {
    Name = "github-pat-build-orchestrator"
  })
}

# ============================================================================
# Azure Resources - Build Orchestrator Service Principal
# ============================================================================

provider "azurerm" {
  features {
    key_vault {
      purge_soft_delete_on_destroy = false
    }
  }
}

# Resource group for customer registries
resource "azurerm_resource_group" "registries" {
  name     = "honua-registries-${var.environment}"
  location = var.azure_location

  tags = merge(var.tags, {
    Environment = var.environment
  })
}

# User-assigned managed identity for build orchestrator
resource "azurerm_user_assigned_identity" "build_orchestrator" {
  name                = var.orchestrator_service_name
  resource_group_name = azurerm_resource_group.registries.name
  location            = azurerm_resource_group.registries.location

  tags = var.tags
}

# Custom role definition for build orchestrator
resource "azurerm_role_definition" "build_orchestrator" {
  name        = "Honua Build Orchestrator"
  scope       = azurerm_resource_group.registries.id
  description = "Custom role for Honua build orchestrator to manage customer container registries"

  permissions {
    actions = [
      "Microsoft.ContainerRegistry/registries/read",
      "Microsoft.ContainerRegistry/registries/write",
      "Microsoft.ContainerRegistry/registries/delete",
      "Microsoft.ContainerRegistry/registries/repositories/read",
      "Microsoft.ContainerRegistry/registries/repositories/write",
      "Microsoft.ContainerRegistry/registries/repositories/delete",
      "Microsoft.ContainerRegistry/registries/scopeMaps/read",
      "Microsoft.ContainerRegistry/registries/scopeMaps/write",
      "Microsoft.ContainerRegistry/registries/scopeMaps/delete",
      "Microsoft.ContainerRegistry/registries/tokens/read",
      "Microsoft.ContainerRegistry/registries/tokens/write",
      "Microsoft.ContainerRegistry/registries/tokens/delete",
      "Microsoft.ContainerRegistry/registries/tokens/operationStatuses/read",
      "Microsoft.Authorization/roleAssignments/read",
      "Microsoft.Authorization/roleAssignments/write",
      "Microsoft.Authorization/roleAssignments/delete",
      "Microsoft.ManagedIdentity/userAssignedIdentities/read",
      "Microsoft.ManagedIdentity/userAssignedIdentities/write",
      "Microsoft.ManagedIdentity/userAssignedIdentities/delete",
      "Microsoft.ManagedIdentity/userAssignedIdentities/assign/action",
      "Microsoft.KeyVault/vaults/secrets/read",
      "Microsoft.Resources/subscriptions/resourceGroups/read"
    ]

    data_actions = [
      "Microsoft.ContainerRegistry/registries/push/write",
      "Microsoft.ContainerRegistry/registries/pull/read",
      "Microsoft.ContainerRegistry/registries/artifacts/delete"
    ]
  }

  assignable_scopes = [
    azurerm_resource_group.registries.id
  ]
}

# Assign custom role to the managed identity
resource "azurerm_role_assignment" "build_orchestrator" {
  scope              = azurerm_resource_group.registries.id
  role_definition_id = azurerm_role_definition.build_orchestrator.role_definition_resource_id
  principal_id       = azurerm_user_assigned_identity.build_orchestrator.principal_id
}

# Key Vault for storing orchestrator secrets
resource "azurerm_key_vault" "orchestrator" {
  name                       = "honua-orchestrator-kv"
  location                   = azurerm_resource_group.registries.location
  resource_group_name        = azurerm_resource_group.registries.name
  tenant_id                  = azurerm_user_assigned_identity.build_orchestrator.tenant_id
  sku_name                   = "standard"
  soft_delete_retention_days = 90
  purge_protection_enabled   = true

  tags = var.tags
}

# Key Vault access policy for the managed identity
resource "azurerm_key_vault_access_policy" "build_orchestrator" {
  key_vault_id = azurerm_key_vault.orchestrator.id
  tenant_id    = azurerm_user_assigned_identity.build_orchestrator.tenant_id
  object_id    = azurerm_user_assigned_identity.build_orchestrator.principal_id

  secret_permissions = [
    "Get",
    "List"
  ]
}

# ============================================================================
# GCP Resources - Build Orchestrator Service Account
# ============================================================================

provider "google" {
  project = var.gcp_project_id
  region  = var.gcp_region
}

# Service account for build orchestrator
resource "google_service_account" "build_orchestrator" {
  account_id   = "honua-build-orchestrator"
  display_name = "Honua Build Orchestrator"
  description  = "Service account for Honua build orchestrator to manage customer Artifact Registry repositories"
  project      = var.gcp_project_id
}

# Custom IAM role for build orchestrator
resource "google_project_iam_custom_role" "build_orchestrator" {
  role_id     = "honuaBuildOrchestrator"
  title       = "Honua Build Orchestrator"
  description = "Custom role for Honua build orchestrator to manage customer Artifact Registry repositories and service accounts"
  project     = var.gcp_project_id

  permissions = [
    # Artifact Registry - Repository Management
    "artifactregistry.repositories.create",
    "artifactregistry.repositories.delete",
    "artifactregistry.repositories.get",
    "artifactregistry.repositories.list",
    "artifactregistry.repositories.update",
    "artifactregistry.repositories.setIamPolicy",
    "artifactregistry.repositories.getIamPolicy",

    # Artifact Registry - Image Operations
    "artifactregistry.dockerimages.get",
    "artifactregistry.dockerimages.list",
    "artifactregistry.tags.create",
    "artifactregistry.tags.update",
    "artifactregistry.tags.delete",
    "artifactregistry.tags.get",
    "artifactregistry.tags.list",

    # Service Account Management
    "iam.serviceAccounts.create",
    "iam.serviceAccounts.delete",
    "iam.serviceAccounts.get",
    "iam.serviceAccounts.list",
    "iam.serviceAccounts.update",
    "iam.serviceAccounts.setIamPolicy",
    "iam.serviceAccounts.getIamPolicy",

    # Service Account Keys
    "iam.serviceAccountKeys.create",
    "iam.serviceAccountKeys.delete",
    "iam.serviceAccountKeys.get",
    "iam.serviceAccountKeys.list",

    # Secret Manager - Access build secrets
    "secretmanager.secrets.get",
    "secretmanager.versions.access",
    "secretmanager.versions.get",
    "secretmanager.versions.list",

    # Resource Manager - Project level access
    "resourcemanager.projects.get",
    "resourcemanager.projects.list"
  ]
}

# Bind custom role to the service account at project level
resource "google_project_iam_member" "build_orchestrator" {
  project = var.gcp_project_id
  role    = google_project_iam_custom_role.build_orchestrator.id
  member  = "serviceAccount:${google_service_account.build_orchestrator.email}"
}

# Secret Manager secret for GitHub PAT
resource "google_secret_manager_secret" "github_pat" {
  secret_id = "honua-github-pat-build-orchestrator"
  project   = var.gcp_project_id

  replication {
    auto {}
  }

  labels = {
    project   = "honua"
    component = "build-orchestrator"
  }
}

# Grant service account access to the GitHub PAT secret
resource "google_secret_manager_secret_iam_member" "github_pat_access" {
  project   = var.gcp_project_id
  secret_id = google_secret_manager_secret.github_pat.secret_id
  role      = "roles/secretmanager.secretAccessor"
  member    = "serviceAccount:${google_service_account.build_orchestrator.email}"
}

# ============================================================================
# Outputs
# ============================================================================

output "aws_orchestrator_role_arn" {
  description = "ARN of the AWS IAM role for the build orchestrator"
  value       = aws_iam_role.build_orchestrator.arn
}

output "aws_orchestrator_policy_arn" {
  description = "ARN of the AWS IAM policy for the build orchestrator"
  value       = aws_iam_policy.build_orchestrator.arn
}

output "aws_kms_key_id" {
  description = "ID of the KMS key for encrypting orchestrator secrets"
  value       = aws_kms_key.orchestrator_secrets.key_id
}

output "aws_github_pat_secret_arn" {
  description = "ARN of the Secrets Manager secret for GitHub PAT"
  value       = aws_secretsmanager_secret.github_pat.arn
}

output "azure_managed_identity_client_id" {
  description = "Client ID of the Azure managed identity for the build orchestrator"
  value       = azurerm_user_assigned_identity.build_orchestrator.client_id
}

output "azure_managed_identity_principal_id" {
  description = "Principal ID of the Azure managed identity for the build orchestrator"
  value       = azurerm_user_assigned_identity.build_orchestrator.principal_id
}

output "azure_resource_group_name" {
  description = "Name of the Azure resource group for customer registries"
  value       = azurerm_resource_group.registries.name
}

output "azure_key_vault_uri" {
  description = "URI of the Azure Key Vault for orchestrator secrets"
  value       = azurerm_key_vault.orchestrator.vault_uri
}

output "gcp_service_account_email" {
  description = "Email of the GCP service account for the build orchestrator"
  value       = google_service_account.build_orchestrator.email
}

output "gcp_custom_role_id" {
  description = "ID of the GCP custom IAM role for the build orchestrator"
  value       = google_project_iam_custom_role.build_orchestrator.id
}

output "gcp_github_pat_secret_id" {
  description = "ID of the GCP Secret Manager secret for GitHub PAT"
  value       = google_secret_manager_secret.github_pat.secret_id
}
