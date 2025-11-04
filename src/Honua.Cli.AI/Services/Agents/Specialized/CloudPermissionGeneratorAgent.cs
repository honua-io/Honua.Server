// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.AI;
using Microsoft.Extensions.Logging;
using Honua.Server.Core.Extensions;
using Honua.Cli.AI.Serialization;


namespace Honua.Cli.AI.Services.Agents.Specialized;

/// <summary>
/// Specialized agent that analyzes deployment topology and generates TWO sets of least-privilege IAM policies:
/// 1. DEPLOYMENT IAM: Permissions for a user/service account to execute Terraform and deploy infrastructure
/// 2. RUNTIME IAM: Permissions for the deployed Honua application to access cloud resources
/// Both include Terraform configuration for easy provisioning.
/// </summary>
public sealed class CloudPermissionGeneratorAgent
{
    private readonly ILlmProvider _llmProvider;
    private readonly ILogger<CloudPermissionGeneratorAgent> _logger;

    public CloudPermissionGeneratorAgent(
        ILlmProvider llmProvider,
        ILogger<CloudPermissionGeneratorAgent> logger)
    {
        _llmProvider = llmProvider ?? throw new ArgumentNullException(nameof(llmProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Analyzes a deployment plan and generates BOTH deployment IAM and runtime IAM with Terraform configuration.
    /// </summary>
    public async Task<PermissionGenerationResult> GeneratePermissionsAsync(
        DeploymentTopology topology,
        AgentExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing deployment topology for cloud provider: {CloudProvider}", topology.CloudProvider);

        var startTime = DateTime.UtcNow;

        try
        {
            // Step 1: Analyze required cloud services
            var requiredServices = await AnalyzeRequiredServicesAsync(topology, cancellationToken);

            // Step 2: Generate DEPLOYMENT IAM (Terraform execution permissions)
            var deploymentPermissions = await GenerateDeploymentIamAsync(topology, requiredServices, cancellationToken);

            // Step 3: Generate RUNTIME IAM (Honua application permissions)
            var runtimePermissions = await GenerateRuntimeIamAsync(topology, requiredServices, cancellationToken);

            // Step 4: Generate Terraform configuration for both
            var deploymentTerraform = await GenerateDeploymentIamTerraformAsync(topology, deploymentPermissions, cancellationToken);
            var runtimeTerraform = await GenerateRuntimeIamTerraformAsync(topology, runtimePermissions, cancellationToken);

            return new PermissionGenerationResult
            {
                Success = true,
                CloudProvider = topology.CloudProvider,
                RequiredServices = requiredServices,
                DeploymentPermissions = deploymentPermissions,
                RuntimePermissions = runtimePermissions,
                DeploymentIamTerraform = deploymentTerraform,
                RuntimeIamTerraform = runtimeTerraform,
                Duration = DateTime.UtcNow - startTime
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate cloud permissions for deployment");
            return new PermissionGenerationResult
            {
                Success = false,
                CloudProvider = topology.CloudProvider,
                ErrorMessage = ex.Message,
                Duration = DateTime.UtcNow - startTime
            };
        }
    }

    private async Task<List<CloudServiceRequirement>> AnalyzeRequiredServicesAsync(
        DeploymentTopology topology,
        CancellationToken cancellationToken)
    {
        var prompt = BuildServiceAnalysisPrompt(topology);

        var systemPrompt = @"You are an expert cloud infrastructure architect specializing in least-privilege security.
Analyze the deployment topology and identify ALL cloud services that will be used.
Consider: compute, storage, databases, networking, monitoring, secrets management, and container orchestration.
Return a JSON array of service requirements with specific actions needed.";

        var request = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = prompt
        };

        var response = await _llmProvider.CompleteAsync(request, cancellationToken);

        if (!response.Success)
        {
            throw new InvalidOperationException($"LLM request failed: {response.ErrorMessage ?? "Unknown error"}");
        }

        var payload = ExtractJsonPayload(response.Content);

        try
        {
            if (payload.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("Service analysis response was empty.");
            }

            var options = DefaultJsonOptions;

            // Support either an array or an envelope object
            if (payload.TrimStart().StartsWith("["))
            {
                var services = JsonSerializer.Deserialize<List<CloudServiceRequirement>>(payload, options);
                if (services is null)
                {
                    throw new InvalidOperationException("Failed to parse service analysis array from LLM response.");
                }

                return services;
            }

            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                JsonElement? servicesElement = null;

                if (root.TryGetProperty("services", out var lowerServices))
                {
                    servicesElement = lowerServices;
                }
                else if (root.TryGetProperty("Services", out var upperServices))
                {
                    servicesElement = upperServices;
                }
                else
                {
                    foreach (var property in root.EnumerateObject())
                    {
                        if (string.Equals(property.Name, "services", StringComparison.OrdinalIgnoreCase))
                        {
                            servicesElement = property.Value;
                            break;
                        }
                    }
                }

                if (servicesElement is not null)
                {
                    var servicesJson = servicesElement.Value.GetRawText();
                    var services = JsonSerializer.Deserialize<List<CloudServiceRequirement>>(servicesJson, options);
                    if (services is null)
                    {
                        throw new InvalidOperationException("Failed to deserialize 'services' array from LLM response.");
                    }

                    return services;
                }
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                var services = JsonSerializer.Deserialize<List<CloudServiceRequirement>>(root.GetRawText(), options);
                if (services is null)
                {
                    throw new InvalidOperationException("Failed to parse service analysis array from LLM response.");
                }

                return services;
            }

            throw new InvalidOperationException("Service analysis response did not contain a recognizable JSON array or 'services' property.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize service analysis response: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates DEPLOYMENT IAM: Permissions for Terraform to provision infrastructure.
    /// </summary>
    private async Task<CloudPermissionSet> GenerateDeploymentIamAsync(
        DeploymentTopology topology,
        List<CloudServiceRequirement> services,
        CancellationToken cancellationToken)
    {
        var prompt = BuildDeploymentIamPrompt(topology, services);

        var systemPrompt = topology.CloudProvider.ToLowerInvariant() switch
        {
            "aws" => @"You are an AWS IAM security expert. Generate least-privilege IAM policies for TERRAFORM DEPLOYMENT.
This user/service account needs permissions to CREATE, UPDATE, and DELETE cloud resources via Terraform.
Include permissions for:
- Creating VPCs, subnets, security groups, route tables
- Creating IAM roles and policies for the application
- Creating compute resources (ECS, EC2, Lambda)
- Creating databases (RDS)
- Creating storage (S3 buckets)
- Creating monitoring resources (CloudWatch)
- Managing Terraform state (if using S3 backend)
Use resource-level permissions and conditions where possible.
Return policies in AWS IAM JSON format.",

            "azure" => @"You are an Azure RBAC security expert. Generate least-privilege role assignments for TERRAFORM DEPLOYMENT.
This service principal needs permissions to CREATE, UPDATE, and DELETE cloud resources via Terraform.
Include permissions for:
- Creating resource groups, VNets, NSGs
- Creating role assignments for the application
- Creating compute resources (Container Apps, App Service)
- Creating databases (Azure Database for PostgreSQL)
- Creating storage accounts
- Creating monitoring resources (Log Analytics)
Use custom roles scoped to specific resource groups where possible.
Return role definitions in Azure RBAC JSON format.",

            "gcp" => @"You are a Google Cloud IAM security expert. Generate least-privilege IAM bindings for TERRAFORM DEPLOYMENT.
This service account needs permissions to CREATE, UPDATE, and DELETE cloud resources via Terraform.
Include permissions for:
- Creating VPCs, subnets, firewall rules
- Creating IAM roles and service accounts for the application
- Creating compute resources (Cloud Run, GCE, GKE)
- Creating databases (Cloud SQL)
- Creating storage buckets (GCS)
- Creating monitoring resources (Cloud Logging)
Use custom roles scoped appropriately.
Return bindings in GCP IAM JSON format.",

            _ => throw new NotSupportedException($"Cloud provider '{topology.CloudProvider}' is not supported")
        };

        var request = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = prompt
        };

        var response = await _llmProvider.CompleteAsync(request, cancellationToken);

        if (!response.Success)
        {
            throw new InvalidOperationException($"LLM request failed: {response.ErrorMessage ?? "Unknown error"}");
        }

        var payload = ExtractJsonPayload(response.Content);
        return ParsePermissionSet(payload);
    }

    /// <summary>
    /// Generates RUNTIME IAM: Permissions for Honua application to access cloud resources.
    /// </summary>
    private async Task<CloudPermissionSet> GenerateRuntimeIamAsync(
        DeploymentTopology topology,
        List<CloudServiceRequirement> services,
        CancellationToken cancellationToken)
    {
        var prompt = BuildRuntimeIamPrompt(topology, services);

        var systemPrompt = topology.CloudProvider.ToLowerInvariant() switch
        {
            "aws" => @"You are an AWS IAM security expert. Generate least-privilege IAM policies for the HONUA APPLICATION at runtime.
This is the role/instance profile attached to the Honua container/instance.
Include ONLY permissions needed for the application to operate:
- Read/Write to S3 buckets (raster data, attachments)
- Connect to RDS database (no admin permissions)
- Write logs to CloudWatch
- Read secrets from Secrets Manager (database credentials)
- Publish metrics to CloudWatch
DO NOT include permissions to create/destroy infrastructure.
Use resource-level permissions with specific bucket/database ARNs.
Return policies in AWS IAM JSON format.",

            "azure" => @"You are an Azure RBAC security expert. Generate least-privilege role assignments for the HONUA APPLICATION at runtime.
This is the managed identity assigned to the Honua container/app.
Include ONLY permissions needed for the application to operate:
- Read/Write to Storage Account (raster data, attachments)
- Connect to PostgreSQL database (no admin permissions)
- Write logs to Log Analytics
- Read secrets from Key Vault (database credentials)
- Write metrics to Azure Monitor
DO NOT include permissions to create/destroy infrastructure.
Scope permissions to specific storage accounts and databases.
Return role definitions in Azure RBAC JSON format.",

            "gcp" => @"You are a Google Cloud IAM security expert. Generate least-privilege IAM bindings for the HONUA APPLICATION at runtime.
This is the service account assigned to the Honua Cloud Run service or GCE instance.
Include ONLY permissions needed for the application to operate:
- Read/Write to GCS buckets (raster data, attachments)
- Connect to Cloud SQL database (no admin permissions)
- Write logs to Cloud Logging
- Read secrets from Secret Manager (database credentials)
- Write metrics to Cloud Monitoring
DO NOT include permissions to create/destroy infrastructure.
Scope permissions to specific buckets and databases.
Return bindings in GCP IAM JSON format.",

            _ => throw new NotSupportedException($"Cloud provider '{topology.CloudProvider}' is not supported")
        };

        var request = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = prompt
        };

        var response = await _llmProvider.CompleteAsync(request, cancellationToken);

        if (!response.Success)
        {
            throw new InvalidOperationException($"LLM request failed: {response.ErrorMessage ?? "Unknown error"}");
        }

        var payload = ExtractJsonPayload(response.Content);
        return ParsePermissionSet(payload);
    }

    private CloudPermissionSet ParsePermissionSet(string payload)
    {
        try
        {
            if (payload.IsNullOrWhiteSpace())
            {
                throw new InvalidOperationException("Permission generation response was empty.");
            }

            var options = DefaultJsonOptions;
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;

            if (root.ValueKind == JsonValueKind.Object)
            {
                JsonElement? permissionElement = null;

                if (root.TryGetProperty("permissionSet", out var lowerPermission))
                {
                    permissionElement = lowerPermission;
                }
                else if (root.TryGetProperty("PermissionSet", out var upperPermission))
                {
                    permissionElement = upperPermission;
                }
                else
                {
                    foreach (var property in root.EnumerateObject())
                    {
                        if (string.Equals(property.Name, "permissionSet", StringComparison.OrdinalIgnoreCase))
                        {
                            permissionElement = property.Value;
                            break;
                        }
                    }
                }

                if (permissionElement is not null)
                {
                    var permissionSet = JsonSerializer.Deserialize<CloudPermissionSet>(permissionElement.Value.GetRawText(), options);
                    if (permissionSet is null)
                    {
                        throw new InvalidOperationException("Failed to deserialize permission generation response: permissionSet property could not be parsed.");
                    }

                    return permissionSet;
                }

                var directSet = JsonSerializer.Deserialize<CloudPermissionSet>(payload, options);
                if (directSet is null)
                {
                    throw new InvalidOperationException("Failed to parse permission generation response into a CloudPermissionSet.");
                }

                return directSet;
            }

            if (root.ValueKind == JsonValueKind.Array)
            {
                var first = root.EnumerateArray().FirstOrDefault();
                if (first.ValueKind == JsonValueKind.Object)
                {
                    // Attempt to construct a CloudPermissionSet from an array of policy documents
                    var policies = JsonSerializer.Deserialize<List<PolicyDocument>>(root.GetRawText(), options);
                    if (policies is not null)
                    {
                        return new CloudPermissionSet
                        {
                            PrincipalName = "honua-generated",
                            Policies = policies,
                            Tags = null
                        };
                    }
                }

                throw new InvalidOperationException("Permission generation response returned an array without metadata. Please include principalName and policies in an object.");
            }

            throw new InvalidOperationException("Permission generation response was not an object or did not contain a recognizable 'permissionSet'.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException($"Failed to deserialize permission generation response: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Generates Terraform configuration for DEPLOYMENT IAM (user to execute Terraform).
    /// </summary>
    private async Task<string> GenerateDeploymentIamTerraformAsync(
        DeploymentTopology topology,
        CloudPermissionSet permissions,
        CancellationToken cancellationToken)
    {
        var prompt = BuildDeploymentIamTerraformPrompt(topology, permissions);

        var systemPrompt = $@"You are a Terraform expert specializing in {topology.CloudProvider} IAM configuration.
Generate Terraform configuration for a DEPLOYMENT service principal/user that can execute Terraform.
This account will be used to provision infrastructure via 'terraform apply'.

Include:
1. Provider configuration
2. User/Service principal creation (e.g., IAM user, Azure service principal, GCP service account)
3. Access key/credential generation
4. IAM policy/role attachment with deployment permissions
5. Output values for credentials (marked sensitive)
6. Inline comments explaining each resource
7. README header explaining this is for Terraform execution

Use Terraform best practices: variables, locals, outputs, proper naming.
File should be saved as 'iam-deployment.tf'";

        var request = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = prompt
        };

        var response = await _llmProvider.CompleteAsync(request, cancellationToken);

        if (!response.Success)
        {
            throw new InvalidOperationException($"Deployment IAM Terraform generation failed: {response.ErrorMessage ?? "Unknown error"}");
        }

        return ExtractTerraformCode(response.Content);
    }

    /// <summary>
    /// Generates Terraform configuration for RUNTIME IAM (Honua application permissions).
    /// </summary>
    private async Task<string> GenerateRuntimeIamTerraformAsync(
        DeploymentTopology topology,
        CloudPermissionSet permissions,
        CancellationToken cancellationToken)
    {
        var prompt = BuildRuntimeIamTerraformPrompt(topology, permissions);

        var systemPrompt = $@"You are a Terraform expert specializing in {topology.CloudProvider} IAM configuration.
Generate Terraform configuration for RUNTIME application permissions.
This role/managed identity will be attached to the Honua containers/instances.

Include:
1. IAM role/managed identity creation
2. Trust policy/assume role policy (for ECS task role, etc.)
3. IAM policy attachment with runtime permissions
4. Output values for role ARN/identity
5. Inline comments explaining each resource
6. README header explaining this is for application runtime

Use Terraform best practices: variables, locals, outputs, proper naming.
File should be saved as 'iam-runtime.tf' and integrated into main Terraform deployment.";

        var request = new LlmRequest
        {
            SystemPrompt = systemPrompt,
            UserPrompt = prompt
        };

        var response = await _llmProvider.CompleteAsync(request, cancellationToken);

        if (!response.Success)
        {
            throw new InvalidOperationException($"Runtime IAM Terraform generation failed: {response.ErrorMessage ?? "Unknown error"}");
        }

        return ExtractTerraformCode(response.Content);
    }

    private string BuildServiceAnalysisPrompt(DeploymentTopology topology)
    {
        var sb = new StringBuilder();
        sb.AppendLine("Analyze this HonuaIO deployment topology and identify ALL required cloud services:");
        sb.AppendLine();
        sb.AppendLine($"Cloud Provider: {topology.CloudProvider}");
        sb.AppendLine($"Region: {topology.Region}");
        sb.AppendLine($"Environment: {topology.Environment}");
        sb.AppendLine();
        sb.AppendLine("Components:");

        if (topology.Database != null)
        {
            sb.AppendLine($"- Database: {topology.Database.Engine} {topology.Database.Version}");
            sb.AppendLine($"  Size: {topology.Database.InstanceSize}");
            sb.AppendLine($"  Storage: {topology.Database.StorageGB}GB");
            sb.AppendLine($"  High Availability: {topology.Database.HighAvailability}");
        }

        if (topology.Compute != null)
        {
            sb.AppendLine($"- Compute: {topology.Compute.Type}");
            sb.AppendLine($"  Instance Size: {topology.Compute.InstanceSize}");
            sb.AppendLine($"  Instance Count: {topology.Compute.InstanceCount}");
            sb.AppendLine($"  Auto Scaling: {topology.Compute.AutoScaling}");
        }

        if (topology.Storage != null)
        {
            sb.AppendLine($"- Object Storage: {topology.Storage.Type}");
            sb.AppendLine($"  Attachments: {topology.Storage.AttachmentStorageGB}GB");
            sb.AppendLine($"  Raster Cache: {topology.Storage.RasterCacheGB}GB");
            sb.AppendLine($"  Replication: {topology.Storage.Replication}");
        }

        if (topology.Networking != null)
        {
            sb.AppendLine($"- Networking:");
            sb.AppendLine($"  Load Balancer: {topology.Networking.LoadBalancer}");
            sb.AppendLine($"  Public Access: {topology.Networking.PublicAccess}");
            sb.AppendLine($"  VPN: {topology.Networking.VpnRequired}");
        }

        if (topology.Monitoring != null)
        {
            sb.AppendLine($"- Monitoring: {topology.Monitoring.Provider}");
            sb.AppendLine($"  Metrics: {topology.Monitoring.EnableMetrics}");
            sb.AppendLine($"  Logs: {topology.Monitoring.EnableLogs}");
            sb.AppendLine($"  Tracing: {topology.Monitoring.EnableTracing}");
        }

        if (topology.Features?.Any() == true)
        {
            sb.AppendLine("- Features:");
            foreach (var feature in topology.Features)
            {
                sb.AppendLine($"  - {feature}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Return a JSON array of cloud services required, with specific actions needed for each.");
        sb.AppendLine("Example format:");
        sb.AppendLine(@"{
  ""services"": [
    {
      ""service"": ""EC2"",
      ""actions"": [""RunInstances"", ""TerminateInstances"", ""DescribeInstances""],
      ""resources"": [""arn:aws:ec2:*:*:instance/*""],
      ""rationale"": ""Deploy and manage compute instances for Honua server""
    }
  ]
}");

        return sb.ToString();
    }

    private string BuildDeploymentIamPrompt(
        DeploymentTopology topology,
        List<CloudServiceRequirement> services)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generate least-privilege {topology.CloudProvider} DEPLOYMENT IAM permissions.");
        sb.AppendLine("This is for a user/service account to EXECUTE TERRAFORM and provision infrastructure.");
        sb.AppendLine();
        sb.AppendLine("Required Cloud Services:");
        foreach (var service in services)
        {
            sb.AppendLine($"- {service.Service}: {string.Join(", ", service.Actions)}");
            if (!service.Rationale.IsNullOrEmpty())
            {
                sb.AppendLine($"  Rationale: {service.Rationale}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Generate a complete permission set for Terraform execution including:");
        sb.AppendLine("1. Permissions to CREATE, UPDATE, DELETE all infrastructure resources");
        sb.AppendLine("2. Permissions to create IAM roles/policies for the application");
        sb.AppendLine("3. Permissions to manage Terraform state (if using S3/Blob/GCS backend)");
        sb.AppendLine("4. Resource-level restrictions scoped to project/region");
        sb.AppendLine("5. Condition keys for additional security (MFA, IP restrictions if applicable)");
        sb.AppendLine();
        sb.AppendLine("Return as JSON with policy documents.");

        return sb.ToString();
    }

    private string BuildRuntimeIamPrompt(
        DeploymentTopology topology,
        List<CloudServiceRequirement> services)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generate least-privilege {topology.CloudProvider} RUNTIME IAM permissions.");
        sb.AppendLine("This is for the HONUA APPLICATION at runtime (attached to containers/instances).");
        sb.AppendLine();
        sb.AppendLine("Application Requirements:");
        sb.AppendLine($"- Region: {topology.Region}");
        sb.AppendLine($"- Environment: {topology.Environment}");
        if (topology.Database != null)
        {
            sb.AppendLine($"- Database: {topology.Database.Engine} {topology.Database.Version}");
        }
        if (topology.Storage != null)
        {
            sb.AppendLine($"- Storage: {topology.Storage.Type} (raster data, attachments)");
        }
        if (topology.Monitoring != null)
        {
            sb.AppendLine($"- Monitoring: {topology.Monitoring.Provider}");
        }

        sb.AppendLine();
        sb.AppendLine("Generate RUNTIME-ONLY permissions including:");
        sb.AppendLine("1. Read/Write to object storage buckets (raster data, attachments)");
        sb.AppendLine("2. Connect to database (NO admin permissions like CreateDatabase, DropDatabase)");
        sb.AppendLine("3. Write logs and metrics to monitoring services");
        sb.AppendLine("4. Read secrets for database credentials (NO write/delete secrets)");
        sb.AppendLine("5. NO permissions to create/delete infrastructure resources");
        sb.AppendLine("6. Resource-level restrictions with specific bucket/database ARNs");
        sb.AppendLine();
        sb.AppendLine("Return as JSON with policy documents.");

        return sb.ToString();
    }

    private string BuildDeploymentIamTerraformPrompt(
        DeploymentTopology topology,
        CloudPermissionSet permissions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generate Terraform configuration to create a {topology.CloudProvider} DEPLOYMENT user/service principal.");
        sb.AppendLine("This account will execute 'terraform apply' to provision infrastructure.");
        sb.AppendLine();
        sb.AppendLine($"Principal Name: honua-terraform-deployer-{topology.Environment}");
        sb.AppendLine($"Region: {topology.Region}");
        sb.AppendLine();
        sb.AppendLine("Permissions to grant:");
        sb.AppendLine(JsonSerializer.Serialize(permissions, CliJsonOptions.Indented));
        sb.AppendLine();
        sb.AppendLine("Requirements:");
        sb.AppendLine("1. Create IAM user/service principal/service account");
        sb.AppendLine("2. Generate access keys/credentials for Terraform");
        sb.AppendLine("3. Attach deployment IAM policies");
        sb.AppendLine("4. Output credentials (marked sensitive)");
        sb.AppendLine("5. Add descriptive comments and README header");
        sb.AppendLine("6. Use tags: Environment={topology.Environment}, Purpose=TerraformDeployment");
        sb.AppendLine();
        sb.AppendLine("Return ONLY Terraform code.");

        return sb.ToString();
    }

    private string BuildRuntimeIamTerraformPrompt(
        DeploymentTopology topology,
        CloudPermissionSet permissions)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Generate Terraform configuration to create {topology.CloudProvider} RUNTIME IAM role/managed identity.");
        sb.AppendLine("This will be attached to Honua application containers/instances at runtime.");
        sb.AppendLine();
        sb.AppendLine($"Role Name: honua-app-{topology.Environment}");
        sb.AppendLine($"Region: {topology.Region}");
        sb.AppendLine();
        sb.AppendLine("Permissions to grant:");
        sb.AppendLine(JsonSerializer.Serialize(permissions, CliJsonOptions.Indented));
        sb.AppendLine();
        sb.AppendLine("Requirements:");
        sb.AppendLine("1. Create IAM role/managed identity (NOT a user)");
        sb.AppendLine("2. Configure trust policy for ECS tasks/Container Apps/Cloud Run");
        sb.AppendLine("3. Attach runtime IAM policies");
        sb.AppendLine("4. Output role ARN/identity ID");
        sb.AppendLine("5. This role should be referenced by compute resources (ECS task definition, etc.)");
        sb.AppendLine("6. Add descriptive comments and README header");
        sb.AppendLine("7. Use tags: Environment={topology.Environment}, Purpose=HonuaRuntime");
        sb.AppendLine();
        sb.AppendLine("Return ONLY Terraform code.");

        return sb.ToString();
    }

    private string ExtractTerraformCode(string llmResponse)
    {
        // Extract code from markdown code blocks if present
        if (llmResponse.Contains("```", StringComparison.Ordinal))
        {
            var startIndex = llmResponse.IndexOf("```", StringComparison.Ordinal);
            if (startIndex >= 0)
            {
                startIndex = llmResponse.IndexOf('\n', startIndex) + 1;
                var endIndex = llmResponse.IndexOf("```", startIndex, StringComparison.Ordinal);
                if (endIndex > startIndex)
                {
                    return llmResponse.Substring(startIndex, endIndex - startIndex).Trim();
                }
            }
        }

        return llmResponse.Trim();
    }

    private static string ExtractJsonPayload(string text)
    {
        if (text.IsNullOrWhiteSpace())
        {
            return string.Empty;
        }

        var trimmed = text.Trim();

        // If fenced markdown appears anywhere, grab the first fenced block
        if (trimmed.Contains("```", StringComparison.Ordinal))
        {
            var firstFence = trimmed.IndexOf("```", StringComparison.Ordinal);
            var languageBreak = trimmed.IndexOf('\n', firstFence);
            if (languageBreak > firstFence)
            {
                var fenceEnd = trimmed.IndexOf("```", languageBreak + 1, StringComparison.Ordinal);
                if (fenceEnd > languageBreak)
                {
                    return trimmed.Substring(languageBreak + 1, fenceEnd - (languageBreak + 1)).Trim();
                }
            }
        }

        // Otherwise, remove any leading explanation before the first JSON token
        var firstObject = trimmed.IndexOf('{');
        var firstArray = trimmed.IndexOf('[');
        var firstTokenIndex = -1;

        if (firstObject >= 0 && firstArray >= 0)
        {
            firstTokenIndex = Math.Min(firstObject, firstArray);
        }
        else if (firstObject >= 0)
        {
            firstTokenIndex = firstObject;
        }
        else if (firstArray >= 0)
        {
            firstTokenIndex = firstArray;
        }

        if (firstTokenIndex > 0)
        {
            return trimmed.Substring(firstTokenIndex).Trim();
        }

        return trimmed;
    }

    private static readonly JsonSerializerOptions DefaultJsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };
}

// ============================================================================
// Models
// ============================================================================

public sealed record DeploymentTopology
{
    public required string CloudProvider { get; init; } // aws, azure, gcp
    public required string Region { get; init; }
    public required string Environment { get; init; } // dev, staging, prod
    public DatabaseConfig? Database { get; init; }
    public ComputeConfig? Compute { get; init; }
    public StorageConfig? Storage { get; init; }
    public NetworkingConfig? Networking { get; init; }
    public MonitoringConfig? Monitoring { get; init; }
    public List<string>? Features { get; init; }
}

public sealed class DatabaseConfig
{
    public required string Engine { get; init; } // postgres, mysql
    public required string Version { get; init; }
    public required string InstanceSize { get; init; }
    public int StorageGB { get; init; }
    public bool HighAvailability { get; init; }
}

public sealed class ComputeConfig
{
    public required string Type { get; init; } // vm, container, serverless
    public required string InstanceSize { get; init; }
    public int InstanceCount { get; init; }
    public bool AutoScaling { get; init; }
}

public sealed class StorageConfig
{
    public required string Type { get; init; } // s3, blob, gcs
    public int AttachmentStorageGB { get; init; }
    public int RasterCacheGB { get; init; }
    public required string Replication { get; init; }
}

public sealed class NetworkingConfig
{
    public bool LoadBalancer { get; init; }
    public bool PublicAccess { get; init; }
    public bool VpnRequired { get; init; }
}

public sealed class MonitoringConfig
{
    public required string Provider { get; init; }
    public bool EnableMetrics { get; init; }
    public bool EnableLogs { get; init; }
    public bool EnableTracing { get; init; }
}

public sealed class CloudServiceRequirement
{
    public required string Service { get; init; }
    public required List<string> Actions { get; init; }
    public List<string>? Resources { get; init; }
    public string? Rationale { get; init; }
}

public sealed class CloudPermissionSet
{
    public required string PrincipalName { get; init; }
    public required List<PolicyDocument> Policies { get; init; }
    public string? TrustPolicy { get; init; }
    public Dictionary<string, string>? Tags { get; init; }
}

public sealed class PolicyDocument
{
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string PolicyJson { get; init; }
}

public sealed class PermissionGenerationResult
{
    public bool Success { get; init; }
    public string? CloudProvider { get; init; }
    public List<CloudServiceRequirement>? RequiredServices { get; init; }

    // DEPLOYMENT IAM: Permissions for Terraform execution
    public CloudPermissionSet? DeploymentPermissions { get; init; }
    public string? DeploymentIamTerraform { get; init; }

    // RUNTIME IAM: Permissions for Honua application
    public CloudPermissionSet? RuntimePermissions { get; init; }
    public string? RuntimeIamTerraform { get; init; }

    // Legacy properties (deprecated - use specific properties above)
    [Obsolete("Use DeploymentPermissions instead")]
    public CloudPermissionSet? Permissions => DeploymentPermissions;

    [Obsolete("Use DeploymentIamTerraform or RuntimeIamTerraform")]
    public string? TerraformConfig => DeploymentIamTerraform;

    public string? ErrorMessage { get; init; }
    public TimeSpan Duration { get; init; }
}

// Internal response models for LLM parsing
internal sealed class ServiceAnalysisResponse
{
    public List<CloudServiceRequirement> Services { get; set; } = new();
}

internal sealed class PermissionSetResponse
{
    public CloudPermissionSet? PermissionSet { get; set; }
}
