// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Migration.GeoservicesRest;

public sealed class GeoservicesRestMetadataTranslatorOptions
{
    public string? ServiceTitle { get; init; }

    public string? ServiceDescription { get; init; }

    public string? TableNamePrefix { get; init; }

    public string GeometryColumnName { get; init; } = "shape";

    public string? LayerIdPrefix { get; init; }

    public string? DefaultKeywordList { get; init; }

    public bool UseLayerIdsForTables { get; init; }
}
