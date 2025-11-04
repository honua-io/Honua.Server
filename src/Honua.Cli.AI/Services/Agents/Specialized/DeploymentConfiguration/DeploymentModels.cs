// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System.Collections.Generic;

namespace Honua.Cli.AI.Services.Agents.Specialized.DeploymentConfiguration;

/// <summary>
/// Analysis result containing deployment requirements.
/// </summary>
public sealed class DeploymentAnalysis
{
    public DeploymentType DeploymentType { get; set; }
    public string TargetEnvironment { get; set; } = "development";
    public List<string> RequiredServices { get; set; } = new();
    public InfrastructureRequirements InfrastructureNeeds { get; set; } = new();
    public int Port { get; set; } = 5000; // Default port
}

/// <summary>
/// Infrastructure requirements for deployment.
/// </summary>
public sealed class InfrastructureRequirements
{
    public bool NeedsDatabase { get; set; }
    public string? DatabaseType { get; set; }
    public bool NeedsCache { get; set; }
    public string? CacheType { get; set; }
    public bool NeedsLoadBalancer { get; set; }
    public bool NeedsMessageQueue { get; set; }
    public bool NeedsObservability { get; set; }
    public string? ObservabilityStack { get; set; }
}

/// <summary>
/// Supported deployment types.
/// </summary>
public enum DeploymentType
{
    DockerCompose,
    Kubernetes,
    TerraformAWS,
    TerraformAzure,
    TerraformGCP,
    AWSLambda,
    AzureFunctions,
    GCPCloudFunctions
}

/// <summary>
/// Generated deployment configuration.
/// </summary>
public sealed class DeploymentConfiguration
{
    public string Type { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public string Summary { get; set; } = string.Empty;
}

/// <summary>
/// Result of configuration validation.
/// </summary>
public sealed class ValidationResult
{
    public bool IsValid { get; set; }
    public string? ErrorMessage { get; set; }
}
