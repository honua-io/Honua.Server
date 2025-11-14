// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;

namespace Honua.Server.Core.Metadata;

// ISO 19115 Metadata Extension
public sealed record Iso19115Metadata
{
    public string? MetadataIdentifier { get; init; }
    public Iso19115MetadataStandard? MetadataStandard { get; init; }
    public Iso19115Contact? MetadataContact { get; init; }
    public Iso19115DateInfo? DateInfo { get; init; }
    public string? SpatialRepresentationType { get; init; } // vector, grid, tin
    public Iso19115SpatialResolution? SpatialResolution { get; init; }
    public string? Language { get; init; } // ISO 639-2 code (e.g., "eng")
    public string? CharacterSet { get; init; } // utf8, utf16, etc.
    public IReadOnlyList<string> TopicCategory { get; init; } = Array.Empty<string>(); // farming, biota, boundaries, etc.
    public Iso19115ResourceConstraints? ResourceConstraints { get; init; }
    public Iso19115Lineage? Lineage { get; init; }
    public Iso19115DataQualityInfo? DataQualityInfo { get; init; }
    public Iso19115MaintenanceInfo? MaintenanceInfo { get; init; }
    public Iso19115DistributionInfo? DistributionInfo { get; init; }
    public Iso19115ReferenceSystemInfo? ReferenceSystemInfo { get; init; }
}

public sealed record Iso19115MetadataStandard
{
    public string? Name { get; init; } // ISO 19115:2014, ISO 19115-1:2014
    public string? Version { get; init; }
}

public sealed record Iso19115Contact
{
    public string? OrganisationName { get; init; }
    public string? IndividualName { get; init; }
    public Iso19115ContactInfo? ContactInfo { get; init; }
    public string? Role { get; init; } // pointOfContact, custodian, owner, etc.
}

public sealed record Iso19115ContactInfo
{
    public string? Email { get; init; }
    public string? Phone { get; init; }
    public Iso19115Address? Address { get; init; }
    public string? OnlineResource { get; init; }
}

public sealed record Iso19115Address
{
    public string? DeliveryPoint { get; init; }
    public string? City { get; init; }
    public string? AdministrativeArea { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}

public sealed record Iso19115DateInfo
{
    public DateTimeOffset? Creation { get; init; }
    public DateTimeOffset? Publication { get; init; }
    public DateTimeOffset? Revision { get; init; }
}

public sealed record Iso19115SpatialResolution
{
    public int? EquivalentScale { get; init; } // e.g., 24000 for 1:24000
    public double? Distance { get; init; } // in meters
}

public sealed record Iso19115ResourceConstraints
{
    public IReadOnlyList<string> UseLimitation { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> AccessConstraints { get; init; } = Array.Empty<string>(); // copyright, patent, license, etc.
    public IReadOnlyList<string> UseConstraints { get; init; } = Array.Empty<string>(); // copyright, patent, otherRestrictions, etc.
    public IReadOnlyList<string> OtherConstraints { get; init; } = Array.Empty<string>();
}

public sealed record Iso19115Lineage
{
    public string? Statement { get; init; }
    public IReadOnlyList<Iso19115Source> Sources { get; init; } = Array.Empty<Iso19115Source>();
    public IReadOnlyList<Iso19115ProcessStep> ProcessSteps { get; init; } = Array.Empty<Iso19115ProcessStep>();
}

public sealed record Iso19115Source
{
    public string? Description { get; init; }
    public int? ScaleDenominator { get; init; }
}

public sealed record Iso19115ProcessStep
{
    public required string Description { get; init; }
    public DateTimeOffset? DateTime { get; init; }
}

public sealed record Iso19115DataQualityInfo
{
    public string? Scope { get; init; } // dataset, series, featureType, etc.
    public Iso19115PositionalAccuracy? PositionalAccuracy { get; init; }
    public string? Completeness { get; init; }
    public string? LogicalConsistency { get; init; }
}

public sealed record Iso19115PositionalAccuracy
{
    public double? Value { get; init; }
    public string? Unit { get; init; } // meter, feet, etc.
    public string? EvaluationMethod { get; init; }
}

public sealed record Iso19115MaintenanceInfo
{
    public string? MaintenanceFrequency { get; init; } // continual, daily, weekly, monthly, quarterly, annually, etc.
    public DateTimeOffset? NextUpdate { get; init; }
    public string? UpdateScope { get; init; } // dataset, series, etc.
}

public sealed record Iso19115DistributionInfo
{
    public Iso19115Distributor? Distributor { get; init; }
    public IReadOnlyList<Iso19115DistributionFormat> DistributionFormats { get; init; } = Array.Empty<Iso19115DistributionFormat>();
    public Iso19115TransferOptions? TransferOptions { get; init; }
}

public sealed record Iso19115Distributor
{
    public string? OrganisationName { get; init; }
    public Iso19115ContactInfo? ContactInfo { get; init; }
}

public sealed record Iso19115DistributionFormat
{
    public required string Name { get; init; } // GeoPackage, Shapefile, GeoTIFF, etc.
    public string? Version { get; init; }
}

public sealed record Iso19115TransferOptions
{
    public string? OnlineResource { get; init; }
}

public sealed record Iso19115ReferenceSystemInfo
{
    public string? Code { get; init; } // e.g., "2227"
    public string? CodeSpace { get; init; } // e.g., "EPSG"
    public string? Version { get; init; }
}

// STAC Metadata Extension
public sealed record StacMetadata
{
    public bool Enabled { get; init; } = true;
    public string? CollectionId { get; init; }
    public string? License { get; init; } // SPDX license identifier (e.g., "CC-BY-4.0", "proprietary")
    public IReadOnlyList<StacProvider> Providers { get; init; } = Array.Empty<StacProvider>();
    public IReadOnlyDictionary<string, StacAssetDefinition> Assets { get; init; } = new Dictionary<string, StacAssetDefinition>();
    public IReadOnlyDictionary<string, StacAssetDefinition> ItemAssets { get; init; } = new Dictionary<string, StacAssetDefinition>();
    public IReadOnlyDictionary<string, object> Summaries { get; init; } = new Dictionary<string, object>();
    public IReadOnlyList<string> StacExtensions { get; init; } = Array.Empty<string>();
    public string? ItemIdTemplate { get; init; } // e.g., "roads-{road_id}"
    public IReadOnlyDictionary<string, object> AdditionalProperties { get; init; } = new Dictionary<string, object>();
}

public sealed record StacProvider
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>(); // producer, licensor, processor, host
    public string? Url { get; init; }
}

public sealed record StacAssetDefinition
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public required string Type { get; init; } // MIME type
    public IReadOnlyList<string> Roles { get; init; } = Array.Empty<string>(); // data, metadata, thumbnail
    public string? Href { get; init; }
    public IReadOnlyDictionary<string, object> AdditionalProperties { get; init; } = new Dictionary<string, object>();
}
