// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Metadata.Snapshots;

public sealed record MetadataSnapshotRequest(string? Label, string? Notes);

public sealed record MetadataSnapshotDescriptor(
    string Label,
    DateTimeOffset CreatedAtUtc,
    long? SizeBytes,
    string? Notes,
    string? Checksum);

public sealed record MetadataSnapshotDetails(
    MetadataSnapshotDescriptor Descriptor,
    string Metadata);
