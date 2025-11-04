// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;
using Honua.Cli.AI.Services.Discovery;
using Honua.Cli.AI.Services.Guardrails;

namespace Honua.Cli.AI.Services.Processes.State;

/// <summary>
/// State object for Deployment Process workflow.
/// Persists across step invocations for checkpointing and resume.
/// </summary>
public class DeploymentState
{
    public string DeploymentId { get; set; } = string.Empty;
    public string CloudProvider { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string DeploymentName { get; set; } = string.Empty;
    public string Tier { get; set; } = "Development"; // Development, Staging, Production
    public List<string> Features { get; set; } = new();
    public Dictionary<string, string> InfrastructureOutputs { get; set; } = new();
    public List<string> CreatedResources { get; set; } = new();
    public DateTime StartTime { get; set; }
    public string Status { get; set; } = "Pending";
    public string? InfrastructureCode { get; set; }
    public string? TerraformWorkspacePath { get; set; }
    public decimal? EstimatedMonthlyCost { get; set; }
    public bool RequiresApproval { get; set; } = true;
    public bool UserApproved { get; set; }
    public string? RejectionReason { get; set; }
    public string WorkloadProfile { get; set; } = string.Empty;
    public int? ConcurrentUsers { get; set; }
    public int? DataVolumeGb { get; set; }
    public DeploymentGuardrailDecision? GuardrailDecision { get; set; }
    public List<GuardrailAuditEntry> GuardrailHistory { get; set; } = new();
    public CloudDiscoverySnapshot? DiscoverySnapshot { get; set; }
    public ExistingInfrastructurePreference ExistingInfrastructure { get; set; } = ExistingInfrastructurePreference.Default;

    /// <summary>
    /// GCP project ID for GCP deployments. Falls back to GCP_PROJECT_ID or GOOGLE_CLOUD_PROJECT environment variables.
    /// </summary>
    public string? GcpProjectId { get; set; }

    /// <summary>
    /// API key for authenticating health checks and deployment validation requests.
    /// Retrieved from deployment secrets/configuration.
    /// </summary>
    public string? ApiKey { get; set; }

    /// <summary>
    /// Bearer token for authenticating health checks and deployment validation requests.
    /// Alternative to ApiKey for token-based authentication.
    /// </summary>
    public string? BearerToken { get; set; }

    /// <summary>
    /// DNS zone name for Route53/Azure DNS/Cloud DNS configuration.
    /// If not specified, will be derived from deployment domain.
    /// </summary>
    public string? DnsZoneName { get; set; }

    /// <summary>
    /// DNS hosted zone ID for Route53 (optional, will be looked up if not provided).
    /// </summary>
    public string? DnsHostedZoneId { get; set; }

    /// <summary>
    /// Custom domain name for the deployment. If not specified, defaults to {DeploymentName}.honua.io
    /// </summary>
    public string? CustomDomain { get; set; }
}

public record ExistingInfrastructurePreference(
    bool ReuseNetwork,
    bool ReuseDatabase,
    bool ReuseDns,
    string? ExistingNetworkId = null,
    string? ExistingDatabaseId = null,
    string? ExistingDnsZoneId = null,
    string? NetworkNotes = null,
    string? DatabaseNotes = null,
    string? DnsNotes = null)
{
    public static ExistingInfrastructurePreference Default { get; } = new(false, false, false);
}
