// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System.ComponentModel.DataAnnotations;

namespace Honua.Integration.Azure.Configuration;

/// <summary>
/// Configuration options for Azure Digital Twins integration.
/// </summary>
public sealed class AzureDigitalTwinsOptions
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "AzureDigitalTwins";

    /// <summary>
    /// Azure Digital Twins instance URL (e.g., https://{instance-name}.api.{region}.digitaltwins.azure.net).
    /// </summary>
    [Required]
    public string InstanceUrl { get; set; } = string.Empty;

    /// <summary>
    /// Whether to use Managed Identity for authentication.
    /// If false, will use connection string or default Azure credentials.
    /// </summary>
    public bool UseManagedIdentity { get; set; } = true;

    /// <summary>
    /// Azure AD Tenant ID (optional, for service principal authentication).
    /// </summary>
    public string? TenantId { get; set; }

    /// <summary>
    /// Azure AD Client ID (optional, for service principal authentication).
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Azure AD Client Secret (optional, for service principal authentication).
    /// </summary>
    public string? ClientSecret { get; set; }

    /// <summary>
    /// Synchronization configuration.
    /// </summary>
    public SyncOptions Sync { get; set; } = new();

    /// <summary>
    /// Event Grid configuration for event-driven sync.
    /// </summary>
    public EventGridOptions EventGrid { get; set; } = new();

    /// <summary>
    /// Layer to model mapping rules.
    /// </summary>
    public List<LayerModelMapping> LayerMappings { get; set; } = new();

    /// <summary>
    /// Default DTDL namespace for generated models.
    /// </summary>
    public string DefaultNamespace { get; set; } = "dtmi:com:honua";

    /// <summary>
    /// Whether to use ETSI NGSI-LD compliant model structure.
    /// </summary>
    public bool UseNgsiLdOntology { get; set; } = true;

    /// <summary>
    /// Maximum number of twins to process in a single batch.
    /// </summary>
    public int MaxBatchSize { get; set; } = 100;

    /// <summary>
    /// Request timeout in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Maximum retry attempts for transient failures.
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Whether to enable telemetry and diagnostics.
    /// </summary>
    public bool EnableTelemetry { get; set; } = true;
}

/// <summary>
/// Synchronization configuration options.
/// </summary>
public sealed class SyncOptions
{
    /// <summary>
    /// Whether synchronization is enabled.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Sync direction: Unidirectional (Honua->ADT), Bidirectional, or ADTToHonua.
    /// </summary>
    public SyncDirection Direction { get; set; } = SyncDirection.Bidirectional;

    /// <summary>
    /// Conflict resolution strategy.
    /// </summary>
    public ConflictResolution ConflictStrategy { get; set; } = ConflictResolution.LastWriteWins;

    /// <summary>
    /// Whether to enable real-time sync via Event Grid.
    /// </summary>
    public bool EnableRealTimeSync { get; set; } = true;

    /// <summary>
    /// Batch sync interval in minutes (0 = disabled).
    /// </summary>
    public int BatchSyncIntervalMinutes { get; set; } = 60;

    /// <summary>
    /// Whether to sync deletions.
    /// </summary>
    public bool SyncDeletions { get; set; } = true;

    /// <summary>
    /// Whether to sync relationships.
    /// </summary>
    public bool SyncRelationships { get; set; } = true;

    /// <summary>
    /// Maximum age in hours for considering a sync conflict (0 = no limit).
    /// </summary>
    public int ConflictWindowHours { get; set; } = 24;
}

/// <summary>
/// Event Grid configuration options.
/// </summary>
public sealed class EventGridOptions
{
    /// <summary>
    /// Event Grid topic endpoint for publishing Honua events.
    /// </summary>
    public string? TopicEndpoint { get; set; }

    /// <summary>
    /// Event Grid topic access key.
    /// </summary>
    public string? TopicAccessKey { get; set; }

    /// <summary>
    /// Event Grid subscription name for ADT lifecycle events.
    /// </summary>
    public string? SubscriptionName { get; set; }

    /// <summary>
    /// Event types to subscribe to from ADT.
    /// </summary>
    public List<string> SubscribedEventTypes { get; set; } = new()
    {
        "Microsoft.DigitalTwins.Twin.Create",
        "Microsoft.DigitalTwins.Twin.Update",
        "Microsoft.DigitalTwins.Twin.Delete",
        "Microsoft.DigitalTwins.Relationship.Create",
        "Microsoft.DigitalTwins.Relationship.Update",
        "Microsoft.DigitalTwins.Relationship.Delete"
    };

    /// <summary>
    /// Whether to enable dead-letter queue for failed events.
    /// </summary>
    public bool EnableDeadLetter { get; set; } = true;

    /// <summary>
    /// Maximum delivery attempts before sending to dead-letter.
    /// </summary>
    public int MaxDeliveryAttempts { get; set; } = 30;

    /// <summary>
    /// Event time-to-live in minutes.
    /// </summary>
    public int EventTtlMinutes { get; set; } = 1440; // 24 hours
}

/// <summary>
/// Mapping configuration between Honua layer and Azure Digital Twins model.
/// </summary>
public sealed class LayerModelMapping
{
    /// <summary>
    /// Honua service ID.
    /// </summary>
    [Required]
    public string ServiceId { get; set; } = string.Empty;

    /// <summary>
    /// Honua layer ID.
    /// </summary>
    [Required]
    public string LayerId { get; set; } = string.Empty;

    /// <summary>
    /// Azure Digital Twins model ID (DTMI).
    /// </summary>
    [Required]
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// Twin ID template (supports {featureId}, {layerId}, {serviceId} placeholders).
    /// </summary>
    public string TwinIdTemplate { get; set; } = "{serviceId}-{layerId}-{featureId}";

    /// <summary>
    /// Property mappings from Honua attribute names to DTDL property names.
    /// </summary>
    public Dictionary<string, string> PropertyMappings { get; set; } = new();

    /// <summary>
    /// Relationship mappings for foreign keys.
    /// </summary>
    public List<RelationshipMapping> Relationships { get; set; } = new();

    /// <summary>
    /// Whether to auto-generate DTDL model from layer schema.
    /// </summary>
    public bool AutoGenerateModel { get; set; } = true;

    /// <summary>
    /// Custom DTDL model JSON (overrides auto-generation).
    /// </summary>
    public string? CustomModelJson { get; set; }
}

/// <summary>
/// Relationship mapping configuration.
/// </summary>
public sealed class RelationshipMapping
{
    /// <summary>
    /// Honua foreign key column name.
    /// </summary>
    [Required]
    public string ForeignKeyColumn { get; set; } = string.Empty;

    /// <summary>
    /// ADT relationship name.
    /// </summary>
    [Required]
    public string RelationshipName { get; set; } = string.Empty;

    /// <summary>
    /// Target model ID (DTMI).
    /// </summary>
    [Required]
    public string TargetModelId { get; set; } = string.Empty;

    /// <summary>
    /// Target twin ID template.
    /// </summary>
    public string TargetTwinIdTemplate { get; set; } = "{targetServiceId}-{targetLayerId}-{targetFeatureId}";

    /// <summary>
    /// Relationship properties.
    /// </summary>
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// Synchronization direction.
/// </summary>
public enum SyncDirection
{
    /// <summary>
    /// Sync only from Honua to Azure Digital Twins.
    /// </summary>
    Unidirectional,

    /// <summary>
    /// Sync in both directions.
    /// </summary>
    Bidirectional,

    /// <summary>
    /// Sync only from Azure Digital Twins to Honua.
    /// </summary>
    AdtToHonua
}

/// <summary>
/// Conflict resolution strategy.
/// </summary>
public enum ConflictResolution
{
    /// <summary>
    /// Last write wins based on timestamp.
    /// </summary>
    LastWriteWins,

    /// <summary>
    /// Honua is always authoritative.
    /// </summary>
    HonuaAuthoritative,

    /// <summary>
    /// Azure Digital Twins is always authoritative.
    /// </summary>
    AdtAuthoritative,

    /// <summary>
    /// Manual conflict resolution required.
    /// </summary>
    Manual
}
