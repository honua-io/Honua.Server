// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Honua.Server.Core.Metadata;

public sealed record LayerEditingDefinition
{
    public static LayerEditingDefinition Disabled { get; } = new()
    {
        Capabilities = LayerEditCapabilitiesDefinition.Disabled,
        Constraints = LayerEditConstraintDefinition.Empty
    };

    public LayerEditCapabilitiesDefinition Capabilities { get; init; } = LayerEditCapabilitiesDefinition.Disabled;
    public LayerEditConstraintDefinition Constraints { get; init; } = LayerEditConstraintDefinition.Empty;
}

public sealed record LayerEditCapabilitiesDefinition
{
    public static LayerEditCapabilitiesDefinition Disabled { get; } = new();

    public bool AllowAdd { get; init; }
    public bool AllowUpdate { get; init; }
    public bool AllowDelete { get; init; }
    public bool RequireAuthentication { get; init; } = true;
    public IReadOnlyList<string> AllowedRoles { get; init; } = Array.Empty<string>();
}

public sealed record LayerEditConstraintDefinition
{
    private static readonly IReadOnlyDictionary<string, string?> EmptyDefaults = new ReadOnlyDictionary<string, string?>(new Dictionary<string, string?>());

    public static LayerEditConstraintDefinition Empty { get; } = new();

    public IReadOnlyList<string> ImmutableFields { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> RequiredFields { get; init; } = Array.Empty<string>();
    public IReadOnlyDictionary<string, string?> DefaultValues { get; init; } = EmptyDefaults;
}

public sealed record LayerAttachmentDefinition
{
    public static LayerAttachmentDefinition Disabled { get; } = new();

    public bool Enabled { get; init; }
    public string? StorageProfileId { get; init; }
    public int? MaxSizeMiB { get; init; }
    public IReadOnlyList<string> AllowedContentTypes { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> DisallowedContentTypes { get; init; } = Array.Empty<string>();
    public bool RequireGlobalIds { get; init; }
    public bool ReturnPresignedUrls { get; init; }
    public bool ExposeOgcLinks { get; init; }
}
