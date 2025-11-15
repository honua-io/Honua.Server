# Example: Read Replica Configuration for Tier 3 Deployments
#
# This configuration demonstrates how to set up read replica routing
# to offload read traffic from the primary database.
#
# Architecture:
# - 1 primary database (handles all writes + reads when replicas unavailable)
# - 2+ read replicas (handle read operations in round-robin fashion)
#
# Use case: High-traffic deployments with 90%+ read operations

honua {
  version     = "1.0"
  environment = "production"
  log_level   = "information"
}

# Primary database - handles all write operations
data_source "primary_db" {
  provider   = "postgresql"
  connection = "${env:DATABASE_PRIMARY_CONNECTION}"

  # Health check query to verify connectivity
  health_check = "SELECT 1"

  # Connection pool settings for primary
  pool {
    min_size = 5
    max_size = 50
    timeout  = 30
  }

  # Primary is NOT read-only
  read_only = false
}

# Read Replica 1 - offloads read operations
data_source "replica_1" {
  provider   = "postgresql"
  connection = "${env:DATABASE_REPLICA_1_CONNECTION}"

  health_check = "SELECT 1"

  # Smaller pool size for replicas (3 replicas share the load)
  pool {
    min_size = 2
    max_size = 20
    timeout  = 30
  }

  # Mark as read-only replica
  read_only = true

  # Optional: Skip this replica if replication lag exceeds 5 seconds
  max_replication_lag = 5
}

# Read Replica 2 - additional capacity
data_source "replica_2" {
  provider   = "postgresql"
  connection = "${env:DATABASE_REPLICA_2_CONNECTION}"

  health_check = "SELECT 1"

  pool {
    min_size = 2
    max_size = 20
    timeout  = 30
  }

  read_only             = true
  max_replication_lag   = 5
}

# Read Replica 3 - cross-region replica (higher latency acceptable)
data_source "replica_3_us_west" {
  provider   = "postgresql"
  connection = "${env:DATABASE_REPLICA_3_CONNECTION}"

  health_check = "SELECT 1"

  pool {
    min_size = 1
    max_size = 10
    timeout  = 45  # Higher timeout for cross-region
  }

  read_only             = true
  max_replication_lag   = 10  # Allow more lag for cross-region
}

# Example service using the primary data source
service "features" {
  type         = "ogc_api"
  enabled      = true
  data_source  = "primary_db"  # Router will automatically use replicas for reads

  settings = {
    max_page_size = 5000
    default_limit = 100
  }
}

# Example layer
layer "buildings" {
  title       = "Building Footprints"
  data_source = "primary_db"  # References primary, but reads go to replicas
  table       = "buildings"
  id_field    = "id"

  geometry {
    column = "geom"
    type   = "Polygon"
    srid   = 4326
  }

  services = ["features"]
}
