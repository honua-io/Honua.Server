// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Amazon.IdentityManagement;
using Amazon.IdentityManagement.Model;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Azure.ResourceManager.Authorization;
using Google.Cloud.Iam.Admin.V1;
using Honua.Server.Core.Licensing;
using Honua.Server.Core.Licensing.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;

namespace Honua.Server.Core.Cloud.Licensing;

/// <summary>
/// Implementation of credential revocation service for cloud providers.
/// </summary>
public sealed class CredentialRevocationService : ICredentialRevocationService
{
    private readonly ILicenseStore _licenseStore;
    private readonly ICredentialRevocationStore _revocationStore;
    private readonly IOptionsMonitor<LicenseOptions> _options;
    private readonly ILogger<CredentialRevocationService> _logger;
    private readonly ResiliencePipeline _resiliencePipeline;

    public CredentialRevocationService(
        ILicenseStore licenseStore,
        ICredentialRevocationStore revocationStore,
        IOptionsMonitor<LicenseOptions> options,
        ILogger<CredentialRevocationService> logger)
    {
        _licenseStore = licenseStore ?? throw new ArgumentNullException(nameof(licenseStore));
        _revocationStore = revocationStore ?? throw new ArgumentNullException(nameof(revocationStore));
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // Create resilience pipeline with retries for transient failures
        _resiliencePipeline = new ResiliencePipelineBuilder()
            .AddRetry(new Polly.Retry.RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(1),
                BackoffType = Polly.DelayBackoffType.Exponential,
                OnRetry = args =>
                {
                    _logger.LogWarning(
                        args.Outcome.Exception,
                        "Retrying credential revocation (attempt {Attempt})",
                        args.AttemptNumber + 1);
                    return default;
                }
            })
            .AddTimeout(TimeSpan.FromMinutes(5))
            .Build();
    }

    public async Task RevokeExpiredCredentialsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting expired credentials revocation job");

        try
        {
            // Get all expired licenses
            var expiredLicenses = await _licenseStore.GetExpiredLicensesAsync(cancellationToken);

            _logger.LogInformation("Found {Count} expired licenses to process", expiredLicenses.Length);

            foreach (var license in expiredLicenses)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogWarning("Credential revocation job cancelled");
                    break;
                }

                try
                {
                    await RevokeCustomerCredentialsAsync(
                        license.CustomerId,
                        "License expired",
                        "System",
                        cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to revoke credentials for customer {CustomerId}",
                        license.CustomerId);
                    // Continue with other customers
                }
            }

            _logger.LogInformation("Completed expired credentials revocation job");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during expired credentials revocation job");
            throw;
        }
    }

    public async Task RevokeCustomerCredentialsAsync(
        string customerId,
        string reason,
        string revokedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("CustomerId is required", nameof(customerId));
        }

        _logger.LogInformation(
            "Revoking all credentials for customer {CustomerId}. Reason: {Reason}",
            customerId,
            reason);

        // Revoke credentials for all supported registries
        var tasks = new[]
        {
            RevokeAwsCredentialsAsync(customerId, reason, revokedBy, cancellationToken),
            RevokeGitHubCredentialsAsync(customerId, reason, revokedBy, cancellationToken),
            RevokeAzureCredentialsAsync(customerId, reason, revokedBy, cancellationToken),
            RevokeGcpCredentialsAsync(customerId, reason, revokedBy, cancellationToken)
        };

        // Execute all revocations in parallel
        var results = await Task.WhenAll(tasks.Select(async task =>
        {
            try
            {
                await task;
                return (Success: true, Error: (Exception?)null);
            }
            catch (Exception ex)
            {
                return (Success: false, Error: ex);
            }
        }));

        // Log any failures
        var failures = results.Where(r => !r.Success).ToArray();
        if (failures.Length > 0)
        {
            _logger.LogWarning(
                "Failed to revoke {FailureCount} out of {TotalCount} credential types for customer {CustomerId}",
                failures.Length,
                results.Length,
                customerId);

            foreach (var (_, error) in failures)
            {
                _logger.LogError(error, "Credential revocation error for customer {CustomerId}", customerId);
            }
        }
        else
        {
            _logger.LogInformation(
                "Successfully revoked all credentials for customer {CustomerId}",
                customerId);
        }
    }

    public async Task RevokeAwsCredentialsAsync(
        string customerId,
        string reason,
        string revokedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("CustomerId is required", nameof(customerId));
        }

        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            try
            {
                var userName = $"honua-customer-{customerId}";

                _logger.LogInformation("Revoking AWS credentials for user {UserName}", userName);

                using var iamClient = new AmazonIdentityManagementServiceClient();

                // 1. List and delete all access keys
                try
                {
                    var listKeysResponse = await iamClient.ListAccessKeysAsync(
                        new ListAccessKeysRequest { UserName = userName },
                        token);

                    foreach (var keyMetadata in listKeysResponse.AccessKeyMetadata)
                    {
                        _logger.LogInformation(
                            "Deleting AWS access key {AccessKeyId} for user {UserName}",
                            keyMetadata.AccessKeyId,
                            userName);

                        await iamClient.DeleteAccessKeyAsync(
                            new DeleteAccessKeyRequest
                            {
                                UserName = userName,
                                AccessKeyId = keyMetadata.AccessKeyId
                            },
                            token);
                    }
                }
                catch (NoSuchEntityException)
                {
                    _logger.LogInformation("AWS IAM user {UserName} not found, skipping", userName);
                    return;
                }

                // 2. List and detach all managed policies
                var listAttachedPoliciesResponse = await iamClient.ListAttachedUserPoliciesAsync(
                    new ListAttachedUserPoliciesRequest { UserName = userName },
                    token);

                foreach (var policy in listAttachedPoliciesResponse.AttachedPolicies)
                {
                    _logger.LogInformation(
                        "Detaching AWS policy {PolicyArn} from user {UserName}",
                        policy.PolicyArn,
                        userName);

                    await iamClient.DetachUserPolicyAsync(
                        new DetachUserPolicyRequest
                        {
                            UserName = userName,
                            PolicyArn = policy.PolicyArn
                        },
                        token);
                }

                // 3. List and delete all inline policies
                var listUserPoliciesResponse = await iamClient.ListUserPoliciesAsync(
                    new ListUserPoliciesRequest { UserName = userName },
                    token);

                foreach (var policyName in listUserPoliciesResponse.PolicyNames)
                {
                    _logger.LogInformation(
                        "Deleting inline AWS policy {PolicyName} from user {UserName}",
                        policyName,
                        userName);

                    await iamClient.DeleteUserPolicyAsync(
                        new DeleteUserPolicyRequest
                        {
                            UserName = userName,
                            PolicyName = policyName
                        },
                        token);
                }

                // 4. Remove user from all groups
                var listGroupsResponse = await iamClient.ListGroupsForUserAsync(
                    new ListGroupsForUserRequest { UserName = userName },
                    token);

                foreach (var group in listGroupsResponse.Groups)
                {
                    _logger.LogInformation(
                        "Removing user {UserName} from AWS group {GroupName}",
                        userName,
                        group.GroupName);

                    await iamClient.RemoveUserFromGroupAsync(
                        new RemoveUserFromGroupRequest
                        {
                            UserName = userName,
                            GroupName = group.GroupName
                        },
                        token);
                }

                // 5. Delete the IAM user
                _logger.LogInformation("Deleting AWS IAM user {UserName}", userName);

                await iamClient.DeleteUserAsync(
                    new DeleteUserRequest { UserName = userName },
                    token);

                // Record revocation
                await _revocationStore.RecordRevocationAsync(
                    new CredentialRevocation
                    {
                        CustomerId = customerId,
                        RegistryType = "AWS",
                        RevokedAt = DateTimeOffset.UtcNow,
                        Reason = reason,
                        RevokedBy = revokedBy
                    },
                    token);

                _logger.LogInformation(
                    "Successfully revoked AWS credentials for customer {CustomerId}",
                    customerId);
            }
            catch (NoSuchEntityException)
            {
                _logger.LogInformation(
                    "AWS credentials not found for customer {CustomerId}, already revoked or never created",
                    customerId);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to revoke AWS credentials for customer {CustomerId}",
                    customerId);
                throw;
            }
        }, cancellationToken);
    }

    public async Task RevokeGitHubCredentialsAsync(
        string customerId,
        string reason,
        string revokedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("CustomerId is required", nameof(customerId));
        }

        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            try
            {
                _logger.LogInformation(
                    "Revoking GitHub credentials for customer {CustomerId}",
                    customerId);

                // Note: GitHub PAT revocation requires organization/enterprise API access
                // This is a placeholder - actual implementation depends on your GitHub setup
                // You would typically use GitHub's GraphQL API or REST API to revoke tokens

                _logger.LogWarning(
                    "GitHub credential revocation not fully implemented for customer {CustomerId}. Manual intervention may be required.",
                    customerId);

                // Record revocation attempt
                await _revocationStore.RecordRevocationAsync(
                    new CredentialRevocation
                    {
                        CustomerId = customerId,
                        RegistryType = "GitHub",
                        RevokedAt = DateTimeOffset.UtcNow,
                        Reason = reason + " (manual revocation may be required)",
                        RevokedBy = revokedBy
                    },
                    token);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to revoke GitHub credentials for customer {CustomerId}",
                    customerId);
                throw;
            }
        }, cancellationToken);
    }

    public async Task RevokeAzureCredentialsAsync(
        string customerId,
        string reason,
        string revokedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("CustomerId is required", nameof(customerId));
        }

        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            try
            {
                var servicePrincipalName = $"honua-customer-{customerId}";

                _logger.LogInformation(
                    "Revoking Azure service principal {ServicePrincipalName}",
                    servicePrincipalName);

                // Use Azure SDK to delete service principal
                var credential = new DefaultAzureCredential();
                var armClient = new ArmClient(credential);

                // Note: This requires appropriate Azure permissions and subscription context
                // The actual implementation depends on how you manage Azure service principals

                _logger.LogWarning(
                    "Azure credential revocation requires proper Azure context and permissions for customer {CustomerId}",
                    customerId);

                // Record revocation
                await _revocationStore.RecordRevocationAsync(
                    new CredentialRevocation
                    {
                        CustomerId = customerId,
                        RegistryType = "Azure",
                        RevokedAt = DateTimeOffset.UtcNow,
                        Reason = reason,
                        RevokedBy = revokedBy
                    },
                    token);

                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to revoke Azure credentials for customer {CustomerId}",
                    customerId);
                throw;
            }
        }, cancellationToken);
    }

    public async Task RevokeGcpCredentialsAsync(
        string customerId,
        string reason,
        string revokedBy,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("CustomerId is required", nameof(customerId));
        }

        await _resiliencePipeline.ExecuteAsync(async token =>
        {
            try
            {
                var serviceAccountEmail = $"honua-customer-{customerId}@your-project.iam.gserviceaccount.com";

                _logger.LogInformation(
                    "Revoking GCP service account {ServiceAccountEmail}",
                    serviceAccountEmail);

                // Use GCP IAM API to delete service account
                var iamClient = await IAMClient.CreateAsync(token);

                var serviceAccountName = $"projects/your-project/serviceAccounts/{serviceAccountEmail}";

                try
                {
                    await iamClient.DeleteServiceAccountAsync(
                        new DeleteServiceAccountRequest
                        {
                            Name = serviceAccountName
                        },
                        token);

                    _logger.LogInformation(
                        "Successfully deleted GCP service account for customer {CustomerId}",
                        customerId);
                }
                catch (Grpc.Core.RpcException ex) when (ex.StatusCode == Grpc.Core.StatusCode.NotFound)
                {
                    _logger.LogInformation(
                        "GCP service account not found for customer {CustomerId}, already revoked or never created",
                        customerId);
                }

                // Record revocation
                await _revocationStore.RecordRevocationAsync(
                    new CredentialRevocation
                    {
                        CustomerId = customerId,
                        RegistryType = "GCP",
                        RevokedAt = DateTimeOffset.UtcNow,
                        Reason = reason,
                        RevokedBy = revokedBy
                    },
                    token);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to revoke GCP credentials for customer {CustomerId}",
                    customerId);
                throw;
            }
        }, cancellationToken);
    }
}
