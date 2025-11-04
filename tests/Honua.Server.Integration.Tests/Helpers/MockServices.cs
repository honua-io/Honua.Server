using Dapper;
using Honua.Server.Intake.Models;
using Honua.Server.Intake.Services;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Honua.Server.Integration.Tests.Helpers;

/// <summary>
/// Mock implementation of IRegistryProvisioner for integration testing.
/// Simulates cloud provider API calls without external dependencies.
/// </summary>
public class MockRegistryProvisioner : Honua.Server.Intake.Services.IRegistryProvisioner
{
    private readonly ILogger<MockRegistryProvisioner> _logger;
    private readonly DatabaseConnectionProvider _connectionProvider;

    public MockRegistryProvisioner(
        ILogger<MockRegistryProvisioner> logger,
        DatabaseConnectionProvider connectionProvider)
    {
        _logger = logger;
        _connectionProvider = connectionProvider;
    }

    public async Task<RegistryProvisioningResult> ProvisionAsync(
        string customerId,
        RegistryType registryType,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock provisioning {RegistryType} for {CustomerId}", registryType, customerId);

        // Check license tier allows this registry
        using var connection = new NpgsqlConnection(_connectionProvider.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var license = await connection.QuerySingleOrDefaultAsync<dynamic>(@"
            SELECT * FROM customer_licenses
            WHERE customer_id = @CustomerId
            AND status = 'active'
        ", new { CustomerId = customerId });

        if (license == null)
        {
            return new RegistryProvisioningResult
            {
                Success = false,
                RegistryType = registryType,
                CustomerId = customerId,
                ErrorMessage = "No active license found",
                ProvisionedAt = DateTimeOffset.UtcNow
            };
        }

        var allowedRegistries = (string[])license.allowed_registries;
        if (!allowedRegistries.Contains(registryType.ToString()))
        {
            return new RegistryProvisioningResult
            {
                Success = false,
                RegistryType = registryType,
                CustomerId = customerId,
                ErrorMessage = $"License tier does not permit access to {registryType}",
                ProvisionedAt = DateTimeOffset.UtcNow
            };
        }

        // Generate mock credentials
        var (registryUrl, namespace_, credential) = registryType switch
        {
            RegistryType.GitHubContainerRegistry => GenerateGitHubCredentials(customerId),
            RegistryType.AwsEcr => GenerateAwsCredentials(customerId),
            RegistryType.AzureAcr => GenerateAzureCredentials(customerId),
            RegistryType.GcpArtifactRegistry => GenerateGcpCredentials(customerId),
            _ => throw new NotSupportedException($"Registry type {registryType} not supported")
        };

        // Store credentials in database
        await connection.ExecuteAsync(@"
            INSERT INTO registry_credentials (
                customer_id,
                registry_type,
                namespace,
                registry_url,
                username,
                expires_at,
                metadata
            )
            VALUES (
                @CustomerId,
                @RegistryType,
                @Namespace,
                @RegistryUrl,
                @Username,
                @ExpiresAt,
                @Metadata::jsonb
            )
        ", new
        {
            CustomerId = customerId,
            RegistryType = registryType.ToString(),
            Namespace = namespace_,
            RegistryUrl = registryUrl,
            Username = credential.Username,
            ExpiresAt = credential.ExpiresAt,
            Metadata = System.Text.Json.JsonSerializer.Serialize(credential.Metadata ?? new Dictionary<string, string>())
        });

        return new RegistryProvisioningResult
        {
            Success = true,
            RegistryType = registryType,
            CustomerId = customerId,
            Namespace = namespace_,
            Credential = credential,
            Metadata = credential.Metadata,
            ProvisionedAt = DateTimeOffset.UtcNow
        };
    }


    private static (string registryUrl, string namespace_, RegistryCredential credential) GenerateGitHubCredentials(string customerId)
    {
        return (
            "ghcr.io",
            $"honua/{customerId}",
            new RegistryCredential
            {
                RegistryUrl = "ghcr.io",
                Username = $"honua-{customerId}",
                Password = $"ghp_mock_{Guid.NewGuid():N}",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                Metadata = new Dictionary<string, string>
                {
                    ["token_type"] = "fine_grained_pat"
                }
            }
        );
    }

    private static (string registryUrl, string namespace_, RegistryCredential credential) GenerateAwsCredentials(string customerId)
    {
        var registryUrl = "123456789012.dkr.ecr.us-west-2.amazonaws.com";
        return (
            registryUrl,
            $"honua/{customerId}",
            new RegistryCredential
            {
                RegistryUrl = registryUrl,
                Username = "AWS",
                Password = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"mock-ecr-token-{customerId}")),
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(12),
                Metadata = new Dictionary<string, string>
                {
                    ["iam_user"] = $"honua-customer-{customerId}",
                    ["repository"] = $"honua/{customerId}"
                }
            }
        );
    }

    private static (string registryUrl, string namespace_, RegistryCredential credential) GenerateAzureCredentials(string customerId)
    {
        return (
            "honuaregistry.azurecr.io",
            $"customers/{customerId}",
            new RegistryCredential
            {
                RegistryUrl = "honuaregistry.azurecr.io",
                Username = $"honua-{customerId}",
                Password = $"mock-azure-token-{Guid.NewGuid():N}",
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
                Metadata = new Dictionary<string, string>
                {
                    ["scope_map"] = $"customer-{customerId}-scope",
                    ["token_type"] = "scoped_access_token"
                }
            }
        );
    }

    private static (string registryUrl, string namespace_, RegistryCredential credential) GenerateGcpCredentials(string customerId)
    {
        var serviceAccountJson = System.Text.Json.JsonSerializer.Serialize(new
        {
            type = "service_account",
            project_id = "honua-project",
            private_key_id = Guid.NewGuid().ToString("N"),
            private_key = "-----BEGIN PRIVATE KEY-----\nMOCK_KEY\n-----END PRIVATE KEY-----\n",
            client_email = $"honua-{customerId}@honua-project.iam.gserviceaccount.com"
        });

        return (
            "us-central1-docker.pkg.dev",
            $"honua-project/honua-builds/{customerId}",
            new RegistryCredential
            {
                RegistryUrl = "us-central1-docker.pkg.dev",
                Username = "_json_key",
                Password = serviceAccountJson,
                Metadata = new Dictionary<string, string>
                {
                    ["service_account_email"] = $"honua-{customerId}@honua-project.iam.gserviceaccount.com",
                    ["project_id"] = "honua-project"
                }
            }
        );
    }
}

/// <summary>
/// Mock implementation of IRegistryAccessManager for testing.
/// </summary>
public class MockRegistryAccessManager : Honua.Server.Intake.Services.IRegistryAccessManager
{
    private readonly ILogger<MockRegistryAccessManager> _logger;
    private readonly DatabaseConnectionProvider _connectionProvider;

    public MockRegistryAccessManager(
        ILogger<MockRegistryAccessManager> logger,
        DatabaseConnectionProvider connectionProvider)
    {
        _logger = logger;
        _connectionProvider = connectionProvider;
    }

    public async Task<RegistryAccessResult> ValidateAccessAsync(
        string customerId,
        RegistryType registryType,
        CancellationToken cancellationToken = default)
    {
        using var connection = new NpgsqlConnection(_connectionProvider.ConnectionString);
        await connection.OpenAsync(cancellationToken);

        var license = await connection.QuerySingleOrDefaultAsync<dynamic>(@"
            SELECT * FROM customer_licenses
            WHERE customer_id = @CustomerId
        ", new { CustomerId = customerId });

        if (license == null)
        {
            return new RegistryAccessResult
            {
                AccessGranted = false,
                CustomerId = customerId,
                RegistryType = registryType,
                DenialReason = "No license found for customer"
            };
        }

        if (license.status != "active")
        {
            return new RegistryAccessResult
            {
                AccessGranted = false,
                CustomerId = customerId,
                RegistryType = registryType,
                DenialReason = $"License status is {license.status}",
                LicenseTier = license.license_tier
            };
        }

        var allowedRegistries = (string[])license.allowed_registries;
        if (!allowedRegistries.Contains(registryType.ToString()))
        {
            return new RegistryAccessResult
            {
                AccessGranted = false,
                CustomerId = customerId,
                RegistryType = registryType,
                DenialReason = $"License tier {license.license_tier} does not permit access to {registryType}",
                LicenseTier = license.license_tier
            };
        }

        return new RegistryAccessResult
        {
            AccessGranted = true,
            CustomerId = customerId,
            RegistryType = registryType,
            LicenseTier = license.license_tier
        };
    }

    public Task<RegistryAccessResult> GenerateRegistryTokenAsync(
        string customerId,
        RegistryType registryType,
        CancellationToken cancellationToken = default)
    {
        var token = $"mock-token-{customerId}-{registryType}-{Guid.NewGuid():N}";

        return Task.FromResult(new RegistryAccessResult
        {
            AccessGranted = true,
            CustomerId = customerId,
            RegistryType = registryType,
            AccessToken = token,
            TokenExpiresAt = DateTimeOffset.UtcNow.AddHours(1)
        });
    }
}

/// <summary>
/// Mock implementation of IBuildDeliveryService for testing.
/// </summary>
public class MockBuildDeliveryService : IBuildDeliveryService
{
    private readonly ILogger<MockBuildDeliveryService> _logger;
    private readonly IRegistryAccessManager _accessManager;

    public MockBuildDeliveryService(
        ILogger<MockBuildDeliveryService> logger,
        IRegistryAccessManager accessManager)
    {
        _logger = logger;
        _accessManager = accessManager;
    }

    public async Task<BuildDeliveryResult> DeliverBuildAsync(
        BuildCacheKey cacheKey,
        RegistryType targetRegistry,
        string? sourceBuildPath = null,
        CancellationToken cancellationToken = default)
    {
        var accessResult = await _accessManager.ValidateAccessAsync(
            cacheKey.CustomerId,
            targetRegistry,
            cancellationToken);

        if (!accessResult.AccessGranted)
        {
            return new BuildDeliveryResult
            {
                Success = false,
                CacheKey = cacheKey,
                TargetRegistry = targetRegistry,
                ErrorMessage = accessResult.DenialReason ?? "Access denied",
                CompletedAt = DateTimeOffset.UtcNow
            };
        }

        var imageReference = $"{GetRegistryUrl(targetRegistry)}/{cacheKey.CustomerId}/{cacheKey.BuildName}:{cacheKey.GenerateTag()}";

        return new BuildDeliveryResult
        {
            Success = true,
            CacheKey = cacheKey,
            TargetRegistry = targetRegistry,
            ImageReference = imageReference,
            Digest = $"sha256:{Guid.NewGuid():N}",
            WasCached = false,
            AdditionalTags = new List<string> { "latest" },
            CompletedAt = DateTimeOffset.UtcNow
        };
    }

    public Task<bool> CopyImageAsync(
        string sourceImage,
        string targetImage,
        RegistryCredential? sourceCredential = null,
        RegistryCredential? targetCredential = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Mock copying image from {Source} to {Target}", sourceImage, targetImage);
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<string>> TagImageAsync(
        string imageReference,
        IEnumerable<string> additionalTags,
        RegistryCredential? credential = null,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<string>>(additionalTags.ToList());
    }

    private static string GetRegistryUrl(RegistryType registryType)
    {
        return registryType switch
        {
            RegistryType.GitHubContainerRegistry => "ghcr.io",
            RegistryType.AwsEcr => "123456789012.dkr.ecr.us-west-2.amazonaws.com",
            RegistryType.AzureAcr => "honuaregistry.azurecr.io",
            RegistryType.GcpArtifactRegistry => "us-central1-docker.pkg.dev",
            _ => throw new NotSupportedException()
        };
    }
}

