# Networking Module - Multi-Cloud Support
# Supports AWS VPC, Azure VNet, and GCP VPC

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

# ==================== AWS VPC ====================
resource "aws_vpc" "honua" {
  count                = var.cloud_provider == "aws" ? 1 : 0
  cidr_block           = var.vpc_cidr
  enable_dns_hostnames = true
  enable_dns_support   = true

  tags = merge(
    var.tags,
    {
      Name        = "${var.network_name}-${var.environment}"
      Environment = var.environment
    }
  )
}

# Internet Gateway
resource "aws_internet_gateway" "honua" {
  count  = var.cloud_provider == "aws" ? 1 : 0
  vpc_id = aws_vpc.honua[0].id

  tags = merge(
    var.tags,
    {
      Name = "${var.network_name}-igw-${var.environment}"
    }
  )
}

# Public Subnets
resource "aws_subnet" "public" {
  count                   = var.cloud_provider == "aws" ? length(var.availability_zones) : 0
  vpc_id                  = aws_vpc.honua[0].id
  cidr_block              = cidrsubnet(var.vpc_cidr, 4, count.index)
  availability_zone       = var.availability_zones[count.index]
  map_public_ip_on_launch = true

  tags = merge(
    var.tags,
    {
      Name                                           = "${var.network_name}-public-${count.index + 1}-${var.environment}"
      "kubernetes.io/role/elb"                      = "1"
      "kubernetes.io/cluster/${var.cluster_name}"   = "shared"
    }
  )
}

# Private Subnets
resource "aws_subnet" "private" {
  count             = var.cloud_provider == "aws" ? length(var.availability_zones) : 0
  vpc_id            = aws_vpc.honua[0].id
  cidr_block        = cidrsubnet(var.vpc_cidr, 4, count.index + length(var.availability_zones))
  availability_zone = var.availability_zones[count.index]

  tags = merge(
    var.tags,
    {
      Name                                           = "${var.network_name}-private-${count.index + 1}-${var.environment}"
      "kubernetes.io/role/internal-elb"             = "1"
      "kubernetes.io/cluster/${var.cluster_name}"   = "shared"
    }
  )
}

# Database Subnets
resource "aws_subnet" "database" {
  count             = var.cloud_provider == "aws" ? length(var.availability_zones) : 0
  vpc_id            = aws_vpc.honua[0].id
  cidr_block        = cidrsubnet(var.vpc_cidr, 4, count.index + 2 * length(var.availability_zones))
  availability_zone = var.availability_zones[count.index]

  tags = merge(
    var.tags,
    {
      Name = "${var.network_name}-database-${count.index + 1}-${var.environment}"
      Tier = "Database"
    }
  )
}

# Elastic IPs for NAT Gateways
resource "aws_eip" "nat" {
  count  = var.cloud_provider == "aws" ? length(var.availability_zones) : 0
  domain = "vpc"

  tags = merge(
    var.tags,
    {
      Name = "${var.network_name}-nat-eip-${count.index + 1}-${var.environment}"
    }
  )

  depends_on = [aws_internet_gateway.honua]
}

# NAT Gateways
resource "aws_nat_gateway" "honua" {
  count         = var.cloud_provider == "aws" ? length(var.availability_zones) : 0
  allocation_id = aws_eip.nat[count.index].id
  subnet_id     = aws_subnet.public[count.index].id

  tags = merge(
    var.tags,
    {
      Name = "${var.network_name}-nat-${count.index + 1}-${var.environment}"
    }
  )

  depends_on = [aws_internet_gateway.honua]
}

# Public Route Table
resource "aws_route_table" "public" {
  count  = var.cloud_provider == "aws" ? 1 : 0
  vpc_id = aws_vpc.honua[0].id

  route {
    cidr_block = "0.0.0.0/0"
    gateway_id = aws_internet_gateway.honua[0].id
  }

  tags = merge(
    var.tags,
    {
      Name = "${var.network_name}-public-rt-${var.environment}"
    }
  )
}

resource "aws_route_table_association" "public" {
  count          = var.cloud_provider == "aws" ? length(var.availability_zones) : 0
  subnet_id      = aws_subnet.public[count.index].id
  route_table_id = aws_route_table.public[0].id
}

# Private Route Tables
resource "aws_route_table" "private" {
  count  = var.cloud_provider == "aws" ? length(var.availability_zones) : 0
  vpc_id = aws_vpc.honua[0].id

  route {
    cidr_block     = "0.0.0.0/0"
    nat_gateway_id = aws_nat_gateway.honua[count.index].id
  }

  tags = merge(
    var.tags,
    {
      Name = "${var.network_name}-private-rt-${count.index + 1}-${var.environment}"
    }
  )
}

resource "aws_route_table_association" "private" {
  count          = var.cloud_provider == "aws" ? length(var.availability_zones) : 0
  subnet_id      = aws_subnet.private[count.index].id
  route_table_id = aws_route_table.private[count.index].id
}

# Database Route Tables
resource "aws_route_table" "database" {
  count  = var.cloud_provider == "aws" ? 1 : 0
  vpc_id = aws_vpc.honua[0].id

  tags = merge(
    var.tags,
    {
      Name = "${var.network_name}-database-rt-${var.environment}"
    }
  )
}

resource "aws_route_table_association" "database" {
  count          = var.cloud_provider == "aws" ? length(var.availability_zones) : 0
  subnet_id      = aws_subnet.database[count.index].id
  route_table_id = aws_route_table.database[0].id
}

# VPC Flow Logs
resource "aws_flow_log" "honua" {
  count                = var.cloud_provider == "aws" ? 1 : 0
  iam_role_arn         = aws_iam_role.flow_logs[0].arn
  log_destination      = aws_cloudwatch_log_group.flow_logs[0].arn
  traffic_type         = "ALL"
  vpc_id               = aws_vpc.honua[0].id

  tags = var.tags
}

resource "aws_cloudwatch_log_group" "flow_logs" {
  count             = var.cloud_provider == "aws" ? 1 : 0
  name              = "/aws/vpc/flow-logs/${var.network_name}-${var.environment}"
  retention_in_days = 7

  tags = var.tags
}

resource "aws_iam_role" "flow_logs" {
  count = var.cloud_provider == "aws" ? 1 : 0
  name  = "${var.network_name}-flow-logs-${var.environment}"

  assume_role_policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = "sts:AssumeRole"
      Effect = "Allow"
      Principal = {
        Service = "vpc-flow-logs.amazonaws.com"
      }
    }]
  })

  tags = var.tags
}

resource "aws_iam_role_policy" "flow_logs" {
  count = var.cloud_provider == "aws" ? 1 : 0
  name  = "flow-logs-policy"
  role  = aws_iam_role.flow_logs[0].id

  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Action = [
        "logs:CreateLogGroup",
        "logs:CreateLogStream",
        "logs:PutLogEvents",
        "logs:DescribeLogGroups",
        "logs:DescribeLogStreams"
      ]
      Effect   = "Allow"
      Resource = "*"
    }]
  })
}

# ==================== Azure VNet ====================
resource "azurerm_virtual_network" "honua" {
  count               = var.cloud_provider == "azure" ? 1 : 0
  name                = "${var.network_name}-${var.environment}"
  resource_group_name = var.resource_group_name
  location            = var.azure_location
  address_space       = [var.vpc_cidr]

  tags = merge(
    var.tags,
    {
      Environment = var.environment
    }
  )
}

# Azure Subnets
resource "azurerm_subnet" "public" {
  count                = var.cloud_provider == "azure" ? length(var.availability_zones) : 0
  name                 = "${var.network_name}-public-${count.index + 1}"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.honua[0].name
  address_prefixes     = [cidrsubnet(var.vpc_cidr, 4, count.index)]
}

resource "azurerm_subnet" "private" {
  count                = var.cloud_provider == "azure" ? length(var.availability_zones) : 0
  name                 = "${var.network_name}-private-${count.index + 1}"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.honua[0].name
  address_prefixes     = [cidrsubnet(var.vpc_cidr, 4, count.index + length(var.availability_zones))]

  delegation {
    name = "aks-delegation"

    service_delegation {
      name = "Microsoft.ContainerService/managedClusters"
      actions = [
        "Microsoft.Network/virtualNetworks/subnets/join/action"
      ]
    }
  }
}

resource "azurerm_subnet" "database" {
  count                = var.cloud_provider == "azure" ? length(var.availability_zones) : 0
  name                 = "${var.network_name}-database-${count.index + 1}"
  resource_group_name  = var.resource_group_name
  virtual_network_name = azurerm_virtual_network.honua[0].name
  address_prefixes     = [cidrsubnet(var.vpc_cidr, 4, count.index + 2 * length(var.availability_zones))]

  delegation {
    name = "postgres-delegation"

    service_delegation {
      name = "Microsoft.DBforPostgreSQL/flexibleServers"
      actions = [
        "Microsoft.Network/virtualNetworks/subnets/join/action"
      ]
    }
  }
}

# Azure NAT Gateway
resource "azurerm_public_ip" "nat" {
  count               = var.cloud_provider == "azure" ? length(var.availability_zones) : 0
  name                = "${var.network_name}-nat-ip-${count.index + 1}"
  resource_group_name = var.resource_group_name
  location            = var.azure_location
  allocation_method   = "Static"
  sku                 = "Standard"

  tags = var.tags
}

resource "azurerm_nat_gateway" "honua" {
  count               = var.cloud_provider == "azure" ? length(var.availability_zones) : 0
  name                = "${var.network_name}-nat-${count.index + 1}"
  resource_group_name = var.resource_group_name
  location            = var.azure_location
  sku_name            = "Standard"

  tags = var.tags
}

resource "azurerm_nat_gateway_public_ip_association" "honua" {
  count                = var.cloud_provider == "azure" ? length(var.availability_zones) : 0
  nat_gateway_id       = azurerm_nat_gateway.honua[count.index].id
  public_ip_address_id = azurerm_public_ip.nat[count.index].id
}

resource "azurerm_subnet_nat_gateway_association" "private" {
  count          = var.cloud_provider == "azure" ? length(var.availability_zones) : 0
  subnet_id      = azurerm_subnet.private[count.index].id
  nat_gateway_id = azurerm_nat_gateway.honua[count.index].id
}

# Azure Network Security Groups
resource "azurerm_network_security_group" "honua" {
  count               = var.cloud_provider == "azure" ? 1 : 0
  name                = "${var.network_name}-nsg-${var.environment}"
  resource_group_name = var.resource_group_name
  location            = var.azure_location

  tags = var.tags
}

# ==================== GCP VPC ====================
resource "google_compute_network" "honua" {
  count                   = var.cloud_provider == "gcp" ? 1 : 0
  name                    = "${var.network_name}-${var.environment}"
  auto_create_subnetworks = false
  project                 = var.gcp_project_id
}

# GCP Subnets
resource "google_compute_subnetwork" "public" {
  count         = var.cloud_provider == "gcp" ? length(var.availability_zones) : 0
  name          = "${var.network_name}-public-${count.index + 1}"
  ip_cidr_range = cidrsubnet(var.vpc_cidr, 4, count.index)
  region        = var.gcp_region
  network       = google_compute_network.honua[0].id
  project       = var.gcp_project_id
}

resource "google_compute_subnetwork" "private" {
  count         = var.cloud_provider == "gcp" ? length(var.availability_zones) : 0
  name          = "${var.network_name}-private-${count.index + 1}"
  ip_cidr_range = cidrsubnet(var.vpc_cidr, 4, count.index + length(var.availability_zones))
  region        = var.gcp_region
  network       = google_compute_network.honua[0].id
  project       = var.gcp_project_id

  secondary_ip_range {
    range_name    = "pods"
    ip_cidr_range = cidrsubnet(var.vpc_cidr, 2, count.index + 1)
  }

  secondary_ip_range {
    range_name    = "services"
    ip_cidr_range = cidrsubnet(var.vpc_cidr, 4, count.index + 3 * length(var.availability_zones))
  }

  private_ip_google_access = true
}

# GCP Cloud Router
resource "google_compute_router" "honua" {
  count   = var.cloud_provider == "gcp" ? 1 : 0
  name    = "${var.network_name}-router-${var.environment}"
  region  = var.gcp_region
  network = google_compute_network.honua[0].id
  project = var.gcp_project_id
}

# GCP Cloud NAT
resource "google_compute_router_nat" "honua" {
  count                              = var.cloud_provider == "gcp" ? 1 : 0
  name                               = "${var.network_name}-nat-${var.environment}"
  router                             = google_compute_router.honua[0].name
  region                             = var.gcp_region
  nat_ip_allocate_option            = "AUTO_ONLY"
  source_subnetwork_ip_ranges_to_nat = "ALL_SUBNETWORKS_ALL_IP_RANGES"
  project                            = var.gcp_project_id

  log_config {
    enable = true
    filter = "ERRORS_ONLY"
  }
}

# GCP Firewall Rules
resource "google_compute_firewall" "allow_internal" {
  count   = var.cloud_provider == "gcp" ? 1 : 0
  name    = "${var.network_name}-allow-internal-${var.environment}"
  network = google_compute_network.honua[0].name
  project = var.gcp_project_id

  allow {
    protocol = "tcp"
    ports    = ["0-65535"]
  }

  allow {
    protocol = "udp"
    ports    = ["0-65535"]
  }

  allow {
    protocol = "icmp"
  }

  source_ranges = [var.vpc_cidr]
}
