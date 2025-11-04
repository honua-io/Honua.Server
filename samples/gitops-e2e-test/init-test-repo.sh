#!/bin/bash
#
# GitOps E2E Test Repository Initialization Script
#
# This script creates a complete test Git repository with sample Honua configuration
# for end-to-end testing of GitOps functionality.
#
# Usage: ./init-test-repo.sh [repo-path] [state-path]
#
# Default paths:
#   - Repository: /tmp/honua-gitops-test-repo
#   - State: /tmp/honua-gitops-state
#

set -e  # Exit on error

# Configuration
REPO_PATH="${1:-/tmp/honua-gitops-test-repo}"
STATE_PATH="${2:-/tmp/honua-gitops-state}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
BLUE='\033[0;34m'
NC='\033[0m' # No Color

# Logging functions
log_info() {
    echo -e "${BLUE}[GitOps E2E Test Setup]${NC} $1"
}

log_success() {
    echo -e "${GREEN}[GitOps E2E Test Setup]${NC} $1"
}

log_warning() {
    echo -e "${YELLOW}[GitOps E2E Test Setup]${NC} $1"
}

log_error() {
    echo -e "${RED}[GitOps E2E Test Setup]${NC} $1"
}

# Check prerequisites
check_prerequisites() {
    log_info "Checking prerequisites..."

    if ! command -v git &> /dev/null; then
        log_error "Git is not installed. Please install Git and try again."
        exit 1
    fi

    GIT_VERSION=$(git --version | awk '{print $3}')
    log_success "Git version $GIT_VERSION found"
}

# Clean up existing test repository
cleanup_existing() {
    if [ -d "$REPO_PATH" ]; then
        log_warning "Existing test repository found at $REPO_PATH"
        read -p "Remove and recreate? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            log_info "Removing existing test repository..."
            rm -rf "$REPO_PATH"
            log_success "Removed existing test repository"
        else
            log_error "Aborted. Please remove the existing repository manually or specify a different path."
            exit 1
        fi
    fi

    if [ -d "$STATE_PATH" ]; then
        log_warning "Existing state directory found at $STATE_PATH"
        read -p "Remove and recreate? (y/N): " -n 1 -r
        echo
        if [[ $REPLY =~ ^[Yy]$ ]]; then
            log_info "Removing existing state directory..."
            rm -rf "$STATE_PATH"
            log_success "Removed existing state directory"
        fi
    fi
}

# Create directory structure
create_structure() {
    log_info "Creating directory structure..."

    # Create repository
    mkdir -p "$REPO_PATH"
    cd "$REPO_PATH"

    # Initialize Git repository
    git init
    git config user.name "GitOps Test User"
    git config user.email "gitops-test@honua.io"

    # Create environment directories
    mkdir -p environments/development
    mkdir -p environments/staging
    mkdir -p environments/production
    mkdir -p environments/common

    # Create .gitops directory for policies
    mkdir -p .gitops

    # Create state directory
    mkdir -p "$STATE_PATH"
    mkdir -p "$STATE_PATH/deployments"
    mkdir -p "$STATE_PATH/approvals"

    log_success "Directory structure created"
}

# Create metadata.json files
create_metadata_files() {
    log_info "Creating sample metadata files..."

    # Development metadata - simple configuration
    cat > "$REPO_PATH/environments/development/metadata.json" <<'EOF'
{
  "services": [
    {
      "id": "test-service",
      "title": "Test GIS Service",
      "description": "Test service for GitOps E2E testing",
      "abstract": "This service contains sample layers for testing GitOps functionality",
      "layers": [
        {
          "id": "sample-points",
          "title": "Sample Points",
          "description": "Sample point layer for testing",
          "datasource": "test-postgis",
          "table": "public.sample_points",
          "geometryColumn": "geom",
          "geometryType": "Point",
          "srid": 4326,
          "fields": [
            {
              "name": "id",
              "type": "integer",
              "alias": "ID"
            },
            {
              "name": "name",
              "type": "string",
              "alias": "Name"
            },
            {
              "name": "category",
              "type": "string",
              "alias": "Category"
            }
          ]
        }
      ]
    }
  ]
}
EOF

    # Staging metadata - similar to development
    cat > "$REPO_PATH/environments/staging/metadata.json" <<'EOF'
{
  "services": [
    {
      "id": "test-service",
      "title": "Test GIS Service (Staging)",
      "description": "Test service for GitOps E2E testing - Staging environment",
      "abstract": "Staging environment for testing before production deployment",
      "layers": [
        {
          "id": "sample-points",
          "title": "Sample Points",
          "description": "Sample point layer for testing",
          "datasource": "test-postgis-staging",
          "table": "public.sample_points",
          "geometryColumn": "geom",
          "geometryType": "Point",
          "srid": 4326,
          "fields": [
            {
              "name": "id",
              "type": "integer",
              "alias": "ID"
            },
            {
              "name": "name",
              "type": "string",
              "alias": "Name"
            },
            {
              "name": "category",
              "type": "string",
              "alias": "Category"
            }
          ]
        }
      ]
    }
  ]
}
EOF

    # Production metadata - production-ready configuration
    cat > "$REPO_PATH/environments/production/metadata.json" <<'EOF'
{
  "services": [
    {
      "id": "production-service",
      "title": "Production GIS Service",
      "description": "Production GIS service",
      "abstract": "Production service with strict validation and approval requirements",
      "layers": [
        {
          "id": "locations",
          "title": "Locations",
          "description": "Production location data",
          "datasource": "production-postgis",
          "table": "public.locations",
          "geometryColumn": "geom",
          "geometryType": "Point",
          "srid": 4326,
          "fields": [
            {
              "name": "id",
              "type": "integer",
              "alias": "Location ID"
            },
            {
              "name": "name",
              "type": "string",
              "alias": "Location Name"
            },
            {
              "name": "type",
              "type": "string",
              "alias": "Location Type"
            },
            {
              "name": "status",
              "type": "string",
              "alias": "Status"
            }
          ]
        }
      ]
    }
  ]
}
EOF

    log_success "Metadata files created"
}

# Create datasources.json files
create_datasource_files() {
    log_info "Creating sample datasource files..."

    # Development datasources
    cat > "$REPO_PATH/environments/development/datasources.json" <<'EOF'
{
  "datasources": [
    {
      "id": "test-postgis",
      "type": "PostgreSQL",
      "connectionString": "Host=localhost;Port=5432;Database=honua_test_dev;Username=honua_dev;Password=dev_password",
      "description": "Development test database",
      "metadata": {
        "environment": "development",
        "purpose": "testing"
      }
    }
  ]
}
EOF

    # Staging datasources
    cat > "$REPO_PATH/environments/staging/datasources.json" <<'EOF'
{
  "datasources": [
    {
      "id": "test-postgis-staging",
      "type": "PostgreSQL",
      "connectionString": "Host=staging-db.internal;Port=5432;Database=honua_test_staging;Username=honua_staging;Password=staging_password",
      "description": "Staging test database",
      "metadata": {
        "environment": "staging",
        "purpose": "pre-production validation"
      }
    }
  ]
}
EOF

    # Production datasources
    cat > "$REPO_PATH/environments/production/datasources.json" <<'EOF'
{
  "datasources": [
    {
      "id": "production-postgis",
      "type": "PostgreSQL",
      "connectionString": "Host=prod-db.internal;Port=5432;Database=honua_production;Username=honua_prod;Password=${POSTGRES_PASSWORD}",
      "description": "Production database",
      "metadata": {
        "environment": "production",
        "purpose": "production",
        "sla": "99.9%"
      }
    }
  ]
}
EOF

    log_success "Datasource files created"
}

# Create common/shared configuration
create_common_config() {
    log_info "Creating common/shared configuration..."

    cat > "$REPO_PATH/environments/common/shared-config.json" <<'EOF'
{
  "global_settings": {
    "timeouts": {
      "query": 30000,
      "connection": 5000
    },
    "caching": {
      "enabled": true,
      "ttl": 300
    },
    "logging": {
      "level": "Information",
      "includeQueryDetails": false
    }
  },
  "security": {
    "requireAuthentication": false,
    "allowedOrigins": ["*"]
  }
}
EOF

    log_success "Common configuration created"
}

# Create deployment policy
create_deployment_policy() {
    log_info "Creating deployment policy..."

    cat > "$REPO_PATH/.gitops/deployment-policy.yaml" <<'EOF'
# GitOps Deployment Policy
# Defines approval requirements and deployment constraints

environments:
  development:
    requiresApproval: false
    autoRollback: false
    minimumRiskLevelForApproval: critical
    approvalTimeout: 1h

  staging:
    requiresApproval: false
    autoRollback: true
    minimumRiskLevelForApproval: high
    approvalTimeout: 4h

  production:
    requiresApproval: true
    autoRollback: true
    minimumRiskLevelForApproval: medium
    approvalTimeout: 24h
    breakingChangesRequireApproval: true
    migrationsRequireApproval: true

riskAssessment:
  breakingChanges:
    - geometryTypeChange
    - sridChange
    - tableRename
    - columnRename

  highRisk:
    - migration
    - datasourceChange
    - securityChange

approvers:
  production:
    - devops-team
    - platform-admin
  staging:
    - developers
EOF

    log_success "Deployment policy created"
}

# Create README for test repository
create_repo_readme() {
    log_info "Creating repository README..."

    cat > "$REPO_PATH/README.md" <<'EOF'
# Honua GitOps Test Repository

This is a test Git repository for Honua GitOps E2E testing.

## Structure

```
.
├── environments/
│   ├── development/     # Development environment configuration
│   ├── staging/         # Staging environment configuration
│   ├── production/      # Production environment configuration
│   └── common/          # Shared configuration across environments
├── .gitops/
│   └── deployment-policy.yaml  # Deployment approval policies
└── README.md
```

## Usage

This repository is automatically monitored by Honua's GitWatcher service.
When you commit changes to environment-specific files, GitWatcher will:

1. Detect the change
2. Trigger reconciliation for the affected environment
3. Apply the configuration changes to the running Honua instance

## Testing

See `/samples/gitops-e2e-test/README.md` in the Honua repository for testing instructions.

## Environments

- **development**: Fast iteration, no approvals, auto-deploy
- **staging**: Pre-production validation, selective approvals
- **production**: Strict approvals, breaking change detection, rollback support
EOF

    log_success "Repository README created"
}

# Make initial commits
make_initial_commits() {
    log_info "Making initial commits..."

    cd "$REPO_PATH"

    # Initial commit with directory structure
    git add .gitops/ README.md
    git commit -m "Initial commit: Add repository structure and policies"

    # Commit common configuration
    git add environments/common/
    git commit -m "Add common/shared configuration"

    # Commit development environment
    git add environments/development/
    git commit -m "Add development environment configuration"

    # Commit staging environment
    git add environments/staging/
    git commit -m "Add staging environment configuration"

    # Commit production environment
    git add environments/production/
    git commit -m "Add production environment configuration"

    INITIAL_COMMIT=$(git rev-parse HEAD)

    log_success "Initial commits completed"
    log_info "Initial commit hash: $INITIAL_COMMIT"
}

# Create .gitignore
create_gitignore() {
    log_info "Creating .gitignore..."

    cat > "$REPO_PATH/.gitignore" <<'EOF'
# Reconciled configuration (output from Honua)
reconciled/

# Temporary files
*.tmp
*.swp
*~

# OS files
.DS_Store
Thumbs.db

# Editor files
.vscode/
.idea/
*.suo
*.user
EOF

    cd "$REPO_PATH"
    git add .gitignore
    git commit -m "Add .gitignore"

    log_success ".gitignore created"
}

# Print summary
print_summary() {
    log_success "====================================="
    log_success "GitOps E2E Test Setup Complete!"
    log_success "====================================="
    echo ""
    log_info "Repository path: $REPO_PATH"
    log_info "State directory: $STATE_PATH"
    echo ""
    log_info "Next steps:"
    echo "  1. Configure Honua to use this repository:"
    echo "     cp appsettings.test.json ../../src/Honua.Server.Host/appsettings.Development.json"
    echo ""
    echo "  2. Start Honua server:"
    echo "     cd ../../src/Honua.Server.Host && dotnet run"
    echo ""
    echo "  3. Validate GitOps is working:"
    echo "     ./validate.sh"
    echo ""
    log_info "Repository contains:"
    cd "$REPO_PATH"
    git log --oneline --all
    echo ""
    log_success "Ready for testing!"
}

# Main execution
main() {
    log_info "Starting GitOps E2E test repository setup..."
    echo ""

    check_prerequisites
    cleanup_existing
    create_structure
    create_metadata_files
    create_datasource_files
    create_common_config
    create_deployment_policy
    create_repo_readme
    create_gitignore
    make_initial_commits

    echo ""
    print_summary
}

# Run main function
main
