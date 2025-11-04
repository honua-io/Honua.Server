// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;

namespace Honua.Cli.AI.Services.Dns;

/// <summary>
/// Factory for creating Azure Resource Manager clients with managed identity support.
/// </summary>
public sealed class AzureDnsClientFactory
{
    private readonly ILogger<AzureDnsClientFactory> _logger;

    public AzureDnsClientFactory(ILogger<AzureDnsClientFactory> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Creates an ArmClient using DefaultAzureCredential which supports:
    /// - Managed Identity (in production)
    /// - Azure CLI credentials (local development)
    /// - Environment variables
    /// - Visual Studio credentials
    /// - Azure PowerShell credentials
    /// </summary>
    public ArmClient CreateClient()
    {
        try
        {
            _logger.LogInformation("Creating Azure Resource Manager client with DefaultAzureCredential");

            var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                // Exclude credential types that may cause unnecessary delays
                // Note: ExcludeSharedTokenCacheCredential is obsolete in newer Azure.Identity versions
                // but we keep it for compatibility with older versions
#pragma warning disable CS0618 // Type or member is obsolete
                ExcludeSharedTokenCacheCredential = true,
#pragma warning restore CS0618
                ExcludeVisualStudioCredential = false,
                ExcludeVisualStudioCodeCredential = true,
                ExcludeAzurePowerShellCredential = false,
                ExcludeInteractiveBrowserCredential = true,

                // Enable detailed logging for troubleshooting
                Diagnostics =
                {
                    LoggedHeaderNames = { "x-ms-request-id" },
                    LoggedQueryParameters = { "api-version" },
                    IsLoggingContentEnabled = true
                }
            });

            var armClient = new ArmClient(credential);

            _logger.LogInformation("Azure Resource Manager client created successfully");
            return armClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Azure Resource Manager client");
            throw;
        }
    }

    /// <summary>
    /// Creates an ArmClient using Managed Identity specifically.
    /// Useful when you want to ensure managed identity is being used.
    /// </summary>
    public ArmClient CreateManagedIdentityClient(string? clientId = null)
    {
        try
        {
            _logger.LogInformation("Creating Azure Resource Manager client with Managed Identity");

            TokenCredential credential = clientId.IsNullOrEmpty()
                ? new ManagedIdentityCredential()
                : new ManagedIdentityCredential(clientId);

            var armClient = new ArmClient(credential);

            _logger.LogInformation("Azure Resource Manager client created with Managed Identity");
            return armClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Azure Resource Manager client with Managed Identity");
            throw;
        }
    }

    /// <summary>
    /// Creates an ArmClient using a Service Principal (client secret).
    /// </summary>
    public ArmClient CreateServicePrincipalClient(
        string tenantId,
        string clientId,
        string clientSecret)
    {
        try
        {
            _logger.LogInformation("Creating Azure Resource Manager client with Service Principal");

            var credential = new ClientSecretCredential(tenantId, clientId, clientSecret);
            var armClient = new ArmClient(credential);

            _logger.LogInformation("Azure Resource Manager client created with Service Principal");
            return armClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Azure Resource Manager client with Service Principal");
            throw;
        }
    }

    /// <summary>
    /// Creates an ArmClient using Azure CLI credentials (useful for local development).
    /// </summary>
    public ArmClient CreateAzureCliClient()
    {
        try
        {
            _logger.LogInformation("Creating Azure Resource Manager client with Azure CLI credentials");

            var credential = new AzureCliCredential();
            var armClient = new ArmClient(credential);

            _logger.LogInformation("Azure Resource Manager client created with Azure CLI credentials");
            return armClient;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create Azure Resource Manager client with Azure CLI credentials");
            throw;
        }
    }
}
