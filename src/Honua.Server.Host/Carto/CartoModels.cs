// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Http;

namespace Honua.Server.Host.Carto;

internal sealed record CartoDatasetSummary
(
    string Id,
    string Name,
    string? Description,
    string GeometryType,
    string ServiceId,
    string LayerId,
    IReadOnlyList<string> Crs,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<CartoDatasetLink> Links
);

internal sealed record CartoDatasetDetail
(
    string Id,
    string Name,
    string? Description,
    string GeometryType,
    string ServiceId,
    string LayerId,
    string? ServiceTitle,
    IReadOnlyList<string> Crs,
    IReadOnlyList<string> Keywords,
    IReadOnlyList<CartoDatasetLink> Links,
    IReadOnlyList<CartoFieldMetadata> Fields,
    long? RecordCount
);

internal sealed record CartoDatasetLink(string Rel, string Href, string? Type, string? Title);

internal sealed record CartoFieldMetadata
(
    string Name,
    string Type,
    bool Nullable,
    int? MaxLength,
    int? Precision,
    int? Scale,
    string? Alias
);

internal sealed record CartoSqlResponse
{
    [JsonPropertyName("time")]
    public double Time { get; init; }

    [JsonPropertyName("fields")]
    public IReadOnlyDictionary<string, CartoSqlFieldInfo> Fields { get; init; } = new Dictionary<string, CartoSqlFieldInfo>(StringComparer.OrdinalIgnoreCase);

    [JsonPropertyName("total_rows")]
    public long TotalRows { get; init; }

    [JsonPropertyName("rows")]
    public IReadOnlyList<IDictionary<string, object?>> Rows { get; init; } = Array.Empty<IDictionary<string, object?>>();
}

internal sealed record CartoSqlFieldInfo
(
    string Type,
    string? DbType,
    bool? Nullable,
    string? GeometryType
);

internal sealed record CartoSqlErrorResponse
(
    string Error,
    string? Detail
);

internal sealed record CartoSqlExecutionResult(CartoSqlResponse? Response, CartoSqlErrorResponse? Error, int StatusCode)
{
    public bool IsSuccess => Response is not null;

    public static CartoSqlExecutionResult Success(CartoSqlResponse response)
    {
        Guard.NotNull(response);
        return new CartoSqlExecutionResult(response, null, StatusCodes.Status200OK);
    }

    public static CartoSqlExecutionResult Failure(int statusCode, string message, string? detail = null)
    {
        if (statusCode < 400)
        {
            statusCode = StatusCodes.Status400BadRequest;
        }

        return new CartoSqlExecutionResult(null, new CartoSqlErrorResponse(message, detail), statusCode);
    }
}

internal sealed record CartoDatasetContext
(
    string DatasetId,
    string ServiceId,
    string LayerId,
    Honua.Server.Core.Metadata.ServiceDefinition Service,
    Honua.Server.Core.Metadata.LayerDefinition Layer,
    Honua.Server.Core.Catalog.CatalogLayerView LayerView
);
