// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿namespace Honua.Server.Core.Migration.GeoservicesRest;

public enum GeoservicesRestMigrationJobStatus
{
    Queued,
    Initializing,
    PreparingSchema,
    CopyingData,
    Completed,
    Failed,
    Cancelled
}
