# Kubernetes Cluster Module - Multi-Cloud Support
# Supports AWS EKS, Azure AKS, and GCP GKE

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
    kubernetes = {
      source  = "hashicorp/kubernetes"
      version = "~> 2.23"
    }
  }
}

# ==================== AWS EKS ====================
resource "aws_eks_cluster" "honua" {
  count    = var.cloud_provider == "aws" ? 1 : 0
  name     = "${var.cluster_name}-${var.environment}"
  role_arn = aws_iam_role.eks_cluster[0].arn
  version  = var.kubernetes_version

  vpc_config {
    subnet_ids              = var.subnet_ids
    endpoint_private_access = true
    endpoint_public_access  = var.environment == "dev" ? true : false
    public_access_cidrs     = var.allowed_public_cidrs
    security_group_ids      = [aws_security_group.eks_cluster[0].id]
  }

  encryption_config {
    provider {
      key_arn = var.kms_key_arn
    }
    resources = ["secrets"]
  }

  enabled_cluster_log_types = [
    "api",
    "audit",
    "authenticator",
    "controllerManager",
    "scheduler"
  ]

  tags = merge(
    var.tags,
    {
      Name        = "${var.cluster_name}-${var.environment}"
      Environment = var.environment
      ManagedBy   = "Terraform"
      Project     = "Honua"
    }
  )

  depends_on = [
    aws_iam_role_policy_attachment.eks_cluster_policy,
    aws_iam_role_policy_attachment.eks_service_policy,
  ]
}

# EKS Node Group - Graviton ARM instances
resource "aws_eks_node_group" "honua_arm" {
  count           = var.cloud_provider == "aws" ? 1 : 0
  cluster_name    = aws_eks_cluster.honua[0].name
  node_group_name = "${var.cluster_name}-arm-${var.environment}"
  node_role_arn   = aws_iam_role.eks_node_group[0].arn
  subnet_ids      = var.private_subnet_ids
  version         = var.kubernetes_version

  scaling_config {
    desired_size = var.node_group_desired_size
    max_size     = var.node_group_max_size
    min_size     = var.node_group_min_size
  }

  update_config {
    max_unavailable_percentage = 33
  }

  instance_types = var.use_spot_instances ? ["t4g.xlarge", "t4g.2xlarge"] : ["m7g.xlarge", "m7g.2xlarge"]
  capacity_type  = var.use_spot_instances ? "SPOT" : "ON_DEMAND"
  ami_type       = "AL2_ARM_64"
  disk_size      = 100

  labels = {
    role        = "honua-worker"
    environment = var.environment
    arch        = "arm64"
  }

  taints {
    key    = "honua.io/build-node"
    value  = "true"
    effect = "NO_SCHEDULE"
  }

  tags = merge(
    var.tags,
    {
      Name                = "${var.cluster_name}-arm-${var.environment}"
      "k8s.io/cluster-autoscaler/${aws_eks_cluster.honua[0].name}" = "owned"
      "k8s.io/cluster-autoscaler/enabled" = "true"
    }
  )

  depends_on = [
    aws_iam_role_policy_attachment.eks_node_policy,
    aws_iam_role_policy_attachment.eks_cni_policy,
    aws_iam_role_policy_attachment.eks_ecr_policy,
  ]
}

# EKS IAM Roles
resource "aws_iam_role" "eks_cluster" {
  count = var.cloud_provider == "aws" ? 1 : 0
  name  = "${var.cluster_name}-eks-cluster-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "eks.amazonaws.com"
      }
    }]
  })

  tags = var.tags
}

resource "aws_iam_role_policy_attachment" "eks_cluster_policy" {
  count      = var.cloud_provider == "aws" ? 1 : 0
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKSClusterPolicy"
  role       = aws_iam_role.eks_cluster[0].name
}

resource "aws_iam_role_policy_attachment" "eks_service_policy" {
  count      = var.cloud_provider == "aws" ? 1 : 0
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKSServicePolicy"
  role       = aws_iam_role.eks_cluster[0].name
}

resource "aws_iam_role" "eks_node_group" {
  count = var.cloud_provider == "aws" ? 1 : 0
  name  = "${var.cluster_name}-eks-node-${var.environment}"

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

resource "aws_iam_role_policy_attachment" "eks_node_policy" {
  count      = var.cloud_provider == "aws" ? 1 : 0
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKSWorkerNodePolicy"
  role       = aws_iam_role.eks_node_group[0].name
}

resource "aws_iam_role_policy_attachment" "eks_cni_policy" {
  count      = var.cloud_provider == "aws" ? 1 : 0
  policy_arn = "arn:aws:iam::aws:policy/AmazonEKS_CNI_Policy"
  role       = aws_iam_role.eks_node_group[0].name
}

resource "aws_iam_role_policy_attachment" "eks_ecr_policy" {
  count      = var.cloud_provider == "aws" ? 1 : 0
  policy_arn = "arn:aws:iam::aws:policy/AmazonEC2ContainerRegistryReadOnly"
  role       = aws_iam_role.eks_node_group[0].name
}

# EKS Security Group
resource "aws_security_group" "eks_cluster" {
  count       = var.cloud_provider == "aws" ? 1 : 0
  name        = "${var.cluster_name}-eks-cluster-${var.environment}"
  description = "Security group for EKS cluster control plane"
  vpc_id      = var.vpc_id

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(
    var.tags,
    {
      Name = "${var.cluster_name}-eks-cluster-${var.environment}"
    }
  )
}

# ==================== Azure AKS ====================
resource "azurerm_kubernetes_cluster" "honua" {
  count               = var.cloud_provider == "azure" ? 1 : 0
  name                = "${var.cluster_name}-${var.environment}"
  location            = var.azure_location
  resource_group_name = var.resource_group_name
  dns_prefix          = "${var.cluster_name}-${var.environment}"
  kubernetes_version  = var.kubernetes_version

  default_node_pool {
    name                = "system"
    vm_size             = var.use_spot_instances ? "Standard_D4ps_v5" : "Standard_D8ps_v5"
    vnet_subnet_id      = var.subnet_ids[0]
    enable_auto_scaling = true
    min_count           = var.node_group_min_size
    max_count           = var.node_group_max_size
    os_disk_size_gb     = 100
    os_sku              = "Ubuntu"
    orchestrator_version = var.kubernetes_version

    node_labels = {
      role        = "system"
      environment = var.environment
      arch        = "arm64"
    }
  }

  identity {
    type = "SystemAssigned"
  }

  network_profile {
    network_plugin     = "azure"
    network_policy     = "calico"
    service_cidr       = "10.240.0.0/16"
    dns_service_ip     = "10.240.0.10"
    load_balancer_sku  = "standard"
  }

  oms_agent {
    log_analytics_workspace_id = var.log_analytics_workspace_id
  }

  azure_policy_enabled = true

  key_vault_secrets_provider {
    secret_rotation_enabled  = true
    secret_rotation_interval = "2m"
  }

  tags = merge(
    var.tags,
    {
      Environment = var.environment
      ManagedBy   = "Terraform"
      Project     = "Honua"
    }
  )
}

# AKS Node Pool - Ampere ARM instances
resource "azurerm_kubernetes_cluster_node_pool" "honua_arm" {
  count                 = var.cloud_provider == "azure" ? 1 : 0
  name                  = "honuaarm"
  kubernetes_cluster_id = azurerm_kubernetes_cluster.honua[0].id
  vm_size               = var.use_spot_instances ? "Standard_D4ps_v5" : "Standard_D8ps_v5"
  vnet_subnet_id        = var.subnet_ids[0]
  enable_auto_scaling   = true
  min_count             = var.node_group_min_size
  max_count             = var.node_group_max_size
  os_disk_size_gb       = 100
  orchestrator_version  = var.kubernetes_version
  priority              = var.use_spot_instances ? "Spot" : "Regular"
  spot_max_price        = var.use_spot_instances ? -1 : null
  eviction_policy       = var.use_spot_instances ? "Delete" : null

  node_labels = {
    role        = "honua-worker"
    environment = var.environment
    arch        = "arm64"
  }

  node_taints = [
    "honua.io/build-node=true:NoSchedule"
  ]

  tags = var.tags
}

# ==================== GCP GKE ====================
resource "google_container_cluster" "honua" {
  count    = var.cloud_provider == "gcp" ? 1 : 0
  name     = "${var.cluster_name}-${var.environment}"
  location = var.gcp_region
  project  = var.gcp_project_id

  # We can't create a cluster with no node pool defined, but we want to only use
  # separately managed node pools. So we create the smallest possible default
  # node pool and immediately delete it.
  remove_default_node_pool = true
  initial_node_count       = 1

  min_master_version = var.kubernetes_version

  network    = var.vpc_id
  subnetwork = var.subnet_ids[0]

  ip_allocation_policy {
    cluster_secondary_range_name  = "pods"
    services_secondary_range_name = "services"
  }

  master_auth {
    client_certificate_config {
      issue_client_certificate = false
    }
  }

  network_policy {
    enabled  = true
    provider = "CALICO"
  }

  addons_config {
    http_load_balancing {
      disabled = false
    }
    horizontal_pod_autoscaling {
      disabled = false
    }
    network_policy_config {
      disabled = false
    }
  }

  workload_identity_config {
    workload_pool = "${var.gcp_project_id}.svc.id.goog"
  }

  database_encryption {
    state    = "ENCRYPTED"
    key_name = var.kms_key_arn
  }

  logging_config {
    enable_components = [
      "SYSTEM_COMPONENTS",
      "WORKLOADS"
    ]
  }

  monitoring_config {
    enable_components = [
      "SYSTEM_COMPONENTS",
      "WORKLOADS"
    ]
    managed_prometheus {
      enabled = true
    }
  }

  maintenance_policy {
    daily_maintenance_window {
      start_time = "03:00"
    }
  }

  resource_labels = merge(
    var.tags,
    {
      environment = var.environment
      managed_by  = "terraform"
      project     = "honua"
    }
  )
}

# GKE Node Pool - Tau ARM instances
resource "google_container_node_pool" "honua_arm" {
  count      = var.cloud_provider == "gcp" ? 1 : 0
  name       = "${var.cluster_name}-arm-${var.environment}"
  location   = var.gcp_region
  cluster    = google_container_cluster.honua[0].name
  version    = var.kubernetes_version
  project    = var.gcp_project_id

  autoscaling {
    min_node_count = var.node_group_min_size
    max_node_count = var.node_group_max_size
  }

  management {
    auto_repair  = true
    auto_upgrade = true
  }

  node_config {
    preemptible  = var.use_spot_instances
    machine_type = var.use_spot_instances ? "t2a-standard-4" : "t2a-standard-8"
    disk_size_gb = 100
    disk_type    = "pd-standard"

    labels = {
      role        = "honua-worker"
      environment = var.environment
      arch        = "arm64"
    }

    taint {
      key    = "honua.io/build-node"
      value  = "true"
      effect = "NO_SCHEDULE"
    }

    oauth_scopes = [
      "https://www.googleapis.com/auth/cloud-platform"
    ]

    shielded_instance_config {
      enable_secure_boot          = true
      enable_integrity_monitoring = true
    }

    workload_metadata_config {
      mode = "GKE_METADATA"
    }

    metadata = {
      disable-legacy-endpoints = "true"
    }

    tags = ["honua-k8s-node", var.environment]
  }
}

# ==================== Cluster Autoscaler ====================
resource "kubernetes_service_account" "cluster_autoscaler" {
  count = var.enable_cluster_autoscaler ? 1 : 0

  metadata {
    name      = "cluster-autoscaler"
    namespace = "kube-system"
    labels = {
      "k8s-app" = "cluster-autoscaler"
    }
  }
}

resource "kubernetes_cluster_role" "cluster_autoscaler" {
  count = var.enable_cluster_autoscaler ? 1 : 0

  metadata {
    name = "cluster-autoscaler"
  }

  rule {
    api_groups = [""]
    resources  = ["events", "endpoints"]
    verbs      = ["create", "patch"]
  }

  rule {
    api_groups = [""]
    resources  = ["pods/eviction"]
    verbs      = ["create"]
  }

  rule {
    api_groups = [""]
    resources  = ["pods/status"]
    verbs      = ["update"]
  }

  rule {
    api_groups = [""]
    resources  = ["endpoints"]
    resource_names = ["cluster-autoscaler"]
    verbs      = ["get", "update"]
  }

  rule {
    api_groups = [""]
    resources  = ["nodes"]
    verbs      = ["watch", "list", "get", "update"]
  }

  rule {
    api_groups = [""]
    resources = [
      "pods",
      "services",
      "replicationcontrollers",
      "persistentvolumeclaims",
      "persistentvolumes"
    ]
    verbs = ["watch", "list", "get"]
  }

  rule {
    api_groups = ["extensions"]
    resources  = ["replicasets", "daemonsets"]
    verbs      = ["watch", "list", "get"]
  }

  rule {
    api_groups = ["policy"]
    resources  = ["poddisruptionbudgets"]
    verbs      = ["watch", "list"]
  }

  rule {
    api_groups = ["apps"]
    resources  = ["statefulsets", "replicasets", "daemonsets"]
    verbs      = ["watch", "list", "get"]
  }

  rule {
    api_groups = ["storage.k8s.io"]
    resources  = ["storageclasses", "csinodes", "csidrivers", "csistoragecapacities"]
    verbs      = ["watch", "list", "get"]
  }

  rule {
    api_groups = ["batch", "extensions"]
    resources  = ["jobs"]
    verbs      = ["get", "list", "watch", "patch"]
  }

  rule {
    api_groups = ["coordination.k8s.io"]
    resources  = ["leases"]
    verbs      = ["create"]
  }

  rule {
    api_groups = ["coordination.k8s.io"]
    resource_names = ["cluster-autoscaler"]
    resources  = ["leases"]
    verbs      = ["get", "update"]
  }
}

# ==================== Network Policies ====================
resource "kubernetes_network_policy" "default_deny" {
  count = var.enable_network_policies ? 1 : 0

  metadata {
    name      = "default-deny-all"
    namespace = "default"
  }

  spec {
    pod_selector {}
    policy_types = ["Ingress", "Egress"]
  }
}

resource "kubernetes_network_policy" "allow_dns" {
  count = var.enable_network_policies ? 1 : 0

  metadata {
    name      = "allow-dns"
    namespace = "default"
  }

  spec {
    pod_selector {}
    policy_types = ["Egress"]

    egress {
      to {
        namespace_selector {
          match_labels = {
            name = "kube-system"
          }
        }
      }

      ports {
        protocol = "UDP"
        port     = "53"
      }
    }
  }
}
