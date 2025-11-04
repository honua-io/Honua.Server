// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Migration.GeoservicesRest;

public sealed class GeoservicesRestServiceMigrationRequest
{
    public required Uri ServiceUri { get; init; }

    public required string TargetServiceId { get; init; }

    public required string TargetFolderId { get; init; }

    public required string TargetDataSourceId { get; init; }

    public IReadOnlyCollection<int>? LayerIds { get; init; }

    public GeoservicesRestMetadataTranslatorOptions? TranslatorOptions { get; init; }

    public string? SecurityProfileId { get; init; }
}
