// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Honua.Cli.AI.Services.Guardrails;
using Honua.Cli.AI.Services.Processes.Steps.Deployment;
using Honua.Cli.AI.Services.Processes.State;
using Honua.Cli.AI.Services.Processes.Steps.Upgrade;
using Honua.Cli.AI.Services.Processes.Steps.Metadata;
using Honua.Cli.AI.Services.Processes.Steps.GitOps;
using Honua.Cli.AI.Services.Processes.Steps.Benchmark;
using Honua.Cli.AI.Services.Processes.Steps.CertificateRenewal;
using Honua.Cli.AI.Services.Processes.Steps.NetworkDiagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Honua.Cli.AI.Serialization;



namespace Honua.Cli.AI.Services.Processes;

/// <summary>
/// Service for extracting structured parameters from natural language requests using LLM.
/// Converts user requests into typed request objects for Process Framework workflows.
/// </summary>
public class ParameterExtractionService
{
    private readonly IChatCompletionService _chatCompletion;
    private readonly ILogger<ParameterExtractionService> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public ParameterExtractionService(
        IChatCompletionService chatCompletion,
        ILogger<ParameterExtractionService> logger)
    {
        _chatCompletion = chatCompletion ?? throw new ArgumentNullException(nameof(chatCompletion));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _jsonOptions = CliJsonOptions.DevTooling;
    }

    /// <summary>
    /// Extracts deployment parameters from a natural language request.
    /// </summary>
    public async Task<DeploymentRequest> ExtractDeploymentParametersAsync(
        string request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting deployment parameters from request");

        var systemPrompt = @"You are a parameter extraction assistant for cloud deployment workflows.
Extract the following parameters from the user's request and return them as a JSON object:

{
  ""cloudProvider"": ""AWS"" | ""Azure"" | ""GCP"",
  ""region"": ""<cloud region identifier>"",
  ""deploymentName"": ""<name for the deployment>"",
  ""tier"": ""Development"" | ""Staging"" | ""Production"",
  ""features"": [""<list of features to deploy>""],
  ""workloadProfile"": ""api-small"" | ""api-standard"" | ""raster-batch"" | ""ai-orchestration"" | ""analytics-heavy"" | null,
  ""concurrentUsers"": <integer or null>,
  ""dataVolumeGb"": <integer or null>,
  ""guardrailJustification"": ""<brief reason for overrides>"" | null,
  ""sizing"": {
    ""requestedVCpu"": <decimal or null>,
    ""requestedMemoryGb"": <decimal or null>,
    ""requestedEphemeralStorageGb"": <decimal or null>,
    ""requestedMinInstances"": <integer or null>,
    ""requestedProvisionedConcurrency"": <integer or null>
  },
  ""reuseExistingNetwork"": true | false,
  ""reuseExistingDatabase"": true | false,
  ""reuseExistingDns"": true | false,
  ""existingNetworkId"": ""<identifier or null>"",
  ""existingDatabaseId"": ""<identifier or null>"",
  ""existingDnsZoneId"": ""<identifier or null>"",
  ""networkNotes"": ""<notes or null>"",
  ""databaseNotes"": ""<notes or null>"",
  ""dnsNotes"": ""<notes or null>""
}

Rules:
- CloudProvider: Extract cloud provider, default to ""AWS"" if not specified
- Region: Extract region, default to ""us-east-1"" for AWS, ""eastus"" for Azure, ""us-central1"" for GCP
- DeploymentName: Extract or generate a sensible name, default to ""honua-deployment""
- Tier: Infer from keywords like ""dev"", ""prod"", ""staging"". Default to ""Development""
- Features: Extract any mentioned features like GeoServer, PostGIS, VectorTiles, COG, Zarr, etc.
  Common features: [""GeoServer"", ""PostGIS"", ""VectorTiles"", ""COG"", ""Zarr"", ""STAC"", ""WMS"", ""WFS""]
- WorkloadProfile: Choose the closest profile. If not specified, use ""api-standard"" for AWS and ""ai-orchestration"" for Azure.
- ConcurrentUsers/DataVolumeGb: Infer scale signals (""1000 users"", ""2TB data""). Use integers; null if absent.
- GuardrailJustification: Short reason if the user insists on custom sizing; null otherwise.
- Sizing: Only populate when the user provides explicit CPU/memory/instance overrides. Leave fields null if not mentioned.
- Reuse flags: Set to true when the user requests using existing VPC/virtual network, managed database, or DNS zone; default to false when unspecified or when the user asks for new infrastructure.
- Existing IDs & notes: Populate identifiers and notes when reuse flags are true and the user names specific resources or constraints. Leave null if unspecified.

Return ONLY the JSON object, no additional text.";

        try
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(request);

            var jsonResponse = await InvokeChatCompletionAsync(chatHistory, cancellationToken);
            _logger.LogDebug("LLM extraction response: {Response}", jsonResponse);

            // Parse JSON response
            var extractedData = ParseDeploymentExtraction(jsonResponse);

            var provider = string.IsNullOrWhiteSpace(extractedData.CloudProvider)
                ? "AWS"
                : extractedData.CloudProvider.Trim();

            var region = extractedData.Region;
            if (string.IsNullOrWhiteSpace(region))
            {
                region = provider.Equals("Azure", StringComparison.OrdinalIgnoreCase)
                    ? "eastus"
                    : provider.Equals("GCP", StringComparison.OrdinalIgnoreCase)
                        ? "us-central1"
                        : "us-east-1";
            }

            var features = extractedData.Features ?? new List<string>
            {
                "GeoServer",
                "PostGIS",
                "VectorTiles"
            };

            var workloadProfile = string.IsNullOrWhiteSpace(extractedData.WorkloadProfile)
                ? provider.Equals("Azure", StringComparison.OrdinalIgnoreCase)
                    ? "ai-orchestration"
                    : "api-standard"
                : extractedData.WorkloadProfile.Trim();

            var sizing = extractedData.Sizing == null
                ? null
                : new DeploymentSizingRequest(
                    RequestedVCpu: extractedData.Sizing.RequestedVCpu,
                    RequestedMemoryGb: extractedData.Sizing.RequestedMemoryGb,
                    RequestedEphemeralStorageGb: extractedData.Sizing.RequestedEphemeralStorageGb,
                    RequestedMinInstances: extractedData.Sizing.RequestedMinInstances,
                    RequestedProvisionedConcurrency: extractedData.Sizing.RequestedProvisionedConcurrency);

            var deploymentRequest = new DeploymentRequest(
                CloudProvider: provider,
                Region: region,
                DeploymentName: extractedData.DeploymentName ?? "honua-deployment",
                Tier: extractedData.Tier ?? "Development",
                Features: features,
                WorkloadProfile: workloadProfile,
                ConcurrentUsers: extractedData.ConcurrentUsers,
                DataVolumeGb: extractedData.DataVolumeGb,
                Sizing: sizing,
                ExistingInfrastructure: new ExistingInfrastructurePreference(
                    ReuseNetwork: extractedData.ReuseExistingNetwork ?? false,
                    ReuseDatabase: extractedData.ReuseExistingDatabase ?? false,
                    ReuseDns: extractedData.ReuseExistingDns ?? false,
                    ExistingNetworkId: extractedData.ExistingNetworkId,
                    ExistingDatabaseId: extractedData.ExistingDatabaseId,
                    ExistingDnsZoneId: extractedData.ExistingDnsZoneId,
                    NetworkNotes: extractedData.NetworkNotes,
                    DatabaseNotes: extractedData.DatabaseNotes,
                    DnsNotes: extractedData.DnsNotes
                ),
                GuardrailJustification: extractedData.GuardrailJustification
            );

            _logger.LogInformation(
                "Extracted deployment parameters: {Provider}, {Region}, {Name}, {Tier}, {FeatureCount} features, profile {Profile}",
                deploymentRequest.CloudProvider,
                deploymentRequest.Region,
                deploymentRequest.DeploymentName,
                deploymentRequest.Tier,
                deploymentRequest.Features.Count,
                deploymentRequest.WorkloadProfile);

            return deploymentRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting deployment parameters, returning defaults");
            return GetDefaultDeploymentRequest();
        }
    }

    private async Task<string> InvokeChatCompletionAsync(ChatHistory chatHistory, CancellationToken cancellationToken)
    {
        var responses = await _chatCompletion.GetChatMessageContentsAsync(
            chatHistory,
            cancellationToken: cancellationToken);

        var message = responses?.FirstOrDefault();
        if (message is null)
        {
            return "{}";
        }

        var content = message.Content;

        if (string.IsNullOrWhiteSpace(content))
        {
            content = message.ToString();
        }

        if (string.IsNullOrWhiteSpace(content) && message.Items is not null)
        {
            var textItem = message.Items.OfType<TextContent>().FirstOrDefault();
            if (textItem is not null)
            {
                content = textItem.Text;
            }
        }

        return string.IsNullOrWhiteSpace(content) ? "{}" : content!;
    }

    private static DeploymentParameterExtraction ParseDeploymentExtraction(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var extraction = new DeploymentParameterExtraction();

            if (root.TryGetProperty("cloudProvider", out var provider) && provider.ValueKind == JsonValueKind.String)
            {
                extraction.CloudProvider = provider.GetString();
            }

            if (root.TryGetProperty("region", out var region) && region.ValueKind == JsonValueKind.String)
            {
                extraction.Region = region.GetString();
            }

            if (root.TryGetProperty("deploymentName", out var name) && name.ValueKind == JsonValueKind.String)
            {
                extraction.DeploymentName = name.GetString();
            }

            if (root.TryGetProperty("tier", out var tier) && tier.ValueKind == JsonValueKind.String)
            {
                extraction.Tier = tier.GetString();
            }

            if (root.TryGetProperty("features", out var features) && features.ValueKind == JsonValueKind.Array)
            {
                extraction.Features = features.EnumerateArray()
                    .Where(e => e.ValueKind == JsonValueKind.String)
                    .Select(e => e.GetString()!)
                    .ToList();
            }

            if (root.TryGetProperty("workloadProfile", out var profile) && profile.ValueKind == JsonValueKind.String)
            {
                extraction.WorkloadProfile = profile.GetString();
            }

            if (root.TryGetProperty("concurrentUsers", out var users) && users.ValueKind == JsonValueKind.Number && users.TryGetInt32(out var cu))
            {
                extraction.ConcurrentUsers = cu;
            }

            if (root.TryGetProperty("dataVolumeGb", out var volume) && volume.ValueKind == JsonValueKind.Number && volume.TryGetInt32(out var dv))
            {
                extraction.DataVolumeGb = dv;
            }

            if (root.TryGetProperty("guardrailJustification", out var justification) && justification.ValueKind == JsonValueKind.String)
            {
                extraction.GuardrailJustification = justification.GetString();
            }

            if (root.TryGetProperty("reuseExistingNetwork", out var reuseNetwork))
            {
                extraction.ReuseExistingNetwork = ReadBoolean(reuseNetwork);
            }

            if (root.TryGetProperty("reuseExistingDatabase", out var reuseDatabase))
            {
                extraction.ReuseExistingDatabase = ReadBoolean(reuseDatabase);
            }

            if (root.TryGetProperty("reuseExistingDns", out var reuseDns))
            {
                extraction.ReuseExistingDns = ReadBoolean(reuseDns);
            }

            if (root.TryGetProperty("existingNetworkId", out var existingNetworkId) && existingNetworkId.ValueKind == JsonValueKind.String)
            {
                extraction.ExistingNetworkId = existingNetworkId.GetString();
            }

            if (root.TryGetProperty("existingDatabaseId", out var existingDatabaseId) && existingDatabaseId.ValueKind == JsonValueKind.String)
            {
                extraction.ExistingDatabaseId = existingDatabaseId.GetString();
            }

            if (root.TryGetProperty("existingDnsZoneId", out var existingDnsZoneId) && existingDnsZoneId.ValueKind == JsonValueKind.String)
            {
                extraction.ExistingDnsZoneId = existingDnsZoneId.GetString();
            }

            if (root.TryGetProperty("networkNotes", out var networkNotes) && networkNotes.ValueKind == JsonValueKind.String)
            {
                extraction.NetworkNotes = networkNotes.GetString();
            }

            if (root.TryGetProperty("databaseNotes", out var databaseNotes) && databaseNotes.ValueKind == JsonValueKind.String)
            {
                extraction.DatabaseNotes = databaseNotes.GetString();
            }

            if (root.TryGetProperty("dnsNotes", out var dnsNotes) && dnsNotes.ValueKind == JsonValueKind.String)
            {
                extraction.DnsNotes = dnsNotes.GetString();
            }

            if (root.TryGetProperty("sizing", out var sizingElement) && sizingElement.ValueKind == JsonValueKind.Object)
            {
                extraction.Sizing = new DeploymentSizingExtraction
                {
                    RequestedVCpu = sizingElement.TryGetProperty("requestedVCpu", out var vCpu) && vCpu.ValueKind == JsonValueKind.Number ? vCpu.GetDecimal() : null,
                    RequestedMemoryGb = sizingElement.TryGetProperty("requestedMemoryGb", out var memory) && memory.ValueKind == JsonValueKind.Number ? memory.GetDecimal() : null,
                    RequestedEphemeralStorageGb = sizingElement.TryGetProperty("requestedEphemeralStorageGb", out var storage) && storage.ValueKind == JsonValueKind.Number ? storage.GetDecimal() : null,
                    RequestedMinInstances = sizingElement.TryGetProperty("requestedMinInstances", out var minInstances) && minInstances.ValueKind == JsonValueKind.Number ? minInstances.GetInt32() : null,
                    RequestedProvisionedConcurrency = sizingElement.TryGetProperty("requestedProvisionedConcurrency", out var provConcurrency) && provConcurrency.ValueKind == JsonValueKind.Number ? provConcurrency.GetInt32() : null
                };
            }

            return extraction;
        }
        catch
        {
            return new DeploymentParameterExtraction();
        }
    }

    private static bool? ReadBoolean(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String when bool.TryParse(element.GetString(), out var result) => result,
            _ => null
        };
    }

    private static UpgradeParameterExtraction ParseUpgradeExtraction(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var extraction = new UpgradeParameterExtraction();

            if (root.TryGetProperty("deploymentName", out var name) && name.ValueKind == JsonValueKind.String)
            {
                extraction.DeploymentName = name.GetString();
            }

            if (root.TryGetProperty("targetVersion", out var version) && version.ValueKind == JsonValueKind.String)
            {
                extraction.TargetVersion = version.GetString();
            }

            return extraction;
        }
        catch
        {
            return new UpgradeParameterExtraction();
        }
    }

    /// <summary>
    /// Extracts upgrade parameters from a natural language request.
    /// </summary>
    public async Task<UpgradeRequest> ExtractUpgradeParametersAsync(
        string request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting upgrade parameters from request");

        var systemPrompt = @"You are a parameter extraction assistant for system upgrade workflows.
Extract the following parameters from the user's request and return them as a JSON object:

{
  ""deploymentName"": ""<name of the deployment to upgrade>"",
  ""targetVersion"": ""<version number in semver format>""
}

Rules:
- DeploymentName: Extract the deployment name, default to ""honua-deployment""
- TargetVersion: Extract version number (e.g., ""2.0.0"", ""1.5.3""). Default to ""latest"" if not specified

Return ONLY the JSON object, no additional text.";

        try
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(request);

            var jsonResponse = await InvokeChatCompletionAsync(chatHistory, cancellationToken);
            _logger.LogDebug("LLM extraction response: {Response}", jsonResponse);

            var extractedData = ParseUpgradeExtraction(jsonResponse);

            var upgradeRequest = new UpgradeRequest(
                DeploymentName: extractedData.DeploymentName ?? "honua-deployment",
                TargetVersion: extractedData.TargetVersion ?? "latest"
            );

            _logger.LogInformation(
                "Extracted upgrade parameters: {Name} -> {Version}",
                upgradeRequest.DeploymentName,
                upgradeRequest.TargetVersion);

            return upgradeRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting upgrade parameters, returning defaults");
            return GetDefaultUpgradeRequest();
        }
    }

    /// <summary>
    /// Extracts metadata parameters from a natural language request.
    /// </summary>
    public async Task<MetadataRequest> ExtractMetadataParametersAsync(
        string request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting metadata parameters from request");

        var systemPrompt = @"You are a parameter extraction assistant for geospatial metadata extraction workflows.
Extract the following parameters from the user's request and return them as a JSON object:

{
  ""datasetPath"": ""<file path to the raster dataset>"",
  ""datasetName"": ""<name for the dataset>""
}

Rules:
- DatasetPath: Extract the file path. Look for file paths, URLs, or filenames. Default to ""/data/raster.tif""
- DatasetName: Extract or generate a dataset name. If not specified, derive from path or default to ""raster-dataset""

Return ONLY the JSON object, no additional text.";

        try
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(request);

            var jsonResponse = await InvokeChatCompletionAsync(chatHistory, cancellationToken);
            _logger.LogDebug("LLM extraction response: {Response}", jsonResponse);

            var extractedData = JsonSerializer.Deserialize<MetadataParameterExtraction>(jsonResponse, _jsonOptions);

            if (extractedData == null)
            {
                _logger.LogWarning("Failed to parse metadata parameters, using defaults");
                return GetDefaultMetadataRequest();
            }

            var metadataRequest = new MetadataRequest(
                DatasetPath: extractedData.DatasetPath ?? "/data/raster.tif",
                DatasetName: extractedData.DatasetName ?? "raster-dataset"
            );

            _logger.LogInformation(
                "Extracted metadata parameters: {Path} -> {Name}",
                metadataRequest.DatasetPath,
                metadataRequest.DatasetName);

            return metadataRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting metadata parameters, returning defaults");
            return GetDefaultMetadataRequest();
        }
    }

    /// <summary>
    /// Extracts GitOps parameters from a natural language request.
    /// </summary>
    public async Task<GitOpsRequest> ExtractGitOpsParametersAsync(
        string request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting GitOps parameters from request");

        var systemPrompt = @"You are a parameter extraction assistant for GitOps configuration workflows.
Extract the following parameters from the user's request and return them as a JSON object:

{
  ""repoUrl"": ""<Git repository URL>"",
  ""branch"": ""<branch name>"",
  ""configPath"": ""<path to config files in repo>""
}

Rules:
- RepoUrl: Extract Git repository URL. Default to ""https://github.com/example/honua-config""
- Branch: Extract branch name, default to ""main""
- ConfigPath: Extract path to config files, default to ""deployments/production""

Return ONLY the JSON object, no additional text.";

        try
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(request);

            var jsonResponse = await InvokeChatCompletionAsync(chatHistory, cancellationToken);
            _logger.LogDebug("LLM extraction response: {Response}", jsonResponse);

            var extractedData = JsonSerializer.Deserialize<GitOpsParameterExtraction>(jsonResponse, _jsonOptions);

            if (extractedData == null)
            {
                _logger.LogWarning("Failed to parse GitOps parameters, using defaults");
                return GetDefaultGitOpsRequest();
            }

            var gitOpsRequest = new GitOpsRequest(
                RepoUrl: extractedData.RepoUrl ?? "https://github.com/example/honua-config",
                Branch: extractedData.Branch ?? "main",
                ConfigPath: extractedData.ConfigPath ?? "deployments/production"
            );

            _logger.LogInformation(
                "Extracted GitOps parameters: {Repo} ({Branch}) @ {Path}",
                gitOpsRequest.RepoUrl,
                gitOpsRequest.Branch,
                gitOpsRequest.ConfigPath);

            return gitOpsRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting GitOps parameters, returning defaults");
            return GetDefaultGitOpsRequest();
        }
    }

    /// <summary>
    /// Extracts benchmark parameters from a natural language request.
    /// </summary>
    public async Task<BenchmarkRequest> ExtractBenchmarkParametersAsync(
        string request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting benchmark parameters from request");

        var systemPrompt = @"You are a parameter extraction assistant for performance benchmark workflows.
Extract the following parameters from the user's request and return them as a JSON object:

{
  ""benchmarkName"": ""<name for the benchmark>"",
  ""targetEndpoint"": ""<URL of the endpoint to benchmark>"",
  ""concurrency"": <number of concurrent users/requests>,
  ""duration"": <duration in seconds>
}

Rules:
- BenchmarkName: Extract or generate a name, default to ""honua-load-test""
- TargetEndpoint: Extract URL, default to ""https://api.honua.io""
- Concurrency: Extract number of concurrent users/requests, default to 100
- Duration: Extract duration in seconds, default to 300 (5 minutes)

Return ONLY the JSON object, no additional text.";

        try
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(request);

            var jsonResponse = await InvokeChatCompletionAsync(chatHistory, cancellationToken);
            _logger.LogDebug("LLM extraction response: {Response}", jsonResponse);

            var extractedData = JsonSerializer.Deserialize<BenchmarkParameterExtraction>(jsonResponse, _jsonOptions);

            if (extractedData == null)
            {
                _logger.LogWarning("Failed to parse benchmark parameters, using defaults");
                return GetDefaultBenchmarkRequest();
            }

            var benchmarkRequest = new BenchmarkRequest(
                BenchmarkName: extractedData.BenchmarkName ?? "honua-load-test",
                TargetEndpoint: extractedData.TargetEndpoint ?? "https://api.honua.io",
                Concurrency: extractedData.Concurrency ?? 100,
                Duration: extractedData.Duration ?? 300
            );

            _logger.LogInformation(
                "Extracted benchmark parameters: {Name} -> {Endpoint} ({Concurrency} users, {Duration}s)",
                benchmarkRequest.BenchmarkName,
                benchmarkRequest.TargetEndpoint,
                benchmarkRequest.Concurrency,
                benchmarkRequest.Duration);

            return benchmarkRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting benchmark parameters, returning defaults");
            return GetDefaultBenchmarkRequest();
        }
    }

    // Default request factory methods
    private static DeploymentRequest GetDefaultDeploymentRequest() => new(
        CloudProvider: "AWS",
        Region: "us-east-1",
        DeploymentName: "honua-deployment",
        Tier: "Development",
        Features: new List<string> { "GeoServer", "PostGIS", "VectorTiles" },
        WorkloadProfile: "api-standard",
        ConcurrentUsers: 250,
        DataVolumeGb: 500,
        Sizing: null,
        GuardrailJustification: null,
        ExistingInfrastructure: ExistingInfrastructurePreference.Default
    );

    private static UpgradeRequest GetDefaultUpgradeRequest() => new(
        DeploymentName: "honua-deployment",
        TargetVersion: "latest"
    );

    private static MetadataRequest GetDefaultMetadataRequest() => new(
        DatasetPath: "/data/raster.tif",
        DatasetName: "raster-dataset"
    );

    private static GitOpsRequest GetDefaultGitOpsRequest() => new(
        RepoUrl: "https://github.com/example/honua-config",
        Branch: "main",
        ConfigPath: "deployments/production"
    );

    private static BenchmarkRequest GetDefaultBenchmarkRequest() => new(
        BenchmarkName: "honua-load-test",
        TargetEndpoint: "https://api.honua.io",
        Concurrency: 100,
        Duration: 300
    );

    /// <summary>
    /// Extracts certificate renewal parameters from a natural language request.
    /// </summary>
    public async Task<CertificateRenewalRequest> ExtractCertificateRenewalParametersAsync(
        string request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting certificate renewal parameters from request");

        var systemPrompt = @"You are a parameter extraction assistant for TLS/SSL certificate renewal workflows.
Extract the following parameters from the user's request and return them as a JSON object:

{
  ""domainNames"": [""<list of domain names>""],
  ""certificateProvider"": ""LetsEncrypt"" | ""ACM"" | ""ZeroSSL"",
  ""validationMethod"": ""DNS-01"" | ""HTTP-01"",
  ""checkWindowDays"": <number of days>
}

Rules:
- DomainNames: Extract all domain names mentioned. Must be a non-empty array.
- CertificateProvider: Extract provider, default to ""LetsEncrypt""
- ValidationMethod: Extract validation method, default to ""DNS-01""
- CheckWindowDays: Extract check window (days before expiry), default to 30

Return ONLY the JSON object, no additional text.";

        try
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(request);

            var jsonResponse = await InvokeChatCompletionAsync(chatHistory, cancellationToken);
            _logger.LogDebug("LLM extraction response: {Response}", jsonResponse);

            var extractedData = JsonSerializer.Deserialize<CertificateRenewalParameterExtraction>(jsonResponse, _jsonOptions);

            if (extractedData == null || extractedData.DomainNames == null || !extractedData.DomainNames.Any())
            {
                _logger.LogWarning("Failed to parse certificate renewal parameters, using defaults");
                return GetDefaultCertificateRenewalRequest();
            }

            var certRenewalRequest = new CertificateRenewalRequest(
                DomainNames: extractedData.DomainNames,
                CertificateProvider: extractedData.CertificateProvider ?? "LetsEncrypt",
                ValidationMethod: extractedData.ValidationMethod ?? "DNS-01",
                CheckWindowDays: extractedData.CheckWindowDays ?? 30
            );

            _logger.LogInformation(
                "Extracted certificate renewal parameters: {DomainCount} domains, {Provider}, {ValidationMethod}, {CheckWindowDays} days",
                certRenewalRequest.DomainNames.Count,
                certRenewalRequest.CertificateProvider,
                certRenewalRequest.ValidationMethod,
                certRenewalRequest.CheckWindowDays);

            return certRenewalRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting certificate renewal parameters, returning defaults");
            return GetDefaultCertificateRenewalRequest();
        }
    }

    private static CertificateRenewalRequest GetDefaultCertificateRenewalRequest() => new(
        DomainNames: new List<string> { "api.honua.io", "*.honua.io" },
        CertificateProvider: "LetsEncrypt",
        ValidationMethod: "DNS-01",
        CheckWindowDays: 30
    );

    /// <summary>
    /// Extracts network diagnostics parameters from a natural language request.
    /// </summary>
    public async Task<NetworkDiagnosticsRequest> ExtractNetworkDiagnosticsParametersAsync(
        string request,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting network diagnostics parameters from request");

        var systemPrompt = @"You are a parameter extraction assistant for network diagnostics workflows.
Extract the following parameters from the user's request and return them as a JSON object:

{
  ""reportedIssue"": ""<description of the network issue>"",
  ""targetHost"": ""<hostname or IP address>"",
  ""targetPort"": <port number or null>,
  ""affectedEndpoints"": [""<list of affected endpoints>""]
}

Rules:
- ReportedIssue: Extract the main issue description from the request
- TargetHost: Extract hostname/domain/IP. Look for URLs, domain names, or IP addresses
- TargetPort: Extract port number if specified (e.g., 443, 80, 8080). Set to null if not specified
- AffectedEndpoints: Extract any additional affected endpoints mentioned. Can be an empty array

Return ONLY the JSON object, no additional text.";

        try
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddSystemMessage(systemPrompt);
            chatHistory.AddUserMessage(request);

            var jsonResponse = await InvokeChatCompletionAsync(chatHistory, cancellationToken);
            _logger.LogDebug("LLM extraction response: {Response}", jsonResponse);

            var extractedData = JsonSerializer.Deserialize<NetworkDiagnosticsParameterExtraction>(jsonResponse, _jsonOptions);

            if (extractedData == null || string.IsNullOrWhiteSpace(extractedData.TargetHost))
            {
                _logger.LogWarning("Failed to parse network diagnostics parameters, using defaults");
                return GetDefaultNetworkDiagnosticsRequest();
            }

            var networkDiagnosticsRequest = new NetworkDiagnosticsRequest(
                ReportedIssue: extractedData.ReportedIssue ?? "Network connectivity issue",
                TargetHost: extractedData.TargetHost,
                TargetPort: extractedData.TargetPort,
                AffectedEndpoints: extractedData.AffectedEndpoints
            );

            _logger.LogInformation(
                "Extracted network diagnostics parameters: {Host}:{Port}, Issue: {Issue}",
                networkDiagnosticsRequest.TargetHost,
                networkDiagnosticsRequest.TargetPort ?? 0,
                networkDiagnosticsRequest.ReportedIssue);

            return networkDiagnosticsRequest;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting network diagnostics parameters, returning defaults");
            return GetDefaultNetworkDiagnosticsRequest();
        }
    }

    private static NetworkDiagnosticsRequest GetDefaultNetworkDiagnosticsRequest() => new(
        ReportedIssue: "Network connectivity issue",
        TargetHost: "api.honua.io",
        TargetPort: 443,
        AffectedEndpoints: new List<string>()
    );
}

// Internal classes for JSON deserialization
internal class DeploymentParameterExtraction
{
    public string? CloudProvider { get; set; }
    public string? Region { get; set; }
    public string? DeploymentName { get; set; }
    public string? Tier { get; set; }
    public List<string>? Features { get; set; }
    public string? WorkloadProfile { get; set; }
    public int? ConcurrentUsers { get; set; }
    public int? DataVolumeGb { get; set; }
    public string? GuardrailJustification { get; set; }
    public DeploymentSizingExtraction? Sizing { get; set; }
    public bool? ReuseExistingNetwork { get; set; }
    public bool? ReuseExistingDatabase { get; set; }
    public bool? ReuseExistingDns { get; set; }
    public string? ExistingNetworkId { get; set; }
    public string? ExistingDatabaseId { get; set; }
    public string? ExistingDnsZoneId { get; set; }
    public string? NetworkNotes { get; set; }
    public string? DatabaseNotes { get; set; }
    public string? DnsNotes { get; set; }
}

internal class DeploymentSizingExtraction
{
    public decimal? RequestedVCpu { get; set; }
    public decimal? RequestedMemoryGb { get; set; }
    public decimal? RequestedEphemeralStorageGb { get; set; }
    public int? RequestedMinInstances { get; set; }
    public int? RequestedProvisionedConcurrency { get; set; }
}

internal class UpgradeParameterExtraction
{
    public string? DeploymentName { get; set; }
    public string? TargetVersion { get; set; }
}

internal class MetadataParameterExtraction
{
    public string? DatasetPath { get; set; }
    public string? DatasetName { get; set; }
}

internal class GitOpsParameterExtraction
{
    public string? RepoUrl { get; set; }
    public string? Branch { get; set; }
    public string? ConfigPath { get; set; }
}

internal class BenchmarkParameterExtraction
{
    public string? BenchmarkName { get; set; }
    public string? TargetEndpoint { get; set; }
    public int? Concurrency { get; set; }
    public int? Duration { get; set; }
}

internal class CertificateRenewalParameterExtraction
{
    public List<string>? DomainNames { get; set; }
    public string? CertificateProvider { get; set; }
    public string? ValidationMethod { get; set; }
    public int? CheckWindowDays { get; set; }
}

internal class NetworkDiagnosticsParameterExtraction
{
    public string? ReportedIssue { get; set; }
    public string? TargetHost { get; set; }
    public int? TargetPort { get; set; }
    public List<string>? AffectedEndpoints { get; set; }
}
