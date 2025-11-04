// Copyright (c) 2025 HonuaIO
// Licensed under the Elastic License 2.0. See LICENSE file in the project root for full license information.
﻿using System;
using Honua.Server.Core.Utilities;
using Honua.Server.Host.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;

namespace Honua.Server.Host.Carto;

internal static class CartoEndpointExtensions
{
    public static IEndpointRouteBuilder MapCartoApi(this IEndpointRouteBuilder endpoints)
    {
        Guard.NotNull(endpoints);

        var group = endpoints.MapGroup("/carto");
        group.RequireAuthorization("RequireViewer");

        group.MapGet(string.Empty, CartoHandlers.GetLanding);

        var apiGroup = group.MapGroup("/api/v3");
        apiGroup.MapGet("/datasets", CartoHandlers.GetDatasets);
        apiGroup.MapGet("/datasets/{datasetId}", CartoHandlers.GetDataset);
        apiGroup.MapGet("/datasets/{datasetId}/schema", CartoHandlers.GetDatasetSchema);
        apiGroup.MapGet("/sql", CartoHandlers.ExecuteSqlGet);
        apiGroup.MapPost("/sql", CartoHandlers.ExecuteSqlPost);

        group.MapGet("/api/v2/sql", CartoHandlers.ExecuteSqlLegacy);

        return endpoints;
    }
}
