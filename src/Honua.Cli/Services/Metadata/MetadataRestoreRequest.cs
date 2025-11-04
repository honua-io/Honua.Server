// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Cli.Services.Metadata;

public sealed record MetadataRestoreRequest(
    string WorkspacePath,
    string Label,
    string? SnapshotsRootOverride,
    bool Overwrite);
