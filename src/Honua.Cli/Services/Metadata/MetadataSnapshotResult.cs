// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Cli.Services.Metadata;

public sealed record MetadataSnapshotResult(
    string Label,
    string SnapshotPath,
    DateTimeOffset CreatedAtUtc,
    string? Notes);
