# IAM Module - Multi-Cloud Support
# Supports AWS IAM, Azure AD/RBAC, and GCP IAM

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

# ==================== AWS IAM ====================

# IAM Role for Build Orchestrator
resource "aws_iam_role" "orchestrator" {
  count = var.cloud_provider == "aws" ? 1 : 0
  name  = "${var.service_name}-orchestrator-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "ec2.amazonaws.com"
      }
    }]
  })

  tags = var.tags
}

# Policy for Orchestrator
resource "aws_iam_role_policy" "orchestrator" {
  count = var.cloud_provider == "aws" ? 1 : 0
  name  = "orchestrator-policy"
  role  = aws_iam_role.orchestrator[0].id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "ecr:GetAuthorizationToken",
          "ecr:BatchCheckLayerAvailability",
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchGetImage",
          "ecr:PutImage",
          "ecr:InitiateLayerUpload",
          "ecr:UploadLayerPart",
          "ecr:CompleteLayerUpload"
        ]
        Resource = "*"
      },
      {
        Effect = "Allow"
        Action = [
          "secretsmanager:GetSecretValue",
          "secretsmanager:DescribeSecret"
        ]
        Resource = "arn:aws:secretsmanager:*:*:secret:${var.service_name}-*"
      },
      {
        Effect = "Allow"
        Action = [
          "kms:Decrypt",
          "kms:DescribeKey"
        ]
        Resource = var.kms_key_arn
      },
      {
        Effect = "Allow"
        Action = [
          "logs:CreateLogGroup",
          "logs:CreateLogStream",
          "logs:PutLogEvents"
        ]
        Resource = "arn:aws:logs:*:*:log-group:/aws/${var.service_name}/*"
      }
    ]
  })
}

# IAM Role for Kubernetes Service Account (IRSA)
resource "aws_iam_role" "k8s_service_account" {
  for_each = var.cloud_provider == "aws" ? var.k8s_service_accounts : {}
  name     = "${var.service_name}-${each.key}-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Principal = {
        Federated = var.oidc_provider_arn
      }
      Action = "sts:AssumeRoleWithWebIdentity"
      Condition = {
        StringEquals = {
          "${replace(var.oidc_provider_arn, "/^(.*provider/)/", "")}:sub" = "system:serviceaccount:${each.value.namespace}:${each.value.service_account}"
          "${replace(var.oidc_provider_arn, "/^(.*provider/)/", "")}:aud" = "sts.amazonaws.com"
        }
      }
    }]
  })

  tags = merge(
    var.tags,
    {
      ServiceAccount = each.key
    }
  )
}

resource "aws_iam_role_policy" "k8s_service_account" {
  for_each = var.cloud_provider == "aws" ? var.k8s_service_accounts : {}
  name     = "${each.key}-policy"
  role     = aws_iam_role.k8s_service_account[each.key].id

  policy = each.value.policy
}

# Customer IAM Users
resource "aws_iam_user" "customer" {
  for_each = var.cloud_provider == "aws" && var.enable_customer_iam ? var.customer_users : {}
  name     = "${var.service_name}-customer-${each.key}"
  path     = "/customers/"

  tags = merge(
    var.tags,
    {
      Customer = each.value
    }
  )
}

resource "aws_iam_access_key" "customer" {
  for_each = var.cloud_provider == "aws" && var.enable_customer_iam ? var.customer_users : {}
  user     = aws_iam_user.customer[each.key].name
}

# Customer IAM Policy - Least Privilege
resource "aws_iam_user_policy" "customer" {
  for_each = var.cloud_provider == "aws" && var.enable_customer_iam ? var.customer_users : {}
  name     = "customer-ecr-access"
  user     = aws_iam_user.customer[each.key].name

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "ecr:GetAuthorizationToken"
        ]
        Resource = "*"
      },
      {
        Effect = "Allow"
        Action = [
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchGetImage",
          "ecr:BatchCheckLayerAvailability"
        ]
        Resource = "arn:aws:ecr:*:*:repository/${var.service_name}-customer-${each.key}*"
      }
    ]
  })
}

# Store customer credentials in Secrets Manager
resource "aws_secretsmanager_secret" "customer_credentials" {
  for_each = var.cloud_provider == "aws" && var.enable_customer_iam ? var.customer_users : {}
  name     = "${var.service_name}-customer-${each.key}-credentials-${var.environment}"

  tags = merge(
    var.tags,
    {
      Customer = each.value
    }
  )
}

resource "aws_secretsmanager_secret_version" "customer_credentials" {
  for_each  = var.cloud_provider == "aws" && var.enable_customer_iam ? var.customer_users : {}
  secret_id = aws_secretsmanager_secret.customer_credentials[each.key].id

  secret_string = jsonencode({
    access_key_id     = aws_iam_access_key.customer[each.key].id
    secret_access_key = aws_iam_access_key.customer[each.key].secret
    user_arn          = aws_iam_user.customer[each.key].arn
  })
}

# GitHub Actions OIDC Provider
resource "aws_iam_openid_connect_provider" "github_actions" {
  count = var.cloud_provider == "aws" && var.enable_github_oidc ? 1 : 0
  url   = "https://token.actions.githubusercontent.com"

  client_id_list = [
    "sts.amazonaws.com"
  ]

  thumbprint_list = [
    "6938fd4d98bab03faadb97b34396831e3780aea1"
  ]

  tags = var.tags
}

resource "aws_iam_role" "github_actions" {
  count = var.cloud_provider == "aws" && var.enable_github_oidc ? 1 : 0
  name  = "${var.service_name}-github-actions-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect = "Allow"
      Principal = {
        Federated = aws_iam_openid_connect_provider.github_actions[0].arn
      }
      Action = "sts:AssumeRoleWithWebIdentity"
      Condition = {
        StringEquals = {
          "token.actions.githubusercontent.com:aud" = "sts.amazonaws.com"
        }
        StringLike = {
          "token.actions.githubusercontent.com:sub" = "repo:${var.github_org}/${var.github_repo}:*"
        }
      }
    }]
  })

  tags = var.tags
}

resource "aws_iam_role_policy" "github_actions" {
  count = var.cloud_provider == "aws" && var.enable_github_oidc ? 1 : 0
  name  = "github-actions-policy"
  role  = aws_iam_role.github_actions[0].id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [
      {
        Effect = "Allow"
        Action = [
          "ecr:GetAuthorizationToken",
          "ecr:BatchCheckLayerAvailability",
          "ecr:GetDownloadUrlForLayer",
          "ecr:BatchGetImage",
          "ecr:PutImage",
          "ecr:InitiateLayerUpload",
          "ecr:UploadLayerPart",
          "ecr:CompleteLayerUpload"
        ]
        Resource = "*"
      },
      {
        Effect = "Allow"
        Action = [
          "eks:DescribeCluster"
        ]
        Resource = "arn:aws:eks:*:*:cluster/${var.cluster_name}-${var.environment}"
      }
    ]
  })
}

# ==================== Azure IAM ====================

# Service Principal for Build Orchestrator
resource "azurerm_user_assigned_identity" "orchestrator" {
  count               = var.cloud_provider == "azure" ? 1 : 0
  name                = "${var.service_name}-orchestrator-${var.environment}"
  resource_group_name = var.resource_group_name
  location            = var.azure_location

  tags = var.tags
}

# Role Assignments
resource "azurerm_role_assignment" "orchestrator_acr" {
  count                = var.cloud_provider == "azure" ? 1 : 0
  scope                = var.acr_id
  role_definition_name = "AcrPush"
  principal_id         = azurerm_user_assigned_identity.orchestrator[0].principal_id
}

# Customer Service Principals
resource "azuread_service_principal" "customer" {
  for_each = var.cloud_provider == "azure" && var.enable_customer_iam ? var.customer_users : {}
  display_name = "${var.service_name}-customer-${each.key}"

  tags = [
    "Customer=${each.value}",
    "Environment=${var.environment}"
  ]
}

resource "azuread_service_principal_password" "customer" {
  for_each             = var.cloud_provider == "azure" && var.enable_customer_iam ? var.customer_users : {}
  service_principal_id = azuread_service_principal.customer[each.key].object_id
}

resource "azurerm_role_assignment" "customer_acr" {
  for_each             = var.cloud_provider == "azure" && var.enable_customer_iam ? var.customer_users : {}
  scope                = var.acr_id
  role_definition_name = "AcrPull"
  principal_id         = azuread_service_principal.customer[each.key].object_id
}

# Store customer credentials in Key Vault
resource "azurerm_key_vault_secret" "customer_credentials" {
  for_each     = var.cloud_provider == "azure" && var.enable_customer_iam ? var.customer_users : {}
  name         = "${var.service_name}-customer-${each.key}-creds"
  value        = jsonencode({
    client_id     = azuread_service_principal.customer[each.key].application_id
    client_secret = azuread_service_principal_password.customer[each.key].value
    tenant_id     = data.azurerm_client_config.current[0].tenant_id
  })
  key_vault_id = var.key_vault_id

  tags = var.tags
}

data "azurerm_client_config" "current" {
  count = var.cloud_provider == "azure" ? 1 : 0
}

# ==================== GCP IAM ====================

# Service Account for Build Orchestrator
resource "google_service_account" "orchestrator" {
  count        = var.cloud_provider == "gcp" ? 1 : 0
  account_id   = "${var.service_name}-orchestrator-${var.environment}"
  display_name = "Honua Build Orchestrator"
  project      = var.gcp_project_id
}

# Grant permissions to orchestrator
resource "google_project_iam_member" "orchestrator" {
  for_each = var.cloud_provider == "gcp" ? toset([
    "roles/artifactregistry.writer",
    "roles/secretmanager.secretAccessor",
    "roles/logging.logWriter"
  ]) : toset([])

  project = var.gcp_project_id
  role    = each.value
  member  = "serviceAccount:${google_service_account.orchestrator[0].email}"
}

# Workload Identity Binding for Kubernetes
resource "google_service_account_iam_binding" "workload_identity" {
  for_each           = var.cloud_provider == "gcp" ? var.k8s_service_accounts : {}
  service_account_id = google_service_account.orchestrator[0].name
  role               = "roles/iam.workloadIdentityUser"

  members = [
    "serviceAccount:${var.gcp_project_id}.svc.id.goog[${each.value.namespace}/${each.value.service_account}]"
  ]
}

# Customer Service Accounts
resource "google_service_account" "customer" {
  for_each     = var.cloud_provider == "gcp" && var.enable_customer_iam ? var.customer_users : {}
  account_id   = "${var.service_name}-customer-${each.key}"
  display_name = "Customer ${each.value}"
  project      = var.gcp_project_id
}

resource "google_service_account_key" "customer" {
  for_each           = var.cloud_provider == "gcp" && var.enable_customer_iam ? var.customer_users : {}
  service_account_id = google_service_account.customer[each.key].name
}

# Grant pull access to customer repositories
resource "google_artifact_registry_repository_iam_member" "customer" {
  for_each = var.cloud_provider == "gcp" && var.enable_customer_iam ? var.customer_users : {}

  project    = var.gcp_project_id
  location   = var.gcp_region
  repository = "${var.service_name}-customer-${each.key}"
  role       = "roles/artifactregistry.reader"
  member     = "serviceAccount:${google_service_account.customer[each.key].email}"
}

# Store customer credentials in Secret Manager
resource "google_secret_manager_secret" "customer_credentials" {
  for_each  = var.cloud_provider == "gcp" && var.enable_customer_iam ? var.customer_users : {}
  secret_id = "${var.service_name}-customer-${each.key}-creds-${var.environment}"
  project   = var.gcp_project_id

  replication {
    automatic = true
  }

  labels = var.tags
}

resource "google_secret_manager_secret_version" "customer_credentials" {
  for_each    = var.cloud_provider == "gcp" && var.enable_customer_iam ? var.customer_users : {}
  secret      = google_secret_manager_secret.customer_credentials[each.key].id
  secret_data = base64decode(google_service_account_key.customer[each.key].private_key)
}
