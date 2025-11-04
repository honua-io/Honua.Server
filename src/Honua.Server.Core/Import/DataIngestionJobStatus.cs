// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System.Text.Json.Serialization;

namespace Honua.Server.Core.Import;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DataIngestionJobStatus
{
    Queued,
    Validating,
    Importing,
    Completed,
    Failed,
    Cancelled
}
