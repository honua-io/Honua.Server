# Honua.Server.Intake

Container registry provisioning and build delivery system for Honua. This library provides isolated namespace and credential management for customers across multiple container registry platforms.

## Features

- **Multi-Registry Support**: GitHub Container Registry (GHCR), AWS ECR, Azure ACR, and GCP Artifact Registry
- **Automated Provisioning**: Creates isolated namespaces and scoped credentials for each customer
- **License-Based Access Control**: Validates customer licenses before granting registry access
- **Build Caching**: Checks if builds exist before rebuilding
- **Efficient Image Distribution**: Uses crane/skopeo for registry-to-registry copying without local pulls
- **Token Management**: Generates short-lived access tokens with automatic expiration
- **Retry Logic**: Built-in resilience with Polly retry policies
- **Comprehensive Logging**: Detailed logging for troubleshooting and auditing

## Architecture

### Core Services

#### 1. RegistryProvisioner
Creates isolated registry namespaces and credentials for customers.

**GitHub Container Registry:**
- Creates fine-grained PATs scoped to customer namespace
- Configures package permissions

**AWS ECR:**
- Creates ECR repository: `honua/{customerId}`
- Creates IAM user: `honua-customer-{customerId}`
- Attaches scoped policy with repository-specific permissions
- Returns access key ID and secret

**Azure ACR:**
- Creates repository-scoped token
- Generates scope map for customer repository path
- Returns token credentials with expiration

**GCP Artifact Registry:**
- Creates service account for customer
- Grants Artifact Registry Writer role
- Returns service account key JSON

#### 2. RegistryCacheChecker
Checks if builds already exist in registries to avoid unnecessary rebuilds.

**Methods by Registry:**
- **GHCR**: HEAD request to `/v2/{org}/{image}/manifests/{tag}`
- **ECR**: AWS ECR `DescribeImages` API
- **ACR**: Azure Container Registry manifest API
- **GCR**: GCP Artifact Registry `GetVersion` API

#### 3. RegistryAccessManager
Validates customer licenses and generates short-lived access tokens.

**License Tiers:**
- **Enterprise**: Access to all registries
- **Professional**: GHCR, ECR, ACR (excludes GCP)
- **Standard**: GHCR only

**Token Generation:**
- **GHCR**: Fine-grained PAT (1 hour expiry)
- **ECR**: Authorization token (12 hours expiry)
- **ACR**: Azure AD token (1 hour expiry)
- **GCR**: Service account token (1 hour expiry)

#### 4. BuildDeliveryService
Orchestrates build delivery to customer registries.

**Workflow:**
1. Validate customer access
2. Check if build exists in cache
3. If not cached:
   - Build container image (if source provided)
   - Copy from source registry
4. Tag image with additional tags (latest, architecture-specific)
5. Return delivery result

**Image Copy Tools:**
- **crane**: Google's container tool (preferred)
- **skopeo**: Alternative for advanced scenarios

## Installation

Add the package reference to your project:

```xml
<ProjectReference Include="../Honua.Server.Intake/Honua.Server.Intake.csproj" />
```

## Configuration

### appsettings.json

```json
{
  "RegistryProvisioning": {
    "GitHubOrganization": "honua-io",
    "GitHubToken": "ghp_xxxxxxxxxxxx",
    "AwsRegion": "us-west-2",
    "AwsAccountId": "123456789012",
    "AzureSubscriptionId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
    "AzureResourceGroup": "honua-registries",
    "AzureRegistryName": "honuaregistry",
    "GcpProjectId": "honua-production",
    "GcpRegion": "us-central1",
    "GcpRepositoryName": "honua-builds"
  },
  "BuildDelivery": {
    "CranePath": "crane",
    "SkopeoPath": "skopeo",
    "PreferredTool": "crane",
    "CopyTimeoutSeconds": 600,
    "AutoTagLatest": true,
    "AutoTagArchitecture": true
  }
}
```

### Startup Configuration

```csharp
using Honua.Server.Intake;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        // Basic registration
        services.AddContainerRegistryIntake(options =>
        {
            options.GitHubOrganization = "honua-io";
            options.GitHubToken = Configuration["GitHub:Token"];
            options.AwsRegion = "us-west-2";
            options.AwsAccountId = Configuration["AWS:AccountId"];
            // ... configure other options
        });

        // With build delivery
        services.AddContainerRegistryIntakeWithBuildDelivery(
            configureProvisioning: options =>
            {
                options.GitHubOrganization = "honua-io";
                // ... configure provisioning
            },
            configureBuildDelivery: options =>
            {
                options.PreferredTool = "crane";
                options.CopyTimeoutSeconds = 600;
            });
    }
}
```

## Usage Examples

### Provisioning a Customer Registry

```csharp
public class CustomerOnboardingService
{
    private readonly IRegistryProvisioner _provisioner;
    private readonly ILogger<CustomerOnboardingService> _logger;

    public async Task OnboardCustomerAsync(string customerId)
    {
        // Provision GitHub Container Registry
        var result = await _provisioner.ProvisionAsync(
            customerId,
            RegistryType.GitHubContainerRegistry);

        if (result.Success)
        {
            _logger.LogInformation(
                "Provisioned registry for {CustomerId}: {RegistryUrl}/{Namespace}",
                customerId,
                result.Credential.RegistryUrl,
                result.Namespace);

            // Store credentials securely
            await StoreCredentialsAsync(customerId, result.Credential);
        }
    }
}
```

### Checking Build Cache

```csharp
public class BuildService
{
    private readonly IRegistryCacheChecker _cacheChecker;

    public async Task<bool> IsBuildCachedAsync(string customerId, string buildName, string version)
    {
        var cacheKey = new BuildCacheKey
        {
            CustomerId = customerId,
            BuildName = buildName,
            Version = version,
            Architecture = "amd64"
        };

        var result = await _cacheChecker.CheckCacheAsync(
            cacheKey,
            RegistryType.GitHubContainerRegistry);

        return result.Exists;
    }
}
```

### Validating Access and Generating Tokens

```csharp
public class RegistryAuthenticationService
{
    private readonly IRegistryAccessManager _accessManager;

    public async Task<string?> GetRegistryTokenAsync(string customerId)
    {
        // Validate access
        var accessResult = await _accessManager.ValidateAccessAsync(
            customerId,
            RegistryType.AwsEcr);

        if (!accessResult.AccessGranted)
        {
            throw new UnauthorizedAccessException(accessResult.DenialReason);
        }

        // Generate token
        var tokenResult = await _accessManager.GenerateRegistryTokenAsync(
            customerId,
            RegistryType.AwsEcr);

        return tokenResult.AccessToken;
    }
}
```

### Delivering a Build

```csharp
public class BuildDeploymentService
{
    private readonly IBuildDeliveryService _deliveryService;
    private readonly ILogger<BuildDeploymentService> _logger;

    public async Task DeployBuildAsync(
        string customerId,
        string buildName,
        string version)
    {
        var cacheKey = new BuildCacheKey
        {
            CustomerId = customerId,
            BuildName = buildName,
            Version = version,
            Architecture = "amd64"
        };

        var result = await _deliveryService.DeliverBuildAsync(
            cacheKey,
            RegistryType.GitHubContainerRegistry,
            sourceBuildPath: null); // Will copy from central registry

        if (result.Success)
        {
            _logger.LogInformation(
                "Delivered build to {ImageReference} (cached: {WasCached})",
                result.ImageReference,
                result.WasCached);

            if (result.AdditionalTags?.Count > 0)
            {
                _logger.LogInformation(
                    "Applied tags: {Tags}",
                    string.Join(", ", result.AdditionalTags));
            }
        }
        else
        {
            _logger.LogError(
                "Failed to deliver build: {Error}",
                result.ErrorMessage);
        }
    }
}
```

### Copying Images Between Registries

```csharp
public class RegistryMigrationService
{
    private readonly IBuildDeliveryService _deliveryService;

    public async Task MigrateImageAsync(
        string sourceImage,
        string targetImage)
    {
        var success = await _deliveryService.CopyImageAsync(
            sourceImage: "ghcr.io/honua-io/app:v1.0.0",
            targetImage: "123456789012.dkr.ecr.us-west-2.amazonaws.com/customer-001/app:v1.0.0",
            sourceCredential: new RegistryCredential
            {
                RegistryUrl = "ghcr.io",
                Username = "honua-bot",
                Password = "ghp_xxxxxxxxxxxx"
            },
            targetCredential: new RegistryCredential
            {
                RegistryUrl = "123456789012.dkr.ecr.us-west-2.amazonaws.com",
                Username = "AWS",
                Password = "eyJwYXlsb2FkIjoiZXlKd..." // ECR token
            });

        if (success)
        {
            Console.WriteLine("Image migrated successfully");
        }
    }
}
```

## Prerequisites

### Required Tools

For image operations, install one of:

**crane** (recommended):
```bash
# Linux
curl -sL "https://github.com/google/go-containerregistry/releases/download/v0.19.0/go-containerregistry_Linux_x86_64.tar.gz" | tar -xz crane
sudo mv crane /usr/local/bin/

# macOS
brew install crane

# Windows
scoop install crane
```

**skopeo** (alternative):
```bash
# Ubuntu/Debian
sudo apt-get install skopeo

# macOS
brew install skopeo

# Fedora
sudo dnf install skopeo
```

### Cloud Provider Setup

**AWS:**
- Configure AWS credentials with ECR and IAM permissions
- Ensure IAM user has `ecr:*` and `iam:*` permissions

**Azure:**
- Authenticate with `az login` or use managed identity
- Assign Contributor role on ACR resource

**GCP:**
- Set `GOOGLE_APPLICATION_CREDENTIALS` environment variable
- Service account needs Artifact Registry Admin and IAM roles

**GitHub:**
- Create a PAT with `write:packages` and `read:packages` scopes
- For organization registries, ensure PAT has `read:org` scope

## Security Considerations

1. **Credential Storage**: Never store registry credentials in plaintext. Use secure secret management (Azure Key Vault, AWS Secrets Manager, etc.)

2. **Token Expiration**: All generated tokens have expiration times. Implement token refresh logic.

3. **Least Privilege**: IAM policies and role assignments follow least-privilege principle, scoped to specific repositories.

4. **Audit Logging**: All provisioning and access operations are logged for compliance.

5. **License Validation**: Always validate customer licenses before granting registry access.

## Troubleshooting

### Common Issues

**"Failed to authenticate with registry"**
- Verify credentials are correct and not expired
- Check network connectivity to registry
- Ensure proper permissions are configured

**"Repository not found"**
- Confirm repository was provisioned successfully
- Check namespace/organization configuration
- Verify customer ID matches provisioned namespace

**"Timeout during image copy"**
- Increase `CopyTimeoutSeconds` for large images
- Check network bandwidth and registry availability
- Consider using a regional mirror/cache

**"License tier does not permit access"**
- Verify customer has appropriate license tier
- Check license validation logic matches requirements
- Update customer license if needed

## Performance Optimization

1. **Parallel Operations**: Use `Task.WhenAll` for provisioning multiple registries
2. **Caching**: Enable build caching to avoid unnecessary rebuilds
3. **Regional Proximity**: Use registries in same region as compute resources
4. **Image Layers**: Optimize Dockerfile to maximize layer reuse
5. **Compression**: Enable compression in crane/skopeo for faster transfers

## Contributing

When adding support for new registry types:

1. Implement provisioning in `RegistryProvisioner.cs`
2. Add cache checking in `RegistryCacheChecker.cs`
3. Implement token generation in `RegistryAccessManager.cs`
4. Update `RegistryType` enum in `RegistryModels.cs`
5. Add configuration options to `RegistryProvisioningOptions`
6. Update this README with usage examples

## License

Copyright (c) 2025 HonuaIO. All rights reserved.
