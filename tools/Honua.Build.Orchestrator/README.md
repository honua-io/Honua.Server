# Honua Build Orchestrator

A cross-repository build system for creating custom GIS server deployments with platform-specific optimizations.

## Overview

The Build Orchestrator enables you to:

- **Clone multiple repositories** (public and private with PAT authentication)
- **Generate unified solutions** combining projects from multiple sources
- **Build native AOT binaries** for multiple platforms (linux-x64, linux-arm64, win-x64)
- **Optimize for cloud platforms** (AWS Graviton, Azure Ampere, GCP Tau)
- **Push to container registries** with automatic tagging

## Features

### Multi-Repository Support
- Clone from public and private repositories
- Support for PAT token authentication
- Automatic dependency resolution
- Incremental cloning with change detection

### Platform-Specific Optimizations
- **AWS Graviton2/3**: NEON vectorization, ARM64 optimizations
- **Azure Ampere**: ARM64 with cloud-specific tuning
- **GCP Tau T2A**: Balanced performance/cost optimization
- **x86_64**: AVX2/AVX512 instruction sets

### Build Features
- Native AOT compilation for minimal runtime dependencies
- Profile-Guided Optimization (PGO)
- Dynamic PGO for improved startup performance
- Code trimming for reduced binary size
- Ready-to-Run (R2R) compilation
- Parallel builds for faster execution

### Container Publishing
- Support for ECR, ACR, GCR registries
- Multiple tagging strategies:
  - `manifest-hash`: Deterministic hash-based tags
  - `semantic-version`: SemVer-based tags
  - `commit-sha`: Git commit-based tags
- Automatic authentication for cloud registries

## Installation

```bash
cd tools/Honua.Build.Orchestrator
dotnet build
```

## Usage

### Build Command

Execute a complete build from a manifest:

```bash
dotnet run -- build \
  --manifest example-manifest.json \
  --output ./output \
  --workspace /tmp/honua-builds \
  --verbose
```

**Options:**
- `--manifest, -m`: Path to build manifest JSON file (required)
- `--output, -o`: Output directory for build artifacts (required)
- `--workspace, -w`: Workspace directory for cloning repositories (default: temp directory)
- `--verbose, -v`: Enable verbose logging

### Validate Command

Validate a manifest file without executing the build:

```bash
dotnet run -- validate --manifest example-manifest.json
```

This checks:
- Required fields are present
- Repository configurations are valid
- Target architectures are supported
- Credentials are configured for private repos
- Module references are valid

### Hash Command

Compute the deterministic hash of a manifest:

```bash
dotnet run -- hash --manifest example-manifest.json
```

Output includes:
- 8-character manifest hash
- Cache keys for each target
- Generated image tags

## Build Manifest Format

A build manifest is a JSON file defining the complete build configuration.

### Basic Structure

```json
{
  "id": "unique-build-id",
  "version": "1.0.0",
  "name": "Build Name",
  "description": "Optional description",
  "repositories": [...],
  "modules": [...],
  "targets": [...],
  "deployment": {...},
  "optimizations": {...},
  "properties": {...}
}
```

### Repository Configuration

```json
{
  "name": "repo-name",
  "url": "https://github.com/org/repo.git",
  "ref": "main",
  "access": "private",
  "credentials": "GITHUB_PAT_ENV_VAR",
  "projects": [
    "src/Project1/Project1.csproj",
    "src/Project2/Project2.csproj"
  ]
}
```

**Fields:**
- `name`: Logical name for the repository
- `url`: Git clone URL (HTTPS or SSH)
- `ref`: Branch, tag, or commit SHA (default: "main")
- `access`: "public" or "private"
- `credentials`: Environment variable name containing PAT token (required for private repos)
- `projects`: Specific projects to include (if omitted, includes all)

### Cloud Target Configuration

```json
{
  "id": "aws-graviton3-prod",
  "provider": "aws",
  "compute": "graviton3",
  "architecture": "linux-arm64",
  "tier": "production",
  "optimizations": {
    "pgo": true,
    "dynamicPgo": true,
    "vectorization": "neon",
    "trim": true,
    "optimizationPreference": "speed"
  },
  "registry": {
    "url": "123456789.dkr.ecr.us-east-1.amazonaws.com",
    "repository": "honua-gis",
    "tagStrategy": "manifest-hash",
    "tags": ["latest", "production"],
    "credentialsVariable": "AWS_ECR_CREDENTIALS"
  }
}
```

**Supported Architectures:**
- `linux-x64`: Linux x86-64
- `linux-arm64`: Linux ARM64
- `linux-musl-x64`: Alpine Linux x86-64
- `linux-musl-arm64`: Alpine Linux ARM64
- `win-x64`: Windows x86-64
- `win-arm64`: Windows ARM64
- `osx-x64`: macOS Intel
- `osx-arm64`: macOS Apple Silicon

**Compute Types:**
- `graviton2`, `graviton3`: AWS Graviton processors
- `ampere`: Azure Ampere Altra
- `tau-t2a`: GCP Tau T2A
- `x86_64`: Standard x86-64

**Vectorization Options:**
- `neon`: ARM NEON SIMD
- `avx2`: x86 AVX2
- `avx512`: x86 AVX-512

### Build Optimizations

```json
{
  "pgo": true,
  "dynamicPgo": true,
  "tieredCompilation": true,
  "vectorization": "neon",
  "readyToRun": true,
  "trim": true,
  "optimizationPreference": "speed",
  "ilcOptions": ["--stacktracedata"]
}
```

**Fields:**
- `pgo`: Enable Profile-Guided Optimization
- `dynamicPgo`: Enable dynamic PGO for AOT
- `tieredCompilation`: Enable tiered compilation
- `vectorization`: SIMD instruction set
- `readyToRun`: Enable R2R compilation
- `trim`: Trim unused code
- `optimizationPreference`: "speed", "size", or "balanced"
- `ilcOptions`: Custom IL compiler options

### Deployment Configuration

```json
{
  "environment": "production",
  "region": "us-east-1",
  "enableCache": true,
  "cacheBackend": "redis",
  "parallelism": 4,
  "resources": {
    "cpuLimit": 8.0,
    "memoryLimit": 16.0,
    "timeout": 90
  }
}
```

## Authentication

### Private Repositories

Set environment variables for private repository access:

```bash
export GITHUB_PAT="ghp_xxxxxxxxxxxxx"
export GITLAB_TOKEN="glpat-xxxxxxxxxxxxx"
```

Reference in manifest:
```json
{
  "credentials": "GITHUB_PAT"
}
```

### Container Registries

#### AWS ECR

```bash
aws ecr get-login-password --region us-east-1 | \
  docker login --username AWS --password-stdin \
  123456789.dkr.ecr.us-east-1.amazonaws.com
```

Or set `AWS_ECR_CREDENTIALS` environment variable.

#### Azure ACR

```bash
az acr login --name honua
```

Or set `AZURE_ACR_CREDENTIALS` with service principal credentials.

#### GCP GCR

```bash
gcloud auth configure-docker
```

Or set `GCP_GCR_CREDENTIALS` with service account JSON key.

## Build Output

The orchestrator produces:

1. **Build Artifacts**: Compiled binaries in `<output>/<target-id>/`
2. **Build Logs**: Detailed compilation output
3. **Container Images**: Tagged and pushed to registries
4. **Build Report**: JSON summary of results

### Build Report Structure

```json
[
  {
    "targetId": "aws-graviton3-prod",
    "success": true,
    "outputPath": "/output/aws-graviton3-prod",
    "imageTag": "v1.0-abc123de-aws-graviton3-prod",
    "duration": 245.3,
    "binarySize": 52428800
  }
]
```

## Manifest Hashing

The orchestrator generates deterministic hashes for:

- **Build identification**: Unique hash per configuration
- **Cache keys**: Efficient build artifact caching
- **Image tags**: Reproducible container versioning

Hash computation:
1. Normalize manifest (remove timestamps, sort collections)
2. Serialize to canonical JSON
3. SHA-256 hash
4. Take first 8 hex characters

Example: `abc123de`

## Examples

### Basic Local Build

```json
{
  "id": "local-dev",
  "version": "0.1.0",
  "name": "Local Development Build",
  "repositories": [
    {
      "name": "honua",
      "url": "https://github.com/honua-io/honua.git",
      "ref": "dev"
    }
  ],
  "modules": ["Honua.Server.Core", "Honua.Server.Host"],
  "targets": [
    {
      "id": "local-x64",
      "provider": "on-premises",
      "compute": "x86_64",
      "architecture": "linux-x64"
    }
  ]
}
```

### Multi-Cloud Production Build

See `example-manifest.json` for a complete production configuration with:
- Multiple cloud providers (AWS, Azure, GCP)
- Private repository access
- Container registry publishing
- Advanced optimizations

## Troubleshooting

### Clone Failures

**Error**: "Credentials environment variable not found"

**Solution**: Ensure PAT token is set:
```bash
export REPO_PAT="your-token-here"
```

### Build Failures

**Error**: "Module not found"

**Solution**: Verify module names in `modules` array match project names exactly.

### Registry Push Failures

**Error**: "Authentication required"

**Solution**: Authenticate to registry before running build:
```bash
aws ecr get-login-password | docker login --username AWS --password-stdin <registry>
```

## Performance Tips

1. **Enable caching**: Use Redis or S3 cache backend for faster incremental builds
2. **Increase parallelism**: Set `deployment.parallelism` based on available cores
3. **Use local workspace**: Avoid network storage for workspace directory
4. **Optimize modules**: Only include required modules in the build

## Security Considerations

- Never commit PAT tokens or credentials to version control
- Use environment variables for all sensitive data
- Rotate credentials regularly
- Use read-only tokens where possible
- Enable audit logging for production builds

## Architecture

The orchestrator follows this workflow:

1. **Load Manifest**: Parse and validate JSON configuration
2. **Clone Repositories**: Fetch all specified repos with authentication
3. **Generate Solution**: Create unified .sln file with all projects
4. **Build Targets**: Compile for each platform in parallel
5. **Containerize**: Build Docker images with optimized binaries
6. **Push**: Upload images to configured registries
7. **Report**: Generate build summary and metrics

## License

Same as parent Honua project.
