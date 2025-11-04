// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;

using Honua.Server.Core.Utilities;
namespace Honua.Server.Core.Migration.GeoservicesRest;

public sealed record GeoservicesRestMigrationJobSnapshot(
    Guid JobId,
    string ServiceId,
    string DataSourceId,
    GeoservicesRestMigrationJobStatus Status,
    string Stage,
    double Progress,
    string? Message,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset? StartedAtUtc,
    DateTimeOffset? CompletedAtUtc);
