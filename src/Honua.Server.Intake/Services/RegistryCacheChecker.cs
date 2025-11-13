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
    private readonly ILogger<RegistryCacheChecker> logger;
    private readonly RegistryProvisioningOptions options;
    private readonly IHttpClientFactory httpClientFactory;
    private readonly AsyncRetryPolicy retryPolicy;

    public RegistryCacheChecker(
        ILogger<RegistryCacheChecker> logger,
        IOptions<RegistryProvisioningOptions> options,
        IHttpClientFactory httpClientFactory)
    {
        this.logger = logger ?? throw new ArgumentNullException(nameof(logger));
        this.options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        this.httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));

        // Configure retry policy with exponential backoff
        this.retryPolicy = Policy
            .Handle<Exception>(ex => IsTransientError(ex))
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                onRetry: (exception, timeSpan, retryCount, context) =>
                {
                    this.logger.LogWarning(
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

        this.logger.LogInformation(
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
            this.logger.LogError(ex, "Failed to check cache for build {BuildName}", cacheKey.BuildName);
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
        if (string.IsNullOrWhiteSpace(this.options.GitHubToken))
        {
            throw new InvalidOperationException("GitHub token is not configured");
        }

        if (string.IsNullOrWhiteSpace(this.options.GitHubOrganization))
        {
            throw new InvalidOperationException("GitHub organization is not configured");
        }

        return await this.retryPolicy.ExecuteAsync(async () =>
        {
            var tag = cacheKey.GenerateTag();
            var imageReference = cacheKey.GenerateImageReference(
                "ghcr.io",
                this.options.GitHubOrganization);

            // GitHub Container Registry uses OCI Distribution API
            // Format: https://ghcr.io/v2/{org}/{image}/manifests/{tag}
            var manifestUrl = $"https://ghcr.io/v2/{this.options.GitHubOrganization}/customers/{cacheKey.CustomerId}/{cacheKey.BuildName}/manifests/{tag}";

            using var client = this.httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", this.options.GitHubToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.oci.image.manifest.v1+json"));
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.docker.distribution.manifest.v2+json"));

            this.logger.LogDebug("Checking GHCR manifest at {Url}", manifestUrl);

            using var request = new HttpRequestMessage(HttpMethod.Head, manifestUrl);
            var response = await client.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var digest = response.Headers.GetValues("Docker-Content-Digest").FirstOrDefault();

                this.logger.LogInformation(
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
                this.logger.LogInformation("Image not found in GHCR: {ImageReference}", imageReference);

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
        if (string.IsNullOrWhiteSpace(this.options.AwsRegion))
        {
            throw new InvalidOperationException("AWS region is not configured");
        }

        if (string.IsNullOrWhiteSpace(this.options.AwsAccountId))
        {
            throw new InvalidOperationException("AWS account ID is not configured");
        }

        return await this.retryPolicy.ExecuteAsync(async () =>
        {
            var ecrClient = new AmazonECRClient(Amazon.RegionEndpoint.GetBySystemName(this.options.AwsRegion));

            var repositoryName = $"honua/{cacheKey.CustomerId}";
            var tag = cacheKey.GenerateTag();
            var registryUrl = $"{this.options.AwsAccountId}.dkr.ecr.{this.options.AwsRegion}.amazonaws.com";
            var imageReference = $"{registryUrl}/{repositoryName}/{cacheKey.BuildName}:{tag}";

            this.logger.LogDebug(
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

                    this.logger.LogInformation(
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
                this.logger.LogInformation("Image not found in ECR: {ImageReference}", imageReference);
            }
            catch (RepositoryNotFoundException)
            {
                this.logger.LogInformation(
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
        if (string.IsNullOrWhiteSpace(this.options.AzureSubscriptionId))
        {
            throw new InvalidOperationException("Azure subscription ID is not configured");
        }

        if (string.IsNullOrWhiteSpace(this.options.AzureResourceGroup))
        {
            throw new InvalidOperationException("Azure resource group is not configured");
        }

        if (string.IsNullOrWhiteSpace(this.options.AzureRegistryName))
        {
            throw new InvalidOperationException("Azure registry name is not configured");
        }

        return await this.retryPolicy.ExecuteAsync(async () =>
        {
            var credential = new DefaultAzureCredential();
            var armClient = new ArmClient(credential);

            var tag = cacheKey.GenerateTag();
            var repositoryPath = $"customers/{cacheKey.CustomerId}/{cacheKey.BuildName}";
            var registryUrl = $"{this.options.AzureRegistryName}.azurecr.io";
            var imageReference = $"{registryUrl}/{repositoryPath}:{tag}";

            this.logger.LogDebug(
                "Checking ACR for artifact {Repository}:{Tag}",
                repositoryPath,
                tag);

            // Use ACR REST API to check for manifest
            // For now, we'll use HTTP client with ACR authentication
            var manifestUrl = $"https://{registryUrl}/v2/{repositoryPath}/manifests/{tag}";

            using var client = this.httpClientFactory.CreateClient();

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

                    this.logger.LogInformation(
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
                    this.logger.LogInformation("Image not found in ACR: {ImageReference}", imageReference);

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
                this.logger.LogWarning(ex, "Failed to check ACR manifest, assuming not found");
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
        if (string.IsNullOrWhiteSpace(this.options.GcpProjectId))
        {
            throw new InvalidOperationException("GCP project ID is not configured");
        }

        if (string.IsNullOrWhiteSpace(this.options.GcpRegion))
        {
            throw new InvalidOperationException("GCP region is not configured");
        }

        if (string.IsNullOrWhiteSpace(this.options.GcpRepositoryName))
        {
            throw new InvalidOperationException("GCP repository name is not configured");
        }

        return await this.retryPolicy.ExecuteAsync(async () =>
        {
            var tag = cacheKey.GenerateTag();
            var packagePath = $"customers/{cacheKey.CustomerId}/{cacheKey.BuildName}";
            var registryUrl = $"{this.options.GcpRegion}-docker.pkg.dev";
            var imageReference = $"{registryUrl}/{this.options.GcpProjectId}/{this.options.GcpRepositoryName}/{packagePath}:{tag}";

            this.logger.LogDebug(
                "Checking GCP Artifact Registry for package {Package}:{Tag}",
                packagePath,
                tag);

            // Use Artifact Registry API
            var client = await ArtifactRegistryClient.CreateAsync(cancellationToken);

            try
            {
                var versionName = $"projects/{this.options.GcpProjectId}/locations/{this.options.GcpRegion}/repositories/{this.options.GcpRepositoryName}/packages/{packagePath.Replace("/", "%2F")}/versions/{tag}";

                var version = await client.GetVersionAsync(versionName, cancellationToken);

                if (version != null)
                {
                    this.logger.LogInformation(
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
                this.logger.LogInformation(
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
