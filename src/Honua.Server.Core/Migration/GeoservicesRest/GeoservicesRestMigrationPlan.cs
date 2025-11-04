// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using Honua.Server.Core.Metadata;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Migration.GeoservicesRest;

public sealed class GeoservicesRestMigrationPlan
{
    internal ServiceDocument ServiceDocument { get; init; } = null!;

    internal IReadOnlyList<LayerDocument> LayerDocuments { get; init; } = Array.Empty<LayerDocument>();

    public required string ServiceId { get; init; }

    public string? ServiceTitle { get; init; }

    public required string DataSourceId { get; init; }

    public required IReadOnlyList<GeoservicesRestLayerMigrationPlan> Layers { get; init; }

    public required Uri SourceServiceUri { get; init; }

    public string SourceServiceName { get; init; } = string.Empty;
}

public sealed class GeoservicesRestLayerMigrationPlan
{
    internal LayerDocument LayerDocument { get; init; } = null!;

    public required string LayerId { get; init; }

    public string? LayerTitle { get; init; }

    public required LayerSchemaDefinition Schema { get; init; }

    public required GeoservicesRestLayerInfo SourceLayer { get; init; }

    public required Uri SourceLayerUri { get; init; }
}

public sealed class LayerSchemaDefinition
{
    public required string ServiceId { get; init; }

    public required string LayerId { get; init; }

    public required string TableName { get; init; }

    public required string GeometryColumn { get; init; }

    public required string PrimaryKey { get; init; }

    public string? TemporalColumn { get; init; }

    public int? Srid { get; init; }

    public string? GeometryType { get; init; }

    public required IReadOnlyList<LayerFieldSchema> Fields { get; init; }
}

public sealed class LayerFieldSchema
{
    public required string Name { get; init; }

    public required string DataType { get; init; }

    public required string StorageType { get; init; }

    public bool Nullable { get; init; }

    public bool Editable { get; init; }

    public int? MaxLength { get; init; }

    public int? Precision { get; init; }

    public int? Scale { get; init; }
}
