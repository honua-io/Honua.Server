// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
using System;
using System.Collections.Generic;

#nullable enable

namespace Honua.Server.Host.GeoservicesREST.Services;

/// <summary>
/// Result of executing edit operations.
/// </summary>
public sealed record GeoservicesEditExecutionResult(
    IReadOnlyList<object> AddResults,
    IReadOnlyList<object> UpdateResults,
    IReadOnlyList<object> DeleteResults,
    bool HasOperations,
    bool ReturnsEditMoment,
    DateTimeOffset? EditMoment);
