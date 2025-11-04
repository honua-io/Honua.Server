// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

namespace Honua.Server.Core.Import;

public sealed record DataIngestionJobSnapshot(
    Guid JobId,
    string ServiceId,
    string LayerId,
    string? SourceFileName,
    DataIngestionJobStatus Status,
    string Stage,
    double Progress,
    string? Message,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc
);
