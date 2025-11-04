# Database Module - Multi-Cloud PostgreSQL Support
# Supports AWS RDS, Azure Database for PostgreSQL, and GCP Cloud SQL

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
    random = {
      source  = "hashicorp/random"
      version = "~> 3.5"
    }
  }
}

# Generate random password for database
resource "random_password" "db_password" {
  length  = 32
  special = true
  override_special = "!#$%&*()-_=+[]{}<>:?"
}

# ==================== AWS RDS PostgreSQL ====================
resource "aws_db_subnet_group" "honua" {
  count      = var.cloud_provider == "aws" ? 1 : 0
  name       = "${var.db_name}-${var.environment}"
  subnet_ids = var.subnet_ids

  tags = merge(
    var.tags,
    {
      Name        = "${var.db_name}-${var.environment}"
      Environment = var.environment
    }
  )
}

resource "aws_db_parameter_group" "honua" {
  count  = var.cloud_provider == "aws" ? 1 : 0
  name   = "${var.db_name}-pg15-${var.environment}"
  family = "postgres15"

  parameter {
    name  = "shared_preload_libraries"
    value = "pg_stat_statements,pgaudit"
  }

  parameter {
    name  = "log_statement"
    value = "all"
  }

  parameter {
    name  = "log_min_duration_statement"
    value = "1000"
  }

  parameter {
    name  = "max_connections"
    value = var.environment == "production" ? "500" : "200"
  }

  tags = var.tags
}

resource "aws_db_instance" "honua_primary" {
  count                  = var.cloud_provider == "aws" ? 1 : 0
  identifier             = "${var.db_name}-${var.environment}"
  engine                 = "postgres"
  engine_version         = var.postgres_version
  instance_class         = var.db_instance_class
  allocated_storage      = var.allocated_storage
  max_allocated_storage  = var.max_allocated_storage
  storage_type           = "gp3"
  storage_encrypted      = true
  kms_key_id            = var.kms_key_arn

  db_name  = var.database_name
  username = var.master_username
  password = random_password.db_password.result
  port     = 5432

  multi_az               = var.environment == "production"
  db_subnet_group_name   = aws_db_subnet_group.honua[0].name
  parameter_group_name   = aws_db_parameter_group.honua[0].name
  vpc_security_group_ids = [aws_security_group.rds[0].id]

  backup_retention_period = var.backup_retention_days
  backup_window          = "03:00-04:00"
  maintenance_window     = "mon:04:00-mon:05:00"

  enabled_cloudwatch_logs_exports = ["postgresql", "upgrade"]

  performance_insights_enabled    = true
  performance_insights_kms_key_id = var.kms_key_arn

  deletion_protection = var.environment == "production"
  skip_final_snapshot = var.environment != "production"
  final_snapshot_identifier = var.environment == "production" ? "${var.db_name}-${var.environment}-final-${formatdate("YYYY-MM-DD-hhmm", timestamp())}" : null

  copy_tags_to_snapshot = true

  tags = merge(
    var.tags,
    {
      Name        = "${var.db_name}-${var.environment}"
      Environment = var.environment
      ManagedBy   = "Terraform"
    }
  )
}

# RDS Read Replicas for production
resource "aws_db_instance" "honua_replica" {
  count                = var.cloud_provider == "aws" && var.environment == "production" ? var.read_replica_count : 0
  identifier           = "${var.db_name}-${var.environment}-replica-${count.index + 1}"
  replicate_source_db  = aws_db_instance.honua_primary[0].identifier
  instance_class       = var.replica_instance_class
  storage_encrypted    = true
  kms_key_id          = var.kms_key_arn

  auto_minor_version_upgrade = true
  publicly_accessible        = false

  performance_insights_enabled    = true
  performance_insights_kms_key_id = var.kms_key_arn

  skip_final_snapshot = true

  tags = merge(
    var.tags,
    {
      Name        = "${var.db_name}-${var.environment}-replica-${count.index + 1}"
      Environment = var.environment
      Role        = "read-replica"
    }
  )
}

# RDS Security Group
resource "aws_security_group" "rds" {
  count       = var.cloud_provider == "aws" ? 1 : 0
  name        = "${var.db_name}-rds-${var.environment}"
  description = "Security group for RDS PostgreSQL"
  vpc_id      = var.vpc_id

  ingress {
    description     = "PostgreSQL from VPC"
    from_port       = 5432
    to_port         = 5432
    protocol        = "tcp"
    cidr_blocks     = [var.vpc_cidr]
  }

  egress {
    from_port   = 0
    to_port     = 0
    protocol    = "-1"
    cidr_blocks = ["0.0.0.0/0"]
  }

  tags = merge(
    var.tags,
    {
      Name = "${var.db_name}-rds-${var.environment}"
    }
  )
}

# ==================== Azure Database for PostgreSQL ====================
resource "azurerm_postgresql_flexible_server" "honua" {
  count                  = var.cloud_provider == "azure" ? 1 : 0
  name                   = "${var.db_name}-${var.environment}"
  resource_group_name    = var.resource_group_name
  location               = var.azure_location
  version                = var.postgres_version
  delegated_subnet_id    = var.subnet_ids[0]
  private_dns_zone_id    = var.private_dns_zone_id

  administrator_login    = var.master_username
  administrator_password = random_password.db_password.result

  sku_name   = var.db_instance_class
  storage_mb = var.allocated_storage * 1024

  backup_retention_days        = var.backup_retention_days
  geo_redundant_backup_enabled = var.environment == "production"

  high_availability {
    mode                      = var.environment == "production" ? "ZoneRedundant" : "Disabled"
    standby_availability_zone = var.environment == "production" ? "2" : null
  }

  maintenance_window {
    day_of_week  = 1
    start_hour   = 4
    start_minute = 0
  }

  tags = merge(
    var.tags,
    {
      Environment = var.environment
      ManagedBy   = "Terraform"
    }
  )
}

resource "azurerm_postgresql_flexible_server_database" "honua" {
  count     = var.cloud_provider == "azure" ? 1 : 0
  name      = var.database_name
  server_id = azurerm_postgresql_flexible_server.honua[0].id
  collation = "en_US.utf8"
  charset   = "utf8"
}

resource "azurerm_postgresql_flexible_server_configuration" "honua" {
  for_each = var.cloud_provider == "azure" ? {
    max_connections           = var.environment == "production" ? "500" : "200"
    shared_preload_libraries  = "pg_stat_statements"
    log_min_duration_statement = "1000"
  } : {}

  name      = each.key
  server_id = azurerm_postgresql_flexible_server.honua[0].id
  value     = each.value
}

# ==================== GCP Cloud SQL for PostgreSQL ====================
resource "google_sql_database_instance" "honua" {
  count            = var.cloud_provider == "gcp" ? 1 : 0
  name             = "${var.db_name}-${var.environment}"
  database_version = "POSTGRES_15"
  region           = var.gcp_region
  project          = var.gcp_project_id

  settings {
    tier              = var.db_instance_class
    availability_type = var.environment == "production" ? "REGIONAL" : "ZONAL"
    disk_type         = "PD_SSD"
    disk_size         = var.allocated_storage
    disk_autoresize       = true
    disk_autoresize_limit = var.max_allocated_storage

    backup_configuration {
      enabled                        = true
      start_time                     = "03:00"
      point_in_time_recovery_enabled = true
      transaction_log_retention_days = var.backup_retention_days
      backup_retention_settings {
        retained_backups = var.backup_retention_days
        retention_unit   = "COUNT"
      }
    }

    ip_configuration {
      ipv4_enabled    = false
      private_network = var.vpc_id
      require_ssl     = true
    }

    database_flags {
      name  = "max_connections"
      value = var.environment == "production" ? "500" : "200"
    }

    database_flags {
      name  = "log_min_duration_statement"
      value = "1000"
    }

    database_flags {
      name  = "cloudsql.enable_pg_stat_statements"
      value = "on"
    }

    maintenance_window {
      day          = 1
      hour         = 4
      update_track = "stable"
    }

    insights_config {
      query_insights_enabled  = true
      query_plans_per_minute  = 5
      query_string_length     = 1024
      record_application_tags = true
    }

    user_labels = merge(
      var.tags,
      {
        environment = var.environment
        managed_by  = "terraform"
      }
    )
  }

  deletion_protection = var.environment == "production"
}

resource "google_sql_database" "honua" {
  count    = var.cloud_provider == "gcp" ? 1 : 0
  name     = var.database_name
  instance = google_sql_database_instance.honua[0].name
  project  = var.gcp_project_id
}

resource "google_sql_user" "honua" {
  count    = var.cloud_provider == "gcp" ? 1 : 0
  name     = var.master_username
  instance = google_sql_database_instance.honua[0].name
  password = random_password.db_password.result
  project  = var.gcp_project_id
}

# ==================== PgBouncer Connection Pooling ====================
# This would typically be deployed as a Kubernetes deployment
# Placeholder for PgBouncer configuration stored in Secret Manager

resource "aws_secretsmanager_secret" "pgbouncer_config" {
  count = var.cloud_provider == "aws" && var.enable_pgbouncer ? 1 : 0
  name  = "${var.db_name}-pgbouncer-${var.environment}"

  tags = var.tags
}

resource "aws_secretsmanager_secret_version" "pgbouncer_config" {
  count     = var.cloud_provider == "aws" && var.enable_pgbouncer ? 1 : 0
  secret_id = aws_secretsmanager_secret.pgbouncer_config[0].id
  secret_string = jsonencode({
    databases = {
      "${var.database_name}" = {
        host     = aws_db_instance.honua_primary[0].address
        port     = 5432
        dbname   = var.database_name
        user     = var.master_username
        password = random_password.db_password.result
      }
    }
    pgbouncer = {
      pool_mode           = "transaction"
      max_client_conn     = 1000
      default_pool_size   = 25
      min_pool_size       = 10
      reserve_pool_size   = 5
      reserve_pool_timeout = 3
      server_lifetime     = 3600
      server_idle_timeout = 600
    }
  })
}

# Store master password in secrets manager
resource "aws_secretsmanager_secret" "db_password" {
  count = var.cloud_provider == "aws" ? 1 : 0
  name  = "${var.db_name}-master-password-${var.environment}"

  tags = var.tags
}

resource "aws_secretsmanager_secret_version" "db_password" {
  count     = var.cloud_provider == "aws" ? 1 : 0
  secret_id = aws_secretsmanager_secret.db_password[0].id
  secret_string = jsonencode({
    username = var.master_username
    password = random_password.db_password.result
    host     = aws_db_instance.honua_primary[0].address
    port     = 5432
    dbname   = var.database_name
    connection_string = "postgresql://${var.master_username}:${random_password.db_password.result}@${aws_db_instance.honua_primary[0].address}:5432/${var.database_name}"
  })
}

resource "azurerm_key_vault_secret" "db_password" {
  count        = var.cloud_provider == "azure" ? 1 : 0
  name         = "${var.db_name}-master-password"
  value        = random_password.db_password.result
  key_vault_id = var.key_vault_id

  tags = var.tags
}

resource "google_secret_manager_secret" "db_password" {
  count     = var.cloud_provider == "gcp" ? 1 : 0
  secret_id = "${var.db_name}-master-password-${var.environment}"
  project   = var.gcp_project_id

  replication {
    automatic = true
  }

  labels = var.tags
}

resource "google_secret_manager_secret_version" "db_password" {
  count  = var.cloud_provider == "gcp" ? 1 : 0
  secret = google_secret_manager_secret.db_password[0].id
  secret_data = jsonencode({
    username = var.master_username
    password = random_password.db_password.result
    host     = google_sql_database_instance.honua[0].private_ip_address
    port     = 5432
    dbname   = var.database_name
  })
}
