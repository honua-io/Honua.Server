// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using Honua.Server.Core.Metadata;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Data;

public sealed record FeatureContext(
    MetadataSnapshot Snapshot,
    ServiceDefinition Service,
    LayerDefinition Layer,
    DataSourceDefinition DataSource,
    IDataStoreProvider Provider);
