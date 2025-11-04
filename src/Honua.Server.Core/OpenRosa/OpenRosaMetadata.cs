// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

namespace Honua.Server.Core.OpenRosa;

/// <summary>
/// OpenRosa configuration for a layer, added to LayerDefinition metadata.
/// </summary>
public sealed record OpenRosaLayerDefinition
{
    /// <summary>
    /// Whether OpenRosa mobile data collection is enabled for this layer.
    /// </summary>
    public bool Enabled { get; init; }

    /// <summary>
    /// Operational mode: "direct" (immediate publication) or "staged" (review workflow).
    /// </summary>
    public string Mode { get; init; } = "direct";

    /// <summary>
    /// Unique XForm identifier (e.g., "tree_survey_v1").
    /// </summary>
    public string? FormId { get; init; }

    /// <summary>
    /// Human-readable form title displayed in ODK Collect.
    /// </summary>
    public string? FormTitle { get; init; }

    /// <summary>
    /// Form version string (e.g., "1.0.2"). Incremented when form schema changes.
    /// </summary>
    public string FormVersion { get; init; } = "1.0.0";

    /// <summary>
    /// Staging table name for "staged" mode (e.g., "tree_inventory_submissions").
    /// If null, defaults to "{layerTableName}_submissions".
    /// </summary>
    public string? StagingTable { get; init; }

    /// <summary>
    /// Review workflow configuration (only applicable in "staged" mode).
    /// </summary>
    public OpenRosaReviewWorkflowDefinition? ReviewWorkflow { get; init; }

    /// <summary>
    /// Field-level configuration for XForm generation (labels, hints, validation, appearance).
    /// </summary>
    public IReadOnlyDictionary<string, OpenRosaFieldMappingDefinition> FieldMappings { get; init; } =
        new Dictionary<string, OpenRosaFieldMappingDefinition>();
}

/// <summary>
/// Review workflow configuration for staged submissions.
/// </summary>
public sealed record OpenRosaReviewWorkflowDefinition
{
    /// <summary>
    /// Automatically approve submissions without manual review.
    /// </summary>
    public bool AutoApprove { get; init; }

    /// <summary>
    /// Email address(es) to notify on new submission (comma-separated).
    /// </summary>
    public string? NotifyOnSubmission { get; init; }

    /// <summary>
    /// Minimum number of reviewers required to approve before promotion to production.
    /// </summary>
    public int RequiredReviewers { get; init; } = 1;

    /// <summary>
    /// Number of days to retain approved/rejected submissions before archival.
    /// </summary>
    public int? RetentionDays { get; init; }
}

/// <summary>
/// Field-specific configuration for XForm generation.
/// </summary>
public sealed record OpenRosaFieldMappingDefinition
{
    /// <summary>
    /// Field label displayed in ODK Collect.
    /// </summary>
    public string? Label { get; init; }

    /// <summary>
    /// Hint text shown below the field.
    /// </summary>
    public string? Hint { get; init; }

    /// <summary>
    /// Whether the field is required (enforced in XForm and server-side).
    /// </summary>
    public bool Required { get; init; }

    /// <summary>
    /// XForm constraint expression (e.g., ". &gt;= 0 and . &lt;= 500").
    /// </summary>
    public string? Constraint { get; init; }

    /// <summary>
    /// Constraint violation message shown to user.
    /// </summary>
    public string? ConstraintMessage { get; init; }

    /// <summary>
    /// XForm field appearance (e.g., "autocomplete", "minimal", "numbers", "signature").
    /// See: https://getodk.github.io/xforms-spec/#appearances
    /// </summary>
    public string? Appearance { get; init; }

    /// <summary>
    /// XForm input type override (e.g., "select1", "select", "decimal", "int", "geopoint").
    /// If null, inferred from field data type.
    /// </summary>
    public string? Type { get; init; }

    /// <summary>
    /// Choice list source for select fields:
    /// - Inline: { "value1": "Label 1", "value2": "Label 2" }
    /// - External CSV: "species_list.csv"
    /// - Database query: "datasource:query_name"
    /// </summary>
    public object? Choices { get; init; }

    /// <summary>
    /// Default value for the field (XForm setvalue).
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Read-only field (displayed but not editable).
    /// </summary>
    public bool ReadOnly { get; init; }

    /// <summary>
    /// Conditional visibility expression (e.g., "selected(${land_use}, 'forest')").
    /// </summary>
    public string? Relevant { get; init; }
}
