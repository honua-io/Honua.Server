// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Amazon.ECR;
using Amazon.ECR.Model;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.ContainerRegistry;
using Google.Cloud.ArtifactRegistry.V1;
using Honua.Server.Intake.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using Polly.Retry;

namespace Honua.Server.Intake.Services;

/// <summary>
/// Interface for checking if builds exist in container registries.
/// </summary>
public interface IRegistryCacheChecker
{
    /// <summary>
    /// Checks if a build exists in the registry cache.
    /// </summary>
    Task<CacheCheckResult> CheckCacheAsync(
        BuildCacheKey cacheKey,
        RegistryType registryType,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Checks if builds exist in container registry caches.
/// </summary>
public sealed class RegistryCacheChecker : IRegistryCacheChecker
{
    private readonly ILogger<RegistryCacheChecker> _logger;
    private readonly RegistryProvisioningOptions _options;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AsyncRetryPolicy _retryPolicy;

    public RegistryCacheChecker(
        ILogger<RegistryCacheChecker> logger,
        IOptions<RegistryProvisioningOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        // Configure retry policy with exponential backoff
        _retryPolicy = Policy
            .Handle<Exception>(ex => IsTransientError(ex))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        exception,
                        "Retry {RetryCount} after {Delay}ms due to transient error",
                        retryCount,
                        timeSpan.TotalMilliseconds);
                });
    }

    /// <inheritdoc/>
    public async Task<CacheCheckResult> CheckCacheAsync(
        BuildCacheKey cacheKey,
        RegistryType registryType,
        CancellationToken cancellationToken = default)
    {
        if (cacheKey == null)
        {
            throw new ArgumentNullException(nameof(cacheKey));
        }

        _logger.LogInformation(
            "Checking cache for build {BuildName}:{Version} in {RegistryType}",
            cacheKey.BuildName,
            cacheKey.Version,
            registryType);

        try
        {
            return registryType switch
            {
                RegistryType.GitHubContainerRegistry => await CheckGitHubRegistryAsync(cacheKey, cancellationToken),
                RegistryType.AwsEcr => await CheckEcrAsync(cacheKey, cancellationToken),
                RegistryType.AzureAcr => await CheckAcrAsync(cacheKey, cancellationToken),
                RegistryType.GcpArtifactRegistry => await CheckGcrAsync(cacheKey, cancellationToken),
                _ => throw new NotSupportedException($"Registry type {registryType} is not supported")
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check cache for build {BuildName}", cacheKey.BuildName);
            return new CacheCheckResult
            {
                Exists = false,
                RegistryType = registryType,
                Tag = cacheKey.GenerateTag(),
                ImageReference = string.Empty,
                ErrorMessage = ex.Message
            };
        }
    }

    /// <summary>
    /// Checks if an image exists in GitHub Container Registry using HEAD request.
    /// </summary>
    private async Task<CacheCheckResult> CheckGitHubRegistryAsync(
        BuildCacheKey cacheKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.GitHubToken))
        {
            throw new InvalidOperationException("GitHub token is not configured");
        }

        if (string.IsNullOrWhiteSpace(_options.GitHubOrganization))
        {
            throw new InvalidOperationException("GitHub organization is not configured");
        }

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var tag = cacheKey.GenerateTag();
            var imageReference = cacheKey.GenerateImageReference(
                "ghcr.io",
                _options.GitHubOrganization);

            // GitHub Container Registry uses OCI Distribution API
            // Format: https://ghcr.io/v2/{org}/{image}/manifests/{tag}
            var manifestUrl = $"https://ghcr.io/v2/{_options.GitHubOrganization}/customers/{cacheKey.CustomerId}/{cacheKey.BuildName}/manifests/{tag}";

            using var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.GitHubToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));

            _logger.LogDebug("Checking GHCR manifest at {Url}", manifestUrl);

            using var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var digest = response.Headers.GetValues("Docker-Content-Digest").FirstOrDefault();

                _logger.LogInformation(
                    "Found cached image in GHCR: {ImageReference} with digest {Digest}",
                    imageReference,
                    digest);

                return new CacheCheckResult
                {
                    Exists = true,
                    RegistryType = RegistryType.GitHubContainerRegistry,
                    Tag = tag,
                    ImageReference = imageReference,
                    Digest = digest
                };
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInformation("Image not found in GHCR: {ImageReference}", imageReference);

                return new CacheCheckResult
                {
                    Exists = false,
                    RegistryType = RegistryType.GitHubContainerRegistry,
                    Tag = tag,
                    ImageReference = imageReference
                };
            }

            throw new HttpRequestException(
                $"Failed to check GHCR manifest: {response.StatusCode} - {response.ReasonPhrase}");
        });
    }

    /// <summary>
    /// Checks if an image exists in AWS ECR using DescribeImages API.
    /// </summary>
    private async Task<CacheCheckResult> CheckEcrAsync(
        BuildCacheKey cacheKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AwsRegion))
        {
            throw new InvalidOperationException("AWS region is not configured");
        }

        if (string.IsNullOrWhiteSpace(_options.AwsAccountId))
        {
            throw new InvalidOperationException("AWS account ID is not configured");
        }

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var ecrClient = new AmazonECRClient(Amazon.RegionEndpoint.GetBySystemName(_options.AwsRegion));

            var repositoryName = $"honua/{cacheKey.CustomerId}";
            var tag = cacheKey.GenerateTag();
            var registryUrl = $"{_options.AwsAccountId}.dkr.ecr.{_options.AwsRegion}.amazonaws.com";
            var imageReference = $"{registryUrl}/{repositoryName}/{cacheKey.BuildName}:{tag}";

            _logger.LogDebug(
                "Checking ECR for image {Repository}:{Tag}",
                repositoryName,
                tag);

            try
            {
                var response = await ecrClient.DescribeImagesAsync(new DescribeImagesRequest
                {
                    RepositoryName = $"{repositoryName}/{cacheKey.BuildName}",
                    ImageIds = new System.Collections.Generic.List<ImageIdentifier>
                    {
                        new() { ImageTag = tag }
                    }
                }, cancellationToken);

                if (response.ImageDetails.Count > 0)
                {
                    var imageDetail = response.ImageDetails[0];
                    var digest = imageDetail.ImageDigest;
                    var lastUpdated = imageDetail.ImagePushedAt;

                    _logger.LogInformation(
                        "Found cached image in ECR: {ImageReference} with digest {Digest}",
                        imageReference,
                        digest);

                    return new CacheCheckResult
                    {
                        Exists = true,
                        RegistryType = RegistryType.AwsEcr,
                        Tag = tag,
                        ImageReference = imageReference,
                        Digest = digest,
                        LastUpdated = lastUpdated
                    };
                }
            }
            catch (ImageNotFoundException)
            {
                _logger.LogInformation("Image not found in ECR: {ImageReference}", imageReference);
            }
            catch (RepositoryNotFoundException)
            {
                _logger.LogInformation(
                    "Repository not found in ECR: {Repository}",
                    $"{repositoryName}/{cacheKey.BuildName}");
            }

            return new CacheCheckResult
            {
                Exists = false,
                RegistryType = RegistryType.AwsEcr,
                Tag = tag,
                ImageReference = imageReference
            };
        });
    }

    /// <summary>
    /// Checks if an artifact exists in Azure Container Registry.
    /// </summary>
    private async Task<CacheCheckResult> CheckAcrAsync(
        BuildCacheKey cacheKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AzureSubscriptionId))
        {
            throw new InvalidOperationException("Azure subscription ID is not configured");
        }

        if (string.IsNullOrWhiteSpace(_options.AzureResourceGroup))
        {
            throw new InvalidOperationException("Azure resource group is not configured");
        }

        if (string.IsNullOrWhiteSpace(_options.AzureRegistryName))
        {
            throw new InvalidOperationException("Azure registry name is not configured");
        }

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var credential = new DefaultAzureCredential();
            var armClient = new ArmClient(credential);

            var tag = cacheKey.GenerateTag();
            var repositoryPath = $"customers/{cacheKey.CustomerId}/{cacheKey.BuildName}";
            var registryUrl = $"{_options.AzureRegistryName}.azurecr.io";
            var imageReference = $"{registryUrl}/{repositoryPath}:{tag}";

            _logger.LogDebug(
                "Checking ACR for artifact {Repository}:{Tag}",
                repositoryPath,
                tag);

            // Use ACR REST API to check for manifest
            // For now, we'll use HTTP client with ACR authentication
            var manifestUrl = $"https://{registryUrl}/v2/{repositoryPath}/manifests/{tag}";

            using var client = _httpClientFactory.CreateClient();

            // Note: In production, you would need to authenticate properly with ACR
            // This might involve getting an access token from Azure AD
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
                var response = await client.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var digest = response.Headers.GetValues("Docker-Content-Digest").FirstOrDefault();

                    _logger.LogInformation(
                        "Found cached image in ACR: {ImageReference} with digest {Digest}",
                        imageReference,
                        digest);

                    return new CacheCheckResult
                    {
                        Exists = true,
                        RegistryType = RegistryType.AzureAcr,
                        Tag = tag,
                        ImageReference = imageReference,
                        Digest = digest
                    };
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Image not found in ACR: {ImageReference}", imageReference);

                    return new CacheCheckResult
                    {
                        Exists = false,
                        RegistryType = RegistryType.AzureAcr,
                        Tag = tag,
                        ImageReference = imageReference
                    };
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Failed to check ACR manifest, assuming not found");
            }

            return new CacheCheckResult
            {
                Exists = false,
                RegistryType = RegistryType.AzureAcr,
                Tag = tag,
                ImageReference = imageReference
            };
        });
    }

    /// <summary>
    /// Checks if an artifact exists in GCP Artifact Registry.
    /// </summary>
    private async Task<CacheCheckResult> CheckGcrAsync(
        BuildCacheKey cacheKey,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.GcpProjectId))
        {
            throw new InvalidOperationException("GCP project ID is not configured");
        }

        if (string.IsNullOrWhiteSpace(_options.GcpRegion))
        {
            throw new InvalidOperationException("GCP region is not configured");
        }

        if (string.IsNullOrWhiteSpace(_options.GcpRepositoryName))
        {
            throw new InvalidOperationException("GCP repository name is not configured");
        }

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var tag = cacheKey.GenerateTag();
            var packagePath = $"customers/{cacheKey.CustomerId}/{cacheKey.BuildName}";
            var registryUrl = $"{_options.GcpRegion}-docker.pkg.dev";
            var imageReference = $"{registryUrl}/{_options.GcpProjectId}/{_options.GcpRepositoryName}/{packagePath}:{tag}";

            _logger.LogDebug(
                "Checking GCP Artifact Registry for package {Package}:{Tag}",
                packagePath,
                tag);

            // Use Artifact Registry API
            var client = await ArtifactRegistryClient.CreateAsync(cancellationToken);

            try
            {
                var versionName = $"projects/{_options.GcpProjectId}/locations/{_options.GcpRegion}/repositories/{_options.GcpRepositoryName}/packages/{packagePath.Replace("/", "%2F")}/versions/{tag}";

                var version = await client.GetVersionAsync(versionName, cancellationToken);

                if (version != null)
                {
                    _logger.LogInformation(
                        "Found cached image in GCP Artifact Registry: {ImageReference}",
                        imageReference);

                    return new CacheCheckResult
                    {
                        Exists = true,
                        RegistryType = RegistryType.GcpArtifactRegistry,
                        Tag = tag,
                        ImageReference = imageReference,
                        LastUpdated = version.UpdateTime?.ToDateTimeOffset()
                    };
                }
            }
            catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
            {
                _logger.LogInformation(
                    "Image not found in GCP Artifact Registry: {ImageReference}",
                    imageReference);
            }

            return new CacheCheckResult
            {
                Exists = false,
                RegistryType = RegistryType.GcpArtifactRegistry,
                Tag = tag,
                ImageReference = imageReference
            };
        });
    }

    /// <summary>
    /// Determines if an exception is transient and should be retried.
    /// </summary>
    private static bool IsTransientError(Exception ex)
    {
        return ex is TimeoutException
            || ex is HttpRequestException
            || (ex is Amazon.Runtime.AmazonServiceException awsEx &&
                (awsEx.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable ||
                 awsEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests))
            || (ex is Grpc.Core.RpcException rpcEx &&
                (rpcEx.StatusCode == Grpc.Core.StatusCode.Unavailable ||
                 rpcEx.StatusCode == Grpc.Core.StatusCode.DeadlineExceeded));
    }
}
