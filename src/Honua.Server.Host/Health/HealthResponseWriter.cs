// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Honua.Server.Core.Utilities;
using Honua.Server.Core.Extensions;
using Honua.Server.Host.Utilities;

namespace Honua.Server.Host.Health;

internal static class HealthResponseWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false
    };

    public static Task WriteResponse(HttpContext httpContext, HealthReport report)
    {
        Guard.NotNull(httpContext);

        Guard.NotNull(report);

        httpContext.Response.ContentType = "application/json";

        var entries = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
        foreach (var (key, value) in report.Entries)
        {
            var entry = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["status"] = value.Status.ToString(),
                ["duration"] = value.Duration
            };

            if (value.Description.HasValue())
            {
                entry["description"] = value.Description;
            }

            if (value.Exception is not null)
            {
                entry["error"] = value.Exception.Message;
            }

            foreach (var data in value.Data)
            {
                entry[data.Key] = data.Value;
            }

            entries[key] = entry;
        }

        var payload = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["status"] = report.Status.ToString(),
            ["duration"] = report.TotalDuration,
            ["entries"] = entries
        };

        return httpContext.Response.WriteAsync(JsonSerializer.Serialize(payload, JsonOptions));
    }
}
