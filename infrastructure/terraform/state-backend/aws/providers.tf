# ============================================================================
# Provider Configuration for State Backend
# ============================================================================

provider "aws" {
  region = var.region

  default_tags {
    tags = {
      Project   = "HonuaIO"
      ManagedBy = "Terraform"
      Component = "StateBackend"
    }
  }
}

# Provider for replica region (if replication is enabled)
provider "aws" {
  alias  = "replica"
  region = var.replication_region

  default_tags {
    tags = {
      Project   = "HonuaIO"
      ManagedBy = "Terraform"
      Component = "StateBackendReplica"
    }
  }
}
