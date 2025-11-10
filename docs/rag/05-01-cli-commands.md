---
tags: [cli, commands, honua-cli, management, deployment, configuration, tools]
category: development
difficulty: beginner
version: 1.0.0
last_updated: 2025-10-15
---

# Honua CLI Commands Complete Reference

Comprehensive guide to all `honua` CLI commands for configuration, deployment, and management.

## Table of Contents
- [Overview](#overview)
- [Installation](#installation)
- [Global Options](#global-options)
- [Configuration Commands](#configuration-commands)
- [Deployment Commands](#deployment-commands)
- [Authentication Commands](#authentication-commands)
- [Metadata Commands](#metadata-commands)
- [Data Commands](#data-commands)
- [Raster Commands](#raster-commands)
- [Monitoring Commands](#monitoring-commands)
- [AI Consultant](#ai-consultant)
- [Troubleshooting](#troubleshooting)
- [Related Documentation](#related-documentation)

## Overview

The `honua` CLI provides commands for managing Honua Server deployments, configuration, and data.

### Command Categories

| Category | Purpose | Example |
|----------|---------|---------|
| Config | Configuration management | `honua config init` |
| Deploy | Deployment planning | `honua deploy plan` |
| Auth | User management | `honua auth create-user` |
| Metadata | Metadata operations | `honua metadata validate` |
| Data | Data ingestion | `honua data ingest` |
| Raster | Raster processing | `honua raster cache preseed` |
| Status | System status | `honua status` |
| Devsecops | AI assistance | `honua devsecops` |

## Installation

### From Binary Release

```bash
# Linux/macOS
curl -L https://github.com/honuaio/honua/releases/latest/download/honua-linux-x64.tar.gz | tar xz
sudo mv honua /usr/local/bin/

# Windows (PowerShell)
Invoke-WebRequest -Uri https://github.com/honuaio/honua/releases/latest/download/honua-win-x64.zip -OutFile honua.zip
Expand-Archive honua.zip
Move-Item honua/honua.exe C:\Windows\System32\
```

### From Source

```bash
git clone https://github.com/honuaio/honua.git
cd honua/src/Honua.Cli
dotnet build -c Release
dotnet publish -c Release -o out
sudo ln -s $(pwd)/out/honua /usr/local/bin/honua
```

### Verify Installation

```bash
honua --version
# Output: Honua CLI 1.0.0
```

## Global Options

Available for all commands:

```bash
honua <command> [options]

Global Options:
  --workspace <PATH>  Workspace directory (default: ./metadata)
  --verbose, -v       Verbose output
  --quiet, -q         Suppress output
  --help, -h          Show help
  --version           Show version
```

## Configuration Commands

### config init

Initialize new Honua configuration.

```bash
honua config init [options]

Options:
  --workspace <PATH>     Workspace directory
  --database-provider    Database provider (postgresql, mysql, sqlserver)
  --interactive          Interactive setup wizard
  --template <NAME>      Use template (quickstart, production, development)

Examples:
  # Interactive setup
  honua config init --interactive

  # Use template
  honua config init --template quickstart

  # Specify workspace
  honua config init --workspace /var/lib/honua
```

**Output:**
Creates `metadata.yaml` and directory structure:
```
workspace/
├── metadata.yaml
├── services/
├── layers/
└── styles/
```

### config show

Display current configuration.

```bash
honua config show [--format json|yaml|table]

Examples:
  # Show as table
  honua config show

  # Export as JSON
  honua config show --format json > config.json

  # Show specific section
  honua config show --section database
```

### config validate

Validate configuration files.

```bash
honua config validate [options]

Options:
  --workspace <PATH>    Workspace to validate
  --strict              Strict validation (fail on warnings)
  --fix                 Auto-fix issues when possible

Examples:
  # Validate current workspace
  honua config validate

  # Strict validation
  honua config validate --strict

  # Auto-fix common issues
  honua config validate --fix
```

## Deployment Commands

### deploy plan

Create deployment plan (like terraform plan).

```bash
honua deploy plan [options]

Options:
  --cloud-provider <NAME>   Cloud provider (aws, azure, gcp, docker)
  --region <NAME>           Deployment region
  --environment <ENV>       Environment (dev, staging, prod)
  --config-file <PATH>      Configuration file
  --output <PATH>           Save plan to file

Examples:
  # Interactive planning
  honua deploy plan

  # AWS deployment
  honua deploy plan --cloud-provider aws --region us-west-2 --environment prod

  # Save plan
  honua deploy plan --output deployment-plan.json
```

### deploy execute

Execute deployment plan.

```bash
honua deploy execute [options]

Options:
  --plan-file <PATH>       Plan file to execute
  --auto-approve           Skip confirmation
  --dry-run                Show what would be done

Examples:
  # Execute saved plan
  honua deploy execute --plan-file deployment-plan.json

  # Auto-approve
  honua deploy execute --plan-file plan.json --auto-approve

  # Dry run
  honua deploy execute --plan-file plan.json --dry-run
```

### deploy validate-topology

Validate deployment topology.

```bash
honua deploy validate-topology [options]

Options:
  --topology-file <PATH>    Topology file
  --cloud-provider <NAME>   Cloud provider
  --check-quotas            Check cloud quotas
  --check-costs             Estimate costs

Examples:
  # Validate topology
  honua deploy validate-topology --topology-file topology.yaml

  # Check quotas and costs
  honua deploy validate-topology --check-quotas --check-costs
```

## Authentication Commands

### auth bootstrap

Create first admin user.

```bash
honua auth bootstrap [options]

Options:
  --username <NAME>      Username
  --password <PASS>      Password
  --email <EMAIL>        Email address

Example:
  honua auth bootstrap \
    --username admin \
    --password "SecureP@ssw0rd123" \
    --email admin@example.com
```

### auth create-user

Create new user.

```bash
honua auth create-user [options]

Options:
  --username <NAME>      Username (required)
  --password <PASS>      Password (required)
  --email <EMAIL>        Email
  --role <ROLE>          Role (Viewer, Editor, Admin)

Examples:
  # Interactive
  honua auth create-user

  # With parameters
  honua auth create-user \
    --username john \
    --password "SecurePass123!" \
    --email john@example.com \
    --role Editor
```

### auth create-api-key

Generate API key for user.

```bash
honua auth create-api-key [options]

Options:
  --username <NAME>      Username
  --name <NAME>          Key name/description
  --expires <DATE>       Expiration date (ISO 8601)
  --scopes <SCOPES>      Comma-separated scopes

Examples:
  # Create key
  honua auth create-api-key \
    --username john \
    --name "Mobile App" \
    --expires "2026-12-31"

  # With scopes
  honua auth create-api-key \
    --username app_user \
    --name "Read-only API" \
    --scopes "read:features,read:rasters"
```

### auth list-api-keys

List API keys for user.

```bash
honua auth list-api-keys --username <NAME>

Example:
  honua auth list-api-keys --username john
```

### auth revoke-api-key

Revoke API key.

```bash
honua auth revoke-api-key --key-id <ID>

Example:
  honua auth revoke-api-key --key-id abc123def456
```

## Metadata Commands

### metadata validate

Validate metadata files.

```bash
honua metadata validate [options]

Options:
  --workspace <PATH>     Workspace directory
  --strict               Strict validation
  --service <ID>         Validate specific service
  --layer <ID>           Validate specific layer
  --dataset <ID>         Validate specific dataset

Examples:
  # Validate all
  honua metadata validate

  # Validate service
  honua metadata validate --service countries

  # Strict mode
  honua metadata validate --strict
```

### metadata snapshot

Create metadata snapshot.

```bash
honua metadata snapshot [options]

Options:
  --workspace <PATH>     Workspace directory
  --output <PATH>        Output file
  --name <NAME>          Snapshot name

Example:
  honua metadata snapshot \
    --name "pre-deployment-backup" \
    --output snapshots/backup-2025-10-15.zip
```

### metadata restore

Restore from snapshot.

```bash
honua metadata restore [options]

Options:
  --snapshot <PATH>      Snapshot file
  --workspace <PATH>     Target workspace
  --force                Overwrite existing

Example:
  honua metadata restore \
    --snapshot snapshots/backup.zip \
    --workspace /var/lib/honua/metadata
```

### metadata sync-schema

Sync database schema with metadata.

```bash
honua metadata sync-schema [options]

Options:
  --workspace <PATH>     Workspace
  --connection <STRING>  Database connection
  --dry-run              Show changes without applying

Example:
  honua metadata sync-schema --dry-run
```

## Data Commands

### data ingest

Ingest data from files.

```bash
honua data ingest [options]

Options:
  --source <PATH>        Source file/directory
  --service <ID>         Target service
  --layer <ID>           Target layer
  --format <FORMAT>      Source format (auto-detect if omitted)
  --append               Append to existing data
  --truncate             Clear before import

Examples:
  # Import Shapefile
  honua data ingest \
    --source data/countries.shp \
    --service public \
    --layer countries

  # Import GeoJSON with truncate
  honua data ingest \
    --source cities.geojson \
    --service public \
    --layer cities \
    --truncate

  # Import directory
  honua data ingest \
    --source /data/shapefiles/ \
    --service public
```

### data ingest-status

Check ingestion job status.

```bash
honua data ingest-status [options]

Options:
  --job-id <ID>          Job ID
  --all                  Show all jobs
  --watch                Watch job progress

Examples:
  # Check specific job
  honua data ingest-status --job-id abc123

  # Watch progress
  honua data ingest-status --job-id abc123 --watch

  # List all jobs
  honua data ingest-status --all
```

## Raster Commands

### raster cache preseed

Pre-generate raster tile cache.

```bash
honua raster cache preseed [options]

Options:
  --dataset <ID>         Raster dataset ID
  --zoom-levels <RANGE>  Zoom levels (e.g., "0-10")
  --bbox <BBOX>          Bounding box to preseed
  --threads <N>          Parallel threads

Examples:
  # Preseed all levels
  honua raster cache preseed --dataset elevation

  # Specific zoom levels
  honua raster cache preseed \
    --dataset elevation \
    --zoom-levels "5-12"

  # Bounded area
  honua raster cache preseed \
    --dataset elevation \
    --bbox "-120,35,-115,40" \
    --zoom-levels "10-14"
```

### raster cache status

Check tile cache status.

```bash
honua raster cache status [options]

Options:
  --dataset <ID>         Dataset ID
  --detailed             Show detailed statistics

Examples:
  # Status for dataset
  honua raster cache status --dataset elevation

  # Detailed stats
  honua raster cache status --dataset elevation --detailed
```

### raster cache purge

Clear tile cache.

```bash
honua raster cache purge [options]

Options:
  --dataset <ID>         Dataset ID (or --all)
  --all                  Purge all caches
  --zoom-levels <RANGE>  Specific zoom levels
  --confirm              Skip confirmation

Examples:
  # Purge dataset cache
  honua raster cache purge --dataset elevation --confirm

  # Purge all
  honua raster cache purge --all --confirm

  # Purge specific zooms
  honua raster cache purge \
    --dataset elevation \
    --zoom-levels "0-5" \
    --confirm
```

## Monitoring Commands

### status

Show system status.

```bash
honua status [options]

Options:
  --detailed             Show detailed status
  --json                 Output as JSON

Examples:
  # Basic status
  honua status

  # Detailed
  honua status --detailed

  # JSON output
  honua status --json
```

### telemetry enable

Enable telemetry (anonymous usage stats).

```bash
honua telemetry enable

Example:
  honua telemetry enable
```

### telemetry status

Check telemetry status.

```bash
honua telemetry status

Output:
  Telemetry: Enabled
  Last reported: 2025-10-15 10:30:00 UTC
```

## AI Devsecops

### devsecops

AI-powered deployment assistance.

```bash
honua devsecops [options]

Options:
  --prompt <TEXT>          Natural language prompt
  --mode <MODE>            Mode (planning, execution, bootstrap)
  --auto-approve           Execute without confirmation
  --dry-run                Show plan without executing

Examples:
  # Interactive mode
  honua devsecops

  # Single prompt
  honua devsecops --prompt "Deploy Honua to AWS with PostgreSQL"

  # Bootstrap mode
  honua devsecops --mode bootstrap
```

### devsecops-chat

Interactive AI chat.

```bash
honua devsecops-chat

Features:
  - Multi-turn conversations
  - Context-aware assistance
  - Code generation
  - Deployment guidance

Example session:
  > honua devsecops-chat
  Welcome to Honua AI Devsecops!

  You: How do I deploy to Kubernetes?
  AI: I'll help you deploy to Kubernetes...

  You: Add PostgreSQL
  AI: Adding PostgreSQL deployment...
```

## Troubleshooting

### Check CLI Version

```bash
honua --version
honua status
```

### Enable Verbose Logging

```bash
honua <command> --verbose

# Or set environment
export HONUA_LOG_LEVEL=Debug
honua <command>
```

### Test Connection

```bash
honua test-connection \
  --host localhost \
  --port 5000 \
  --https
```

### Common Issues

**Command not found:**
```bash
# Check PATH
echo $PATH

# Add to PATH
export PATH=$PATH:/usr/local/bin

# Verify
which honua
```

**Permission denied:**
```bash
# Make executable
chmod +x /usr/local/bin/honua

# Or use sudo
sudo honua <command>
```

**Configuration errors:**
```bash
# Validate config
honua config validate --verbose

# Reset config
rm -rf metadata/
honua config init --template quickstart
```

## Related Documentation

- [Configuration Reference](./02-01-configuration-reference.md) - Config options
- [Docker Deployment](./04-01-docker-deployment.md) - Deployment
- [Kubernetes](./04-02-kubernetes-deployment.md) - K8s deployment
- [Common Issues](./05-02-common-issues.md) - Troubleshooting

---

**Last Updated**: 2025-10-15
**Honua Version**: 1.0.0-rc1
**CLI Version**: 1.0.0
